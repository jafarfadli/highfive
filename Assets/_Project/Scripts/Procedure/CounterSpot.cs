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
    /// One service window. The scene has one per window; the director arms the window the current
    /// procedure calls the player to (`queue.counter`).
    /// </summary>
    public class CounterSpot : ArmedInteractable
    {
        public string staffName;
        public int counterNumber = 2;
        [Tooltip("The customer chair at a low counter; its colliders are switched off when the player sits there.")]
        public GameObject chair;
        public override string Prompt => Loc.T("talk_to", $"{staffName} ({Loc.T("counter_n", counterNumber)})");
    }
}
