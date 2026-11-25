using System.Collections.Generic;
using UnityEngine;

namespace SeoulFirstWeek.Audio
{
    /// <summary>Synthesised sound effects, so the project ships without audio assets.</summary>
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new();

        public static AudioClip Chime => Get("chime", () => Tones(new[] { 783.99f, 659.25f }, 0.55f, 0.35f));
        public static AudioClip Success => Get("success", () => Tones(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.14f, 0.3f));
        public static AudioClip Soft => Get("soft", () => Tones(new[] { 440f, 392f }, 0.16f, 0.2f));
        public static AudioClip Blip => Get("blip", () => Tones(new[] { 1318.5f }, 0.07f, 0.18f));
        public static AudioClip Tick => Get("tick", () => Tones(new[] { 1760f }, 0.03f, 0.12f));
        public static AudioClip Paper => Get("paper", MakePaper);
        public static AudioClip Step => Get("step", MakeStep);

        static AudioClip Get(string key, System.Func<AudioClip> make)
        {
            if (!Cache.TryGetValue(key, out var clip) || clip == null) Cache[key] = clip = make();
            return clip;
        }

        static AudioClip Tones(float[] freqs, float noteLength, float volume)
        {
            int noteSamples = (int)(noteLength * Rate);
            int tail = (int)(0.6f * Rate);
            var data = new float[noteSamples * freqs.Length + tail];
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = n * noteSamples;
                for (int i = 0; i < noteSamples + tail && start + i < data.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 5.5f);
                    float s = Mathf.Sin(2 * Mathf.PI * freqs[n] * t) + 0.25f * Mathf.Sin(4 * Mathf.PI * freqs[n] * t);
                    data[start + i] += s * env * volume;
                }
            }
            return Clip("tones", data);
        }

        static AudioClip MakePaper()
        {
            var rng = new System.Random(7);
            var data = new float[(int)(0.45f * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float clicks = (Mathf.Sin(t * 2 * Mathf.PI * 38f) > 0.6f) ? 1f : 0.25f;
                data[i] = (float)(rng.NextDouble() * 2 - 1) * 0.25f * clicks * Mathf.Exp(-t * 3f);
            }
            return Clip("paper", data);
        }

        static AudioClip MakeStep()
        {
            var rng = new System.Random(3);
            var data = new float[(int)(0.12f * Rate)];
            float lp = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                lp += 0.08f * ((float)(rng.NextDouble() * 2 - 1) - lp);
                data[i] = lp * 2.2f * Mathf.Exp(-t * 40f);
            }
            return Clip("step", data);
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>One shared 2D source for UI and feedback sounds.</summary>
    public static class Sfx
    {
        static AudioSource source;

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (source == null)
            {
                var go = new GameObject("[Sfx]");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.spatialBlend = 0;
                source.playOnAwake = false;
                // UI clicks stay audible in the pause menu
                source.ignoreListenerPause = true;
            }
            source.PlayOneShot(clip, volume * Core.Settings.Sfx);
            AudioTap.Note(clip, volume * Core.Settings.Sfx);
        }
    }
}
