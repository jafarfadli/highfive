using System;
using System.Linq;
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
        RectTransform page, sheet, caption, prompt, pickRow;
        Text storyText;
        float shownAt;
        bool leaving, picking;
        LevelDef level;
        System.Collections.Generic.List<SituationQuestion> questions;
        int asking;
        string baseStory;

        /// <summary>The player still has to answer a question (a personalised visit).</summary>
        public bool Picking => picking;

        /// <summary>The question being asked (0 = the first).</summary>
        public int Asking => asking;

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
            storyText = text;
            sheet.localRotation = Quaternion.Euler(0, 0, 1f);

            // prompt
            prompt = UIKit.Rect("Prompt", page);
            prompt.Place(new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(560, 76));
            var strip = UIKit.BrushStrip(prompt, "Strip", Role.Brush);
            strip.rectTransform.Stretch();
            strip.boil = true;
            UIKit.Words(prompt, "▶  " + Loc.T("click_to_start"), 30, Role.OnBrush, Face.ReadBold, TextAnchor.MiddleCenter).rectTransform.Stretch(20, 20, 0, 0);

            this.level = level;
            baseStory = story;
            questions = level?.situations;
            if (questions != null && questions.Count > 0) Ask(0);
            else ShowFacts();

            shownAt = Time.unscaledTime;
            _ = Motion.Slap(caption, -2f, 0.05f);
            _ = Motion.Slap(sheet, 1f, 0.18f, -6f, 0.3f);
            _ = Motion.Draw(strip, 0.3f, 0.5f);
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
        }

        /// <summary>A personalised visit: question <paramref name="question"/> on the sheet and one card per option under it (keys 1, 2, …).</summary>
        void Ask(int question)
        {
            asking = question;
            if (question >= questions.Count)
            {
                Told();
                return;
            }
            var q = questions[question];
            if (q.options == null || q.options.Count == 0)
            {
                Ask(question + 1);
                return;
            }
            if (q.options.Count == 1)
            {
                GameSession.I.PickSituation(q.id, q.options[0]);
                Ask(question + 1);
                return;
            }
            picking = true;
            prompt.gameObject.SetActive(false);
            storyText.text = q.question?.Get() ?? Loc.T("pick_situation");
            storyText.fontSize = 42;
            if (pickRow) Destroy(pickRow.gameObject);
            var situations = q.options;
            pickRow = UIKit.Rect("Situations", page);
            pickRow.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1400, 120));
            var h = UIKit.HStack(pickRow, 34, null, TextAnchor.MiddleCenter);
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            for (int i = 0; i < situations.Count; i++)
            {
                int index = i;
                var card = UIKit.Rect("Situation", pickRow);
                var hc = UIKit.HStack(card, 16, new RectOffset(26, 34, 0, 0), TextAnchor.MiddleLeft);
                hc.childForceExpandHeight = false;
                hc.childForceExpandWidth = false;
                var body = UIKit.PaperPanel(card, "Card", Role.Paper, 3.5f);
                body.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                body.rectTransform.Stretch();
                body.raycastTarget = true;
                body.shadow = new Vector2(8, -9);
                UIKit.Keycap(card, (i + 1).ToString(), 24, Role.Brush, Role.Ink, Role.OnBrush);
                var t = UIKit.Words(card, situations[i].title?.Get(), 32, Role.Ink, Face.ReadBold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.Layout(t.preferredWidth + 4, 60);
                card.Layout(t.preferredWidth + 26 + 34 + 16 + 44, 110);
                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = body;
                btn.onClick.AddListener(() => Pick(index));
                var hover = card.gameObject.AddComponent<InkHover>();
                hover.visual = body.rectTransform;
                hover.body = body;
                card.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -1.5f : 1.2f);
                _ = Motion.Slap(card, i % 2 == 0 ? -1.5f : 1.2f, 0.3f + 0.08f * i);
            }
        }

        /// <summary>Answers the question being asked: its flags shape the visit; then the next question, or the story.</summary>
        public void Pick(int index)
        {
            if (!picking || questions == null || asking >= questions.Count) return;
            var q = questions[asking];
            if (index < 0 || index >= q.options.Count) return;
            GameSession.I.PickSituation(q.id, q.options[index]);
            picking = false;
            Sfx.Play(ProceduralAudio.Paper, 0.6f);
            Ask(asking + 1);
        }

        /// <summary>Every question answered: the story the answers tell, the facts that fit it, and the start bar.</summary>
        void Told()
        {
            picking = false;
            if (pickRow) Destroy(pickRow.gameObject);
            var parts = questions.Select(q => GameSession.I.Picked(q.id)?.story?.Get()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            storyText.fontSize = 46;
            storyText.text = parts.Count > 0 ? string.Join("\n", parts) : baseStory;
            prompt.gameObject.SetActive(true);
            // the click that picked doesn't also start the level
            shownAt = Time.unscaledTime;
            _ = Motion.Slap(sheet, 1f, 0f, -4f, 0.2f);
            ShowFacts();
        }

        /// <summary>Know before you go: the facts that fit, as notes along the bottom above the start bar.</summary>
        void ShowFacts()
        {
            var facts = KnowBefore.For(level, GameSession.I.Has);
            if (facts.Count == 0) return;
            var block = UIKit.Rect("KnowBefore", page);
            block.Place(new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(1720, 0));
            UIKit.VStack(block, 10, null, TextAnchor.LowerCenter, false);
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var heading = UIKit.Words(block, Loc.T("know_before").ToUpperInvariant(), 26, Role.Glow, Face.Display, TextAnchor.MiddleLeft);
            heading.Layout(-1, 32);
            var row = UIKit.Rect("Facts", block);
            var h = UIKit.HStack(row, 20, null, TextAnchor.UpperLeft);
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            float w = Mathf.Min(420f, (1720f - 20f * (facts.Count - 1)) / facts.Count);
            for (int i = 0; i < facts.Count; i++)
            {
                var c = KnowBefore.Card(row, facts[i], w);
                float tilt = i % 2 == 0 ? -1.2f : 1f;
                c.localRotation = Quaternion.Euler(0, 0, tilt);
                _ = Motion.Slap(c, tilt, 0.3f + 0.07f * i);
            }

            // the story hangs just under the caption box (growing downwards, so a long one never covers the
            // caption), wider than before, and its type gets smaller until it stays clear of the notes
            LayoutRebuilder.ForceRebuildLayoutImmediate(block);
            const float top = 222f, sheetWidth = 1440f, padding = 56f + 60f;
            float notesTop = -540f + 168f + LayoutUtility.GetPreferredHeight(block);
            float textRoom = top - notesTop - 24f - padding;
            sheet.pivot = new Vector2(0.5f, 1f);
            sheet.anchoredPosition = new Vector2(0, top);
            sheet.sizeDelta = new Vector2(sheetWidth, sheet.sizeDelta.y);
            for (int size = Mathf.Min(storyText.fontSize, 40); size >= 28; size -= 2)
            {
                storyText.fontSize = size;
                var fit = storyText.GetGenerationSettings(new Vector2(sheetWidth - 140f, 0));
                if (storyText.cachedTextGeneratorForLayout.GetPreferredHeight(storyText.text, fit) / storyText.pixelsPerUnit <= textRoom) break;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (picking)
            {
                var options = questions[asking].options;
                for (int i = 0; i < options.Count && i < 9; i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Pick(i);
                return;
            }
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Begin();
        }

        /// <summary>Starts the level (also used by editor play-tests).</summary>
        public async void Begin()
        {
            // ignore the click that may still be held from the previous screen, and wait for a situation
            if (picking || leaving || Time.unscaledTime - shownAt < 0.4f) return;
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
