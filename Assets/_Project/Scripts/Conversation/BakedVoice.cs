using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SeoulFirstWeek.Conversation
{
    /// <summary>
    /// Pre-rendered voice lines (Tools/voice/bake_voices.py, OmniVoice) in Resources/Voice. They ship with
    /// the build, so staff speak on any platform without a TTS engine or network.
    /// The file name is the key below; the bake script computes the same key.
    /// </summary>
    public static class BakedVoice
    {
        const string Folder = "Voice/";
        static readonly Dictionary<string, AudioClip> Cache = new();

        /// <summary>First 20 hex chars of md5("voice|normal|text") or md5("voice|slow|text").</summary>
        public static string Key(string voice, bool slow, string text)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"{voice}|{(slow ? "slow" : "normal")}|{text}"));
            var sb = new StringBuilder(40);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString(0, 20);
        }

        /// <summary>The baked clip for a line, or null when it wasn't baked.</summary>
        public static AudioClip Load(string voice, string text, bool slow)
        {
            if (string.IsNullOrEmpty(voice) || string.IsNullOrWhiteSpace(text)) return null;
            var key = Key(voice, slow, text.Trim());
            if (Cache.TryGetValue(key, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(Folder + key);
            Cache[key] = clip;
            return clip;
        }

        /// <summary>Splits a PA template into the pieces the bake script renders separately ("딩동." / "{0}번 고객님," / …).</summary>
        public static string[] Segments(string template) => Regex.Split(template.Trim(), @"(?<=[.,?])\s+");

        /// <summary>
        /// A PA announcement stitched from baked pieces, so random queue numbers still have a voice.
        /// Null when any piece is missing.
        /// </summary>
        public static AudioClip Compose(string voice, string template, params object[] args)
        {
            var parts = new List<AudioClip>();
            foreach (var segment in Segments(template))
            {
                var clip = Load(voice, string.Format(segment, args), false);
                if (!clip || clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData()) return null;
                parts.Add(clip);
            }
            if (parts.Count == 0) return null;
            int rate = parts[0].frequency;
            int gap = Mathf.RoundToInt(rate * 0.08f);
            int total = 0;
            foreach (var p in parts)
            {
                if (p.frequency != rate || p.channels != 1) return null;
                total += p.samples + gap;
            }
            var data = new float[total];
            int at = 0;
            foreach (var p in parts)
            {
                var chunk = new float[p.samples];
                p.GetData(chunk, 0);
                chunk.CopyTo(data, at);
                at += chunk.Length + gap;
            }
            var result = AudioClip.Create("pa", total, 1, rate, false);
            result.SetData(data, 0);
            return result;
        }
    }
}
