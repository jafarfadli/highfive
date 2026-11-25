using UnityEngine;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// The shared look of the UI: the ink material and its noise texture, and the lettering. Painted and
    /// saved by the editor's InkKit into Resources/InkStyle.asset; the UI falls back to plain UI graphics
    /// and Noto Sans KR when it is missing.
    /// </summary>
    public class InkStyle : ScriptableObject
    {
        [Tooltip("SeoulFirstWeek/UI Ink, used by every InkShape.")]
        public Material material;
        [Tooltip("R = paper grain, G = dry-brush bristles, B = stamp grunge (tileable).")]
        public Texture2D noise;

        [Header("Lettering")]
        [Tooltip("Bangers: titles, banners, big numbers, keycaps (Latin and Vietnamese; Hangul falls back to Black Han Sans).")]
        public Font display;
        [Tooltip("Black Han Sans: place names, signboards, stamps.")]
        public Font displayKo;

        static InkStyle current;
        static bool loaded;

        public static InkStyle Current
        {
            get
            {
                if (!loaded || current == null)
                {
                    current = Resources.Load<InkStyle>("InkStyle");
                    loaded = true;
                }
                return current;
            }
        }
    }
}
