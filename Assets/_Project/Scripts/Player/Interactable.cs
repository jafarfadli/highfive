using System;
using UnityEngine;

namespace SeoulFirstWeek.Player
{
    /// <summary>Anything the player can look at and press Interact on.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual string Prompt => name;
        public virtual bool CanInteract => isActiveAndEnabled;
        public abstract void Interact(PlayerInteractor player);
    }
}
