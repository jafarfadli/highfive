using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// How inked controls answer the pointer: on hover (or keyboard focus) the piece tilts a little and lifts
    /// off its shadow; pressed, it slams flat onto the shadow. Callers can repaint on hover through
    /// <see cref="Hovered"/>.
    /// </summary>
    public class InkHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [Tooltip("The part that moves (defaults to this transform).")]
        public RectTransform visual;
        [Tooltip("The paper or strip whose hard shadow grows on hover.")]
        public InkShape body;
        public float tilt = -1.5f;
        public float lift = 1.04f;
        public float shadowGrow = 1.6f;

        public event Action<bool> Hovered;

        Selectable selectable;
        bool over, down, selected;
        float baseAngle;
        Vector2 baseShadow, basePos;
        bool captured;
        float k;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (!visual) visual = (RectTransform)transform;
        }

        void Capture()
        {
            if (captured || !visual) return;
            captured = true;
            baseAngle = visual.localEulerAngles.z > 180f ? visual.localEulerAngles.z - 360f : visual.localEulerAngles.z;
            basePos = visual.anchoredPosition;
            if (body) baseShadow = body.shadow;
        }

        bool Interactable => !selectable || selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData e) => SetOver(true);
        public void OnPointerExit(PointerEventData e) { down = false; SetOver(false); }
        public void OnPointerDown(PointerEventData e) => down = Interactable;
        public void OnPointerUp(PointerEventData e) => down = false;
        public void OnSelect(BaseEventData e) { selected = true; SetOver(over); }
        public void OnDeselect(BaseEventData e) { selected = false; SetOver(over); }

        void SetOver(bool on)
        {
            Capture();
            bool was = over || selected;
            over = on;
            bool now = (over || selected) && Interactable;
            if (now != was) Hovered?.Invoke(now);
        }

        void OnDisable()
        {
            over = down = false;
            if (!captured || !visual) return;
            visual.localRotation = Quaternion.Euler(0, 0, baseAngle);
            visual.localScale = Vector3.one;
            visual.anchoredPosition = basePos;
            if (body) body.shadow = baseShadow;
            k = 0;
        }

        void Update()
        {
            if (!captured) return;
            bool hot = (over || selected) && Interactable;
            float target = down ? 2f : hot ? 1f : 0f;
            float speed = Motion.Reduced ? 100f : 18f;
            float next = Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * speed * (target > k ? 1f : 0.7f));
            if (Mathf.Approximately(next, k)) return;
            k = next;
            float h = Mathf.Min(k, 1f), p = Mathf.Max(0f, k - 1f);
            visual.localRotation = Quaternion.Euler(0, 0, baseAngle + tilt * h);
            visual.localScale = Vector3.one * Mathf.Lerp(1f, lift, h) * Mathf.Lerp(1f, 0.985f, p);
            if (body && baseShadow != Vector2.zero)
            {
                var grown = baseShadow * Mathf.Lerp(1f, shadowGrow, h);
                body.shadow = Vector2.Lerp(grown, Vector2.zero, p);
                body.SetVerticesDirty();
                visual.anchoredPosition = basePos + Vector2.Lerp(Vector2.zero, baseShadow * 0.8f, p);
            }
        }
    }
}
