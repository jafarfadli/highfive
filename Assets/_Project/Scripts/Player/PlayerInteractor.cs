using System.Collections.Generic;
using SeoulFirstWeek.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SeoulFirstWeek.Player
{
    /// <summary>
    /// Finds the Interactable under the mouse pointer (or the screen centre while dragging
    /// the camera) and triggers it on Interact or on a left click.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerInteractor : MonoBehaviour
    {
        public float reach = 3.6f;

        FirstPersonController fps;
        InputAction interact;
        Interactable current;

        public bool Enabled { get; set; } = true;
        public FirstPersonController Controller => fps;

        void Awake()
        {
            fps = GetComponent<FirstPersonController>();
            interact = fps.actions.FindActionMap("Player", true).FindAction("Interact", true);
        }

        void Update()
        {
            current = Enabled ? Find() : null;
            var hud = Hud.Current;
            if (hud)
            {
                hud.SetPrompt(current ? current.Prompt : null);
                hud.SetCrosshair(Enabled && fps.Dragging && Cursor.lockState == CursorLockMode.Locked);
            }
            if (current && (interact.WasPressedThisFrame() || fps.ClickedThisFrame)) current.Interact(this);
        }

        static readonly RaycastHit[] Hits = new RaycastHit[32];
        static readonly IComparer<RaycastHit> ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        /// <summary>
        /// The nearest thing along the pointer that can be used. Standing right at it counts (a cast never sees a
        /// collider it starts inside, so the counter's or the door's box used to vanish when the player stepped
        /// up close); people and triggers that can't be used don't stand in the way; walls and furniture do.
        /// </summary>
        /// <summary>Where the pointer meets the thing it would use this frame (the VR laser ends there).</summary>
        public static Vector3? CurrentHit { get; private set; }

        Interactable Find()
        {
            CurrentHit = null;
            var cam = fps.Camera;
            Ray ray;
            if (fps.VR)
            {
                // in a headset the right controller points; the panels take what it points at first
                if (UI.VRUI.PointerOverUI) return null;
                ray = fps.VR.PointerRay;
            }
            else if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null)
                ray = new Ray(cam.transform.position, cam.transform.forward);
            else
            {
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return null;
                ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            }
            // the invisible street barriers and walk-through boxes sit on Ignore Raycast
            int n = Physics.SphereCastNonAlloc(ray, 0.2f, Hits, reach, ~(1 << 2), QueryTriggerInteraction.Collide);
            System.Array.Sort(Hits, 0, n, ByDistance);
            var flatDir = new Vector3(ray.direction.x, 0, ray.direction.z).normalized;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it && it.CanInteract)
                {
                    if (h.distance > 0f)
                    {
                        CurrentHit = h.point;
                        return it;
                    }
                    // already inside its box (distance 0): it counts while the player faces its way
                    var to = h.collider.bounds.center - ray.origin;
                    to.y = 0;
                    if (to.sqrMagnitude < 0.04f || Vector3.Dot(to.normalized, flatDir) > -0.05f)
                    {
                        CurrentHit = ray.origin + ray.direction * 0.6f;
                        return it;
                    }
                    continue;
                }
                // started inside it (the player's own capsule), a trigger, or a person: look past it
                if (h.distance <= 0f || h.collider.isTrigger || h.collider.GetComponentInParent<World.HumanRig>()) continue;
                break;
            }
            return null;
        }
    }
}
