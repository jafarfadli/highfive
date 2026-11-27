using SeoulFirstWeek.UI;
using SeoulFirstWeek.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;

namespace SeoulFirstWeek.Player
{
    /// <summary>
    /// The player in a headset (added by <see cref="FirstPersonController"/> when <see cref="Core.VRMode.Active"/>).
    /// The camera follows the head; the camera pivot is the tracking space, raised so the eyes sit at the game's
    /// eye height (standing or seated) whatever the real posture, and shifted so the body's capsule stays under
    /// the head when the player leans or steps. The left stick walks where the head looks, the right stick snap-turns
    /// 45° round the head (no smooth turning, which makes people sick), and the right controller points: its ray
    /// ends on the panel or the thing it points at, and its trigger clicks or uses. A = hold to answer by voice,
    /// B = hear again, ☰ (left) = pause, left stick click = walk faster.
    /// </summary>
    public class VRRig : MonoBehaviour
    {
        public static VRRig Current { get; private set; }

        const float SnapAngle = 45f;

        FirstPersonController fps;
        CharacterController controller;
        Transform pivot, head, rightAim, rightGrip, leftGrip;
        LineRenderer laser;
        Transform dot;
        InputAction turn, rightTracked, leftTracked;
        bool turnHeld, calibrated;
        float heightOffset;
        Vector3 room;   // the tracking space's shift under the capsule (x, z), in the player's frame

        public Transform Head => head;

        /// <summary>The right controller's aim (falls back to the eyes while it isn't tracked).</summary>
        public Ray PointerRay =>
            RightTracked ? new Ray(rightAim.position, rightAim.forward) : VRUI.ControllerRay ?? new Ray(head.position, head.forward);

        bool RightTracked => SimulatedHand || (rightTracked != null && rightTracked.IsPressed());

        /// <summary>
        /// Editor recordings in a simulated headset (<see cref="Core.VRMode.Simulated"/>): the recorder holds the
        /// right controller up and poses it itself, so the laser and the pointer work as with a tracked controller.
        /// <see cref="SimulatedLaserEnd"/> ends the laser on a point of a panel (the recorder knows what it points at).
        /// </summary>
        public static bool SimulatedHand;
        public static Vector3? SimulatedLaserEnd;

        /// <summary>True while the right controller is tracked through the Input System (its laser shows).</summary>
        public bool HandTracked => RightTracked;

        public Transform Pivot => pivot;
        public Transform RightAim => rightAim;
        public Transform RightGrip => rightGrip;
        public Transform LeftGrip => leftGrip;

        /// <summary>The head's heading as a rotation about the vertical (walking goes this way).</summary>
        public Quaternion HeadYaw
        {
            get
            {
                var f = head.forward;
                f.y = 0;
                return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f) : transform.rotation;
            }
        }

        public static VRRig Attach(FirstPersonController fps)
        {
            var rig = fps.gameObject.AddComponent<VRRig>();
            rig.Setup(fps);
            return rig;
        }

        void Setup(FirstPersonController owner)
        {
            fps = owner;
            controller = GetComponent<CharacterController>();
            pivot = fps.cameraPivot;
            head = fps.Camera.transform;
            TrackHead(fps.Camera);

            rightAim = Hand("RightAim", "RightHand", "pointer", false);
            rightGrip = Hand("RightGrip", "RightHand", "device", true);
            leftGrip = Hand("LeftGrip", "LeftHand", "device", true);
            rightTracked = new InputAction("Right Tracked", binding: "<XRController>{RightHand}/isTracked");
            leftTracked = new InputAction("Left Tracked", binding: "<XRController>{LeftHand}/isTracked");
            rightTracked.Enable();
            leftTracked.Enable();
            turn = new InputAction("Snap Turn", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", expectedControlType: "Vector2");
            turn.Enable();
            BuildLaser();
            AddBindings(fps.Map);
            Current = this;
        }

        /// <summary>
        /// Makes a camera follow the headset inside its parent (the tracking space). Used for the player's eyes
        /// and, through <see cref="TrackMenuCamera"/>, for the cameras of the menus and the results.
        /// </summary>
        public static void TrackHead(Camera cam)
        {
            var t = cam.transform;
            if (t.GetComponent<TrackedPoseDriver>()) return;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            var tpd = t.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(Pose("Head Position", "<XRHMD>/centerEyePosition", "Vector3"));
            tpd.rotationInput = new InputActionProperty(Pose("Head Rotation", "<XRHMD>/centerEyeRotation", "Quaternion"));
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            // a simulated headset has no head to follow: the recorder turns the camera itself
            if (Core.VRMode.Simulated) tpd.enabled = false;
            // near plane close enough for a hand held up to the face
            cam.nearClipPlane = 0.03f;
        }

        /// <summary>
        /// A menu's own camera in a headset: it stops drifting (a view that moves by itself makes people sick)
        /// and becomes the eyes of someone standing on the street where it was, at eye height even where the
        /// desktop camera hangs high over the street.
        /// </summary>
        public static void TrackMenuCamera(Camera cam)
        {
            if (cam.GetComponent<TrackedPoseDriver>() || cam.GetComponentInParent<FirstPersonController>()) return;
            var drift = cam.GetComponent<CameraDrift>();
            if (drift) drift.enabled = false;
            var at = cam.transform.position;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var ground, 40f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && at.y - ground.point.y > 2.4f)
                at.y = ground.point.y + 1.62f;
            var f = cam.transform.forward;
            f.y = 0;
            var space = new GameObject("VR Tracking Space").transform;
            space.SetParent(cam.transform.parent, false);
            space.SetPositionAndRotation(at, f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f) : Quaternion.identity);
            cam.transform.SetParent(space, false);
            TrackHead(cam);
        }

        static InputAction Pose(string name, string path, string type)
        {
            var a = new InputAction(name, InputActionType.Value, path, expectedControlType: type);
            a.Enable();
            return a;
        }

        /// <summary>A tracked hand under the tracking space, with a small inked controller on the grip ones.</summary>
        Transform Hand(string name, string side, string pose, bool model)
        {
            var t = new GameObject(name).transform;
            t.SetParent(pivot, false);
            var tpd = t.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(Pose($"{name} Position", $"<XRController>{{{side}}}/{pose}Position", "Vector3"));
            tpd.rotationInput = new InputActionProperty(Pose($"{name} Rotation", $"<XRController>{{{side}}}/{pose}Rotation", "Quaternion"));
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            if (Core.VRMode.Simulated) tpd.enabled = false;
            if (model)
            {
                var dark = new Color(0.12f, 0.13f, 0.16f);
                var grip = Prims.Make(PrimitiveType.Cylinder, t, "Grip", new Vector3(0, -0.02f, -0.035f), new Vector3(0.034f, 0.055f, 0.034f), Mats.Lit(dark));
                grip.transform.localRotation = Quaternion.Euler(-35f, 0, 0);
                var ring = Prims.Make(PrimitiveType.Cylinder, t, "Ring", new Vector3(0, 0.03f, 0.005f), new Vector3(0.075f, 0.006f, 0.075f), Mats.Lit(dark));
                ring.transform.localRotation = Quaternion.Euler(-20f, 0, 0);
                var face = Prims.Make(PrimitiveType.Cylinder, t, "Face", new Vector3(0, 0.012f, -0.012f), new Vector3(0.04f, 0.004f, 0.04f), Mats.Lit(Theme.Get(Role.Brush)));
                face.transform.localRotation = Quaternion.Euler(-10f, 0, 0);
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    var c = r.GetComponent<Collider>();
                    if (c) Destroy(c);
                }
            }
            return t;
        }

        void BuildLaser()
        {
            laser = VRUI.MakeLaser(transform, out dot);
        }

        /// <summary>Controller buttons for the game's own actions (added once to the shared actions asset).</summary>
        static void AddBindings(InputActionMap map)
        {
            void Add(string action, string path)
            {
                var a = map.FindAction(action);
                if (a == null) return;
                foreach (var b in a.bindings) if (b.path == path) return;
                a.AddBinding(path);
            }
            bool wasEnabled = map.enabled;
            if (wasEnabled) map.Disable();
            Add("Move", "<XRController>{LeftHand}/{Primary2DAxis}");
            Add("Sprint", "<XRController>{LeftHand}/{Primary2DAxisClick}");
            Add("Interact", "<XRController>{RightHand}/{TriggerButton}");
            Add("Pause", "<XRController>{LeftHand}/{MenuButton}");
            Add("Repeat", "<XRController>{RightHand}/{SecondaryButton}");
            Add("PushToTalk", "<XRController>{RightHand}/{PrimaryButton}");
            if (wasEnabled) map.Enable();
        }

        /// <summary>Per frame, from the controller: snap turns, keeps the eyes at the game's height.</summary>
        public void Tick(float eye)
        {
            // the eye height: measured once tracking has started in this scene, so real posture doesn't matter
            if (!calibrated && head.localPosition != Vector3.zero)
            {
                heightOffset = -head.localPosition.y;
                calibrated = true;
            }

            var stick = turn.ReadValue<Vector2>();
            if (!turnHeld && Mathf.Abs(stick.x) > 0.7f && fps.MovementEnabled)
            {
                turnHeld = true;
                TurnAroundHead(Mathf.Sign(stick.x) * SnapAngle);
            }
            else if (turnHeld && Mathf.Abs(stick.x) < 0.3f) turnHeld = false;

            pivot.localRotation = Quaternion.identity;
            pivot.localPosition = new Vector3(room.x, eye + heightOffset, room.z);
        }

        /// <summary>After the capsule has walked: move it under the head if the player leaned or stepped.</summary>
        public void FollowHead()
        {
            var local = pivot.localPosition + head.localPosition;
            local.y = 0;
            if (local.sqrMagnitude < 0.0004f) return;
            var before = transform.position;
            controller.Move(transform.TransformVector(local));
            var moved = transform.InverseTransformVector(transform.position - before);
            moved.y = 0;
            room -= moved;
            pivot.localPosition = new Vector3(room.x, pivot.localPosition.y, room.z);
        }

        void LateUpdate()
        {
            if (!laser) return;
            bool show = RightTracked && fps.enabled;
            laser.enabled = show;
            dot.gameObject.SetActive(show);
            if (!show) return;
            var ray = PointerRay;
            Vector3 end;
            if (SimulatedLaserEnd.HasValue) end = SimulatedLaserEnd.Value;
            else if (VRUI.PointerHit(out var ui)) end = ui;
            else if (PlayerInteractor.CurrentHit.HasValue) end = PlayerInteractor.CurrentHit.Value;
            else end = ray.origin + ray.direction * 3f;
            laser.SetPosition(0, ray.origin);
            laser.SetPosition(1, end);
            dot.position = end;
        }

        /// <summary>Turns the body about the head, so the eyes stay where they are.</summary>
        public void TurnAroundHead(float degrees)
        {
            var at = head.position;
            controller.enabled = false;
            transform.RotateAround(at, Vector3.up, degrees);
            controller.enabled = true;
        }

        /// <summary>Puts the head over <paramref name="position"/>, looking along <paramref name="yaw"/>.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            // the tracking space shifts so the head is right over the body's origin, then the body turns so
            // the head (not the body) faces the way asked
            room = new Vector3(-head.localPosition.x, 0, -head.localPosition.z);
            pivot.localPosition = new Vector3(room.x, pivot.localPosition.y, room.z);
            float headYaw = head.localEulerAngles.y;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw - headYaw, 0));
            controller.enabled = true;
        }

        /// <summary>Turn towards a point only when it is well off to the side (a quick snap, never a smooth swing).</summary>
        public void FocusOn(Vector3 point)
        {
            var to = point - head.position;
            to.y = 0;
            if (to.sqrMagnitude < 0.01f) return;
            var f = head.forward;
            f.y = 0;
            float delta = Vector3.SignedAngle(f, to, Vector3.up);
            if (Mathf.Abs(delta) > 50f) TurnAroundHead(delta);
        }

        void OnDestroy()
        {
            turn?.Dispose();
            rightTracked?.Dispose();
            leftTracked?.Dispose();
            if (Current == this) Current = null;
        }
    }
}
