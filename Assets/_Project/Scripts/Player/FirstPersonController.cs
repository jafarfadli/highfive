using System.Collections;
using SeoulFirstWeek.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SeoulFirstWeek.Player
{
    /// <summary>
    /// First-person walker driven only by Input System actions (Move, Look, LookHold, Sprint),
    /// so the rig can be swapped for an XR origin later. Mouse look is "hold left button and drag";
    /// the cursor stays free otherwise so the player can point at things and click UI.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public InputActionAsset actions;
        public Transform cameraPivot;
        public float walkSpeed = 3.0f;
        public float sprintSpeed = 5.6f;
        public float mouseSensitivity = 0.2f;
        public float stickSensitivity = 140f;
        public float gravity = -20f;
        public float eyeHeight = 1.62f;
        [Tooltip("Eye height while sitting at a counter or on a stool.")]
        public float seatedEyeHeight = 1.2f;

        // The view eases towards where the input points over about this long (seconds): it evens out mouse
        // events that arrive unevenly between frames without making the camera feel floaty.
        const float LookEase = 0.04f;
        // Frames of mouse movement ignored after the cursor is locked or freed. Locking moves the cursor to
        // the middle of the window, and the OS reports that jump as movement, which swung the camera back.
        const int SettleFrames = 2;

        CharacterController controller;
        InputActionMap map;
        InputAction move, look, lookHold, sprint;
        float pitch, yaw, viewPitch, viewYaw, verticalVelocity, bobPhase, stepTimer, eye = -1f;
        Coroutine focusRoutine;
        bool dragging;
        Vector2 dragDistance;
        int settle;
        // set when the cursor is locked: the jump comes with the first movement after it, which is the next
        // frame while the mouse keeps moving but later if the player paused just as the cursor locked
        bool awaitJump;

        public bool MovementEnabled { get; set; } = true;
        public bool LookEnabled { get; set; } = true;
        public InputActionMap Map => map;
        public Camera Camera { get; private set; }

        /// <summary>True while the player sits (the camera is lowered to <see cref="seatedEyeHeight"/>).</summary>
        public bool Seated { get; private set; }

        float TargetEye => Seated ? seatedEyeHeight : eyeHeight;

        /// <summary>Sit down or stand up; the camera eases to the new height unless <paramref name="instant"/>.</summary>
        public void SetSeated(bool seated, bool instant = false)
        {
            Seated = seated;
            if (instant) eye = TargetEye;
        }

        /// <summary>True while the player is holding the mouse button to look around.</summary>
        public bool Dragging => dragging;

        /// <summary>True for a short press-and-release without real mouse movement (a click, not a drag).</summary>
        public bool ClickedThisFrame { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Camera = cameraPivot ? cameraPivot.GetComponentInChildren<Camera>() : GetComponentInChildren<Camera>();
            map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            lookHold = map.FindAction("LookHold", true);
            sprint = map.FindAction("Sprint", true);
            yaw = viewYaw = transform.eulerAngles.y;
            if (Core.VRMode.Active) vr = VRRig.Attach(this);
        }

        VRRig vr;

        /// <summary>The headset rig, while the game runs in VR.</summary>
        public VRRig VR => vr;

        void OnEnable() => map?.Enable();
        void OnDisable() => map?.Disable();

        void Update()
        {
            var dt = Time.deltaTime;
            if (vr)
            {
                UpdateVR(dt);
                return;
            }
            // taken before UpdateDrag, which may lock the cursor: the jump that causes arrives from the next frame
            bool settling = settle > 0;
            if (settling) settle--;
            UpdateDrag();
            if (LookEnabled && focusRoutine == null)
            {
                var delta = look.ReadValue<Vector2>();
                bool stick = look.activeControl != null && look.activeControl.device is Gamepad;
                if (!stick && (settling || awaitJump))
                {
                    if (delta != Vector2.zero) awaitJump = false;
                    delta = Vector2.zero;
                }
                if (stick || dragging)
                {
                    var scale = (stick ? stickSensitivity * dt : mouseSensitivity) * Core.Settings.Sensitivity;
                    yaw += delta.x * scale;
                    pitch = Mathf.Clamp(pitch - delta.y * scale, -80f, 80f);
                }
            }

            // FocusOn eases on its own, so the view follows it exactly; Teleport sets both
            if (focusRoutine != null)
            {
                viewYaw = yaw;
                viewPitch = pitch;
            }
            else
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / LookEase);
                // yaw grows past 360 as the player turns, and FocusOn ends on the same heading written within
                // ±180: easing by angle keeps the view from whirling a full turn between the two
                viewYaw = Mathf.LerpAngle(viewYaw, yaw, k);
                viewPitch = Mathf.Lerp(viewPitch, pitch, k);
            }
            transform.rotation = Quaternion.Euler(0, viewYaw, 0);

            var input = MovementEnabled ? move.ReadValue<Vector2>() : Vector2.zero;
            var speed = sprint.IsPressed() ? sprintSpeed : walkSpeed;
            var planar = (transform.right * input.x + transform.forward * input.y) * speed;
            verticalVelocity = controller.isGrounded ? -2f : verticalVelocity + gravity * dt;
            controller.Move((planar + Vector3.up * verticalVelocity) * dt);

            // head bob and footsteps
            float moving = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            if (moving > 0.2f && controller.isGrounded)
            {
                bobPhase += dt * moving * 2.2f;
                stepTimer -= dt * moving;
                if (stepTimer <= 0)
                {
                    Sfx.Play(ProceduralAudio.Step, 0.35f);
                    stepTimer = 1.55f;
                }
            }
            float bob = Mathf.Sin(bobPhase) * 0.035f * Mathf.Clamp01(moving / walkSpeed);
            eye = eye < 0 ? TargetEye : Mathf.MoveTowards(eye, TargetEye, dt * 1.4f);
            cameraPivot.localPosition = new Vector3(0, eye + bob, 0);
            cameraPivot.localRotation = Quaternion.Euler(viewPitch, 0, 0);
        }

        /// <summary>
        /// In a headset the head looks around by itself: no mouse look, no pitch, no head bob. The stick walks where
        /// the head looks, and the eyes ease to the standing or seated height.
        /// </summary>
        void UpdateVR(float dt)
        {
            ClickedThisFrame = false;
            eye = eye < 0 ? TargetEye : Mathf.MoveTowards(eye, TargetEye, dt * 1.4f);
            vr.Tick(eye);
            var input = MovementEnabled ? move.ReadValue<Vector2>() : Vector2.zero;
            var speed = sprint.IsPressed() ? sprintSpeed : walkSpeed;
            var basis = vr.HeadYaw;
            var planar = (basis * Vector3.right * input.x + basis * Vector3.forward * input.y) * speed;
            verticalVelocity = controller.isGrounded ? -2f : verticalVelocity + gravity * dt;
            controller.Move((planar + Vector3.up * verticalVelocity) * dt);
            if (MovementEnabled) vr.FollowHead();
            yaw = viewYaw = transform.eulerAngles.y;

            float moving = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            if (moving > 0.2f && controller.isGrounded)
            {
                stepTimer -= dt * moving;
                if (stepTimer <= 0)
                {
                    Sfx.Play(ProceduralAudio.Step, 0.35f);
                    stepTimer = 1.55f;
                }
            }
        }

        void UpdateDrag()
        {
            ClickedThisFrame = false;
            if (!LookEnabled)
            {
                if (dragging) EndDrag();
                return;
            }
            if (lookHold.WasPressedThisFrame())
            {
                // presses that start on UI belong to the UI, not the camera
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    dragging = true;
                    dragDistance = Vector2.zero;
                }
            }
            if (dragging)
            {
                dragDistance += look.ReadValue<Vector2>();
                // lock only once the mouse really moves, so a plain click still points at things
                if (dragDistance.magnitude > 6f && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    settle = SettleFrames;
                    awaitJump = true;
                }
                if (!lookHold.IsPressed())
                {
                    ClickedThisFrame = dragDistance.magnitude <= 6f;
                    EndDrag();
                }
            }
        }

        void EndDrag()
        {
            dragging = false;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                settle = SettleFrames;
                awaitJump = false;
            }
        }

        public void Teleport(Vector3 position, float newYaw)
        {
            if (vr)
            {
                vr.Teleport(position, newYaw);
                yaw = viewYaw = transform.eulerAngles.y;
                pitch = viewPitch = 0;
                return;
            }
            controller.enabled = false;
            transform.position = position;
            yaw = viewYaw = newYaw;
            pitch = viewPitch = 0;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            controller.enabled = true;
        }

        /// <summary>Smoothly turn body and head to look at a world point.</summary>
        public void FocusOn(Vector3 point, float seconds = 0.6f)
        {
            // never swing the view in a headset: at most a quick snap when the point is well off to the side
            if (vr)
            {
                vr.FocusOn(point);
                yaw = viewYaw = transform.eulerAngles.y;
                return;
            }
            if (focusRoutine != null) StopCoroutine(focusRoutine);
            focusRoutine = StartCoroutine(Focus(point, seconds));
        }

        IEnumerator Focus(Vector3 point, float seconds)
        {
            // aim from where the eyes will be once a sit-down or stand-up has finished
            var dir = point - (transform.position + Vector3.up * TargetEye);
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            float startYaw = yaw, startPitch = pitch;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                yaw = Mathf.LerpAngle(startYaw, targetYaw, k);
                pitch = Mathf.Lerp(startPitch, targetPitch, k);
                yield return null;
            }
            yaw = targetYaw;
            pitch = targetPitch;
            focusRoutine = null;
        }
    }
}
