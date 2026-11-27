using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// An unattended play-test of the whole game, for the command line or the menu: it rebuilds every scene (or only
    /// plays), says every answer of every procedure with the player's baked voice to measure how often answering by
    /// voice picks the right answer, photographs a spoken pick, then plays parts through with one wrong answer and
    /// one wrong turn per step, photographing every step. Results go to Recordings/Playtest (git-ignored): log.txt,
    /// playlog.txt, speech-report.txt and the PNGs. The player's progress and the telemetry log are restored after.
    /// From a terminal (the editor quits when done):
    /// <c>Unity -projectPath . -executeMethod SeoulFirstWeek.EditorTools.AutoPlaytest.Run</c> (build, then play),
    /// <c>…AutoPlaytest.RunPlayOnly</c> (play the scenes as built).
    /// </summary>
    [InitializeOnLoad]
    public static class AutoPlaytest
    {
        const string Stage = "autoPlaytest.stage";
        public static string Out = Path.GetFullPath("Recordings/Playtest");
        // "level#n" picks situation n on a personalised visit's story card
        // every part, and every option of a personalised part at least once ("level#a.b": option a of the first
        // question, option b of the second); the order matters (the residence card opens the bank, …)
        static string[] LevelsToPlay =
        {
            "clinic-1#0.0", "clinic-1#1.1", "clinic-2", "immigration-1", "immigration-2", "bank-1", "bank-2",
            "airport-1#0", "airport-1#1", "airport-2", "subway-1#0", "subway-1#1", "subway-2",
            "emergency-1", "emergency-2#0", "emergency-2#1",
        };
        const string QuickKey = "autoPlaytest.quick";

        static AutoPlaytest()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                int stage = SessionState.GetInt(Stage, 0);
                if (s == PlayModeStateChange.EnteredPlayMode && stage == 1)
                {
                    SessionState.SetInt(Stage, 2);
                    EditorApplication.delayCall += Play;
                }
                else if (s == PlayModeStateChange.EnteredEditMode && stage == 2)
                {
                    SessionState.SetInt(Stage, 0);
                    // started from the command line: quit; from the menu: stay open
                    if (Application.isBatchMode || System.Environment.GetCommandLineArgs().Any(a => a == "-executeMethod")) EditorApplication.Exit(0);
                }
            };
        }

        [MenuItem("Seoul First Week/Play-test/Build and Play Everything")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText(Path.Combine(Out, "log.txt"), "");
            try
            {
                SeoulSceneBuilder.BuildAll();
                Note("built");
            }
            catch (System.Exception e)
            {
                Note("BUILD FAILED " + e);
                EditorApplication.Exit(1);
                return;
            }
            RunPlayOnly();
        }

        /// <summary>Rebuilds only the scenes that changed (the 119 street, the airport), then plays the clinic in both situations and the changed parts.</summary>
        public static void RunQuick()
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText(Path.Combine(Out, "log.txt"), "");
            try
            {
                SeoulSceneBuilder.BuildStreet119();
                SeoulSceneBuilder.BuildAirport();
                SeoulSceneBuilder.BuildSubway();
                SeoulSceneBuilder.BuildER();
                Note("built quick");
            }
            catch (System.Exception e)
            {
                Note("BUILD FAILED " + e);
                EditorApplication.Exit(1);
                return;
            }
            SessionState.SetBool(QuickKey, true);
            RunPlayOnly();
        }

        [MenuItem("Seoul First Week/Play-test/Play Everything")]
        public static void RunPlayOnly()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
            SessionState.SetInt(Stage, 1);
            EditorApplication.EnterPlaymode();
        }

        static string BackupPath => Path.Combine(Out, "backup.json");
        static string OnlyPath => Path.Combine(Out, "only.txt");

        [System.Serializable]
        class Backup
        {
            public bool hasProgress, hasCsv;
            public string progress, csv;
            public List<string> sits = new(); // "level id=option ids" remembered for the Logbook
        }

        static List<string> Sits() =>
            Core.Levels.All.Where(l => PlayerPrefs.HasKey("sfw.sit." + l.id)).Select(l => l.id + "=" + PlayerPrefs.GetString("sfw.sit." + l.id)).ToList();

        static void WriteBackup(string progress, string csvBefore) =>
            File.WriteAllText(BackupPath, JsonUtility.ToJson(new Backup
            {
                hasProgress = progress != null, progress = progress ?? "", hasCsv = csvBefore != null, csv = csvBefore ?? "", sits = Sits(),
            }));

        static void RestoreBackup(string csv)
        {
            if (!File.Exists(BackupPath)) return;
            var b = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            Restore(b.hasProgress ? b.progress : null, b.hasCsv ? b.csv : null, csv, b.sits);
            File.Delete(BackupPath);
            Note("restored the progress and telemetry an interrupted run had changed");
        }

        /// <summary>Puts back the player's progress, the situations remembered for the Logbook, and the telemetry log.</summary>
        static void Restore(string progress, string csvBefore, string csv, List<string> sits)
        {
            if (progress != null) PlayerPrefs.SetString("sfw.progress", progress);
            else PlayerPrefs.DeleteKey("sfw.progress");
            foreach (var level in Core.Levels.All) PlayerPrefs.DeleteKey("sfw.sit." + level.id);
            foreach (var kv in sits ?? new List<string>())
            {
                int eq = kv.IndexOf('=');
                if (eq > 0) PlayerPrefs.SetString("sfw.sit." + kv.Substring(0, eq), kv.Substring(eq + 1));
            }
            PlayerPrefs.Save();
            if (csvBefore != null) File.WriteAllText(csv, csvBefore);
            else if (File.Exists(csv)) File.Delete(csv);
        }

        static void Note(string s) => File.AppendAllText(Path.Combine(Out, "log.txt"), s + "\n");

        static void Shot(string name)
        {
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
                if (v.profile && v.profile.TryGet<DepthOfField>(out var dof)) dof.active = false;
            if (!Camera.main) { Note("no camera for " + name); return; }
            PlaytestTools.Frames(2);
            PlaytestTools.Capture(Path.Combine(Out, name + ".png"));
            if (Core.VRMode.Active)
            {
                // the game view shows what each eye gets, side by side (VRPlaytest)
                ScreenCapture.CaptureScreenshot(Path.Combine(Out, name + "-eyes.png"));
                PlaytestTools.Frames(3);
            }
        }

        /// <summary>Stands the player a few metres from a waypoint, looking at it, for a photo of the place.</summary>
        static void LookAt(ScenarioDirector d, string id)
        {
            var wp = d.waypoints?.FirstOrDefault(w => w && w.stationId == id);
            if (!wp || !d.player) return;
            var p = d.player.transform.position;
            var dir = p - wp.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            var box = wp.GetComponent<BoxCollider>().bounds;
            var at = wp.transform.position + dir.normalized * Mathf.Max(2.8f, box.extents.magnitude + 1.2f);
            at.y = p.y;
            var look = wp.transform.position - at;
            d.player.Teleport(at, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        }

        static void Play()
        {
            // in the pretend headset every shader needs its stereo variants, which the editor would otherwise draw
            // as cyan placeholders while it compiles them in the background
            if (Core.VRMode.Active) ShaderUtil.allowAsyncCompilation = false;
            // a run of only some parts keeps the other parts' photos
            if (!File.Exists(OnlyPath))
                foreach (var f in Directory.GetFiles(Out, "*.png")) File.Delete(f);
            string csv = Path.Combine(Application.persistentDataPath, "telemetry", "levels.csv");
            // a run that was stopped half-way left the player's progress and the telemetry as it changed them: put
            // them back from its backup before taking this run's own
            RestoreBackup(csv);
            string progress = PlayerPrefs.HasKey("sfw.progress") ? PlayerPrefs.GetString("sfw.progress") : null;
            string csvBefore = File.Exists(csv) ? File.ReadAllText(csv) : null;
            var sitsBefore = Sits();
            WriteBackup(progress, csvBefore);
            // (a quick run plays the same parts after rebuilding only the scenes that changed)
            try
            {
                EditorApplication.isPaused = true;
                PlaytestTools.Frames(200);
                Shot("home");
                HomeMicCheck();
                Core.SceneFlow.Load("Select");
                PlaytestTools.WaitScene("Select");
                PlaytestTools.Frames(60);
                Shot("select-page1");
                var sel = Object.FindAnyObjectByType<LevelSelectScreen>();
                if (sel)
                {
                    sel.SendMessage("GoTo", 1, SendMessageOptions.DontRequireReceiver);
                    PlaytestTools.Frames(60);
                    Shot("select-page2");
                }
                var only = File.Exists(OnlyPath)
                    ? File.ReadAllLines(OnlyPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList()
                    : null;
                if (only != null) Note("only: " + string.Join(", ", only));
                if (only == null || only.Contains("mic"))
                {
                    try { MicPathCheck(); }
                    catch (System.Exception e) { Note($"FAIL mic: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
                }
                if (only == null || only.Contains("speech"))
                {
                    try { SpeechTest(); }
                    catch (System.Exception e) { Note($"FAIL speech: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
                    try { VoicePickShot(); }
                    catch (System.Exception e) { Note($"FAIL voiceshot: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
                }
                foreach (var id in only?.Where(l => l != "speech" && l != "mic") ?? LevelsToPlay)
                {
                    try { PlayLevel(id); }
                    catch (System.Exception e) { Note($"FAIL {id}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
                }
                try { AfterShots(); }
                catch (System.Exception e) { Note($"FAIL after: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
            }
            catch (System.Exception e) { Note("FATAL " + e); }
            finally
            {
                File.AppendAllText(Path.Combine(Out, "playlog.txt"), string.Join("\n", PlaytestTools.Log));
                Restore(progress, csvBefore, csv, sitsBefore);
                File.Delete(BackupPath);
                if (File.Exists(OnlyPath)) File.Delete(OnlyPath);
                Time.timeScale = 1;
                EditorApplication.isPaused = false;
                EditorApplication.isPlaying = false;
            }
        }

        static string Transcribe(AudioClip clip, string prompt)
        {
            var t = Conversation.Speech.TranscribeAsync(clip, prompt);
            for (int i = 0; i < 4000 && !t.IsCompleted; i++) PlaytestTools.Frames(1);
            return t.IsCompleted && !t.IsFaulted ? t.Result : null;
        }

        /// <summary>
        /// Says every answer of every procedure with the player's baked voice, as if into the microphone, and checks
        /// that what Whisper hears picks that same answer among the ones on screen.
        /// </summary>
        static void SpeechTest()
        {
            Note("== speech");
            Note("microphones: " + string.Join(", ", Microphone.devices));
            Conversation.Speech.PretendMic = true;
            Conversation.Speech.Warm();
            var report = new System.Text.StringBuilder();
            int total = 0, right = 0, missingClip = 0;
            float scoreSum = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var path in Directory.GetFiles("Assets/_Project/Data/Procedures", "*.json"))
            {
                var proc = Procedure.ProcedureDef.Parse(File.ReadAllText(path));
                int pt = 0, pr = 0;
                foreach (var step in proc.steps)
                {
                    var answers = step.choices.Select(c => c.ko).ToList();
                    var speakable = answers.Where(Conversation.KoreanMatch.Speakable).ToList();
                    if (speakable.Count == 0) continue;
                    var prompt = string.Join(" ", speakable);
                    for (int i = 0; i < answers.Count; i++)
                    {
                        if (!Conversation.KoreanMatch.Speakable(answers[i])) continue;
                        var clip = Conversation.BakedVoice.Load("player", answers[i], false);
                        if (!clip) { missingClip++; continue; }
                        var heard = Transcribe(clip, prompt);
                        // the same words twice in one step belong to different situations, never shown together
                        var shown = Enumerable.Range(0, answers.Count).Where(k => k == i || answers[k] != answers[i]).ToList();
                        var m = Conversation.KoreanMatch.Pick(heard, shown.Select(k => answers[k]).ToList());
                        int picked = m.Index >= 0 ? shown[m.Index] : -1;
                        total++; pt++;
                        if (picked == i) { right++; pr++; scoreSum += m.Score; }
                        else report.AppendLine($"  MISS {proc.id}/{step.id}: said '{answers[i]}' heard '{heard}' -> {(picked >= 0 ? answers[picked] : "none")} ({m.Score:0.00} vs {m.Runner:0.00})");
                    }
                }
                report.Insert(0, $"{proc.id}: {pr}/{pt}\n");
            }
            Note($"speech: {right}/{total} picked right ({(total > 0 ? 100f * right / total : 0):0.0}%), average score {(right > 0 ? scoreSum / right : 0):0.00}, {missingClip} answers not baked yet, {sw.Elapsed.TotalSeconds:0}s");
            File.WriteAllText(Path.Combine(Out, "speech-report.txt"), report.ToString());
        }

        /// <summary>
        /// The real microphone path, which the other voice checks skip (they hand Whisper the baked clip): holds the
        /// talk key on the first microphone while a baked answer plays, lets go, and checks that the recording came
        /// out of the ring buffer and through Whisper, and that off a headset the microphone is closed again. Whether
        /// the answer is also heard depends on the machine (a loopback device hears it, a quiet room doesn't), so
        /// that part is only reported. Skipped where there is no microphone.
        /// </summary>
        static void MicPathCheck()
        {
            Note("== mic");
            var devices = Microphone.devices;
            if (devices.Length == 0) { Note("mic: skipped, no microphone on this machine"); return; }
            foreach (var dev in devices)
            {
                Microphone.GetDeviceCaps(dev, out int lo, out int hi);
                Note($"mic: device '{dev}', rates {lo}-{hi} Hz");
            }
            var proc = Procedure.ProcedureDef.Parse(File.ReadAllText("Assets/_Project/Data/Procedures/clinic.json"));
            var step = proc.steps.First(s => s.choices.Any(c => c.result == "advance" && Conversation.KoreanMatch.Speakable(c.ko)));
            var answers = step.choices.Select(c => c.ko).Where(Conversation.KoreanMatch.Speakable).Distinct().ToList();
            var said = step.choices.First(c => c.result == "advance" && Conversation.KoreanMatch.Speakable(c.ko)).ko;
            var clip = Conversation.BakedVoice.Load("player", said, false);
            var lines = new List<string>();
            void Log(string msg, string trace, LogType type) { if (msg.StartsWith("[Speech]")) lines.Add(msg); }
            bool pretended = Conversation.Speech.PretendMic, pretendedNone = Conversation.Speech.PretendNoMic;
            Conversation.Speech.PretendMic = Conversation.Speech.PretendNoMic = false;
            Application.logMessageReceived += Log;
            GameObject speaker = null;
            try
            {
                if (!Conversation.Speech.Begin()) { Note("FAIL mic: Begin() refused although a microphone is there"); return; }
                bool open = Conversation.Speech.Listening && devices.Any(d => Microphone.IsRecording(d));
                if (clip)
                {
                    speaker = new GameObject("[MicCheck]");
                    var a = speaker.AddComponent<AudioSource>();
                    a.clip = clip;
                    a.spatialBlend = 0;
                    a.Play();
                }
                var held = System.Diagnostics.Stopwatch.StartNew();
                double hold = clip ? clip.length + 0.6 : 1.5;
                while (held.Elapsed.TotalSeconds < hold) PlaytestTools.Frames(1);
                var t = Conversation.Speech.EndAsync(string.Join(" ", answers));
                for (int i = 0; i < 4000 && !t.IsCompleted; i++) PlaytestTools.Frames(1);
                bool closed = !Conversation.Speech.Listening && !devices.Any(d => Microphone.IsRecording(d));
                foreach (var l in lines) Note("mic: " + l);
                if (!t.IsCompleted || t.IsFaulted)
                {
                    Note($"FAIL mic: the transcription {(t.IsFaulted ? "threw " + t.Exception?.GetBaseException().Message : "never finished")}");
                    return;
                }
                var m = Conversation.KoreanMatch.Pick(t.Result ?? "", answers);
                string heard = m.Index >= 0 && answers[m.Index] == said ? "heard the answer" : "the answer didn't reach the microphone (no loopback here)";
                bool recorded = lines.Any(l => l.StartsWith("[Speech] answer:"));
                Note(open && closed && recorded
                    ? $"mic: ok, open while held, closed after, the recording went through ({(t.Result == null ? "nothing heard" : "Whisper heard '" + t.Result + "'")}; {heard})"
                    : $"FAIL mic: open while held={open}, closed after={closed}, recording logged={recorded}");
            }
            finally
            {
                Application.logMessageReceived -= Log;
                if (speaker) Object.Destroy(speaker);
                if (Conversation.Speech.Listening) Conversation.Speech.Cancel();
                Conversation.Speech.PretendMic = pretended;
                Conversation.Speech.PretendNoMic = pretendedNone;
            }
        }

        /// <summary>The conversation after an answer was said out loud: the pick, what was heard and how well.</summary>
        static void VoicePickShot()
        {
            var level = Core.Levels.Find("clinic-1");
            Core.GameSession.I.CurrentLevel = level;
            Core.SceneFlow.Load(level.scene);
            PlaytestTools.WaitScene(level.scene);
            PlaytestTools.Frames(40);
            PlaytestTools.ToCounter();
            PlaytestTools.WaitForInput();
            PlaytestTools.Frames(40);
            Shot("voice-0-asked");
            var d = PlaytestTools.Director();
            var p = PlaytestTools.Panel();
            var right = d.ShownChoices.First(c => c.result == "advance");
            var prompt = string.Join(" ", d.ShownChoices.Select(c => c.ko).Where(Conversation.KoreanMatch.Speakable));
            var heard = Transcribe(Conversation.BakedVoice.Load("player", right.ko, false), prompt);
            Note($"voice: said '{right.ko}' heard '{heard}'");
            p.Heard(heard);
            PlaytestTools.Frames(12);
            Shot("voice-1-picked");
            // a line that matches nothing: the player is told what was heard
            PlaytestTools.WaitForInput();
            PlaytestTools.Frames(30);
            p.Heard("오늘 날씨가 좋네요");
            PlaytestTools.Frames(12);
            Shot("voice-2-unclear");
        }

        /// <summary>The story card through its questions: one photo per question, then the story with its facts.</summary>
        static void StoryShots(string prefix)
        {
            var card = StoryCard.Current;
            if (!card) return;
            int k = 0;
            PlaytestTools.Frames(30);
            Shot($"{prefix}-story-{k++}");
            for (int guard = 0; card && card.Picking && guard < 8; guard++)
            {
                card.Pick(card.Asking < PlaytestTools.Situations.Length ? PlaytestTools.Situations[card.Asking] : 0);
                PlaytestTools.Frames(45);
                Shot($"{prefix}-story-{k++}");
            }
            PlaytestTools.SkipStory();
        }

        /// <summary>After the parts: the scenario select with its case counts, and the Logbook of a personalised part.</summary>
        static void ShowLogbook(LevelSelectScreen sel, string id)
        {
            foreach (var book in Object.FindObjectsByType<Logbook>(FindObjectsInactive.Include))
                Object.Destroy(book.GetComponentInParent<Canvas>().rootCanvas.gameObject);
            PlaytestTools.Frames(2);
            Logbook.Show(sel.procedures, Core.Levels.Find(id), null);
        }

        static void AfterShots()
        {
            Time.timeScale = 1f;
            Core.SceneFlow.Load("Select");
            PlaytestTools.WaitScene("Select");
            PlaytestTools.Frames(90);
            Shot("after-select-1");
            var sel = Object.FindAnyObjectByType<LevelSelectScreen>();
            ShowLogbook(sel, "clinic-1");
            PlaytestTools.Frames(80);
            Shot("after-logbook-brief");
            PlaytestTools.Click("Stomach");
            PlaytestTools.Frames(30);
            PlaytestTools.Click("CONVERSATION");
            PlaytestTools.Frames(40);
            Shot("after-logbook-stomach-talk");
            ShowLogbook(sel, "immigration-1");
            PlaytestTools.Frames(60);
            Shot("after-logbook-immigration");
            // what to bring follows the case: no card (bring cash, get a card) or an empty one (bring it)
            ShowLogbook(sel, "subway-1");
            PlaytestTools.Frames(60);
            PlaytestTools.Click("No, not yet");
            PlaytestTools.Frames(30);
            Shot("after-logbook-subway-nocard");
            PlaytestTools.Click("out of money");
            PlaytestTools.Frames(30);
            Shot("after-logbook-subway-empty");
            ShowLogbook(sel, "emergency-2");
            PlaytestTools.Frames(60);
            PlaytestTools.Click("Seven months");
            PlaytestTools.Frames(30);
            Shot("after-logbook-er-insured");
        }

        static bool voiceChecked;

        /// <summary>
        /// Answers are said, never clicked: a click on the answer the run means to give leaves the question open and
        /// asks for it out loud (with a microphone, pretended) or says a microphone is needed (without one). Checked
        /// once a run, on an answer that can be said; the run then gives the answer itself.
        /// </summary>
        static void VoiceRequiredCheck(ConversationPanel p, string plan, int pick)
        {
            var choice = p.CurrentChoices[pick];
            if (choice.Tried || !Conversation.KoreanMatch.Speakable(choice.Korean)) return;
            voiceChecked = true;
            var button = p.GetComponentsInChildren<UnityEngine.UI.Button>()
                .FirstOrDefault(b => b.name == "Answer" && b.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text == choice.Korean));
            if (!button)
            {
                Note($"FAIL voice-required: no button for '{choice.Korean}' in {plan}");
                return;
            }
            string key = Core.VRMode.Active ? "A" : "SPACE";
            bool pretended = Conversation.Speech.PretendMic, pretendedNone = Conversation.Speech.PretendNoMic;
            try
            {
                foreach (var (mic, expect, label) in new[] { (true, "say_required", "with a microphone"), (false, "say_no_mic", "without a microphone") })
                {
                    Conversation.Speech.PretendMic = mic;
                    Conversation.Speech.PretendNoMic = !mic;
                    button.onClick.Invoke();
                    PlaytestTools.Frames(10);
                    bool stillOpen = p.IsWaiting;
                    string asked = Core.Loc.T(expect, key);
                    bool note = p.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.isActiveAndEnabled && t.text.Contains(asked));
                    Shot($"{plan.Replace('#', '_')}-voice-required-{(mic ? "mic" : "nomic")}");
                    Note(stillOpen && note ? $"voice-required: ok {label}, a click left the question open and said why ({plan})"
                        : $"FAIL voice-required {label}: question open={stillOpen}, note shown={note} ({plan})");
                }
            }
            finally
            {
                Conversation.Speech.PretendMic = pretended;
                Conversation.Speech.PretendNoMic = pretendedNone;
            }
        }

        /// <summary>Without a microphone the Home screen says so and its start stays locked.</summary>
        static void HomeMicCheck()
        {
            Conversation.Speech.PretendNoMic = true;
            try
            {
                PlaytestTools.Frames(70);
                Shot("home-no-mic");
                string title = Core.Loc.T(Conversation.Speech.ModelPresent ? "mic_needed_title" : "speech_missing_title");
                bool shown = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude).Any(t => t.text.Contains(title));
                Note(shown ? "home-mic: ok, without a microphone the note shows and the start is locked" : "FAIL home-mic: no note without a microphone");
            }
            finally
            {
                Conversation.Speech.PretendNoMic = false;
                PlaytestTools.Frames(70);
            }
        }

        static void PlayLevel(string plan)
        {
            var parts = plan.Split('#');
            var id = parts[0];
            PlaytestTools.Situations = parts.Length > 1 ? parts[1].Split('.').Select(int.Parse).ToArray() : new int[0];
            // as SceneFlow.Play does: a new start of a part picks its situation afresh
            Core.GameSession.I.ClearSituation();
            Note($"== {plan}");
            PlaytestTools.Log.Add($"===== {plan}");
            var level = Core.Levels.Find(id);
            Core.GameSession.I.CurrentLevel = level;
            if (!level.StartsInside)
            {
                // the walk down 명동길 to the door, then through it
                Core.SceneFlow.Load(Core.SceneFlow.Hub);
                PlaytestTools.WaitScene(Core.SceneFlow.Hub);
                PlaytestTools.Frames(40);
                if (StoryCard.Current) { StoryShots($"{plan.Replace('#', '_')}-street"); PlaytestTools.Frames(40); }
                Shot($"{plan.Replace('#', '_')}-street-start");
                var door = Object.FindObjectsByType<Quest.BuildingEntrance>(FindObjectsInactive.Exclude).FirstOrDefault(e => e.entranceId == level.entrance);
                var fpc = Object.FindAnyObjectByType<Player.FirstPersonController>();
                if (door && fpc)
                {
                    var at = door.transform.position + door.transform.forward * 6f;
                    at.y = fpc.transform.position.y;
                    var look = door.transform.position + Vector3.up * 2f - at;
                    fpc.Teleport(at, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
                    PlaytestTools.Frames(20);
                    Shot($"{plan.Replace('#', '_')}-street-door");
                }
                else Note($"no door '{level.entrance}' on the street");
            }
            Core.SceneFlow.Load(level.scene);
            PlaytestTools.WaitScene(level.scene);
            PlaytestTools.Frames(40);
            int n = 0;
            if (StoryCard.Current)
            {
                StoryShots($"{plan.Replace('#', '_')}-{n++:00}");
                PlaytestTools.Frames(40);
            }
            Shot($"{plan.Replace('#', '_')}-{n++:00}-begin");
            var d = PlaytestTools.Director();
            string lastTarget = null, lastLine = null;
            var triedWrong = new HashSet<string>();
            bool finishedShot = false;
            for (int f = 0; f < 60000; f++)
            {
                if (d.Finished)
                {
                    if (!finishedShot)
                    {
                        Time.timeScale = 1f;
                        PlaytestTools.Frames(90);
                        Shot($"{plan.Replace('#', '_')}-{n++:00}-finished");
                        finishedShot = true;
                    }
                    break;
                }
                if (StoryCard.Current) { PlaytestTools.SkipStory(); continue; }
                Time.timeScale = 3f;
                // a bank or a public office: take a number first
                if (d.ticketMachine && d.ticketMachine.Armed)
                {
                    PlaytestTools.Log.Add("   (takes a number)");
                    d.ticketMachine.Interact(null);
                }
                foreach (var st in d.stations ?? new FetchStation[0])
                    if (st && st.Armed) { PlaytestTools.Log.Add($"   (fills in {st.document})"); st.Complete(); }
                if (d.counter && d.counter.Armed) d.counter.Interact(null);
                foreach (var room in d.rooms ?? new RoomEntrance[0])
                    if (room && room.Armed) { PlaytestTools.Log.Add($"   (walks into {room.label})"); room.Interact(null); }
                if (!string.IsNullOrEmpty(d.CurrentTarget))
                {
                    bool wrong = d.CurrentWrong.Length > 0 && triedWrong.Add(d.CurrentTarget);
                    if (d.CurrentTarget != lastTarget || wrong)
                    {
                        Time.timeScale = 1f;
                        LookAt(d, wrong ? d.CurrentWrong[0] : d.CurrentTarget);
                        PlaytestTools.Frames(20);
                        Shot($"{plan.Replace('#', '_')}-{n++:00}-go-{(wrong ? d.CurrentWrong[0] : d.CurrentTarget)}");
                        lastTarget = d.CurrentTarget;
                    }
                    PlaytestTools.CompleteTarget(d, wrong);
                    PlaytestTools.Frames(wrong ? 240 : 4);
                    if (wrong) Shot($"{plan.Replace('#', '_')}-{n++:00}-wrongturn-{d.CurrentTarget}");
                    continue;
                }
                var p = PlaytestTools.Panel();
                if (p && p.IsWaiting)
                {
                    Time.timeScale = 1f;
                    if (p.CurrentLine != lastLine)
                    {
                        PlaytestTools.Frames(30);
                        Shot($"{plan.Replace('#', '_')}-{n++:00}-talk");
                    }
                    if (p.WaitingForPin)
                    {
                        // a phone: dial the wrong number once
                        if (!string.IsNullOrEmpty(d.CurrentExpect) && p.CurrentLine != lastLine)
                        {
                            lastLine = p.CurrentLine;
                            PlaytestTools.Log.Add("   YOU> [dials 112]");
                            p.EnterPin("112");
                            PlaytestTools.Frames(60);
                            Shot($"{plan.Replace('#', '_')}-{n++:00}-wrongnumber");
                            continue;
                        }
                        var code = string.IsNullOrEmpty(d.CurrentExpect) ? "2580" : d.CurrentExpect;
                        PlaytestTools.Log.Add($"NPC> {p.CurrentLine}\n   YOU> [PIN {code}]");
                        p.EnterPin(code);
                    }
                    else
                    {
                        var shown = d.ShownChoices;
                        int pick = -1;
                        // on a new line: first the fair answer that turns the talk aside (branch), then one mistake
                        if (p.CurrentLine != lastLine)
                        {
                            pick = shown.FindIndex(c => c.result == "branch");
                            if (pick < 0) pick = shown.FindIndex(c => c.result == "retry");
                        }
                        lastLine = p.CurrentLine;
                        foreach (var want in new[] { "advance", "fetch", "block" })
                            if (pick < 0) pick = shown.FindIndex(c => c.result == want);
                        if (pick < 0) pick = 0;
                        PlaytestTools.Log.Add($"NPC> {p.CurrentLine}\n   YOU> {shown[pick].ko} ({shown[pick].result})");
                        if (!voiceChecked) VoiceRequiredCheck(p, plan, pick);
                        p.Pick(pick);
                    }
                    PlaytestTools.Frames(3);
                    continue;
                }
                PlaytestTools.Frames(1);
            }
            Time.timeScale = 1f;
            if (!d.Finished) Note($"TIMEOUT {plan} at target={d.CurrentTarget} line={PlaytestTools.Panel()?.CurrentLine}");
            PlaytestTools.WaitScene("Results", 2500);
            PlaytestTools.Frames(120);
            Shot($"{plan.Replace('#', '_')}-{n++:00}-results");
            Note($"done {plan}: {SceneManager.GetActiveScene().name}");
        }
    }
}
