using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// End of a level as a page from the notebook: the teacher's stamp (참 잘했어요 when done), a report card with
    /// progress, wrong answers and time, the lesson on a brush strip, mistakes on sticky notes, and the phrase card
    /// taped on as an index card to keep.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [Tooltip("Street photos by entrance id (clinic, immigration, bank), shown behind the results.")]
        public string[] backdropIds;
        public Texture2D[] backdrops;

        ScenarioResult result;
        RectTransform root;

        void Start()
        {
            CursorState.Gameplay(false);
            result = GameSession.I.LastResult ?? Demo();
            var canvas = UIKit.CreateCanvas("Results", 20);
            root = (RectTransform)canvas.transform;
            _ = Build();
        }

        async Task Build()
        {
            bool done = result.Outcome == ScenarioOutcome.Completed;

            // the street the player just came from, under ink
            var entrance = GameSession.I.CurrentLevel?.entrance;
            int k = backdropIds == null || entrance == null ? -1 : System.Array.IndexOf(backdropIds, entrance);
            if (k >= 0 && k < backdrops.Length && backdrops[k])
            {
                var photo = UIKit.Rect("Backdrop", root).gameObject.AddComponent<RawImage>();
                photo.texture = backdrops[k];
                photo.raycastTarget = false;
                var fit = photo.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backdrops[k].width / (float)backdrops[k].height;
            }
            UIKit.InkScrim(root, 0.66f, 0.38f);

            // a kraft notebook page on the left: the teacher's stamp at the top, the notes stuck below it
            var page = UIKit.Rect("Page", root);
            page.Place(new Vector2(0, 0), new Vector2(56, 44), new Vector2(460, 980));
            page.localRotation = Quaternion.Euler(0, 0, -1.6f);
            var kraft = UIKit.PaperPanel(page, "Kraft", Role.PaperDeep, 3f);
            kraft.rectTransform.Stretch();
            kraft.shadow = new Vector2(10, -12);
            string stampWord = done ? "참\n잘했어요!" : result.Outcome == ScenarioOutcome.Blocked ? "다시\n해 봐요" : "다음에\n또!";
            var stamp = UIKit.StampMark(page, stampWord, 330f, done ? Role.Brush : Role.BrushDeep, 62);
            stamp.Place(new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(330, 330));
            stamp.localRotation = Quaternion.Euler(0, 0, -12f);
            stamp.Group().alpha = 0f;

            // headline
            var outcome = done ? Loc.T("outcome_completed") : result.Outcome == ScenarioOutcome.Blocked ? Loc.T("outcome_blocked") : Loc.T("outcome_abandoned");
            var head = UIKit.Lettering(root, outcome.ToUpperInvariant(), 84);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(560, -54), new Vector2(1300, 110));
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.rectTransform.localRotation = Quaternion.Euler(0, 0, 1.5f);
            var title = UIKit.Words(root, result.Title, 32, Role.Paper, Face.ReadBold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(574, -170), new Vector2(1250, 46));
            Outline(title);

            // the report card
            var card = UIKit.Rect("Report", root);
            card.Place(new Vector2(0, 1), new Vector2(560, -240), new Vector2(700, 0));
            UIKit.VStack(card, 18, new RectOffset(40, 40, 36, 34), TextAnchor.UpperLeft);
            card.localRotation = Quaternion.Euler(0, 0, -1f);
            var paper = UIKit.PaperPanel(card, "Paper", Role.Paper, 3.5f);
            paper.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            paper.rectTransform.Stretch();
            paper.shadow = new Vector2(10, -12);

            var stats = UIKit.Rect("Stats", card);
            UIKit.HStack(stats, 10, null, TextAnchor.UpperLeft).childForceExpandWidth = true;
            stats.Layout(-1, 104);
            Stat(stats, Loc.T("progress"), $"{Mathf.RoundToInt(result.Progress * 100)}%");
            Stat(stats, Loc.T("mistakes"), result.Mistakes.ToString());
            Stat(stats, Loc.T("time"), UIKit.Mmss(result.Seconds));

            var barRow = UIKit.Rect("Bar", card);
            barRow.Layout(-1, 28);
            var track = UIKit.PenStroke(barRow, Role.Ink, 2.5f, new Vector2(0, 0.3f), new Vector2(0.5f, 0.25f), new Vector2(1, 0.3f));
            track.fillAlpha = 0.3f;
            track.rectTransform.Stretch();
            var bar = UIKit.BrushStrip(barRow, "Fill", done ? Role.Brush : Role.Glow);
            bar.rectTransform.Stretch(0, 0, 2, 2);
            bar.dry = 0.5f;
            bar.reveal = 0f;

            if (!string.IsNullOrEmpty(result.Lesson))
            {
                var lesson = UIKit.Rect("Lesson", card);
                UIKit.VStack(lesson, 0, new RectOffset(30, 30, 16, 18), TextAnchor.UpperLeft, false);
                InkShape bg = done ? UIKit.BrushStrip(lesson, "Strip", Role.Brush) : UIKit.StickyNote(lesson);
                bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                bg.rectTransform.Stretch();
                if (done) bg.dry = 0.4f;
                UIKit.Words(lesson, (done ? "✓  " : "!  ") + result.Lesson, 24, done ? Role.OnBrush : Role.Ink, Face.ReadBold).lineSpacing = 1.12f;
            }
            foreach (var g in result.Granted.Where(g => g.StartsWith("doc:")))
            {
                var doc = g.Substring(4);
                var line = UIKit.Words(card, $"+ {Documents.Korean(doc)} · {Documents.LocalName(doc)}" +
                    (doc == "arc" ? $"  <size=22><color={Theme.Html(Role.InkSoft)}>{Loc.T("arc_arrived")}</color></size>" : ""), 24, Role.BrushDeep, Face.ReadBold);
                line.lineSpacing = 1.1f;
            }

            // what to remember: sticky notes stuck down the page under the stamp
            var notes = UIKit.Rect("Notes", page);
            notes.Place(new Vector2(0.5f, 1), new Vector2(0, -404), new Vector2(400, 0));
            UIKit.VStack(notes, 18, null, TextAnchor.UpperLeft);
            var tips = result.Tips.Take(3).ToList();
            if (tips.Count == 0) tips.Add(Loc.T("no_mistakes"));
            var tipViews = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < tips.Count; i++)
            {
                var n = UIKit.Rect("Tip", notes);
                UIKit.VStack(n, 0, new RectOffset(26, 26, 18, 18), TextAnchor.UpperLeft);
                var sticky = UIKit.StickyNote(n);
                sticky.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                sticky.rectTransform.Stretch();
                UIKit.Words(n, tips[i], 22, Role.Ink, Face.ReadBold).lineSpacing = 1.12f;
                n.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -1.5f : 1.2f);
                n.Group().alpha = 0f;
                tipViews.Add(n);
            }

            // the phrase card, right
            var phrase = UIKit.Rect("PhraseCard", root);
            phrase.Place(new Vector2(1, 1), new Vector2(-60, -250), new Vector2(660, 0));
            UIKit.VStack(phrase, 10, new RectOffset(40, 36, 40, 36), TextAnchor.UpperLeft);
            phrase.localRotation = Quaternion.Euler(0, 0, 1.6f);
            var index = UIKit.PaperPanel(phrase, "Card", Role.Balloon, 3f);
            index.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            index.rectTransform.Stretch();
            index.radius = 3f;
            index.shadow = new Vector2(10, -12);
            UIKit.Lettering(phrase, Loc.T("phrase_card").ToUpperInvariant(), 40, Role.Brush);
            foreach (var p in result.PhraseCard.Take(6))
            {
                var sub = string.Join(" · ", new[] { p.Romanization, p.Meaning }.Where(x => !string.IsNullOrEmpty(x)));
                var block = UIKit.Rect("Phrase", phrase);
                UIKit.VStack(block, 0, new RectOffset(0, 0, 4, 6), TextAnchor.UpperLeft);
                UIKit.Words(block, p.Korean, 25, Role.Ink, Face.ReadBold);
                if (sub.Length > 0) UIKit.Words(block, sub, 22, Role.Ink, Face.Read);
                var rule = UIKit.PenStroke(block, Role.Brush, 1.6f, new Vector2(0, 0.5f), new Vector2(1, 0.5f));
                rule.fillAlpha = 0.45f;
                rule.Layout(-1, 6);
            }

            // buttons, bottom right
            var buttons = UIKit.Rect("Buttons", root);
            buttons.Place(new Vector2(1, 0), new Vector2(-70, 52), new Vector2(980, 84));
            var h = UIKit.HStack(buttons, 26, null, TextAnchor.MiddleRight);
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            UIKit.InkButton(buttons, Loc.T("save_card").ToUpperInvariant(), SaveCard, ButtonStyle.Paper, 28).Layout(320);
            UIKit.InkButton(buttons, Loc.T("retry").ToUpperInvariant(), Retry, ButtonStyle.Paper, 30).Layout(220);
            UIKit.InkButton(buttons, Loc.T("levels").ToUpperInvariant() + "  →", () => SceneFlow.Load(SceneFlow.Select), ButtonStyle.Primary, 34).Layout(330);

            // the reveal, in order
            _ = Motion.Slap(page, -1.6f, 0.02f, -5f);
            _ = Motion.Slap(head.rectTransform, 1.5f, 0.1f, -5f, 0.3f);
            _ = Motion.Slap(card, -1f, 0.25f);
            _ = Motion.Slap(phrase, 1.6f, 0.4f, 6f);
            _ = Motion.SlideIn(buttons, new Vector2(0, -140), 0.28f, 0.6f);
            await Motion.Tween(bar, 0.5f, t => bar.SetReveal(Mathf.Max(0.02f, result.Progress) * t), true, 0.5f);
            if (!this) return;
            stamp.Group().alpha = 1f;
            Sfx.Play(done ? ProceduralAudio.Success : ProceduralAudio.Soft, 0.7f);
            await Motion.Stamp(stamp, -12f, page);
            for (int i = 0; i < tipViews.Count; i++)
            {
                if (!this) return;
                Sfx.Play(ProceduralAudio.Paper, 0.45f);
                await Motion.Slap(tipViews[i], i % 2 == 0 ? -1.5f : 1.2f, 0.05f, 7f);
            }
        }

        static void Outline(Text t)
        {
            var fx = t.gameObject.AddComponent<InkText>();
            fx.outline = 2.5f;
            fx.directions = 8;
            fx.shadow = new Vector2(3, -3);
        }

        static void Stat(RectTransform parent, string label, string value)
        {
            var box = UIKit.Rect("Stat", parent);
            box.Layout(200, 104);
            UIKit.Lettering(box, value, 70, Role.Ink).rectTransform.Stretch(0, 0, 0, 34);
            UIKit.Words(box, label, 24, Role.Ink, Face.ReadBold).rectTransform.Stretch(2, 0, 72, 0);
        }

        void Retry()
        {
            var level = GameSession.I.CurrentLevel;
            if (level != null) SceneFlow.Play(level);
            else SceneFlow.Load(SceneFlow.Select);
        }

        void SaveCard()
        {
            var dir = Path.Combine(Application.persistentDataPath, "PhraseCards");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{result.ScenarioId}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            Toasts.Show(Loc.T("saved_to", path));
        }

        static ScenarioResult Demo() => new()
        {
            ScenarioId = "demo",
            Title = "튼튼내과의원 · Part 1 · See a doctor",
            Outcome = ScenarioOutcome.Completed,
            StepsCompleted = 14,
            StepsTotal = 14,
            Mistakes = 2,
            Seconds = 214,
            Lesson = "Demo result",
            Tips = { "초진 = first visit. 재진 = returning patient." },
            PhraseCard =
            {
                new PhraseLine { Korean = "진료 받으러 왔어요.", Romanization = "jillyo badeureo wasseoyo", Meaning = "I'm here to see a doctor." },
            },
        };
    }
}
