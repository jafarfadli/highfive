using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// Per-scene heads-up display: a quest card top-left (the title on an orange band, place and part, a
    /// brush progress bar, the one to-do on a highlighter band), a thermal queue ticket right-middle, a
    /// keycap prompt bottom-centre, the controls help on a dark plate bottom-left, and lettered banners
    /// over the scene. No compass: the to-do, the open door and the signs show the way.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Current { get; private set; }

        const float Margin = 40f;

        public Canvas Canvas { get; private set; }

        InkShape crosshair;
        RectTransform promptBox;
        Text promptText;
        RectTransform questBox;
        Text questTitle, questSub, progressText, todoText;
        InkShape progressStroke, todoArrow;
        RectTransform todoRow;
        RectTransform ticketBox;
        Text ticketNumber, ticketServing, ticketAhead;
        InkShape ticketRing;
        bool ticketShown, ticketCalled;
        CanvasGroup bannerGroup;
        RectTransform bannerRoot;
        InkShape bannerSwash, bannerPlate;
        Text bannerBig, bannerSmall;
        CanvasGroup helpGroup;
        RectTransform helpBox;
        Text helpText;
        int bannerToken;
        float bannerUntil;
        Vector2 bannerHome;
        float helpUntil;
        bool helpLeaving;
        float progressTarget;

        public static Hud Create()
        {
            var canvas = UIKit.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.Canvas = canvas;
            hud.Build();
            Current = hud;
            return hud;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Build()
        {
            var root = (RectTransform)transform;

            crosshair = UIKit.Dot(root, Role.Paper, 2f, "Crosshair");
            crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
            crosshair.gameObject.SetActive(false);

            BuildPrompt(root);
            BuildQuest(root);
            BuildTicket(root);

            // controls help on a dark plate, bottom left; drops away when its time is up
            helpBox = UIKit.Rect("Help", root);
            helpBox.Place(new Vector2(0, 0), new Vector2(Margin, Margin), new Vector2(0, 48));
            var hh = UIKit.HStack(helpBox, 0, new RectOffset(24, 30, 0, 0), TextAnchor.MiddleLeft);
            hh.childForceExpandHeight = false;
            helpBox.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var plate = UIKit.BrushStrip(helpBox, "Plate", Role.Night);
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            plate.rectTransform.Stretch(-6, -12, 0, 0);
            plate.dry = 0.5f;
            helpText = UIKit.Words(helpBox, "", 22, Role.Paper, Face.ReadBold);
            helpText.horizontalOverflow = HorizontalWrapMode.Overflow;
            helpGroup = helpBox.Group();
            helpGroup.alpha = 0;

            // centre banner: lettering over a brush swash
            bannerRoot = UIKit.Rect("Banner", root);
            bannerRoot.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1500, 200));
            bannerHome = bannerRoot.anchoredPosition;
            bannerGroup = bannerRoot.Group();
            bannerGroup.alpha = 0;
            bannerSwash = UIKit.BrushStrip(bannerRoot, "Swash", Role.Brush);
            bannerSwash.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(980, 112));
            bannerSwash.rectTransform.localRotation = Quaternion.Euler(0, 0, -2f);
            bannerBig = UIKit.Lettering(bannerRoot, "", 72, Role.Paper, TextAnchor.MiddleCenter);
            bannerBig.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(1500, 100));
            bannerBig.horizontalOverflow = HorizontalWrapMode.Overflow;
            bannerPlate = UIKit.BrushStrip(bannerRoot, "Plate", Role.Night);
            bannerPlate.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(600, 54));
            bannerPlate.dry = 0.5f;
            bannerSmall = UIKit.Words(bannerRoot, "", 28, Role.Paper, Face.ReadBold, TextAnchor.MiddleCenter);
            bannerSmall.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(1500, 50));
        }

        void BuildPrompt(RectTransform root)
        {
            promptBox = UIKit.Rect("Prompt", root);
            promptBox.Place(new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(0, 66));
            var h = UIKit.HStack(promptBox, 16, new RectOffset(14, 34, 0, 0), TextAnchor.MiddleCenter);
            h.childForceExpandHeight = false;
            promptBox.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var strip = UIKit.BrushStrip(promptBox, "Strip", Role.Night);
            strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            strip.rectTransform.Stretch(-10, -16, 0, 0);
            strip.dry = 0.6f;
            UIKit.Keycap(promptBox, "E", 24);
            promptText = UIKit.Words(promptBox, "", 26, Role.Paper, Face.ReadBold);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptBox.gameObject.SetActive(false);
        }

        void BuildQuest(RectTransform root)
        {
            questBox = UIKit.Rect("Quest", root);
            questBox.Place(new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(480, 0));
            UIKit.VStack(questBox, 10, new RectOffset(26, 26, 22, 24), TextAnchor.UpperLeft);
            questBox.localRotation = Quaternion.Euler(0, 0, -1f);
            var card = UIKit.PaperPanel(questBox, "Card", Role.Paper);
            card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            card.rectTransform.Stretch();

            // the title on an orange band across the card
            var head = UIKit.Rect("Head", questBox);
            UIKit.VStack(head, 0, new RectOffset(16, 16, 6, 4), TextAnchor.MiddleLeft, false);
            var band = UIKit.BrushStrip(head, "Band", Role.Brush);
            band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            band.rectTransform.Stretch(-8, -8, 0, 0);
            band.dry = 0.4f;
            questTitle = UIKit.Words(head, "", 36, Role.OnBrush, Face.Display);
            questTitle.lineSpacing = 0.92f;
            questSub = UIKit.Words(questBox, "", 24, Role.Ink, Face.ReadBold);

            var progressRow = UIKit.Rect("Progress", questBox);
            progressRow.Layout(-1, 30);
            var track = UIKit.PenStroke(progressRow, Role.Ink, 2.5f, new Vector2(0, 0.3f), new Vector2(0.5f, 0.26f), new Vector2(1, 0.3f));
            track.fillAlpha = 0.35f;
            track.taper = 0.7f;
            track.rectTransform.Stretch(0, 70, 0, 0);
            progressStroke = UIKit.BrushStrip(progressRow, "Fill", Role.Brush);
            progressStroke.rectTransform.Stretch(0, 70, 3, 3);
            progressStroke.dry = 0.55f;
            progressStroke.rough = 1.4f;
            progressStroke.reveal = 0f;
            progressText = UIKit.Words(progressRow, "0%", 24, Role.Ink, Face.ReadBold, TextAnchor.MiddleRight);
            progressText.rectTransform.Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(64, 30));

            // the one to-do, on a highlighter band
            todoRow = UIKit.Rect("Todo", questBox);
            var th = UIKit.HStack(todoRow, 12, new RectOffset(8, 10, 6, 6), TextAnchor.UpperLeft);
            th.childForceExpandHeight = false;
            var marker = UIKit.Highlight(todoRow);
            marker.rectTransform.Stretch(-4, 0, 0, 0);
            var arrowSlot = UIKit.Rect("Arrow", todoRow);
            arrowSlot.Layout(44, 30);
            todoArrow = UIKit.Arrow(arrowSlot, Role.Brush, 4f);
            todoArrow.rectTransform.Stretch(0, 0, 2, 0);
            todoText = UIKit.Words(todoRow, "", 24, Role.Ink, Face.ReadBold);
            todoText.Layout(346);
            questBox.gameObject.SetActive(false);
        }

        void BuildTicket(RectTransform root)
        {
            ticketBox = UIKit.Rect("Ticket", root);
            ticketBox.Place(new Vector2(1, 0.5f), new Vector2(-Margin, 70), new Vector2(250, 320));
            ticketBox.localRotation = Quaternion.Euler(0, 0, 1.5f);
            var slip = UIKit.Ink(ticketBox, "Slip", InkKind.Ticket, Role.Balloon, 0f);
            slip.rectTransform.Stretch();
            slip.shadow = new Vector2(6, -7);
            slip.tooth = 12f;
            UIKit.Words(ticketBox, "번호표", 32, Role.Ink, Face.DisplayKo, TextAnchor.MiddleCenter).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(240, 44));
            var rule = UIKit.PenStroke(ticketBox, Role.Ink, 2f, new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            rule.fillAlpha = 0.5f;
            rule.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -74), new Vector2(190, 6));
            ticketNumber = UIKit.Lettering(ticketBox, "000", 104, Role.Brush, TextAnchor.MiddleCenter, Face.DisplayKo);
            ticketNumber.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(240, 120));
            ticketRing = UIKit.CircleMark(ticketBox, Role.Brush, 7f);
            ticketRing.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(230, 132));
            ticketRing.gameObject.SetActive(false);
            ticketServing = UIKit.Words(ticketBox, "", 24, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter);
            ticketServing.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(240, 34));
            ticketAhead = UIKit.Words(ticketBox, "", 24, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter);
            ticketAhead.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(240, 40));
            ticketBox.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (helpGroup.alpha > 0 && Time.unscaledTime > helpUntil && !helpLeaving) _ = HelpExit();
        }

        public void SetCrosshair(bool on)
        {
            if (crosshair.gameObject.activeSelf != on) crosshair.gameObject.SetActive(on);
        }

        public void SetPrompt(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (promptBox.gameObject.activeSelf != on)
            {
                promptBox.gameObject.SetActive(on);
                if (on) _ = Motion.Pop(promptBox, 1.08f, 0.18f);
            }
            if (on && promptText.text != text) promptText.text = text;
        }

        public void ShowHelp(string text, float seconds = 25f)
        {
            helpText.text = text;
            helpGroup.alpha = 1;
            helpUntil = Time.unscaledTime + seconds;
        }

        public void HideHelp() => helpGroup.alpha = 0;

        // the help line slips down off the page in three steps, then is gone (no cross-fade)
        async Task HelpExit()
        {
            helpLeaving = true;
            var rt = helpBox;
            var home = rt.anchoredPosition;
            float shownUntil = helpUntil;
            await Motion.Tween(this, 0.25f, k => rt.anchoredPosition = home + new Vector2(0, -20f * k), true);
            if (!this) return;
            rt.anchoredPosition = home;
            if (helpUntil == shownUntil) helpGroup.alpha = 0;
            helpLeaving = false;
        }

        /// <summary>Shows the quest tag for a level. (The colour argument is kept for callers; the theme decides.)</summary>
        public void SetQuest(string title, string subtitle, Color accent)
        {
            bool first = !questBox.gameObject.activeSelf;
            questBox.gameObject.SetActive(true);
            questTitle.text = title;
            questSub.text = subtitle ?? "";
            questSub.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            if (first) _ = Motion.Slap(questBox, -1f, 0.1f);
        }

        /// <summary>The one thing the player should do now: the arrow redraws and the line pops in.</summary>
        public void SetTask(string text)
        {
            bool changed = todoText.text != (text ?? "");
            todoText.text = text ?? "";
            todoRow.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!changed || string.IsNullOrEmpty(text)) return;
            _ = Motion.Draw(todoArrow, 0.25f);
            _ = Motion.Pop(todoText.rectTransform, 1.06f, 0.25f);
            _ = Motion.Pop(questBox, 1.03f, 0.25f);
            Sfx.Play(ProceduralAudio.Tick, 0.4f);
        }

        public void ShowTicket(int mine, int serving)
        {
            ticketBox.gameObject.SetActive(true);
            if (!ticketShown)
            {
                ticketShown = true;
                _ = Motion.Print(ticketBox, 0.5f);
            }
            var number = mine.ToString("000");
            if (ticketNumber.text != number) ticketNumber.text = number;
            ticketServing.text = $"{Loc.T("now_serving")} {serving:000}";
            int ahead = Mathf.Max(0, mine - serving);
            ticketAhead.text = ahead > 0 ? Loc.T("people_ahead", ahead) : Loc.T("your_turn");
            ticketAhead.GetComponent<Themed>().Set(ahead > 0 ? Role.Ink : Role.Brush);
            if (ahead == 0 && !ticketCalled)
            {
                ticketCalled = true;
                ticketRing.gameObject.SetActive(true);
                _ = Motion.Draw(ticketRing, 0.35f);
                _ = Motion.Pop(ticketNumber.rectTransform, 1.2f, 0.3f);
            }
        }

        public void HideTicket()
        {
            ticketBox.gameObject.SetActive(false);
            ticketShown = false;
            ticketCalled = false;
            ticketRing.gameObject.SetActive(false);
        }

        /// <summary>Quest progress: the brush stroke paints further and the percentage updates.</summary>
        public void SetProgress(int done, int total)
        {
            float k = total > 0 ? Mathf.Clamp01(done / (float)total) : 0;
            progressText.text = $"{Mathf.RoundToInt(k * 100)}%";
            progressTarget = k;
            _ = Fill();
        }

        async Task Fill()
        {
            float start = progressStroke.reveal, target = progressTarget;
            await Motion.Tween(progressStroke, 0.4f, k => progressStroke.SetReveal(Mathf.Lerp(start, target, k)), true);
        }

        public async void Banner(string big, string small, float seconds = 3f)
        {
            int token = ++bannerToken;
            bannerRoot.anchoredPosition = bannerHome;
            bannerRoot.localRotation = Quaternion.identity;
            bannerBig.text = big;
            bannerSmall.text = small ?? "";
            bannerPlate.gameObject.SetActive(!string.IsNullOrEmpty(small));
            bannerPlate.rectTransform.sizeDelta = new Vector2(bannerSmall.preferredWidth + 90f, 54);
            bannerGroup.alpha = 1;
            bannerSwash.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(bannerBig.preferredWidth + 160f, 500f, 1400f), 112);
            _ = Motion.Draw(bannerSwash, 0.25f);
            bannerUntil = Time.unscaledTime + seconds;
            await Motion.Slap(bannerBig.rectTransform, 0f, 0.06f, -5f, 0.28f);
            while (Time.unscaledTime < bannerUntil)
            {
                await Awaitable.NextFrameAsync();
                if (!this || token != bannerToken) return;
            }
            // exit: peeled off like a sticker, curling down-right while the swash is wiped away, in steps
            await Motion.Tween(this, 0.34f, k =>
            {
                if (token != bannerToken) return;
                float e = k * k;
                bannerRoot.anchoredPosition = bannerHome + new Vector2(26f * e, 10f * Mathf.Sin(k * Mathf.PI) - 130f * e);
                bannerRoot.localRotation = Quaternion.Euler(0, 0, 9f * e);
                bannerSwash.SetReveal(1f - Mathf.Clamp01((k - 0.25f) / 0.75f));
                bannerGroup.alpha = k < 0.999f ? 1f : 0f;
            }, true);
            if (!this || token != bannerToken) return;
            bannerGroup.alpha = 0f;
            bannerRoot.anchoredPosition = bannerHome;
            bannerRoot.localRotation = Quaternion.identity;
            bannerSwash.SetReveal(1f);
        }

        /// <summary>Peels the current banner off now, so it never covers a conversation.</summary>
        public void DismissBanner() => bannerUntil = 0f;
    }
}
