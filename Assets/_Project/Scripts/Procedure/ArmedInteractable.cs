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
    /// <summary>Awaitable interactable: armed by the director, completes when the player presses E.</summary>
    public abstract class ArmedInteractable : Interactable
    {
        TaskCompletionSource<bool> tcs;
        public bool Armed => tcs != null;
        public override bool CanInteract => base.CanInteract && Armed;

        public Task WaitAsync(CancellationToken ct)
        {
            tcs = new TaskCompletionSource<bool>();
            ct.Register(() => tcs?.TrySetCanceled());
            return tcs.Task;
        }

        protected void Fire()
        {
            var t = tcs;
            tcs = null;
            t?.TrySetResult(true);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (Armed) OnFire(player);
        }

        protected virtual void OnFire(PlayerInteractor player) => Fire();
    }
}
