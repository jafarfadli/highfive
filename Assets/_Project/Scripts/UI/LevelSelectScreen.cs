using System.Linq;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// Scenario select. Each place is a projecting signboard (돌출간판) with its name stacked one syllable per
    /// row, and its parts hang beside it as paper cards: done parts carry a 완료 stamp, parts that need a
    /// document carry a sticky note, unfinished ones a shutter and 준비중 tape.
    /// </summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        const float ColumnWidth = 540f, SignWidth = 112f;

        [Tooltip("Every procedure file, for the Logbook (assigned by the scene builder).")]
        public TextAsset[] procedures;

        RectTransform root;
        bool intro = true;

        static readonly Role[] SignFill = { Role.Brush, Role.Night, Role.Glow };

        void Start()
        {
            CursorState.Gameplay(false);
            root = (RectTransform)UIKit.CreateCanvas("Select", 20).transform;
            Build();
        }

        void Build()
        {
            foreach (Transform c in root) Destroy(c.gameObject);
            var session = GameSession.I;

            UIKit.InkScrim(root, 0.5f, 0.3f);

            // title strip, top left
            var title = UIKit.Rect("Title", root);
            title.Place(new Vector2(0, 1), new Vector2(64, -44), new Vector2(720, 104));
            title.localRotation = Quaternion.Euler(0, 0, -1.5f);
            var strip = UIKit.BrushStrip(title, "Strip", Role.Brush);
            strip.rectTransform.Stretch();
            strip.dry = 0.7f;
            var t = UIKit.Words(title, "어디 갈까요?", 62, Role.OnBrush, Face.DisplayKo);
            t.rectTransform.Stretch(44, 300, 0, 4);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = UIKit.Words(title, Loc.T("choose_scenario"), 30, Role.OnBrush, Face.ReadBold, TextAnchor.MiddleLeft);
            sub.rectTransform.Stretch(420, 20, 26, 0);

            // logbook and progress, top right
            var playable = Levels.All.Where(l => l.available).ToList();
            int done = playable.Count(session.IsDone);
            var count = UIKit.Words(root, Loc.T("levels_done", done, playable.Count), 30, Role.Paper, Face.ReadBold, TextAnchor.MiddleRight);
            count.rectTransform.Place(new Vector2(1, 1), new Vector2(-300, -64), new Vector2(360, 50));
            Shadow(count);
            var logbook = UIKit.InkButton(root, Loc.T("logbook").ToUpperInvariant(), () => OpenLogbook(null), ButtonStyle.Paper, 28);
            ((RectTransform)logbook.transform).Place(new Vector2(1, 1), new Vector2(-64, -54), new Vector2(210, 70));
            ((RectTransform)logbook.transform).localRotation = Quaternion.Euler(0, 0, 1.5f);

            // one signboard per place, its parts hanging beside it
            var row = UIKit.Rect("Places", root);
            row.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(ColumnWidth * 3 + 120, 720));
            var h = UIKit.HStack(row, 60, null, TextAnchor.UpperCenter);
            h.childForceExpandHeight = false;
            int index = 0;
            foreach (var place in Levels.Places) Column(row, place, index++);

            // back (Esc) and reset, along the bottom
            var back = UIKit.Rect("Back", root);
            back.Place(new Vector2(0, 0), new Vector2(64, 44), new Vector2(260, 64));
            var bh = UIKit.HStack(back, 14, null, TextAnchor.MiddleLeft);
            bh.childForceExpandHeight = false;
            var esc = UIKit.Keycap(back, "ESC", 22);
            esc.gameObject.AddComponent<Button>().onClick.AddListener(() => SceneFlow.Load(SceneFlow.Home));
            esc.GetComponentInChildren<InkShape>().raycastTarget = true;
            var backText = UIKit.Words(back, Loc.T("back"), 30, Role.Paper, Face.ReadBold);
            backText.Layout(160, 50);
            Shadow(backText);

            var reset = UIKit.Words(root, Loc.T("reset_progress"), 24, Role.Paper, Face.ReadBold, TextAnchor.MiddleCenter);
            reset.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 44));
            reset.raycastTarget = true;
            reset.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                Dialog.Confirm(null, Loc.T("reset_progress_confirm"), () =>
                {
                    GameSession.I.ResetProgress();
                    intro = false;
                    Build();
                }));
            var line = UIKit.PenStroke(reset.transform, Role.Paper, 2f, new Vector2(0.15f, 0.12f), new Vector2(0.5f, 0.06f), new Vector2(0.85f, 0.1f));
            line.fillAlpha = 0.6f;
            line.rectTransform.Stretch();

            if (!intro) return;
            _ = Motion.Draw(strip, 0.28f);
        }

        static void Shadow(Text t)
        {
            var fx = t.gameObject.AddComponent<InkText>();
            fx.outline = 2f;
            fx.directions = 8;
            fx.shadow = new Vector2(2, -2);
        }

        void Column(RectTransform parent, PlaceDef place, int index)
        {
            var col = UIKit.Rect(place.id, parent);
            col.Layout(ColumnWidth, 720);

            // the signboard
            var sign = UIKit.Rect("Sign", col);
            sign.Place(new Vector2(0, 1), new Vector2(0, -6), new Vector2(SignWidth, 640));
            sign.localRotation = Quaternion.Euler(0, 0, index % 2 == 0 ? -1f : 1f);
            var fill = SignFill[index % SignFill.Length];
            var board = UIKit.PaperPanel(sign, "Board", fill, 4f);
            board.rectTransform.Stretch();
            board.radius = 5f;
            board.shadow = new Vector2(8, -9);
            board.shadowAlpha = 1f;
            var ink = fill == Role.Brush ? Role.OnBrush : fill == Role.Glow ? Role.Ink : Role.Paper;
            Emblem(sign, place.id, ink).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(54, 56));
            var syllables = string.Join("\n", place.nameKo.Replace(" ", "").ToCharArray());
            int size = place.nameKo.Length > 6 ? 44 : 52;
            var name = UIKit.Words(sign, syllables, size, ink, Face.DisplayKo, TextAnchor.UpperCenter);
            name.rectTransform.Stretch(4, 4, 92, 26);
            name.lineSpacing = 0.98f;
            // the place's name in the player's language on a plaque hanging under the sign (it may be wider than the sign)
            var plaque = UIKit.Rect("Plaque", sign);
            var plate = UIKit.PaperPanel(plaque, "Plate", Role.Paper, 3.5f);
            plate.radius = 5f;
            plate.shadow = new Vector2(5, -6);
            plate.rectTransform.Stretch();
            var local = UIKit.Words(plaque, place.name?.Get(), 22, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter);
            local.horizontalOverflow = HorizontalWrapMode.Overflow;
            local.rectTransform.Stretch(12, 12, 0, 2);
            plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 0);
            plaque.pivot = new Vector2(0.5f, 1f);
            plaque.anchoredPosition = new Vector2(0, -8);
            plaque.sizeDelta = new Vector2(Mathf.Max(SignWidth - 8f, local.preferredWidth + 30f), 46);
            if (intro) _ = Motion.Slap(sign, sign.localEulerAngles.z > 180 ? sign.localEulerAngles.z - 360 : sign.localEulerAngles.z, 0.06f + index * 0.08f);

            // the parts
            var parts = UIKit.Rect("Parts", col);
            parts.Place(new Vector2(0, 1), new Vector2(SignWidth + 30, -26), new Vector2(ColumnWidth - SignWidth - 30, 0));
            UIKit.VStack(parts, 26, null, TextAnchor.UpperLeft);
            int k = 0;
            foreach (var level in place.levels) Card(parts, level, index, k++);
        }

        /// <summary>A place's small ink drawing (a stethoscope, a passport, or a coin with ₩); the caller places it.</summary>
        static InkShape Emblem(Transform parent, string placeId, Role ink)
        {
            var nan = new Vector2(float.NaN, float.NaN);
            Vector2[] pts = placeId switch
            {
                "clinic" => new[]
                {
                    new Vector2(0.22f, 0.92f), new Vector2(0.22f, 0.55f), new Vector2(0.5f, 0.3f), new Vector2(0.78f, 0.55f), new Vector2(0.78f, 0.92f), nan,
                    new Vector2(0.5f, 0.3f), new Vector2(0.52f, 0.12f), new Vector2(0.75f, 0.06f), new Vector2(0.86f, 0.22f),
                },
                "immigration" => new[]
                {
                    new Vector2(0.24f, 0.08f), new Vector2(0.24f, 0.94f), new Vector2(0.78f, 0.94f), new Vector2(0.78f, 0.08f), new Vector2(0.24f, 0.08f), nan,
                    new Vector2(0.38f, 0.62f), new Vector2(0.52f, 0.74f), new Vector2(0.66f, 0.6f), new Vector2(0.52f, 0.44f), new Vector2(0.38f, 0.6f), nan,
                    new Vector2(0.36f, 0.24f), new Vector2(0.66f, 0.24f),
                },
                _ => new[]
                {
                    new Vector2(0.5f, 0.06f), new Vector2(0.14f, 0.24f), new Vector2(0.08f, 0.56f), new Vector2(0.3f, 0.9f), new Vector2(0.7f, 0.9f),
                    new Vector2(0.92f, 0.56f), new Vector2(0.86f, 0.24f), new Vector2(0.5f, 0.06f), nan,
                    new Vector2(0.3f, 0.66f), new Vector2(0.4f, 0.3f), new Vector2(0.5f, 0.58f), new Vector2(0.6f, 0.3f), new Vector2(0.7f, 0.66f), nan,
                    new Vector2(0.24f, 0.5f), new Vector2(0.76f, 0.5f),
                },
            };
            var e = UIKit.PenStroke(parent, ink, 3.5f, pts);
            e.taper = 0.7f;
            return e;
        }

        void Card(RectTransform parent, LevelDef level, int column, int index)
        {
            var session = GameSession.I;
            bool done = session.IsDone(level);
            bool missing = level.available && !string.IsNullOrEmpty(level.needs) && !session.Has(level.needs);

            var root = UIKit.Rect("Part", parent);
            root.Layout(-1, 150);
            float tilt = (column + index) % 2 == 0 ? -1.2f : 0.9f;
            var body = UIKit.PaperPanel(root, "Card", Role.Paper, 3f);
            body.rectTransform.Stretch();
            body.rectTransform.localRotation = Quaternion.Euler(0, 0, tilt);
            body.raycastTarget = level.available;

            // the part number as a keycap in the line colour, beside the title
            var key = UIKit.Keycap(body.transform, level.level.ToString(), 24, level.available ? Role.Brush : Role.PaperDeep,
                level.available ? Role.Ink : Role.InkSoft, level.available ? Role.OnBrush : Role.InkSoft);
            key.Place(new Vector2(0, 1), new Vector2(22, -24), key.sizeDelta);
            var title = UIKit.Words(body.transform, level.title?.Get(), 29, level.available ? Role.Ink : Role.InkSoft, Face.ReadBold);
            // shrinks to fit (best fit needs a bounded height), clear of the stamp and of the logbook pill on a done card
            title.rectTransform.Stretch(22 + key.sizeDelta.x + 14, done ? 104 : 26, 22, done ? 38 : 18);
            title.alignment = TextAnchor.UpperLeft;
            title.lineSpacing = 0.95f;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 22;
            title.resizeTextMaxSize = 29;

            if (level.available)
            {
                var btn = root.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = body;
                btn.onClick.AddListener(() => Play(level));
                var hover = root.gameObject.AddComponent<InkHover>();
                hover.visual = body.rectTransform;
                hover.body = body;
                hover.tilt = -1.2f;
            }

            if (done)
            {
                var stamp = UIKit.StampMark(body.transform, "완료", 96f, Role.Brush, 29);
                stamp.Place(new Vector2(1, 1), new Vector2(-10, -10), new Vector2(96, 96));
                stamp.localRotation = Quaternion.Euler(0, 0, 14f);
                if (intro) _ = Motion.Stamp(stamp, 14f, null, 0.45f + column * 0.08f + index * 0.06f);

                // a nested button: opens this part's logbook page instead of starting the level
                var book = UIKit.InkButton(root, Loc.T("logbook").ToUpperInvariant(), () => OpenLogbook(level), ButtonStyle.Night, 20);
                ((RectTransform)book.transform).Place(new Vector2(1, 0), new Vector2(-18, -14), new Vector2(150, 48));
            }
            else if (!level.available)
            {
                // shutter down: ink slats across the card, and a strip of tape saying it is being prepared
                for (int i = 0; i < 6; i++)
                {
                    var slat = UIKit.PenStroke(body.transform, Role.Ink, 3f, new Vector2(0.02f, 0.5f), new Vector2(0.5f, 0.55f), new Vector2(0.98f, 0.5f));
                    slat.fillAlpha = 0.35f;
                    slat.taper = 0.8f;
                    slat.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -18 - i * 22), new Vector2(360, 10));
                }
                var soon = UIKit.Ink(body.transform, "Soon", InkKind.Panel, Role.Glow, 3.5f);
                soon.radius = 6f;
                soon.shadow = new Vector2(5, -6);
                soon.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 52));
                soon.rectTransform.localRotation = Quaternion.Euler(0, 0, -5f);
                UIKit.Words(soon.transform, $"준비중 · {Loc.T("coming_soon")}", 24, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter).rectTransform.Stretch();
            }
            else if (missing)
            {
                var note = UIKit.StickyNote(root, "Needs");
                note.rectTransform.Place(new Vector2(1, 0), new Vector2(8, -18), new Vector2(230, 56));
                note.rectTransform.localRotation = Quaternion.Euler(0, 0, 4f);
                UIKit.Words(note.transform, "! " + Loc.T("needs_doc", Documents.LocalName(level.needs.Replace("doc:", ""))), 22, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter)
                    .rectTransform.Stretch(8, 8, 4, 4);
            }
            else
            {
                var go = UIKit.Rect("Go", body.transform);
                go.Place(new Vector2(1, 0), new Vector2(-24, 18), new Vector2(150, 36));
                var arrow = UIKit.Arrow(go, Role.Brush, 4f);
                arrow.rectTransform.Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(46, 30));
                UIKit.Words(go, Loc.T("play"), 26, Role.Brush, Face.ReadBold, TextAnchor.MiddleRight).rectTransform.Stretch(0, 54, 0, 0);
            }

            if (intro) _ = Motion.Slap(body.rectTransform, tilt, 0.18f + column * 0.08f + index * 0.06f);
        }

        void OpenLogbook(LevelDef focus)
        {
            Sfx.Play(ProceduralAudio.Paper, 0.6f);
            Logbook.Show(procedures, focus, root.gameObject);
        }

        void Play(LevelDef level)
        {
            if (!level.available) return;
            Sfx.Play(ProceduralAudio.Blip, 0.7f);
            SceneFlow.Play(level);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Modal.OpenCount == 0) SceneFlow.Load(SceneFlow.Home);
        }
    }
}
