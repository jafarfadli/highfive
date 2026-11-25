using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// The clinic's waiting monitor (진료 대기 현황): who is in the consulting room and who waits, with the
    /// names masked the way Korean clinics show them (김*수). At a walk-in place the director adds the
    /// player after they register (their row lights up) and moves names into the room as they are
    /// called. It alternates with a few static pages about the other rooms.
    /// </summary>
    public class WaitingBoard : MonoBehaviour
    {
        public Text text;
        public string heading = "진료 대기 현황";
        public string room = "1진료실 · 박준호 원장";
        [Tooltip("Who is in the room when the level starts.")]
        public string current = "김민수";
        public List<string> waiting = new();
        [TextArea] public string[] otherPages;
        public float interval = 8f;
        [Tooltip("How long the live page stays up after a change before the static pages come back.")]
        public float hold = 12f;

        const int PerLine = 3;
        string mine;
        float liveUntil;
        int shown = -1;
        bool dirty = true;

        /// <summary>The player has registered: their name joins the end of the list.</summary>
        public void Join(string name)
        {
            mine = name;
            if (!waiting.Contains(name)) waiting.Add(name);
            Changed();
        }

        /// <summary>A name was called into the room.</summary>
        public void Call(string name)
        {
            current = name;
            waiting.Remove(name);
            Changed();
        }

        /// <summary>The names waiting before the player, in order (set at the start of a walk-in level).</summary>
        public void Queue(IEnumerable<string> names)
        {
            waiting = new List<string>(names);
            Changed();
        }

        void Changed()
        {
            dirty = true;
            liveUntil = Time.time + hold;
        }

        /// <summary>김민수 → 김*수, 박나 → 박*, 민 → 민*.</summary>
        public static string Mask(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (name.Length <= 2) return name[0] + "*";
            return name[0] + new string('*', name.Length - 2) + name[^1];
        }

        void Update()
        {
            if (!text) return;
            int pages = 1 + (otherPages?.Length ?? 0);
            int page = Time.time < liveUntil ? 0 : Mathf.FloorToInt(Time.time / Mathf.Max(1f, interval)) % pages;
            if (page == shown && !(page == 0 && dirty)) return;
            shown = page;
            if (page > 0)
            {
                text.text = otherPages[page - 1];
                return;
            }
            dirty = false;
            var sb = new StringBuilder();
            sb.Append(heading).Append('\n').Append(room).Append('\n');
            sb.Append("진료중  ").Append(Mask(current)).Append('\n');
            sb.Append("대기  ");
            for (int i = 0; i < waiting.Count; i++)
            {
                // three names to a line, so a long queue wraps instead of running off the screen
                if (i > 0) sb.Append(i % PerLine == 0 ? "\n" : " · ");
                var masked = Mask(waiting[i]);
                // the player's own name stands out
                sb.Append(waiting[i] == mine ? $"<color=#FFC23A>{masked}</color>" : masked);
            }
            if (waiting.Count == 0) sb.Append("-");
            text.text = sb.ToString();
        }
    }
}
