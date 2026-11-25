using System;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>Typefaces of the UI.</summary>
    public enum Face
    {
        /// <summary>Noto Sans KR: every line people read (staff lines, answers, translations, notes).</summary>
        Read,
        /// <summary>Noto Sans KR Bold: answers, labels, romanization, hints, anything small.</summary>
        ReadBold,
        /// <summary>Bangers: titles, banners, big numbers, keycaps (Hangul falls back to Black Han Sans).</summary>
        Display,
        /// <summary>Black Han Sans: place names, signboards, stamps.</summary>
        DisplayKo,
    }

    public enum ButtonStyle
    {
        /// <summary>A brush strip in the brush colour: the one primary action on a screen.</summary>
        Primary,
        /// <summary>A paper keycap with an ink outline.</summary>
        Paper,
        /// <summary>A dark night strip with paper lettering.</summary>
        Night,
    }

    /// <summary>The parts kit of the retro-print UI: cards, brush strips, balloons, marks, keycaps, stamps, highlights, sunbursts and lettering.</summary>
    public static partial class UIKit
    {
        static Font display, displayKo;

        public static Font Display => display ? display : display = LoadFont("Fonts/Bangers-Regular");
        public static Font DisplayKo => displayKo ? displayKo : displayKo = LoadFont("Fonts/BlackHanSans-Regular");

        static Font LoadFont(string path)
        {
            var f = Resources.Load<Font>(path);
            return f ? f : Bold;
        }

        public static Font FontFor(Face face) => face switch
        {
            Face.ReadBold => Bold,
            Face.Display => Display,
            Face.DisplayKo => DisplayKo,
            _ => Regular,
        };

        static int seedCounter = 7;

        static int NextSeed()
        {
            unchecked { seedCounter = (seedCounter * 1103515245 + 12345) & 0x7fffffff; }
            return seedCounter % 9973 + 1;
        }

        // ------------------------------------------------------------------ lettering

        /// <summary>Text in one of the four voices, coloured by a palette role.</summary>
        public static Text Words(Transform parent, string text, int size, Role role, Face face = Face.Read,
            TextAnchor align = TextAnchor.MiddleLeft, float alpha = 1f)
        {
            var rt = Rect("Words", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = FontFor(face);
            t.fontSize = size;
            t.alignment = align;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = face is Face.Display or Face.DisplayKo ? 0.95f : 1.05f;
            Themed.On(t, role, alpha);
            return t;
        }

        /// <summary>
        /// Display lettering (titles, banners, big numbers) with an outline and a hard offset shadow that scale
        /// with the type: light letters get an ink outline of 5 % of the size, dark and brush letters none (it
        /// clogs their counters); the shadow sits 5.5 % right and 6.5 % down, in ink, or in the brush colour
        /// under dark letters so ink never doubles ink.
        /// </summary>
        public static Text Lettering(Transform parent, string text, int size, Role fill = Role.Paper,
            TextAnchor align = TextAnchor.MiddleLeft, Face face = Face.Display)
        {
            var t = Words(parent, text, size, fill, face, align);
            var fx = t.gameObject.AddComponent<InkText>();
            bool dark = fill is Role.Ink or Role.InkSoft or Role.Night or Role.BrushDeep;
            bool light = fill is Role.Paper or Role.PaperDeep or Role.Balloon or Role.Glow or Role.Ground;
            fx.outline = light ? Mathf.Max(2f, size * 0.05f) : 0f;
            fx.directions = Mathf.Clamp(Mathf.CeilToInt(fx.outline * 1.6f), 8, 16);
            fx.shadow = new Vector2(Mathf.Max(2f, size * 0.055f), -Mathf.Max(2f, size * 0.065f));
            fx.shadowRole = dark ? Role.Brush : Role.Ink;
            return t;
        }

        // ------------------------------------------------------------------ shapes

        public static InkShape Ink(Transform parent, string name, InkKind kind, Role fill, float line = 3f, Role lineRole = Role.Ink)
        {
            var rt = Rect(name, parent);
            var s = rt.gameObject.AddComponent<InkShape>();
            s.kind = kind;
            s.fillRole = fill;
            s.lineRole = lineRole;
            s.line = line;
            s.seed = NextSeed();
            s.raycastTarget = false;
            return s;
        }

        /// <summary>A flat card with a thick ink outline and a solid offset shadow.</summary>
        public static InkShape PaperPanel(Transform parent, string name = "Paper", Role fill = Role.Paper, float line = 4.5f, bool shadow = true)
        {
            var s = Ink(parent, name, InkKind.Panel, fill, line);
            if (!shadow) s.shadow = Vector2.zero;
            return s;
        }

        /// <summary>A dry-brush strip (no outline): dark answer strips, primary buttons, banners.</summary>
        public static InkShape BrushStrip(Transform parent, string name = "Strip", Role fill = Role.Night)
        {
            var s = Ink(parent, name, InkKind.Strip, fill, 0f);
            s.shadow = Vector2.zero;
            s.rough = 2.2f;
            s.tear = 1.1f;
            return s;
        }

        /// <summary>A speech balloon whose tail points at <paramref name="tail"/> (normalized rect coordinates).</summary>
        public static InkShape SpeechBalloon(Transform parent, Vector2 tail, string name = "Balloon")
        {
            var s = Ink(parent, name, InkKind.Balloon, Role.Balloon, 4.5f);
            s.tail = tail;
            s.radius = 30f;
            s.shadow = new Vector2(9, -10);
            return s;
        }

        /// <summary>The hand-drawn ○ (right answers, picked items, the selected language).</summary>
        public static InkShape CircleMark(Transform parent, Role role = Role.Brush, float width = 7f, string name = "Circle")
        {
            var s = Ink(parent, name, InkKind.Ring, role, width);
            s.shadow = Vector2.zero;
            s.rough = 2.5f;
            return s;
        }

        /// <summary>A pen stroke through normalized points (NaN starts a new stroke).</summary>
        public static InkShape PenStroke(Transform parent, Role role, float width, params Vector2[] points)
        {
            var s = Ink(parent, "Stroke", InkKind.Stroke, role, width);
            s.points = points;
            s.shadow = Vector2.zero;
            s.rough = 1.6f;
            return s;
        }

        public static InkShape Dot(Transform parent, Role fill, float line = 2.5f, string name = "Dot")
        {
            var s = Ink(parent, name, InkKind.Disc, fill, line);
            s.shadow = Vector2.zero;
            return s;
        }

        /// <summary>A right-pointing hand-drawn arrow (to-dos, "go" buttons).</summary>
        public static InkShape Arrow(Transform parent, Role role, float width = 3.5f)
        {
            var nan = new Vector2(float.NaN, float.NaN);
            return PenStroke(parent, role, width,
                new Vector2(0.04f, 0.42f), new Vector2(0.45f, 0.52f), new Vector2(0.94f, 0.5f), nan,
                new Vector2(0.62f, 0.86f), new Vector2(0.95f, 0.5f), new Vector2(0.64f, 0.12f));
        }

        // ------------------------------------------------------------------ notes, highlights, sunbursts

        /// <summary>A glow-coloured note with an ink outline and a solid shadow (lessons, warnings, toasts).</summary>
        public static InkShape StickyNote(Transform parent, string name = "Sticky")
        {
            var s = Ink(parent, name, InkKind.Panel, Role.Glow, 4f);
            s.radius = 4f;
            s.shadow = new Vector2(7, -8);
            return s;
        }

        /// <summary>A highlighter band behind a line of text (ignored by layouts; stretch it over the line).</summary>
        public static InkShape Highlight(Transform parent, Role role = Role.Glow, string name = "Highlight")
        {
            var s = BrushStrip(parent, name, role);
            s.dry = 0.2f;
            s.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return s;
        }

        /// <summary>A retro sunburst of <paramref name="rays"/> rays (turn its RectTransform to spin it).</summary>
        public static InkShape Rays(Transform parent, int rays, Role role, float alpha = 1f, string name = "Rays")
        {
            var s = Ink(parent, name, InkKind.Rays, role, 0f);
            s.spikes = rays;
            s.fillAlpha = alpha;
            s.shadow = Vector2.zero;
            return s;
        }

        /// <summary>A small document: a sheet with a folded corner and three lines of writing.</summary>
        public static RectTransform DocIcon(Transform parent, Role fill = Role.Balloon)
        {
            var root = Rect("DocIcon", parent);
            var sheet = Ink(root, "Sheet", InkKind.Panel, fill, 2.5f);
            sheet.radius = 2f;
            sheet.shadow = Vector2.zero;
            sheet.rectTransform.Stretch();
            var fold = PenStroke(root, Role.Ink, 2.2f, new Vector2(0.62f, 1f), new Vector2(0.62f, 0.78f), new Vector2(0.98f, 0.78f));
            fold.taper = 0.9f;
            fold.rectTransform.Stretch();
            for (int i = 0; i < 3; i++)
            {
                var l = PenStroke(root, Role.Ink, 2f, new Vector2(0, 0.5f), new Vector2(1, 0.5f));
                l.taper = 0.9f;
                var rt = l.rectTransform;
                rt.anchorMin = new Vector2(0.2f, 0.56f - i * 0.17f);
                rt.anchorMax = new Vector2(i == 2 ? 0.58f : 0.8f, 0.56f - i * 0.17f);
                rt.sizeDelta = new Vector2(0, 4);
            }
            return root;
        }

        /// <summary>An inked keycap with a label (1–4, E, R, Esc, a part number or a short word); the label is in the ink colour unless given.</summary>
        public static RectTransform Keycap(Transform parent, string label, int size = 24, Role fill = Role.Paper, Role ink = Role.Ink, Role? labelRole = null)
        {
            var root = Rect("Key", parent);
            var cap = Ink(root, "Cap", InkKind.Panel, fill, Mathf.Max(3f, size * 0.13f), ink);
            cap.lineRole = ink;
            cap.radius = size * 0.32f;
            cap.shadow = new Vector2(0, -Mathf.Max(3f, size * 0.18f));
            cap.shadowRole = ink;
            cap.shadowAlpha = 1f;
            cap.rough = 0.9f;
            cap.rectTransform.Stretch();
            var t = Words(root, label, size, labelRole ?? ink, Face.Display, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(2, 2, 0, size * 0.08f);
            float w = Mathf.Max(size * 1.75f, label.Length * size * 0.62f + size);
            root.sizeDelta = new Vector2(w, size * 1.75f);
            root.Layout(w, size * 1.75f);
            return root;
        }

        /// <summary>A round rubber stamp: two rough rings and a word, printed with missing ink.</summary>
        public static RectTransform StampMark(Transform parent, string text, float diameter, Role role = Role.Brush, int fontSize = 30)
        {
            var root = Rect("Stamp", parent);
            root.sizeDelta = new Vector2(diameter, diameter);
            var outer = CircleMark(root, role, Mathf.Max(4f, diameter * 0.045f), "Outer");
            outer.rectTransform.Stretch();
            outer.sweep = 362f;
            outer.taper = 0.85f;
            outer.grunge = 0.55f;
            var inner = CircleMark(root, role, Mathf.Max(2f, diameter * 0.018f), "Inner");
            inner.rectTransform.Stretch(diameter * 0.075f, diameter * 0.075f, diameter * 0.075f, diameter * 0.075f);
            inner.sweep = 360f;
            inner.taper = 0.9f;
            inner.grunge = 0.4f;
            var t = Words(root, text, fontSize, role, Face.DisplayKo, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(diameter * 0.12f, diameter * 0.12f, diameter * 0.12f, diameter * 0.12f);
            t.lineSpacing = 0.92f;
            root.Group();
            return root;
        }

        // ------------------------------------------------------------------ controls

        /// <summary>
        /// An inked button: the body tilts and lifts off its shadow on hover and slams flat when pressed.
        /// Primary = brush strip, Paper = keycap paper, Night = dark strip.
        /// </summary>
        public static Button InkButton(Transform parent, string label, Action onClick, ButtonStyle style = ButtonStyle.Paper, int size = 26)
        {
            var root = Rect("Button", parent);
            var body = style switch
            {
                ButtonStyle.Primary => BrushStrip(root, "Body", Role.Brush),
                ButtonStyle.Night => BrushStrip(root, "Body", Role.Night),
                _ => PaperPanel(root, "Body", Role.Paper, 4f),
            };
            body.shadow = new Vector2(6, -7);
            body.shadowRole = Role.Ink;
            if (style == ButtonStyle.Paper) body.radius = 6f;
            else body.dry = 0.7f;
            body.raycastTarget = true;
            body.rectTransform.Stretch();
            var textRole = style switch { ButtonStyle.Primary => Role.OnBrush, ButtonStyle.Night => Role.Paper, _ => Role.Ink };
            var t = Words(body.transform, label, size, textRole, Face.Display, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(18, 18, 4, 4);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = body;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var hover = root.gameObject.AddComponent<InkHover>();
            hover.visual = body.rectTransform;
            hover.body = body;
            return btn;
        }

        /// <summary>A slider drawn as an ink line with a brush fill and a round blob handle.</summary>
        public static Slider InkSlider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;

            var track = PenStroke(root, Role.Ink, 3f, new Vector2(0, 0.5f), new Vector2(0.5f, 0.52f), new Vector2(1, 0.5f));
            track.taper = 0.6f;
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 14);
            track.rectTransform.anchoredPosition = Vector2.zero;

            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-14, 16);
            fillArea.anchoredPosition = Vector2.zero;
            var fill = BrushStrip(fillArea, "Fill", Role.Brush);
            fill.dry = 0.4f;
            fill.rough = 1.2f;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("HandleArea", root).Stretch(14, 14, 0, 0);
            var handle = Dot(handleArea, Role.Paper, 3f, "Handle");
            handle.raycastTarget = true;
            handle.shadow = new Vector2(2, -3);
            handle.shadowAlpha = 1f;
            handle.rectTransform.sizeDelta = new Vector2(30, -18);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>A full-screen wash of ink (behind menus): a translucent ink field printed with halftone dots.</summary>
        public static RectTransform InkScrim(Transform parent, float alpha = 0.62f, float dots = 0.35f)
        {
            var root = Rect("Scrim", parent).Stretch();
            var wash = root.gameObject.AddComponent<Image>();
            wash.raycastTarget = true;
            Themed.On(wash, Role.Ink, alpha);
            if (dots > 0f)
            {
                var dotLayer = Ink(root, "Dots", InkKind.Panel, Role.Ink, 0f);
                dotLayer.rough = 0f;
                dotLayer.tear = 0f;
                dotLayer.radius = 0f;
                dotLayer.shadow = Vector2.zero;
                dotLayer.halftone = dots;
                dotLayer.fillAlpha = 0.55f;
                dotLayer.rectTransform.Stretch(-4, -4, -4, -4);
            }
            return root;
        }
    }
}
