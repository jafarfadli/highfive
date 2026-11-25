using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SeoulFirstWeek.Conversation
{
    /// <summary>Offline Korean TTS using the macOS "say" command (voices Yuna, Eddy, Flo, ...).</summary>
    public class MacSayTextToSpeech : ITextToSpeech
    {
        int counter;
        public string Label => "macOS voices";

        public static bool Supported =>
            Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;

        public async Task<AudioClip> SynthesizeAsync(string text, VoiceSpec voice, bool slow, CancellationToken ct)
        {
            var dir = Application.temporaryCachePath;
            var id = Interlocked.Increment(ref counter);
            var input = Path.Combine(dir, $"say_{id}.txt");
            var output = Path.Combine(dir, $"say_{id}.wav");
            File.WriteAllText(input, text);
            int rate = slow ? Mathf.RoundToInt(voice.MacRate * 0.7f) : voice.MacRate;
            var args = $"-v \"{voice.MacVoice}\" -r {rate} -f \"{input}\" -o \"{output}\" --file-format=WAVE --data-format=LEI16@22050";

            var ok = await Task.Run(() =>
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo("/usr/bin/say", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                    });
                    if (p == null) return false;
                    p.WaitForExit(15000);
                    return p.HasExited && p.ExitCode == 0;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[TTS] say failed: " + e.Message);
                    return false;
                }
            }, ct);

            if (!ok || !File.Exists(output)) return null;
            var bytes = File.ReadAllBytes(output);
            TryDelete(input);
            TryDelete(output);
            return WavUtility.FromWav(bytes, "npc");
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* temp file */ }
        }
    }

    /// <summary>No audio: subtitles only.</summary>
    public class SilentTextToSpeech : ITextToSpeech
    {
        public string Label => "Subtitles only";
        public Task<AudioClip> SynthesizeAsync(string text, VoiceSpec voice, bool slow, CancellationToken ct) =>
            Task.FromResult<AudioClip>(null);
    }
}
