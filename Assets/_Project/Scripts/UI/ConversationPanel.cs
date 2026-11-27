using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Conversation;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>One answer as shown to the player.</summary>
    public class ChoiceView
    {
        public string Korean;
        public string Romanization;
        public string Meaning;
        /// <summary>The document this answer hands over: its Korean name and its name in the player's language.</summary>
        public string DocKorean, DocLocal;
        public bool Tried;
    }

    /// <summary>
    /// Counter conversation as a decision, laid out so the staff member stays in view: what they say runs in
    /// a dark dialogue box along the bottom (name on an orange band, the Korean line, its translation, "R hear
    /// again"), the answers stack up on the right as dark bars with keycaps 1–4, and the lesson of a wrong
    /// answer is a note on the left. Hovering lights a bar; the pick is circled; a tried answer is slashed;
    /// an answer that hands something over says what on a glow chip.
    /// </summary>
    public class ConversationPanel : MonoBehaviour
    {
        public event Action ReplayRequested;

        /// <summary>Kept for callers; the UI takes its colours from the palette.</summary>
        public Color Accent { get; set; }

        const float DialogWidth = 1300f, DialogBottom = 30f, ChoicesWidth = 660f, ChoicesBottom = 296f, Margin = 40f;

        FirstPersonController player;
        InputAction pickAction, replayAction, talkAction;

        // answering by voice: hold Space (or the keycap) and say the line
        RectTransform speak;
        Text speakLabel;
        InkShape speakRing;
        bool talking;
        // answers are said, never clicked (owner, 2026-11-26): a click or a number key only asks for the answer
        // out loud; an action in [brackets], which can't be said, is the one thing clicked

        /// <summary>Whether the last answer was said out loud, how well it came across, and what was heard.</summary>
        public bool LastPickSpoken { get; private set; }
        public float LastPickScore { get; private set; }
        public string LastHeard { get; private set; }

        CanvasGroup group;
        RectTransform dialog, choicesBox, feedbackBox, keypad;
        bool visible;
        Text speakerText, npcText, npcMeaningText, feedbackText;
        readonly List<InkShape> pinDots = new();
        readonly List<Answer> answers = new();
        List<ChoiceView> shown = new();

        TaskCompletionSource<int> pendingChoice;
        TaskCompletionSource<string> pendingPin;
        string keypadCode = "";
        int keypadLength = 4;
        Text keypadTitle;

        class Answer
        {
            public RectTransform Root, Body, Key;
            public InkShape Strip;
            public Text Korean, Sub, Stars;
            public bool Tried;
        }

        public static ConversationPanel Create(Hud hud, FirstPersonController player)
        {
            var root = UIKit.Rect("Conversation", hud.Canvas.transform).Stretch();
            var panel = root.gameObject.AddComponent<ConversationPanel>();
            panel.player = player;
            panel.Build(root);
            panel.Show(false);
            return panel;
        }

        void Build(RectTransform root)
        {
            group = root.Group();
            pickAction = player.Map.FindAction("Document", true);
            replayAction = player.Map.FindAction("Repeat", true);
            talkAction = player.Map.FindAction("PushToTalk", true);

            // the dialogue box along the bottom: what the staff member said
            dialog = UIKit.Rect("Dialog", root);
            dialog.Place(new Vector2(0.5f, 0), new Vector2(0, DialogBottom), new Vector2(DialogWidth, 0));
            UIKit.VStack(dialog, 6, new RectOffset(56, 230, 46, 30), TextAnchor.UpperLeft);
            var box = UIKit.PaperPanel(dialog, "Box", Role.Night, 4.5f);
            box.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            box.rectTransform.Stretch();
            box.radius = 10f;

            var name = UIKit.Rect("Name", dialog);
            name.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            name.Place(new Vector2(0, 1), new Vector2(40, 26), new Vector2(0, 52));
            name.localRotation = Quaternion.Euler(0, 0, -1.5f);
            var nh = UIKit.HStack(name, 0, new RectOffset(26, 26, 0, 0), TextAnchor.MiddleCenter);
            nh.childForceExpandHeight = false;
            name.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var nameBand = UIKit.BrushStrip(name, "Band", Role.Brush);
            nameBand.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            nameBand.rectTransform.Stretch();
            nameBand.shadow = new Vector2(5, -6);
            nameBand.shadowRole = Role.Ink;
            speakerText = UIKit.Words(name, "", 24, Role.OnBrush, Face.ReadBold);
            speakerText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var again = UIKit.Rect("Again", dialog);
            again.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            again.Place(new Vector2(1, 1), new Vector2(-34, -30), new Vector2(190, 44));
            var ah = UIKit.HStack(again, 10, null, TextAnchor.MiddleRight);
            ah.childForceExpandHeight = false;
            bool touch = Player.TouchControls.Active;
            var rKey = UIKit.Keycap(again, Core.VRMode.Active ? "B" : "R", 18);
            rKey.gameObject.AddComponent<Button>().onClick.AddListener(() => ReplayRequested?.Invoke());
            rKey.GetComponentInChildren<InkShape>().raycastTarget = true;
            var againWords = UIKit.Words(again, Loc.T("hear_again"), 22, Role.Paper, Face.ReadBold);
            againWords.Layout(130, 40);
            if (touch)
            {
                // a fingertip needs more than the keycap: the words take the tap too
                againWords.raycastTarget = true;
                again.gameObject.AddComponent<Button>().onClick.AddListener(() => ReplayRequested?.Invoke());
            }

            // say it: hold Space (or this keycap) and say the answer; a ring swells with the voice while listening
            speak = UIKit.Rect("Speak", dialog);
            speak.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            speak.Place(new Vector2(1, 1), new Vector2(-34, -86), new Vector2(220, 48));
            var sh = UIKit.HStack(speak, 10, null, TextAnchor.MiddleRight);
            sh.childForceExpandHeight = false;
            var spaceKey = UIKit.Keycap(speak, TalkKey, 18);
            spaceKey.GetComponentInChildren<InkShape>().raycastTarget = true;
            var hold = spaceKey.gameObject.AddComponent<HoldButton>();
            hold.Pressed += StartTalk;
            hold.Released += () => { if (Speech.Listening) _ = EndTalk(); };
            if (touch)
            {
                // on a touch screen the whole dialogue box is the "say it" control: hold it anywhere and speak
                box.raycastTarget = true;
                var holdBox = dialog.gameObject.AddComponent<HoldButton>();
                holdBox.Pressed += StartTalk;
                holdBox.Released += () => { if (Speech.Listening) _ = EndTalk(); };
            }
            speakRing = UIKit.CircleMark(spaceKey, Role.Glow, 4f);
            speakRing.rectTransform.Stretch(-12, -12, -12, -12);
            speakRing.gameObject.SetActive(false);
            speakLabel = UIKit.Words(speak, Loc.T("say_it"), 22, Role.Paper, Face.ReadBold);
            speakLabel.Layout(112, 40);
            speak.gameObject.SetActive(false);

            npcText = UIKit.Words(dialog, "", 34, Role.Paper, Face.ReadBold);
            npcText.lineSpacing = 1.1f;
            npcMeaningText = UIKit.Words(dialog, "", 24, Role.Glow, Face.Read);

            // the answers, stacked up on the right above the dialogue box
            choicesBox = UIKit.Rect("Choices", root);
            choicesBox.Place(new Vector2(1, 0), new Vector2(-Margin, ChoicesBottom), new Vector2(ChoicesWidth, 0));
            UIKit.VStack(choicesBox, 12, null, TextAnchor.LowerRight);

            // the lesson of the last answer: a note on the left, above the dialogue box
            feedbackBox = UIKit.Rect("Feedback", root);
            feedbackBox.Place(new Vector2(0, 0), new Vector2(Margin, ChoicesBottom), new Vector2(470, 0));
            UIKit.VStack(feedbackBox, 0, new RectOffset(26, 26, 22, 22), TextAnchor.UpperLeft);
            var note = UIKit.StickyNote(feedbackBox);
            note.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            note.rectTransform.Stretch();
            feedbackText = UIKit.Words(feedbackBox, "", 22, Role.Ink, Face.ReadBold);
            feedbackText.lineSpacing = 1.12f;
            feedbackBox.localRotation = Quaternion.Euler(0, 0, -2f);
            feedbackBox.gameObject.SetActive(false);

            BuildKeypad(root);
        }

        void BuildKeypad(RectTransform root)
        {
            keypad = UIKit.Rect("Keypad", root);
            // right of the staff member and left of the toast column (x ≥ 1456), so a toast never covers it
            keypad.Place(new Vector2(0.5f, 0.5f), new Vector2(310, 80), new Vector2(340, 560));
            keypad.localRotation = Quaternion.Euler(0, 0, 1.5f);
            var paper = UIKit.PaperPanel(keypad, "Paper", Role.Balloon, 3.5f);
            paper.rectTransform.Stretch();
            paper.radius = 22f;
            keypadTitle = UIKit.Words(keypad, Loc.T("keypad_title"), 26, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter);
            keypadTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(300, 40));
            pinDots.Clear();
            for (int i = 0; i < 4; i++)
            {
                var dot = UIKit.Dot(keypad, Role.Paper, 3f);
                dot.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(-75 + i * 50, -96), new Vector2(30, 30));
                pinDots.Add(dot);
            }
            var grid = UIKit.Rect("Grid", keypad);
            grid.Place(new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(300, 348));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(92, 78);
            g.spacing = new Vector2(12, 12);
            g.childAlignment = TextAnchor.MiddleCenter;
            foreach (var k in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "←", "0", "OK" })
            {
                var key = k;
                var style = key == "OK" ? ButtonStyle.Primary : key == "←" ? ButtonStyle.Night : ButtonStyle.Paper;
                UIKit.InkButton(grid, key, () => KeypadPress(key), style, 36);
            }
            keypad.gameObject.SetActive(false);
        }

        public void Show(bool on)
        {
            group.blocksRaycasts = on;
            group.interactable = on;
            if (on)
            {
                group.alpha = 1;
                if (!visible && isActiveAndEnabled)
                {
                    _ = Motion.SlideIn(dialog, new Vector2(0, -120), 0.26f);
                    Sfx.Play(ProceduralAudio.Paper, 0.45f);
                }
                visible = true;
                return;
            }
            visible = false;
            group.alpha = 0;
            dialog.anchoredPosition = new Vector2(0, DialogBottom);
            keypad.gameObject.SetActive(false);
            HideChoices();
            ClearFeedback();
        }

        public void SetSpeaker(string name, string role) => speakerText.text = $"{name} · {role}";

        /// <summary>Shows or hides the dialogue box alone (a phone that is still ringing has no one speaking yet).</summary>
        public void ShowDialog(bool on)
        {
            if (dialog) dialog.gameObject.SetActive(on);
        }

        public void ShowNpcLine(string korean, string meaning)
        {
            bool changed = npcText.text != korean;
            npcText.text = korean;
            npcMeaningText.text = meaning ?? "";
            npcMeaningText.gameObject.SetActive(!string.IsNullOrEmpty(meaning));
            if (changed && visible) _ = Motion.Pop(npcText.rectTransform, 1.03f, 0.2f);
        }

        public void ShowFeedback(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            bool was = feedbackBox.gameObject.activeSelf;
            feedbackBox.gameObject.SetActive(on);
            feedbackText.text = $"<color={Theme.Html(Role.BrushDeep)}>×</color>  " + text;
            if (on && !was)
            {
                _ = Motion.Slap(feedbackBox, -2f, 0f, -9f);
                Sfx.Play(ProceduralAudio.Paper, 0.6f);
            }
            else if (on) _ = Motion.Pop(feedbackBox, 1.06f, 0.2f);
        }

        public void ClearFeedback() => feedbackBox.gameObject.SetActive(false);

        public void HideChoices()
        {
            foreach (Transform c in choicesBox) Destroy(c.gameObject);
            answers.Clear();
            if (Speech.Listening) Speech.Cancel();
        }

        // ---------------------------------------------------------------- answering by voice

        /// <summary>An answer on screen that can be said (the SPACE control shows while there is one).</summary>
        bool HasSpeakable => pendingChoice != null && shown.Any(c => !c.Tried && KoreanMatch.Speakable(c.Korean));

        bool CanSpeak => HasSpeakable && Speech.Enabled;

        static string TalkKey => Core.VRMode.Active ? "A" : Player.TouchControls.Active ? "MIC" : "SPACE";

        void StartTalk()
        {
            if (talking || !HasSpeakable) return;
            // the microphone went away after the Home screen checked it: say so, there is no other way to answer
            if (!Speech.Enabled)
            {
                ShowFeedback(Loc.T("say_no_mic", TalkKey));
                return;
            }
            if (!Speech.Begin()) return;
            talking = true;
            Music.DuckFor(8f);
            speakLabel.text = Loc.T("listening");
            speakRing.gameObject.SetActive(true);
            _ = Motion.Draw(speakRing, 0.2f);
        }

        async Task EndTalk()
        {
            speakLabel.text = "…";
            var prompt = string.Join(" ", shown.Where(c => !c.Tried && KoreanMatch.Speakable(c.Korean)).Select(c => c.Korean));
            string heard = null;
            try
            {
                heard = await Speech.EndAsync(prompt);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (!this) return;
            talking = false;
            speakRing.gameObject.SetActive(false);
            speakLabel.text = Loc.T("say_it");
            Heard(heard);
        }

        /// <summary>
        /// What was heard picks the answer it matches, with how well it came across; otherwise the player is told what
        /// was heard and may try again or click. Public so play-tests can feed a transcript.
        /// </summary>
        public void Heard(string heard)
        {
            if (pendingChoice == null) return;
            if (string.IsNullOrWhiteSpace(heard))
            {
                ShowFeedback(Loc.T("say_nothing", TalkKey));
                return;
            }
            var candidates = shown.Select(c => c.Tried ? null : c.Korean).ToList();
            var m = KoreanMatch.Pick(heard, candidates);
            if (m.Index < 0)
            {
                Sfx.Play(ProceduralAudio.Soft, 0.5f);
                ShowFeedback(Loc.T("say_unclear", heard));
                return;
            }
            ClearFeedback();
            var missed = KoreanMatch.Missed(heard, shown[m.Index].Korean);
            SaidLine(answers[m.Index], heard, m.Score, missed);
            Resolve(m.Index, true, m.Score, heard);
        }

        /// <summary>Whether this answer has to be said rather than clicked (all but actions in [brackets]).</summary>
        bool MustSay(int index) => index >= 0 && index < shown.Count && KoreanMatch.Speakable(shown[index].Korean);

        /// <summary>A click on an answer or its number key: an action is done; anything else is asked for out loud.</summary>
        void Clicked(int index)
        {
            if (pendingChoice == null || index < 0 || index >= shown.Count || shown[index].Tried) return;
            if (!MustSay(index))
            {
                Pick(index);
                return;
            }
            Sfx.Play(ProceduralAudio.Soft, 0.5f);
            ShowFeedback(Loc.T(Speech.Enabled ? "say_required" : "say_no_mic", TalkKey));
            if (speak && speak.gameObject.activeInHierarchy) _ = Motion.Shake(speak, 6f, 0.3f);
        }

        /// <summary>Under the answer that was said: what was heard, how well it came across, and what didn't.</summary>
        static void SaidLine(Answer a, string heard, float score, List<string> missed)
        {
            if (a == null || !a.Korean) return;
            var text = $"{Loc.T("you_said")} {heard}  ·  {Mathf.RoundToInt(score * 100)}%";
            if (missed.Count > 0 && missed.Count <= 4) text += $"  ·  {Loc.T("say_missed")} {string.Join(" ", missed)}";
            var line = UIKit.Words(a.Korean.transform.parent, text, 22, Role.Glow, Face.ReadBold);
            line.lineSpacing = 1.05f;
        }

        /// <summary>Shows the answers and waits for one. Returns its index in <paramref name="choices"/>.</summary>
        public async Task<int> Choose(List<ChoiceView> choices, CancellationToken ct)
        {
            HideChoices();
            shown = choices;
            for (int i = 0; i < choices.Count; i++) answers.Add(BuildAnswer(i, choices[i]));
            // lay the column out now, so each answer slides in from where it will rest
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesBox);
            for (int i = 0; i < answers.Count; i++)
                if (!answers[i].Tried) _ = Motion.SlideIn(answers[i].Body, new Vector2(80, 0), 0.22f, 0.05f * i);
            pendingChoice = new TaskCompletionSource<int>();
            using (ct.Register(() => pendingChoice?.TrySetCanceled()))
            {
                try
                {
                    var picked = await pendingChoice.Task;
                    MarkPicked(picked);
                    return picked;
                }
                finally
                {
                    pendingChoice = null;
                }
            }
        }

        /// <summary>
        /// One answer: a dark bar with its keycap, the Korean line, romanization · meaning below it and, for an
        /// answer that hands something over, a glow chip saying what. The body holds everything, so hovering
        /// tilts and lifts the whole answer.
        /// </summary>
        Answer BuildAnswer(int index, ChoiceView c)
        {
            var a = new Answer { Tried = c.Tried };
            a.Root = UIKit.Rect("Answer", choicesBox);
            UIKit.VStack(a.Root, 0, null, TextAnchor.UpperLeft, false);
            a.Body = UIKit.Rect("Body", a.Root);
            // the right padding keeps text and chips clear of the bar's dry brush end
            var row = UIKit.HStack(a.Body, 18, new RectOffset(24, 52, 16, 16), TextAnchor.MiddleLeft);
            row.childForceExpandHeight = false;
            a.Strip = UIKit.BrushStrip(a.Body, "Strip", Role.Night);
            a.Strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            a.Strip.rectTransform.Stretch();
            a.Strip.raycastTarget = true;
            a.Strip.dry = 0.75f;
            a.Strip.shadow = new Vector2(6, -7);
            a.Strip.shadowRole = Role.Ink;

            a.Key = UIKit.Keycap(a.Body, c.Tried ? "×" : (index + 1).ToString(), 24, c.Tried ? Role.PaperDeep : Role.Paper);

            var text = UIKit.Rect("Text", a.Body);
            UIKit.VStack(text, 3, null, TextAnchor.UpperLeft, false);
            text.Layout(0, -1, 1).minWidth = 0;
            a.Korean = UIKit.Words(text, c.Korean, 28, Role.Paper, Face.ReadBold);
            // an answer in [brackets] is something the player does, not a line to say, so it has no
            // romanization: the tag says so in its place
            bool action = c.Korean != null && c.Korean.TrimStart().StartsWith("[");
            string tag = c.Tried ? $"▶ {Loc.T("answer_action")}" : $"<color={Theme.Html(Role.Glow)}>▶ {Loc.T("answer_action")}</color>";
            // the level sets how much help the card gives: Easy romanization and translation, Medium the
            // romanization only, Hard the Korean alone (an action keeps its tag, which is not a translation)
            var level = Settings.Difficulty;
            var pron = action ? tag : level == Difficulty.Hard ? null : c.Romanization;
            var meaning = level == Difficulty.Easy ? c.Meaning : null;
            var sub = string.Join("  ·  ", new[] { pron, meaning }.Where(s => !string.IsNullOrEmpty(s)));
            a.Sub = UIKit.Words(text, sub, 22, Role.Paper, Face.ReadBold);
            a.Sub.lineSpacing = 1.05f;
            a.Sub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            if (!string.IsNullOrEmpty(c.DocKorean))
                DocChip(text, c.DocKorean, level == Difficulty.Easy ? c.DocLocal : null, ChoicesWidth - 24f - 52f - 42f - 18f);

            a.Stars = UIKit.Words(a.Body, "★ ★", 28, Role.Glow, Face.Display, TextAnchor.UpperRight);
            a.Stars.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            a.Stars.rectTransform.Stretch(0, 22, 10, 0);
            a.Stars.gameObject.SetActive(false);

            if (c.Tried)
            {
                // set aside, never see-through: a softer bar, muted lettering, crossed out by hand
                a.Strip.fillRole = Role.InkSoft;
                a.Korean.GetComponent<Themed>().Set(Role.PaperDeep);
                a.Sub.GetComponent<Themed>().Set(Role.PaperDeep);
                // the slash in the glow of the lesson note, so it stands out on the ink-soft bar
                var slash = UIKit.PenStroke(a.Korean.transform, Role.Glow, 5f, new Vector2(0f, 0.42f), new Vector2(0.55f, 0.6f), new Vector2(1f, 0.5f));
                slash.rectTransform.anchorMin = new Vector2(0, 0.5f);
                slash.rectTransform.anchorMax = new Vector2(0, 0.5f);
                slash.rectTransform.pivot = new Vector2(0, 0.5f);
                slash.rectTransform.anchoredPosition = new Vector2(-6, 0);
                slash.rectTransform.sizeDelta = new Vector2(Mathf.Min(a.Korean.preferredWidth + 12f, ChoicesWidth - 140f), 24);
                return a;
            }

            var btn = a.Root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = a.Strip;
            btn.onClick.AddListener(() => Clicked(index));
            var hover = a.Root.gameObject.AddComponent<InkHover>();
            hover.visual = a.Body;
            hover.tilt = -0.8f;
            hover.lift = 1.02f;
            hover.Hovered += hot => Light(a, hot);
            return a;
        }

        static void Light(Answer a, bool hot)
        {
            if (!a.Strip) return;
            a.Strip.fillRole = hot ? Role.Brush : Role.Night;
            a.Strip.SetVerticesDirty();
            a.Korean.GetComponent<Themed>().Set(hot ? Role.OnBrush : Role.Paper);
            a.Sub.GetComponent<Themed>().Set(hot ? Role.OnBrush : Role.Paper);
            a.Stars.gameObject.SetActive(hot);
        }

        /// <summary>
        /// What an answer hands over: a glow tag with a document icon and "Hand over: 여권 · Passport" in the
        /// player's language, wrapping onto a second line when the name is long.
        /// </summary>
        static void DocChip(Transform parent, string korean, string local, float maxWidth)
        {
            var slot = UIKit.Rect("Document", parent);
            var chip = UIKit.Rect("Chip", slot);
            var tag = UIKit.Ink(chip, "Tag", InkKind.Panel, Role.Glow, 3.5f);
            tag.radius = 8f;
            tag.shadow = new Vector2(4, -5);
            tag.rectTransform.Stretch();
            var icon = UIKit.DocIcon(chip);
            icon.Place(new Vector2(0, 0.5f), new Vector2(14, 1), new Vector2(26, 34));
            var label = UIKit.Words(chip, Loc.T("hand_over", string.IsNullOrEmpty(local) ? korean : $"{korean} · {local}"), 22, Role.Ink, Face.ReadBold);
            label.lineSpacing = 1.0f;
            float width = Mathf.Min(label.preferredWidth + 4f, maxWidth - 74f);
            label.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(52, 1), new Vector2(width, 38));
            float height = Mathf.Max(38f, label.preferredHeight);
            label.rectTransform.sizeDelta = new Vector2(width, height);
            chip.Place(new Vector2(0, 0.5f), new Vector2(0, -2), new Vector2(width + 70f, height + 12f));
            chip.localRotation = Quaternion.Euler(0, 0, 1f);
            slot.Layout(-1, height + 18f);
        }

        void MarkPicked(int picked)
        {
            for (int i = 0; i < answers.Count; i++)
            {
                var a = answers[i];
                if (!a.Root) continue;
                var btn = a.Root.GetComponent<Button>();
                if (btn) btn.interactable = false;
                if (i == picked)
                {
                    Light(a, true);
                    a.Stars.gameObject.SetActive(false);
                    var ring = UIKit.CircleMark(a.Key, Role.Ink, 6f);
                    ring.rectTransform.Stretch(-22, -22, -20, -20);
                    _ = Motion.Draw(ring, 0.3f);
                }
                else _ = Motion.Peel(a.Body, false);
            }
        }

        /// <summary>
        /// The pad: a 4-digit PIN by default, or a phone's dial pad (<paramref name="length"/> digits, e.g. 119)
        /// with its own <paramref name="title"/>.
        /// </summary>
        public async Task<string> Keypad(CancellationToken ct, int length = 4, string title = null)
        {
            HideChoices();
            keypadCode = "";
            keypadLength = Mathf.Clamp(length, 1, pinDots.Count);
            keypadTitle.text = string.IsNullOrEmpty(title) ? Loc.T("keypad_title") : title;
            for (int i = 0; i < pinDots.Count; i++)
            {
                pinDots[i].gameObject.SetActive(i < keypadLength);
                pinDots[i].rectTransform.anchoredPosition = new Vector2((i - (keypadLength - 1) / 2f) * 50f, -96);
            }
            UpdateKeypadDots();
            keypad.gameObject.SetActive(true);
            _ = Motion.Slap(keypad, 1.5f);
            pendingPin = new TaskCompletionSource<string>();
            using (ct.Register(() => pendingPin?.TrySetCanceled()))
            {
                try
                {
                    return await pendingPin.Task;
                }
                finally
                {
                    pendingPin = null;
                    if (keypad) keypad.gameObject.SetActive(false);
                }
            }
        }

        // Automation hooks for editor play-tests (Pick chooses an answer whether or not it has to be said).
        public bool IsWaiting => pendingChoice != null || pendingPin != null;
        public bool WaitingForPin => pendingPin != null;
        public string CurrentLine => $"{speakerText.text} | {npcText.text}";
        public IReadOnlyList<ChoiceView> CurrentChoices => shown;
        public void EnterPin(string pin) => pendingPin?.TrySetResult(pin);

        public void Pick(int index) => Resolve(index, false, 0f, null);

        void Resolve(int index, bool spoken, float score, string heard)
        {
            if (pendingChoice == null || index < 0 || index >= shown.Count || shown[index].Tried) return;
            if (Speech.Listening) Speech.Cancel();
            talking = false;
            LastPickSpoken = spoken;
            LastPickScore = score;
            LastHeard = heard;
            Sfx.Play(ProceduralAudio.Blip, 0.6f);
            pendingChoice.TrySetResult(index);
        }

        void Update()
        {
            if (group.alpha <= 0) return;
            if (replayAction.WasPressedThisFrame()) ReplayRequested?.Invoke();

            // say it: shown while there is an answer that can be said; held Space records, letting go hears it
            bool hasSpeakable = HasSpeakable;
            if (speak.gameObject.activeSelf != (hasSpeakable || talking)) speak.gameObject.SetActive(hasSpeakable || talking);
            if (hasSpeakable && !talking && (talkAction.WasPressedThisFrame() || VRUI.FallbackTalkPressed)) StartTalk();
            if (Speech.Listening && (talkAction.WasReleasedThisFrame() || VRUI.FallbackTalkReleased)) _ = EndTalk();
            if (Speech.Listening)
            {
                float k = 1f + Speech.Level() * 0.35f;
                speakRing.rectTransform.localScale = Vector3.Lerp(speakRing.rectTransform.localScale, new Vector3(k, k, 1), 0.4f);
            }

            if (pendingChoice != null && pickAction.WasPressedThisFrame() && pickAction.activeControl != null &&
                int.TryParse(pickAction.activeControl.name, out var n))
                Clicked(n - 1);

            var kb = Keyboard.current;
            if (pendingPin == null || kb == null) return;
            for (int d = 0; d <= 9; d++)
            {
                var key = kb[(Key)((int)Key.Digit1 + (d == 0 ? 9 : d - 1))];
                var pad = kb[(Key)((int)Key.Numpad0 + d)];
                if (key.wasPressedThisFrame || pad.wasPressedThisFrame) KeypadPress(d.ToString());
            }
            if (kb.backspaceKey.wasPressedThisFrame) KeypadPress("←");
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) KeypadPress("OK");
        }

        void KeypadPress(string key)
        {
            if (pendingPin == null) return;
            Sfx.Play(ProceduralAudio.Tick, 0.5f);
            if (key == "←")
            {
                if (keypadCode.Length > 0) keypadCode = keypadCode.Substring(0, keypadCode.Length - 1);
            }
            else if (key != "OK" && keypadCode.Length < keypadLength) keypadCode += key;
            UpdateKeypadDots();
            if (keypadCode.Length == keypadLength && key != "←") pendingPin.TrySetResult(keypadCode);
        }

        void UpdateKeypadDots()
        {
            for (int i = 0; i < pinDots.Count; i++)
            {
                var dot = pinDots[i];
                var role = i < keypadCode.Length ? Role.Ink : Role.Paper;
                if (dot.fillRole == role) continue;
                dot.fillRole = role;
                dot.SetVerticesDirty();
                if (role == Role.Ink) _ = Motion.Pop(dot.rectTransform, 1.3f, 0.18f);
            }
        }
    }
}
