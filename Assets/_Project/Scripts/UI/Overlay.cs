using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// Persistent top-most canvas: the panel-cut scene transition (a huge ink sheet sweeping across the
    /// screen, stepped like hand-drawn animation) and toast notes slapped onto the top-right corner.
    /// </summary>
    public class Overlay : MonoBehaviour
    {
        static Overlay instance;

        public static Overlay I
        {
            get
            {
                if (instance == null)
                {
                    var canvas = UIKit.CreateCanvas("[Overlay]", 1000);
                    DontDestroyOnLoad(canvas.gameObject);
                    instance = canvas.gameObject.AddComponent<Overlay>();
                    instance.Build();
                }
                return instance;
            }
        }

        const float Park = 3800f;

        RectTransform cut;
        Image blocker;
        RectTransform toastColumn;
        bool covered;

        void Build()
        {
            toastColumn = UIKit.Rect("Toasts", transform);
            toastColumn.Place(new Vector2(1, 1), new Vector2(-44, -112), new Vector2(420, 0));
            UIKit.VStack(toastColumn, 20, null, TextAnchor.UpperRight);

            blocker = UIKit.Rect("Blocker", transform).Stretch().gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = false;

            // the panel cut: a brush-coloured edge riding just ahead of a sheet of ink, turned a few degrees
            cut = UIKit.Rect("Cut", transform);
            cut.anchorMin = cut.anchorMax = new Vector2(0.5f, 0.5f);
            cut.sizeDelta = new Vector2(3400, 2600);
            cut.localRotation = Quaternion.Euler(0, 0, -8f);
            var edge = UIKit.Ink(cut, "Edge", InkKind.Panel, Role.Brush, 0f);
            edge.rectTransform.Stretch(-60, 30, 0, 0);
            edge.shadow = Vector2.zero;
            edge.radius = 0f;
            edge.rough = 10f;
            edge.tear = 5f;
            var sheet = UIKit.Ink(cut, "Sheet", InkKind.Panel, Role.Ink, 0f);
            sheet.rectTransform.Stretch();
            sheet.shadow = Vector2.zero;
            sheet.radius = 0f;
            sheet.rough = 9f;
            sheet.tear = 4f;
            sheet.halftone = 0f;
            cut.anchoredPosition = new Vector2(-Park, 0);
            // on a panel in a headset it would hang in the air beside the view: VRUI.Fade covers the eyes instead
            if (Core.VRMode.Active) cut.gameObject.SetActive(false);
        }

        /// <summary>
        /// Scene transitions: alpha 1 sweeps the ink sheet across to cover the screen, alpha 0 sweeps it on and
        /// off to the right (the names are kept from the old fade so callers stay the same).
        /// </summary>
        public async Task FadeTo(float alpha, float seconds)
        {
            bool cover = alpha >= 0.5f;
            if (Core.VRMode.Active)
            {
                // a sheet sweeping across a panel can't cover the eyes: in a headset the ink comes up over the view
                if (cover == covered) return;
                blocker.raycastTarget = true;
                await VRUI.Fade(cover, Mathf.Clamp(seconds * 0.75f, 0.2f, 0.4f));
                if (!this) return;
                covered = cover;
                blocker.raycastTarget = cover;
                if (!cover) VRUI.Recenter();
                return;
            }
            if (cover == covered && (cover || cut.anchoredPosition.x <= -Park + 1f)) return;
            blocker.raycastTarget = true;
            float from = cover ? -Park : 0f, to = cover ? 0f : Park;
            float duration = Mathf.Clamp(seconds * 0.75f, 0.28f, 0.5f);
            await Motion.Tween(this, duration, k =>
            {
                float e = k * k * (3f - 2f * k);
                cut.anchoredPosition = new Vector2(Mathf.Lerp(from, to, e), 0);
            }, true);
            if (!this) return;
            covered = cover;
            if (!cover)
            {
                cut.anchoredPosition = new Vector2(-Park, 0);
                blocker.raycastTarget = false;
            }
        }

        public void ClearToasts()
        {
            foreach (Transform t in toastColumn) Destroy(t.gameObject);
        }

        /// <summary>A note slapped onto the corner: paper with a ○ for good news, a glow sticky note with ! for warnings.</summary>
        public void Toast(string title, string subtitle, bool good, float seconds)
        {
            var root = UIKit.Rect("Toast", toastColumn);
            UIKit.VStack(root, 4, new RectOffset(30, 30, 26, 26), TextAnchor.UpperLeft);
            root.Layout(440);
            var note = good ? UIKit.PaperPanel(root, "Note", Role.Balloon, 2.5f) : UIKit.StickyNote(root, "Note");
            note.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            note.rectTransform.Stretch();
            if (good) note.shadow = new Vector2(5, -6);

            var head = UIKit.Rect("Head", root);
            UIKit.HStack(head, 12, null, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            var mark = UIKit.Words(head, good ? "✓" : "!", 34, good ? Role.Brush : Role.BrushDeep, Face.Display, TextAnchor.MiddleCenter);
            mark.Layout(30, 40);
            var t = UIKit.Words(head, title, 30, Role.Ink, Face.Display);
            t.Layout(340);
            if (!string.IsNullOrEmpty(subtitle))
                UIKit.Words(root, subtitle, 22, Role.Ink, Face.ReadBold).lineSpacing = 1.15f;

            root.localRotation = Quaternion.Euler(0, 0, Random.Range(-2.5f, 2.5f));
            Sfx.Play(ProceduralAudio.Paper, 0.5f);
            _ = Life(root, seconds);
        }

        async Task Life(RectTransform root, float seconds)
        {
            float rest = root.localEulerAngles.z > 180f ? root.localEulerAngles.z - 360f : root.localEulerAngles.z;
            await Motion.Slap(root, rest, 0f, 8f);
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                await Awaitable.NextFrameAsync();
                if (!root) return;
            }
            await Motion.Peel(root);
        }
    }

    public static class Toasts
    {
        public static void Show(string title, string subtitle = null, float seconds = 3.5f) =>
            Overlay.I.Toast(title, subtitle, true, seconds);

        public static void Warn(string title, string subtitle = null, float seconds = 4f) =>
            Overlay.I.Toast(title, subtitle, false, seconds);
    }
}
