using System;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// An analogue wall clock that keeps the player's local time. The hands are pivots on the clock face
    /// (front = +Z) that turn about their local Z axis; the second hand ticks once a second.
    /// </summary>
    public class WallClock : MonoBehaviour
    {
        public Transform hour, minute, second;

        void Update()
        {
            var now = DateTime.Now;
            float m = now.Minute + now.Second / 60f;
            float h = now.Hour % 12 + m / 60f;
            // seen from the front (+Z), clockwise is a positive turn about +Z
            if (hour) hour.localRotation = Quaternion.Euler(0, 0, h * 30f);
            if (minute) minute.localRotation = Quaternion.Euler(0, 0, m * 6f);
            if (second) second.localRotation = Quaternion.Euler(0, 0, now.Second * 6f);
        }
    }
}
