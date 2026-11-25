using SeoulFirstWeek.Core;
using UnityEngine;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// The door of a treatment room (1진료실). At a walk-in place the player walks in by themselves once
    /// their name is called: the director arms the door, and pressing E takes them in. <see cref="outside"/>
    /// is where they stand when they come back out.
    /// </summary>
    public class RoomEntrance : ArmedInteractable
    {
        [Tooltip("The procedure location this door leads to (a step's `location`, e.g. \"doctor\").")]
        public string location = "doctor";
        [Tooltip("The room's name on its door plate, e.g. 1진료실.")]
        public string label = "1진료실";
        [Tooltip("Just outside the door, facing the waiting room: where the player stands after leaving the room.")]
        public Transform outside;
        [Tooltip("Where patients stop in front of the door before they go in.")]
        public Transform approach;

        public override string Prompt => Loc.T("enter", label);
    }
}
