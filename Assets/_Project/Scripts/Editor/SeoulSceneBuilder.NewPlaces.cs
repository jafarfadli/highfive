using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.Quest;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// The places added for the ITB x CAU build (2026-11-25): the arrival at the airport (immigration booths, the
    /// baggage carousel, customs, and the arrivals hall's bus ticket counter), 명동역 (the concourse with its
    /// convenience store, the gate line and the 고객안전실 window, and the platform with its train), 명동길 again
    /// for the 119 call, and a hospital emergency room at night. Research: references/RESEARCH-NEW-SCENARIOS.md.
    /// </summary>
    public static partial class SeoulSceneBuilder
    {
        /// <summary>
        /// Lists where along 명동길 a new entrance would find a facade (both sides, every 2 m), with the distance to the
        /// wall: a quick way to place one. Run with the Hub open.
        /// </summary>
        [MenuItem("Seoul First Week/Tools/Scan Entrance Spots")]
        public static string ScanEntranceSpots()
        {
            var sb = new System.Text.StringBuilder();
            for (float z = -64f; z <= 70f; z += 2f)
            {
                var street = StreetPoint(z);
                var fwd = StreetDir(z);
                street.y = GroundY(street) + 1.2f;
                foreach (var side in new[] { -1, 1 })
                {
                    var dir = side < 0 ? Vector3.Cross(fwd, Vector3.up) : Vector3.Cross(Vector3.up, fwd);
                    bool ok = FacadeHit(street, dir, 30f, out var hit);
                    sb.Append($"z={z,4} side={side,2}: {(ok ? $"wall at {hit.distance:F1} m" : "-")}\n");
                }
            }
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        // ------------------------------------------------------------------- shared pieces

        /// <summary>
        /// A free-standing service counter anywhere in a room (a store till, a station office window, a ticket
        /// counter). <paramref name="yaw"/> is the way the customer faces when they stand at it; the staff member
        /// sits on the far side. Adds the counter's trigger, the customer's spot and the staff member, like a window
        /// of <see cref="Counters"/>.
        /// </summary>
        static Transform ServiceCounter(Room r, int number, Vector3 pos, float yaw, float length, Material front, Material top, string staffId, HumanLook look,
            Color accent, bool glass = true)
        {
            var g = Group($"ServiceCounter_{number}", r.Root);
            g.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var dark = Mats.Lit(C("#3A3D42"), 0.35f);
            // a standing-height ledge in front of a desk, the customer on the -Z side, the staff on +Z
            Slab(g, "Front", new Vector3(0, 0.55f, 0), new Vector3(length, 1.1f, 0.08f), front);
            Slab(g, "Ledge", new Vector3(0, 1.11f, 0.12f), new Vector3(length + 0.08f, 0.04f, 0.36f), top);
            Slab(g, "Desk", new Vector3(0, InteriorKit.DeskTop - 0.02f, 0.55f), new Vector3(length, 0.04f, 0.8f), top);
            foreach (var sx in new[] { -1f, 1f })
                Slab(g, "End", new Vector3(sx * (length / 2f - 0.03f), 0.55f, 0.45f), new Vector3(0.06f, 1.1f, 0.95f), front);
            Box(g, "Band", new Vector3(0, 0.95f, -0.045f), new Vector3(length, 0.06f, 0.01f), accent, false);
            Box(g, "Kick", new Vector3(0, 0.05f, -0.045f), new Vector3(length, 0.1f, 0.02f), C("#2B2E33"), false);
            if (glass)
            {
                Glass(g, "ScreenL", new Vector3(-0.45f, 1.5f, 0.12f), new Vector3(0.5f, 0.72f, 0.02f));
                Glass(g, "ScreenR", new Vector3(0.45f, 1.5f, 0.12f), new Vector3(0.5f, 0.72f, 0.02f));
                Box(g, "ScreenRail", new Vector3(0, 1.87f, 0.12f), new Vector3(1.45f, 0.03f, 0.05f), C("#9AA0A6"), false);
            }
            InteriorKit.CardTerminal(g, g.TransformPoint(new Vector3(-0.35f, 1.13f, 0.16f)), yaw + 180);

            InteriorKit.OfficeChair(r.Root, g.TransformPoint(new Vector3(0, 0, 1.25f)), yaw + 180, C("#2B2E33"));
            var seat = Group($"StaffSeat_{number}", r.Root);
            seat.SetPositionAndRotation(g.TransformPoint(new Vector3(0, 0, 1.32f)), Quaternion.Euler(0, yaw + 180, 0));
            InteriorKit.Monitor(r.Root, g.TransformPoint(new Vector3(0.38f, InteriorKit.DeskTop, 0.72f)), yaw + 200, C("#9CC7FF"));
            Staff(r, staffId, seat, look);

            var counterGo = new GameObject($"Counter_{number}");
            counterGo.transform.SetParent(g, false);
            counterGo.transform.localPosition = new Vector3(0, 1.0f, -0.3f);
            var box = counterGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 1.4f, 0.7f);
            var counter = counterGo.AddComponent<CounterSpot>();
            counter.counterNumber = number;
            r.Counters.Add(counter);
            var spot = Group($"CounterSpot_{number}", r.Root);
            spot.SetPositionAndRotation(g.TransformPoint(new Vector3(0, 0, -0.55f)), Quaternion.Euler(0, yaw, 0));
            r.CounterSpots.Add(spot);
            var mySeat = Group($"Seat_counter_{number}", r.Root);
            mySeat.SetPositionAndRotation(g.TransformPoint(new Vector3(0, 0.05f, -0.5f)), Quaternion.Euler(0, yaw, 0));
            r.Anchors.Add(mySeat);
            return g;
        }

        /// <summary>A place a `go` step sends the player to (see <see cref="Waypoint"/>).</summary>
        static Waypoint MakeWaypoint(Transform parent, string id, Vector3 pos, Vector3 size, string promptKey, string label, bool walkThrough = false)
        {
            var go = new GameObject($"Waypoint_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            // walk-through boxes stay out of the interaction ray, so they never hide what is behind them
            if (walkThrough) go.layer = 2;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            var wp = go.AddComponent<Waypoint>();
            wp.stationId = id;
            wp.promptKey = promptKey;
            wp.label = label;
            wp.walkThrough = walkThrough;
            return wp;
        }

        static Transform Anchor(Room r, string name, Vector3 pos, float yaw)
        {
            var a = Group(name, r.Root);
            a.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            r.Anchors.Add(a);
            return a;
        }

        /// <summary>A plain box room (floor, walls, ceiling, lights) for a second space in the same scene.</summary>
        static void BoxRoom(Transform parent, string name, Vector3 center, float w, float d, float h, Material floor, Material wall, Material ceiling, Color light)
        {
            var g = Group(name, parent);
            g.position = center;
            Slab(g, "Floor", new Vector3(0, -0.05f, 0), new Vector3(w, 0.1f, d), floor);
            Slab(g, "Ceiling", new Vector3(0, h + 0.05f, 0), new Vector3(w, 0.1f, d), ceiling, true, false);
            Slab(g, "WallN", new Vector3(0, h / 2, d / 2 + 0.1f), new Vector3(w + 0.4f, h, 0.2f), wall);
            Slab(g, "WallS", new Vector3(0, h / 2, -d / 2 - 0.1f), new Vector3(w + 0.4f, h, 0.2f), wall);
            Slab(g, "WallE", new Vector3(w / 2 + 0.1f, h / 2, 0), new Vector3(0.2f, h, d), wall);
            Slab(g, "WallW", new Vector3(-w / 2 - 0.1f, h / 2, 0), new Vector3(0.2f, h, d), wall);
            HallLights(g, Vector3.zero, w, d, h, light);
        }

        /// <summary>
        /// Ceiling lights for a big or high room: a grid about <paramref name="spacing"/> apart that lights the floor
        /// as brightly as the clinic's lights do under its 2.7 m ceiling (point light falls off with the square of the
        /// distance, so the intensity grows with the square of the drop to the floor) and reaches the walls. Under a
        /// high ceiling they hang 1.2 m down, so the ceiling doesn't flare around them. The renderer is Forward+, so a
        /// big hall may hold a dozen lights; the clinic, immigration office and bank keep their six.
        /// </summary>
        static void HallLights(Transform parent, Vector3 center, float w, float d, float h, Color color, float spacing = 8f)
        {
            int nx = Mathf.Max(2, Mathf.CeilToInt(w / spacing));
            int nz = Mathf.Max(1, Mathf.CeilToInt(d / spacing));
            float y = h > 3.6f ? h - 1.2f : h - 0.4f;
            float intensity = 1.05f * Mathf.Max(1f, y / 2.3f * (y / 2.3f));
            float cell = Mathf.Max(w / nx, d / nz);
            float range = Mathf.Max(8f, 1.3f * Mathf.Sqrt(y * y + cell * cell));
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                PointLight(parent, center + new Vector3(-w / 2 + w * (i + 0.5f) / nx, y, -d / 2 + d * (j + 0.5f) / nz), range, intensity, color);
        }

        /// <summary>An invisible wall along a stanchion line (the belts alone would let the player through).</summary>
        static void Fence(Transform parent, Vector3 from, Vector3 to, float height = 1.2f)
        {
            var go = new GameObject("Fence") { layer = 2 };
            go.transform.SetParent(parent, false);
            go.transform.position = (from + to) / 2f + Vector3.up * height / 2f;
            var dir = to - from;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(dir);
            go.AddComponent<BoxCollider>().size = new Vector3(0.12f, height, dir.magnitude);
        }

        static HumanLook Traveller(System.Random rng, bool female) => rng.Next(3) == 0 ? HumanLook.Student(rng, female) : HumanLook.Abroad(rng, female);

        /// <summary>A hard-shell suitcase with its handle up (in front of a standing traveller).</summary>
        static Transform Suitcase(Transform parent, Vector3 pos, float yaw, Color shell)
        {
            var g = Group("Suitcase", parent);
            g.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            Box(g, "Shell", new Vector3(0, 0.36f, 0), new Vector3(0.42f, 0.62f, 0.26f), shell, false);
            Box(g, "Rib", new Vector3(0, 0.36f, 0.133f), new Vector3(0.36f, 0.56f, 0.01f), Color.Lerp(shell, Color.black, 0.15f), false);
            foreach (var sx in new[] { -0.12f, 0.12f })
                Cylinder(g, "Rod", new Vector3(sx, 0.86f, -0.1f), new Vector3(0.02f, 0.2f, 0.02f), C("#2B2E33"));
            Box(g, "Grip", new Vector3(0, 1.06f, -0.1f), new Vector3(0.28f, 0.03f, 0.04f), C("#2B2E33"), false);
            foreach (var sx in new[] { -0.16f, 0.16f })
                Cylinder(g, "Wheel", new Vector3(sx, 0.03f, -0.08f), new Vector3(0.05f, 0.03f, 0.05f), C("#1E1E1E"));
            return g;
        }

        // ------------------------------------------------------------------- airport

        /// <summary>
        /// The arrival: a corridor from the gate opens into the immigration hall. Foreigners queue on the red line,
        /// Koreans on the blue (stanchion lanes, a guide in a vest), and a row of raised booths with glass fronts
        /// and fingerprint scanners closes the hall; booth 3 is the player's. The baggage carousel stands in the
        /// west wing with the two customs exits (신고없음 green, 신고있음 red) beside it. Part 2 plays in the
        /// arrivals hall, built as a second room east of the hall: the bus ticket counter, the information desk,
        /// an exchange counter and the doors out to the numbered bus stops.
        /// </summary>
        [MenuItem("Seoul First Week/Build Airport")]
        public static void BuildAirport()
        {
            var scene = NewScene("Airport");
            BeginMaterials();
            float w = 34, d = 24, h = 6f;
            var red = C("#D8343B");
            var blue = C("#2A6FDB");
            var panel = TextureKit.Surface("Panel_Airport", TextureKit.Plastic(), C("#E9EBEE"), 0.6f);
            var steel = TextureKit.Surface("Steel_Airport", TextureKit.Metal(), C("#C3C8CE"), 0.8f, 0.85f, true);
            var floor = TextureKit.Surface("Floor_Airport", TextureKit.FloorGranite(), C("#D9D6CF"));
            var ceiling = TextureKit.Surface("Ceiling_Airport", TextureKit.CeilingTiles(), C("#F4F5F6"), 0.6f);
            var r = Shell(new RoomSpec
            {
                Name = "Airport", W = w, D = d, H = h, Style = CounterStyle.High, Enclosed = true,
                PassageSign = "탑승구 · Gates", Light = C("#F4F7FF"), Wall = C("#EEF0F2"), Accent = blue,
                CounterX0 = -10.5f, CounterX1 = 10.5f, WindowX = new[] { -8.75f, -5.25f, -1.75f, 1.75f, 5.25f, 8.75f },
                Floor = floor, Ceiling = ceiling, CounterFront = panel, Cabinet = panel, Partition = panel,
            });
            var root = r.Root;
            var rng = new System.Random(41);
            float F = r.CounterFrontZ;

            // lights: long lines across the high ceiling
            var lit = Mats.Emissive(C("#FFFFFF"), 1.6f);
            for (float z = -d / 2 + 2f; z < d / 2; z += 4f)
                InteriorKit.LinearLight(root, new Vector3(0, 0, z), h, w - 4f, 90, lit);

            // the booths: raised frames over each window, numbered, red for foreigners (1–3) and blue for Koreans (4–6)
            for (int i = 0; i < r.WindowX.Length; i++)
            {
                float x = r.WindowX[i];
                var c = i < 3 ? red : blue;
                Slab(root, "BoothFrameL", new Vector3(x - 0.86f, 1.6f, F + 0.6f), new Vector3(0.1f, 3.2f, 1.3f), steel, false);
                Slab(root, "BoothRoof", new Vector3(x, 3.25f, F + 0.6f), new Vector3(1.82f, 0.12f, 1.3f), steel, false);
                Sign(root, "BoothNumber", new Vector3(x, 2.85f, F - 0.06f), new Vector2(0.5f, 0.42f), c, $"{i + 1}", Color.white, 0, 80, 0.06f)
                    .localRotation = Quaternion.Euler(0, 180, 0);
                Box(root, "Scanner", new Vector3(x + 0.38f, 1.12f, F + 0.18f), new Vector3(0.24f, 0.06f, 0.16f), C("#2B2E33"), false);
                Emissive(root, "ScannerGlass", new Vector3(x + 0.38f, 1.153f, F + 0.18f), new Vector3(0.16f, 0.006f, 0.09f), C("#39FF88"), 1.5f);
                // the camera that takes the face photo, on a short arm towards the traveller
                Cylinder(root, "CameraArm", new Vector3(x - 0.35f, 1.5f, F + 0.15f), new Vector3(0.03f, 0.3f, 0.03f), C("#2B2E33"));
                Box(root, "FaceCamera", new Vector3(x - 0.35f, 1.82f, F + 0.12f), new Vector3(0.12f, 0.08f, 0.08f), C("#1E1E1E"), false);
            }
            CounterStaff(r, 1, "booth1", HumanLook.Officer(false, 0));
            CounterStaff(r, 2, "booth2", HumanLook.Officer(true, 1));
            CounterStaff(r, 3, "inspector", HumanLook.Officer(false, 2));
            CounterStaff(r, 4, "booth4", HumanLook.Officer(true, 3));
            CounterStaff(r, 5, "booth5", HumanLook.Officer(false, 4));
            CounterStaff(r, 6, "booth6", HumanLook.Officer(true, 5));
            Sign(root, "HallSign", new Vector3(0, 4.5f, d / 2 - 0.04f), new Vector2(9f, 0.9f), C("#1F2F55"), "입국심사 · Immigration", Color.white, 180, 90, 0.06f);

            // the queue lanes: stanchions, floor lines in red and blue, overhead signs
            float laneZ0 = -d / 2 + 4.6f, laneZ1 = F - 1.4f;
            foreach (var (x0, x1, c, label, id) in new[]
            {
                (-10.4f, -0.6f, red, "외국인 Foreigners", "lane_foreign"),
                (0.6f, 10.4f, blue, "내국인 Koreans", "lane_korean"),
            })
            {
                InteriorKit.Stanchions(root, new Vector3(x0, 0, laneZ0), new Vector3(x0, 0, laneZ1), 8, c);
                InteriorKit.Stanchions(root, new Vector3(x1, 0, laneZ0), new Vector3(x1, 0, laneZ1), 8, c);
                Fence(root, new Vector3(x0, 0, laneZ0), new Vector3(x0, 0, laneZ1));
                Fence(root, new Vector3(x1, 0, laneZ0), new Vector3(x1, 0, laneZ1));
                float cx = (x0 + x1) / 2f;
                Box(root, "LaneLine", new Vector3(cx, 0.004f, laneZ0 - 0.2f), new Vector3(x1 - x0 - 0.4f, 0.008f, 0.18f), c, false);
                Box(root, "LaneArrow", new Vector3(cx, 0.004f, laneZ0 + 0.8f), new Vector3(0.18f, 0.008f, 1.2f), c, false);
                HangingSign(r, "LaneSign", new Vector3(cx, 3.6f, laneZ0 - 0.4f), 180, new Vector2(4.4f, 0.7f), c, label, Color.white, 64);
                var wp = MakeWaypoint(root, id, new Vector3(cx, 1f, laneZ0 + 0.3f), new Vector3(x1 - x0 - 0.6f, 2f, 0.8f), "wp_lane", label, true);
            }
            // the only way to the booths is through a lane: belts close the gap between them and the sides of the hall
            foreach (var (a, b) in new[] { (-w / 2, -10.4f), (-0.6f, 0.6f), (10.4f, w / 2) })
            {
                InteriorKit.Stanchions(root, new Vector3(a, 0, laneZ0), new Vector3(b, 0, laneZ0), Mathf.Max(2, Mathf.RoundToInt((b - a) / 1.4f) + 1), C("#3A3D42"));
                Fence(root, new Vector3(a, 0, laneZ0), new Vector3(b, 0, laneZ0));
            }
            // a few travellers queuing, suitcases in front of them, out of the player's walk up booth 3's lane
            for (int i = 0; i < 3; i++)
            {
                var look = Traveller(rng, i % 2 == 0);
                var pos = new Vector3(-7.2f, 0, F - 2.4f - i * 1.4f);
                Extra(r, $"Queue_F{i}", look, pos, 0);
                Suitcase(root, pos + new Vector3(0.45f, 0, 0.2f), 0, C(new[] { "#3B5B8C", "#B83B3B", "#3A3D42" }[i]));
            }
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(5.6f + (i % 2) * 1.6f, 0, F - 2.2f - i * 1.3f);
                Extra(r, $"Queue_K{i}", HumanLook.Random(rng, Crowd.Street), pos, 0, phone: i == 1);
                Suitcase(root, pos + new Vector3(-0.45f, 0, 0.2f), 0, C(new[] { "#2B2E33", "#C9A227", "#6B7A8C", "#8C3B6B" }[i]));
            }
            // the guide at the lane entrance, in a vest
            var guideLook = HumanLook.Officer(true, 7);
            guideLook.Outer = OuterKind.UniformVest;
            guideLook.OuterColor = C("#F2C230");
            var guideSpot = Group("GuideSpot", root);
            guideSpot.SetPositionAndRotation(new Vector3(0, 0, laneZ0 + 0.2f), Quaternion.Euler(0, 180, 0));
            Staff(r, "guide", guideSpot, guideLook, false);
            InteriorKit.Easel(root, new Vector3(-1.6f, 0, laneZ0 - 1.4f), 180, "입국 순서 · Arrival", new[]
            {
                "입국심사 · Immigration", "수하물 · Baggage", "세관 · Customs", "입국장 · Arrivals",
            }, new[] { red, blue, C("#1FA37A"), C("#E58B2B") });

            // the west wing: baggage carousel 5, its board, and the customs exits
            float cx0 = -w / 2 + 4.2f;
            var belt = TextureKit.Surface("Belt_Rubber", TextureKit.Rubber(), C("#3A3D42"), 0.4f);
            var carousel = Group("Carousel", root);
            carousel.position = new Vector3(cx0, 0, 1.5f);
            Slab(carousel, "Island", new Vector3(0, 0.25f, 0), new Vector3(1.2f, 0.5f, 7.0f), steel);
            Slab(carousel, "Belt", new Vector3(0, 0.42f, 0), new Vector3(2.6f, 0.08f, 8.4f), belt);
            Slab(carousel, "Skirt", new Vector3(0, 0.2f, 0), new Vector3(2.7f, 0.4f, 8.5f), steel);
            var bags = new[] { "#3B5B8C", "#2B2E33", "#B83B3B", "#6B7A8C", "#3A6B4F", "#8C3B6B" };
            for (int i = 0; i < 9; i++)
            {
                float z = -3.6f + i * 0.9f;
                if (i == 4) continue;
                var bag = Box(carousel, "Bag", new Vector3(i % 2 == 0 ? -0.75f : 0.75f, 0.6f, z), new Vector3(0.45f, 0.26f, 0.65f), C(bags[i % bags.Length]), false, rng.Next(-20, 20));
            }
            // the player's suitcase: bright yellow, with a luggage tag
            var mine = Group("MySuitcase", carousel);
            mine.localPosition = new Vector3(-0.75f, 0.6f, 0f);
            Box(mine, "Shell", Vector3.zero, new Vector3(0.48f, 0.28f, 0.68f), C("#F2C230"), false);
            Box(mine, "Tag", new Vector3(0.26f, 0.05f, 0.2f), new Vector3(0.02f, 0.12f, 0.08f), C("#FFFFFF"), false);
            var bagWp = MakeWaypoint(root, "carousel", carousel.TransformPoint(new Vector3(-0.75f, 0.9f, 0f)), new Vector3(1.4f, 1.6f, 1.4f), "wp_bag", "");
            bagWp.opens = new[] { mine.gameObject };
            Sign(root, "CarouselBoard", new Vector3(cx0 + 0.2f, 3.0f, 1.5f), new Vector2(2.4f, 1.1f), C("#1F2F55"), "5\n<size=34>하노이 HANOI</size>", C("#FFD23F"), 90, 90, 0.08f);
            HangingSign(r, "BaggageSign", new Vector3(cx0 + 2.8f, 4.3f, 1.5f), 90, new Vector2(5f, 0.7f), C("#1F2F55"), "수하물 찾는 곳 · Baggage Claim", Color.white, 58);
            for (int i = 0; i < 2; i++)
                Extra(r, $"BagWaiter{i}", Traveller(rng, i == 0), new Vector3(cx0 + 1.9f, 0, -1.2f + i * 2.4f), -90);

            // customs: two exits in the west wall, green for nothing to declare, red for goods to declare
            foreach (var (z, c, ko, en, id) in new[]
            {
                (-4.3f, C("#1FA37A"), "신고없음", "Nothing to Declare", "customs_none"),
                (-6.6f, red, "신고있음", "Goods to Declare", "customs_declare"),
            })
            {
                var x = -w / 2 + 0.05f;
                Slab(root, "ExitFrame", new Vector3(x + 0.05f, 1.3f, z), new Vector3(0.1f, 2.6f, 2.0f), steel, false);
                Box(root, "ExitPanel", new Vector3(x + 0.08f, 1.25f, z), new Vector3(0.04f, 2.4f, 1.8f), C("#C9D3DC"), false);
                Sign(root, "ExitSign", new Vector3(x + 0.12f, 3.1f, z), new Vector2(2.0f, 0.7f), c, $"{ko}\n<size=30>{en}</size>", Color.white, 90, 60, 0.06f);
                Box(root, "ExitLine", new Vector3(x + 1.6f, 0.004f, z), new Vector3(0.18f, 0.008f, 1.8f), c, false);
                MakeWaypoint(root, id, new Vector3(x + 0.9f, 1f, z), new Vector3(1.4f, 2f, 1.8f), "wp_walk", ko, true);
            }
            var customsSpot = Group("CustomsSpot", root);
            customsSpot.SetPositionAndRotation(new Vector3(-w / 2 + 2.4f, 0, -5.45f), Quaternion.Euler(0, 90, 0));
            var customsLook = HumanLook.Officer(false, 6);
            Staff(r, "customs", customsSpot, customsLook, false);
            // where a traveller who declares something stands to talk to the customs officer
            Anchor(r, "Seat_customs", new Vector3(-w / 2 + 3.6f, 0.05f, -5.45f), -90);
            Poster(root, new Vector3(-w / 2 + 0.06f, 1.7f, -2.2f), 90, new Vector2(1.3f, 0.9f),
                "꼭 신고하세요\n<size=24>육류 · 과일 · 유제품\nMeat · Fruit · Dairy</size>", "#FFFFFF", "#C8402E", 36);

            DressAirport(r, w, d, h, F);

            // ---- part 2: the arrivals hall, a second room to the east
            var hall = new Vector3(70f, 0, 0);
            float hw = 30, hd = 18, hh = 5.5f;
            var hallFloor = TextureKit.Surface("Floor_Arrivals", TextureKit.FloorGranite(), C("#E3E0DA"));
            BoxRoom(root, "ArrivalsHall", hall, hw, hd, hh, hallFloor, TextureKit.Surface("Wall_Arrivals", TextureKit.WallPaint(), C("#F1F2F3")), ceiling, C("#FFF4E6"));
            for (float z = -hd / 2 + 2f; z < hd / 2; z += 4f)
                InteriorKit.LinearLight(root, hall + new Vector3(0, 0, z), hh, hw - 4f, 90, lit);
            Sign(root, "ArrivalsSign", hall + new Vector3(0, 4.2f, hd / 2 - 0.05f), new Vector2(8f, 0.8f), C("#1F2F55"), "입국장 · Arrivals Hall", Color.white, 180, 80, 0.06f);
            // the arrivals board over the hall, columns, planters and luggage carts
            Sign(root, "ArrivalsBoard", hall + new Vector3(-1f, 3.4f, -hd / 2 + 0.06f), new Vector2(6.4f, 1.6f), C("#14171C"),
                "<color=#FFD23F>도착 Arrivals</color>\n<size=30>하노이 HANOI   도착 Landed   5\n자카르타 JAKARTA   도착 Landed   7\n방콕 BANGKOK   지연 Delayed   -</size>", Color.white, 0, 54, 0.1f);
            foreach (var x in new[] { -12.5f, 12.5f })
                Slab(root, "Column", hall + new Vector3(x, hh / 2, 1.5f), new Vector3(0.9f, hh, 0.9f), panel);
            Plant(root, hall + new Vector3(-hw / 2 + 0.8f, 0, hd / 2 - 0.8f), 1.2f);
            Plant(root, hall + new Vector3(hw / 2 - 0.8f, 0, -hd / 2 + 0.8f), 1.2f);
            for (int i = 0; i < 3; i++)
            {
                var cart = Group("LuggageCart", root);
                cart.SetPositionAndRotation(hall + new Vector3(-12f + i * 0.5f, 0, 5.5f), Quaternion.Euler(0, 90, 0));
                Box(cart, "Base", new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.05f, 1.0f), C("#9AA0A6"), false);
                Box(cart, "Back", new Vector3(0, 0.7f, 0.48f), new Vector3(0.6f, 0.9f, 0.04f), C("#9AA0A6"), false);
                Box(cart, "Handle", new Vector3(0, 1.15f, 0.5f), new Vector3(0.62f, 0.05f, 0.05f), C("#2A6FDB"), false);
            }
            for (int i = 0; i < 3; i++)
            {
                var pos = hall + new Vector3(-2f + i * 3.2f, 0, -0.5f - (i % 2) * 2f);
                Extra(r, $"Traveller{i}", Traveller(rng, i == 1), pos, rng.Next(0, 360), phone: i == 2);
                Suitcase(root, pos + new Vector3(0.45f, 0, 0.1f), 0, C(new[] { "#3B5B8C", "#B83B3B", "#3A3D42" }[i]));
            }

            // the bus ticket counter on the north wall, under its sign
            var busCounter = ServiceCounter(r, 9, hall + new Vector3(-5f, 0, hd / 2 - 2.6f), 0, 2.4f, panel, r.TopMat, "busclerk", HumanLook.Office(new System.Random(91), true), blue);
            // the player's goal here, so the biggest sign on the wall
            Sign(root, "BusSign", hall + new Vector3(-5f, 3.25f, hd / 2 - 0.05f), new Vector2(5.2f, 1.15f), blue, "공항버스 매표소\n<size=36>Airport Bus Tickets</size>", Color.white, 180, 76, 0.06f);
            InteriorKit.RateBoard(root, hall + new Vector3(-1.6f, 1.9f, hd / 2 - 0.05f), 180, new Vector2(2.2f, 1.4f), "공항버스 · Airport Bus",
                new[] { "행선지", "요금" }, new[] { new[] { "명동 Myeong-dong", "17,000" }, new[] { "서울역 Seoul Stn.", "17,000" }, new[] { "강남 Gangnam", "17,000" } }, blue);
            // the information desk and the game's own bank's exchange counter
            var info = InteriorKit.InfoDesk(root, hall + new Vector3(6f, 0, 2f), 180, r.TopMat, panel, C("#1F2F55"), "안내 · Information");
            Extra(r, "InfoStaff", HumanLook.Office(new System.Random(92), true), info.TransformPoint(new Vector3(0, 0, -0.42f)), 180, talking: true);
            Sign(root, "Exchange", hall + new Vector3(10.5f, 2.6f, hd / 2 - 0.05f), new Vector2(3.6f, 0.7f), C("#5BAA2E"), "새싹은행 환전 · EXCHANGE", Color.white, 180, 50, 0.06f);
            Slab(root, "ExchangeCounter", hall + new Vector3(10.5f, 0.55f, hd / 2 - 1.2f), new Vector3(3.2f, 1.1f, 0.6f), panel);
            // greeters behind a rail, benches
            InteriorKit.Stanchions(root, hall + new Vector3(-hw / 2 + 2f, 0, -1.5f), hall + new Vector3(-hw / 2 + 9f, 0, -1.5f), 6, C("#1F2F55"));
            for (int i = 0; i < 4; i++)
                Extra(r, $"Greeter{i}", HumanLook.Random(rng, Crowd.Street), hall + new Vector3(-hw / 2 + 2.6f + i * 1.7f, 0, -2.2f), 180 + rng.Next(-20, 20), phone: i == 2);
            var bench = TextureKit.Surface("Seat_Perforated", TextureKit.Perforated(), C("#B7BEC6"), 0.6f, 0.5f);
            for (int i = 0; i < 3; i++)
            {
                var b = InteriorKit.BeamSeat(root, hall + new Vector3(4f + i * 0.62f, 0, -4f), bench, i == 0);
                b.rotation = Quaternion.Euler(0, 180, 0);
            }
            // more arrivals, kept off the player's way from the doors to counter 9 and out to stop 5: two people
            // queuing at the exchange counter, a pair talking by the east wall, someone asking at the information desk
            Extra(r, "ExchangeCustomer", Traveller(rng, true), hall + new Vector3(10.5f, 0, 6.5f), 0);
            Suitcase(root, hall + new Vector3(11.15f, 0, 6.3f), 90, C("#2F6B5A"));
            Extra(r, "ExchangeNext", Traveller(rng, false), hall + new Vector3(10.4f, 0, 5.2f), 0, phone: true);
            Suitcase(root, hall + new Vector3(9.75f, 0, 5.3f), 90, C("#C9A227"));
            Extra(r, "ChatA", Traveller(rng, true), hall + new Vector3(11.4f, 0, -5.4f), 61, talking: true);
            Extra(r, "ChatB", Traveller(rng, false), hall + new Vector3(12.3f, 0, -4.9f), 241);
            Suitcase(root, hall + new Vector3(10.9f, 0, -6.0f), 30, C("#7A3B8C"));
            Extra(r, "InfoVisitor", Traveller(rng, false), hall + new Vector3(6f, 0, 0.95f), 0);
            // the doors out to the bus stops, numbered
            foreach (var (x, n) in new[] { (-6f, 5), (4f, 3) })
            {
                var doorPos = hall + new Vector3(x, 0, -hd / 2);
                Glass(root, "StopDoor", doorPos + new Vector3(0, 1.3f, 0.06f), new Vector3(2.4f, 2.6f, 0.04f), 0, false);
                Sign(root, "StopSign", doorPos + new Vector3(0, 3.0f, 0.1f), new Vector2(2.6f, 0.7f), C("#1F2F55"), $"{n}번 승차장\n<size=30>Bus Stop {n}</size>", C("#FFD23F"), 0, 60, 0.06f);
                Emissive(root, "OutsideSky", doorPos + new Vector3(0, 1.75f, 0.01f), new Vector3(2.4f, 1.7f, 0.02f), C("#A9CBE6"), 0.3f)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Box(root, "OutsideKerb", doorPos + new Vector3(0, 0.45f, 0.012f), new Vector3(2.4f, 0.9f, 0.02f), C("#86847E"), false);
                Box(root, "OutsideLine", doorPos + new Vector3(0, 0.62f, 0.016f), new Vector3(2.4f, 0.05f, 0.01f), C("#F2C230"), false);
                if (n == 5)
                {
                    Box(root, "BusBody", doorPos + new Vector3(0.25f, 1.25f, 0.02f), new Vector3(2.1f, 1.1f, 0.01f), C("#F4F4F2"), false);
                    Box(root, "BusStripe", doorPos + new Vector3(0.25f, 0.95f, 0.025f), new Vector3(2.1f, 0.14f, 0.01f), C("#2A6FDB"), false);
                    Emissive(root, "BusWindows", doorPos + new Vector3(0.25f, 1.5f, 0.026f), new Vector3(1.9f, 0.4f, 0.01f), C("#2B3A4A"), 0.4f);
                    Cylinder(root, "BusWheel", doorPos + new Vector3(-0.35f, 0.72f, 0.03f), new Vector3(0.36f, 0.02f, 0.36f), C("#1E1E1E")).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    Cylinder(root, "BusWheel", doorPos + new Vector3(0.95f, 0.72f, 0.03f), new Vector3(0.36f, 0.02f, 0.36f), C("#1E1E1E")).transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                MakeWaypoint(root, $"bus_stop_{n}", doorPos + new Vector3(0, 1f, 0.7f), new Vector3(2.4f, 2f, 1.2f), "wp_stop", $"{n}번 승차장", true);
            }
            Anchor(r, "Start_arrivals", hall + new Vector3(4f, 0.05f, -hd / 2 + 2.5f), -20);

            DressArrivals(r, hall, hw, hd, hh);

            var director = Director(r, new[] { "airport", "airport_bus" }, 41, 0);
            EndMaterials();
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------- subway

        const float PlatformX = 70f;

        /// <summary>
        /// 명동역: the concourse under the street (stairs up to 명동길 behind the player), the convenience store and
        /// its till, the ticket and charging machines, the 고객안전실 window, the gate line with one lane the player
        /// taps through, and two stairways down marked by direction (회현 · 사당 방면 / 충무로 방면). The platform
        /// is built as a second space to the east: an island platform with full-height screen doors, car–door
        /// numbers, and the train that pulls in.
        /// </summary>
        [MenuItem("Seoul First Week/Build Subway")]
        public static void BuildSubway()
        {
            var scene = NewScene("Subway");
            BeginMaterials();
            float w = 28, d = 18, h = 3.4f;
            var line4 = C("#00A4E3");
            var panel = TextureKit.Surface("Panel_Station", TextureKit.Plastic(), C("#E4E6E8"), 0.6f);
            var steel = TextureKit.Surface("Steel_Station", TextureKit.Metal(), C("#BFC4CA"), 0.8f, 0.85f, true);
            var floor = TextureKit.Surface("Floor_Station", TextureKit.FloorGranite(), C("#CFCBC3"));
            var ceiling = TextureKit.Surface("Ceiling_Station", TextureKit.CeilingTiles(), C("#E9EAEB"), 0.5f);
            var r = Shell(new RoomSpec
            {
                Name = "Subway", W = w, D = d, H = h, Style = CounterStyle.High, Enclosed = true,
                PassageSign = "출구 · Exit  명동길", PassageGlow = C("#FFE3B8"),
                Light = C("#F3F6FF"), Wall = C("#EDEFF1"), Accent = line4, WindowX = new float[0],
                Floor = floor, Ceiling = ceiling, CounterFront = panel, Cabinet = panel,
            });
            var root = r.Root;
            var rng = new System.Random(51);
            var lit = Mats.Emissive(C("#FFFFFF"), 1.5f);
            for (float z = -d / 2 + 1.5f; z < d / 2; z += 3f)
                InteriorKit.LinearLight(root, new Vector3(0, 0, z), h, w - 3f, 90, lit);
            // the line colour runs round the walls, with the station name
            foreach (var (pos, yaw, len) in new[]
            {
                (new Vector3(0, 2.55f, d / 2 - 0.03f), 180f, w), (new Vector3(-w / 2 + 0.03f, 2.55f, 0), 90f, d), (new Vector3(w / 2 - 0.03f, 2.55f, 0), -90f, d),
            })
                Box(root, "LineBand", pos, new Vector3(len, 0.18f, 0.01f), line4, false, yaw);
            Sign(root, "StationName", new Vector3(0, 2.9f, d / 2 - 0.05f), new Vector2(5.2f, 0.62f), C("#2B2E33"), "<color=#00A4E3>●</color> 명동 Myeong-dong  424", Color.white, 180, 56, 0.06f);

            // ---- the unpaid side (south): the convenience store, the machines, the station office window
            var store = ServiceCounter(r, 1, new Vector3(-w / 2 + 3.6f, 0, -3.6f), -90, 1.8f, panel, r.TopMat, "storeclerk", HumanLook.Student(new System.Random(52), false), C("#F28C28"), false);
            Sign(root, "StoreSign", new Vector3(-w / 2 + 0.06f, 2.7f, -3.6f), new Vector2(4.2f, 0.6f), C("#F28C28"), "늘봄24 · 편의점", Color.white, 90, 56, 0.06f);
            for (int i = 0; i < 3; i++)
            {
                var shelf = Group("StoreShelf", root);
                shelf.position = new Vector3(-w / 2 + 0.4f, 0, -6.2f + i * 1.3f);
                Slab(shelf, "Frame", new Vector3(0, 0.9f, 0), new Vector3(0.5f, 1.8f, 1.1f), steel, true);
                for (int k = 0; k < 4; k++)
                for (int j = 0; j < 4; j++)
                    Box(shelf, "Goods", new Vector3(0.2f, 0.3f + k * 0.42f, -0.4f + j * 0.27f), new Vector3(0.16f, 0.24f, 0.2f),
                        C(new[] { "#E5484D", "#F2C230", "#2F5FD0", "#1FA37A", "#FFFFFF", "#F28C28" }[rng.Next(6)]), false);
            }
            Poster(root, new Vector3(-w / 2 + 0.06f, 1.75f, -1.3f), 90, new Vector2(0.9f, 0.62f), "교통카드 판매 · 충전\n<size=24>현금만 가능 · Cash only</size>", "#FFFFFF", "#0F4A6B", 34);
            for (int i = 0; i < 3; i++)
                InteriorKit.Kiosk(root, new Vector3(-6.5f + i * 1.2f, 0, -d / 2 + 0.55f), 0, i == 2 ? "1회용 발매" : "교통카드 충전");
            var office = ServiceCounter(r, 2, new Vector3(w / 2 - 2.6f, 0, -3.2f), 90, 2.2f, panel, r.TopMat, "stationstaff", HumanLook.Office(new System.Random(53), false), line4);
            Sign(root, "OfficeSign", new Vector3(w / 2 - 0.06f, 2.7f, -3.2f), new Vector2(4.4f, 0.62f), line4, "고객안전실 · 역무실\n<size=28>Customer Safety Office</size>", Color.white, -90, 50, 0.06f);
            for (int i = 0; i < 3; i++)
                Emissive(root, "Cctv", new Vector3(w / 2 - 0.08f, 1.55f + (i % 2) * 0.4f, -4.4f + i * 0.5f), new Vector3(0.02f, 0.32f, 0.44f), C("#6FA8DC"), 0.8f);
            Poster(root, new Vector3(w / 2 - 0.06f, 1.7f, -0.8f), -90, new Vector2(1.0f, 0.8f),
                "물건을 두고 내렸을 땐\n<size=24>아! 차량 시간\n차! 내린 칸 위치\n내! 짐의 위치</size>", "#FFFFFF", "#0F4A6B", 30);

            // ---- the gate line across the hall; only the lane at x = 0 opens, when the card is tapped
            float gz = 0.6f;
            var cabinet = TextureKit.Surface("Gate_Cabinet", TextureKit.Metal(), C("#AEB4BA"), 0.7f, 0.8f, true);
            GameObject[] myFlaps = null;
            for (int i = 0; i < 12; i++)
            {
                float x = -5.5f + i * 1.0f;
                Slab(root, "GateCabinet", new Vector3(x, 0.5f, gz), new Vector3(0.3f, 1.0f, 1.7f), cabinet);
                Emissive(root, "GateReader", new Vector3(x - 0.08f, 1.02f, gz - 0.55f), new Vector3(0.14f, 0.02f, 0.14f), C("#3DDC84"), 1.3f);
                if (i == 11) break;
                // the flaps across the lane to its right
                var flapL = Box(root, "Flap", new Vector3(x + 0.33f, 0.7f, gz), new Vector3(0.36f, 0.5f, 0.04f), C("#D8343B"), true);
                var flapR = Box(root, "Flap", new Vector3(x + 0.67f, 0.7f, gz), new Vector3(0.36f, 0.5f, 0.04f), C("#D8343B"), true);
                if (i == 5) myFlaps = new[] { flapL, flapR };
            }
            // glass railings close the rest of the line
            foreach (var (x0, x1) in new[] { (-w / 2, -5.65f), (5.65f, w / 2) })
            {
                Glass(root, "GateRail", new Vector3((x0 + x1) / 2f, 0.55f, gz), new Vector3(x1 - x0, 1.1f, 0.03f));
                Box(root, "GateRailTop", new Vector3((x0 + x1) / 2f, 1.11f, gz), new Vector3(x1 - x0, 0.04f, 0.06f), C("#9AA0A6"), false);
            }
            var gateWp = MakeWaypoint(root, "gate", new Vector3(0.0f, 1f, gz - 1.0f), new Vector3(0.8f, 1.6f, 0.6f), "wp_tap_card", "");
            gateWp.opens = myFlaps;
            HangingSign(r, "GateSign", new Vector3(0, 2.75f, gz - 0.2f), 180, new Vector2(3.2f, 0.42f), C("#2B2E33"), "타는 곳 · To Trains", Color.white, 50);

            // ---- the paid side: two stairways down, one per direction
            foreach (var (x, label, id, dest) in new[]
            {
                (-6f, "회현 · 사당 방면", "stairs_sadang", "Hoehyeon · Sadang"),
                (6f, "충무로 방면", "stairs_jinjeop", "Chungmuro"),
            })
            {
                var z = d / 2 - 0.15f;
                Slab(root, "StairFrame", new Vector3(x, 1.25f, z - 0.05f), new Vector3(3.0f, 2.5f, 0.1f), steel, false);
                Box(root, "StairDark", new Vector3(x, 1.15f, z - 0.1f), new Vector3(2.6f, 2.3f, 0.02f), C("#151719"), false);
                // the flight going down, painted in perspective on the dark opening
                for (int k = 0; k < 6; k++)
                    Box(root, "StairNosing", new Vector3(x, 0.15f + k * 0.28f, z - 0.115f), new Vector3(2.4f - k * 0.18f, 0.03f, 0.01f), C("#F2C230"), false);
                HangingSign(r, "DirectionSign", new Vector3(x, 2.85f, z - 0.6f), 180, new Vector2(3.4f, 0.6f), C("#2B2E33"),
                    $"<color=#00A4E3>④</color> ↓ {label}\n<size=26>{dest}</size>", Color.white, 46);
                var wp = MakeWaypoint(root, id, new Vector3(x, 1f, z - 0.9f), new Vector3(2.6f, 2f, 1.2f), "wp_stairs", label);
                if (id == "stairs_sadang") wp.moveTo = Anchor(r, "PlatformArrival", new Vector3(PlatformX - 9f, 0.05f, -1.2f), 0);
            }
            for (int i = 0; i < 3; i++)
                Extra(r, $"Commuter{i}", HumanLook.Random(rng, Crowd.Street), new Vector3(-9f + i * 7f, 0, 4.5f + (i % 2) * 1.6f), rng.Next(0, 360), phone: i != 1);
            Extra(r, "Shopper", HumanLook.Random(rng, Crowd.Street), new Vector3(-w / 2 + 5.2f, 0, -6.2f), -90);

            DressConcourse(r, w, d, h, line4);

            // ---- the platform, a second space to the east
            BuildPlatform(r, line4, steel, floor, ceiling, rng);

            Director(r, new[] { "subway", "subway_lost" }, 51, 0);
            EndMaterials();
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildPlatform(Room r, Color line4, Material steel, Material floor, Material ceiling, System.Random rng)
        {
            var root = r.Root;
            var p = Group("Platform", root);
            p.position = new Vector3(PlatformX, 0, 0);
            float len = 40f, pw = 6f, h = 3.2f;
            var wall = TextureKit.Surface("Wall_Platform", TextureKit.WallPaint(), C("#DADCDE"));
            Slab(p, "Floor", new Vector3(0, -0.05f, 0), new Vector3(len, 0.1f, pw), floor);
            Slab(p, "Ceiling", new Vector3(0, h + 0.05f, 0), new Vector3(len, 0.1f, pw + 6f), ceiling, true, false);
            Slab(p, "WallS", new Vector3(0, h / 2, -pw / 2 - 0.1f), new Vector3(len, h, 0.2f), wall);
            foreach (var sx in new[] { -1f, 1f })
                Slab(p, "WallEnd", new Vector3(sx * (len / 2 + 0.1f), h / 2, 0), new Vector3(0.2f, h, pw), wall);
            // the yellow safety line and the tactile strip along the screen doors
            Box(p, "SafetyLine", new Vector3(0, 0.004f, pw / 2 - 0.5f), new Vector3(len, 0.008f, 0.3f), C("#F2C230"), false);
            for (float x = -len / 2 + 2; x < len / 2; x += 4)
                InteriorKit.LinearLight(p, p.TransformPoint(new Vector3(x, 0, 0)), h, 3f, 0, Mats.Emissive(Color.white, 1.4f));
            HallLights(p, Vector3.zero, len, pw, h, C("#F4F7FF"));

            // the track bed beyond the screen doors, and the tunnel wall
            // only as far as can be seen through the screen doors, and more up the tunnel the train comes from: a longer
            // track ran on through the concourse 70 m to the west and walled off its stairs down
            float tx0 = -len / 2 - 30f, tx1 = len / 2 + 4f, tlen = tx1 - tx0, tcx = (tx0 + tx1) / 2f;
            Slab(p, "TrackBed", new Vector3(tcx, -1.1f, pw / 2 + 2.2f), new Vector3(tlen, 0.2f, 4.2f), TextureKit.Surface("Ballast", TextureKit.FloorStone(), C("#5A5752")), false);
            Slab(p, "TunnelWall", new Vector3(tcx, 0.6f, pw / 2 + 4.3f), new Vector3(tlen, 4f, 0.2f), TextureKit.Surface("Tunnel", TextureKit.WallPaint(), C("#6E6C68")), false);
            foreach (var z in new[] { pw / 2 + 1.5f, pw / 2 + 2.9f })
                Box(p, "Rail", new Vector3(tcx, -0.95f, z), new Vector3(tlen, 0.12f, 0.08f), C("#8A8F96"), false);

            // full-height platform screen doors: fixed panels with a sliding pair at each car door, numbered car–door
            var psdLeft = new List<Transform>();
            var psdRight = new List<Transform>();
            float z0 = pw / 2;
            var doorXs = new[] { -15f, -10f, -5f, 0f, 5f, 10f, 15f };
            string[] numbers = { "3-4", "4-1", "4-2", "4-3", "4-4", "5-1", "5-2" };
            Slab(p, "PsdHeader", new Vector3(0, 2.45f, z0), new Vector3(len, 0.5f, 0.15f), steel, false);
            for (float x = -len / 2; x < len / 2; x += 1.25f)
            {
                if (doorXs.Any(dx => Mathf.Abs(x + 0.625f - dx) < 1.0f)) continue;
                Glass(p, "PsdPanel", new Vector3(x + 0.625f, 1.1f, z0), new Vector3(1.2f, 2.2f, 0.04f));
            }
            for (int i = 0; i < doorXs.Length; i++)
            {
                float x = doorXs[i];
                var l = Group("PsdLeafL", p);
                l.localPosition = new Vector3(x - 0.32f, 0, z0);
                Glass(l, "Glass", new Vector3(0, 1.1f, 0), new Vector3(0.64f, 2.2f, 0.04f));
                var rr = Group("PsdLeafR", p);
                rr.localPosition = new Vector3(x + 0.32f, 0, z0);
                Glass(rr, "Glass", new Vector3(0, 1.1f, 0), new Vector3(0.64f, 2.2f, 0.04f));
                psdLeft.Add(l);
                psdRight.Add(rr);
                // the car–door number on a sticker above the doors and painted on the floor in front of them
                var label = Sign(p, "DoorNumber", new Vector3(x, 2.45f, z0 - 0.09f), new Vector2(0.6f, 0.32f), line4, numbers[i], Color.white, 180, 60, 0.02f);
                var floorNum = Group("FloorNumber", p);
                floorNum.localPosition = new Vector3(x, 0.006f, z0 - 1.0f);
                Box(floorNum, "Plate", Vector3.zero, new Vector3(0.7f, 0.01f, 0.45f), C("#F2C230"), false);
                WorldText.Create(floorNum, "Text", numbers[i], new Vector3(0, 0.008f, 0), new Vector2(0.6f, 0.36f), 60, C("#1E1E1E"), true, TextAnchor.MiddleCenter,
                    Quaternion.Euler(90, 0, 0));
            }
            // the direction board and the next-station sign
            HangingSign(r, "PlatformSign", p.TransformPoint(new Vector3(-6f, 2.85f, -0.4f)), 0, new Vector2(4.8f, 0.62f), C("#2B2E33"),
                "<color=#00A4E3>④</color> 회현 · 사당 방면\n<size=26>다음역 회현 Hoehyeon</size>", Color.white, 46);
            Sign(p, "StationName", new Vector3(8f, 1.7f, -pw / 2 + 0.05f), new Vector2(3.6f, 0.9f), C("#FFFFFF"),
                "<color=#00A4E3>●</color> 명동\n<size=30>Myeong-dong  424</size>", C("#1E1E1E"), 0, 70, 0.04f);

            // the train: two cars, their doors in line with the screen doors at the stop
            var train = Group("Train", p);
            var trainBody = Group("Body", train);
            var carMat = TextureKit.Surface("Car_Steel", TextureKit.Metal(), C("#D9DDE1"), 0.8f, 0.85f, true);
            var carLeft = new List<Transform>();
            var carRight = new List<Transform>();
            float tz = z0 + 2.2f;
            Slab(trainBody, "Cars", new Vector3(0, 1.2f, tz), new Vector3(len - 0.6f, 2.9f, 3.0f), carMat, false);
            Box(trainBody, "Stripe", new Vector3(0, 1.75f, tz - 1.505f), new Vector3(len - 0.6f, 0.22f, 0.01f), line4, false);
            foreach (var x in doorXs)
            {
                var l = Group("CarLeafL", trainBody);
                l.localPosition = new Vector3(x - 0.32f, 0, tz - 1.52f);
                Box(l, "Door", new Vector3(0, 1.0f, 0), new Vector3(0.64f, 1.9f, 0.03f), C("#C5CAD0"), false);
                Emissive(l, "Window", new Vector3(0, 1.45f, -0.02f), new Vector3(0.44f, 0.7f, 0.01f), C("#FFF6E0"), 0.9f);
                var rr = Group("CarLeafR", trainBody);
                rr.localPosition = new Vector3(x + 0.32f, 0, tz - 1.52f);
                Box(rr, "Door", new Vector3(0, 1.0f, 0), new Vector3(0.64f, 1.9f, 0.03f), C("#C5CAD0"), false);
                Emissive(rr, "Window", new Vector3(0, 1.45f, -0.02f), new Vector3(0.44f, 0.7f, 0.01f), C("#FFF6E0"), 0.9f);
                carLeft.Add(l);
                carRight.Add(rr);
            }
            for (float x = -len / 2 + 2.5f; x < len / 2 - 2f; x += 2.5f)
                if (!doorXs.Any(dx => Mathf.Abs(x - dx) < 1.0f))
                    Emissive(trainBody, "CarWindow", new Vector3(x, 1.5f, tz - 1.51f), new Vector3(1.6f, 0.7f, 0.01f), C("#FFF6E0"), 0.9f);
            var trainSet = train.gameObject.AddComponent<StationTrain>();
            trainSet.body = trainBody;
            trainSet.stopPosition = Vector3.zero;
            trainSet.runIn = 90f;
            trainSet.leavesLeft = psdLeft.Concat(carLeft).ToArray();
            trainSet.leavesRight = psdRight.Concat(carRight).ToArray();

            // waypoints: board at 4-2, read the number at 4-2, the stairs up at the west end
            var board = MakeWaypoint(root, "train", p.TransformPoint(new Vector3(-5f, 1f, z0 - 0.6f)), new Vector3(1.4f, 2f, 1.0f), "wp_board", "");
            board.performer = trainSet;
            board.moveTo = BuildCarInterior(r, line4, rng);
            MakeWaypoint(root, "door_number", p.TransformPoint(new Vector3(-5f, 0.6f, z0 - 1.0f)), new Vector3(1.0f, 1.2f, 0.8f), "wp_read", "4-2");
            Slab(p, "StairFrame", new Vector3(-len / 2 + 0.15f, 1.25f, -1f), new Vector3(0.1f, 2.5f, 3.0f), steel, false);
            Box(p, "StairDark", new Vector3(-len / 2 + 0.21f, 1.15f, -1f), new Vector3(0.02f, 2.3f, 2.6f), C("#151719"), false);
            HangingSign(r, "ExitSign", p.TransformPoint(new Vector3(-len / 2 + 0.8f, 2.8f, -1f)), 90, new Vector2(3.0f, 0.5f), C("#F2C230"), "↑ 나가는 곳 · 고객안전실", C("#1E1E1E"), 44);
            var up = MakeWaypoint(root, "stairs_up", p.TransformPoint(new Vector3(-len / 2 + 1.0f, 1f, -1f)), new Vector3(1.2f, 2f, 2.6f), "wp_stairs_up", "고객안전실");
            up.moveTo = Anchor(r, "OfficeArrival", new Vector3(8f, 0.05f, -4.8f), 70);
            Anchor(r, "Start_platform", p.TransformPoint(new Vector3(-10.5f, 0.05f, -1.8f)), 62);
            for (int i = 0; i < 3; i++)
                Extra(r, $"Waiting{i}", HumanLook.Random(rng, Crowd.Street), p.TransformPoint(new Vector3(4f + i * 3.5f, 0, 1.2f)), 0, phone: i != 0);
            DressPlatform(r, p, len, pw, h, line4);
        }

        /// <summary>
        /// The inside of a Line 4 car, a third space further east: long side benches between four doors a side,
        /// poles and straps, the dark tunnel in the windows, and the display over the door that shows the next stop.
        /// Returns where the player stands once aboard.
        /// </summary>
        static Transform BuildCarInterior(Room r, Color line4, System.Random rng)
        {
            var car = Group("CarInterior", r.Root);
            car.position = new Vector3(PlatformX + 70f, 0, 0);
            float cl = 19.5f, cw = 3.1f, ch = 2.3f;
            var shell = TextureKit.Surface("Car_Inside", TextureKit.Plastic(), C("#EEF0F2"), 0.7f);
            var seat = TextureKit.Surface("Seat_Car", TextureKit.FabricWeave(), C("#3E6FB5"), 0.3f);
            var steel = Mats.Lit(C("#C9CED4"), 0.6f, 0.8f);
            Slab(car, "Floor", new Vector3(0, -0.05f, 0), new Vector3(cl, 0.1f, cw), TextureKit.Surface("Floor_Car", TextureKit.FloorVinyl(), C("#8E9399")));
            Slab(car, "Ceiling", new Vector3(0, ch + 0.05f, 0), new Vector3(cl, 0.1f, cw), shell, true, false);
            foreach (var sx in new[] { -1f, 1f })
                Slab(car, "EndWall", new Vector3(sx * (cl / 2 + 0.05f), ch / 2, 0), new Vector3(0.1f, ch, cw), shell);
            float[] doors = { -7.3f, -2.45f, 2.45f, 7.3f };
            foreach (var sz in new[] { -1f, 1f })
            {
                float z = sz * (cw / 2 + 0.05f);
                Slab(car, "SideWall", new Vector3(0, ch / 2, z), new Vector3(cl, ch, 0.1f), shell);
                foreach (var dx in doors)
                {
                    Box(car, "Door", new Vector3(dx, 1.0f, z - sz * 0.06f), new Vector3(1.3f, 1.95f, 0.02f), C("#C5CAD0"), false);
                    Emissive(car, "DoorWindow", new Vector3(dx, 1.4f, z - sz * 0.075f), new Vector3(0.9f, 0.6f, 0.01f), C("#1B2433"), 0.6f);
                    Cylinder(car, "Pole", new Vector3(dx + 0.8f, ch / 2, z - sz * 0.35f), new Vector3(0.04f, ch / 2, 0.04f), C("#C9CED4"));
                }
                // seven-seat benches between the doors, the dark tunnel in the windows above them
                for (int i = 0; i < doors.Length - 1; i++)
                {
                    float x0 = doors[i] + 0.75f, x1 = doors[i + 1] - 0.75f, cx = (x0 + x1) / 2f;
                    Slab(car, "Bench", new Vector3(cx, 0.4f, z - sz * 0.32f), new Vector3(x1 - x0, 0.1f, 0.48f), seat, true);
                    Slab(car, "BenchBack", new Vector3(cx, 0.72f, z - sz * 0.1f), new Vector3(x1 - x0, 0.55f, 0.08f), seat, false);
                    Emissive(car, "Window", new Vector3(cx, 1.45f, z - sz * 0.06f), new Vector3(x1 - x0 - 0.2f, 0.62f, 0.01f), C("#141B26"), 0.5f);
                    for (float sx = x0 + 0.2f; sx < x1; sx += 0.45f)
                        Box(car, "Strap", new Vector3(sx, 1.85f, z - sz * 0.55f), new Vector3(0.1f, 0.14f, 0.02f), C("#F2C230"), false);
                    Box(car, "Rack", new Vector3(cx, 1.95f, z - sz * 0.3f), new Vector3(x1 - x0, 0.03f, 0.35f), C("#B9BEC4"), false);
                }
                Box(car, "Rail", new Vector3(0, 1.98f, z - sz * 0.55f), new Vector3(cl - 0.4f, 0.03f, 0.03f), C("#C9CED4"), false);
            }
            Box(car, "LineStripe", new Vector3(0, ch - 0.12f, -cw / 2 + 0.02f), new Vector3(cl, 0.06f, 0.01f), line4, false);
            for (float x = -cl / 2 + 2f; x < cl / 2; x += 4f)
                Emissive(car, "CeilingLight", new Vector3(x, ch - 0.02f, 0), new Vector3(2.6f, 0.03f, 0.3f), C("#FFFFFF"), 1.5f)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(car, new Vector3(-4f, ch - 0.3f, 0), 9f, 1.1f, C("#F4F7FF"));
            PointLight(car, new Vector3(4f, ch - 0.3f, 0), 9f, 1.1f, C("#F4F7FF"));
            // the display over the door: the next stop and the door side
            var display = Group("Display", car);
            display.SetPositionAndRotation(car.TransformPoint(new Vector3(2.45f, 2.12f, cw / 2 - 0.09f)), Quaternion.Euler(0, 180, 0));
            Box(display, "Frame", Vector3.zero, new Vector3(1.1f, 0.24f, 0.05f), C("#1E1E1E"), false);
            WorldText.Create(display, "Text", "이번 역  동작  Dongjak", new Vector3(0, 0, -0.03f), new Vector2(1.0f, 0.18f), 44, C("#FFB547"), true, TextAnchor.MiddleCenter,
                Quaternion.identity);
            // other passengers, seated and standing
            for (int i = 0; i < 6; i++)
            {
                float sz = i % 2 == 0 ? -1f : 1f;
                var pos = car.TransformPoint(new Vector3(-6f + i * 2.1f, 0, sz * (cw / 2 - 0.5f)));
                Extra(r, $"Passenger{i}", HumanLook.Random(rng, Crowd.Street), pos, sz < 0 ? 0 : 180, sitting: true, phone: i % 3 != 1, seat: 0.45f);
            }
            Extra(r, "Standing", HumanLook.Random(rng, Crowd.Street), car.TransformPoint(new Vector3(-0.8f, 0, 0.3f)), 90, phone: true);
            return Anchor(r, "InCar", car.TransformPoint(new Vector3(1.6f, 0.05f, -0.4f)), 20);
        }

        // ------------------------------------------------------------------- 119 on the street

        /// <summary>
        /// 명동길 again (a copy of the Hub, like the menus): a man in his sixties has fallen in front of 별빛화장품
        /// and can't get up; a few people have stopped. The player calls 119 and talks to the dispatcher (a voice
        /// on the phone, filtered) while looking at him, until the ambulance pulls up and its crew steps out.
        /// </summary>
        [MenuItem("Seoul First Week/Build Street 119")]
        public static void BuildStreet119()
        {
            var path = $"{SceneFolder}/Street119.unity";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset($"{SceneFolder}/Hub.unity", path);
            var scene = EditorSceneManager.OpenScene(path);
            BeginMaterials();
            var hub = scene.GetRootGameObjects().First(g => g.name == "[Hub]").transform;
            var director0 = hub.Find("HubDirector");
            if (director0) Object.DestroyImmediate(director0.gameObject);
            foreach (var b in hub.GetComponentsInChildren<ObjectiveBeacon>(true)) Object.DestroyImmediate(b.gameObject);
            foreach (var e in hub.GetComponentsInChildren<BuildingEntrance>(true)) Object.DestroyImmediate(e);
            var player = hub.GetComponentInChildren<FirstPersonController>(true);

            // where he fell: in front of a shop on the east side, 14 m ahead of the spawn, in view from the start
            const float z = -52f;
            var street = StreetPoint(z);
            var fwd = StreetDir(z);
            var east = Vector3.Cross(Vector3.up, fwd);
            street.y = GroundY(street) + 1.2f;
            var shopFront = street + east * 5f;
            var normal = -east;
            if (FacadeHit(street, east, 30f, out var hit))
            {
                normal = hit.normal;
                normal.y = 0;
                normal.Normalize();
                shopFront = hit.point;
            }
            var ground = GroundY(shopFront + normal * 1.5f);
            var scenery = Group("Emergency", hub);
            var shop = Sign(scenery, "ShopSign", new Vector3(shopFront.x, ground + 3.4f, shopFront.z) + normal * 0.25f, new Vector2(4.6f, 1.1f), C("#E4405F"),
                "별빛화장품\n<size=34>BYEOLBIT COSMETICS</size>", Color.white, 0, 70, 0.25f);
            shop.rotation = Quaternion.LookRotation(normal);
            var patientPos = new Vector3(shopFront.x, ground, shopFront.z) + normal * 2.0f;
            var decorRoot = hub.Find("Decor");
            if (decorRoot)
                foreach (var t in decorRoot.Cast<Transform>().ToList())
                {
                    var dp = t.position - patientPos;
                    dp.y = 0;
                    if (dp.magnitude < 5f && t.name != "StreetLamp") Object.DestroyImmediate(t.gameObject);
                }
            // the shop's standing board on the pavement: the landmark the player can read out to the dispatcher
            var along = Vector3.Cross(normal, Vector3.up);
            var standee = Group("ShopBoard", scenery);
            // beyond him as the player comes up the street, so it frames him instead of hiding him
            standee.SetPositionAndRotation(patientPos - along * 1.4f - normal * 0.3f, Quaternion.LookRotation(normal));
            Box(standee, "LegL", new Vector3(-0.3f, 0.55f, 0), new Vector3(0.04f, 1.1f, 0.04f), C("#2B2E33"), false);
            Box(standee, "LegR", new Vector3(0.3f, 0.55f, 0), new Vector3(0.04f, 1.1f, 0.04f), C("#2B2E33"), false);
            Sign(standee, "Board", new Vector3(0, 0.82f, 0), new Vector2(0.7f, 0.62f), C("#E4405F"), "별빛\n화장품", Color.white, 0, 70, 0.03f);
            var patient = HumanBuilder.Build(scenery, "Patient", HumanLook.Ajeossi(new System.Random(61)));
            patient.transform.SetPositionAndRotation(patientPos, Quaternion.LookRotation(Vector3.Cross(normal, Vector3.up)));
            patient.sitting = true;
            patient.seatHeight = 0.14f;
            patient.Pose(0f);
            Box(scenery, "Bag", patientPos + Vector3.Cross(normal, Vector3.up) * 0.9f + normal * 0.3f + Vector3.up * 0.12f, new Vector3(0.4f, 0.24f, 0.18f), C("#5E4334"), false);
            var rng = new System.Random(62);
            // bystanders stand round him, clear of the spot where the player stands to make the call
            for (int i = 0; i < 3; i++)
            {
                var a = new[] { -78f, 70f, 118f }[i];
                var dir = Quaternion.Euler(0, a, 0) * normal;
                var pos = patientPos + dir * 1.9f;
                pos.y = GroundY(pos + Vector3.up);
                var rig = HumanBuilder.Build(scenery, $"Bystander{i}", HumanLook.Random(rng, Crowd.Street));
                rig.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(patientPos - pos));
                rig.phone = i == 2;
                rig.lookTarget = patient.head;
                rig.Pose(0f);
            }

            // the call: a voice with no body; the player looks at him while talking
            var dispatcherGo = new GameObject("Dispatcher");
            dispatcherGo.transform.SetParent(scenery, false);
            var dispatcher = dispatcherGo.AddComponent<NpcActor>();
            dispatcher.staffId = "dispatcher";
            dispatcher.phone = true;
            dispatcher.face = patient.head;
            var talkSpot = Group("Seat_phone", scenery);
            var standAt = patientPos + normal * 1.7f - Vector3.Cross(normal, Vector3.up) * 0.6f;
            standAt.y = GroundY(standAt + Vector3.up) + 0.05f;
            talkSpot.SetPositionAndRotation(standAt, Quaternion.LookRotation(patientPos - standAt));
            var callWp = MakeWaypoint(scenery, "patient", patientPos + Vector3.up * 0.7f, new Vector3(2.2f, 1.6f, 2.2f), "wp_call", "");
            // the part opens 9 m down the street from him, looking his way, so the scene is clear from the first frame
            var startSpot = Group("Start_patient", scenery);
            var startAt = StreetPoint(z - 9f);
            startAt.y = GroundY(startAt + Vector3.up) + 0.05f;
            var toHim = patientPos - startAt;
            toHim.y = 0;
            startSpot.SetPositionAndRotation(startAt, Quaternion.LookRotation(toHim));

            // the ambulance: a white van with an orange stripe and a light bar, from the south end of the street
            var amb = Group("Ambulance", scenery);
            var body = Group("Body", amb);
            Box(body, "Box", new Vector3(0, 1.35f, 0), new Vector3(2.1f, 2.2f, 5.6f), C("#F7F7F5"), false);
            Box(body, "Cab", new Vector3(0, 1.0f, 3.2f), new Vector3(2.0f, 1.5f, 1.0f), C("#F7F7F5"), false);
            Box(body, "Stripe", new Vector3(0, 1.2f, 0.4f), new Vector3(2.12f, 0.3f, 6.4f), C("#E85D1C"), false);
            Emissive(body, "Windscreen", new Vector3(0, 1.45f, 3.71f), new Vector3(1.7f, 0.6f, 0.02f), C("#9FB4C8"), 0.4f);
            foreach (var sx in new[] { -1f, 1f })
                WorldText.Create(body, "Label", "119 구급", new Vector3(sx * 1.07f, 1.75f, 0), new Vector2(2.6f, 0.5f), 70, C("#D8343B"), true, TextAnchor.MiddleCenter,
                    Quaternion.Euler(0, sx * -90f, 0));
            foreach (var (x, zz) in new[] { (-0.75f, -2.2f), (0.75f, -2.2f), (-0.75f, 2.6f), (0.75f, 2.6f) })
                Cylinder(body, "Wheel", new Vector3(x * 1.3f, 0.36f, zz), new Vector3(0.7f, 0.12f, 0.7f), C("#1E1E1E")).transform.localRotation = Quaternion.Euler(0, 0, 90);
            var la = Emissive(body, "LightA", new Vector3(-0.5f, 2.55f, 2.4f), new Vector3(0.8f, 0.16f, 0.3f), C("#FF2A2A"), 4f).GetComponent<Renderer>();
            var lb = Emissive(body, "LightB", new Vector3(0.5f, 2.55f, 2.4f), new Vector3(0.8f, 0.16f, 0.3f), C("#2A5BFF"), 4f).GetComponent<Renderer>();
            var ambulance = amb.gameObject.AddComponent<Ambulance>();
            ambulance.body = body;
            var stop = patientPos + normal * 2.8f + Vector3.Cross(normal, Vector3.up) * 5f;
            var from = StreetPoint(-70f);
            ambulance.from = new Vector3(from.x, GroundY(from + Vector3.up * 2f), from.z);
            ambulance.to = new Vector3(stop.x, GroundY(stop + Vector3.up * 2f), stop.z);
            ambulance.lightA = la;
            ambulance.lightB = lb;
            var crew = new List<GameObject>();
            for (int i = 0; i < 2; i++)
            {
                var look = HumanLook.Officer(i == 1, 8 + i);
                look.Outer = OuterKind.UniformVest;
                look.OuterColor = C("#E85D1C");
                var pos = patientPos + Vector3.Cross(normal, Vector3.up) * (i == 0 ? 1.0f : -1.0f) + normal * 0.6f;
                pos.y = GroundY(pos + Vector3.up);
                var rig = HumanBuilder.Build(scenery, $"Paramedic{i}", look);
                rig.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(patientPos - pos));
                rig.lookTarget = patient.head;
                rig.Pose(0f);
                crew.Add(rig.gameObject);
            }
            ambulance.crew = crew.ToArray();
            var ambWp = MakeWaypoint(scenery, "ambulance", ambulance.to + Vector3.up, new Vector3(1f, 1f, 1f), "wp_read", "");
            ambWp.performer = ambulance;
            ambWp.autoComplete = true;

            // the director: no counter, no crowd of visitors; the street keeps its pedestrians and its sound
            var go = new GameObject("ScenarioDirector");
            go.transform.SetParent(hub, false);
            var d = go.AddComponent<ScenarioDirector>();
            d.procedures = new[] { AssetDatabase.LoadAssetAtPath<TextAsset>($"{ProcedureFolder}/emergency_119.json") };
            if (!d.procedures[0]) Debug.LogError($"[Builder] Missing procedure file {ProcedureFolder}/emergency_119.json");
            d.player = player;
            d.counters = new CounterSpot[0];
            d.boards = new QueueBoard[0];
            d.actors = new[] { dispatcher };
            d.anchors = new[] { talkSpot, startSpot };
            d.stations = new FetchStation[0];
            d.waypoints = new[] { callWp, ambWp };
            var amb2 = go.AddComponent<AmbientAudio>();
            amb2.street = true;
            amb2.volume = 0.35f;

            EndMaterials();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Builder] Street119 built");
        }

        // ------------------------------------------------------------------- the ER at night

        /// <summary>
        /// 한빛대학교병원 응급실 at night: the ER desk (응급접수 · 수납) with the form shelf beside it, the waiting
        /// seats under the status monitor, curtained treatment bays along the east wall, and the triage room
        /// (예진실) in the south-east corner that the player is called into by name, as at the clinic.
        /// </summary>
        [MenuItem("Seoul First Week/Build ER")]
        public static void BuildER()
        {
            var scene = NewScene("ER");
            BeginMaterials();
            float w = 18, d = 12, h = 2.8f;
            var red = C("#D8343B");
            var panel = TextureKit.Surface("Panel_ER", TextureKit.Plastic(), C("#EDEFF0"), 0.6f);
            var r = Shell(new RoomSpec
            {
                Name = "ER", W = w, D = d, H = h, Style = CounterStyle.High, Enclosed = true,
                PassageSign = "출입구 · Entrance", Light = C("#F2F6FF"), Wall = C("#F1F3F4"), Accent = red,
                CounterX0 = -7.4f, CounterX1 = -1.4f, WindowX = new[] { -5.6f, -3.2f },
                Floor = TextureKit.Surface("Floor_ER", TextureKit.FloorVinyl(), C("#DCDFE0")),
                Ceiling = TextureKit.Surface("Ceiling", TextureKit.CeilingTiles(), Color.white, 0.6f),
                CounterFront = panel, Cabinet = panel,
            });
            var root = r.Root;
            var rng = new System.Random(71);
            float F = r.CounterFrontZ;
            GridCeiling(r, false, C("#F4F8FF"));
            Wainscot(r, panel, Mats.Lit(red, 0.35f), 1.0f, C("#9AA0A6"));

            Sign(root, "ERSign", new Vector3(-4.4f, 2.35f, d / 2 - 0.04f), new Vector2(5.4f, 0.6f), red, "응급실 · EMERGENCY", Color.white, 180, 60, 0.06f);
            CounterStaff(r, 1, "erdesk", HumanLook.Office(new System.Random(72), false));
            CounterStaff(r, 2, "erdesk2", HumanLook.Office(new System.Random(73), true));
            InteriorKit.DeskSign(root, new Vector3(-5.6f - 0.55f, 1.09f, F), 0, "1 응급접수 · 수납", red, 0.44f);
            InteriorKit.DeskSign(root, new Vector3(-3.2f - 0.55f, 1.09f, F), 0, "2 원무", red, 0.4f);
            Poster(root, new Vector3(-7.9f, 1.6f, d / 2 - 0.04f), 180, new Vector2(0.9f, 0.7f), "진료 순서 안내\n<size=22>위급한 환자부터\n진료합니다</size>", "#FFFFFF", "#A3301E", 28);

            // the form shelf beside the desk: 진료신청서 and pens
            var shelf = Group("FormShelf", root);
            shelf.position = new Vector3(-0.6f, 0, d / 2 - 2.9f);
            Slab(shelf, "Top", new Vector3(0, 0.95f, 0), new Vector3(1.2f, 0.05f, 0.5f), r.TopMat);
            Slab(shelf, "Base", new Vector3(0, 0.46f, 0), new Vector3(1.1f, 0.92f, 0.45f), panel);
            for (int i = 0; i < 3; i++)
                Box(shelf, "Form", new Vector3(-0.35f + i * 0.35f, 0.985f, 0), new Vector3(0.28f, 0.01f, 0.38f), Color.white, false, 6 * (i % 2));
            Sign(shelf, "Label", new Vector3(0, 1.45f, 0.24f), new Vector2(1.1f, 0.3f), red, "진료신청서 · Forms", Color.white, 0, 34, 0.03f);
            var scol = shelf.gameObject.AddComponent<BoxCollider>();
            scol.isTrigger = true;
            scol.center = new Vector3(0, 1.0f, 0);
            scol.size = new Vector3(1.4f, 1.2f, 1.0f);
            var station = shelf.gameObject.AddComponent<FetchStation>();
            station.stationId = "er_form";
            station.document = "er_form";

            // the status monitor (masked names), the waiting seats facing the desk
            var monitor = InteriorKit.WallTv(root, new Vector3(1.6f, 1.85f, d / 2 - 0.05f), 180, C("#16324A"),
                "응급실 진료 현황\n<size=24>위급도 순으로 진료합니다</size>\n대기  -\n\n", width: 1.3f);
            var waiting = monitor.gameObject.AddComponent<WaitingBoard>();
            waiting.text = monitor.Find("Caption").GetComponentInChildren<UnityEngine.UI.Text>();
            waiting.otherPages = new[] { "응급실 진료 현황\n<size=24>Patients are seen by urgency</size>\n검사 중  3명\n결과 대기  2명" };
            var sheet = TextureKit.Surface("Seat_Perforated", TextureKit.Perforated(), C("#B7BEC6"), 0.6f, 0.5f);
            float row = r.AisleZ - 1.3f;
            for (int i = 0; i < 2; i++, row -= 1.35f)
                SeatRow(r, row, SeatKind.Steel, sheet, null, (-7.2f, 4), (-3.3f, 4));
            r.SeatGaps.AddRange(new[] { -4.4f, -0.6f });

            // treatment bays along the east wall: beds behind curtains, a nurses' station
            for (int i = 0; i < 3; i++)
            {
                float bz = d / 2 - 1.3f - i * 2.4f;
                var bed = Group("Bed", root);
                bed.position = new Vector3(w / 2 - 1.0f, 0, bz);
                Box(bed, "Frame", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.1f, 2.0f), C("#C9CED4"), false);
                Box(bed, "Mattress", new Vector3(0, 0.56f, 0), new Vector3(0.85f, 0.12f, 1.95f), C("#E9F0F4"), false);
                foreach (var dz in new[] { -0.9f, 0.9f })
                foreach (var dx in new[] { -0.38f, 0.38f })
                    Box(bed, "Leg", new Vector3(dx, 0.2f, dz), new Vector3(0.04f, 0.4f, 0.04f), C("#9AA0A6"), false);
                Box(root, "CurtainRail", new Vector3(w / 2 - 2.0f, h - 0.08f, bz), new Vector3(0.03f, 0.03f, 2.3f), C("#B9BEC4"), false);
                for (int k = 0; k < (i == 1 ? 6 : 3); k++)
                    Box(root, "Curtain", new Vector3(w / 2 - 2.0f + (k % 2) * 0.04f, 1.45f, bz - 1.05f + k * 0.18f), new Vector3(0.03f, 2.4f, 0.2f), C("#9FC3D6"), false);
                Emissive(root, "BayLamp", new Vector3(w / 2 - 0.06f, 2.1f, bz), new Vector3(0.02f, 0.12f, 0.3f), i == 0 ? red : C("#3DDC84"), 1.2f);
            }
            InteriorKit.IvPole(root, new Vector3(w / 2 - 1.8f, 0, d / 2 - 2.6f));
            Extra(r, "Nurse_Station", HumanLook.Nurse(2), new Vector3(3.6f, 0, 2.8f), 90, talking: true);

            // ---- 예진실 (triage) in the south-east corner
            float rx0 = 3.6f, rz1 = -1.4f, rz0 = -d / 2;
            var roomWall = TextureKit.Surface("Wall_Triage", TextureKit.WallPaint(), C("#F4F5F6"));
            Slab(root, "RoomWallN", new Vector3((rx0 + w / 2) / 2f, h / 2, rz1), new Vector3(w / 2 - rx0, h, 0.15f), roomWall);
            Slab(root, "RoomWallW", new Vector3(rx0, h / 2, (rz0 + rz1) / 2f), new Vector3(0.15f, h, rz1 - rz0), roomWall);
            var door = TextureKit.Surface("Door_ER", TextureKit.Plastic(), C("#DDE3E8"), 0.5f);
            InteriorKit.RoomDoor(root, new Vector3(4.4f, 0, rz1 + 0.075f), 0, "예진실", "Triage", door, red, "진료중");
            var entranceGo = new GameObject("Room_triage");
            entranceGo.transform.SetParent(root, false);
            entranceGo.transform.position = new Vector3(4.4f, 1.0f, rz1 + 0.35f);
            var roomCol = entranceGo.AddComponent<BoxCollider>();
            roomCol.isTrigger = true;
            roomCol.size = new Vector3(1.2f, 2.0f, 0.6f);
            var triage = entranceGo.AddComponent<RoomEntrance>();
            triage.location = "triage";
            triage.label = "예진실";
            triage.approach = Group("DoorApproach_triage", root);
            triage.approach.SetPositionAndRotation(new Vector3(4.4f, 0, rz1 + 0.12f), Quaternion.identity);
            triage.outside = Group("Outside_triage", root);
            triage.outside.SetPositionAndRotation(new Vector3(4.4f, 0.05f, rz1 + 1.15f), Quaternion.LookRotation(new Vector3(-8f, 0, 4f)));
            float dx2 = 6.0f, deskZ = -3.6f;
            InteriorKit.Desk(root, new Vector3(dx2, 0, deskZ), 180, new Vector3(1.4f, InteriorKit.DeskTop, 0.7f), r.TopMat, panel);
            var nurseSeat = Group("TriageSeat", root);
            nurseSeat.SetPositionAndRotation(new Vector3(dx2, 0, deskZ + 0.72f), Quaternion.Euler(0, 180, 0));
            InteriorKit.Workstation(root, nurseSeat.position, deskZ + 0.35f, 1f, C("#DDEBFF"));
            InteriorKit.OfficeChair(root, new Vector3(dx2, 0, deskZ + 0.65f), 180, C("#2B2E33"));
            Staff(r, "triage", nurseSeat, HumanLook.Nurse(1));
            // a vitals monitor on its stand beside the desk
            Cylinder(root, "MonitorStand", new Vector3(dx2 + 1.1f, 0.6f, deskZ + 0.1f), new Vector3(0.05f, 0.6f, 0.05f), C("#9AA0A6"));
            Box(root, "VitalsMonitor", new Vector3(dx2 + 1.1f, 1.32f, deskZ + 0.1f), new Vector3(0.36f, 0.28f, 0.12f), C("#2B2E33"), false, -30);
            Emissive(root, "VitalsScreen", new Vector3(dx2 + 1.07f, 1.32f, deskZ + 0.04f), new Vector3(0.3f, 0.22f, 0.01f), C("#3DDC84"), 0.9f);
            InteriorKit.Stool(root, new Vector3(dx2, 0, deskZ - 0.85f), C("#2B2E33"));
            var triageSeat = Group("Seat_triage", root);
            triageSeat.SetPositionAndRotation(new Vector3(dx2, 0.05f, deskZ - 0.91f), Quaternion.identity);
            triageSeat.gameObject.AddComponent<PlayerSeat>();
            r.Anchors.Add(triageSeat);
            Poster(root, new Vector3(w / 2 - 0.06f, 1.6f, -4.2f), -90, new Vector2(0.9f, 0.7f), "통증 점수\n<size=24>0  1  2  3  4  5\n6  7  8  9  10</size>", "#FFFFFF", "#16324A", 30);
            InteriorKit.Clock(root, new Vector3(-w / 2 + 0.05f, 2.2f, 0.5f), 90, 0.34f);
            InteriorKit.Extinguisher(root, new Vector3(-w / 2 + 0.28f, 0, -d / 2 + 0.5f), 90);
            Plant(root, new Vector3(2.6f, 0, -d / 2 + 0.6f), 0.9f);

            DressER(r, w, d, h);

            var director = Director(r, new[] { "emergency_er" }, 71, 5, station);
            director.rooms = new[] { triage };
            director.waitingBoard = waiting;
            EndMaterials();
            EditorSceneManager.SaveScene(scene);
        }
    }
}
