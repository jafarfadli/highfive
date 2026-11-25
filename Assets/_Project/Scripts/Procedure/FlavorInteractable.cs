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
    /// <summary>Flavour interactable (ATM, poster) that just shows a toast.</summary>
    public class FlavorInteractable : Interactable
    {
        public string promptKey;
        public string messageKey;
        public override string Prompt => Loc.T(promptKey);
        public override void Interact(PlayerInteractor player) => Toasts.Warn(Loc.T(messageKey));
    }
}
