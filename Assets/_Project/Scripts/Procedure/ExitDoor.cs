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
    public class ExitDoor : Interactable
    {
        public ScenarioDirector director;
        public override string Prompt => Loc.T("leave_building");

        public override void Interact(PlayerInteractor player)
        {
            if (director && director.Finished) return;
            Dialog.Confirm(player.Controller, Loc.T("leave_confirm"), () => SceneFlow.Load(SceneFlow.Select));
        }
    }
}
