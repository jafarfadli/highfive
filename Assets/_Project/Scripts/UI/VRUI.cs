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
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr) gr.enabled = false;
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
                map.AddAction("Click", InputActionType.PassThrough, "<XRController>{RightHand}/{TriggerButton}", expectedControlLayout: "Button");
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
