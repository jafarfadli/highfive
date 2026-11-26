using System;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    /// <summary>Player settings stored in PlayerPrefs: music and SFX volume, look sensitivity, hint language.</summary>
    public static class Settings
    {
        const string MusicKey = "sfw.music", SfxKey = "sfw.sfx", SensKey = "sfw.sensitivity", LangKey = "sfw.language",
            MotionKey = "sfw.reducedMotion";

        public static event Action Changed;

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.5f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        /// <summary>Multiplier on mouse look (0.25 to 2.5).</summary>
        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(SensKey, 1f);
            set { PlayerPrefs.SetFloat(SensKey, Mathf.Clamp(value, 0.25f, 2.5f)); Changed?.Invoke(); }
        }

        public static HintLanguage Language
        {
            get => (HintLanguage)PlayerPrefs.GetInt(LangKey, 0);
            set { PlayerPrefs.SetInt(LangKey, (int)value); Changed?.Invoke(); }
        }

        /// <summary>Calms the hand-drawn animation: no line boil, shakes or stepped drawing.</summary>
        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(MotionKey, 0) == 1;
            set { PlayerPrefs.SetInt(MotionKey, value ? 1 : 0); Changed?.Invoke(); }
        }

        /// <summary>Answering by voice: hold Space and say the line (needs a microphone and the Whisper model).</summary>
        public static bool SpeakAnswers
        {
            get => PlayerPrefs.GetInt(SpeakKey, 1) == 1;
            set { PlayerPrefs.SetInt(SpeakKey, value ? 1 : 0); Changed?.Invoke(); }
        }

        const string SpeakKey = "sfw.speak";

        public static void Save() => PlayerPrefs.Save();
    }
}
