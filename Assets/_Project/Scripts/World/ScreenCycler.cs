using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// A TV or information screen that cycles through pages of text (health tips, notices, the waiting
    /// list), the way the screens in Korean waiting rooms never stay on one slide. Optionally recolours
    /// the screen per page.
    /// </summary>
    public class ScreenCycler : MonoBehaviour
    {
        public Text text;
        [TextArea] public string[] pages;
        public float interval = 7f;
        [Tooltip("Optional: the screen surface, tinted with the matching colour per page.")]
        public Renderer screen;
        public Color[] colors;
        [Tooltip("Shifts this screen's timing so screens in one room don't all change together.")]
        public float offset;

        int shown = -1;
        MaterialPropertyBlock block;

        void Update()
        {
            if (!text || pages == null || pages.Length == 0) return;
            int page = Mathf.FloorToInt((Time.time + offset) / Mathf.Max(0.5f, interval)) % pages.Length;
            if (page == shown) return;
            shown = page;
            text.text = pages[page];
            if (screen && colors != null && colors.Length > 0)
            {
                block ??= new MaterialPropertyBlock();
                var c = colors[page % colors.Length];
                screen.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_EmissionColor", c * 1f);
                screen.SetPropertyBlock(block);
            }
        }
    }
}
