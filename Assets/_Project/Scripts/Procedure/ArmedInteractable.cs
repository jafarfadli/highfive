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
        GuideMarker marker;
        public bool Armed => tcs != null;
        public override bool CanInteract => base.CanInteract && Armed;

        /// <summary>
        /// Whether a <see cref="GuideMarker"/> floats over it while it is armed. The director turns it off for a
        /// step that tests a choice (where the signs are the lesson) and for all but one of many identical targets.
        /// </summary>
        [NonSerialized] public bool guide = true;

        public Task WaitAsync(CancellationToken ct)
        {
            tcs = new TaskCompletionSource<bool>();
            ct.Register(() =>
            {
                tcs?.TrySetCanceled();
                ShowGuide(false);
            });
            ShowGuide(guide);
            return tcs.Task;
        }

        /// <summary>Stops waiting without completing (another way through the step was taken).</summary>
        public void Disarm()
        {
            var t = tcs;
            tcs = null;
            ShowGuide(false);
            t?.TrySetCanceled();
        }

        protected void Fire()
        {
            var t = tcs;
            tcs = null;
            ShowGuide(false);
            t?.TrySetResult(true);
        }

        void ShowGuide(bool on)
        {
            if (!this) return;
            if (on && !marker) marker = GuideMarker.Attach(this);
            else if (!on && marker)
            {
                Destroy(marker.gameObject);
                marker = null;
            }
        }

        public override void Interact(PlayerInteractor player)
        {
            if (Armed) OnFire(player);
        }

        protected virtual void OnFire(PlayerInteractor player) => Fire();
    }
}
