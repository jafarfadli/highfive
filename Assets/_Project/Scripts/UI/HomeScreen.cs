using System;
using System.Collections.Generic;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// Title screen as one page of key art: 명동길 at street level with the people this game is about
    /// (the student talking with the clinic nurse, the immigration officer and the bank teller beside them,
    /// built into the Home scene as "Cast_…"), the nurse's question in a speech balloon, and on the left a column
    /// on a fading halftone field: "READY SET / SEOUL!" in big block letters, the 서울 첫 주 tag, the tagline, a
    /// language dropdown and the start bar. Clicking anywhere (or Enter / Space) starts.
    /// </summary>
    public class HomeScreen : MonoBehaviour
    {
        RectTransform root, start, menu;
        bool leaving;
        bool intro = true;

        readonly List<(RectTransform balloon, Transform head, float lift)> balloons = new();

        void Start()
        {
            GameSession.I.CurrentLevel = null;
            CursorState.Gameplay(false);
            root = (RectTransform)UIKit.CreateCanvas("Home", 20).transform;
            Build();
        }

        void Build()
        {
            foreach (Transform c in root) Destroy(c.gameObject);
            balloons.Clear();
            menu = null;

            // the whole screen is the start button; everything else sits on top of it
            var hit = UIKit.Rect("ClickAnywhere", root).Stretch().gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.gameObject.AddComponent<Button>().onClick.AddListener(Go);

            // print dots behind the left column, fading out before the street
            var dots = UIKit.Rect("Dots", root).gameObject.AddComponent<HalftoneField>();
            dots.raycastTarget = false;
            dots.alpha = 0.62f;
            dots.density = 0.42f;
            dots.fadeStart = 0.42f;
            dots.rectTransform.anchorMin = new Vector2(0, 0);
            dots.rectTransform.anchorMax = new Vector2(0, 1);
            dots.rectTransform.pivot = new Vector2(0, 0.5f);
            dots.rectTransform.sizeDelta = new Vector2(1180, 0);
            dots.rectTransform.anchoredPosition = Vector2.zero;

            // the title, Ready Set Seoul: big block letters, "READY SET" stepping up into "SEOUL!"
            var readySet = Block("READY SET", 136, 15f);
            At(readySet.rectTransform, new Vector2(400, -150), new Vector2(820, 170));
            var seoul = Block("SEOUL!", 236, 22f);
            At(seoul.rectTransform, new Vector2(440, -306), new Vector2(900, 260));
            foreach (var t in new[] { readySet, seoul }) t.rectTransform.localRotation = Quaternion.Euler(0, 0, -6f);

            var tag = UIKit.BrushStrip(root, "Tag", Role.Night);
            At(tag.rectTransform, new Vector2(330, -478), new Vector2(300, 76));
            tag.rectTransform.localRotation = Quaternion.Euler(0, 0, 2.5f);
            tag.shadow = new Vector2(6, -7);
            tag.shadowRole = Role.Ink;
            UIKit.Words(tag.transform, "서울 첫 주", 44, Role.Paper, Face.DisplayKo, TextAnchor.MiddleCenter).rectTransform.Stretch();

            var tagline = UIKit.Rect("Tagline", root);
            tagline.Place(new Vector2(0, 1), new Vector2(92, -540), new Vector2(860, 0));
            UIKit.VStack(tagline, 0, new RectOffset(28, 28, 14, 16), TextAnchor.MiddleLeft);
            var card = UIKit.PaperPanel(tagline, "Card", Role.Paper);
            card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            card.rectTransform.Stretch();
            UIKit.Words(tagline, Loc.T("tagline"), 27, Role.Ink, Face.ReadBold).lineSpacing = 1.1f;

            LanguagePicker();

            // start, bottom left
            start = UIKit.Rect("Start", root);
            start.anchorMin = start.anchorMax = new Vector2(0, 0);
            start.pivot = new Vector2(0.5f, 0.5f);
            start.sizeDelta = new Vector2(600, 88);
            start.anchoredPosition = new Vector2(96 + 300, 80 + 44);
            start.localRotation = Quaternion.Euler(0, 0, -1.5f);
            var strip = UIKit.BrushStrip(start, "Strip", Role.Glow);
            strip.rectTransform.Stretch();
            strip.shadow = new Vector2(8, -9);
            strip.shadowRole = Role.Ink;
            var startRow = UIKit.Rect("Row", start).Stretch(36, 36, 0, 0);
            UIKit.HStack(startRow, 18, null, TextAnchor.MiddleCenter).childForceExpandHeight = false;
            UIKit.Words(startRow, "▶ 시작", 42, Role.OnBrush, Face.DisplayKo, TextAnchor.MiddleCenter).Layout(170, 60);
            UIKit.Words(startRow, Loc.T("click_to_start"), 26, Role.OnBrush, Face.ReadBold, TextAnchor.MiddleLeft).Layout(340, 60);

            // the nurse asks what the game is about: a real question at a real counter, with its translation
            var nurse = Balloon("Cast_Nurse", "어디가 불편하세요?", Loc.T("home_nurse"), 0.16f);

            if (!intro) return;
            _ = Motion.Slap(readySet.rectTransform, -6f, 0.1f, -14f, 0.28f);
            _ = Motion.Stamp(seoul.rectTransform, -6f, null, 0.22f);
            _ = Motion.Slap(tag.rectTransform, 2.5f, 0.5f);
            _ = Motion.SlideIn(tagline, new Vector2(-80, 0), 0.24f, 0.6f);
            _ = Motion.SlideIn(start, new Vector2(0, -160), 0.26f, 0.85f);
            if (nurse) _ = Motion.Slap(nurse, 0f, 1.1f, -6f);
        }

        /// <summary>Retro block lettering: cream letters, a thick ink outline and a rust block behind them.</summary>
        Text Block(string text, int size, float depth)
        {
            var t = UIKit.Lettering(root, text, size, Role.Balloon, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fx = t.GetComponent<InkText>();
            fx.outline = size * 0.055f;
            fx.directions = 16;
            fx.shadow = new Vector2(0.55f, -0.85f);
            fx.extrude = depth;
            fx.extrudeRole = Role.BrushDeep;
            return t;
        }

        /// <summary>Places a rect by its centre, measured from the top-left corner of the canvas.</summary>
        static void At(RectTransform rt, Vector2 centre, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centre;
            rt.sizeDelta = size;
        }

        /// <summary>The hint language as a dropdown: a card showing the current one; it opens a short list upward.</summary>
        void LanguagePicker()
        {
            var name = Loc.LanguageName(Settings.Language);
            var button = UIKit.InkButton(root, "", ToggleMenu, ButtonStyle.Paper, 24);
            var label = button.GetComponentInChildren<Text>();
            label.font = UIKit.Bold;
            label.fontSize = 24;
            label.text = $"{Loc.T("language")}:  {name}   ▼";
            var rt = (RectTransform)button.transform;
            rt.Place(new Vector2(0, 0), new Vector2(96, 196), new Vector2(label.preferredWidth + 60f, 60));
        }

        void ToggleMenu()
        {
            if (menu)
            {
                Destroy(menu.gameObject);
                menu = null;
                return;
            }
            Sfx.Play(ProceduralAudio.Tick, 0.5f);
            menu = UIKit.Rect("LanguageMenu", root).Stretch();
            // a click anywhere outside the list closes it instead of starting the game
            var blocker = menu.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            menu.gameObject.AddComponent<Button>().onClick.AddListener(ToggleMenu);
            var list = UIKit.Rect("List", menu);
            list.Place(new Vector2(0, 0), new Vector2(96, 266), new Vector2(320, 0));
            UIKit.VStack(list, 10, new RectOffset(16, 16, 16, 16), TextAnchor.UpperLeft);
            var card = UIKit.PaperPanel(list, "Card", Role.Paper);
            card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            card.rectTransform.Stretch();
            foreach (HintLanguage l in Enum.GetValues(typeof(HintLanguage)))
            {
                var value = l;
                bool chosen = Settings.Language == l;
                var b = UIKit.InkButton(list, "", () => Choose(value), ButtonStyle.Paper, 24);
                var body = b.GetComponentInChildren<InkShape>();
                if (chosen)
                {
                    body.fillRole = Role.Glow;
                    body.SetVerticesDirty();
                }
                var text = b.GetComponentInChildren<Text>();
                text.font = UIKit.Bold;
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleLeft;
                text.text = (chosen ? "✓  " : "     ") + Loc.LanguageName(l);
                ((RectTransform)b.transform).Layout(-1, 56);
            }
            _ = Motion.Slap(list, 0f, 0f, -3f, 0.2f);
        }

        void Choose(HintLanguage language)
        {
            if (Settings.Language != language)
            {
                Settings.Language = language;
                Settings.Save();
                Sfx.Play(ProceduralAudio.Tick, 0.6f);
            }
            intro = false;
            Build();
        }

        /// <summary>
        /// A speech balloon over one of the cast, its tail down at their head: the Korean line with its
        /// translation. Null when the person isn't in the scene.
        /// </summary>
        RectTransform Balloon(string personName, string korean, string meaning, float tailX)
        {
            var person = GameObject.Find(personName);
            var rig = person ? person.GetComponent<HumanRig>() : null;
            if (!rig || !rig.head) return null;
            var b = UIKit.Rect("Balloon", root);
            b.anchorMin = b.anchorMax = new Vector2(0.5f, 0.5f);
            UIKit.VStack(b, 2, new RectOffset(24, 24, 16, 18), TextAnchor.UpperLeft);
            var shape = UIKit.SpeechBalloon(b, new Vector2(tailX, -0.55f));
            shape.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            shape.rectTransform.Stretch();
            shape.radius = 22f;
            shape.tailWidth = 34f;
            var ko = UIKit.Words(b, korean, 26, Role.Ink, Face.ReadBold);
            var tr = UIKit.Words(b, meaning, 22, Role.Ink, Face.Read);
            float w = Mathf.Max(ko.preferredWidth, tr.preferredWidth) + 52f;
            b.sizeDelta = new Vector2(Mathf.Min(w, 460f), 0);
            b.pivot = new Vector2(tailX, 0f);
            balloons.Add((b, rig.head, 0.5f));
            KeepOver(b, rig.head, 0.5f);
            return b;
        }

        void LateUpdate()
        {
            foreach (var (balloon, head, lift) in balloons) KeepOver(balloon, head, lift);
        }

        /// <summary>Keeps a balloon over a head: its tail tip (below the balloon) lands just above the head.</summary>
        void KeepOver(RectTransform balloon, Transform head, float lift)
        {
            var cam = Camera.main;
            if (!balloon || !head || !cam) return;
            var screen = cam.WorldToScreenPoint(head.position + Vector3.up * lift);
            bool visible = screen.z > 0;
            if (balloon.gameObject.activeSelf != visible) balloon.gameObject.SetActive(visible);
            if (!visible) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local)) return;
            float tail = balloon.rect.height * 0.55f + 4f;
            balloon.anchoredPosition = local + new Vector2(0, tail);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) && !menu) Go();
            if (Settings.ReducedMotion || !start) return;
            // the start bar breathes
            start.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 3.4f));
        }

        void Go()
        {
            if (leaving) return;
            leaving = true;
            Sfx.Play(ProceduralAudio.Blip, 0.7f);
            SceneFlow.Load(SceneFlow.Select);
        }
    }
}
