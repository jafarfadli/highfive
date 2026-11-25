using System;
using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Conversation;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.UI;
using SeoulFirstWeek.World;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// LED number board. The main board shows the latest call ("0047 → 2번 창구") and, when it has
    /// <see cref="rows"/>, the last number of every counter; a counter's own small display (<see cref="window"/>
    /// set) only changes when its counter is called. The called number blinks for a few seconds.
    /// </summary>
    public class QueueBoard : MonoBehaviour
    {
        public Text numberText;
        public Text counterText;
        [Tooltip("0 = the main board; otherwise the counter whose own display this is.")]
        public int window;
        [Tooltip("Optional per-counter rows on the main board (index 0 = counter 1).")]
        public Text[] rows;
        float blinkUntil;
        Text blinking;

        void Start()
        {
            // before the first call, every display shows the last number its counter served
            int start = UnityEngine.Random.Range(18, 36);
            if (numberText && numberText.text == "0000" && window > 0) numberText.text = (start + window * 3).ToString("0000");
            if (rows != null)
                for (int i = 0; i < rows.Length; i++)
                    if (rows[i]) rows[i].text = (start + i * 3).ToString("0000");
        }

        public void Show(int number, int counter)
        {
            if (window > 0 && counter != window) return;
            if (numberText) numberText.text = number.ToString("0000");
            if (counterText) counterText.text = $"→ {counter}번 창구";
            if (blinking) blinking.enabled = true;
            blinking = numberText;
            if (rows != null && counter >= 1 && counter <= rows.Length && rows[counter - 1])
            {
                rows[counter - 1].text = number.ToString("0000");
                if (!numberText) blinking = rows[counter - 1];
            }
            blinkUntil = Time.time + 3f;
        }

        void Update()
        {
            if (!blinking) return;
            bool on = Time.time > blinkUntil || Mathf.FloorToInt(Time.time * 4f) % 2 == 0;
            blinking.enabled = on;
        }
    }
}
