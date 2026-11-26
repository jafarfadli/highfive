using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SeoulFirstWeek.UI
{
    /// <summary>A control that does something while it is held down with the mouse (the "say it" keycap).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Pressed, Released;
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            down = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!down) return;
            down = false;
            Released?.Invoke();
        }

        void OnDisable()
        {
            if (!down) return;
            down = false;
            Released?.Invoke();
        }
    }
}
