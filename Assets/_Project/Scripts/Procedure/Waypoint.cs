using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using UnityEngine;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// A place in the world a `go` step sends the player to: a card reader at the gate, the stairs for one direction,
    /// a queue lane, the baggage carousel, a customs exit, a bus stop. The director arms the step's target and its
    /// wrong turns together; whichever the player uses first answers the step. Pressed with E, or (with
    /// <see cref="walkThrough"/>) used just by walking into its box.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Waypoint : ArmedInteractable
    {
        [Tooltip("The id a procedure's `go` step names as its target or a wrong turn.")]
        public string stationId;
        [Tooltip("Loc key of the prompt (wp_…); {0} is the label.")]
        public string promptKey = "wp_read";
        [Tooltip("The sign or the thing's name in Korean, shown in the prompt.")]
        public string label;
        [Tooltip("Used by walking into the box instead of pressing E (a lane, a customs exit).")]
        public bool walkThrough;
        [Tooltip("Played before the waypoint is armed (the train pulls in and opens its doors).")]
        public SetPiece performer;
        [Tooltip("With a performer: the step is done when the performer is (the ambulance arrives).")]
        public bool autoComplete;
        [Tooltip("Where the player is taken after using it, behind a short fade (down the stairs to the platform).")]
        public Transform moveTo;
        [Tooltip("Switched off once it is used: the suitcase is taken off the belt.")]
        public GameObject[] opens;
        [Tooltip("A gate lane whose flaps fold back, and stay open, once it is used (a card reader).")]
        public World.GateLane gate;

        public override string Prompt => Loc.T(promptKey, label);
        public override bool CanInteract => base.CanInteract && !walkThrough;

        BoxCollider box;
        Transform player;
        bool wasInside;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void Update()
        {
            if (!walkThrough) return;
            if (!player)
            {
                var fpc = FindAnyObjectByType<FirstPersonController>();
                if (!fpc) return;
                player = fpc.transform;
            }
            // the player's feet inside the box (it may be taller than the player; the floor counts). It fires on
            // the way in, so someone who took the wrong stairs and is turning round there isn't told off again.
            bool inside = box.bounds.Contains(player.position + Vector3.up * 0.5f);
            if (inside && !wasInside && Armed) Fire();
            wasInside = inside;
        }

        /// <summary>Uses the waypoint as if the player had (editor play-tests).</summary>
        public void Complete()
        {
            if (Armed) Fire();
        }
    }
}
