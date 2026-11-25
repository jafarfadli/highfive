using System;
using UnityEngine;

namespace SeoulFirstWeek.Player
{
    /// <summary>Interactable whose prompt and action are supplied from code.</summary>
    public class SimpleInteractable : Interactable
    {
        public Func<string> PromptText;
        public Func<bool> Available;
        public Action<PlayerInteractor> OnInteract;

        public override string Prompt => PromptText != null ? PromptText() : name;
        public override bool CanInteract => isActiveAndEnabled && (Available == null || Available());
        public override void Interact(PlayerInteractor player) => OnInteract?.Invoke(player);
    }
}
