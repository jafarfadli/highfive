using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// The cursor stays free in every mode: the camera turns only while the left button is held
    /// (see FirstPersonController), so menus and gameplay both just need a visible, unlocked cursor.
    /// </summary>
    public static class CursorState
    {
        public static void Gameplay(bool on)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>Small helpers for building uGUI from code.</summary>
    public static partial class UIKit
    {
        static Font regular, bold;
        static Sprite rounded, circle, ring, play;

        public static Font Regular => regular ? regular : regular = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        public static Font Bold => bold ? bold : bold = Resources.Load<Font>("Fonts/NotoSansKR-Bold");

        /// <summary>
        /// Binds a short last word (a counter or room number, up to three letters) to the word before it with a
        /// no-break space, so a wrapped line never ends on a lone "3".
        /// </summary>
        public static string NoOrphan(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int cut = text.LastIndexOf(' ');
            if (cut <= 0 || text.Length - cut - 1 > 3) return text;
            return text.Substring(0, cut) + ' ' + text.Substring(cut + 1);
        }

        public static Sprite Rounded => rounded ? rounded : rounded = MakeRounded(64, 24, false);
        public static Sprite Circle => circle ? circle : circle = MakeRounded(64, 32, false);
        public static Sprite Ring => ring ? ring : ring = MakeRounded(64, 32, true);
        public static Sprite Play => play ? play : play = MakePlay(64);

        static Sprite MakeRounded(int size, int radius, bool hollow)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                if (hollow) a *= Mathf.Clamp01(d - (radius - 6f) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = hollow ? Vector4.zero : new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>A right-pointing triangle (the listen buttons), antialiased like the rounded sprites.</summary>
        static Sprite MakePlay(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            // counter-clockwise, so every edge has the inside on its left
            var a = new Vector2(size * 0.16f, size * 0.08f);
            var b = new Vector2(size * 0.92f, size * 0.5f);
            var c = new Vector2(size * 0.16f, size * 0.92f);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(EdgeDistance(a, b, p), Mathf.Min(EdgeDistance(b, c, p), EdgeDistance(c, a, p)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        static float EdgeDistance(Vector2 from, Vector2 to, Vector2 p)
        {
            var e = to - from;
            return (e.x * (p.y - from.y) - e.y * (p.x - from.x)) / e.magnitude;
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor and pivot at the same normalized point, then offset and size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool roundedCorners = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (roundedCorners)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, bool isBold = false,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = isBold ? Bold : Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            return t;
        }

        public static Button Button(Transform parent, string label, Color background, Action onClick, int fontSize = 26,
            Color? textColor = null)
        {
            var img = Panel(parent, "Button", background);
            var btn = Clickable(img, onClick);
            var t = Label(img.transform, label, fontSize, textColor ?? Palette.Text, true, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(12, 12, 4, 4);
            return btn;
        }

        /// <summary>Makes a graphic a button: hover brightens slightly, press darkens slightly.</summary>
        public static Button Clickable(Image img, Action onClick)
        {
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Text ButtonLabel(this Button b) => b.GetComponentInChildren<Text>();

        /// <summary>Horizontal slider: rounded track, accent fill, round handle.</summary>
        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var track = Panel(root, "Track", Color.white.WithAlpha(0.14f));
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(0, 0.5f);
            trt.anchorMax = new Vector2(1, 0.5f);
            trt.sizeDelta = new Vector2(0, 12);
            trt.anchoredPosition = Vector2.zero;

            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 12);
            fillArea.anchoredPosition = Vector2.zero;
            var fill = Panel(fillArea, "Fill", Palette.Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("HandleArea", root).Stretch(14, 14, 0, 0);
            var handle = Panel(handleArea, "Handle", Color.white, false);
            handle.sprite = Circle;
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(28, -26);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool fitHeight = true)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (fitHeight)
            {
                var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Layout(this Component c, float prefWidth = -1, float prefHeight = -1,
            float flexWidth = -1, float minHeight = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (!le) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = prefWidth;
            le.preferredHeight = prefHeight;
            le.flexibleWidth = flexWidth;
            le.minHeight = minHeight;
            return le;
        }

        public static CanvasGroup Group(this Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        public static string Mmss(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
