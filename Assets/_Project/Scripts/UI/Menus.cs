using System;
using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>Base for full-screen modal overlays that free the cursor and freeze the player.</summary>
    public abstract class Modal : MonoBehaviour
    {
        public static int OpenCount { get; private set; }

        protected FirstPersonController player;
        CursorLockMode prevLock;
        bool prevVisible, prevMove, prevLook;

        protected RectTransform Open(FirstPersonController p, string name, int order)
        {
            player = p;
            OpenCount++;
            counted = true;
            prevLock = Cursor.lockState;
            prevVisible = Cursor.visible;
            if (player)
            {
                prevMove = player.MovementEnabled;
                prevLook = player.LookEnabled;
                player.MovementEnabled = false;
                player.LookEnabled = false;
                var interactor = player.GetComponent<PlayerInteractor>();
                if (interactor) interactor.Enabled = false;
            }
            CursorState.Gameplay(false);
            var canvas = UIKit.CreateCanvas(name, order);
            transform.SetParent(canvas.transform, false);
            var scrim = UIKit.InkScrim(canvas.transform, 0.62f, 0.32f);
            scrim.SetAsFirstSibling();
            return (RectTransform)canvas.transform;
        }

        bool counted;

        void Uncount()
        {
            if (!counted) return;
            counted = false;
            OpenCount = Mathf.Max(0, OpenCount - 1);
        }

        protected void Close()
        {
            Uncount();
            if (player)
            {
                player.MovementEnabled = prevMove;
                player.LookEnabled = prevLook;
                var interactor = player.GetComponent<PlayerInteractor>();
                if (interactor) interactor.Enabled = prevMove;
            }
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
            Destroy(transform.parent.gameObject);
        }

        protected virtual void OnDestroy() => Uncount();
    }

    /// <summary>A yes/no question on a slapped-down sheet of paper.</summary>
    public class Dialog : Modal
    {
        public static void Confirm(FirstPersonController player, string text, Action onYes)
        {
            var go = new GameObject("Dialog", typeof(RectTransform));
            var d = go.AddComponent<Dialog>();
            var root = d.Open(player, "DialogCanvas", 80);
            var card = UIKit.Rect("Card", root);
            card.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 0));
            UIKit.VStack(card, 30, new RectOffset(56, 56, 54, 50), TextAnchor.UpperCenter);
            var paper = UIKit.PaperPanel(card, "Paper", Role.Balloon, 3.5f);
            paper.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            paper.rectTransform.Stretch();
            paper.shadow = new Vector2(12, -14);
            UIKit.Words(card, text, 32, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter).lineSpacing = 1.15f;
            var row = UIKit.Rect("Row", card);
            UIKit.HStack(row, 28, null, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            row.Layout(-1, 84);
            UIKit.InkButton(row, Loc.T("no").ToUpperInvariant(), () => d.Close(), ButtonStyle.Paper, 32).Layout(250, 78);
            UIKit.InkButton(row, Loc.T("yes").ToUpperInvariant(), () => { d.Close(); onYes?.Invoke(); }, ButtonStyle.Primary, 32).Layout(250, 78);
            card.localRotation = Quaternion.Euler(0, 0, -1f);
            _ = Motion.Slap(card, -1f);
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
        }
    }

    /// <summary>Esc menu on a torn sheet: resume, settings, reset the level, main menu.</summary>
    public class PauseMenu : MonoBehaviour
    {
        FirstPersonController player;
        InputAction pause;
        GameObject menu;
        RectTransform card;
        CursorLockMode prevLock;
        bool prevVisible;

        public static PauseMenu Create(FirstPersonController player)
        {
            var go = new GameObject("PauseMenu");
            var p = go.AddComponent<PauseMenu>();
            p.player = player;
            p.pause = player.Map.FindAction("Pause", true);
            return p;
        }

        public bool IsOpen => menu;

        void Update()
        {
            if (!pause.WasPressedThisFrame()) return;
            if (menu) Hide();
            else if (Modal.OpenCount == 0) Show();
        }

        void OnDestroy()
        {
            if (menu) Hide();
        }

        public void Show()
        {
            prevLock = Cursor.lockState;
            prevVisible = Cursor.visible;
            Time.timeScale = 0;
            AudioListener.pause = true;
            CursorState.Gameplay(false);
            var canvas = UIKit.CreateCanvas("PauseCanvas", 70);
            menu = canvas.gameObject;
            UIKit.InkScrim(canvas.transform, 0.66f, 0.32f);
            card = UIKit.Rect("Card", canvas.transform);
            card.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 0));
            UIKit.VStack(card, 16, new RectOffset(54, 54, 46, 50), TextAnchor.UpperCenter);
            card.localRotation = Quaternion.Euler(0, 0, -1f);
            MainView();
            _ = Motion.Slap(card, -1f);
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
        }

        void Clear()
        {
            foreach (Transform t in card) Destroy(t.gameObject);
            var paper = UIKit.PaperPanel(card, "Paper", Role.Paper, 5f);
            paper.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            paper.rectTransform.Stretch();
            paper.shadow = new Vector2(12, -14);
        }

        void Title(string text, string sub = null)
        {
            // the title in cream letters on an orange band across the card
            var band = UIKit.Rect("TitleBand", card);
            UIKit.VStack(band, 0, new RectOffset(0, 0, 6, 10), TextAnchor.MiddleCenter, false);
            var strip = UIKit.BrushStrip(band, "Band", Role.Brush);
            strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            strip.rectTransform.Stretch(-24, -24, 0, 0);
            strip.dry = 0.4f;
            UIKit.Lettering(band, text.ToUpperInvariant(), 64, Role.Paper, TextAnchor.MiddleCenter).Layout(-1, 76);
            if (!string.IsNullOrEmpty(sub)) UIKit.Words(card, sub, 26, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter).Layout(-1, 36);
            UIKit.Rect("Gap", card).Layout(-1, 6);
        }

        void MainView()
        {
            Clear();
            var level = GameSession.I.CurrentLevel;
            Title(Loc.T("pause_title"), level?.FullTitle);
            Button(card, Loc.T("resume"), ButtonStyle.Primary, Hide);
            Button(card, Loc.T("settings"), ButtonStyle.Paper, SettingsView);
            // a reset restarts the whole level: back outside, story first
            Button(card, Loc.T("reset"), ButtonStyle.Paper, () => ConfirmView(Loc.T("reset_confirm"), () =>
            {
                Hide();
                if (level != null) SceneFlow.Play(level);
                else SceneFlow.Reload();
            }));
            Button(card, Loc.T("main_menu"), ButtonStyle.Paper, () => ConfirmView(Loc.T("menu_confirm"), () =>
            {
                Hide();
                SceneFlow.Load(SceneFlow.Home);
            }));
        }

        void SettingsView()
        {
            Clear();
            Title(Loc.T("settings"));
            SettingsPanel.Build(card);
            UIKit.Rect("Gap", card).Layout(-1, 6);
            Button(card, "←  " + Loc.T("back"), ButtonStyle.Night, MainView);
        }

        void ConfirmView(string question, Action onYes)
        {
            Clear();
            UIKit.Words(card, question, 30, Role.Ink, Face.ReadBold, TextAnchor.MiddleCenter).Layout(-1, 120);
            var row = UIKit.Rect("Row", card);
            UIKit.HStack(row, 24, null, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            row.Layout(-1, 80);
            UIKit.InkButton(row, Loc.T("no").ToUpperInvariant(), MainView, ButtonStyle.Paper, 30).Layout(240, 74);
            UIKit.InkButton(row, Loc.T("yes").ToUpperInvariant(), () => onYes(), ButtonStyle.Primary, 30).Layout(240, 74);
        }

        static void Button(Transform parent, string label, ButtonStyle style, Action onClick) =>
            UIKit.InkButton(parent, label.ToUpperInvariant(), onClick, style, 32).Layout(-1, 76);

        public void Hide()
        {
            if (!menu) return;
            Destroy(menu);
            menu = null;
            Time.timeScale = 1;
            AudioListener.pause = false;
            Settings.Save();
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
        }
    }

    /// <summary>Music, SFX, look sensitivity, calm animation and the level (pause menu).</summary>
    public static class SettingsPanel
    {
        public static void Build(Transform parent)
        {
            Row(parent, Loc.T("music"), 0, 1, Settings.Music, v => Settings.Music = v, v => $"{Mathf.RoundToInt(v * 100)}%");
            Row(parent, Loc.T("sfx"), 0, 1, Settings.Sfx, v =>
            {
                Settings.Sfx = v;
                Sfx.Play(ProceduralAudio.Tick, 0.6f);
            }, v => $"{Mathf.RoundToInt(v * 100)}%");
            Row(parent, Loc.T("sensitivity"), 0.25f, 2.5f, Settings.Sensitivity, v => Settings.Sensitivity = v, v => $"{v:0.00}×");
            Choices(parent, Loc.T("reduced_motion"), new[] { Loc.T("off"), Loc.T("on") }, Settings.ReducedMotion ? 1 : 0, i => Settings.ReducedMotion = i == 1);
            Choices(parent, Loc.T("difficulty"), new[] { Loc.T("difficulty_0"), Loc.T("difficulty_1"), Loc.T("difficulty_2") }, (int)Settings.Difficulty,
                i => Settings.Difficulty = (Difficulty)i);
        }

        static void Row(Transform parent, string label, float min, float max, float value, Action<float> set, Func<float, string> format)
        {
            var row = UIKit.Rect("Setting", parent);
            row.Layout(-1, 58);
            UIKit.HStack(row, 18, new RectOffset(4, 4, 0, 0), TextAnchor.MiddleLeft).childForceExpandHeight = true;
            UIKit.Words(row, label, 23, Role.Ink, Face.ReadBold).Layout(196);
            Text readout = null;
            var slider = UIKit.InkSlider(row, min, max, value, v =>
            {
                set(v);
                readout.text = format(v);
            });
            slider.Layout(-1, -1, 1);
            readout = UIKit.Words(row, format(value), 26, Role.Ink, Face.ReadBold, TextAnchor.MiddleRight);
            readout.Layout(84);
        }

        /// <summary>A row of paper tags; the chosen one is circled.</summary>
        static void Choices(Transform parent, string label, string[] options, int chosen, Action<int> pick)
        {
            var row = UIKit.Rect("Choice", parent);
            row.Layout(-1, 64);
            UIKit.HStack(row, 16, new RectOffset(4, 4, 0, 0), TextAnchor.MiddleLeft).childForceExpandHeight = false;
            UIKit.Words(row, label, 23, Role.Ink, Face.ReadBold).Layout(196, 56);
            var rings = new List<InkShape>();
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                InkShape ring = null;
                var b = UIKit.InkButton(row, "", () =>
                {
                    pick(index);
                    Sfx.Play(ProceduralAudio.Tick, 0.6f);
                    for (int r = 0; r < rings.Count; r++) rings[r].gameObject.SetActive(r == index);
                    _ = Motion.Draw(rings[index], 0.28f);
                }, ButtonStyle.Paper, 22);
                var t = b.GetComponentInChildren<Text>();
                t.font = UIKit.Bold;
                t.fontSize = 22;
                t.text = options[i];
                float w = t.preferredWidth + 52f;
                b.Layout(w, 52);
                ring = UIKit.CircleMark(b.transform, Role.Brush, 5f);
                ring.rectTransform.Stretch(-18, -18, -14, -14);
                ring.gameObject.SetActive(i == chosen);
                rings.Add(ring);
            }
        }
    }

    /// <summary>The ARC application form: tick the right box, then sign.</summary>
    public class FormFillPanel : Modal
    {
        public static void Show(FirstPersonController player, Action onDone)
        {
            var go = new GameObject("Form", typeof(RectTransform));
            var f = go.AddComponent<FormFillPanel>();
            var root = f.Open(player, "FormCanvas", 60);
            var s = GameSession.I;

            var paper = UIKit.Rect("Form", root);
            paper.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 0));
            UIKit.VStack(paper, 14, new RectOffset(60, 60, 50, 46));
            paper.localRotation = Quaternion.Euler(0, 0, 0.8f);
            var sheet = UIKit.PaperPanel(paper, "Sheet", Role.Balloon, 3f);
            sheet.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            sheet.rectTransform.Stretch();
            sheet.radius = 2f;
            sheet.shadow = new Vector2(10, -12);
            UIKit.Words(paper, "통합신청서 (신고서)", 36, Role.Ink, Face.DisplayKo, TextAnchor.MiddleCenter);
            UIKit.Words(paper, "APPLICATION FORM (REPORT FORM)", 26, Role.Ink, Face.Display, TextAnchor.MiddleCenter);
            UIKit.Words(paper, Loc.T("form_instruction"), 26, Role.Brush, Face.ReadBold, TextAnchor.MiddleCenter);

            var options = new List<(string ko, string en, bool correct)>
            {
                ("외국인 등록", "FOREIGN RESIDENT REGISTRATION", true),
                ("체류기간 연장허가", "EXTENSION OF SOJOURN PERIOD", false),
                ("재입국 허가", "RE-ENTRY PERMIT", false),
                ("체류자격 변경허가", "CHANGE OF STATUS OF SOJOURN", false),
            }.OrderBy(_ => UnityEngine.Random.value).ToList();

            Button sign = null;
            var ticks = new List<InkShape>();
            foreach (var (ko, en, correct) in options)
            {
                InkShape tick = null;
                var row = UIKit.Rect("Option", paper);
                row.Layout(-1, 68);
                var rule = UIKit.PenStroke(row, Role.Ink, 2f, new Vector2(0, 0.02f), new Vector2(1, 0.02f));
                rule.fillAlpha = 0.35f;
                rule.rectTransform.Stretch();
                var box = UIKit.PaperPanel(row, "Box", Role.Balloon, 3f);
                box.shadow = Vector2.zero;
                box.radius = 2f;
                box.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(44, 44));
                tick = UIKit.PenStroke(box.transform, Role.Brush, 6f, new Vector2(0.1f, 0.5f), new Vector2(0.4f, 0.12f), new Vector2(1.05f, 1.05f));
                tick.rectTransform.Stretch(-4, -8, -8, -4);
                tick.gameObject.SetActive(false);
                ticks.Add(tick);
                var label = UIKit.Words(row, $"{ko}   <size=22><color={Theme.Html(Role.InkSoft)}>{en}</color></size>", 28, Role.Ink, Face.ReadBold);
                label.rectTransform.Stretch(80, 10, 0, 0);
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    if (correct)
                    {
                        foreach (var t in ticks) t.gameObject.SetActive(false);
                        tick.gameObject.SetActive(true);
                        _ = Motion.Draw(tick, 0.25f);
                        Sfx.Play(ProceduralAudio.Blip);
                        SetSignable(sign, true);
                    }
                    else
                    {
                        Sfx.Play(ProceduralAudio.Soft, 0.6f);
                        Toasts.Warn(Loc.T("form_wrong"));
                    }
                });
            }

            // Minh is the Vietnamese student of every story; only the Indonesian hints make the player Indonesian
            string nationality = s.Language switch
            {
                HintLanguage.Indonesian => "INDONESIA",
                _ => "VIET NAM",
            };
            UIKit.Words(paper,
                $"<b>성명 Name</b>  {s.PlayerName.ToUpperInvariant()}      <b>국적 Nationality</b>  {nationality}\n<b>체류자격 Status</b>  D-4      <b>주소 Address in Korea</b>  서울특별시 동작구 흑석로 84",
                23, Role.Ink, Face.Read).lineSpacing = 1.25f;

            sign = UIKit.InkButton(paper, "서명하기 · SIGN", () =>
            {
                Sfx.Play(ProceduralAudio.Paper);
                Toasts.Show(Loc.T("form_done"));
                f.Close();
                onDone?.Invoke();
            }, ButtonStyle.Primary, 32);
            sign.Layout(-1, 80);
            SetSignable(sign, false);
            _ = Motion.Slap(paper, 0.8f);
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
        }

        /// <summary>
        /// Before a box is ticked the sign button is set aside, never see-through: an opaque kraft bar with
        /// soft-ink lettering. Ticking the right box turns it into the brush bar.
        /// </summary>
        static void SetSignable(Button sign, bool on)
        {
            sign.interactable = on;
            var body = sign.GetComponentInChildren<InkShape>();
            body.fillRole = on ? Role.Brush : Role.PaperDeep;
            body.fillAlpha = 1f;
            body.SetVerticesDirty();
            var label = sign.GetComponentInChildren<Text>().GetComponent<Themed>();
            if (label) label.Set(on ? Role.OnBrush : Role.InkSoft);
            if (on) _ = Motion.Pop((RectTransform)sign.transform, 1.06f, 0.2f);
        }
    }
}
