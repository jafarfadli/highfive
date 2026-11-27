using System.Collections.Generic;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SeoulFirstWeek.Player
{
    /// <summary>
    /// Playing on a phone or tablet. A thumb stick comes to wherever the left thumb lands and walks; a drag anywhere
    /// else looks around, the way a mouse drag does; a quick tap on a door, a counter or a person uses it, and so
    /// does a tap on the prompt; the II key top right pauses. Buttons, answers and menus are ordinary UI, which takes
    /// a touch as a click. The controller and the interactor add the values read here to their own input each
    /// frame, so keyboard, mouse and headset play as before. The stick hides while a conversation, a menu or a
    /// cut-scene holds the player.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TouchControls : MonoBehaviour
    {
        /// <summary>Editor checks: the touch controls without a phone (also -touch on the command line, or SFW_TOUCH=1).</summary>
        public static bool Forced;

        public static bool Active => !VRMode.Active && (Application.isMobilePlatform || Forced || Requested);

        static bool? requested;
        static bool Requested => requested ??= System.Environment.GetEnvironmentVariable("SFW_TOUCH") == "1" ||
                                               System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-touch") >= 0;

        /// <summary>The thumb stick, -1..1 on each axis; zero while no thumb is on it.</summary>
        public static Vector2 Move { get; private set; }

        /// <summary>How far the looking finger moved this frame, in screen pixels.</summary>
        public static Vector2 LookDelta { get; private set; }

        /// <summary>Where a quick tap outside the UI landed this frame, if there was one.</summary>
        public static Vector2? Tap { get; private set; }

        /// <summary>True on the frame after the prompt was tapped.</summary>
        public static bool InteractPressed { get; private set; }

        /// <summary>Play-tests: hold the stick this far over (added to a thumb on it).</summary>
        public static Vector2 SimulatedMove;

        static bool interactQueued;

        /// <summary>The prompt was tapped: the interactor uses what it names on the next frame.</summary>
        public static void PressInteract() => interactQueued = true;

        // canvas units at the 1920 × 1080 reference
        const float StickRadius = 110f, KnobSize = 100f, Margin = 40f;
        // a thumb that lands in the left 45 % of the screen, below the quest card, takes the stick
        const float StickZone = 0.45f, StickTop = 0.72f;
        const float TapSeconds = 0.3f;

        Canvas canvas;
        FirstPersonController player;
        PauseMenu pauseMenu;
        RectTransform stick, knob, pauseKey;
        Vector2 stickHome;
        int stickFinger = -1, lookFinger = -1;
        Vector2 stickOrigin;
        float lookStart, lookTravel;
        static readonly List<RaycastResult> Hits = new();

        /// <summary>Adds the controls under everything else on the HUD (prompt, banners, conversation).</summary>
        public static TouchControls Create(Hud hud)
        {
            var root = UIKit.Rect("Touch", hud.Canvas.transform).Stretch();
            root.SetAsFirstSibling();
            var touch = root.gameObject.AddComponent<TouchControls>();
            touch.canvas = hud.Canvas;
            touch.Build(root);
            return touch;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // the screen stays on while the player reads a line or waits for their number
            if (Application.isMobilePlatform) Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void Build(RectTransform root)
        {
            stickHome = new Vector2(Margin + StickRadius + 40f, Margin + StickRadius + 30f);
            stick = UIKit.Rect("Stick", root);
            stick.Place(Vector2.zero, stickHome, new Vector2(StickRadius * 2f, StickRadius * 2f));
            stick.pivot = new Vector2(0.5f, 0.5f);
            stick.anchoredPosition = stickHome;
            var pad = UIKit.Dot(stick, Role.Night, 4.5f, "Pad");
            pad.rectTransform.Stretch();
            pad.raycastTarget = false;
            var ring = UIKit.Dot(stick, Role.Night, 3f, "Ring");
            ring.lineRole = Role.Paper;
            ring.rectTransform.Stretch(34, 34, 34, 34);
            ring.raycastTarget = false;
            knob = UIKit.Rect("Knob", stick);
            knob.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(KnobSize, KnobSize));
            knob.pivot = new Vector2(0.5f, 0.5f);
            var cap = UIKit.Dot(knob, Role.Paper, 4.5f, "Cap");
            cap.rectTransform.Stretch();
            cap.shadow = new Vector2(5, -6);
            cap.raycastTarget = false;

            // the pause key: big enough for a fingertip, above the toast column
            pauseKey = UIKit.Keycap(root, "II", 44);
            pauseKey.Place(new Vector2(1, 1), new Vector2(-Margin, -18), pauseKey.sizeDelta);
            pauseKey.GetComponentInChildren<InkShape>().raycastTarget = true;
            pauseKey.gameObject.AddComponent<Button>().onClick.AddListener(Pause);
            pauseKey.gameObject.AddComponent<InkHover>();
        }

        void OnEnable() => EnhancedTouchSupport.Enable();

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            Move = LookDelta = Vector2.zero;
            Tap = null;
            InteractPressed = interactQueued = false;
            stickFinger = lookFinger = -1;
        }

        void Pause()
        {
            if (!pauseMenu) pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (pauseMenu && !pauseMenu.IsOpen && Modal.OpenCount == 0) pauseMenu.Show();
        }

        void Update()
        {
            InteractPressed = interactQueued;
            interactQueued = false;
            Tap = null;
            if (!player) player = FindAnyObjectByType<FirstPersonController>();
            if (!pauseMenu) pauseMenu = FindAnyObjectByType<PauseMenu>();
            bool free = Modal.OpenCount == 0 && !(pauseMenu && pauseMenu.IsOpen);
            bool live = free && player && player.isActiveAndEnabled;
            bool canMove = live && player.MovementEnabled;
            bool canLook = live && player.LookEnabled;
            float scale = canvas ? canvas.scaleFactor : 1f;
            float tapSlop = Screen.dpi > 0 ? Screen.dpi * 0.15f : 24f;

            var look = Vector2.zero;
            Vector2? thumb = null;
            foreach (var t in ETouch.activeTouches)
            {
                int id = t.touchId;
                var pos = t.screenPosition;
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        // a touch that starts on a button, an answer or a card belongs to the UI
                        if (OverUi(pos)) break;
                        if (canMove && stickFinger < 0 && pos.x < Screen.width * StickZone && pos.y < Screen.height * StickTop)
                        {
                            stickFinger = id;
                            stickOrigin = KeepOnScreen(pos, scale);
                            thumb = pos;
                        }
                        else if (canLook && lookFinger < 0)
                        {
                            lookFinger = id;
                            lookStart = Time.unscaledTime;
                            lookTravel = 0f;
                        }
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (id == stickFinger) thumb = pos;
                        else if (id == lookFinger)
                        {
                            look += t.delta;
                            lookTravel += t.delta.magnitude;
                        }
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (id == lookFinger)
                        {
                            lookTravel += t.delta.magnitude;
                            // a quick touch that hardly moved is a tap: use what it landed on
                            if (t.phase == TouchPhase.Ended && Time.unscaledTime - lookStart < TapSeconds && lookTravel < tapSlop)
                                Tap = pos;
                            else look += t.delta;
                            lookFinger = -1;
                        }
                        if (id == stickFinger) stickFinger = -1;
                        break;
                }
            }
            // a finger lost without an Ended (the app went to the background) lets go
            if (stickFinger >= 0 && (!canMove || !IsDown(stickFinger))) stickFinger = -1;
            if (lookFinger >= 0 && (!canLook || !IsDown(lookFinger))) lookFinger = -1;

            var move = Vector2.zero;
            var centre = stickHome * scale;
            if (stickFinger >= 0)
            {
                centre = stickOrigin;
                if (thumb is Vector2 at)
                {
                    move = Vector2.ClampMagnitude((at - stickOrigin) / (StickRadius * scale), 1f);
                    lastThumb = move;
                }
                else move = lastThumb;
                // a small dead zone so a resting thumb doesn't creep
                if (move.magnitude < 0.12f) move = Vector2.zero;
            }
            else lastThumb = Vector2.zero;
            if (canMove) move = Vector2.ClampMagnitude(move + SimulatedMove, 1f);
            Move = canMove ? move : Vector2.zero;
            LookDelta = canLook ? look : Vector2.zero;

            if (stick.gameObject.activeSelf != canMove) stick.gameObject.SetActive(canMove);
            if (canMove)
            {
                stick.anchoredPosition = centre / scale;
                knob.anchoredPosition = move * (StickRadius - KnobSize * 0.25f);
            }
            if (pauseKey.gameObject.activeSelf != free) pauseKey.gameObject.SetActive(free);
        }

        Vector2 lastThumb;

        static bool IsDown(int id)
        {
            foreach (var t in ETouch.activeTouches)
                if (t.touchId == id && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                    return true;
            return false;
        }

        /// <summary>Where the stick's pad goes for a thumb landing here: wholly on the screen.</summary>
        Vector2 KeepOnScreen(Vector2 pos, float scale)
        {
            float r = (StickRadius + 16f) * scale;
            return new Vector2(Mathf.Clamp(pos.x, r, Screen.width - r), Mathf.Clamp(pos.y, r, Screen.height - r));
        }

        static bool OverUi(Vector2 pos)
        {
            var es = EventSystem.current;
            if (!es) return false;
            Hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, Hits);
            return Hits.Count > 0;
        }
    }
}
