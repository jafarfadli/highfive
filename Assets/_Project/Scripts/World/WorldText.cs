using SeoulFirstWeek.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// World-space Hangul text (signs, boards, labels) via a small world canvas. Font sizes are canvas
    /// pixels at <see cref="PixelsPerMeter"/>; best fit shrinks the text into its rect. Best fit cannot go
    /// below 8 pixels, so an 8 cm line at 100 px/m would simply be cut off: labels under 30 cm tall (name
    /// plates, counter signs, notices) and boards with more lines than their height holds (the clinic's
    /// waiting monitor) get a finer canvas, just fine enough for about 40 pixels of height or 14 per line.
    /// Not finer: every glyph size lands in one dynamic font atlas, and big Hangul glyphs overflow it.
    /// </summary>
    public static class WorldText
    {
        public const float PixelsPerMeter = 100f;
        const float MaxPixelsPerMeter = 400f;

        public static Text Create(Transform parent, string name, string text, Vector3 localPos, Vector2 sizeMeters,
            int fontSize, Color color, bool bold = true, TextAnchor align = TextAnchor.MiddleCenter, Quaternion? rotation = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            float ppm = PixelsPerMeter;
            int lines = 1;
            foreach (char c in text) if (c == '\n') lines++;
            // small labels, and boards with more lines than their height holds at 100 px/m
            if (sizeMeters.y < 0.3f || 14f * lines > sizeMeters.y * PixelsPerMeter)
                ppm = Mathf.Clamp(Mathf.Max(40f, 14f * lines) / Mathf.Max(0.01f, sizeMeters.y), PixelsPerMeter, MaxPixelsPerMeter);
            fontSize = Mathf.RoundToInt(fontSize * ppm / PixelsPerMeter);
            rt.sizeDelta = sizeMeters * ppm;
            rt.localPosition = localPos;
            rt.localRotation = rotation ?? Quaternion.identity;
            rt.localScale = Vector3.one / ppm;
            var label = UIKit.Label(rt, text, fontSize, color, bold, align);
            label.rectTransform.Stretch();
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = fontSize;
            return label;
        }
    }
}
