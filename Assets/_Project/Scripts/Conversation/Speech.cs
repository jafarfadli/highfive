using System.IO;
using System.Threading.Tasks;
using SeoulFirstWeek.Core;
using UnityEngine;
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
        const int Rate = 16000, MaxSeconds = 8;
        const float MinSeconds = 0.35f;

        static WhisperManager manager;
        static AudioClip recording;
        static string device;
        static float startedAt;

        public static bool ModelPresent => File.Exists(Path.Combine(Application.streamingAssetsPath, ModelFile));
        public static bool MicPresent => PretendMic || Microphone.devices.Length > 0;

        /// <summary>Play-tests on a machine without a microphone still show the control.</summary>
        public static bool PretendMic;

        /// <summary>The player may answer by voice: the setting is on, the model is there and a microphone is plugged in.</summary>
        /// <summary>Answers are said out loud whenever the game can hear them (a microphone and the model).</summary>
        public static bool Enabled => ModelPresent && MicPresent;

        public static bool Listening => recording != null;
        public static bool Ready => manager && manager.IsLoaded;

        /// <summary>Starts loading the model in the background (once per run of the game).</summary>
        public static void Warm()
        {
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

        public static bool Begin()
        {
            if (Listening || Microphone.devices.Length == 0) return false;
            Warm();
            device = PickDevice();
            recording = Microphone.Start(device, false, MaxSeconds, Rate);
            startedAt = Time.unscaledTime;
            return recording != null;
        }

        /// <summary>How loud the microphone is right now (0..1), for the listening meter.</summary>
        public static float Level()
        {
            if (!Listening) return 0f;
            int pos = Microphone.GetPosition(device);
            const int window = 512;
            if (pos < window) return 0f;
            var buf = new float[window];
            recording.GetData(buf, pos - window);
            float sum = 0;
            foreach (var s in buf) sum += s * s;
            return Mathf.Clamp01(Mathf.Sqrt(sum / window) * 6f);
        }

        /// <summary>Stops listening and transcribes what was said (null when it was too short or nothing was heard).</summary>
        public static async Task<string> EndAsync(string prompt)
        {
            if (!Listening) return null;
            int pos = Microphone.GetPosition(device);
            float seconds = Time.unscaledTime - startedAt;
            Microphone.End(device);
            var clip = recording;
            recording = null;
            if (seconds < MinSeconds || pos <= 0) return null;
            var samples = new float[Mathf.Min(pos, clip.samples) * clip.channels];
            clip.GetData(samples, 0);
            Object.Destroy(clip);
            return await TranscribeAsync(samples, Rate, 1, prompt);
        }

        public static void Cancel()
        {
            if (!Listening) return;
            Microphone.End(device);
            Object.Destroy(recording);
            recording = null;
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
