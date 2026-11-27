using System.IO;
using System.Threading.Tasks;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whisper;
using Whisper.Utils;

namespace SeoulFirstWeek.Conversation
{
    /// <summary>
    /// Hearing the player: the microphone and an offline Whisper model (whisper.cpp through whisper.unity, the
    /// ggml-base-q5_1 weights in StreamingAssets/Whisper, fetched by Tools/whisper/get_model.sh). Hold the key,
    /// say the line, let go: the recording is transcribed in Korean with the answers on screen as the decoder's
    /// prompt, which steers it towards the words the player is choosing between. Nothing leaves the machine.
    /// </summary>
    public static class Speech
    {
        public const string ModelFile = "Whisper/ggml-base-q5_1.bin";
        const int Rate = 16000, MaxSeconds = 8, RingSeconds = 12;
        const float MinSeconds = 0.35f, PreRoll = 0.3f;

        static WhisperManager manager;
        // the recording is a looping ring buffer; the talk key marks where the answer starts and ends. On a screen the
        // microphone is open only while the key is held (the system's microphone light, and Bluetooth headsets drop
        // to their call sound while a microphone is open). In a headset it stays open while a level is on: Quest
        // Link's virtual microphone is slow to wake under the player's first syllable and goes quiet when the
        // headset comes off.
        static AudioClip ring;
        static string device;
        static float startedAt;
        static bool listening;
        static int startPos;
        static bool KeepOpen => Core.VRMode.Active;
        // the watchdog (SpeechMicWatchdog): in a headset the ring is wanted while a level is on, and a device that stops
        // delivering (the headset taken off suspends its microphone, and it doesn't always wake again) is opened afresh
        static bool wanted;
        static int lastPos = -1;
        static float lastMove, nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            // menus don't need the microphone: close it there (the next level opens it again)
            SceneManager.activeSceneChanged += (_, now) =>
            {
                if (now.name == "Home" || now.name == "Select" || now.name == "Results") Close();
            };
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // on Android StreamingAssets stay packed inside the APK, where File.Exists can't see them (Whisper reads the
        // model through a web request); the Android build refuses to go without it
        public static bool ModelPresent => true;
        static bool MicAllowed => UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
        public static bool ModelPresent => File.Exists(Path.Combine(Application.streamingAssetsPath, ModelFile));
        static bool MicAllowed => true;
#endif
        public static bool MicPresent => !PretendNoMic && (PretendMic || (MicAllowed && Microphone.devices.Length > 0));

        /// <summary>On a phone: shows the system's question for the microphone unless it is already allowed.
        /// (Desktop systems ask by themselves when a microphone is first opened.)</summary>
        public static void AskForMic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!MicAllowed) UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
        }

        /// <summary>Play-tests: behave as if no microphone were connected (the Home screen's notice).</summary>
        public static bool PretendNoMic;

        /// <summary>Play-tests on a machine without a microphone still show the control.</summary>
        public static bool PretendMic;

        /// <summary>Whether the game can hear answers (a microphone and the model). Without that it can't be played:
        /// the Home screen says so and keeps the start locked.</summary>
        public static bool Enabled => ModelPresent && MicPresent;

        public static bool Listening => listening;
        public static bool Ready => manager && manager.IsLoaded;

        /// <summary>Starts loading the model in the background (once per run of the game).</summary>
        public static void Warm()
        {
            if (KeepOpen) Open();
            if (manager || !ModelPresent) return;
            var go = new GameObject("[Whisper]");
            // created inactive so the model path and language are set before its Awake starts loading
            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            manager = go.AddComponent<WhisperManager>();
            manager.ModelPath = ModelFile;
            manager.language = "ko";
            manager.noContext = true;
            manager.singleSegment = true;
            manager.logLevel = LogLevel.Warning;
            go.SetActive(true);
        }

        /// <summary>The first microphone, or in a headset the headset's own (Quest Link adds it to Windows as
        /// "Headset Microphone (Oculus Virtual Audio Device)").</summary>
        static string PickDevice()
        {
            if (Core.VRMode.Active)
                foreach (var d in Microphone.devices)
                    if (d.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.IndexOf("Headset", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return d;
            return Microphone.devices[0];
        }

        /// <summary>Opens the microphone into the ring buffer (once; again if the device was lost).</summary>
        public static bool Open()
        {
            wanted = KeepOpen;
            if (PretendMic || PretendNoMic || !MicAllowed || Microphone.devices.Length == 0) return false;
            if (ring && device != null && Microphone.IsRecording(device)) return true;
            Release();
            device = PickDevice();
            // some devices (Quest Link's virtual one) only run at their own rate: ask for one they support
            Microphone.GetDeviceCaps(device, out int lo, out int hi);
            int rate = Rate;
            if (hi > 0) rate = Mathf.Clamp(Rate, lo, hi);
            ring = Microphone.Start(device, true, RingSeconds, rate);
            lastPos = -1;
            lastMove = Time.unscaledTime + 1.5f; // a device takes a moment to deliver its first samples
            if (!ring) return false;
            Debug.Log($"[Speech] microphone open: '{device}' at {ring.frequency} Hz, {ring.channels} ch (devices: {string.Join(" | ", Microphone.devices)})");
            return true;
        }

        public static void Close()
        {
            wanted = false;
            Release();
        }

        static void Release()
        {
            listening = false;
            if (device != null && Microphone.IsRecording(device)) Microphone.End(device);
            if (ring) Object.Destroy(ring);
            ring = null;
            device = null;
        }

        /// <summary>Opens the microphone afresh (not while an answer is being held).</summary>
        public static void Reopen(string why)
        {
            if (!wanted || listening) return;
            Debug.Log("[Speech] reopening the microphone: " + why);
            Release();
            Open();
        }

        /// <summary>Called every frame by the watchdog: a device that stopped delivering samples is opened again.</summary>
        internal static void Watch()
        {
            if (!wanted || listening) return;
            float now = Time.unscaledTime;
            if (now < nextCheck) return;
            nextCheck = now + 0.25f;
            if (!ring || device == null)
            {
                if (now > lastMove) { lastMove = now + 1f; Open(); }
                return;
            }
            if (!Microphone.IsRecording(device)) { Reopen("the device stopped recording"); return; }
            int pos = Microphone.GetPosition(device);
            if (pos != lastPos) { lastPos = pos; lastMove = now; return; }
            if (now - lastMove > 0.75f) Reopen("no samples arriving (headset taken off?)");
        }

        /// <summary>Marks the start of an answer: the audio from a moment before this press is kept.</summary>
        public static bool Begin()
        {
            if (listening || !MicAllowed || Microphone.devices.Length == 0) return false;
            Warm();
            if (!Open()) return false;
            int len = ring.samples;
            int pos = Microphone.GetPosition(device);
            startPos = ((pos - (int)(PreRoll * ring.frequency)) % len + len) % len;
            startedAt = Time.unscaledTime;
            listening = true;
            return true;
        }

        /// <summary>How loud the microphone is right now (0..1), for the listening meter.</summary>
        public static float Level()
        {
            if (!listening || !ring) return 0f;
            int pos = Microphone.GetPosition(device);
            const int window = 512;
            if (pos < window) return 0f;
            var buf = new float[window];
            ring.GetData(buf, pos - window);
            float sum = 0;
            foreach (var s in buf) sum += s * s;
            return Mathf.Clamp01(Mathf.Sqrt(sum / window) * 6f);
        }

        /// <summary>Stops listening and transcribes what was said (null when it was too short or nothing was heard).</summary>
        public static async Task<string> EndAsync(string prompt)
        {
            if (!listening) return null;
            listening = false;
            if (!ring) return null;
            float seconds = Time.unscaledTime - startedAt;
            int len = ring.samples, freq = ring.frequency, ch = Mathf.Max(1, ring.channels);
            int endPos = Microphone.GetPosition(device);
            var all = new float[len * ch];
            ring.GetData(all, 0);
            if (!KeepOpen) Close();
            if (seconds < MinSeconds) return null;

            // from the start mark to now, round the loop if it wrapped, at most MaxSeconds
            int count = ((endPos - startPos) % len + len) % len;
            if (seconds >= RingSeconds - 1f || seconds > MaxSeconds) count = Mathf.Min(len, MaxSeconds * freq);
            count = Mathf.Min(count, MaxSeconds * freq);
            var mono = new float[count];
            for (int i = 0; i < count; i++)
            {
                int at = ((startPos + i) % len) * ch;
                float v = all[at];
                for (int c = 1; c < ch; c++) v += all[at + c];
                mono[i] = v / ch;
            }
            var samples = freq == Rate ? mono : Resample(mono, freq, Rate);

            float peak = 0, sq = 0;
            foreach (var v in samples) { peak = Mathf.Max(peak, Mathf.Abs(v)); sq += v * v; }
            bool silent = peak < 0.003f;
            Debug.Log($"[Speech] answer: {seconds:F2} s held, {samples.Length / (float)Rate:F2} s kept ({freq} Hz -> {Rate} Hz), peak {peak:F3}, rms {Mathf.Sqrt(sq / Mathf.Max(1, samples.Length)):F4}" +
                      (silent ? " - SILENT: the microphone delivers no sound (muted in the system or the headset?)" : ""));
            // nothing heard: Whisper makes a line up out of silence ("너는 왜 이렇게 안 왔어요?" from a muted
            // microphone), which could even pick an answer
            if (count <= 0 || silent) return null;
            return await TranscribeAsync(samples, Rate, 1, prompt);
        }

        public static void Cancel()
        {
            listening = false;
            if (!KeepOpen) Close();
        }

        /// <summary>Averages down (or interpolates up) to the rate the model wants.</summary>
        static float[] Resample(float[] src, int from, int to)
        {
            int n = (int)((long)src.Length * to / from);
            var dst = new float[n];
            double r = (double)from / to;
            for (int i = 0; i < n; i++)
            {
                double a = i * r, b = a + r;
                int ia = (int)a, ib = Mathf.Min(src.Length, Mathf.Max(ia + 1, (int)System.Math.Ceiling(b)));
                float sum = 0;
                for (int k = ia; k < ib; k++) sum += src[k];
                dst[i] = sum / Mathf.Max(1, ib - ia);
            }
            return dst;
        }

        public static async Task<string> TranscribeAsync(float[] samples, int rate, int channels, string prompt)
        {
            Warm();
            if (!manager) return null;
            // a very short answer ("그냥이요") can come back empty: give Whisper some silence around it
            int min = (int)(rate * 1.6f) * channels, lead = (int)(rate * 0.25f) * channels;
            if (samples.Length < min)
            {
                var padded = new float[min + lead];
                System.Array.Copy(samples, 0, padded, lead, samples.Length);
                samples = padded;
            }
            // the manager waits for a model that is still loading
            manager.initialPrompt = prompt;
            var result = await manager.GetTextAsync(samples, rate, channels);
            return result?.Result?.Trim();
        }

        public static async Task<string> TranscribeAsync(AudioClip clip, string prompt)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            return await TranscribeAsync(samples, clip.frequency, clip.channels, prompt);
        }
    }
}
