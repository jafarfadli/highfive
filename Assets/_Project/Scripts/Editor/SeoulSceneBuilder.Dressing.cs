using System.Collections.Generic;
using SeoulFirstWeek.World;
using UnityEngine;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// The details that make the new places read as the real thing (added 2026-11-25): the airport hall's quarantine
    /// cameras, smart-entry gates, baggage services and customs X-ray; the arrivals hall's clerestory, shops, café and
    /// SIM counter; the station's exit sign, area map, lockers, train times and Line 4 route strip; the platform's
    /// benches and station-name boards; the ER's nurses' station, wheelchairs, arriving stretcher and vending machine.
    /// Every piece keeps clear of the player's way (the lanes, the paths to the counters, the doors and waypoints).
    /// All brands are fictional; 늘봄24, 별빛화장품 and 새싹은행 are the game's own.
    /// </summary>
    public static partial class SeoulSceneBuilder
    {
        // ------------------------------------------------------------------- shared pieces

        /// <summary>A lit advertising box (fictional ads) on a wall or a column face, facing +Z after yaw.</summary>
        static Transform LightBox(Transform parent, Vector3 pos, float yaw, Vector2 size, Color face, string text, Color textColor, int font)
        {
            var g = Group("LightBox", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(g, "Frame", Vector3.zero, new Vector3(size.x + 0.08f, size.y + 0.08f, 0.1f), C("#2B2E33"), false);
            Emissive(g, "Face", new Vector3(0, 0, 0.052f), new Vector3(size.x, size.y, 0.004f), face, 0.8f);
            WorldText.Create(g, "Text", text, new Vector3(0, 0, 0.06f), size * 0.88f, font, textColor, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            return g;
        }

        /// <summary>A hanging sign readable from both sides.</summary>
        static void HangingSign2(Room r, string name, Vector3 pos, float yaw, Vector2 size, Color board, string text, Color textColor, int font)
        {
            HangingSign(r, name, pos, yaw, size, board, text, textColor, font);
            Sign(r.Root, name + "Back", pos, size, board, text, textColor, yaw + 180f, font, 0.04f);
        }

        static void Wheelchair(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("Wheelchair", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var frame = C("#A4AAB0");
            var cloth = C("#2B3A55");
            Box(g, "Seat", new Vector3(0, 0.5f, 0.02f), new Vector3(0.44f, 0.04f, 0.42f), cloth, true);
            Box(g, "Back", new Vector3(0, 0.76f, -0.2f), new Vector3(0.44f, 0.44f, 0.03f), cloth, false);
            foreach (var sx in new[] { -1f, 1f })
            {
                Cylinder(g, "Wheel", new Vector3(sx * 0.27f, 0.3f, -0.06f), new Vector3(0.6f, 0.012f, 0.6f), C("#2B2E33")).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Cylinder(g, "Hand rim", new Vector3(sx * 0.3f, 0.3f, -0.06f), new Vector3(0.52f, 0.006f, 0.52f), frame).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Cylinder(g, "Caster", new Vector3(sx * 0.19f, 0.06f, 0.26f), new Vector3(0.12f, 0.012f, 0.12f), C("#2B2E33")).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Box(g, "Rail", new Vector3(sx * 0.23f, 0.47f, 0.06f), new Vector3(0.025f, 0.025f, 0.5f), frame, false);
                Box(g, "Upright", new Vector3(sx * 0.23f, 0.72f, -0.22f), new Vector3(0.025f, 0.56f, 0.025f), frame, false);
                Box(g, "Handle", new Vector3(sx * 0.23f, 1.0f, -0.29f), new Vector3(0.03f, 0.03f, 0.13f), C("#1E1E1E"), false);
                Box(g, "Armrest", new Vector3(sx * 0.23f, 0.7f, 0.03f), new Vector3(0.05f, 0.03f, 0.34f), C("#1E1E1E"), false);
                Box(g, "Footrest", new Vector3(sx * 0.1f, 0.13f, 0.34f), new Vector3(0.16f, 0.02f, 0.11f), C("#2B2E33"), false);
                Box(g, "Leg", new Vector3(sx * 0.17f, 0.3f, 0.27f), new Vector3(0.022f, 0.36f, 0.022f), frame, false);
            }
        }

        /// <summary>A wheeled stretcher (침대차) with a pillow and a folded blanket.</summary>
        static void Stretcher(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("Stretcher", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var frame = C("#A4AAB0");
            Box(g, "Frame", new Vector3(0, 0.62f, 0), new Vector3(0.6f, 0.06f, 1.95f), frame, true);
            Box(g, "Mattress", new Vector3(0, 0.71f, 0), new Vector3(0.56f, 0.1f, 1.9f), C("#E4ECF2"), false);
            Box(g, "Pillow", new Vector3(0, 0.79f, -0.76f), new Vector3(0.42f, 0.07f, 0.3f), Color.white, false);
            Box(g, "Blanket", new Vector3(0, 0.78f, 0.5f), new Vector3(0.58f, 0.05f, 0.62f), C("#E9B445"), false);
            foreach (var sx in new[] { -0.25f, 0.25f })
            {
                Box(g, "SideRail", new Vector3(sx * 1.2f, 0.86f, 0.05f), new Vector3(0.025f, 0.025f, 1.2f), frame, false);
                foreach (var sz in new[] { -0.85f, 0.85f })
                {
                    Box(g, "Leg", new Vector3(sx, 0.33f, sz), new Vector3(0.04f, 0.56f, 0.04f), frame, false);
                    Cylinder(g, "Wheel", new Vector3(sx, 0.05f, sz), new Vector3(0.1f, 0.02f, 0.1f), C("#2B2E33")).transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
            Box(g, "PushBar", new Vector3(0, 0.95f, -1.0f), new Vector3(0.5f, 0.03f, 0.03f), frame, false);
        }

        static void VendingMachine(Transform parent, Vector3 pos, float yaw, Color body, string label, System.Random rng)
        {
            var g = Group("VendingMachine", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(g, "Body", new Vector3(0, 0.92f, 0), new Vector3(1.0f, 1.84f, 0.75f), body, true);
            Emissive(g, "Window", new Vector3(-0.1f, 1.12f, 0.377f), new Vector3(0.68f, 1.0f, 0.004f), C("#EEF4F8"), 0.6f);
            string[] cans = { "#D8343B", "#2F5FD0", "#1FA37A", "#F2C230", "#F28C28", "#FFFFFF", "#7A3B8C" };
            for (int row = 0; row < 4; row++)
            for (int k = 0; k < 6; k++)
                Box(g, "Drink", new Vector3(-0.38f + k * 0.112f, 0.72f + row * 0.25f, 0.386f), new Vector3(0.07f, 0.16f, 0.012f), C(cans[rng.Next(cans.Length)]), false);
            Box(g, "Slot", new Vector3(-0.1f, 0.3f, 0.38f), new Vector3(0.6f, 0.14f, 0.01f), C("#1E1E1E"), false);
            Box(g, "Panel", new Vector3(0.38f, 1.08f, 0.38f), new Vector3(0.16f, 0.5f, 0.01f), C("#2B2E33"), false);
            Emissive(g, "Display", new Vector3(0.38f, 1.25f, 0.387f), new Vector3(0.1f, 0.05f, 0.004f), C("#3DDC84"), 1f);
            Sign(g, "Header", new Vector3(0, 1.73f, 0.38f), new Vector2(0.96f, 0.16f), C("#FFFFFF"), label, body, 0, 34, 0.01f);
        }

        static void Lockers(Transform parent, Vector3 pos, float yaw, int cols, int rows, Color doors)
        {
            var g = Group("Lockers", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            float cw = 0.42f, rh = 0.44f, width = cols * cw;
            Box(g, "Cabinet", new Vector3(0, rows * rh / 2f + 0.1f, 0), new Vector3(width + 0.06f, rows * rh + 0.2f, 0.55f), C("#C9CED4"), true);
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                float x = -width / 2f + cw * (c + 0.5f), y = 0.1f + rh * (r + 0.5f);
                if (c == cols / 2 && r == 1)
                {
                    // the touch screen where you pay and get the code
                    Box(g, "Screen", new Vector3(x, y + rh / 2f, 0.28f), new Vector3(cw - 0.04f, rh * 2f - 0.04f, 0.02f), C("#2B2E33"), false);
                    Emissive(g, "ScreenGlow", new Vector3(x, y + rh / 2f + 0.05f, 0.292f), new Vector3(cw - 0.12f, rh - 0.08f, 0.004f), C("#6FC3F0"), 0.9f);
                    continue;
                }
                if (c == cols / 2 && r == 2) continue;
                Box(g, "Door", new Vector3(x, y, 0.28f), new Vector3(cw - 0.03f, rh - 0.03f, 0.015f), doors, false);
                Box(g, "Handle", new Vector3(x + cw * 0.3f, y, 0.292f), new Vector3(0.02f, 0.1f, 0.015f), C("#5A5F66"), false);
            }
            Sign(g, "Label", new Vector3(0, rows * rh + 0.38f, 0.2f), new Vector2(Mathf.Min(width, 1.6f), 0.24f), doors, "물품보관함 · Lockers", Color.white, 0, 30, 0.02f);
        }

        /// <summary>A smart-entry gate (자동출입국심사): two glass-topped cabinets, a passport reader, a face camera.</summary>
        static void SmartGate(Transform parent, Vector3 pos, float yaw, Color accent)
        {
            var g = Group("SmartGate", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var sx in new[] { -0.5f, 0.5f })
            {
                Box(g, "Cabinet", new Vector3(sx, 0.5f, 0), new Vector3(0.24f, 1.0f, 1.8f), C("#D5DADF"), true);
                Box(g, "Band", new Vector3(sx, 1.0f, 0), new Vector3(0.25f, 0.03f, 1.8f), accent, false);
                Glass(g, "Fin", new Vector3(sx, 1.35f, 0.2f), new Vector3(0.02f, 0.7f, 1.3f), 0, false);
            }
            Glass(g, "FlapIn", new Vector3(0, 0.8f, -0.75f), new Vector3(0.76f, 0.9f, 0.02f), 0, false);
            Glass(g, "FlapOut", new Vector3(0, 0.8f, 0.75f), new Vector3(0.76f, 0.9f, 0.02f), 0, false);
            Box(g, "Reader", new Vector3(-0.42f, 1.06f, -0.45f), new Vector3(0.16f, 0.06f, 0.22f), C("#2B2E33"), false);
            Emissive(g, "ReaderGlass", new Vector3(-0.42f, 1.095f, -0.45f), new Vector3(0.12f, 0.004f, 0.16f), C("#39FF88"), 1.2f);
            Cylinder(g, "CameraPost", new Vector3(0.5f, 1.5f, 0.55f), new Vector3(0.05f, 0.5f, 0.05f), C("#9AA0A6"));
            Box(g, "Camera", new Vector3(0.44f, 1.86f, 0.5f), new Vector3(0.14f, 0.12f, 0.08f), C("#1E1E1E"), false, -30);
        }

        /// <summary>A thermal camera on a tripod, aimed at the travellers coming in.</summary>
        static void ThermalCamera(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("ThermalCamera", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 3; i++)
            {
                var q = Quaternion.Euler(0, i * 120f, 0);
                var leg = Box(g, "Leg", q * new Vector3(0, 0.62f, 0.2f), new Vector3(0.025f, 1.3f, 0.025f), C("#2B2E33"), false);
                leg.transform.localRotation = q * Quaternion.Euler(-9f, 0, 0);
            }
            Box(g, "Camera", new Vector3(0, 1.38f, 0.05f), new Vector3(0.16f, 0.14f, 0.26f), C("#E6E8EA"), false);
            Box(g, "Lens", new Vector3(0, 1.38f, 0.2f), new Vector3(0.1f, 0.1f, 0.05f), C("#1E1E1E"), false);
        }

        /// <summary>An X-ray baggage scanner with its rollers, lead curtain and screen.</summary>
        static void XRayScanner(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group("XRay", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(g, "Tunnel", new Vector3(0, 0.95f, 0), new Vector3(0.95f, 1.1f, 1.4f), C("#D9DDE1"), true);
            Box(g, "Opening", new Vector3(0, 0.82f, -0.705f), new Vector3(0.66f, 0.5f, 0.01f), C("#2B2E33"), false);
            for (int i = 0; i < 6; i++)
                Box(g, "Curtain", new Vector3(-0.27f + i * 0.108f, 0.82f, -0.712f), new Vector3(0.1f, 0.48f, 0.006f), C("#4A4F57"), false);
            foreach (var sz in new[] { -1f, 1f })
            {
                Box(g, "Roller", new Vector3(0, 0.62f, sz * 1.15f), new Vector3(0.8f, 0.06f, 0.9f), C("#5A5F66"), true);
                for (int i = 0; i < 6; i++)
                    Cylinder(g, "Roll", new Vector3(0, 0.66f, sz * (0.75f + i * 0.14f)), new Vector3(0.05f, 0.38f, 0.05f), C("#9AA0A6")).transform.localRotation = Quaternion.Euler(0, 0, 90);
                foreach (var sx in new[] { -0.36f, 0.36f })
                    Box(g, "RollerLeg", new Vector3(sx, 0.3f, sz * 1.45f), new Vector3(0.04f, 0.6f, 0.04f), C("#9AA0A6"), false);
            }
            Box(g, "Stripe", new Vector3(0, 1.38f, 0), new Vector3(0.96f, 0.08f, 1.41f), C("#1F2F55"), false);
        }

        /// <summary>A nested row of luggage carts in their rack.</summary>
        static void CartRow(Transform parent, Vector3 pos, float yaw, int carts, Color handle)
        {
            var g = Group("Carts", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < carts; i++)
            {
                float z = i * 0.32f;
                Box(g, "Base", new Vector3(0, 0.25f, z), new Vector3(0.6f, 0.04f, 0.95f), C("#9AA0A6"), false);
                Box(g, "Back", new Vector3(0, 0.7f, z + 0.46f), new Vector3(0.6f, 0.9f, 0.03f), C("#AEB4BA"), false);
                Box(g, "Handle", new Vector3(0, 1.15f, z + 0.5f), new Vector3(0.62f, 0.05f, 0.05f), handle, false);
            }
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, (carts - 1) * 0.16f + 0.2f);
            col.size = new Vector3(0.7f, 1.2f, (carts - 1) * 0.32f + 1.1f);
        }

        /// <summary>A small shop set into a wall: lit shelves behind glass, a fascia with its name.</summary>
        static void ShopFront(Transform parent, Vector3 pos, float yaw, float width, Color fascia, string name, System.Random rng)
        {
            var g = Group("Shop", parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            // local frame: +Z faces the hall, the wall is at z = 0
            Emissive(g, "Inside", new Vector3(0, 1.3f, 0.02f), new Vector3(width - 0.2f, 2.6f, 0.02f), C("#F6F3EC"), 0.55f);
            string[] goods = { "#D8343B", "#2F5FD0", "#1FA37A", "#F2C230", "#F28C28", "#FFFFFF", "#E26A9B" };
            for (int s = 0; s < 4; s++)
            {
                Box(g, "Shelf", new Vector3(0, 0.35f + s * 0.5f, 0.25f), new Vector3(width - 0.5f, 0.03f, 0.4f), C("#D5DADF"), false);
                for (float x = -width / 2f + 0.4f; x < width / 2f - 0.35f; x += 0.22f)
                    Box(g, "Goods", new Vector3(x, 0.47f + s * 0.5f, 0.3f), new Vector3(0.16f, 0.2f, 0.16f), C(goods[rng.Next(goods.Length)]), false);
            }
            Glass(g, "Window", new Vector3(-0.6f, 1.3f, 0.62f), new Vector3(width - 1.6f, 2.6f, 0.03f), 0, true);
            Box(g, "Mullion", new Vector3(width / 2f - 1.1f, 1.3f, 0.62f), new Vector3(0.06f, 2.6f, 0.06f), C("#3A3D42"), false);
            Box(g, "Frame", new Vector3(0, 2.63f, 0.62f), new Vector3(width, 0.06f, 0.08f), C("#3A3D42"), false);
            // the doorway: an open sliding door, with a blocker so the player can't walk into the shop
            var blocker = new GameObject("ShopBlocker") { layer = 2 };
            blocker.transform.SetParent(g, false);
            blocker.transform.localPosition = new Vector3(width / 2f - 0.55f, 1.2f, 0.62f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(1.0f, 2.4f, 0.1f);
            Sign(g, "Fascia", new Vector3(0, 3.0f, 0.62f), new Vector2(width, 0.62f), fascia, name, Color.white, 0, 52, 0.12f);
            Emissive(g, "LedStrip", new Vector3(0, 2.68f, 0.66f), new Vector3(width - 0.2f, 0.03f, 0.02f), C("#FFF4E0"), 1.2f);
        }

        /// <summary>A painted guide line on the floor with words in it now and then (hospitals and stations).</summary>
        static void FloorLine(Transform parent, Vector3 from, Vector3 to, Color color, string words)
        {
            var dir = to - from;
            dir.y = 0;
            float len = dir.magnitude;
            var g = Group("FloorLine", parent);
            g.localPosition = (from + to) / 2f + Vector3.up * 0.004f;
            g.localRotation = Quaternion.LookRotation(dir.normalized);
            Box(g, "Line", Vector3.zero, new Vector3(0.22f, 0.006f, len), color, false);
            if (string.IsNullOrEmpty(words)) return;
            // the words sit on a wider plate of the line's colour, their tops towards where the line leads
            for (float z = -len / 2f + 1.0f; z < len / 2f - 0.5f; z += 3.2f)
            {
                Box(g, "Plate", new Vector3(0, 0.001f, z), new Vector3(0.95f, 0.006f, 0.34f), color, false);
                WorldText.Create(g, "Words", words, new Vector3(0, 0.0045f, z), new Vector2(0.88f, 0.28f), 44, Color.white, true, TextAnchor.MiddleCenter,
                    Quaternion.Euler(90, 0, 0));
            }
        }

        // ------------------------------------------------------------------- the airport hall

        static void DressAirport(Room r, float w, float d, float h, float counterZ)
        {
            var root = r.Root;
            var rng = new System.Random(4101);
            var navy = C("#1F2F55");
            var red = C("#D8343B");
            var blue = C("#2A6FDB");
            var beam = TextureKit.Surface("Panel_Airport", TextureKit.Plastic(), C("#E9EBEE"), 0.6f);

            // the high ceiling's structure: deep beams between the light lines
            for (float z = -8f; z <= 8f; z += 4f)
                Slab(root, "Beam", new Vector3(0, h - 0.3f, z), new Vector3(w, 0.6f, 0.5f), beam, false, false);

            // welcome, in the languages the visitors read, over the corridor from the gates
            Sign(root, "Welcome", new Vector3(0, 4.6f, -d / 2 + 0.04f), new Vector2(12f, 1.3f), navy,
                "환영합니다 · Welcome to Korea\n<size=40>Chào mừng đến Hàn Quốc · Selamat datang di Korea</size>", Color.white, 0, 52, 0.06f);

            // quarantine (검역) first: thermal cameras on the travellers coming in, an officer at a screen
            ThermalCamera(root, new Vector3(1.9f, 0, -9.7f), -120f);
            ThermalCamera(root, new Vector3(3.2f, 0, -10.8f), -105f);
            InteriorKit.Desk(root, new Vector3(5.4f, 0, -10.1f), -90, new Vector3(1.3f, InteriorKit.DeskTop, 0.7f), r.TopMat, beam);
            var screen = InteriorKit.Monitor(root, new Vector3(5.5f, InteriorKit.DeskTop, -10.1f), 90, C("#F28C28"));
            Extra(r, "QuarantineOfficer", HumanLook.Officer(true, 11), new Vector3(6.25f, 0, -10.1f), -90, lookAt: screen);
            HangingSign2(r, "QuarantineSign", new Vector3(4.2f, 3.4f, -9.0f), 0, new Vector2(3.0f, 0.55f), C("#1FA37A"), "검역 · Quarantine", Color.white, 46);
            Poster(root, new Vector3(7.4f, 1.6f, -d / 2 + 0.04f), 0, new Vector2(1.6f, 1.0f),
                "발열 · 기침이 있으면\n<size=24>검역관에게 알려 주세요\nFever or cough? Tell an officer</size>", "#FFFFFF", "#1F6B4F", 30);

            // a second file of travellers in the foreigners' lane, away from the player's way to booth 3
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(-8.7f, 0, counterZ - 3.0f - i * 1.5f);
                Extra(r, $"Queue_F2_{i}", Traveller(rng, i % 2 == 1), pos, 0, phone: i == 2);
                Suitcase(root, pos + new Vector3(-0.45f, 0, 0.2f), 0, C(new[] { "#7A3B8C", "#2F6B5A", "#C9A227" }[i]));
            }

            // smart entry (자동출입국심사) gates east of the Koreans' lane, one in use
            for (int i = 0; i < 3; i++)
                SmartGate(root, new Vector3(12.4f + i * 1.5f, 0, counterZ - 0.4f), 0, blue);
            HangingSign2(r, "SmartEntrySign", new Vector3(13.9f, 3.5f, counterZ - 2.0f), 0, new Vector2(4.0f, 0.6f), blue,
                "자동출입국심사 · Smart Entry", Color.white, 46);
            Extra(r, "SmartEntryUser", HumanLook.Office(rng, false), new Vector3(13.9f, 0, counterZ - 1.75f), 0);

            // ads on the east wall
            LightBox(root, new Vector3(w / 2 - 0.06f, 2.3f, -3.5f), -90, new Vector2(3.2f, 1.7f), C("#E26A9B"),
                "별빛화장품\n<size=40>명동 본점 · K-Beauty</size>", Color.white, 84);
            LightBox(root, new Vector3(w / 2 - 0.06f, 2.3f, 2.5f), -90, new Vector2(3.2f, 1.7f), C("#1F2F55"),
                "eSIM · 로밍\n<size=40>도착하자마자 바로 개통\nReady when you land</size>", C("#FFD23F"), 72);
            InteriorKit.Clock(root, new Vector3(w / 2 - 0.05f, 4.0f, -0.5f), -90, 0.7f);

            // baggage services and the cart rack in the north-west corner
            Slab(root, "BaggageDesk", new Vector3(-w / 2 + 1.6f, 0.55f, 9.4f), new Vector3(0.7f, 1.1f, 2.6f), beam, true);
            Box(root, "BaggageDeskTop", new Vector3(-w / 2 + 1.6f, 1.12f, 9.4f), new Vector3(0.8f, 0.04f, 2.7f), C("#3A3D42"), false);
            Sign(root, "BaggageServices", new Vector3(-w / 2 + 0.06f, 2.6f, 9.4f), new Vector2(3.4f, 0.62f), navy, "수하물 서비스 · Baggage Services", Color.white, 90, 46, 0.06f);
            Extra(r, "BaggageStaff", HumanLook.Office(rng, true), new Vector3(-w / 2 + 0.85f, 0, 9.0f), 90, talking: true);
            Extra(r, "BaggageAsk", Traveller(rng, false), new Vector3(-w / 2 + 2.5f, 0, 9.8f), -90);
            CartRow(root, new Vector3(-11.6f, 0, 8.6f), 0, 7, blue);
            HangingSign2(r, "CartSign", new Vector3(-11.6f, 3.0f, 9.6f), 90, new Vector2(2.4f, 0.5f), navy, "카트 · Carts", Color.white, 40);
            Extra(r, "BagWaiter2", Traveller(rng, true), new Vector3(-10.9f, 0, 3.8f), -90, phone: true);
            Extra(r, "BagWaiter3", Traveller(rng, false), new Vector3(-10.9f, 0, -2.7f), -90);

            // customs: the X-ray for declared goods, a second officer at its screen, the sign over the exits
            XRayScanner(root, new Vector3(-w / 2 + 1.5f, 0, -9.6f), 0);
            Extra(r, "CustomsXRay", HumanLook.Officer(true, 12), new Vector3(-w / 2 + 2.7f, 0, -9.4f), -90);
            HangingSign2(r, "CustomsSign", new Vector3(-w / 2 + 2.6f, 3.6f, -5.45f), 90, new Vector2(3.0f, 0.6f), navy, "세관 · Customs", Color.white, 48);
            Poster(root, new Vector3(-w / 2 + 0.06f, 1.7f, -8.2f), 90, new Vector2(1.1f, 0.8f),
                "면세 범위\n<size=26>USD 800 · Duty-free</size>", "#FFFFFF", "#1F2F55", 34);

            InteriorKit.Extinguisher(root, new Vector3(w / 2 - 0.3f, 0, -8.6f), -90);
            InteriorKit.ExitSign(root, new Vector3(2.4f, 2.5f, -d / 2 + 0.05f), 0);
        }

        // ------------------------------------------------------------------- the arrivals hall

        static void DressArrivals(Room r, Vector3 hall, float hw, float hd, float hh)
        {
            var root = r.Root;
            var rng = new System.Random(4102);
            var navy = C("#1F2F55");
            var panel = TextureKit.Surface("Panel_Airport", TextureKit.Plastic(), C("#E9EBEE"), 0.6f);

            // a band of glass high in the south wall: the sky over the bus stops
            Emissive(root, "Clerestory", hall + new Vector3(0, 4.85f, -hd / 2 + 0.03f), new Vector3(hw - 2f, 1.0f, 0.02f), C("#BFD9EE"), 0.35f);
            for (float x = -hw / 2 + 1f; x <= hw / 2 - 1f; x += 2.5f)
                Box(root, "Mullion", hall + new Vector3(x, 4.85f, -hd / 2 + 0.06f), new Vector3(0.08f, 1.1f, 0.05f), C("#3A3D42"), false);
            Box(root, "ClerestorySill", hall + new Vector3(0, 4.3f, -hd / 2 + 0.06f), new Vector3(hw - 2f, 0.06f, 0.08f), C("#3A3D42"), false);

            // shops along the north wall: the game's own convenience store, a café, and a SIM counter on the east wall
            ShopFront(root, hall + new Vector3(-10.8f, 0, hd / 2 - 0.02f), 180, 5.0f, C("#F28C28"), "늘봄24 · 편의점", rng);
            // the café: a counter, a menu board, a coffee machine, the barista
            var cafe = hall + new Vector3(2.5f, 0, hd / 2);
            Slab(root, "CafeCounter", cafe + new Vector3(0, 0.55f, -0.9f), new Vector3(3.4f, 1.1f, 0.6f), panel, true);
            Box(root, "CafeTop", cafe + new Vector3(0, 1.12f, -0.9f), new Vector3(3.5f, 0.04f, 0.7f), C("#8A6A55"), false);
            Box(root, "CoffeeMachine", cafe + new Vector3(-0.9f, 1.36f, -0.85f), new Vector3(0.6f, 0.44f, 0.42f), C("#B9BEC4"), false);
            Glass(root, "PastryCase", cafe + new Vector3(0.8f, 1.3f, -0.9f), new Vector3(1.1f, 0.32f, 0.5f), 0, false);
            for (int i = 0; i < 4; i++)
                Box(root, "Pastry", cafe + new Vector3(0.45f + i * 0.24f, 1.18f, -0.9f), new Vector3(0.16f, 0.08f, 0.12f), C(new[] { "#C98A4B", "#E9B445", "#8A5A3B", "#F2D7A0" }[i]), false);
            Sign(root, "CafeSign", cafe + new Vector3(0, 3.0f, -0.05f), new Vector2(3.6f, 0.6f), C("#3B2A20"), "구름카페 · Cloud Café", C("#F6E7C8"), 180, 50, 0.08f);
            LightBox(root, cafe + new Vector3(0, 2.1f, -0.06f), 180, new Vector2(2.6f, 0.8f), C("#2B2420"),
                "아메리카노 4,500  ·  라떼 5,000\n<size=30>Americano · Latte · 유자차 5,500</size>", C("#F6E7C8"), 40);
            Extra(r, "Barista", HumanLook.Vendor(rng), cafe + new Vector3(-0.2f, 0, -0.35f), 180);
            Extra(r, "CafeCustomer", Traveller(rng, true), cafe + new Vector3(0.3f, 0, -1.75f), 0, phone: true);
            Suitcase(root, cafe + new Vector3(0.85f, 0, -1.8f), 0, C("#3B5B8C"));

            // the SIM / roaming counter on the east wall
            var sim = hall + new Vector3(hw / 2, 0, -1.0f);
            Slab(root, "SimCounter", sim + new Vector3(-0.9f, 0.55f, 0), new Vector3(0.6f, 1.1f, 2.6f), panel, true);
            Box(root, "SimTop", sim + new Vector3(-0.9f, 1.12f, 0), new Vector3(0.7f, 0.04f, 2.7f), C("#2B2E33"), false);
            Sign(root, "SimSign", sim + new Vector3(-0.06f, 2.7f, 0), new Vector2(3.6f, 0.62f), C("#6A3FB5"), "하늘텔레콤 · eSIM · USIM · 로밍", Color.white, -90, 44, 0.08f);
            InteriorKit.Monitor(root, sim + new Vector3(-0.85f, 1.14f, 0.6f), 90, C("#B9A8F0"));
            Extra(r, "SimStaff", HumanLook.Office(rng, true), sim + new Vector3(-0.35f, 0, -0.3f), -90, talking: true);
            Extra(r, "SimCustomer", Traveller(rng, false), sim + new Vector3(-1.55f, 0, 0.2f), 90);

            // ads on the columns, both faces
            foreach (var x in new[] { -12.5f, 12.5f })
            {
                var c = hall + new Vector3(x, 0, 1.5f);
                bool west = x < 0;
                LightBox(root, c + new Vector3(0, 2.2f, -0.46f), 180, new Vector2(0.8f, 1.6f), west ? C("#E26A9B") : C("#1FA37A"),
                    west ? "별빛\n화장품\n<size=34>명동</size>" : "서울\n시티\n투어\n<size=30>City Tour</size>", Color.white, 56);
                LightBox(root, c + new Vector3(0, 2.2f, 0.46f), 0, new Vector2(0.8f, 1.6f), west ? C("#2F5FD0") : C("#F28C28"),
                    west ? "환전\n<size=34>EXCHANGE\n24시</size>" : "공항\n버스\n<size=30>Airport Bus</size>", Color.white, 56);
            }

            // wayfinding for the way out: bus stops below, taxis to the east
            HangingSign2(r, "ToBuses", hall + new Vector3(-1.0f, 3.7f, -4.2f), 0, new Vector2(6.0f, 0.7f), C("#2B2E33"),
                "<color=#FFD23F>↓</color> 버스 승차장 · Bus Stops  <color=#FFD23F>5 · 3</color>     택시 · Taxi <color=#FFD23F>→</color>", Color.white, 46);

            // people on their way out, a family at the information desk
            Extra(r, "Leaving0", Traveller(rng, false), hall + new Vector3(-1.8f, 0, -6.6f), 180, phone: true);
            Suitcase(root, hall + new Vector3(-1.3f, 0, -6.8f), 180, C("#B83B3B"));
            Extra(r, "Leaving1", Traveller(rng, true), hall + new Vector3(0.6f, 0, -7.4f), 200);
            Extra(r, "FamilyKid", HumanLook.Schoolkid(rng, true), hall + new Vector3(7.0f, 0, 0.7f), 10);
            Extra(r, "FamilyParent", HumanLook.Office(rng, true), hall + new Vector3(6.6f, 0, 0.35f), 20, talking: true);

            InteriorKit.WaterDispenser(root, hall + new Vector3(-hw / 2 + 0.35f, 0, -4.5f), 90);
            InteriorKit.Extinguisher(root, hall + new Vector3(-hw / 2 + 0.3f, 0, -6.0f), 90);
            InteriorKit.Clock(root, hall + new Vector3(-hw / 2 + 0.05f, 3.2f, 2.0f), 90, 0.6f);
        }

        // ------------------------------------------------------------------- the station concourse

        static void DressConcourse(Room r, float w, float d, float h, Color line4)
        {
            var root = r.Root;
            var rng = new System.Random(5101);
            var yellow = C("#F2C230");

            // the way out, in the yellow of Seoul's exit signs
            // one-sided: from the gates it points the way out; people coming in see only its back
            HangingSign(r, "ExitBoard", new Vector3(0, 2.8f, -d / 2 + 3.5f), 0, new Vector2(3.6f, 0.5f), yellow,
                "<b>↑ 6</b> 나가는 곳 · Exit · 명동길", C("#1E1E1E"), 44);
            // its back, for people coming down from the street: the way to the trains
            Sign(root, "ExitBoardBack", new Vector3(0, 2.8f, -d / 2 + 3.5f - 0.045f), new Vector2(3.6f, 0.5f), C("#2B2E33"),
                "<color=#00A4E3>④</color> ↑ 타는 곳 · To Trains", Color.white, 180, 44, 0.04f);

            // the area map, south wall between the stairs up and the phone shop
            var map = Group("AreaMap", root);
            map.localPosition = new Vector3(2.45f, 1.55f, -d / 2 + 0.06f);
            Box(map, "Board", Vector3.zero, new Vector3(1.6f, 1.5f, 0.06f), C("#F4F1EA"), false);
            Box(map, "Title", new Vector3(0, 0.64f, 0.035f), new Vector3(1.6f, 0.22f, 0.01f), C("#2B2E33"), false);
            WorldText.Create(map, "TitleText", "주변지역안내도 · Area Map", new Vector3(0, 0.64f, 0.045f), new Vector2(1.5f, 0.18f), 36, Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            string[] blocks = { "#E8D7B5", "#D6E3C8", "#E2C8D6", "#C8D8E8", "#EEDFC0", "#D9D2E8" };
            for (int bx = 0; bx < 4; bx++)
            for (int by = 0; by < 3; by++)
                Box(map, "Block", new Vector3(-0.6f + bx * 0.4f, -0.45f + by * 0.36f, 0.034f), new Vector3(0.32f, 0.26f, 0.01f), C(blocks[rng.Next(blocks.Length)]), false);
            Box(map, "Street", new Vector3(0.2f, -0.08f, 0.04f), new Vector3(0.08f, 1.1f, 0.01f), Color.white, false);
            Box(map, "Here", new Vector3(0.2f, -0.55f, 0.046f), new Vector3(0.08f, 0.08f, 0.01f), C("#D8343B"), false);
            WorldText.Create(map, "HereText", "현위치", new Vector3(0.5f, -0.55f, 0.05f), new Vector2(0.4f, 0.12f), 30, C("#D8343B"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            Extra(r, "MapReader", HumanLook.Random(rng, Crowd.Street), new Vector3(2.45f, 0, -d / 2 + 0.95f), 180);

            // lockers along the south wall
            Lockers(root, new Vector3(10.2f, 0, -d / 2 + 0.3f), 0, 6, 4, C("#2F7FA8"));
            Extra(r, "LockerUser", HumanLook.Random(rng, Crowd.Street), new Vector3(9.6f, 0, -d / 2 + 1.25f), 180);

            // posters on the paid side: how to ride, and what's on in the city
            Poster(root, new Vector3(-w / 2 + 0.06f, 1.65f, 2.3f), 90, new Vector2(0.9f, 1.25f),
                "내리는 사람 먼저\n<size=24>Let people off first\n타실 때는 양옆으로</size>", "#FFFFFF", "#0F4A6B", 34);
            Poster(root, new Vector3(-w / 2 + 0.06f, 1.65f, 7.6f), 90, new Vector2(0.9f, 1.25f),
                "임산부 배려석\n<size=24>Seats for mothers-to-be\n비워 두세요</size>", "#F7E1E8", "#8C3B6B", 34);
            Poster(root, new Vector3(w / 2 - 0.06f, 1.65f, 6.6f), -90, new Vector2(0.9f, 1.25f),
                "서울 빛초롱 축제\n<size=24>Lantern Festival\n청계천 · 매일 17시</size>", "#1F2F55", "#FFD23F", 34);
            Poster(root, new Vector3(w / 2 - 0.06f, 1.65f, 8.1f), -90, new Vector2(0.9f, 1.25f),
                "에스컬레이터\n<size=24>두 줄 서기 · 걷거나 뛰지 마세요\nStand, don't walk</size>", "#FFFFFF", "#C8402E", 32);

            // the tactile strip from the stairs up to the gate that opens, and a kiosk in use
            Box(root, "TactileStrip", new Vector3(0, 0.004f, -4.6f), new Vector3(0.3f, 0.008f, 8.0f), yellow, false);
            Extra(r, "KioskUser", HumanLook.Random(rng, Crowd.Street), new Vector3(-5.3f, 0, -d / 2 + 1.25f), 180);

            // train times over the gate line, facing the people coming to the gates
            HangingSign2(r, "TrainTimes", new Vector3(-3.6f, 2.75f, 0.35f), 180, new Vector2(3.0f, 0.55f), C("#111214"),
                "<color=#FFB547>사당</color> 2분 후 · 회현 출발\n<color=#FFB547>진접</color> 5분 후 · 충무로 출발", C("#F2F2F2"), 34);

            // the emergency kit (gas masks) on the east wall, bins by the gates
            var kit = Group("EmergencyKit", root);
            kit.localPosition = new Vector3(w / 2 - 0.15f, 1.1f, -5.4f);
            kit.localRotation = Quaternion.Euler(0, -90, 0);
            Box(kit, "Cabinet", Vector3.zero, new Vector3(0.8f, 1.0f, 0.25f), C("#D8343B"), true);
            Glass(kit, "Door", new Vector3(0, 0, 0.13f), new Vector3(0.7f, 0.9f, 0.01f), 0, false);
            Sign(kit, "Label", new Vector3(0, 0.62f, 0.12f), new Vector2(0.8f, 0.2f), C("#FFFFFF"), "비상용품 · Emergency", C("#D8343B"), 0, 30, 0.01f);
            foreach (var (x, c) in new[] { (-6.6f, "#2F5FD0"), (-7.1f, "#1FA37A") })
            {
                Box(root, "Bin", new Vector3(x, 0.45f, -0.4f), new Vector3(0.42f, 0.9f, 0.42f), C(c), true);
                Box(root, "BinTop", new Vector3(x, 0.92f, -0.4f), new Vector3(0.44f, 0.04f, 0.44f), C("#2B2E33"), false);
            }

            // the paid side: Line 4 between Chungmuro and Dongjak, where the player changes for Line 9
            var strip = Group("RouteStrip", root);
            strip.localPosition = new Vector3(0, 1.55f, d / 2 - 0.06f);
            strip.localRotation = Quaternion.Euler(0, 180, 0);
            Box(strip, "Board", Vector3.zero, new Vector3(7.2f, 0.72f, 0.05f), C("#FFFFFF"), false);
            Box(strip, "Line", new Vector3(0, 0.06f, 0.03f), new Vector3(6.6f, 0.06f, 0.01f), line4, false);
            string[] stations = { "충무로", "명동", "회현", "서울역", "숙대입구", "삼각지", "신용산", "이촌", "동작" };
            for (int i = 0; i < stations.Length; i++)
            {
                float x = -3.2f + i * 0.8f;
                bool here = i == 1, change = i == stations.Length - 1;
                Box(strip, "Stop", new Vector3(x, 0.06f, 0.035f), new Vector3(here ? 0.16f : 0.11f, here ? 0.16f : 0.11f, 0.01f), here ? C("#D8343B") : change ? C("#BDB092") : Color.white, false);
                WorldText.Create(strip, "Name", stations[i] + (change ? " ⑨" : ""), new Vector3(x, -0.17f, 0.04f), new Vector2(0.78f, 0.16f), 34,
                    here ? C("#D8343B") : C("#1E1E1E"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            }
            // the strip faces south, so its local +x is the viewer's left: the west, the way to Sadang
            WorldText.Create(strip, "Title", "<color=#00A4E3>④</color> 4호선 · Line 4   ← 사당 방면", new Vector3(2.0f, 0.27f, 0.04f), new Vector2(3.0f, 0.15f), 34, C("#1E1E1E"), true,
                TextAnchor.MiddleLeft, Quaternion.Euler(0, 180, 0));
            Extra(r, "Commuter3", HumanLook.Random(rng, Crowd.Street), new Vector3(-10.0f, 0, 5.2f), 160, phone: true);
            Extra(r, "Commuter4", HumanLook.Random(rng, Crowd.Street), new Vector3(7.4f, 0, 6.2f), 20, phone: true);
            InteriorKit.Extinguisher(root, new Vector3(-w / 2 + 0.3f, 0, 7.5f), 90);
        }

        // ------------------------------------------------------------------- the platform

        static void DressPlatform(Room r, Transform p, float len, float pw, float h, Color line4)
        {
            var rng = new System.Random(5102);
            var bench = TextureKit.Surface("Seat_Perforated", TextureKit.Perforated(), C("#B7BEC6"), 0.6f, 0.5f);
            // benches against the back wall, facing the screen doors
            foreach (var bx in new[] { -1.5f, 11.5f })
                for (int i = 0; i < 3; i++)
                    InteriorKit.BeamSeat(p, new Vector3(bx + i * 0.55f, 0, -pw / 2 + 0.45f), bench, i == 0);
            // the station board with the stations either side
            var board = Group("StationBoard", p);
            board.localPosition = new Vector3(-9f, 1.75f, -pw / 2 + 0.05f);
            Box(board, "Board", Vector3.zero, new Vector3(4.4f, 0.8f, 0.05f), Color.white, false);
            Box(board, "Band", new Vector3(0, -0.28f, 0.03f), new Vector3(4.4f, 0.12f, 0.01f), line4, false);
            WorldText.Create(board, "Here", "명동 Myeong-dong 424", new Vector3(0, 0.08f, 0.035f), new Vector2(2.4f, 0.32f), 60, C("#1E1E1E"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(board, "Prev", "← 충무로", new Vector3(-1.75f, -0.28f, 0.04f), new Vector2(0.8f, 0.1f), 30, Color.white, true, TextAnchor.MiddleLeft, Quaternion.Euler(0, 180, 0));
            WorldText.Create(board, "Next", "회현 →", new Vector3(1.75f, -0.28f, 0.04f), new Vector2(0.8f, 0.1f), 30, Color.white, true, TextAnchor.MiddleRight, Quaternion.Euler(0, 180, 0));
            // ads
            LightBox(p, new Vector3(3.5f, 1.65f, -pw / 2 + 0.06f), 0, new Vector2(2.6f, 1.3f), C("#E26A9B"), "별빛화장품\n<size=34>K-Beauty · 명동</size>", Color.white, 64);
            LightBox(p, new Vector3(16.5f, 1.65f, -pw / 2 + 0.06f), 0, new Vector2(2.6f, 1.3f), C("#1FA37A"), "한강 자전거 대여\n<size=34>Han River bikes</size>", Color.white, 58);
            // the next train, over the platform
            var led = Sign(p, "TrainLed", new Vector3(4f, 2.7f, -0.6f), new Vector2(2.6f, 0.36f), C("#111214"),
                "<color=#FFB547>사당</color> 전역 출발 · Arriving", C("#F2F2F2"), 0, 34, 0.06f);
            Sign(p, "TrainLedBack", new Vector3(4f, 2.7f, -0.6f), new Vector2(2.6f, 0.36f), C("#111214"),
                "<color=#FFB547>사당</color> 전역 출발 · Arriving", C("#F2F2F2"), 180, 34, 0.06f);
            foreach (var x in new[] { -1.0f, 1.0f })
                Box(led, "Rod", new Vector3(x, (0.18f + (h - 2.7f)) / 2f, 0), new Vector3(0.015f, h - 2.7f - 0.18f, 0.015f), C("#9AA0A6"), false);
            // more people waiting, the emergency phone and an extinguisher on the back wall
            Extra(r, "Waiting5", HumanLook.SeniorPerson(rng, false), p.TransformPoint(new Vector3(-0.95f, 0, -pw / 2 + 0.385f)), 0, sitting: true);
            Sign(p, "SosPhone", new Vector3(-17.5f, 1.4f, -pw / 2 + 0.05f), new Vector2(0.4f, 0.5f), C("#D8343B"), "비상\n전화", Color.white, 0, 40, 0.1f);
            // posters on the back wall
            Poster(p, new Vector3(-13.2f, 1.65f, -pw / 2 + 0.06f), 0, new Vector2(1.0f, 1.3f), "지하철 예절\n<size=24>통화는 작게 · 가방은 앞으로\nQuiet calls, bags in front</size>", "#FFFFFF", "#0F4A6B", 32);
            Poster(p, new Vector3(-5.2f, 1.65f, -pw / 2 + 0.06f), 0, new Vector2(1.0f, 1.3f), "분실물 센터\n<size=24>Lost & Found\n고객안전실에 문의</size>", "#F2C230", "#1E1E1E", 32);
            Poster(p, new Vector3(8.0f + 3.0f, 1.65f, -pw / 2 + 0.06f), 0, new Vector2(1.0f, 1.3f), "한국어 말하기 대회\n<size=24>Korean Speech Contest\n12.12 · 중앙대</size>", "#1FA37A", "#FFFFFF", 30);
            InteriorKit.Extinguisher(p, new Vector3(-18.6f, 0, -pw / 2 + 0.3f), 0);
        }

        // ------------------------------------------------------------------- the ER

        static void DressER(Room r, float w, float d, float h)
        {
            var root = r.Root;
            var rng = new System.Random(7101);
            var red = C("#D8343B");
            var panel = TextureKit.Surface("Panel_ER", TextureKit.Plastic(), C("#EDEFF0"), 0.6f);

            // the nurses' station facing the bays, a crash cart at its end
            Slab(root, "NurseCounter", new Vector3(4.3f, 0.55f, 2.8f), new Vector3(0.6f, 1.1f, 1.8f), panel, true);
            Box(root, "NurseCounterTop", new Vector3(4.3f, 1.12f, 2.8f), new Vector3(0.7f, 0.04f, 1.9f), C("#2B2E33"), false);
            InteriorKit.Monitor(root, new Vector3(4.25f, 1.14f, 2.4f), -90, C("#DDEBFF"));
            InteriorKit.Monitor(root, new Vector3(4.25f, 1.14f, 3.2f), -90, C("#3DDC84"));
            HangingSign2(r, "NurseSign", new Vector3(4.3f, 2.45f, 2.8f), 90, new Vector2(1.6f, 0.36f), C("#16324A"), "간호사실 · Nurses", Color.white, 40);
            var cart = Group("CrashCart", root);
            cart.localPosition = new Vector3(5.3f, 0, 4.1f);
            Box(cart, "Body", new Vector3(0, 0.5f, 0), new Vector3(0.55f, 0.9f, 0.45f), red, true);
            for (int i = 0; i < 4; i++)
                Box(cart, "Drawer", new Vector3(0, 0.22f + i * 0.21f, 0.228f), new Vector3(0.5f, 0.17f, 0.01f), C("#B82E33"), false);
            Box(cart, "Monitor", new Vector3(0, 1.08f, -0.05f), new Vector3(0.34f, 0.26f, 0.12f), C("#2B2E33"), false);

            // an ambulance has just come in: the stretcher by the entrance, the crew beside it
            Stretcher(root, new Vector3(2.9f, 0, -3.6f), 0);
            for (int i = 0; i < 2; i++)
            {
                var look = HumanLook.Officer(i == 1, 8 + i);
                look.Outer = OuterKind.UniformVest;
                look.OuterColor = C("#E85D1C");
                Extra(r, $"Paramedic{i}", look, new Vector3(2.05f, 0, -4.3f + i * 1.3f), 80 + i * 20, talking: i == 0);
            }
            Extra(r, "Guard", HumanLook.Guard(), new Vector3(-1.7f, 0, -4.9f), 60);

            // along the west wall: wheelchairs, a vending machine, water, the health TV and the triage poster
            Wheelchair(root, new Vector3(-w / 2 + 0.5f, 0, -4.4f), 90);
            Wheelchair(root, new Vector3(-w / 2 + 0.5f, 0, -3.6f), 90);
            VendingMachine(root, new Vector3(-w / 2 + 0.42f, 0, -1.6f), 90, C("#2F5FD0"), "음료 · Drinks", rng);
            InteriorKit.WaterDispenser(root, new Vector3(-w / 2 + 0.3f, 0, -0.3f), 90);
            InteriorKit.WallTv(root, new Vector3(-w / 2 + 0.05f, 2.0f, 2.6f), 90, C("#16324A"), "건강 정보\n<size=24>겨울철 독감 예방 접종</size>",
                new[] { "건강 정보\n<size=24>손을 자주 씻으세요\nWash your hands</size>", "응급실 안내\n<size=24>위급도 순으로 진료합니다</size>" });
            Poster(root, new Vector3(-w / 2 + 0.05f, 1.55f, 4.4f), 90, new Vector2(1.0f, 0.9f),
                "응급환자 분류 KTAS\n<size=22>1 소생 · 2 긴급\n3 응급 · 4 준응급\n5 비응급</size>", "#FFFFFF", "#A3301E", 26);
            Poster(root, new Vector3(-3.6f, 1.55f, -d / 2 + 0.05f), 0, new Vector2(1.1f, 0.8f),
                "마스크 착용 · 손 소독\n<size=22>Wear a mask\nSanitize your hands</size>", "#FFFFFF", "#16324A", 26);

            // guide lines on the floor from the entrance: red to registration, blue to triage
            FloorLine(root, new Vector3(0, 0, -5.4f), new Vector3(0, 0, -0.8f), red, "접수 ↑");
            FloorLine(root, new Vector3(0.4f, 0, -0.9f), new Vector3(4.2f, 0, -0.9f), C("#2F5FD0"), "예진실 →");

            // people waiting by the bays: family at a curtain
            Extra(r, "Family0", HumanLook.Ajumma(rng), new Vector3(6.5f, 0, 3.4f), 90, talking: true);
            Extra(r, "Family1", HumanLook.Ajeossi(rng), new Vector3(6.4f, 0, -0.7f), 80, phone: true);
        }
    }
}
