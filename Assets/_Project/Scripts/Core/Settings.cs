using System;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    /// <summary>
    /// How much help the answer cards give: Easy shows each answer's romanization and translation, Medium the
    /// romanization only, Hard the Korean only. The staff member's line keeps its translation at every level.
    /// </summary>
    public enum Difficulty { Easy, Medium, Hard }

    /// <summary>Player settings stored in PlayerPrefs: music and SFX volume, look sensitivity, hint language, difficulty.</summary>
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

        public static Difficulty Difficulty
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(DifficultyKey, (int)value); Changed?.Invoke(); }
        }

        const string DifficultyKey = "sfw.difficulty";

        public static void Save() => PlayerPrefs.Save();
    }
}
