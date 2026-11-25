using System;
using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Conversation;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Procedure;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// Review of the parts the player has finished, opened from the scenario select, so a player who forgot a little
    /// can look it up instead of replaying. Three tabs per part: before you go (the lesson, what to bring, what you
    /// leave with, the phrase card), key words, and the whole conversation (right answers, ways to ask, mistakes to
    /// avoid). Every line plays in its baked voice. Everything is read from the procedure files; nothing is written twice.
    /// </summary>
    public class Logbook : Modal
    {
        const float Margin = 56f;
        const float IndexWidth = 440f;
        const float Top = 176f;
        const float Bottom = 118f;
        const float Pad = 56f;           // inside the page sheet
        const float HeadHeight = 104f;
        const float TabsTop = 34f + HeadHeight + 14f;
        const float TabsHeight = 60f;
        static readonly HashSet<string> Payment = new() { "card", "checkcard", "cash" };
        static readonly Role[] SignFill = { Role.Brush, Role.Night, Role.Glow };

        enum Tab { Brief, Words, Talk }

        enum Kind { Right, Ask, Mistake, Blocked }

        class Paper
        {
            public string Doc, Korean, Local;
        }

        class Voiced
        {
            public InkShape Button, Icon;
            public Role ButtonIdle, IconIdle;
        }

        class Row
        {
            public LevelDef Level;
            public InkShape Body, Key;
            public Text KeyLabel, Title;
            public bool Locked;
        }

        TextAsset[] sources;
        GameObject hidden;
        readonly Dictionary<string, ProcedureDef> parsed = new();
        readonly List<Row> rows = new();
        readonly List<(Tab tab, InkShape body, Text label)> tabButtons = new();
        readonly Dictionary<string, AudioClip> announcements = new();
        RectTransform root, tabsRow, page;
        RectTransform head;
        Text headPlace, headPart;
        ScrollRect pageScroll;
        LevelDef current;
        ProcedureDef proc;
        Tab tab;
        AudioSource speaker;
        Voiced playing;
        float playingUntil;

        /// <summary>
        /// Opens the logbook on <paramref name="focus"/> when it is finished, else on the part just played or the first
        /// finished one. <paramref name="hide"/> (the screen underneath) is hidden meanwhile, so its text can't show through.
        /// </summary>
        public static void Show(TextAsset[] procedures, LevelDef focus = null, GameObject hide = null)
        {
            var book = new GameObject("Logbook", typeof(RectTransform)).AddComponent<Logbook>();
            book.sources = procedures ?? Array.Empty<TextAsset>();
            if (hide && hide.activeSelf)
            {
                book.hidden = hide;
                hide.SetActive(false);
            }
            book.root = book.Open(null, "LogbookCanvas", 60);
            book.Build(focus);
        }

        static IEnumerable<LevelDef> Parts => Levels.All.Where(l => l.available && !string.IsNullOrEmpty(l.procedure));

        ProcedureDef Procedure(LevelDef level)
        {
            if (level == null) return null;
            if (parsed.TryGetValue(level.procedure, out var proc)) return proc;
            var asset = sources.FirstOrDefault(t => t && t.name == level.procedure);
            proc = asset ? ProcedureDef.Parse(asset.text) : null;
            if (!asset) Debug.LogWarning($"[Logbook] No procedure file for {level.procedure}");
            return parsed[level.procedure] = proc;
        }

        void Build(LevelDef focus)
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0;

            // over the modal scrim, a deeper wash of ink: this screen is for reading
            var wash = UIKit.Rect("Wash", root).Stretch().gameObject.AddComponent<Image>();
            Themed.On(wash, Role.Ink, 0.55f);
            wash.raycastTarget = false;

            // title strip and subtitle, like the scenario select
            var title = UIKit.Rect("Title", root);
            title.Place(new Vector2(0, 1), new Vector2(Margin + 8, -44), new Vector2(460, 100));
            title.localRotation = Quaternion.Euler(0, 0, -1.5f);
            var strip = UIKit.BrushStrip(title, "Strip", Role.Brush);
            strip.rectTransform.Stretch();
            strip.dry = 0.7f;
            UIKit.Words(title, Loc.T("logbook").ToUpperInvariant(), 58, Role.OnBrush, Face.Display, TextAnchor.MiddleCenter).rectTransform.Stretch(10, 10, 0, 4);
            var sub = UIKit.Words(root, Loc.T("logbook_sub"), 28, Role.Paper, Face.ReadBold);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(Margin + 520, -58), new Vector2(1240, 80));
            Outline(sub);

            // back (Esc), bottom left
            var back = UIKit.Rect("Back", root);
            back.Place(new Vector2(0, 0), new Vector2(Margin + 8, 40), new Vector2(260, 64));
            var bh = UIKit.HStack(back, 14, null, TextAnchor.MiddleLeft);
            bh.childForceExpandHeight = false;
            var esc = UIKit.Keycap(back, "ESC", 22);
            esc.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            esc.GetComponentInChildren<InkShape>().raycastTarget = true;
            var backText = UIKit.Words(back, Loc.T("back"), 30, Role.Paper, Face.ReadBold);
            backText.Layout(160, 50);
            Outline(backText);

            // parts on the left, on a sheet of kraft paper
            var indexArea = UIKit.Rect("Index", root);
            indexArea.anchorMin = Vector2.zero;
            indexArea.anchorMax = new Vector2(0, 1);
            indexArea.offsetMin = new Vector2(Margin, Bottom);
            indexArea.offsetMax = new Vector2(Margin + IndexWidth, -Top);
            var indexSheet = UIKit.PaperPanel(indexArea, "Sheet", Role.PaperDeep, 3f);
            indexSheet.rectTransform.Stretch();
            indexSheet.shadow = new Vector2(8, -9);
            indexArea.localRotation = Quaternion.Euler(0, 0, -0.6f);
            var indexInner = UIKit.Rect("Inner", indexArea).Stretch(14, 6, 16, 16);
            var index = ScrollArea(indexInner, new RectOffset(12, 22, 6, 12), 12, 2, out _);
            BuildIndex(index);

            // the page on the right: the part's header and tabs stay put, the tab scrolls under them
            var pageArea = UIKit.Rect("Page", root).Stretch(Margin + IndexWidth + 40, Margin, Top, Bottom);
            var sheet = UIKit.PaperPanel(pageArea, "Sheet", Role.Balloon, 3.5f);
            sheet.rectTransform.Stretch();
            sheet.shadow = new Vector2(10, -12);
            sheet.radius = 4f;

            head = UIKit.Rect("Head", pageArea);
            TopBand(head, 34f, HeadHeight);
            headPlace = UIKit.Words(head, "", 46, Role.Ink, Face.DisplayKo);
            headPlace.rectTransform.Stretch(0, 0, 0, 46);
            headPlace.horizontalOverflow = HorizontalWrapMode.Overflow;
            headPart = UIKit.Words(head, "", 28, Role.Ink, Face.ReadBold);
            headPart.rectTransform.Stretch(0, 0, 58, 4);
            var underline = UIKit.PenStroke(head, Role.Brush, 5f, new Vector2(0, 0.5f), new Vector2(0.5f, 0.62f), new Vector2(1, 0.45f));
            underline.rectTransform.Place(new Vector2(0, 0), new Vector2(0, -8), new Vector2(360, 14));

            tabsRow = UIKit.Rect("Tabs", pageArea);
            TopBand(tabsRow, TabsTop, TabsHeight);
            UIKit.HStack(tabsRow, 16, null, TextAnchor.MiddleLeft);
            AddTab(Tab.Brief, Loc.T("tab_brief"));
            AddTab(Tab.Words, Loc.T("key_words"));
            AddTab(Tab.Talk, Loc.T("tab_talk"));

            var scrollArea = UIKit.Rect("Scroll", pageArea).Stretch(0, 0, TabsTop + TabsHeight + 14, 10);
            page = ScrollArea(scrollArea, new RectOffset((int)Pad, (int)Pad, 14, 48), 20, 16, out pageScroll);

            var finished = Parts.Where(GameSession.I.IsDone).ToList();
            var last = GameSession.I.CurrentLevel;
            Pick(focus != null && finished.Contains(focus) ? focus
                : last != null && finished.Contains(last) ? last
                : finished.FirstOrDefault());

            _ = Motion.Draw(strip, 0.28f);
            _ = Motion.Slap(indexArea, -0.6f, 0.04f);
            _ = Motion.Slap(pageArea, 0f, 0.1f, 4f);
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
        }

        static void Outline(Text t)
        {
            var fx = t.gameObject.AddComponent<InkText>();
            fx.outline = 2f;
            fx.directions = 8;
            fx.shadow = new Vector2(2, -2);
        }

        /// <summary>Pins a rect to the top of the page panel, inset like the page content.</summary>
        static void TopBand(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(Pad, -top - height);
            rt.offsetMax = new Vector2(-Pad, -top);
        }

        void AddTab(Tab t, string label)
        {
            var button = UIKit.InkButton(tabsRow, label.ToUpperInvariant(), () => ShowTab(t), ButtonStyle.Paper, 26);
            var text = button.GetComponentInChildren<Text>();
            button.Layout(text.preferredWidth + 70, TabsHeight);
            tabButtons.Add((t, button.GetComponentInChildren<InkShape>(), text));
        }

        /// <summary>A vertically scrolling stack with a slim pill scrollbar that only shows when needed.</summary>
        static RectTransform ScrollArea(RectTransform area, RectOffset padding, float spacing, float barInset, out ScrollRect scroll)
        {
            var viewport = UIKit.Rect("Viewport", area).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear; // the wheel scrolls anywhere over the area
            var content = UIKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            UIKit.VStack(content, spacing, padding);

            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 14f;

            var bar = UIKit.Rect("Scrollbar", area);
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(-barInset - 6, 28);
            bar.offsetMax = new Vector2(-barInset, -28);
            var handle = UIKit.Ink(UIKit.Rect("Slide", bar).Stretch(), "Handle", InkKind.Panel, Role.Ink, 0f);
            handle.fillAlpha = 0.45f;
            handle.radius = 3f;
            handle.shadow = Vector2.zero;
            handle.rough = 0.6f;
            handle.raycastTarget = true;
            handle.rectTransform.Stretch();
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        // =================================================================== index

        void BuildIndex(RectTransform index)
        {
            int p = 0;
            foreach (var place in Levels.Places)
            {
                var fill = SignFill[p++ % SignFill.Length];
                var parts = place.levels.Where(l => l.available && !string.IsNullOrEmpty(l.procedure)).ToList();
                if (parts.Count == 0) continue;
                var head = UIKit.Rect("Place", index);
                UIKit.HStack(head, 12, new RectOffset(4, 0, 12, 2), TextAnchor.MiddleLeft).childForceExpandHeight = false;
                var dot = UIKit.Dot(head, fill, 2.5f);
                dot.Layout(22, 22);
                UIKit.Words(head, $"{place.nameKo}  <size=23><color={Theme.Html(Role.InkSoft)}>{place.name?.Get()}</color></size>", 30, Role.Ink, Face.DisplayKo)
                    .Layout(0, -1, 1);

                foreach (var level in parts) IndexRow(index, level);
            }
        }

        void IndexRow(RectTransform index, LevelDef level)
        {
            bool done = GameSession.I.IsDone(level);
            var root = UIKit.Rect("Row", index);
            root.Layout(-1, done ? 80 : 104);
            var body = UIKit.PaperPanel(root, "Body", Role.Paper, 2.5f);
            body.rectTransform.Stretch();
            body.shadow = new Vector2(4, -5);
            body.raycastTarget = true;
            // a locked part is set aside, not see-through: kraft paper and soft ink
            if (!done) body.fillRole = Role.PaperDeep;
            var row = new Row { Level = level, Body = body, Locked = !done };
            // the part number as a keycap in the line colour, beside the title
            var key = UIKit.Keycap(body.transform, level.level.ToString(), 20, done ? Role.Brush : Role.PaperDeep,
                done ? Role.Ink : Role.InkSoft, done ? Role.OnBrush : Role.InkSoft);
            key.Place(new Vector2(0, 0.5f), new Vector2(16, 2), key.sizeDelta);
            row.Key = key.GetComponentInChildren<InkShape>();
            row.KeyLabel = key.GetComponentInChildren<Text>();
            var title = level.title?.Get();
            row.Title = UIKit.Words(body.transform, done ? title : $"{title}\n<size=22>{Loc.T("logbook_locked")}</size>", 22,
                done ? Role.Ink : Role.InkSoft, Face.ReadBold);
            row.Title.rectTransform.Stretch(16 + key.sizeDelta.x + 14, 14, 8, 8);
            row.Title.verticalOverflow = VerticalWrapMode.Truncate;
            row.Title.resizeTextForBestFit = true;
            row.Title.resizeTextMinSize = 16;
            row.Title.resizeTextMaxSize = 22;
            row.Title.lineSpacing = 0.95f;
            rows.Add(row);
            if (!done) return;
            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = body;
            btn.onClick.AddListener(() => Pick(level));
            var hover = root.gameObject.AddComponent<InkHover>();
            hover.visual = body.rectTransform;
            hover.body = body;
            hover.tilt = -1f;
        }

        void Pick(LevelDef level)
        {
            if (current != null && level != null && level != current) Sfx.Play(ProceduralAudio.Paper, 0.45f);
            current = level;
            proc = Procedure(level);
            foreach (var r in rows)
            {
                bool on = r.Level == level;
                if (r.Locked) continue;
                r.Body.fillRole = on ? Role.Brush : Role.Paper;
                r.Body.SetVerticesDirty();
                // on the brush row the keycap turns paper so it doesn't sink into it
                r.Key.fillRole = on ? Role.Paper : Role.Brush;
                r.Key.SetVerticesDirty();
                r.KeyLabel.GetComponent<Themed>().Set(on ? Role.Brush : Role.OnBrush);
                r.Title.GetComponent<Themed>().Set(on ? Role.OnBrush : Role.Ink);
            }

            head.gameObject.SetActive(proc != null);
            tabsRow.gameObject.SetActive(proc != null);
            if (proc != null)
            {
                headPlace.text = level.Place?.nameKo ?? proc.titleKo;
                headPart.text = level.FullTitle;
            }
            ShowTab(tab);
        }

        void ShowTab(Tab t)
        {
            if (t != tab) Sfx.Play(ProceduralAudio.Tick, 0.4f);
            tab = t;
            foreach (var (tt, body, label) in tabButtons)
            {
                bool on = tt == t;
                body.fillRole = on ? Role.Brush : Role.Paper;
                body.SetVerticesDirty();
                label.GetComponent<Themed>().Set(on ? Role.OnBrush : Role.Ink);
            }
            BuildPage();
        }

        // =================================================================== page

        void BuildPage()
        {
            StopVoice();
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                var child = page.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            if (proc == null) Empty();
            else if (tab == Tab.Brief) Brief();
            else if (tab == Tab.Words) Words();
            else Conversation();

            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
            pageScroll.StopMovement();
            page.anchoredPosition = Vector2.zero;
        }

        void Empty()
        {
            var box = UIKit.Rect("Empty", page);
            UIKit.VStack(box, 14, new RectOffset(0, 0, 160, 0), TextAnchor.UpperCenter, false);
            UIKit.Lettering(box, Loc.T("logbook_empty_title").ToUpperInvariant(), 52, Role.Ink, TextAnchor.MiddleCenter);
            UIKit.Words(box, Loc.T("logbook_empty"), 28, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter);
        }

        /// <summary>Before you go: the lesson, what to bring and what you leave with, and the phrase card.</summary>
        void Brief()
        {
            var note = proc.completeNote?.Get();
            if (!string.IsNullOrEmpty(note)) Strip(page, "✓  " + note, true);
            Papers();
            PhraseCard();
        }

        /// <summary>What to bring (left) and what the visit gives you (right).</summary>
        void Papers()
        {
            var bring = Bring(proc);
            var earned = Earned(proc);
            if (bring.Count == 0 && earned.Count == 0) return;
            Gap(page, 6);

            var row = UIKit.Rect("Papers", page);
            UIKit.HStack(row, 28, null, TextAnchor.UpperLeft);
            if (bring.Count > 0)
            {
                var left = Column(row, 2);
                Heading(left, Loc.T("what_to_bring"));
                Grid(left, bring, 2, 16, Chip);
                var lesson = proc.blockedLesson?.Get();
                if (!string.IsNullOrEmpty(lesson)) Strip(left, "!  " + lesson, false);
            }
            if (earned.Count > 0)
            {
                var right = Column(row, 1);
                Heading(right, Loc.T("you_get"));
                Grid(right, earned, 1, 16, Chip);
            }
        }

        void PhraseCard()
        {
            if (proc.phrases.Count == 0) return;
            Gap(page, 6);
            var card = UIKit.Rect("PhraseCard", page);
            UIKit.VStack(card, 14, new RectOffset(36, 32, 34, 30), TextAnchor.UpperLeft, false);
            var paper = UIKit.PaperPanel(card, "Card", Role.Paper, 3f);
            paper.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            paper.rectTransform.Stretch();
            paper.radius = 3f;
            UIKit.Lettering(card, Loc.T("phrase_card").ToUpperInvariant(), 36, Role.Brush);
            Grid(card, proc.phrases, 2, 36, (parent, p) => Line(parent, p.ko, Join(p.rom, p.meaning?.Get()), 25, Clip("player", p.ko), true));
        }

        void Words()
        {
            Grid(page, proc.glossary, 2, 36, (parent, w) => Line(parent, w.ko, Join(w.rom, w.meaning?.Get()), 25, Clip("pa", w.ko)));
        }

        void Conversation()
        {
            string phase = null;
            for (int i = 0; i < proc.steps.Count; i++)
            {
                var step = proc.steps[i];
                var task = step.task?.Get();
                if (!string.IsNullOrEmpty(task) && task != phase)
                {
                    Phase(task);
                    phase = task;
                }
                else if (i > 0) Rule();
                Step(step);
            }
        }

        /// <summary>One visit phase, echoing the HUD to-do: a dark strip with an arrow and the task.</summary>
        void Phase(string task)
        {
            Gap(page, 4);
            var band = UIKit.Rect("Phase", page);
            UIKit.HStack(band, 14, new RectOffset(26, 26, 14, 14), TextAnchor.MiddleLeft).childForceExpandHeight = false;
            var strip = UIKit.BrushStrip(band, "Strip", Role.Night);
            strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            strip.rectTransform.Stretch();
            strip.dry = 0.5f;
            var arrowSlot = UIKit.Rect("Arrow", band);
            arrowSlot.Layout(40, 26);
            UIKit.Arrow(arrowSlot, Role.Glow, 3.5f).rectTransform.Stretch();
            UIKit.Words(band, task, 24, Role.Paper, Face.ReadBold).Layout(0, -1, 1);
        }

        void Step(StepDef step)
        {
            var staff = proc.Staff(step.staff);
            var speakerName = $"{staff.name} · {staff.roleText?.Get() ?? staff.role}";
            var block = UIKit.Rect("Step", page);
            UIKit.VStack(block, 12, new RectOffset(0, 0, 4, 4), TextAnchor.UpperLeft, false);

            if (!string.IsNullOrEmpty(step.announce))
            {
                UIKit.Words(block, Loc.T("announcement"), 23, Role.Ink, Face.ReadBold);
                Line(block, string.Format(step.announce, GameSession.I.PlayerNameKo), null, 23, Announcement(step.announce));
            }

            Speaker(block, speakerName);
            Line(block, step.npc, step.meaning?.Get(), 27, Clip(staff.id, step.npc));

            if (step.type == "keypad")
                Answer(block, Kind.Right, Loc.T(step.confirmPrevious ? "enter_pin_again" : "enter_pin"), null, null);
            // right answers first (a blank form sends you to fill it in, then you answer again), then help, then mistakes
            foreach (var c in step.choices.Where(c => c.result == "fetch"))
            {
                Choice(block, c);
                if (step.fetch?.objective != null)
                {
                    var row = UIKit.Rect("Fetch", block);
                    UIKit.HStack(row, 16, null, TextAnchor.UpperLeft);
                    UIKit.Rect("Indent", row).Layout(32, 1);
                    UIKit.Words(row, "→  " + step.fetch.objective.Get(), 24, Role.BrushDeep, Face.ReadBold).Layout(0, -1, 1);
                }
            }
            foreach (var c in step.choices.Where(c => c.Advances)) Choice(block, c);
            foreach (var c in step.choices.Where(c => c.result == "ask")) Choice(block, c);
            foreach (var c in step.choices.Where(c => c.result == "retry" || c.result == "block")) Choice(block, c);

            if (!string.IsNullOrEmpty(step.after))
            {
                Speaker(block, speakerName);
                Line(block, step.after, step.afterMeaning?.Get(), 24, Clip(staff.id, step.after));
            }
        }

        void Choice(Transform parent, ChoiceDef c)
        {
            var sub = Join(c.rom, c.meaning?.Get());
            if (c.Advances || c.result == "fetch")
            {
                var text = Answer(parent, Kind.Right, c.ko, sub, Clip("player", c.ko));
                if (!string.IsNullOrEmpty(c.give))
                    UIKit.Words(text, Loc.T("hand_over", $"{Documents.Korean(c.give)} · {Documents.LocalName(c.give)}"), 23, Role.BrushDeep, Face.ReadBold);
            }
            else if (c.result == "ask")
            {
                var text = Answer(parent, Kind.Ask, c.ko, sub, Clip("player", c.ko));
                if (string.IsNullOrEmpty(c.reply)) return;
                UIKit.Rect("Gap", text).Layout(-1, 4);
                UIKit.Words(text, "→  " + c.reply, 22, Role.Ink, Face.Read);
                var meaning = c.replyMeaning?.Get();
                if (!string.IsNullOrEmpty(meaning)) UIKit.Words(text, meaning, 22, Role.Ink, Face.Read);
            }
            else
            {
                bool blocked = c.result == "block";
                var text = Answer(parent, blocked ? Kind.Blocked : Kind.Mistake, c.ko, sub, null);
                var note = c.note?.Get();
                if (string.IsNullOrEmpty(note) && blocked) note = proc.blockedLesson?.Get();
                if (!string.IsNullOrEmpty(note))
                {
                    var n = UIKit.Rect("Note", text);
                    UIKit.VStack(n, 0, new RectOffset(16, 16, 10, 12), TextAnchor.UpperLeft, false);
                    var sticky = UIKit.StickyNote(n);
                    sticky.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    sticky.rectTransform.Stretch();
                    sticky.shadow = new Vector2(3, -4);
                    UIKit.Words(n, note, 22, Role.Ink, Face.ReadBold);
                }
            }
        }

        /// <summary>
        /// One thing the player can say: a badge (✓ right, ? asking for help, × mistake, ! turned away), the line with
        /// its romanization and translation, and a listen button when the line was baked. Returns the text column.
        /// </summary>
        RectTransform Answer(Transform parent, Kind kind, string ko, string sub, AudioClip clip)
        {
            bool mistake = kind is Kind.Mistake or Kind.Blocked;
            var row = UIKit.Rect("Answer", parent);
            UIKit.HStack(row, 16, null, TextAnchor.UpperLeft);
            var fill = kind switch { Kind.Right => Role.Brush, Kind.Ask => Role.Paper, _ => Role.Glow };
            var badge = UIKit.Dot(row, fill, 2.5f);
            badge.Layout(36, 36);
            var glyph = kind switch { Kind.Right => "✓", Kind.Ask => "?", Kind.Blocked => "!", _ => "×" };
            UIKit.Words(badge.transform, glyph, 22, kind == Kind.Right ? Role.OnBrush : Role.Ink, Face.Display, TextAnchor.MiddleCenter).rectTransform.Stretch();

            var text = UIKit.Rect("Text", row);
            UIKit.VStack(text, 4, null, TextAnchor.UpperLeft, false);
            text.Layout(0, -1, 1).minWidth = 0;
            // Korean first and largest; romanization and translation below it in solid regular type
            UIKit.Words(text, ko, mistake ? 24 : 25, Role.Ink, Face.ReadBold);
            if (!string.IsNullOrEmpty(sub)) UIKit.Words(text, sub, 22, Role.Ink, Face.Read);
            Listen(row, clip);
            return text;
        }

        /// <summary>A Korean line with its romanization and translation below, and a listen button.</summary>
        Component Line(Transform parent, string ko, string sub, int size, AudioClip clip, bool onPaper = false)
        {
            var row = UIKit.Rect("Line", parent);
            UIKit.HStack(row, 14, null, TextAnchor.UpperLeft);
            var text = UIKit.Rect("Text", row);
            UIKit.VStack(text, 2, null, TextAnchor.UpperLeft, false);
            text.Layout(0, -1, 1).minWidth = 0;
            UIKit.Words(text, ko, size, Role.Ink, Face.ReadBold);
            if (!string.IsNullOrEmpty(sub)) UIKit.Words(text, sub, 22, Role.Ink, Face.Read);
            Listen(row, clip);
            return row;
        }

        /// <summary>A paper tag for a document: its colour swatch with the glyph, Korean name and local name.</summary>
        Component Chip(Transform parent, Paper p)
        {
            var chip = UIKit.Rect("Chip", parent);
            UIKit.HStack(chip, 14, new RectOffset(16, 20, 14, 14), TextAnchor.MiddleLeft);
            var paper = UIKit.PaperPanel(chip, "Tag", Role.PaperDeep, 2.5f);
            paper.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            paper.rectTransform.Stretch();
            paper.shadow = new Vector2(4, -5);
            var info = Documents.Get(p.Doc);
            var swatch = UIKit.Dot(chip, Role.Paper, 2.5f);
            swatch.themed = false;
            swatch.fillColor = info?.Color ?? Theme.Get(Role.InkSoft);
            swatch.lineColor = Theme.Get(Role.Ink);
            swatch.Layout(42, 42);
            UIKit.Words(swatch.transform, info?.Glyph ?? "", 22, Role.Paper, Face.ReadBold, TextAnchor.MiddleCenter).rectTransform.Stretch();
            var text = UIKit.Rect("Text", chip);
            UIKit.VStack(text, 1, null, TextAnchor.MiddleLeft, false);
            text.Layout(0, -1, 1).minWidth = 0;
            UIKit.Words(text, p.Korean, 22, Role.Ink, Face.ReadBold);
            UIKit.Words(text, p.Local, 22, Role.Ink, Face.ReadBold);
            return chip;
        }

        // =================================================================== what to bring

        /// <summary>
        /// Documents handed over on the way to the end, in order: the ones given with a right answer (alternatives in
        /// one step become "A 또는 B"), the ones handed over while fetching something, and the ones the story says you
        /// bring. A form you fill in at the office says so.
        /// </summary>
        static List<Paper> Bring(ProcedureDef proc)
        {
            var list = new List<Paper>();
            var seen = new HashSet<string>();
            var filledThere = new HashSet<string>(proc.steps.Where(s => s.fetch != null).Select(s => s.fetch.document));

            void Add(IEnumerable<string> docs)
            {
                var alternatives = docs.Where(d => !string.IsNullOrEmpty(d) && seen.Add(d)).ToList();
                if (alternatives.Count == 0) return;
                if (alternatives.Count > 1 && alternatives.All(Payment.Contains) && alternatives.Contains("cash"))
                {
                    list.Add(new Paper { Doc = "card", Korean = "카드 또는 현금", Local = Loc.T("pay_card_cash") });
                    return;
                }
                var paper = new Paper
                {
                    Doc = alternatives[0],
                    Korean = string.Join(" 또는 ", alternatives.Select(Documents.Korean)),
                    Local = string.Join($" {Loc.T("or")} ", alternatives.Select(Documents.LocalName)),
                };
                if (alternatives.Count == 1 && filledThere.Contains(paper.Doc)) paper.Local += " · " + Loc.T("filled_there");
                list.Add(paper);
            }

            foreach (var step in proc.steps)
            {
                Add(step.choices.Where(c => c.result == "fetch").Select(c => c.give));
                Add(step.choices.Where(c => c.Advances).Select(c => c.give));
            }
            foreach (var doc in proc.carry) Add(new[] { doc });
            return list;
        }

        /// <summary>Documents every finished visit ends with (a document only one answer earns is optional, so it's left out).</summary>
        static List<Paper> Earned(ProcedureDef proc)
        {
            var flags = new List<string>();
            foreach (var step in proc.steps)
            {
                flags.AddRange(step.grants);
                var right = step.choices.Where(c => c.Advances).ToList();
                if (right.Count > 0) flags.AddRange(right[0].grants.Where(g => right.All(c => c.grants.Contains(g))));
            }
            flags.AddRange(proc.grants);
            return flags.Where(f => f.StartsWith("doc:")).Select(f => f.Substring(4)).Distinct()
                .Select(d => new Paper
                {
                    Doc = d,
                    Korean = Documents.Korean(d),
                    Local = Documents.LocalName(d) + (d == "arc" ? " " + Loc.T("arc_arrived") : ""),
                })
                .ToList();
        }

        // =================================================================== building blocks

        static void Gap(Transform parent, float height) => UIKit.Rect("Gap", parent).Layout(-1, height);

        void Rule()
        {
            var line = UIKit.PenStroke(page, Role.Ink, 2f, new Vector2(0, 0.5f), new Vector2(0.5f, 0.6f), new Vector2(1, 0.5f));
            line.fillAlpha = 0.18f;
            line.taper = 0.6f;
            line.Layout(-1, 8);
        }

        static void Heading(Transform parent, string text) =>
            UIKit.Lettering(parent, text.ToUpperInvariant(), 34, Role.Ink);

        static void Speaker(Transform parent, string name) =>
            UIKit.Words(parent, name, 23, Role.BrushDeep, Face.ReadBold);

        /// <summary>The lesson of a part: a brush strip (a visit that went right) or a sticky note (turned away).</summary>
        static void Strip(Transform parent, string text, bool good)
        {
            var strip = UIKit.Rect("Lesson", parent);
            UIKit.VStack(strip, 0, new RectOffset(34, 34, 18, 20), TextAnchor.UpperLeft, false);
            InkShape bg;
            if (good)
            {
                bg = UIKit.BrushStrip(strip, "Strip", Role.Brush);
                bg.dry = 0.45f;
            }
            else bg = UIKit.StickyNote(strip);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bg.rectTransform.Stretch();
            UIKit.Words(strip, text, 23, good ? Role.OnBrush : Role.Ink, Face.ReadBold).lineSpacing = 1.12f;
        }

        static RectTransform Column(Transform parent, float weight)
        {
            var col = UIKit.Rect("Column", parent);
            UIKit.VStack(col, 14, null, TextAnchor.UpperLeft, false);
            col.Layout(0, -1, weight).minWidth = 0;
            return col;
        }

        /// <summary>Rows of equal-width, equal-height cells; a short last row keeps the same widths.</summary>
        static void Grid<T>(Transform parent, IList<T> items, int perRow, float spacing, Func<Transform, T, Component> cell)
        {
            for (int i = 0; i < items.Count; i += perRow)
            {
                var row = UIKit.Rect("Row", parent);
                UIKit.HStack(row, spacing, null, TextAnchor.UpperLeft).childForceExpandHeight = true;
                // stretching the cells makes the row report itself flexible; keep it at its own height
                row.gameObject.AddComponent<LayoutElement>().flexibleHeight = 0;
                for (int k = i; k < i + perRow; k++)
                {
                    var c = k < items.Count ? cell(row, items[k]) : UIKit.Rect("Filler", row);
                    c.Layout(0, -1, 1).minWidth = 0;
                }
            }
        }

        static string Join(string rom, string meaning) =>
            string.Join(" · ", new[] { rom, meaning }.Where(s => !string.IsNullOrEmpty(s)));

        // =================================================================== voices

        static AudioClip Clip(string voice, string text) => BakedVoice.Load(voice, text, false);

        AudioClip Announcement(string template)
        {
            if (announcements.TryGetValue(template, out var clip)) return clip;
            return announcements[template] = BakedVoice.Compose("pa", template, GameSession.I.PlayerNameKo);
        }

        void Listen(Transform row, AudioClip clip)
        {
            var slot = UIKit.Rect("Listen", row);
            slot.Layout(42, 42);
            if (!clip) return; // an empty slot keeps the lines aligned

            var v = new Voiced { ButtonIdle = Role.PaperDeep, IconIdle = Role.Ink };
            v.Button = UIKit.Dot(slot, v.ButtonIdle, 2.5f, "Button");
            v.Button.rectTransform.Stretch();
            v.Button.raycastTarget = true;
            v.Icon = UIKit.PenStroke(v.Button.transform, v.IconIdle, 3f,
                new Vector2(0.38f, 0.28f), new Vector2(0.38f, 0.72f), new Vector2(0.74f, 0.5f), new Vector2(0.38f, 0.28f));
            v.Icon.taper = 0.9f;
            v.Icon.rectTransform.Stretch();
            var btn = slot.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = v.Button;
            btn.onClick.AddListener(() => Play(clip, v));
        }

        void Play(AudioClip clip, Voiced v)
        {
            StopVoice();
            speaker.clip = clip;
            speaker.Play();
            AudioTap.Note(clip, speaker.volume);
            Music.DuckFor(clip.length + 0.3f);
            playing = v;
            // the button stays lit for the length of the line (also while a recording steps the editor and no audio plays)
            playingUntil = Time.unscaledTime + clip.length;
            v.Button.fillRole = Role.Brush;
            v.Button.SetVerticesDirty();
            v.Icon.fillRole = Role.OnBrush;
            v.Icon.SetVerticesDirty();
            _ = Motion.Pop(v.Button.rectTransform, 1.15f, 0.2f);
        }

        void StopVoice()
        {
            if (speaker && speaker.isPlaying)
            {
                AudioTap.NoteStop(speaker.clip);
                speaker.Stop();
            }
            if (playing == null) return;
            if (playing.Button)
            {
                playing.Button.fillRole = playing.ButtonIdle;
                playing.Button.SetVerticesDirty();
            }
            if (playing.Icon)
            {
                playing.Icon.fillRole = playing.IconIdle;
                playing.Icon.SetVerticesDirty();
            }
            playing = null;
        }

        // =================================================================== input

        // LateUpdate, so the scenario select (which leaves for Home on Esc when no modal is open) sees this one still open
        void LateUpdate()
        {
            if (playing != null && !speaker.isPlaying && Time.unscaledTime >= playingUntil) StopVoice();
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (proc != null && (kb.rightArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) ShowTab((Tab)(((int)tab + 1) % 3));
            if (proc != null && kb.leftArrowKey.wasPressedThisFrame) ShowTab((Tab)(((int)tab + 2) % 3));
            float step = (kb.downArrowKey.isPressed ? 1 : 0) - (kb.upArrowKey.isPressed ? 1 : 0);
            if (step != 0) ScrollBy(step * 900f * Time.unscaledDeltaTime);
            if (kb.pageDownKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) ScrollBy(pageScroll.viewport.rect.height * 0.85f);
            if (kb.pageUpKey.wasPressedThisFrame) ScrollBy(-pageScroll.viewport.rect.height * 0.85f);
        }

        void ScrollBy(float pixels)
        {
            float max = Mathf.Max(0, page.rect.height - pageScroll.viewport.rect.height);
            pageScroll.StopMovement();
            page.anchoredPosition = new Vector2(0, Mathf.Clamp(page.anchoredPosition.y + pixels, 0, max));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (hidden) hidden.SetActive(true);
            foreach (var clip in announcements.Values)
                if (clip) Destroy(clip);
        }
    }
}
