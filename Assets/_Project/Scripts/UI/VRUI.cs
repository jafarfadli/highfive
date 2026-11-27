using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// The UI in a headset. Every canvas the game makes with <see cref="UIKit.CreateCanvas"/> becomes a panel the
    /// size of the desktop screen's layout (1920 × 1080 units) floating 1.1 m in front of the player, 1.3 m wide,
    /// a little under the eyes. The panels stay put in the world while the head looks around them, and swing round
    /// to follow once the head has turned well away (or when a scene starts or a conversation opens). They are drawn
    /// on their own layer after everything else (the renderer's "VR UI" pass), so a wall or a counter between the
    /// eyes and the panel never hides it. The right controller points at them; its trigger clicks. Scene changes
    /// fade through ink on a sheet held just in front of the eyes.
    /// </summary>
    public class VRUI : MonoBehaviour
    {
        /// <summary>The layer the panels live on: the main passes skip it, the "VR UI" pass draws it last.</summary>
        public const int Layer = 31;
        const float Distance = 1.1f, Width = 1.3f, Drop = 0.14f, FollowAngle = 34f, Turn = 9f;

        static VRUI instance;
        readonly List<Canvas> canvases = new();
        readonly HashSet<Canvas> headLocked = new();
        Camera head;
        float anchorYaw;
        bool placed;
        float turning;
        Canvas fader;
        Image faderImage;
        InputSystemUIInputModule module;
        InputActionAsset pointerActions;

        public static VRUI I
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject("[VR UI]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<VRUI>();
                    instance.Build();
                }
                return instance;
            }
        }

        /// <summary>Turns a new screen-space canvas into a panel (does nothing outside VR).</summary>
        public static void Register(Canvas canvas)
        {
            if (!Core.VRMode.Active || !canvas || !Application.isPlaying) return;
            I.Convert(canvas);
        }

        /// <summary>Swing the panels round to where the player now looks (a scene start, a conversation).</summary>
        public static void Recenter()
        {
            if (instance) instance.placed = false;
        }

        /// <summary>The head camera the panels face (the player's eyes, or the menu camera).</summary>
        public static Camera Head => instance ? instance.head : null;

        /// <summary>Where the right controller's ray meets the UI it points at, if it does.</summary>
        public static bool PointerHit(out Vector3 point)
        {
            point = default;
            if (!instance || !instance.module) return false;
            var device = InputSystem.GetDevice<XRController>(CommonUsages.RightHand);
            if (device == null) return false;
            var hit = instance.module.GetLastRaycastResult(device.deviceId);
            if (!hit.isValid || !hit.gameObject) return false;
            point = hit.worldPosition;
            return true;
        }

        /// <summary>True while the right controller points at a panel's button or card.</summary>
        public static bool PointerOverUI
        {
            get
            {
                if (!instance || !instance.module) return false;
                var device = InputSystem.GetDevice<XRController>(CommonUsages.RightHand);
                return device != null && instance.module.IsPointerOverGameObject(device.deviceId);
            }
        }

        /// <summary>
        /// The pointer's laser and the dot where it lands, in the controller's glow. They live on the panels' layer,
        /// so the VR UI pass draws them last, over the panel they point at (on the world layers the panels, drawn
        /// after everything, hid the laser exactly where it met them).
        /// </summary>
        public static LineRenderer MakeLaser(Transform parent, out Transform dot)
        {
            var go = new GameObject("Laser");
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            var laser = go.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.widthMultiplier = 0.006f;
            laser.numCapVertices = 2;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
            laser.sortingOrder = 31000;
            laser.material = new Material(Shader.Find("Sprites/Default"));
            var glow = Theme.Get(Role.Glow);
            laser.startColor = new Color(glow.r, glow.g, glow.b, 0.95f);
            laser.endColor = new Color(glow.r, glow.g, glow.b, 0.55f);
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            d.name = "Dot";
            d.layer = Layer;
            d.transform.SetParent(go.transform, false);
            d.transform.localScale = Vector3.one * 0.018f;
            Destroy(d.GetComponent<Collider>());
            var r = d.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 31001;
            r.material = laser.material;
            r.material.color = glow;
            dot = d.transform;
            return laser;
        }

        /// <summary>Editor recordings in a simulated headset: where the right controller points on the menus.</summary>
        public static Ray? SimulatedPointer;
        public static Vector3? SimulatedPointerEnd;

        LineRenderer menuLaser;
        Transform menuDot;

        /// <summary>
        /// The pointer on the menus and the results, where there is no player and so no rig with its own laser:
        /// from the right controller's aim to the panel it points at.
        /// </summary>
        void UpdateMenuLaser()
        {
            bool show = false;
            Vector3 from = default, end = default;
            // the menus (no rig), or in the street when the rig's own hand isn't tracked through the Input System
            if (head && (!Player.VRRig.Current || !Player.VRRig.Current.HandTracked))
            {
                if (Core.VRMode.Simulated)
                {
                    if (SimulatedPointer.HasValue)
                    {
                        var ray = SimulatedPointer.Value;
                        from = ray.origin;
                        end = SimulatedPointerEnd ?? ray.origin + ray.direction * 2f;
                        show = true;
                    }
                }
                else if (ReadRightController(out var o, out var r, out _, out _))
                {
                    from = o;
                    end = PointerHit(out var hit) ? hit : fbHit ?? o + r * Vector3.forward * 2.5f;
                    show = true;
                }
            }
            if (show && !menuLaser) menuLaser = MakeLaser(transform, out menuDot);
            if (!menuLaser) return;
            menuLaser.enabled = show;
            menuDot.gameObject.SetActive(show);
            if (!show) return;
            menuLaser.SetPosition(0, from);
            menuLaser.SetPosition(1, end);
            menuDot.position = end;
        }

        // ---------------------------------------------------------------- a second way to point and click
        // The controller is read twice: through the Input System (as the UI module does) and through UnityEngine.XR
        // (a separate path). This pointer raycasts the panels itself and sends the UI events straight to what it
        // hits, so the panels answer even when the UI module's tracked pointer doesn't. It only acts while the
        // module's own pointer isn't over the UI, so the two never both click. The trigger on nothing uses what the
        // pointer looks at (a door, a counter), and A held answers by voice, when the Input System has no controller.

        static readonly List<UnityEngine.XR.InputDevice> xrDevices = new();
        static readonly List<UnityEngine.XR.InputFeatureUsage> xrUsages = new();
        static readonly List<Canvas> nestedCanvases = new();

        /// <summary>A held: pressed or released this frame, from the second path (read by the conversation).</summary>
        public static bool FallbackTalkPressed { get; private set; }
        public static bool FallbackTalkReleased { get; private set; }

        /// <summary>The trigger pulled on nothing (no panel), from the second path: the interactor uses its target.</summary>
        public static bool FallbackInteractPressed { get; private set; }

        /// <summary>Editor tests: the simulated controller's trigger.</summary>
        public static bool SimulatedTrigger;

        PointerEventData fbData;
        GameObject fbHover, fbDown, fbPress;
        bool fbTrigger, fbTalk;
        Vector3? fbHit;
        string fbSource;
        float nextLog;

        static UnityEngine.InputSystem.XR.XRController RightIS =>
            InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRController>(UnityEngine.InputSystem.CommonUsages.RightHand);

        /// <summary>The right controller's aim (world space), trigger and A, from the Input System or else UnityEngine.XR.</summary>
        bool ReadRightController(out Vector3 origin, out Quaternion rotation, out bool trigger, out bool primary)
        {
            origin = default;
            rotation = Quaternion.identity;
            trigger = primary = false;
            fbSource = null;
            if (Core.VRMode.Simulated)
            {
                if (!SimulatedPointer.HasValue) return false;
                origin = SimulatedPointer.Value.origin;
                rotation = Quaternion.LookRotation(SimulatedPointer.Value.direction);
                trigger = SimulatedTrigger;
                fbSource = "simulated";
                return true;
            }
            var space = head ? head.transform.parent : null;
            var dev = RightIS;
            if (dev != null && dev.enabled)
            {
                var p = dev.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("pointerPosition") ?? dev.devicePosition;
                var q = dev.TryGetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("pointerRotation") ?? dev.deviceRotation;
                if (p != null && q != null && (p.ReadValue() != Vector3.zero || q.ReadValue() != Quaternion.identity))
                {
                    origin = space ? space.TransformPoint(p.ReadValue()) : p.ReadValue();
                    rotation = space ? space.rotation * q.ReadValue() : q.ReadValue();
                    var t = dev.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("{TriggerButton}");
                    var a = dev.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("{PrimaryButton}");
                    trigger = t != null && t.isPressed;
                    primary = a != null && a.isPressed;
                    fbSource = "InputSystem " + dev.layout;
                    return true;
                }
            }
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Right | UnityEngine.XR.InputDeviceCharacteristics.Controller, xrDevices);
            foreach (var d in xrDevices)
            {
                if (!d.isValid) continue;
                // the aim pose if the device names one ("pointer…" or "aim…"), else the grip pose tipped down
                Vector3 pos = default;
                Quaternion rot = Quaternion.identity;
                bool aimPos = false, aimRot = false;
                if (d.TryGetFeatureUsages(xrUsages))
                    foreach (var u in xrUsages)
                    {
                        var n = u.name.ToLowerInvariant();
                        if (!(n.Contains("pointer") || n.Contains("aim"))) continue;
                        if (!aimPos && u.type == typeof(Vector3) && d.TryGetFeatureValue(u.As<Vector3>(), out pos)) aimPos = true;
                        if (!aimRot && u.type == typeof(Quaternion) && d.TryGetFeatureValue(u.As<Quaternion>(), out rot)) aimRot = true;
                    }
                if (!aimPos || !aimRot)
                {
                    if (!d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos) ||
                        !d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot)) continue;
                    rot *= Quaternion.Euler(35f, 0, 0);
                }
                d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out trigger);
                if (!trigger && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float tv)) trigger = tv > 0.6f;
                d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primary);
                origin = space ? space.TransformPoint(pos) : pos;
                rotation = space ? space.rotation * rot : rot;
                fbSource = "XR " + d.name;
                return true;
            }
            return false;
        }

        /// <summary>The topmost raycastable graphic of a panel under <paramref name="ray"/>.</summary>
        Graphic RaycastPanels(Ray ray, out Vector3 point)
        {
            point = default;
            Graphic best = null;
            int bestOrder = int.MinValue, bestDepth = int.MinValue;
            foreach (var c in canvases)
            {
                if (!c || !c.isActiveAndEnabled || headLocked.Contains(c)) continue;
                var plane = new Plane(-c.transform.forward, c.transform.position);
                if (!plane.Raycast(ray, out float dist) || dist <= 0f) continue;
                var hit = ray.GetPoint(dist);
                var screen = head ? (Vector2)head.WorldToScreenPoint(hit) : Vector2.zero;
                c.GetComponentsInChildren(false, nestedCanvases);
                foreach (var nc in nestedCanvases)
                {
                    var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(nc);
                    for (int i = 0; i < graphics.Count; i++)
                    {
                        var g = graphics[i];
                        if (!g || !g.raycastTarget || !g.isActiveAndEnabled || g.depth < 0 || g.canvasRenderer.cull) continue;
                        var rt = g.rectTransform;
                        var local = rt.InverseTransformPoint(hit);
                        if (!rt.rect.Contains(new Vector2(local.x, local.y))) continue;
                        if (!g.Raycast(screen, head)) continue;
                        int order = nc.sortingOrder;
                        if (order < bestOrder || (order == bestOrder && g.depth <= bestDepth)) continue;
                        best = g;
                        bestOrder = order;
                        bestDepth = g.depth;
                        point = hit;
                    }
                }
            }
            return best;
        }

        void UpdateFallbackPointer()
        {
            FallbackTalkPressed = FallbackTalkReleased = FallbackInteractPressed = false;
            fbHit = null;
            bool read = ReadRightController(out var origin, out var rotation, out bool trigger, out bool primary);
            // A for answering by voice, only when the Input System has no controller (else the action has it)
            bool talk = read && primary && RightIS == null;
            FallbackTalkPressed = talk && !fbTalk;
            FallbackTalkReleased = !talk && fbTalk;
            fbTalk = talk;
            var es = EventSystem.current;
            if (!read || !es || (!Core.VRMode.Simulated && PointerOverUI))
            {
                ReleaseFallback();
                fbTrigger = trigger;
                return;
            }
            var ray = new Ray(origin, rotation * Vector3.forward);
            var g = RaycastPanels(ray, out var point);
            var target = g ? g.gameObject : null;
            if (target) fbHit = point;
            fbData ??= new PointerEventData(es);
            fbData.button = PointerEventData.InputButton.Left;
            fbData.position = head ? (Vector2)head.WorldToScreenPoint(point) : Vector2.zero;
            fbData.pointerCurrentRaycast = new RaycastResult { gameObject = target, worldPosition = point, module = null };

            var enter = target ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target) : null;
            if (enter != fbHover)
            {
                if (fbHover) ExecuteEvents.Execute(fbHover, fbData, ExecuteEvents.pointerExitHandler);
                fbHover = enter;
                if (fbHover) ExecuteEvents.Execute(fbHover, fbData, ExecuteEvents.pointerEnterHandler);
            }
            if (trigger && !fbTrigger)
            {
                if (target)
                {
                    fbDown = ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
                    fbPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                    if (fbDown) ExecuteEvents.Execute(fbDown, fbData, ExecuteEvents.pointerDownHandler);
                }
                else if (RightIS == null && Player.VRRig.Current)
                    // the trigger on nothing: use what the pointer looks at, as the Interact action would
                    FallbackInteractPressed = true;
            }
            else if (!trigger && fbTrigger)
            {
                if (fbDown) ExecuteEvents.Execute(fbDown, fbData, ExecuteEvents.pointerUpHandler);
                var click = target ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) : null;
                if (click && click == fbPress) ExecuteEvents.Execute(click, fbData, ExecuteEvents.pointerClickHandler);
                fbDown = fbPress = null;
            }
            fbTrigger = trigger;
        }

        void ReleaseFallback()
        {
            if (fbHover && fbData != null) ExecuteEvents.Execute(fbHover, fbData, ExecuteEvents.pointerExitHandler);
            fbHover = fbDown = fbPress = null;
        }

        /// <summary>Every 5 s in the Player.log: which controllers the game sees, by which path, and the pointer.</summary>
        void LogInput()
        {
            if (Core.VRMode.Simulated || Time.unscaledTime < nextLog) return;
            nextLog = Time.unscaledTime + 5f;
            var sb = new System.Text.StringBuilder("[VR input] ");
            foreach (var d in InputSystem.devices)
                if (d is UnityEngine.InputSystem.XR.XRController || d is UnityEngine.InputSystem.XR.XRHMD)
                    sb.Append($"IS {d.layout} [{string.Join(",", d.usages)}] enabled={d.enabled}; ");
            UnityEngine.XR.InputDevices.GetDevices(xrDevices);
            foreach (var d in xrDevices) sb.Append($"XR '{d.name}' {d.characteristics} valid={d.isValid}; ");
            sb.Append($"pointer={fbSource ?? "none"} overUI={PointerOverUI} panelHit={(fbHit.HasValue ? "yes" : "no")} trigger={fbTrigger} focus={Application.isFocused}");
            Debug.Log(sb.ToString());
        }

        /// <summary>Ink comes up over the eyes (cover) or clears (uncover) over <paramref name="seconds"/>.</summary>
        public static async Task Fade(bool cover, float seconds)
        {
            var ui = I;
            if (!ui.faderImage) return;
            float from = ui.faderImage.color.a, to = cover ? 1f : 0f;
            ui.fader.enabled = true;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (!ui || !ui.faderImage) return;
                var c = ui.faderImage.color;
                c.a = Mathf.Lerp(from, to, t / seconds);
                ui.faderImage.color = c;
                await Awaitable.NextFrameAsync();
            }
            if (!ui || !ui.faderImage) return;
            var end = ui.faderImage.color;
            end.a = to;
            ui.faderImage.color = end;
            ui.fader.enabled = cover;
        }

        void Build()
        {
            // the fade sheet: held 0.2 m in front of the eyes, wide enough to fill the view
            var go = new GameObject("Fader", typeof(RectTransform));
            go.layer = Layer;
            DontDestroyOnLoad(go);
            fader = go.AddComponent<Canvas>();
            fader.renderMode = RenderMode.WorldSpace;
            fader.sortingOrder = 32000;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1000, 1000);
            rt.localScale = Vector3.one * 0.0012f;
            var img = new GameObject("Ink", typeof(RectTransform)).AddComponent<Image>();
            img.gameObject.layer = Layer;
            img.transform.SetParent(go.transform, false);
            ((RectTransform)img.transform).Stretch();
            img.color = Theme.Get(Role.Ink, 0f);
            img.raycastTarget = false;
            faderImage = img;
            fader.enabled = false;
            headLocked.Add(fader);
        }

        void Convert(Canvas canvas)
        {
            if (canvases.Contains(canvas)) return;
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvas.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1920, 1080);
            rt.localScale = Vector3.one * (Width / 1920f);
            canvas.gameObject.layer = Layer;
            foreach (var nested in canvas.GetComponentsInChildren<Canvas>(true)) nested.gameObject.layer = Layer;
            // the GraphicRaycaster stays: with the head as the event camera, a mouse click on the laptop's mirror
            // window still reaches the panels (a way out if the controllers fail)
            var tr = canvas.GetComponent<TrackedDeviceRaycaster>();
            if (!tr) tr = canvas.gameObject.AddComponent<TrackedDeviceRaycaster>();
            tr.checkFor3DOcclusion = false;
            tr.checkFor2DOcclusion = false;
            tr.ignoreReversedGraphics = true;
            canvases.Add(canvas);
            if (head) canvas.worldCamera = head;
            Place(canvas);
        }

        void LateUpdate()
        {
            canvases.RemoveAll(c => !c);
            if (!head || !head.isActiveAndEnabled)
            {
                head = Camera.main;
                placed = false;
                if (head)
                {
                    // the menus' and the results' own cameras become the headset's eyes too
                    Player.VRRig.TrackMenuCamera(head);
                    foreach (var c in canvases) c.worldCamera = head;
                }
            }
            if (!head) return;
            EnsureModule();

            var fwd = head.transform.forward;
            fwd.y = 0;
            float headYaw = fwd.sqrMagnitude > 0.0001f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : anchorYaw;
            if (!placed)
            {
                anchorYaw = headYaw;
                placed = true;
                turning = 0;
            }
            // look around the panels freely; once the head has stayed well away, they swing round to follow
            float off = Mathf.DeltaAngle(anchorYaw, headYaw);
            if (Mathf.Abs(off) > FollowAngle) turning = 1f;
            if (turning > 0)
            {
                anchorYaw = Mathf.MoveTowardsAngle(anchorYaw, headYaw, Mathf.Max(Mathf.Abs(off), 20f) * Turn * Time.unscaledDeltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(anchorYaw, headYaw)) < 2f) turning = 0;
            }
            foreach (var c in canvases) Place(c);
            UpdateMenuLaser();
            UpdateFallbackPointer();
            LogInput();

            if (fader)
            {
                var ft = fader.transform;
                ft.SetPositionAndRotation(head.transform.position + head.transform.forward * 0.2f, head.transform.rotation);
            }
        }

        void Place(Canvas canvas)
        {
            if (!head || headLocked.Contains(canvas)) return;
            var rot = Quaternion.Euler(0, anchorYaw, 0);
            var pos = head.transform.position + rot * Vector3.forward * Distance + Vector3.down * Drop;
            canvas.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>The UI input module points with the right controller's aim and clicks with its trigger.</summary>
        void EnsureModule()
        {
            var es = EventSystem.current;
            var m = es ? es.GetComponent<InputSystemUIInputModule>() : null;
            if (!m) return;
            if (m == module)
            {
                var origin = head ? head.transform.parent : null;
                if (module.xrTrackingOrigin != origin) module.xrTrackingOrigin = origin;
                return;
            }
            module = m;
            if (!pointerActions)
            {
                pointerActions = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = pointerActions.AddActionMap("VR Pointer");
                map.AddAction("Position", InputActionType.PassThrough, "<XRController>{RightHand}/pointerPosition", expectedControlLayout: "Vector3");
                map.AddAction("Orientation", InputActionType.PassThrough, "<XRController>{RightHand}/pointerRotation", expectedControlLayout: "Quaternion");
                var click = map.AddAction("Click", InputActionType.PassThrough, "<XRController>{RightHand}/{TriggerButton}", expectedControlLayout: "Button");
                click.AddBinding("<Mouse>/leftButton");
                pointerActions.Enable();
            }
            var vr = pointerActions.FindActionMap("VR Pointer");
            module.trackedDevicePosition = InputActionReference.Create(vr.FindAction("Position"));
            module.trackedDeviceOrientation = InputActionReference.Create(vr.FindAction("Orientation"));
            module.leftClick = InputActionReference.Create(vr.FindAction("Click"));
            module.xrTrackingOrigin = head ? head.transform.parent : null;
        }
    }
}
