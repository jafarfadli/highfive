using System.Collections.Generic;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Furniture and fittings of Korean service interiors (clinic, public office, bank), built from
    /// primitives at real-world sizes. Every builder places the object with its origin on the floor and
    /// +Z as its front. Seat heights are shared with the people (see HumanRig.seatHeight).
    /// </summary>
    public static class InteriorKit
    {
        /// <summary>Top of the seat of waiting-room chairs and customer chairs.</summary>
        public const float ChairSeat = 0.44f;
        /// <summary>Top of the seat of office swivel chairs.</summary>
        public const float OfficeSeat = 0.47f;
        /// <summary>Top of desks and low counters.</summary>
        public const float DeskTop = 0.74f;

        static readonly Color Chrome = C("#B9BEC4");
        static readonly Color Black = C("#1E2024");

        static GameObject Part(Transform p, string name, PrimitiveType type, Vector3 pos, Vector3 size, Material m, bool collider = false, Quaternion? rot = null)
        {
            var go = Prims.Make(type, p, name, pos, size, m, collider, rot);
            return go;
        }

        /// <summary>Waiting-room chair: moulded shell on a steel frame (gang seating adds the beam).</summary>
        public static Transform WaitingChair(Transform parent, Vector3 pos, float yaw, Material shell, bool legs = true)
        {
            var g = Group("Chair", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var steel = Mats.Lit(Chrome, 0.6f, 0.8f);
            Part(g, "Seat", PrimitiveType.Cube, new Vector3(0, ChairSeat - 0.03f, 0.03f), new Vector3(0.47f, 0.06f, 0.44f), shell, true);
            Part(g, "SeatLip", PrimitiveType.Cube, new Vector3(0, ChairSeat - 0.045f, 0.245f), new Vector3(0.47f, 0.05f, 0.04f), shell);
            var back = Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 0.72f, -0.2f), new Vector3(0.46f, 0.5f, 0.05f), shell);
            back.transform.localRotation = Quaternion.Euler(-10, 0, 0);
            Part(g, "Bracket", PrimitiveType.Cube, new Vector3(0, ChairSeat - 0.08f, -0.08f), new Vector3(0.06f, 0.04f, 0.3f), steel);
            Part(g, "BackPost", PrimitiveType.Cube, new Vector3(0, 0.5f, -0.2f), new Vector3(0.05f, 0.2f, 0.03f), steel);
            if (legs) Part(g, "Leg", PrimitiveType.Cube, new Vector3(0, 0.2f, 0), new Vector3(0.05f, 0.4f, 0.05f), steel);
            return g;
        }

        /// <summary>Office swivel chair: five-star base, gas lift, seat and backrest.</summary>
        public static Transform OfficeChair(Transform parent, Vector3 pos, float yaw, Color fabric)
        {
            var g = Group("OfficeChair", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var dark = Mats.Lit(Black, 0.35f);
            var seat = Mats.Lit(fabric, 0.1f);
            for (int i = 0; i < 5; i++)
            {
                var leg = Part(g, "Star", PrimitiveType.Cube, new Vector3(0, 0.06f, 0), new Vector3(0.05f, 0.04f, 0.3f), dark);
                leg.transform.localRotation = Quaternion.Euler(0, i * 72f, 0);
                leg.transform.localPosition = leg.transform.localRotation * new Vector3(0, 0.06f, 0.15f);
                var wheel = Part(g, "Caster", PrimitiveType.Sphere, leg.transform.localRotation * new Vector3(0, 0.03f, 0.3f), Vector3.one * 0.055f, dark);
                wheel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Part(g, "Lift", PrimitiveType.Cylinder, new Vector3(0, 0.24f, 0), new Vector3(0.05f, 0.17f, 0.05f), Mats.Lit(Chrome, 0.6f, 0.8f));
            Part(g, "Seat", PrimitiveType.Cube, new Vector3(0, OfficeSeat - 0.04f, 0.02f), new Vector3(0.5f, 0.08f, 0.48f), seat);
            var back = Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 0.82f, -0.24f), new Vector3(0.46f, 0.55f, 0.07f), seat);
            back.transform.localRotation = Quaternion.Euler(-8, 0, 0);
            Part(g, "Spine", PrimitiveType.Cube, new Vector3(0, 0.55f, -0.22f), new Vector3(0.06f, 0.2f, 0.04f), dark);
            foreach (var x in new[] { -0.26f, 0.26f })
                Part(g, "Arm", PrimitiveType.Cube, new Vector3(x, 0.64f, 0.02f), new Vector3(0.04f, 0.04f, 0.3f), dark);
            return g;
        }

        /// <summary>Round backless stool (the patient's seat by the doctor's desk).</summary>
        public static Transform Stool(Transform parent, Vector3 pos, Color top)
        {
            var g = Group("Stool", parent);
            g.localPosition = pos;
            Part(g, "Base", PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(0.44f, 0.02f, 0.44f), Mats.Lit(Black, 0.35f));
            Part(g, "Post", PrimitiveType.Cylinder, new Vector3(0, 0.22f, 0), new Vector3(0.06f, 0.2f, 0.06f), Mats.Lit(Chrome, 0.6f, 0.8f));
            Part(g, "Seat", PrimitiveType.Cylinder, new Vector3(0, ChairSeat - 0.035f, 0), new Vector3(0.4f, 0.035f, 0.4f), Mats.Lit(top, 0.25f));
            return g;
        }

        /// <summary>
        /// Desk open underneath for a seated person on its -Z side (top, side panels, modesty panel on
        /// the +Z side). <paramref name="size"/> is width, height, depth.
        /// </summary>
        public static Transform Desk(Transform parent, Vector3 pos, float yaw, Vector3 size, Material top, Material body)
        {
            var g = Group("Desk", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Top", PrimitiveType.Cube, new Vector3(0, size.y - 0.015f, 0), new Vector3(size.x, 0.03f, size.z), top, true);
            foreach (var x in new[] { -1f, 1f })
                Part(g, "Side", PrimitiveType.Cube, new Vector3(x * (size.x / 2f - 0.015f), (size.y - 0.03f) / 2f, 0), new Vector3(0.03f, size.y - 0.03f, size.z - 0.02f), body, true);
            Part(g, "Modesty", PrimitiveType.Cube, new Vector3(0, size.y * 0.55f, size.z / 2f - 0.03f), new Vector3(size.x - 0.06f, size.y * 0.5f, 0.02f), body, true);
            return g;
        }

        /// <summary>Flat monitor on a stand, screen facing +Z.</summary>
        public static Transform Monitor(Transform parent, Vector3 pos, float yaw, Color screen)
        {
            var g = Group("Monitor", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var dark = Mats.Lit(Black, 0.45f);
            Part(g, "Foot", PrimitiveType.Cube, new Vector3(0, 0.008f, -0.05f), new Vector3(0.2f, 0.016f, 0.16f), dark);
            Part(g, "Neck", PrimitiveType.Cube, new Vector3(0, 0.15f, -0.08f), new Vector3(0.05f, 0.28f, 0.03f), dark);
            Part(g, "Bezel", PrimitiveType.Cube, new Vector3(0, 0.3f, -0.05f), new Vector3(0.56f, 0.34f, 0.03f), dark);
            var s = Part(g, "Screen", PrimitiveType.Cube, new Vector3(0, 0.305f, -0.034f), new Vector3(0.52f, 0.3f, 0.004f), Mats.Emissive(screen, 0.9f));
            s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        /// <summary>Keyboard with the mouse to its right (as seen by the typist facing -Z).</summary>
        public static void Keyboard(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("Keyboard", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Board", PrimitiveType.Cube, new Vector3(0, 0.012f, 0), new Vector3(0.44f, 0.02f, 0.14f), Mats.Lit(C("#2B2E33"), 0.3f));
            Part(g, "Keys", PrimitiveType.Cube, new Vector3(0, 0.023f, 0), new Vector3(0.41f, 0.004f, 0.11f), Mats.Lit(C("#DADDE0"), 0.2f));
            Part(g, "Mouse", PrimitiveType.Sphere, new Vector3(-0.32f, 0.015f, 0), new Vector3(0.06f, 0.03f, 0.1f), Mats.Lit(Black, 0.45f));
        }

        /// <summary>
        /// A desk workstation for someone sitting at <paramref name="seat"/> (facing -Z): the keyboard
        /// right in front of them and the monitor beside it, turned towards them, so it never hides
        /// their face from the person across the desk.
        /// </summary>
        public static void Workstation(Transform parent, Vector3 seat, float deskEdgeZ, float side, Color screen)
        {
            Keyboard(parent, new Vector3(seat.x, DeskTop, deskEdgeZ - 0.08f), 0);
            var monitor = new Vector3(seat.x + side * 0.55f, DeskTop, deskEdgeZ - 0.38f);
            var toSeat = seat - monitor;
            Monitor(parent, monitor, Mathf.Atan2(toSeat.x, toSeat.z) * Mathf.Rad2Deg, screen);
        }

        /// <summary>Card payment terminal (카드 단말기) on a counter, keypad facing +Z.</summary>
        public static void CardTerminal(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("CardTerminal", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Body", PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(0.1f, 0.06f, 0.18f), Mats.Lit(C("#2B2E33"), 0.4f));
            Part(g, "Screen", PrimitiveType.Cube, new Vector3(0, 0.062f, -0.04f), new Vector3(0.07f, 0.004f, 0.05f), Mats.Emissive(C("#7FD1FF"), 0.8f));
            Part(g, "Keys", PrimitiveType.Cube, new Vector3(0, 0.062f, 0.035f), new Vector3(0.07f, 0.004f, 0.07f), Mats.Lit(C("#DADDE0"), 0.2f));
        }

        /// <summary>Red fire extinguisher (소화기) on the floor with its wall sign.</summary>
        public static void Extinguisher(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("Extinguisher", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var red = Mats.Lit(C("#D12A2A"), 0.55f);
            Part(g, "Tank", PrimitiveType.Cylinder, new Vector3(0, 0.27f, 0), new Vector3(0.16f, 0.25f, 0.16f), red, true);
            Part(g, "Dome", PrimitiveType.Sphere, new Vector3(0, 0.52f, 0), new Vector3(0.16f, 0.08f, 0.16f), red);
            Part(g, "Valve", PrimitiveType.Cube, new Vector3(0, 0.6f, 0.02f), new Vector3(0.04f, 0.08f, 0.1f), Mats.Lit(Black, 0.4f));
            Part(g, "Hose", PrimitiveType.Cube, new Vector3(0.07f, 0.42f, 0.04f), new Vector3(0.02f, 0.3f, 0.02f), Mats.Lit(Black, 0.3f));
            Sign(g, "Sign", new Vector3(0, 1.25f, -0.08f), new Vector2(0.3f, 0.2f), C("#D12A2A"), "소화기", Color.white, 0, 40, 0.01f);
        }

        /// <summary>Hand sanitiser on a pedestal by the entrance.</summary>
        public static void Sanitizer(Transform parent, Vector3 pos)
        {
            var g = Group("Sanitizer", parent);
            g.localPosition = pos;
            Part(g, "Foot", PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.01f, 0.3f), Mats.Lit(Chrome, 0.5f, 0.7f));
            Part(g, "Pole", PrimitiveType.Cylinder, new Vector3(0, 0.5f, 0), new Vector3(0.04f, 0.5f, 0.04f), Mats.Lit(Chrome, 0.5f, 0.7f));
            Part(g, "Tray", PrimitiveType.Cube, new Vector3(0, 1.0f, 0), new Vector3(0.18f, 0.02f, 0.14f), Mats.Lit(C("#E6E6E1"), 0.3f));
            Part(g, "Bottle", PrimitiveType.Cylinder, new Vector3(0, 1.09f, 0), new Vector3(0.07f, 0.08f, 0.07f), Mats.Glass(new Color(0.75f, 0.9f, 1f, 0.5f)));
            Part(g, "Pump", PrimitiveType.Cube, new Vector3(0, 1.2f, 0.02f), new Vector3(0.03f, 0.04f, 0.06f), Mats.Lit(C("#F4F1EA"), 0.3f));
            Sign(g, "Label", new Vector3(0, 0.75f, 0.03f), new Vector2(0.16f, 0.1f), C("#1FA37A"), "손소독", Color.white, 0, 30, 0.004f);
        }

        /// <summary>Water dispenser with a paper-cup holder.</summary>
        public static void WaterDispenser(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("WaterDispenser", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Body", PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(0.34f, 1.1f, 0.36f), Mats.Lit(C("#EEF1F3"), 0.4f), true);
            Part(g, "Panel", PrimitiveType.Cube, new Vector3(0, 0.85f, 0.181f), new Vector3(0.26f, 0.2f, 0.005f), Mats.Lit(C("#C9D6E2"), 0.4f));
            Part(g, "TapHot", PrimitiveType.Cube, new Vector3(-0.06f, 0.82f, 0.2f), new Vector3(0.04f, 0.05f, 0.04f), Mats.Lit(C("#D12A2A"), 0.4f));
            Part(g, "TapCold", PrimitiveType.Cube, new Vector3(0.06f, 0.82f, 0.2f), new Vector3(0.04f, 0.05f, 0.04f), Mats.Lit(C("#2F6FD0"), 0.4f));
            Part(g, "DripTray", PrimitiveType.Cube, new Vector3(0, 0.62f, 0.2f), new Vector3(0.22f, 0.02f, 0.08f), Mats.Lit(C("#8C9298"), 0.4f));
            Part(g, "Cups", PrimitiveType.Cylinder, new Vector3(0.24f, 0.8f, 0.05f), new Vector3(0.07f, 0.14f, 0.07f), Mats.Lit(C("#F4F1EA"), 0.2f));
        }

        /// <summary>Automatic blood-pressure machine (자동혈압계) with its seat, as in every Korean clinic.</summary>
        public static void BloodPressure(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("BloodPressure", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = Mats.Lit(C("#E6E8EA"), 0.45f);
            Part(g, "Cabinet", PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(0.56f, 0.9f, 0.5f), body, true);
            Part(g, "Head", PrimitiveType.Cube, new Vector3(0, 1.08f, -0.02f), new Vector3(0.5f, 0.36f, 0.42f), body, true);
            var cuff = Part(g, "Cuff", PrimitiveType.Cylinder, new Vector3(0.12f, 1.02f, 0.2f), new Vector3(0.16f, 0.1f, 0.16f), Mats.Lit(C("#2B2E33"), 0.3f));
            cuff.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part(g, "Display", PrimitiveType.Cube, new Vector3(-0.12f, 1.16f, 0.192f), new Vector3(0.16f, 0.1f, 0.005f), Mats.Emissive(C("#8CFFB0"), 0.8f));
            Part(g, "Armrest", PrimitiveType.Cube, new Vector3(0.12f, 0.92f, 0.32f), new Vector3(0.14f, 0.05f, 0.3f), Mats.Lit(C("#2B2E33"), 0.3f));
            Sign(g, "Label", new Vector3(0, 1.36f, 0.02f), new Vector2(0.46f, 0.12f), C("#1FA37A"), "자동혈압계", Color.white, 0, 36, 0.01f);
            WaitingChair(g, new Vector3(0, 0, 0.75f), 180, Mats.Lit(C("#5DA38C"), 0.35f));
        }

        /// <summary>Magazine and leaflet rack against a wall.</summary>
        public static void LeafletRack(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var g = Group("LeafletRack", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 0.75f, -0.05f), new Vector3(0.6f, 1.5f, 0.03f), Mats.Lit(C("#D8CDB8"), 0.3f), true);
            var covers = new[] { "#E4405F", "#1FA37A", "#3D7BFF", "#F7B32B", "#FFFFFF", "#8E44AD" };
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 2; col++)
            {
                var cover = Part(g, "Leaflet", PrimitiveType.Cube, new Vector3(-0.14f + col * 0.28f, 0.55f + row * 0.28f, 0f), new Vector3(0.22f, 0.26f, 0.015f),
                    Mats.Lit(C(covers[rng.Next(covers.Length)]), 0.3f));
                cover.transform.localRotation = Quaternion.Euler(-12, 0, 0);
                Part(g, "Pocket", PrimitiveType.Cube, new Vector3(-0.14f + col * 0.28f, 0.45f + row * 0.28f, 0.02f), new Vector3(0.25f, 0.08f, 0.01f), Mats.Glass(new Color(0.9f, 0.95f, 1f, 0.3f)));
            }
        }

        /// <summary>ID photo booth (즉석 증명사진) with a half-drawn curtain.</summary>
        public static void PhotoBooth(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("PhotoBooth", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var shell = Mats.Lit(C("#F4F1EA"), 0.35f);
            Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 1.0f, -0.45f), new Vector3(1.1f, 2.0f, 0.1f), shell, true);
            foreach (var x in new[] { -0.52f, 0.52f })
                Part(g, "Side", PrimitiveType.Cube, new Vector3(x, 1.0f, 0), new Vector3(0.06f, 2.0f, 1.0f), shell, true);
            Part(g, "Roof", PrimitiveType.Cube, new Vector3(0, 2.02f, 0), new Vector3(1.1f, 0.06f, 1.0f), shell);
            Part(g, "Header", PrimitiveType.Cube, new Vector3(0, 1.9f, 0.48f), new Vector3(1.1f, 0.2f, 0.04f), Mats.Lit(C("#2F5FD0"), 0.35f));
            WorldText.Create(g, "Title", "즉석 증명사진", new Vector3(0, 1.9f, 0.505f), new Vector2(1f, 0.18f), 50, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            Part(g, "Curtain", PrimitiveType.Cube, new Vector3(-0.22f, 1.1f, 0.46f), new Vector3(0.55f, 1.5f, 0.02f), Mats.Lit(C("#3B4E7A"), 0.1f));
            Part(g, "Rail", PrimitiveType.Cube, new Vector3(0, 1.86f, 0.46f), new Vector3(1.0f, 0.02f, 0.02f), Mats.Lit(Chrome, 0.5f, 0.7f));
            Part(g, "Screen", PrimitiveType.Cube, new Vector3(0.3f, 1.35f, -0.39f), new Vector3(0.3f, 0.2f, 0.01f), Mats.Emissive(C("#9CC7FF"), 0.8f));
            Part(g, "Seat", PrimitiveType.Cylinder, new Vector3(0.2f, 0.45f, -0.1f), new Vector3(0.34f, 0.03f, 0.34f), Mats.Lit(Black, 0.3f));
            Part(g, "Slot", PrimitiveType.Cube, new Vector3(0.53f, 1.0f, 0.3f), new Vector3(0.01f, 0.06f, 0.16f), Mats.Lit(Black, 0.3f));
        }

        /// <summary>Unmanned certificate kiosk (무인민원발급기).</summary>
        public static void Kiosk(Transform parent, Vector3 pos, float yaw, string title)
        {
            var g = Group("Kiosk", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = Mats.Lit(C("#DDE1E6"), 0.45f);
            Part(g, "Base", PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.9f, 0.5f), body, true);
            var head = Part(g, "Head", PrimitiveType.Cube, new Vector3(0, 1.25f, -0.05f), new Vector3(0.6f, 0.7f, 0.4f), body, true);
            head.transform.localRotation = Quaternion.Euler(-12, 0, 0);
            var screen = Part(g, "Screen", PrimitiveType.Cube, new Vector3(0, 1.26f, 0.15f), new Vector3(0.46f, 0.36f, 0.01f), Mats.Emissive(C("#7FB7FF"), 0.9f));
            screen.transform.localRotation = Quaternion.Euler(-12, 0, 0);
            Part(g, "Slot", PrimitiveType.Cube, new Vector3(0, 0.78f, 0.252f), new Vector3(0.3f, 0.03f, 0.01f), Mats.Lit(Black, 0.3f));
            Part(g, "Band", PrimitiveType.Cube, new Vector3(0, 1.68f, -0.02f), new Vector3(0.6f, 0.14f, 0.36f), Mats.Lit(C("#2F5FD0"), 0.35f));
            WorldText.Create(g, "Title", title, new Vector3(0, 1.68f, 0.165f), new Vector2(0.56f, 0.12f), 40, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
        }

        /// <summary>Office copier (복사기) with its paper trays.</summary>
        public static void Copier(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("Copier", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = Mats.Lit(C("#E8E8E4"), 0.4f);
            Part(g, "Body", PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 1.0f, 0.6f), body, true);
            Part(g, "Lid", PrimitiveType.Cube, new Vector3(0, 1.04f, 0), new Vector3(0.6f, 0.08f, 0.55f), Mats.Lit(C("#3A3D42"), 0.4f));
            for (int i = 0; i < 3; i++) Part(g, "Tray", PrimitiveType.Cube, new Vector3(0, 0.2f + i * 0.2f, 0.301f), new Vector3(0.5f, 0.01f, 0.005f), Mats.Lit(C("#9AA0A6"), 0.4f));
            Part(g, "Panel", PrimitiveType.Cube, new Vector3(0.18f, 1.0f, 0.25f), new Vector3(0.2f, 0.04f, 0.1f), Mats.Emissive(C("#9CC7FF"), 0.6f));
            Sign(g, "Sign", new Vector3(0, 1.4f, -0.25f), new Vector2(0.44f, 0.16f), C("#2F5FD0"), "복사기 · 유료", Color.white, 0, 34, 0.01f);
        }

        /// <summary>Queue stanchion with a retractable belt towards +X.</summary>
        public static void Stanchions(Transform parent, Vector3 from, Vector3 to, int posts, Color belt)
        {
            var g = Group("Stanchions", parent);
            var steel = Mats.Lit(Chrome, 0.6f, 0.8f);
            for (int i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(from, to, posts == 1 ? 0 : i / (float)(posts - 1));
                Part(g, "Foot", PrimitiveType.Cylinder, p + new Vector3(0, 0.01f, 0), new Vector3(0.32f, 0.01f, 0.32f), steel);
                Part(g, "Post", PrimitiveType.Cylinder, p + new Vector3(0, 0.46f, 0), new Vector3(0.06f, 0.45f, 0.06f), steel, true);
                Part(g, "Top", PrimitiveType.Sphere, p + new Vector3(0, 0.92f, 0), Vector3.one * 0.08f, steel);
                if (i == 0) continue;
                var q = Vector3.Lerp(from, to, (i - 1) / (float)(posts - 1));
                var mid = (p + q) / 2f + Vector3.up * 0.86f;
                var b = Part(g, "Belt", PrimitiveType.Cube, mid, new Vector3(0.005f, 0.05f, (p - q).magnitude), Mats.Lit(belt, 0.3f));
                b.transform.localRotation = Quaternion.LookRotation(p - q);
            }
        }

        /// <summary>Stand-up desk for filling in slips (기록대) with slip holders and pens.</summary>
        public static void SlipDesk(Transform parent, Vector3 pos, float yaw, Material top, Material body)
        {
            var g = Group("SlipDesk", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Top", PrimitiveType.Cube, new Vector3(0, 1.0f, 0), new Vector3(1.4f, 0.04f, 0.6f), top, true);
            Part(g, "Base", PrimitiveType.Cube, new Vector3(0, 0.49f, 0), new Vector3(1.2f, 0.98f, 0.4f), body, true);
            var slips = new[] { "#FFFFFF", "#DFF1D2", "#FDE7C8", "#D6E6FF" };
            for (int i = 0; i < 4; i++)
            {
                float x = -0.5f + i * 0.33f;
                Part(g, "Holder", PrimitiveType.Cube, new Vector3(x, 1.08f, -0.22f), new Vector3(0.26f, 0.12f, 0.08f), Mats.Glass(new Color(0.9f, 0.95f, 1f, 0.35f)));
                Part(g, "Slips", PrimitiveType.Cube, new Vector3(x, 1.08f, -0.22f), new Vector3(0.22f, 0.1f, 0.03f), Mats.Lit(C(slips[i]), 0.2f));
                Part(g, "Pen", PrimitiveType.Cylinder, new Vector3(x + 0.08f, 1.03f, 0.08f), new Vector3(0.012f, 0.07f, 0.012f), Mats.Lit(C("#2B2E33"), 0.4f),
                    false, Quaternion.Euler(90, 20, 0));
            }
            Part(g, "Glass", PrimitiveType.Cube, new Vector3(0.45f, 1.03f, 0.1f), new Vector3(0.12f, 0.01f, 0.12f), Mats.Glass(new Color(0.85f, 0.95f, 1f, 0.25f)));
        }

        /// <summary>Ceiling cassette air conditioner (천장형 에어컨), flush with the ceiling at height h.</summary>
        public static void CeilingCassette(Transform parent, Vector3 center, float h)
        {
            var g = Group("CeilingAC", parent);
            g.localPosition = new Vector3(center.x, h, center.z);
            var white = Mats.Lit(C("#F2F2EF"), 0.35f);
            var grille = Mats.Lit(C("#C9CCCF"), 0.3f);
            var panel = Part(g, "Panel", PrimitiveType.Cube, new Vector3(0, -0.02f, 0), new Vector3(0.84f, 0.04f, 0.84f), white);
            panel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Part(g, "Intake", PrimitiveType.Cube, new Vector3(0, -0.041f, 0), new Vector3(0.44f, 0.002f, 0.44f), grille);
            for (int i = 0; i < 4; i++)
            {
                var slot = Part(g, "Vent", PrimitiveType.Cube, Quaternion.Euler(0, i * 90, 0) * new Vector3(0, -0.041f, 0.33f), new Vector3(0.6f, 0.002f, 0.07f), Mats.Lit(C("#8E9296"), 0.3f));
                slot.transform.localRotation = Quaternion.Euler(0, i * 90, 0);
            }
        }

        /// <summary>Green emergency exit sign (비상구) over a door, lit.</summary>
        public static void ExitSign(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("ExitSign", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var box = Part(g, "Box", PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.16f, 0.05f), Mats.Emissive(C("#1FA35A"), 1.6f));
            box.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            WorldText.Create(g, "Text", "비상구  EXIT", new Vector3(0, 0, 0.03f), new Vector2(0.4f, 0.14f), 40, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
        }

        /// <summary>Dome CCTV camera on the ceiling.</summary>
        public static void Cctv(Transform parent, Vector3 pos)
        {
            var g = Group("CCTV", parent);
            g.localPosition = pos;
            Part(g, "Base", PrimitiveType.Cylinder, new Vector3(0, -0.01f, 0), new Vector3(0.16f, 0.01f, 0.16f), Mats.Lit(C("#F2F2EF"), 0.4f));
            Part(g, "Dome", PrimitiveType.Sphere, new Vector3(0, -0.03f, 0), new Vector3(0.12f, 0.1f, 0.12f), Mats.Lit(C("#1E2024"), 0.85f));
        }

        /// <summary>A framed certificate (licence, specialist diploma) on a wall facing +Z.</summary>
        public static void Certificate(Transform parent, Vector3 pos, float yaw, string title, System.Random rng)
        {
            var g = Group("Certificate", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Frame", PrimitiveType.Cube, Vector3.zero, new Vector3(0.36f, 0.46f, 0.03f), Mats.Lit(C("#8A6647"), 0.4f));
            Part(g, "Paper", PrimitiveType.Cube, new Vector3(0, 0, 0.016f), new Vector3(0.3f, 0.4f, 0.004f), Mats.Lit(C("#FBF8EF"), 0.1f));
            WorldText.Create(g, "Title", title, new Vector3(0, 0.12f, 0.02f), new Vector2(0.26f, 0.07f), 30, C("#3A2E22"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            for (int i = 0; i < 5; i++)
                Part(g, "Line", PrimitiveType.Cube, new Vector3(0, 0.03f - i * 0.04f, 0.019f), new Vector3(0.16f + (float)rng.NextDouble() * 0.06f, 0.008f, 0.001f), Mats.Lit(C("#9C9082"), 0.1f));
            Part(g, "Seal", PrimitiveType.Cylinder, new Vector3(0.08f, -0.15f, 0.019f), new Vector3(0.05f, 0.001f, 0.05f), Mats.Lit(C("#C0392B"), 0.2f), false, Quaternion.Euler(90, 0, 0));
        }

        /// <summary>
        /// Wall-mounted TV, screen facing +Z, with a caption. With more than one page the screen cycles
        /// through them (and through <paramref name="colors"/>, if given), like every waiting-room TV.
        /// </summary>
        public static Transform WallTv(Transform parent, Vector3 pos, float yaw, Color screen, string caption, string[] pages = null, Color[] colors = null,
            float width = 1.3f, float offset = 0f)
        {
            var g = Group("WallTV", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            float h = width * 0.585f;
            Part(g, "Bezel", PrimitiveType.Cube, Vector3.zero, new Vector3(width, h, 0.05f), Mats.Lit(Black, 0.5f));
            var s = Part(g, "Screen", PrimitiveType.Cube, new Vector3(0, 0, 0.026f), new Vector3(width - 0.06f, h - 0.06f, 0.004f), Mats.Emissive(screen, 1f));
            s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var text = WorldText.Create(g, "Caption", caption, new Vector3(0, 0, 0.032f), new Vector2(width - 0.2f, h - 0.16f), 40, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            if (pages != null && pages.Length > 1)
            {
                var cycler = g.gameObject.AddComponent<ScreenCycler>();
                cycler.text = text;
                cycler.pages = pages;
                cycler.screen = s.GetComponent<Renderer>();
                cycler.colors = colors;
                cycler.offset = offset;
            }
            return g;
        }

        // ================================================================== waiting rooms, 2026-10 overhaul

        /// <summary>Analogue wall clock (front +Z) whose hands keep the player's local time.</summary>
        public static Transform Clock(Transform parent, Vector3 pos, float yaw, float diameter = 0.42f, string rim = "#2B2D31")
        {
            var g = Group("Clock", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var flat = Quaternion.Euler(90, 0, 0);
            Part(g, "Rim", PrimitiveType.Cylinder, new Vector3(0, 0, 0.02f), new Vector3(diameter * 1.1f, 0.02f, diameter * 1.1f), Mats.Lit(C(rim), 0.5f), false, flat);
            Part(g, "Face", PrimitiveType.Cylinder, new Vector3(0, 0, 0.045f), new Vector3(diameter, 0.012f, diameter), Mats.Lit(C("#FAFAF7"), 0.3f), false, flat);
            var ink = Mats.Lit(C("#1E1E22"), 0.3f);
            for (int i = 0; i < 12; i++)
            {
                var q = Quaternion.Euler(0, 0, i * 30f);
                bool major = i % 3 == 0;
                Part(g, "Tick", PrimitiveType.Cube, q * new Vector3(0, diameter * 0.41f, 0) + new Vector3(0, 0, 0.058f),
                    new Vector3(major ? 0.014f : 0.008f, diameter * (major ? 0.09f : 0.06f), 0.002f), ink, false, q);
            }
            Transform Hand(string name, float length, float width, float z, Material m)
            {
                var pivot = Group(name, g);
                pivot.localPosition = new Vector3(0, 0, z);
                var hand = Part(pivot, "Hand", PrimitiveType.Cube, new Vector3(0, length / 2f - length * 0.15f, 0), new Vector3(width, length, 0.003f), m);
                hand.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                return pivot;
            }
            var clock = g.gameObject.AddComponent<WallClock>();
            clock.hour = Hand("HourHand", diameter * 0.28f, 0.016f, 0.061f, ink);
            clock.minute = Hand("MinuteHand", diameter * 0.42f, 0.011f, 0.064f, ink);
            clock.second = Hand("SecondHand", diameter * 0.44f, 0.004f, 0.067f, Mats.Lit(C("#D12A2A"), 0.3f));
            Part(g, "Cap", PrimitiveType.Cylinder, new Vector3(0, 0, 0.069f), new Vector3(0.022f, 0.003f, 0.022f), ink, false, flat);
            return g;
        }

        /// <summary>
        /// Upholstered waiting sofa (front +Z) of <paramref name="seats"/> places with arms, its seat top at
        /// <see cref="ChairSeat"/>. Returns one anchor per place, where a seated person goes.
        /// </summary>
        public static List<Transform> Sofa(Transform parent, Vector3 pos, float yaw, int seats, Material fabric, Material arms, float pitch = 0.62f, bool tufted = false)
        {
            var g = Group("Sofa", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            float inner = seats * pitch, width = inner + 0.18f;
            var dark = Mats.Lit(Black, 0.35f);
            Part(g, "Base", PrimitiveType.Cube, new Vector3(0, 0.18f, 0.02f), new Vector3(inner, 0.24f, 0.66f), fabric, true);
            foreach (var x in new[] { -1f, 1f })
            {
                Part(g, "Arm", PrimitiveType.Cube, new Vector3(x * (inner / 2f + 0.045f), 0.33f, 0.0f), new Vector3(0.09f, 0.56f, 0.72f), arms, true);
                foreach (var z in new[] { -0.3f, 0.3f })
                    Part(g, "Foot", PrimitiveType.Cube, new Vector3(x * (inner / 2f + 0.03f), 0.025f, z), new Vector3(0.05f, 0.05f, 0.05f), dark);
            }
            var back = Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 0.67f, -0.27f), new Vector3(inner, 0.5f, 0.14f), fabric, true);
            back.transform.localRotation = Quaternion.Euler(-6, 0, 0);
            var anchors = new List<Transform>();
            for (int i = 0; i < seats; i++)
            {
                float x = -inner / 2f + pitch * (i + 0.5f);
                Part(g, "Cushion", PrimitiveType.Cube, new Vector3(x, ChairSeat - 0.07f, 0.05f), new Vector3(pitch - 0.02f, 0.14f, 0.56f), fabric);
                if (tufted)
                    for (int k = 0; k < 2; k++)
                        Part(g, "Button", PrimitiveType.Sphere, new Vector3(x + (k - 0.5f) * pitch * 0.4f, 0.74f, -0.192f), Vector3.one * 0.022f, dark, false, Quaternion.Euler(-6, 0, 0));
                var a = Group("SeatAnchor", g);
                a.localPosition = new Vector3(x, 0, -0.1f);
                anchors.Add(a);
            }
            return anchors;
        }

        /// <summary>
        /// The 번호표 kiosk: a white floor-standing touch screen (front +Z) that lists the services to pick
        /// from, a ticket printer under it, and a header band. Returns the slot where the ticket appears.
        /// </summary>
        public static Transform TicketKiosk(Transform parent, Vector3 pos, float yaw, Color accent, string header, string screenText, out Transform root)
        {
            var g = root = Group("TicketMachine", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = Mats.Lit(C("#F2F3F4"), 0.5f);
            Part(g, "Plinth", PrimitiveType.Cube, new Vector3(0, 0.05f, 0), new Vector3(0.56f, 0.1f, 0.44f), Mats.Lit(C("#3A3D42"), 0.3f));
            Part(g, "Body", PrimitiveType.Cube, new Vector3(0, 0.62f, 0), new Vector3(0.52f, 1.04f, 0.4f), body);
            var head = Part(g, "Head", PrimitiveType.Cube, new Vector3(0, 1.32f, 0.02f), new Vector3(0.56f, 0.5f, 0.36f), body);
            head.transform.localRotation = Quaternion.Euler(-14, 0, 0);
            var screen = Part(g, "Screen", PrimitiveType.Cube, new Vector3(0, 1.33f, 0.205f), new Vector3(0.46f, 0.34f, 0.006f), Mats.Emissive(C("#EAF4FF"), 0.9f));
            screen.transform.localRotation = Quaternion.Euler(-14, 0, 0);
            screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            WorldText.Create(g, "ScreenText", screenText, new Vector3(0, 1.335f, 0.212f), new Vector2(0.42f, 0.3f), 30, C("#1F2F55"), true, TextAnchor.MiddleCenter,
                Quaternion.Euler(14, 180, 0));
            Part(g, "Band", PrimitiveType.Cube, new Vector3(0, 1.66f, -0.02f), new Vector3(0.56f, 0.16f, 0.32f), Mats.Lit(accent, 0.4f));
            WorldText.Create(g, "Header", header, new Vector3(0, 1.66f, 0.142f), new Vector2(0.52f, 0.14f), 44, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            Part(g, "SlotFrame", PrimitiveType.Cube, new Vector3(0, 0.95f, 0.2f), new Vector3(0.22f, 0.06f, 0.02f), Mats.Lit(C("#2B2D31"), 0.4f));
            Part(g, "Arrow", PrimitiveType.Cube, new Vector3(0, 0.87f, 0.201f), new Vector3(0.05f, 0.05f, 0.004f), Mats.Lit(accent, 0.3f), false, Quaternion.Euler(0, 0, 45));
            var slot = Group("Slot", g);
            slot.localPosition = new Vector3(0, 0.95f, 0.21f);
            return slot;
        }

        /// <summary>A small red LED number display for one counter (front +Z): "0047" and the counter number.</summary>
        public static QueueBoard CounterDisplay(Transform parent, Vector3 pos, float yaw, int window, float width = 0.62f)
        {
            var g = Group($"CounterDisplay_{window}", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            float h = width * 0.3f;
            Part(g, "Case", PrimitiveType.Cube, Vector3.zero, new Vector3(width, h, 0.06f), Mats.Lit(C("#121317"), 0.5f));
            var face = Part(g, "Face", PrimitiveType.Cube, new Vector3(0, 0, 0.031f), new Vector3(width - 0.04f, h - 0.04f, 0.002f), Mats.Lit(C("#1B0E0E"), 0.7f));
            face.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var num = WorldText.Create(g, "Number", "0000", new Vector3(-width * 0.12f, 0, 0.034f), new Vector2(width * 0.66f, h * 0.86f), 60, C("#FF3B30"), true,
                TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(g, "Window", $"{window}", new Vector3(width * 0.36f, 0, 0.034f), new Vector2(width * 0.22f, h * 0.86f), 60, C("#FFB547"), true,
                TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            var board = g.gameObject.AddComponent<QueueBoard>();
            board.numberText = num;
            board.window = window;
            return board;
        }

        /// <summary>
        /// A door in a wall (front +Z) to a treatment room: leaf, frame, handle, a name plate beside it and,
        /// when <paramref name="lamp"/> is set, a lit "in use" box over it (진료중, 촬영중).
        /// </summary>
        public static Transform RoomDoor(Transform parent, Vector3 pos, float yaw, string label, string sub, Material leaf, Color accent, string lamp = null,
            Color? lampColor = null)
        {
            var g = Group("RoomDoor", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var frame = Mats.Lit(C("#E9E6E0"), 0.4f);
            Part(g, "Leaf", PrimitiveType.Cube, new Vector3(0, 1.05f, 0.02f), new Vector3(0.95f, 2.1f, 0.04f), leaf, true);
            Part(g, "Frame", PrimitiveType.Cube, new Vector3(0, 2.13f, 0.03f), new Vector3(1.09f, 0.07f, 0.06f), frame);
            foreach (var x in new[] { -0.51f, 0.51f })
                Part(g, "Jamb", PrimitiveType.Cube, new Vector3(x, 1.05f, 0.03f), new Vector3(0.07f, 2.1f, 0.06f), frame);
            Part(g, "Handle", PrimitiveType.Cube, new Vector3(0.36f, 1.0f, 0.06f), new Vector3(0.14f, 0.025f, 0.04f), Mats.Lit(Chrome, 0.6f, 0.8f));
            Part(g, "Kick", PrimitiveType.Cube, new Vector3(0, 0.1f, 0.042f), new Vector3(0.95f, 0.2f, 0.004f), Mats.Lit(Chrome, 0.5f, 0.7f));
            var plate = Group("Plate", g);
            plate.localPosition = new Vector3(0.82f, 1.55f, 0.012f);
            Part(plate, "Board", PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.3f, 0.02f), Mats.Lit(C("#FFFFFF"), 0.4f));
            Part(plate, "Band", PrimitiveType.Cube, new Vector3(0, 0.11f, 0.011f), new Vector3(0.42f, 0.08f, 0.002f), Mats.Lit(accent, 0.3f));
            WorldText.Create(plate, "Label", label, new Vector3(0, -0.02f, 0.013f), new Vector2(0.38f, 0.14f), 44, C("#1E2A28"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            if (!string.IsNullOrEmpty(sub))
                WorldText.Create(plate, "Sub", sub, new Vector3(0, -0.11f, 0.013f), new Vector2(0.38f, 0.06f), 26, C("#5A6664"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            if (!string.IsNullOrEmpty(lamp))
            {
                var col = lampColor ?? C("#E5484D");
                var box = Part(g, "Lamp", PrimitiveType.Cube, new Vector3(0, 2.3f, 0.04f), new Vector3(0.46f, 0.15f, 0.06f), Mats.Emissive(col, 1.4f));
                box.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                WorldText.Create(g, "LampText", lamp, new Vector3(0, 2.3f, 0.072f), new Vector2(0.42f, 0.13f), 44, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            }
            return g;
        }

        /// <summary>Water purifier (정수기) on its cabinet with a paper-cup tube and a small bin, front +Z.</summary>
        public static void WaterPurifier(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("WaterPurifier", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var white = Mats.Lit(C("#F4F5F6"), 0.6f);
            Part(g, "Cabinet", PrimitiveType.Cube, new Vector3(0, 0.38f, 0), new Vector3(0.5f, 0.76f, 0.42f), Mats.Lit(C("#D9D4CA"), 0.35f), true);
            Part(g, "Unit", PrimitiveType.Cube, new Vector3(0, 1.03f, -0.02f), new Vector3(0.26f, 0.54f, 0.36f), white, true);
            Part(g, "Panel", PrimitiveType.Cube, new Vector3(0, 1.2f, 0.161f), new Vector3(0.2f, 0.12f, 0.004f), Mats.Lit(C("#C7CCD2"), 0.7f, 0.6f));
            foreach (var (x, c) in new[] { (-0.04f, "#2F8FE0"), (0.04f, "#E5484D") })
                Part(g, "Button", PrimitiveType.Cube, new Vector3(x, 1.19f, 0.165f), new Vector3(0.03f, 0.03f, 0.006f), Mats.Emissive(C(c), 0.8f));
            Part(g, "Spout", PrimitiveType.Cube, new Vector3(0, 1.08f, 0.17f), new Vector3(0.04f, 0.03f, 0.05f), Mats.Lit(Chrome, 0.6f, 0.8f));
            Part(g, "Tray", PrimitiveType.Cube, new Vector3(0, 0.79f, 0.12f), new Vector3(0.2f, 0.02f, 0.1f), Mats.Lit(C("#8C9298"), 0.4f));
            Part(g, "CupTube", PrimitiveType.Cylinder, new Vector3(0.2f, 1.02f, 0.05f), new Vector3(0.075f, 0.16f, 0.075f), Mats.Glass(new Color(0.9f, 0.95f, 1f, 0.35f)));
            Part(g, "Cups", PrimitiveType.Cylinder, new Vector3(0.2f, 0.98f, 0.05f), new Vector3(0.065f, 0.12f, 0.065f), Mats.Lit(C("#F4F1EA"), 0.2f));
            Part(g, "Bin", PrimitiveType.Cylinder, new Vector3(0.38f, 0.17f, 0.05f), new Vector3(0.22f, 0.17f, 0.22f), Mats.Lit(C("#B9C2CA"), 0.4f));
            Sign(g, "Label", new Vector3(0, 0.6f, 0.215f), new Vector2(0.3f, 0.1f), C("#2F8FE0"), "정수기", Color.white, 0, 34, 0.01f);
        }

        /// <summary>Umbrella bagger (우산 비닐 포장기) by the door, front +Z.</summary>
        public static void UmbrellaWrapper(Transform parent, Vector3 pos, float yaw, Color accent)
        {
            var g = Group("UmbrellaWrapper", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var steel = Mats.Lit(Chrome, 0.6f, 0.8f);
            Part(g, "Foot", PrimitiveType.Cube, new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.04f, 0.3f), Mats.Lit(Black, 0.4f));
            Part(g, "Column", PrimitiveType.Cube, new Vector3(0, 0.5f, -0.05f), new Vector3(0.28f, 0.92f, 0.16f), steel, true);
            Part(g, "Mouth", PrimitiveType.Cube, new Vector3(0, 0.98f, 0.03f), new Vector3(0.3f, 0.06f, 0.3f), Mats.Lit(accent, 0.4f));
            Part(g, "Bags", PrimitiveType.Cube, new Vector3(0, 0.99f, 0.03f), new Vector3(0.18f, 0.07f, 0.18f), Mats.Glass(new Color(0.95f, 0.97f, 1f, 0.45f)));
            Sign(g, "Label", new Vector3(0, 0.62f, 0.035f), new Vector2(0.26f, 0.12f), Color.white, "우산 비닐", C("#222222"), 0, 30, 0.004f);
            // a wet umbrella someone left in the stand next to it
            var u = Part(g, "Umbrella", PrimitiveType.Cylinder, new Vector3(0.3f, 0.45f, 0.05f), new Vector3(0.07f, 0.42f, 0.07f), Mats.Lit(C("#2E3A5C"), 0.6f));
            u.transform.localRotation = Quaternion.Euler(0, 0, -8);
        }

        /// <summary>Height and weight station (신장·체중계) against a wall, front +Z.</summary>
        public static void HeightScale(Transform parent, Vector3 pos, float yaw, Color accent)
        {
            var g = Group("HeightScale", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var white = Mats.Lit(C("#EEF0F1"), 0.5f);
            Part(g, "Platform", PrimitiveType.Cube, new Vector3(0, 0.05f, 0.1f), new Vector3(0.44f, 0.1f, 0.5f), white, true);
            Part(g, "Mat", PrimitiveType.Cube, new Vector3(0, 0.102f, 0.12f), new Vector3(0.36f, 0.006f, 0.38f), Mats.Lit(C("#3A3D42"), 0.2f));
            Part(g, "Column", PrimitiveType.Cube, new Vector3(0, 1.05f, -0.12f), new Vector3(0.12f, 1.9f, 0.08f), white, true);
            Part(g, "Rod", PrimitiveType.Cube, new Vector3(0, 1.75f, -0.02f), new Vector3(0.05f, 0.03f, 0.22f), Mats.Lit(Chrome, 0.6f, 0.8f));
            var d = Part(g, "Display", PrimitiveType.Cube, new Vector3(0, 1.35f, -0.075f), new Vector3(0.1f, 0.08f, 0.004f), Mats.Emissive(C("#8CFFB0"), 0.8f));
            d.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Sign(g, "Label", new Vector3(0, 2.06f, -0.1f), new Vector2(0.44f, 0.12f), accent, "신장 · 체중", Color.white, 0, 34, 0.01f);
        }

        /// <summary>
        /// A notice board (front +Z) with A4 sheets pinned on it; each sheet's first line is its heading.
        /// </summary>
        public static void NoticeBoard(Transform parent, Vector3 pos, float yaw, Vector2 size, string title, Color accent, string[] notices, System.Random rng)
        {
            var g = Group("NoticeBoard", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Frame", PrimitiveType.Cube, Vector3.zero, new Vector3(size.x + 0.06f, size.y + 0.06f, 0.03f), Mats.Lit(C("#9AA0A6"), 0.5f, 0.6f));
            Part(g, "Board", PrimitiveType.Cube, new Vector3(0, 0, 0.016f), new Vector3(size.x, size.y, 0.004f), Mats.Lit(C("#E9E3D3"), 0.1f));
            Part(g, "Header", PrimitiveType.Cube, new Vector3(0, size.y / 2f - 0.07f, 0.02f), new Vector3(size.x, 0.14f, 0.004f), Mats.Lit(accent, 0.3f));
            WorldText.Create(g, "Title", title, new Vector3(0, size.y / 2f - 0.07f, 0.024f), new Vector2(size.x - 0.1f, 0.12f), 44, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            int cols = Mathf.Max(1, Mathf.FloorToInt((size.x - 0.06f) / 0.26f));
            float sheetW = 0.21f, sheetH = 0.297f;
            for (int i = 0; i < notices.Length; i++)
            {
                int col = i % cols, row = i / cols;
                float x = -size.x / 2f + 0.04f + sheetW / 2f + col * ((size.x - 0.08f - sheetW) / Mathf.Max(1, cols - 1));
                float y = size.y / 2f - 0.2f - sheetH / 2f - row * (sheetH + 0.05f);
                if (y - sheetH / 2f < -size.y / 2f) break;
                float tilt = ((float)rng.NextDouble() - 0.5f) * 5f;
                var sheet = Group("Notice", g);
                sheet.localPosition = new Vector3(x, y, 0.022f);
                sheet.localRotation = Quaternion.Euler(0, 0, tilt);
                Part(sheet, "Paper", PrimitiveType.Cube, Vector3.zero, new Vector3(sheetW, sheetH, 0.002f), Mats.Lit(C("#FBFAF6"), 0.15f));
                WorldText.Create(sheet, "Text", notices[i], new Vector3(0, 0.01f, 0.003f), new Vector2(sheetW - 0.03f, sheetH - 0.04f), 22, C("#2A2A2E"), true, TextAnchor.UpperCenter,
                    Quaternion.Euler(0, 180, 0));
                Part(sheet, "Pin", PrimitiveType.Sphere, new Vector3(0, sheetH / 2f - 0.02f, 0.004f), Vector3.one * 0.016f, Mats.Lit(C(new[] { "#E5484D", "#2F8FE0", "#F2C230" }[i % 3]), 0.5f));
            }
        }

        /// <summary>
        /// A black LED rate board (front +Z): a lit header and rows of label + value columns in amber, as
        /// banks show exchange and deposit rates.
        /// </summary>
        public static Transform RateBoard(Transform parent, Vector3 pos, float yaw, Vector2 size, string title, string[] headers, string[][] rows, Color accent)
        {
            var g = Group("RateBoard", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Case", PrimitiveType.Cube, Vector3.zero, new Vector3(size.x, size.y, 0.06f), Mats.Lit(C("#121317"), 0.5f));
            var title_ = Part(g, "TitleBand", PrimitiveType.Cube, new Vector3(0, size.y / 2f - 0.09f, 0.031f), new Vector3(size.x - 0.04f, 0.16f, 0.004f), Mats.Lit(accent, 0.35f));
            title_.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            WorldText.Create(g, "Title", title, new Vector3(0, size.y / 2f - 0.09f, 0.035f), new Vector2(size.x - 0.1f, 0.14f), 48, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            int cols = headers.Length;
            float top = size.y / 2f - 0.2f, rowH = (size.y - 0.26f) / (rows.Length + 1);
            float colW = (size.x - 0.12f) / cols;
            for (int r = -1; r < rows.Length; r++)
            for (int c = 0; c < cols; c++)
            {
                // the viewer sees column 0 on their left, which is local +X for a board facing +Z
                float x = (size.x - 0.12f) / 2f - colW * (c + 0.5f);
                float y = top - rowH * (r + 1.5f);
                string s = r < 0 ? headers[c] : rows[r][c];
                var col = r < 0 ? C("#9AA0A6") : c == 0 ? C("#FFFFFF") : C("#FFB547");
                WorldText.Create(g, "Cell", s, new Vector3(x, y, 0.035f), new Vector2(colW - 0.04f, rowH * 0.9f), r < 0 ? 30 : 40, col, true,
                    c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, Quaternion.Euler(0, 180, 0));
            }
            return g;
        }

        /// <summary>
        /// A one-line scrolling LED strip (front +Z) in red or amber, clipped to its case.
        /// </summary>
        public static void TickerStrip(Transform parent, Vector3 pos, float yaw, float width, string text, Color color, float speed = 55f)
        {
            var g = Group("Ticker", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            const float h = 0.16f;
            Part(g, "Case", PrimitiveType.Cube, Vector3.zero, new Vector3(width, h, 0.05f), Mats.Lit(C("#121317"), 0.5f));
            var go = new GameObject("Viewport", typeof(RectTransform));
            go.transform.SetParent(g, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<RectMask2D>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width - 0.04f, h - 0.03f) * WorldText.PixelsPerMeter;
            rt.localPosition = new Vector3(0, 0, 0.027f);
            rt.localRotation = Quaternion.Euler(0, 180, 0);
            rt.localScale = Vector3.one / WorldText.PixelsPerMeter;
            var label = SeoulFirstWeek.UI.UIKit.Label(rt, text, 11, color, true, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.sizeDelta = new Vector2(2000, 0);
            lrt.anchoredPosition = Vector2.zero;
            var ticker = g.gameObject.AddComponent<Ticker>();
            ticker.text = label;
            ticker.speed = speed;
        }

        /// <summary>Recessed round downlight in a ceiling at height h.</summary>
        public static void Downlight(Transform parent, Vector3 pos, float h, Material lit)
        {
            var d = Part(parent, "Downlight", PrimitiveType.Cylinder, new Vector3(pos.x, h - 0.004f, pos.z), new Vector3(0.17f, 0.005f, 0.17f), lit);
            d.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A long fluorescent or LED troffer flush with the ceiling, along local X.</summary>
        public static void LinearLight(Transform parent, Vector3 pos, float h, float length, float yaw, Material lit)
        {
            var d = Part(parent, "LinearLight", PrimitiveType.Cube, new Vector3(pos.x, h - 0.008f, pos.z), new Vector3(length, 0.014f, 0.22f), lit, false, Quaternion.Euler(0, yaw, 0));
            d.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Self-service coffee and tea corner (front +Z): a low cabinet with a capsule machine, cups, tea
        /// box and a sugar caddy, and a small sign.
        /// </summary>
        public static void CoffeeCorner(Transform parent, Vector3 pos, float yaw, Material top, Material body, Color accent)
        {
            var g = Group("CoffeeCorner", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Cabinet", PrimitiveType.Cube, new Vector3(0, 0.43f, 0), new Vector3(1.2f, 0.86f, 0.5f), body, true);
            Part(g, "Top", PrimitiveType.Cube, new Vector3(0, 0.875f, 0.01f), new Vector3(1.24f, 0.03f, 0.54f), top);
            var machine = Mats.Lit(C("#2B2E33"), 0.6f, 0.3f);
            Part(g, "Machine", PrimitiveType.Cube, new Vector3(-0.32f, 1.07f, -0.05f), new Vector3(0.26f, 0.36f, 0.34f), machine);
            Part(g, "MachineSpout", PrimitiveType.Cube, new Vector3(-0.32f, 1.0f, 0.13f), new Vector3(0.08f, 0.04f, 0.06f), Mats.Lit(Chrome, 0.6f, 0.8f));
            Part(g, "Cup", PrimitiveType.Cylinder, new Vector3(-0.32f, 0.93f, 0.12f), new Vector3(0.06f, 0.04f, 0.06f), Mats.Lit(C("#F4F1EA"), 0.3f));
            for (int i = 0; i < 4; i++)
                Part(g, "Cups", PrimitiveType.Cylinder, new Vector3(0.02f + i * 0.0f, 0.95f + i * 0.04f, 0.0f), new Vector3(0.08f, 0.02f, 0.08f), Mats.Lit(C("#F4F1EA"), 0.25f));
            Part(g, "TeaBox", PrimitiveType.Cube, new Vector3(0.25f, 0.94f, -0.05f), new Vector3(0.24f, 0.1f, 0.16f), Mats.Lit(C("#6E8F4E"), 0.3f));
            Part(g, "Sugar", PrimitiveType.Cylinder, new Vector3(0.48f, 0.95f, 0.08f), new Vector3(0.08f, 0.06f, 0.08f), Mats.Lit(C("#E9D8B4"), 0.3f));
            Sign(g, "Label", new Vector3(0, 1.62f, -0.24f), new Vector2(0.8f, 0.2f), accent, "고객 휴게 · 커피 · 차", Color.white, 0, 40, 0.02f);
        }

        /// <summary>
        /// A hanging two-tier counter sign (front +Z): the counter number and its service on top, and the
        /// counter's own red LED number display under it. Returns the display.
        /// </summary>
        public static QueueBoard HangingCounterSign(Transform parent, Vector3 pos, float yaw, int window, string service, string english, Color accent, float ceiling)
        {
            var g = Group($"WindowSign_{window}", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var steel = Mats.Lit(C("#9AA0A6"), 0.5f, 0.6f);
            float rod = ceiling - pos.y - 0.2f;
            foreach (var x in new[] { -0.5f, 0.5f })
                Part(g, "Rod", PrimitiveType.Cube, new Vector3(x, 0.2f + rod / 2f, 0), new Vector3(0.015f, rod, 0.015f), steel);
            Part(g, "Board", PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(1.36f, 0.4f, 0.05f), Mats.Lit(accent, 0.35f));
            Part(g, "NumberDisc", PrimitiveType.Cylinder, new Vector3(0.5f, 0, 0.026f), new Vector3(0.3f, 0.004f, 0.3f), Mats.Lit(Color.white, 0.3f), false, Quaternion.Euler(90, 0, 0));
            WorldText.Create(g, "Number", $"{window}", new Vector3(0.5f, 0, 0.032f), new Vector2(0.26f, 0.26f), 80, accent, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(g, "Service", service, new Vector3(-0.14f, 0.06f, 0.03f), new Vector2(0.96f, 0.2f), 56, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(g, "English", english, new Vector3(-0.14f, -0.12f, 0.03f), new Vector2(0.96f, 0.1f), 30, Color.Lerp(accent, Color.white, 0.75f), true, TextAnchor.MiddleCenter,
                Quaternion.Euler(0, 180, 0));
            var display = CounterDisplay(g, new Vector3(0, -0.3f, 0), 0, window, 0.66f);
            return display;
        }

        /// <summary>A low acrylic sign that stands on a counter (front +Z).</summary>
        public static void DeskSign(Transform parent, Vector3 pos, float yaw, string text, Color accent, float width = 0.42f)
        {
            var g = Group("DeskSign", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Foot", PrimitiveType.Cube, new Vector3(0, 0.005f, 0), new Vector3(width, 0.01f, 0.08f), Mats.Glass(new Color(0.9f, 0.95f, 1f, 0.4f)));
            var b = Part(g, "Board", PrimitiveType.Cube, new Vector3(0, 0.08f, 0), new Vector3(width, 0.14f, 0.008f), Mats.Lit(accent, 0.5f));
            b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            WorldText.Create(g, "Text", text, new Vector3(0, 0.08f, 0.006f), new Vector2(width - 0.03f, 0.12f), 40, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
        }

        /// <summary>Round information desk (안내) with a counter sign, its open side towards -Z.</summary>
        public static Transform InfoDesk(Transform parent, Vector3 pos, float yaw, Material top, Material body, Color accent, string label)
        {
            var g = Group("InfoDesk", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            // a three-quarter ring of panels with a high ledge, open at the back for the staff member
            for (int i = 0; i < 7; i++)
            {
                float a = -135f + i * 45f;
                var q = Quaternion.Euler(0, a, 0);
                Part(g, "Panel", PrimitiveType.Cube, q * new Vector3(0, 0.53f, 0.62f), new Vector3(0.52f, 1.06f, 0.06f), body, true, q);
                Part(g, "Ledge", PrimitiveType.Cube, q * new Vector3(0, 1.07f, 0.6f), new Vector3(0.56f, 0.04f, 0.2f), top, false, q);
                Part(g, "Band", PrimitiveType.Cube, q * new Vector3(0, 0.82f, 0.655f), new Vector3(0.52f, 0.07f, 0.01f), Mats.Lit(accent, 0.4f), false, q);
            }
            Part(g, "Desk", PrimitiveType.Cylinder, new Vector3(0, DeskTop - 0.02f, 0.15f), new Vector3(0.95f, 0.02f, 0.95f), top);
            Part(g, "SignPost", PrimitiveType.Cube, new Vector3(0, 1.22f, 0.6f), new Vector3(0.03f, 0.26f, 0.03f), Mats.Lit(Chrome, 0.6f, 0.8f));
            Sign(g, "Sign", new Vector3(0, 1.5f, 0.6f), new Vector2(0.9f, 0.3f), accent, label, Color.white, 0, 44, 0.03f);
            return g;
        }

        /// <summary>
        /// Airport-style beam seat (front +Z): one perforated steel sheet bent into seat and back on a bracket,
        /// with an armrest on its right (+X) when <paramref name="arm"/> is set. Gang rows add the beam.
        /// </summary>
        public static Transform BeamSeat(Transform parent, Vector3 pos, Material sheet, bool arm)
        {
            var g = Group("BeamSeat", parent);
            g.localPosition = pos;
            var frame = Mats.Lit(C("#4A5058"), 0.5f, 0.6f);
            Part(g, "Seat", PrimitiveType.Cube, new Vector3(0, ChairSeat - 0.015f, 0.03f), new Vector3(0.5f, 0.03f, 0.46f), sheet, true);
            var lip = Part(g, "Lip", PrimitiveType.Cube, new Vector3(0, ChairSeat - 0.04f, 0.27f), new Vector3(0.5f, 0.05f, 0.03f), sheet);
            lip.transform.localRotation = Quaternion.Euler(30, 0, 0);
            var back = Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 0.72f, -0.22f), new Vector3(0.5f, 0.52f, 0.03f), sheet, true);
            back.transform.localRotation = Quaternion.Euler(-12, 0, 0);
            Part(g, "Bracket", PrimitiveType.Cube, new Vector3(0, ChairSeat - 0.07f, -0.04f), new Vector3(0.06f, 0.06f, 0.4f), frame);
            Part(g, "BackPost", PrimitiveType.Cube, new Vector3(0, 0.5f, -0.24f), new Vector3(0.05f, 0.2f, 0.03f), frame);
            if (arm)
            {
                Part(g, "ArmPost", PrimitiveType.Cube, new Vector3(0.31f, 0.52f, 0.0f), new Vector3(0.035f, 0.2f, 0.035f), frame);
                Part(g, "ArmPad", PrimitiveType.Cube, new Vector3(0.31f, 0.635f, -0.02f), new Vector3(0.05f, 0.03f, 0.38f), Mats.Lit(Black, 0.3f));
            }
            return g;
        }

        /// <summary>IV drip stand (수액 걸이) on casters with a bag and its line.</summary>
        public static void IvPole(Transform parent, Vector3 pos)
        {
            var g = Group("IvPole", parent);
            g.localPosition = pos;
            var steel = Mats.Lit(Chrome, 0.6f, 0.8f);
            for (int i = 0; i < 5; i++)
            {
                var q = Quaternion.Euler(0, i * 72f, 0);
                Part(g, "Leg", PrimitiveType.Cube, q * new Vector3(0, 0.05f, 0.14f), new Vector3(0.025f, 0.02f, 0.28f), steel, false, q);
                var c = Part(g, "Caster", PrimitiveType.Sphere, q * new Vector3(0, 0.025f, 0.27f), Vector3.one * 0.045f, Mats.Lit(Black, 0.4f));
                c.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Part(g, "Pole", PrimitiveType.Cylinder, new Vector3(0, 0.95f, 0), new Vector3(0.025f, 0.9f, 0.025f), steel, true);
            Part(g, "Hook", PrimitiveType.Cube, new Vector3(0, 1.84f, 0), new Vector3(0.28f, 0.015f, 0.015f), steel);
            Part(g, "Bag", PrimitiveType.Cube, new Vector3(0.12f, 1.68f, 0), new Vector3(0.12f, 0.2f, 0.04f), Mats.Glass(new Color(0.92f, 0.96f, 1f, 0.55f)));
            Part(g, "Chamber", PrimitiveType.Cylinder, new Vector3(0.12f, 1.52f, 0), new Vector3(0.025f, 0.04f, 0.025f), Mats.Glass(new Color(0.9f, 0.95f, 1f, 0.6f)));
            var line = Part(g, "Line", PrimitiveType.Cube, new Vector3(0.2f, 1.1f, 0.06f), new Vector3(0.006f, 0.82f, 0.006f), Mats.Lit(C("#E6EEF2"), 0.3f));
            line.transform.localRotation = Quaternion.Euler(8, 0, -12);
        }

        /// <summary>A potted phalaenopsis with a red ribbon, the congratulation gift every new clinic or branch keeps on its counter.</summary>
        public static void Orchid(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var g = Group("Orchid", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Pot", PrimitiveType.Cylinder, new Vector3(0, 0.07f, 0), new Vector3(0.14f, 0.07f, 0.14f), Mats.Lit(C("#F7F6F2"), 0.7f));
            var stem = Mats.Lit(C("#4F7A3A"), 0.2f);
            var petals = new[] { "#FFFFFF", "#F6D3E2", "#E58BB6" };
            for (int s = 0; s < 3; s++)
            {
                float a = s * 120f + (float)rng.NextDouble() * 30f;
                var q = Quaternion.Euler(0, a, 0);
                var st = Part(g, "Stem", PrimitiveType.Cube, q * new Vector3(0, 0.3f, 0.03f), new Vector3(0.008f, 0.42f, 0.008f), stem, false, q * Quaternion.Euler(8, 0, 0));
                st.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var col = Mats.Lit(C(petals[rng.Next(petals.Length)]), 0.3f);
                for (int f = 0; f < 5; f++)
                    Part(g, "Flower", PrimitiveType.Sphere, q * new Vector3(0, 0.4f + f * 0.035f, 0.05f + f * 0.03f), new Vector3(0.045f, 0.04f, 0.018f), col, false, q);
            }
            foreach (var x in new[] { -0.05f, 0.05f })
                Part(g, "Leaf", PrimitiveType.Cube, new Vector3(x, 0.15f, 0), new Vector3(0.12f, 0.012f, 0.05f), Mats.Lit(C("#3F6B2F"), 0.4f), false, Quaternion.Euler(0, 0, x * 300f));
            Part(g, "Ribbon", PrimitiveType.Cube, new Vector3(0.0f, 0.12f, 0.072f), new Vector3(0.04f, 0.24f, 0.004f), Mats.Lit(C("#C8242E"), 0.4f));
        }

        /// <summary>X-banner (엑스배너) on its folding stand, front +Z: a title band and a body text.</summary>
        public static void XBanner(Transform parent, Vector3 pos, float yaw, string title, string body, Color accent, Color paper)
        {
            var g = Group("XBanner", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var frame = Mats.Lit(C("#3A3D42"), 0.5f, 0.5f);
            foreach (var s in new[] { -1f, 1f })
            {
                var leg = Part(g, "Leg", PrimitiveType.Cube, new Vector3(0, 0.95f, -0.12f), new Vector3(0.012f, 1.95f, 0.012f), frame);
                leg.transform.localRotation = Quaternion.Euler(0, 0, s * 18f);
            }
            Part(g, "Strut", PrimitiveType.Cube, new Vector3(0, 0.6f, -0.24f), new Vector3(0.012f, 1.2f, 0.012f), frame, false, Quaternion.Euler(-12, 0, 0));
            Part(g, "Banner", PrimitiveType.Cube, new Vector3(0, 1.0f, -0.1f), new Vector3(0.6f, 1.8f, 0.004f), Mats.Lit(paper, 0.2f), true);
            Part(g, "Title", PrimitiveType.Cube, new Vector3(0, 1.62f, -0.097f), new Vector3(0.6f, 0.42f, 0.002f), Mats.Lit(accent, 0.3f));
            WorldText.Create(g, "TitleText", title, new Vector3(0, 1.62f, -0.094f), new Vector2(0.54f, 0.38f), 64, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(g, "Body", body, new Vector3(0, 0.95f, -0.096f), new Vector2(0.52f, 0.86f), 40, C("#1E2024"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
        }

        /// <summary>An easel board (front +Z) with a title and colour-coded numbered steps.</summary>
        public static void Easel(Transform parent, Vector3 pos, float yaw, string title, string[] steps, Color[] colors)
        {
            var g = Group("Easel", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var frame = Mats.Lit(C("#3A3D42"), 0.5f, 0.5f);
            foreach (var x in new[] { -0.32f, 0.32f })
                Part(g, "Leg", PrimitiveType.Cube, new Vector3(x, 0.8f, -0.02f), new Vector3(0.03f, 1.6f, 0.03f), frame, false, Quaternion.Euler(-8, 0, 0));
            Part(g, "BackLeg", PrimitiveType.Cube, new Vector3(0, 0.75f, -0.24f), new Vector3(0.03f, 1.5f, 0.03f), frame, false, Quaternion.Euler(16, 0, 0));
            var board = Group("Board", g);
            board.localPosition = new Vector3(0, 1.15f, 0.02f);
            board.localRotation = Quaternion.Euler(-8, 0, 0);
            Part(board, "Panel", PrimitiveType.Cube, Vector3.zero, new Vector3(0.7f, 0.95f, 0.02f), Mats.Lit(C("#FBFAF6"), 0.2f), true);
            Part(board, "Head", PrimitiveType.Cube, new Vector3(0, 0.39f, 0.011f), new Vector3(0.7f, 0.17f, 0.002f), Mats.Lit(C("#1F2F55"), 0.3f));
            WorldText.Create(board, "Title", title, new Vector3(0, 0.39f, 0.014f), new Vector2(0.64f, 0.15f), 40, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            float y = 0.22f;
            for (int i = 0; i < steps.Length; i++, y -= 0.17f)
            {
                Part(board, "Step", PrimitiveType.Cube, new Vector3(0.27f, y, 0.012f), new Vector3(0.1f, 0.13f, 0.002f), Mats.Lit(colors[i % colors.Length], 0.3f));
                WorldText.Create(board, "StepNo", $"{i + 1}", new Vector3(0.27f, y, 0.015f), new Vector2(0.09f, 0.12f), 44, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
                WorldText.Create(board, "StepText", steps[i], new Vector3(-0.05f, y, 0.014f), new Vector2(0.5f, 0.15f), 30, C("#1E2024"), true, TextAnchor.MiddleLeft, Quaternion.Euler(0, 180, 0));
            }
        }

        /// <summary>A free-standing wire newspaper rack with folded papers (front +Z).</summary>
        public static void NewspaperRack(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("NewspaperRack", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var wire = Mats.Lit(C("#3A3D42"), 0.5f, 0.6f);
            foreach (var x in new[] { -0.22f, 0.22f })
                Part(g, "Side", PrimitiveType.Cube, new Vector3(x, 0.5f, 0), new Vector3(0.02f, 1.0f, 0.3f), wire, true);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.3f + i * 0.28f;
                Part(g, "Shelf", PrimitiveType.Cube, new Vector3(0, y, 0.02f), new Vector3(0.44f, 0.015f, 0.24f), wire, false, Quaternion.Euler(-15, 0, 0));
                var paper = Part(g, "Paper", PrimitiveType.Cube, new Vector3(0, y + 0.1f, 0.03f), new Vector3(0.36f, 0.22f, 0.02f), Mats.Lit(C("#ECE8DE"), 0.1f), false, Quaternion.Euler(-15, 0, 0));
                paper.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                WorldText.Create(g, "Masthead", new[] { "경제신문", "매일신문", "생활신문" }[i], new Vector3(0, y + 0.18f, 0.057f), new Vector2(0.32f, 0.05f), 30,
                    C("#1E2024"), true, TextAnchor.MiddleCenter, Quaternion.Euler(15, 180, 0));
            }
        }

        /// <summary>A glass partition with a frosted band at eye level along local X (front +Z), on a floor track.</summary>
        public static void GlassPartition(Transform parent, Vector3 from, Vector3 to, float height, Color frost)
        {
            var g = Group("GlassPartition", parent);
            var mid = (from + to) / 2f;
            var dir = to - from;
            float len = dir.magnitude;
            var q = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir.normalized));
            var frame = Mats.Lit(C("#3A3D42"), 0.4f, 0.4f);
            Part(g, "Glass", PrimitiveType.Cube, mid + Vector3.up * height / 2f, new Vector3(len, height, 0.012f), Mats.Glass(new Color(0.85f, 0.93f, 0.98f, 0.12f)), true, q);
            Part(g, "Frost", PrimitiveType.Cube, mid + Vector3.up * 1.3f, new Vector3(len, 0.4f, 0.016f), Mats.Glass(new Color(frost.r, frost.g, frost.b, 0.55f)), false, q);
            Part(g, "Track", PrimitiveType.Cube, mid + Vector3.up * 0.02f, new Vector3(len, 0.04f, 0.05f), frame, false, q);
            Part(g, "Head", PrimitiveType.Cube, mid + Vector3.up * (height - 0.02f), new Vector3(len, 0.04f, 0.05f), frame, false, q);
        }

        /// <summary>Brochure stand with product leaflets (bank products, clinic health guides).</summary>
        public static void BrochureStand(Transform parent, Vector3 pos, float yaw, Color accent, System.Random rng)
        {
            var g = Group("BrochureStand", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Part(g, "Foot", PrimitiveType.Cube, new Vector3(0, 0.02f, 0), new Vector3(0.45f, 0.04f, 0.35f), Mats.Lit(C("#3A3D42"), 0.4f));
            var back = Part(g, "Back", PrimitiveType.Cube, new Vector3(0, 0.8f, -0.05f), new Vector3(0.44f, 1.5f, 0.03f), Mats.Lit(accent, 0.35f), true);
            back.transform.localRotation = Quaternion.Euler(-6, 0, 0);
            var covers = new[] { "#FFFFFF", "#FFD23F", "#7FB7FF", "#F4C6D4", "#DFF1D2" };
            for (int i = 0; i < 4; i++)
            {
                // the back leans back, so each higher brochure sits a little further back
                var b = Part(g, "Brochure", PrimitiveType.Cube, new Vector3(0, 0.45f + i * 0.3f, 0.012f - i * 0.032f), new Vector3(0.3f, 0.24f, 0.01f),
                    Mats.Lit(C(covers[rng.Next(covers.Length)]), 0.3f));
                b.transform.localRotation = Quaternion.Euler(-6, 0, 0);
            }
        }
    }
}
