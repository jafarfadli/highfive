using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Builds the desktop players the team plays on laptops: macOS (one app for Apple Silicon and Intel Macs)
    /// and Windows (64-bit), into Builds/ (git-ignored). Each lands in its own folder with a note on opening an
    /// unsigned build and, built on a Mac, is zipped for sharing. From a terminal:
    /// Unity -batchmode -quit -projectPath . -executeMethod SeoulFirstWeek.EditorTools.PlayerBuilds.BuildAll
    /// The scenes are built as they are on disk, so run Build All Scenes first after changing the builder.
    /// </summary>
    public static class PlayerBuilds
    {
        const string Root = "Builds";
        const string AppName = "Ready Set Go";
        const string Secrets = "Assets/StreamingAssets/secrets.json";
        const string WhisperModel = "Assets/StreamingAssets/Whisper/ggml-base-q5_1.bin";

        [MenuItem("Seoul First Week/Build Players/macOS and Windows")]
        public static void BuildAll()
        {
            BuildMac();
            BuildWindows();
        }

        [MenuItem("Seoul First Week/Build Players/macOS")]
        public static void BuildMac()
        {
            string folder = Prepare("ReadySetGo-macOS");
            string app = Path.Combine(folder, AppName + ".app");
            SetMacArchitecture("x64ARM64");
            Build(BuildTarget.StandaloneOSX, app);
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                CheckMicrophoneUsage(app);
                Run("/usr/bin/codesign", $"--verify --deep --strict \"{app}\"");
            }
            Finish(folder, MacNote, appBundle: true);
        }

        [MenuItem("Seoul First Week/Build Players/Windows")]
        public static void BuildWindows()
        {
            string folder = Prepare("ReadySetGo-Windows");
            Build(BuildTarget.StandaloneWindows64, Path.Combine(folder, AppName + ".exe"));
            // the same player starts in a headset when launched with -vr (Core.VRMode)
            File.WriteAllText(Path.Combine(folder, AppName + " (VR).bat"),
                $"@echo off\r\nstart \"\" \"%~dp0{AppName}.exe\" -vr\r\n");
            Finish(folder, WindowsNote, appBundle: false);
        }

        static string Prepare(string name)
        {
            // StreamingAssets ships as it is, so a local OpenAI key would go out with every copy of the build
            if (File.Exists(Secrets))
                throw new InvalidOperationException($"{Secrets} would ship inside the build: move it out of the project, build, then put it back.");
            if (!File.Exists(WhisperModel))
                Debug.LogWarning($"[Players] {WhisperModel} is missing (run Tools/whisper/get_model.sh), so this build can't hear answers said out loud.");
            // OpenXR for the headset, never the Mock HMD a play-test may have left assigned
            VRSetup.Configure();
            if (VRSetup.MockHmdAssigned())
                throw new InvalidOperationException("The Mock HMD loader is still assigned to Standalone: a build would start a fake headset.");
            string folder = Path.GetFullPath(Path.Combine(Root, name));
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            return folder;
        }

        static void Build(BuildTarget target, string location)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No scenes in the build settings: run Seoul First Week → Build All Scenes first.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.CompressWithLz4HC,
            });
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"[Players] The {target} build ended {summary.result} with {summary.totalErrors} errors (see the Console).");
            Debug.Log($"[Players] {target}: {summary.totalSize / 1048576f:0} MB in {summary.totalTime.TotalMinutes:0.0} min, {location}");
        }

        // The setting lives in the Mac build support module, so it is looked up by name: this file must
        // still compile in an editor without that module.
        static void SetMacArchitecture(string value)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings")).FirstOrDefault(t => t != null);
            var property = type?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                Debug.LogWarning("[Players] The macOS architecture setting wasn't found; building the editor's default.");
                return;
            }
            property.SetValue(null, Enum.Parse(property.PropertyType, value));
        }

        // Unity writes Player Settings' microphone usage text into Info.plist. Without it macOS never asks for
        // the microphone, and answering by voice stays silent.
        static void CheckMicrophoneUsage(string app)
        {
            string plist = Path.Combine(app, "Contents", "Info.plist");
            if (Run("/usr/bin/plutil", $"-extract NSMicrophoneUsageDescription raw -o - \"{plist}\"", check: false).code != 0)
                Debug.LogWarning("[Players] Info.plist has no microphone usage text (Player Settings > Microphone Usage Description), so macOS won't let the game hear answers.");
        }

        static void Finish(string folder, string note, bool appBundle)
        {
            // Burst's debug symbols land next to the player; as the name says, they are not for players
            foreach (var dir in Directory.GetDirectories(folder, "*_DoNotShip")) Directory.Delete(dir, true);
            File.WriteAllText(Path.Combine(folder, "How to open.txt"), note.Replace("\r\n", "\n"));
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.Log($"[Players] Zip {folder} to share it.");
                return;
            }
            // ditto keeps the app bundle's permissions, links and signature, which a plain zip would lose
            string zip = folder + ".zip";
            if (File.Exists(zip)) File.Delete(zip);
            Run("/usr/bin/ditto", $"-c -k {(appBundle ? "" : "--norsrc ")}--keepParent \"{folder}\" \"{zip}\"");
            Debug.Log($"[Players] {Path.GetFileName(zip)}: {new FileInfo(zip).Length / 1048576f:0} MB");
        }

        static (int code, string output) Run(string file, string arguments, bool check = true)
        {
            var info = new ProcessStartInfo(file, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(info);
            var error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            output += error.Result;
            if (check && process.ExitCode != 0)
                throw new InvalidOperationException($"{Path.GetFileName(file)} {arguments} failed ({process.ExitCode}): {output}");
            return (process.ExitCode, output);
        }

        const string Controls =
@"Controls: WASD or the arrow keys walk, Shift runs, hold the left mouse button and drag to look around,
E opens doors and talks, 1-4 pick an answer, hold Space to say it, R hears the line again, Esc pauses.

Kontrol: WASD atau tombol panah untuk jalan, Shift untuk lari, tahan klik kiri lalu geser untuk melihat
sekeliling, E untuk membuka pintu dan bicara, 1-4 untuk memilih jawaban, tahan Space untuk mengucapkannya,
R untuk mendengar lagi, Esc untuk jeda.
";

        const string MacNote =
@"Ready Set Go! for macOS (Apple Silicon and Intel, macOS 12 or later)

1. Unzip and drag ""Ready Set Go.app"" to Applications.
2. The build isn't signed by Apple, so macOS blocks it the first time.
   macOS 15 or later: open the app once and click Done, then open System Settings > Privacy & Security,
   scroll down and click ""Open Anyway"" next to Ready Set Go.
   macOS 14 or earlier: right-click (Control-click) the app, choose Open, then Open again.
   If macOS says the app ""is damaged"", run this once in Terminal:
       xattr -dr com.apple.quarantine ""/Applications/Ready Set Go.app""
3. macOS asks for the microphone as soon as the game starts, and the game appears once you answer.
   Allow it to answer by voice; without it, click an answer or press 1-4.

1. Unzip lalu seret ""Ready Set Go.app"" ke Applications.
2. Build ini tidak ditandatangani Apple, jadi macOS memblokirnya saat pertama dibuka.
   macOS 15 ke atas: buka app sekali dan klik Done, lalu buka System Settings > Privacy & Security,
   gulir ke bawah dan klik ""Open Anyway"" di samping Ready Set Go.
   macOS 14 ke bawah: klik kanan (Control-klik) app, pilih Open, lalu Open lagi.
   Kalau macOS bilang app-nya ""is damaged"", jalankan ini sekali di Terminal:
       xattr -dr com.apple.quarantine ""/Applications/Ready Set Go.app""
3. macOS langsung meminta izin mikrofon saat game dibuka, dan game baru muncul setelah kamu menjawab.
   Pilih Allow untuk menjawab dengan suara; tanpa mikrofon, klik jawaban atau tekan 1-4.

" + Controls;

        const string WindowsNote =
@"Ready Set Go! for Windows (Windows 10 or 11, 64-bit)

1. Extract the whole folder first (right-click the zip > Extract All). The game doesn't run from inside the zip.
2. Open ""Ready Set Go.exe"". The build isn't signed, so Windows may say ""Windows protected your PC"":
   click ""More info"", then ""Run anyway"".
3. To answer by voice, let desktop apps use the microphone (Settings > Privacy & security > Microphone).
   Without it, click an answer or press 1-4. If the game never hears you, install the Microsoft Visual C++
   Redistributable (x64) from https://aka.ms/vs/17/release/vc_redist.x64.exe and update the graphics driver.

1. Ekstrak dulu seluruh folder (klik kanan zip > Extract All). Game tidak bisa jalan dari dalam zip.
2. Buka ""Ready Set Go.exe"". Build ini tidak ditandatangani, jadi Windows bisa menampilkan
   ""Windows protected your PC"": klik ""More info"", lalu ""Run anyway"".
3. Untuk menjawab dengan suara, izinkan aplikasi desktop memakai mikrofon (Settings > Privacy & security >
   Microphone). Tanpa mikrofon, klik jawaban atau tekan 1-4. Kalau game tidak pernah mendengar suaramu, pasang
   Microsoft Visual C++ Redistributable (x64) dari https://aka.ms/vs/17/release/vc_redist.x64.exe dan perbarui
   driver kartu grafis.

" + Controls + @"
Playing in VR (Meta Quest 2 or 3 over Quest Link, on a PC with a gaming graphics card):
1. Install the Meta Quest Link app on this PC (https://www.meta.com/quest/setup/), sign in, and connect the
   headset with a USB-C cable (or Air Link on 5 GHz Wi-Fi).
2. In the Link app, Settings > General: turn on ""Unknown sources"" and set Meta Quest Link as the active
   OpenXR runtime.
3. Put the headset on and start Quest Link from its Quick Settings.
4. Open ""Ready Set Go (VR).bat"". The game starts in the headset (without one it plays on the screen).
VR controls: the left stick walks (click it to walk faster), the right stick turns 45 degrees, point with
the right controller and pull its trigger to choose or to open a door, hold A and say the answer, B hears the
line again, the left Menu button pauses.

Main di VR (Meta Quest 2 atau 3 lewat Quest Link, di PC dengan kartu grafis gaming):
1. Pasang aplikasi Meta Quest Link di PC ini (https://www.meta.com/quest/setup/), login, lalu sambungkan
   headset dengan kabel USB-C (atau Air Link lewat Wi-Fi 5 GHz).
2. Di aplikasi Link, Settings > General: nyalakan ""Unknown sources"" dan jadikan Meta Quest Link sebagai
   OpenXR runtime yang aktif.
3. Pakai headset, lalu mulai Quest Link dari Quick Settings.
4. Buka ""Ready Set Go (VR).bat"". Game muncul di headset (tanpa headset, game jalan di layar).
Kontrol VR: stik kiri untuk jalan (klik stiknya untuk lebih cepat), stik kanan untuk berbelok 45 derajat,
tunjuk dengan controller kanan lalu tarik trigger untuk memilih atau membuka pintu, tahan A lalu ucapkan
jawabannya, B untuk mendengar lagi, tombol Menu kiri untuk jeda.
";
    }
}
