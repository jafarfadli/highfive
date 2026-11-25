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
        public float reach = 3.4f;

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

        Interactable Find()
        {
            var cam = fps.Camera;
            Ray ray;
            if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null)
                ray = new Ray(cam.transform.position, cam.transform.forward);
            else
            {
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return null;
                ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            }
            // the invisible street barriers sit on Ignore Raycast
            if (Physics.SphereCast(ray, 0.12f, out var hit, reach, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (it && it.CanInteract) return it;
            }
            return null;
        }
    }
}
