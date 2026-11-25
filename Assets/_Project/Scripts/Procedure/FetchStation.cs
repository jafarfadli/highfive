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
    /// <summary>A place where the player picks up a missing document (e.g. the form-writing table).</summary>
    public class FetchStation : ArmedInteractable
    {
        public string stationId;
        public string document;
        public override string Prompt => Loc.T("fill_form");

        /// <summary>Skips the form UI (editor play-tests).</summary>
        public void Complete()
        {
            if (!Armed) return;
            GameSession.I.GiveDocument(document, true);
            Fire();
        }

        protected override void OnFire(PlayerInteractor player)
        {
            if (document == "arc_form")
            {
                FormFillPanel.Show(player.Controller, () =>
                {
                    GameSession.I.GiveDocument(document, true);
                    Fire();
                });
            }
            else
            {
                GameSession.I.GiveDocument(document, true);
                Fire();
            }
        }
    }
}
