using System;
using System.Text;
using UnityEngine;

namespace SeoulFirstWeek.Conversation
{
    public static class WavUtility
    {
        public static AudioClip FromPcm16(byte[] pcm, int rate, int channels, string name, int offset = 0, int length = -1)
        {
            if (length < 0) length = pcm.Length - offset;
            int count = length / 2;
            if (count <= 0) return null;
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = BitConverter.ToInt16(pcm, offset + i * 2) / 32768f;
            var clip = AudioClip.Create(name, count / channels, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Brings speech to a common loudness: the macOS voices differ by up to 4 dB and some are
        /// quiet next to the room ambience. Silence is ignored when measuring; peaks stay below 0.97.
        /// </summary>
        public static void Normalize(AudioClip clip, float targetRms = 0.17f, float maxGain = 4f)
        {
            if (!clip) return;
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            double sum = 0;
            int voiced = 0;
            float peak = 0;
            foreach (var x in data)
            {
                float a = Mathf.Abs(x);
                peak = Mathf.Max(peak, a);
                if (a < 0.02f) continue;
                sum += x * x;
                voiced++;
            }
            if (voiced == 0 || peak <= 0) return;
            float rms = (float)System.Math.Sqrt(sum / voiced);
            float gain = Mathf.Min(targetRms / rms, maxGain, 0.97f / peak);
            if (Mathf.Abs(gain - 1f) < 0.05f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            clip.SetData(data, 0);
        }

        /// <summary>Parses a 16-bit PCM WAV file.</summary>
        public static AudioClip FromWav(byte[] wav, string name)
        {
            int channels = 1, rate = 22050, pos = 12;
            while (pos + 8 <= wav.Length)
            {
                var id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, pos + 10);
                    rate = BitConverter.ToInt32(wav, pos + 12);
                }
                else if (id == "data")
                {
                    if (size <= 0 || pos + 8 + size > wav.Length) size = wav.Length - pos - 8;
                    return FromPcm16(wav, rate, channels, name, pos + 8, size);
                }
                pos += 8 + size + (size & 1);
            }
            return null;
        }
    }
}
