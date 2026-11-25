using System;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// The page before a level: an ink page with the place and part in a caption box and the background story
    /// hand-lettered on a sheet of paper, like the opening panel of a comic chapter. Click anywhere (or Enter /
    /// Space) to begin; the paper peels off and the page turns away.
    /// </summary>
    public class StoryCard : Modal
    {
        public static StoryCard Current { get; private set; }

        Action onStart;
        RectTransform page, sheet, caption, prompt;
        float shownAt;
        bool leaving;

        public static StoryCard Show(FirstPersonController player, string story, Action onStart)
        {
            var go = new GameObject("StoryCard", typeof(RectTransform));
            var card = go.AddComponent<StoryCard>();
            card.onStart = onStart;
            card.Build(card.Open(player, "StoryCanvas", 95), story);
            Current = card;
            return card;
        }

        void Build(RectTransform root, string story)
        {
            // the scrim from Modal is replaced by a full ink page
            var scrim = root.Find("Scrim");
            if (scrim) Destroy(scrim.gameObject);

            page = UIKit.Rect("Page", root).Stretch();
            var ink = UIKit.Ink(page, "Ink", InkKind.Panel, Role.Ink, 0f);
            ink.rectTransform.Stretch(-40, -40, -40, -40);
            ink.shadow = Vector2.zero;
            ink.radius = 0f;
            ink.rough = 0f;
            ink.tear = 0f;
            ink.grain = 1.6f;
            ink.raycastTarget = true;
            page.gameObject.AddComponent<Button>().onClick.AddListener(Begin);
            var dots = UIKit.Ink(page, "Dots", InkKind.Panel, Role.Night, 0f);
            dots.rectTransform.Stretch(-40, -40, -40, -40);
            dots.shadow = Vector2.zero;
            dots.radius = 0f;
            dots.rough = 0f;
            dots.tear = 0f;
            dots.halftone = 0.22f;

            var level = GameSession.I.CurrentLevel;
            var place = level?.Place;

            // caption box: where we are
            caption = UIKit.Rect("Caption", page);
            caption.Place(new Vector2(0.5f, 0.5f), new Vector2(-470, 300), new Vector2(560, 110));
            caption.localRotation = Quaternion.Euler(0, 0, -2f);
            var capPaper = UIKit.PaperPanel(caption, "Box", Role.Glow, 3f);
            capPaper.rectTransform.Stretch();
            capPaper.radius = 2f;
            capPaper.shadowAlpha = 0f;
            var capKo = UIKit.Words(caption, place?.nameKo ?? "서울", 40, Role.Ink, Face.DisplayKo);
            capKo.rectTransform.Stretch(26, 20, 10, 50);
            var capSub = UIKit.Words(caption, level?.FullTitle ?? "", 24, Role.Ink, Face.ReadBold);
            capSub.rectTransform.Stretch(26, 20, 60, 10);

            // the story, hand-lettered on paper
            sheet = UIKit.Rect("Sheet", page);
            sheet.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1300, 0));
            UIKit.VStack(sheet, 0, new RectOffset(70, 70, 56, 60), TextAnchor.MiddleCenter);
            var paper = UIKit.PaperPanel(sheet, "Paper", Role.Paper, 3.5f);
            paper.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            paper.rectTransform.Stretch();
            paper.shadow = new Vector2(10, -12);
            paper.shadowRole = Role.Night;
            paper.shadowAlpha = 1f;
            var text = UIKit.Words(sheet, story, 46, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter);
            text.lineSpacing = 1.12f;
            sheet.localRotation = Quaternion.Euler(0, 0, 1f);

            // prompt
            prompt = UIKit.Rect("Prompt", page);
            prompt.Place(new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(560, 76));
            var strip = UIKit.BrushStrip(prompt, "Strip", Role.Brush);
            strip.rectTransform.Stretch();
            strip.boil = true;
            UIKit.Words(prompt, "▶  " + Loc.T("click_to_start"), 30, Role.OnBrush, Face.ReadBold, TextAnchor.MiddleCenter).rectTransform.Stretch(20, 20, 0, 0);

            shownAt = Time.unscaledTime;
            _ = Motion.Slap(caption, -2f, 0.05f);
            _ = Motion.Slap(sheet, 1f, 0.18f, -6f, 0.3f);
            _ = Motion.Draw(strip, 0.3f, 0.5f);
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Begin();
        }

        /// <summary>Starts the level (also used by editor play-tests).</summary>
        public async void Begin()
        {
            // ignore the click that may still be held from the previous screen
            if (leaving || Time.unscaledTime - shownAt < 0.4f) return;
            leaving = true;
            Sfx.Play(ProceduralAudio.Blip, 0.6f);
            _ = Motion.Peel(caption, false);
            _ = Motion.Peel(sheet, false);
            var home = page.anchoredPosition;
            await Motion.Tween(page, 0.42f, k =>
            {
                float e = k * k;
                page.anchoredPosition = home + new Vector2(0, 1200f * e);
                page.localRotation = Quaternion.Euler(0, 0, -6f * e);
            }, true, 0.12f);
            if (!this) return;
            if (Current == this) Current = null;
            Close();
            onStart?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (Current == this) Current = null;
            base.OnDestroy();
        }
    }
}
