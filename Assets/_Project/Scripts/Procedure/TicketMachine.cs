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
    /// <summary>번호표 machine by the entrance.</summary>
    public class TicketMachine : ArmedInteractable
    {
        public Transform slot;
        public override string Prompt => Loc.T("take_ticket");

        protected override void OnFire(PlayerInteractor player)
        {
            Sfx.Play(ProceduralAudio.Paper);
            if (slot)
            {
                var paper = Prims.Box(slot, "Ticket", new Vector3(0, 0, 0.06f), new Vector3(0.07f, 0.005f, 0.12f), Color.white, false);
                Destroy(paper, 2.5f);
            }
            Fire();
        }
    }
}
