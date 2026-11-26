using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.World;
using UnityEngine;
using UnityEngine.Rendering;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// The airport in motion (2026-11-26): a real baggage carousel (a loop of sloping plates round a steel island,
    /// fed by a chute through the wall) with bags riding it, the player's yellow suitcase among them; travellers
    /// walking from the gates through the lanes, past the booths, waiting at the belt and out through customs; the
    /// arrivals exit where they come out past the people waiting for them; and the kerb outside the arrivals
    /// hall's doors, with the airport bus at stop 5.
    /// </summary>
    public static partial class SeoulSceneBuilder
    {
        // ------------------------------------------------------------------- the carousel

        /// <summary>
        /// Carousel 5 at <paramref name="centre"/>, its long side along z: plates on a stadium-shaped loop round a
        /// stainless island, a rim with its rubber bumper, the feed chute from the wall with its rubber curtain,
        /// and bags riding round. The player's suitcase is one of them and carries its `carousel` waypoint.
        /// </summary>
        static void BuildCarousel(Room r, Vector3 centre, System.Random rng)
        {
            var root = r.Root;
            var c = Group("Carousel", root);
            c.position = centre;
            var parts = Group("Parts", c);
            var steel = TextureKit.Surface("Steel_Carousel", TextureKit.Metal(), C("#C3C8CE"), 0.45f, 0.6f);
            const float half = 3.0f, radius = 1.0f, beltW = 0.72f, beltY = 0.46f;
            // the loop the plates and the bags follow: two straights and two half circles
            var loop = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
                loop.Add(new Vector3(radius, beltY, -half + 2f * half * i / 12f));
            for (int i = 1; i < 12; i++)
            {
                float a = Mathf.PI * i / 12f;
                loop.Add(new Vector3(radius * Mathf.Cos(a), beltY, half + radius * Mathf.Sin(a)));
            }
            for (int i = 0; i <= 12; i++)
                loop.Add(new Vector3(-radius, beltY, half - 2f * half * i / 12f));
            for (int i = 1; i < 12; i++)
            {
                float a = Mathf.PI * i / 12f;
                loop.Add(new Vector3(-radius * Mathf.Cos(a), beltY, -half - radius * Mathf.Sin(a)));
            }
            // the island and the skirt under the plates
            Slab(parts, "Island", new Vector3(0, 0.32f, 0), new Vector3(2f * (radius - beltW / 2f) - 0.04f, 0.64f, 2f * half), steel, false);
            foreach (var sz in new[] { -1f, 1f })
                Cylinder(parts, "IslandEnd", new Vector3(0, 0.32f, sz * half), new Vector3(2f * (radius - beltW / 2f) - 0.04f, 0.32f, 2f * (radius - beltW / 2f) - 0.04f), C("#C3C8CE"));
            Slab(parts, "Skirt", new Vector3(0, 0.2f, 0), new Vector3(2f * (radius + beltW / 2f), 0.4f, 2f * half), steel, false);
            foreach (var sz in new[] { -1f, 1f })
                Cylinder(parts, "SkirtEnd", new Vector3(0, 0.2f, sz * half), new Vector3(2f * (radius + beltW / 2f), 0.2f, 2f * (radius + beltW / 2f)), C("#B9BEC4"));
            // the plates: overlapping slats along the loop, tipped a little towards the island
            float len = 0;
            for (int i = 0; i < loop.Count; i++) len += Vector3.Distance(loop[i], loop[(i + 1) % loop.Count]);
            int plates = Mathf.RoundToInt(len / 0.24f);
            for (int k = 0; k < plates; k++)
            {
                float d = len * k / plates;
                var (p, dir) = PointOnLoop(loop, d);
                var plate = Box(parts, "Plate", p + Vector3.up * 0.005f, new Vector3(beltW, 0.025f, 0.26f), k % 2 == 0 ? C("#2B2E33") : C("#33363B"), false);
                var outward = Vector3.Cross(Vector3.up, dir);
                plate.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, Vector3.Dot(outward, p) > 0 ? -5f : 5f);
            }
            // the rim and its black rubber bumper round the outside
            for (int k = 0; k < loop.Count; k++)
            {
                var a = loop[k];
                var b = loop[(k + 1) % loop.Count];
                var mid = (a + b) / 2f;
                var dir = (b - a).normalized;
                var outward = Vector3.Cross(Vector3.up, dir);
                if (Vector3.Dot(outward, new Vector3(mid.x, 0, Mathf.Abs(mid.z) > half ? mid.z - Mathf.Sign(mid.z) * half : 0)) < 0) outward = -outward;
                float seg = Vector3.Distance(a, b) * (radius + beltW / 2f + 0.06f) / radius + 0.02f;
                var rim = Box(parts, "Rim", mid + outward * (beltW / 2f + 0.06f) + Vector3.up * 0.02f, new Vector3(0.06f, 0.1f, seg), C("#C9CED4"), false);
                rim.transform.localRotation = Quaternion.LookRotation(dir);
                var bumper = Box(parts, "Bumper", mid + outward * (beltW / 2f + 0.1f) - Vector3.up * 0.06f, new Vector3(0.05f, 0.08f, seg), C("#1E1E1E"), false);
                bumper.transform.localRotation = Quaternion.LookRotation(dir);
            }
            // the chute from the wall to the far end of the loop, its rubber curtain at the wall
            float wallX = -r.W / 2f - centre.x;
            var chute = Group("Chute", parts);
            chute.localPosition = new Vector3((wallX + (-radius - beltW / 2f)) / 2f, 0, half + 0.4f);
            float chuteLen = Mathf.Abs(wallX + radius + beltW / 2f);
            var bed = Box(chute, "Bed", new Vector3(0, 0.95f, 0), new Vector3(chuteLen, 0.06f, 0.9f), C("#2B2E33"), false);
            bed.transform.localRotation = Quaternion.Euler(0, 0, -18f);
            foreach (var sz in new[] { -0.47f, 0.47f })
            {
                var side = Box(chute, "Side", new Vector3(0, 1.08f, sz), new Vector3(chuteLen, 0.3f, 0.04f), C("#C3C8CE"), false);
                side.transform.localRotation = Quaternion.Euler(0, 0, -18f);
            }
            Box(parts, "ChuteHood", new Vector3(wallX + 0.25f, 1.9f, half + 0.4f), new Vector3(0.5f, 0.9f, 1.3f), C("#C3C8CE"), false);
            for (int k = 0; k < 6; k++)
                Box(parts, "Curtain", new Vector3(wallX + 0.52f, 1.55f, half + 0.4f - 0.4f + k * 0.16f), new Vector3(0.01f, 0.7f, 0.15f), C("#1E1E1E"), false);
            Slab(parts, "ChuteLegs", new Vector3((wallX + (-radius - beltW / 2f)) / 2f, 0.4f, half + 0.4f), new Vector3(0.1f, 0.8f, 0.1f), steel, false);
            MergeByMaterial(parts, "Airport_Carousel");
            Solid(c, "Carousel", new Vector3(0, 0.3f, 0), new Vector3(2f * (radius + beltW / 2f + 0.1f), 0.6f, 2f * (half + radius + beltW / 2f)));

            // the bags on the belt: hard cases, soft bags and a box, at irregular gaps; the player's yellow one
            var belt = c.gameObject.AddComponent<CarouselBelt>();
            var bags = new List<Transform>();
            var start = new List<float>();
            var yaw = new List<float>();
            string[] colors = { "#3B5B8C", "#2B2E33", "#B83B3B", "#6B7A8C", "#3A6B4F", "#8C3B6B", "#C9A227", "#E8E6E1", "#5A4636" };
            float at = 0.4f;
            for (int i = 0; i < 12; i++)
            {
                var bag = Group($"Bag{i}", c);
                bool box = i == 7, soft = i % 3 == 1;
                var col = C(colors[rng.Next(colors.Length)]);
                if (box)
                {
                    Box(bag, "Box", new Vector3(0, 0.16f, 0), new Vector3(0.5f, 0.32f, 0.42f), C("#C9A877"), false);
                    Box(bag, "Tape", new Vector3(0, 0.322f, 0), new Vector3(0.06f, 0.005f, 0.42f), C("#E8D7A8"), false);
                }
                else if (soft)
                {
                    Box(bag, "Bag", new Vector3(0, 0.13f, 0), new Vector3(0.62f, 0.26f, 0.34f), col, false);
                    Box(bag, "Strap", new Vector3(0, 0.27f, 0), new Vector3(0.4f, 0.03f, 0.05f), C("#1E1E1E"), false);
                }
                else
                {
                    Box(bag, "Shell", new Vector3(0, 0.13f, 0), new Vector3(0.66f, 0.26f, 0.44f), col, false);
                    Box(bag, "Rib", new Vector3(0, 0.262f, 0), new Vector3(0.58f, 0.005f, 0.36f), Color.Lerp(col, Color.black, 0.2f), false);
                    Box(bag, "Handle", new Vector3(0, 0.27f, 0.15f), new Vector3(0.22f, 0.03f, 0.04f), C("#1E1E1E"), false);
                }
                bag.localPosition = new Vector3(0, beltY + 0.02f, 0);
                bags.Add(bag);
                start.Add(at);
                yaw.Add(rng.Next(-30, 30) + (rng.Next(2) == 0 ? 0 : 90));
                at += 0.9f + (float)rng.NextDouble() * 0.9f;
            }
            var mine = Group("MySuitcase", c);
            Box(mine, "Shell", new Vector3(0, 0.14f, 0), new Vector3(0.68f, 0.28f, 0.46f), C("#F2C230"), false);
            Box(mine, "Rib", new Vector3(0, 0.282f, 0), new Vector3(0.6f, 0.005f, 0.38f), C("#D9A916"), false);
            Box(mine, "Handle", new Vector3(0, 0.29f, 0.16f), new Vector3(0.22f, 0.03f, 0.04f), C("#1E1E1E"), false);
            Box(mine, "Tag", new Vector3(0.3f, 0.2f, 0.16f), new Vector3(0.02f, 0.12f, 0.08f), C("#FFFFFF"), false);
            mine.localPosition = new Vector3(0, beltY + 0.02f, 0);
            bags.Add(mine);
            start.Add(at + 0.6f);
            yaw.Add(8f);
            // saved where they ride, so the scene shows them on the belt before it starts moving
            for (int i = 0; i < bags.Count; i++)
            {
                var (p, dir) = PointOnLoop(loop, start[i]);
                bags[i].localPosition = new Vector3(p.x, beltY + 0.02f, p.z);
                bags[i].localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, yaw[i], 0);
            }
            belt.loop = loop.ToArray();
            belt.bags = bags.ToArray();
            belt.startAt = start.ToArray();
            belt.yaw = yaw.ToArray();
            belt.speed = 0.36f;
            // the pick-up rides with the suitcase
            var bagWp = MakeWaypoint(mine, "carousel", mine.position + Vector3.up * 0.4f, new Vector3(1.2f, 1.4f, 1.2f), "wp_bag", "");
            bagWp.opens = new[] { mine.gameObject };
            // the board over the belt
            Sign(root, "CarouselBoard", centre + new Vector3(0.0f, 3.0f, 0f), new Vector2(2.4f, 1.1f), C("#1F2F55"), "5\n<size=34>하노이 HANOI</size>", C("#FFD23F"), 90, 90, 0.08f);
            Sign(root, "CarouselBoardBack", centre + new Vector3(-0.09f, 3.0f, 0f), new Vector2(2.4f, 1.1f), C("#1F2F55"), "5\n<size=34>하노이 HANOI</size>", C("#FFD23F"), -90, 90, 0.08f);
            foreach (var sz in new[] { -0.9f, 0.9f })
                Box(root, "BoardRod", centre + new Vector3(-0.045f, 3.55f + (r.H - 3.55f) / 2f, sz), new Vector3(0.02f, r.H - 3.55f, 0.02f), C("#9AA0A6"), false);
        }

        static (Vector3 p, Vector3 dir) PointOnLoop(List<Vector3> loop, float d)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                float seg = Vector3.Distance(a, b);
                if (d <= seg) return (Vector3.Lerp(a, b, seg > 0 ? d / seg : 0), (b - a).normalized);
                d -= seg;
            }
            return (loop[0], (loop[1] - loop[0]).normalized);
        }

        // ------------------------------------------------------------------- the arrivals hall's ways in and out

        /// <summary>
        /// The arrivals exit in the hall's west wall (frosted sliding doors that open as travellers come through,
        /// a lit corridor behind), the people waiting for them along the rail, and the kerb outside the doors in
        /// the south wall: sidewalk under a canopy, the bus stop poles, the road with the airport bus at stop 5,
        /// taxis, and the sky beyond. The south wall gets real openings with automatic doors.
        /// </summary>
        static void BuildArrivalsWays(Room r, Vector3 hall, float hw, float hd, float hh, System.Random rng)
        {
            var root = r.Root;
            var wallMat = TextureKit.Surface("Wall_Arrivals", TextureKit.WallPaint(), C("#F1F2F3"));
            // the south wall with the two door openings to the bus stops
            var hallGroup = root.Find("ArrivalsHall");
            var oldWall = hallGroup ? hallGroup.Find("WallS") : null;
            if (oldWall) Object.DestroyImmediate(oldWall.gameObject);
            WallWithOpenings(root, "ArrivalsWallS", hall + new Vector3(0, 0, -hd / 2 - 0.1f), 0, hw + 0.4f, hh, 0.2f, wallMat, 2.6f, (-6f, 2.6f), (4f, 2.6f));
            foreach (var x in new[] { -6f, 4f })
            {
                var door = Group("StopDoor", root);
                door.position = hall + new Vector3(x, 0, -hd / 2 - 0.1f);
                var left = Group("Left", door);
                left.localPosition = new Vector3(-0.62f, 0, 0);
                Glass(left, "Glass", new Vector3(0, 1.3f, 0), new Vector3(1.22f, 2.58f, 0.03f), 0, false);
                Box(left, "Edge", new Vector3(-0.6f, 1.3f, 0), new Vector3(0.04f, 2.58f, 0.05f), C("#9AA0A6"), false);
                var right = Group("Right", door);
                right.localPosition = new Vector3(0.62f, 0, 0);
                Glass(right, "Glass", new Vector3(0, 1.3f, 0), new Vector3(1.22f, 2.58f, 0.03f), 0, false);
                Box(right, "Edge", new Vector3(0.6f, 1.3f, 0), new Vector3(0.04f, 2.58f, 0.05f), C("#9AA0A6"), false);
                var auto = door.gameObject.AddComponent<AutoDoor>();
                auto.left = left;
                auto.right = right;
                auto.travel = 1.15f;
                auto.radius = 2.2f;
                Box(door, "Header", new Vector3(0, 2.68f, 0), new Vector3(2.7f, 0.16f, 0.3f), C("#9AA0A6"), false);
                Box(door, "Sensor", new Vector3(0, 2.56f, 0.18f), new Vector3(0.3f, 0.06f, 0.06f), C("#1E1E1E"), false);
                Box(door, "Mat", new Vector3(0, 0.01f, 0.9f), new Vector3(2.4f, 0.02f, 1.2f), C("#3A3A3A"), false);
            }

            // ---- outside: the sidewalk under its canopy, the kerb, the road, the bus and the sky
            var outside = Group("Outside", root);
            outside.position = hall;
            var parts = Group("Parts", outside);
            var paving = TextureKit.Surface("Paving_Airport", TextureKit.Paving(), C("#D8D3CB"));
            var asphalt = TextureKit.Surface("Asphalt_Airport", TextureKit.Asphalt(), C("#6B6A68"));
            float z0 = -hd / 2 - 0.2f, kerb = -hd / 2 - 4.2f, road1 = kerb - 7.5f;
            Slab(outside, "Sidewalk", new Vector3(0, -0.05f, (z0 + kerb) / 2f), new Vector3(hw + 6f, 0.1f, z0 - kerb), paving);
            Slab(parts, "Kerb", new Vector3(0, -0.04f, kerb - 0.08f), new Vector3(hw + 6f, 0.18f, 0.16f), TextureKit.Surface("Kerb_Airport", TextureKit.FloorStone(), C("#B5B1A8")), false);
            Slab(outside, "Road", new Vector3(0, -0.2f, (kerb + road1) / 2f), new Vector3(hw + 40f, 0.1f, kerb - road1), asphalt);
            for (float x = -hw / 2 - 18f; x < hw / 2 + 18f; x += 4f)
                Box(parts, "LaneDash", new Vector3(x, -0.145f, kerb - 3.7f), new Vector3(2f, 0.01f, 0.14f), C("#F4F4F2"), false);
            Box(parts, "BusLane", new Vector3(0, -0.145f, kerb - 0.4f), new Vector3(hw + 36f, 0.01f, 0.14f), C("#2F6FD8"), false);
            // the far side: a planted median and the multi-storey car park
            Slab(parts, "Median", new Vector3(0, 0.1f, road1 - 1.2f), new Vector3(hw + 40f, 0.5f, 2.4f), TextureKit.Surface("Kerb_Airport", TextureKit.FloorStone(), C("#B5B1A8")), false);
            for (float x = -hw / 2 - 16f; x < hw / 2 + 16f; x += 3f)
                Sphere(parts, "Shrub", new Vector3(x + (float)rng.NextDouble(), 0.65f, road1 - 1.2f), new Vector3(1.6f, 0.9f, 1.4f), C("#4C7A47"));
            Slab(parts, "CarPark", new Vector3(0, 6f, road1 - 9f), new Vector3(hw + 60f, 12f, 10f), TextureKit.Surface("Concrete_CarPark", TextureKit.WallPaint(), C("#BFC2C4")), false);
            for (int level = 0; level < 4; level++)
                Box(parts, "CarParkSlot", new Vector3(0, 1.6f + level * 2.9f, road1 - 3.95f), new Vector3(hw + 58f, 1.1f, 0.05f), C("#3A3D42"), false);
            Emissive(parts, "Sky", new Vector3(0, 12f, road1 - 30f), new Vector3(hw + 140f, 30f, 0.2f), C("#B9D7F0"), 0.9f)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // the canopy over the sidewalk, on its columns
            Slab(parts, "Canopy", new Vector3(0, 4.3f, (z0 + kerb + 0.6f) / 2f), new Vector3(hw + 6f, 0.25f, z0 - kerb + 0.6f), TextureKit.Surface("Steel_Airport", TextureKit.Metal(), C("#C3C8CE"), 0.8f, 0.85f, true), false);
            for (float x = -hw / 2 - 2f; x <= hw / 2 + 2.1f; x += 6f)
            {
                Cylinder(parts, "CanopyColumn", new Vector3(x, 2.15f, kerb + 0.6f), new Vector3(0.36f, 2.15f, 0.36f), C("#C3C8CE"));
                Emissive(parts, "CanopyLight", new Vector3(x + 3f, 4.16f, (z0 + kerb) / 2f), new Vector3(1.6f, 0.03f, 0.3f), C("#FFF8EC"), 1.6f)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // the bus stop poles
            foreach (var (x, n, lines) in new[] { (-6f, 5, "6015 명동 · 6001 서울역"), (4f, 3, "6020 강남 · 6009 잠실") })
            {
                Cylinder(parts, "StopPole", new Vector3(x + 1.6f, 1.4f, kerb + 0.5f), new Vector3(0.08f, 1.4f, 0.08f), C("#2B2E33"));
                Sign(outside, "StopPlate", new Vector3(x + 1.6f, 2.6f, kerb + 0.5f), new Vector2(0.9f, 0.9f), C("#1F2F55"), $"{n}\n<size=22>{lines}</size>", C("#FFD23F"), 0, 70, 0.05f);
                Sign(outside, "StopPlateBack", new Vector3(x + 1.6f, 2.6f, kerb + 0.44f), new Vector2(0.9f, 0.9f), C("#1F2F55"), $"{n}\n<size=22>{lines}</size>", C("#FFD23F"), 180, 70, 0.05f);
            }
            // the airport bus at stop 5, its door open towards the kerb; taxis further along
            AirportBus(outside, new Vector3(-6.8f, -0.15f, kerb - 1.6f));
            foreach (var x in new[] { 11f, 17f })
                Taxi(outside, new Vector3(x, -0.15f, kerb - 1.3f), rng);
            MergeByMaterial(parts, "Airport_Outside");
            // the sidewalk is as far as anyone goes
            Solid(outside, "KerbFence", new Vector3(0, 1f, kerb - 0.2f), new Vector3(hw + 6f, 2f, 0.2f));
            foreach (var sx in new[] { -1f, 1f })
                Solid(outside, "EndFence", new Vector3(sx * (hw / 2f + 3f), 1f, (z0 + kerb) / 2f), new Vector3(0.2f, 2f, z0 - kerb));

            // ---- the arrivals exit in the west wall
            var exit = Group("ArrivalsExit", root);
            exit.SetPositionAndRotation(hall + new Vector3(-hw / 2f, 0, -0.6f), Quaternion.Euler(0, 90, 0));
            Box(exit, "Recess", new Vector3(0, 1.4f, 0.006f), new Vector3(3.0f, 2.8f, 0.01f), C("#F4F6F8"), false);
            Emissive(exit, "CorridorLight", new Vector3(0, 1.4f, 0.013f), new Vector3(2.6f, 2.4f, 0.004f), C("#EEF3F8"), 0.8f);
            foreach (var sx in new[] { -1f, 1f })
                Box(exit, "Jamb", new Vector3(sx * 1.52f, 1.4f, 0.06f), new Vector3(0.1f, 2.8f, 0.12f), C("#9AA0A6"), false);
            Box(exit, "Head", new Vector3(0, 2.86f, 0.06f), new Vector3(3.14f, 0.14f, 0.12f), C("#9AA0A6"), false);
            var el = Group("Left", exit);
            el.localPosition = new Vector3(-0.72f, 0, 0.045f);
            Box(el, "Frosted", new Vector3(0, 1.38f, 0), new Vector3(1.44f, 2.74f, 0.04f), C("#DCE6EE"), false);
            var er = Group("Right", exit);
            er.localPosition = new Vector3(0.72f, 0, 0.045f);
            Box(er, "Frosted", new Vector3(0, 1.38f, 0), new Vector3(1.44f, 2.74f, 0.04f), C("#DCE6EE"), false);
            var auto2 = exit.gameObject.AddComponent<AutoDoor>();
            auto2.left = el;
            auto2.right = er;
            auto2.travel = 1.35f;
            auto2.radius = 2.4f;
            Sign(exit, "ExitSign", new Vector3(0, 3.35f, 0.08f), new Vector2(3.4f, 0.5f), C("#1F2F55"), "입국장 출구 A · Arrival Exit A", Color.white, 0, 44, 0.05f);
            Solid(exit, "Closed", new Vector3(0, 1.4f, 0.08f), new Vector3(3.0f, 2.8f, 0.1f));
        }

        /// <summary>A 45-seat airport limousine bus, white with a blue band, its door open towards the kerb (+z).</summary>
        static void AirportBus(Transform parent, Vector3 pos)
        {
            var bus = Group("AirportBus", parent);
            bus.localPosition = pos;
            var p = Group("Parts", bus);
            const float L = 11.6f, W = 2.5f, H = 3.4f, floor = 0.45f;
            Box(p, "Body", new Vector3(0, floor + 1.45f, 0), new Vector3(L, 2.9f, W), C("#F4F4F2"), false);
            Box(p, "Roof", new Vector3(0, floor + 2.95f, 0), new Vector3(L - 0.4f, 0.12f, W - 0.2f), C("#E4E4E2"), false);
            foreach (var sz in new[] { -1f, 1f })
            {
                Emissive(p, "Windows", new Vector3(0.3f, floor + 2.05f, sz * (W / 2f + 0.005f)), new Vector3(L - 2.2f, 1.0f, 0.01f), C("#1E2A38"), 0.35f);
                Box(p, "Band", new Vector3(0, floor + 1.15f, sz * (W / 2f + 0.008f)), new Vector3(L, 0.32f, 0.01f), C("#2F6FD8"), false);
                Box(p, "BandThin", new Vector3(0, floor + 0.85f, sz * (W / 2f + 0.008f)), new Vector3(L, 0.08f, 0.01f), C("#F2C230"), false);
                Box(p, "Skirt", new Vector3(0, floor + 0.12f, sz * (W / 2f + 0.006f)), new Vector3(L, 0.24f, 0.01f), C("#B9BEC4"), false);
                foreach (var wx in new[] { -L / 2f + 2.3f, L / 2f - 3.3f })
                {
                    var wheel = Cylinder(p, "Wheel", new Vector3(wx, 0.5f, sz * (W / 2f - 0.15f)), new Vector3(1.0f, 0.16f, 1.0f), C("#1E1E1E"));
                    wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    Box(p, "WheelArch", new Vector3(wx, 1.02f, sz * (W / 2f + 0.01f)), new Vector3(1.25f, 0.06f, 0.02f), C("#2B2E33"), false);
                }
            }
            // the front (+x): windscreen, the route display, lights
            Emissive(p, "Windscreen", new Vector3(L / 2f + 0.005f, floor + 1.95f, 0), new Vector3(0.01f, 1.4f, W - 0.3f), C("#1E2A38"), 0.35f);
            Box(p, "DestinationBox", new Vector3(L / 2f + 0.01f, floor + 2.82f, 0), new Vector3(0.01f, 0.26f, 1.8f), C("#141414"), false);
            WorldText.Create(bus, "Destination", "<color=#FFB547>6015</color> 명동 Myeong-dong", new Vector3(L / 2f + 0.02f, floor + 2.82f, 0), new Vector2(1.7f, 0.22f), 44,
                Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, -90, 0));
            foreach (var hz in new[] { -0.9f, 0.9f })
                Emissive(p, "Headlight", new Vector3(L / 2f + 0.01f, floor + 0.55f, hz), new Vector3(0.01f, 0.18f, 0.36f), C("#FFFBEA"), 2.4f);
            // the door near the front on the kerb side, open: a dark doorway and the first step
            Box(p, "Doorway", new Vector3(L / 2f - 1.6f, floor + 1.1f, W / 2f + 0.012f), new Vector3(1.0f, 2.1f, 0.01f), C("#1A1D22"), false);
            Box(p, "Step", new Vector3(L / 2f - 1.6f, 0.32f, W / 2f + 0.15f), new Vector3(0.9f, 0.08f, 0.3f), C("#3A3D42"), false);
            // the route on the side, over the band
            Sign(bus, "SideRoute", new Vector3(-0.8f, floor + 1.55f, W / 2f + 0.02f), new Vector2(3.0f, 0.34f), C("#2F6FD8"), "공항버스 6015 · 명동 · 서울역", Color.white, 0, 40, 0.01f);
            MergeByMaterial(p, "Airport_Bus");
        }

        /// <summary>A Seoul taxi: an orange-yellow saloon with its roof sign.</summary>
        static void Taxi(Transform parent, Vector3 pos, System.Random rng)
        {
            var t = Group("Taxi", parent);
            t.localPosition = pos;
            var p = Group("Parts", t);
            var body = rng.Next(2) == 0 ? C("#F28C28") : C("#E9E6DF");
            Box(p, "Body", new Vector3(0, 0.65f, 0), new Vector3(4.7f, 0.7f, 1.8f), body, false);
            Box(p, "Cabin", new Vector3(-0.2f, 1.2f, 0), new Vector3(2.6f, 0.55f, 1.6f), body, false);
            Emissive(p, "Glass", new Vector3(-0.2f, 1.22f, 0), new Vector3(2.5f, 0.42f, 1.62f), C("#1E2A38"), 0.3f);
            Box(p, "RoofSign", new Vector3(-0.2f, 1.58f, 0), new Vector3(0.5f, 0.18f, 0.25f), C("#F2C230"), false);
            foreach (var wx in new[] { -1.5f, 1.5f })
            foreach (var wz in new[] { -0.85f, 0.85f })
            {
                var wheel = Cylinder(p, "Wheel", new Vector3(wx, 0.33f, wz), new Vector3(0.66f, 0.1f, 0.66f), C("#1E1E1E"));
                wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            MergeByMaterial(p, $"Airport_Taxi_{Mathf.RoundToInt(pos.x)}");
        }

        // ------------------------------------------------------------------- people walking through

        /// <summary>
        /// Travellers in the immigration hall: in from the gates, up the foreigners' lane to a booth and round to
        /// the belt (picking up a case) and out through 신고없음; or up the Koreans' lane and out through the smart
        /// entry gates. In the arrivals hall: out of the arrivals exit past the people waiting, to the café, the
        /// exchange counter or the arrivals board, and out through the doors to the buses.
        /// </summary>
        static void BuildAirportFlows(Room r, Vector3 hall)
        {
            var hallFlow = new GameObject("ImmigrationFlow").AddComponent<CrowdFlow>();
            hallFlow.transform.SetParent(r.Root, false);
            hallFlow.travellers = true;
            hallFlow.count = 6;
            hallFlow.seed = 43;
            hallFlow.speed = 1.2f;
            hallFlow.gap = new Vector2(3f, 10f);
            Vector3 V(float x, float z) => new(x, 0, z);
            var fromGate = new[] { V(0, -16.6f), V(0, -12.6f) };
            CrowdFlow.Route Foreign(float boothX) => new()
            {
                points = fromGate.Concat(new[]
                {
                    V(-1.5f, -9.6f), V(-5.0f, -8.2f), V(-4.0f, -6.6f), V(-4.0f, 7.2f), V(boothX, 8.95f), V(-9.4f, 8.65f), V(-10.9f, 8.6f),
                    V(-11.0f, 7.6f), V(-13.2f, 6.6f), V(-14.6f, 4.8f), V(-14.65f, 2.8f), V(-14.7f, -2.4f), V(-15.3f, -4.3f), V(-16.75f, -4.3f),
                }).ToArray(),
                waits = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 6f, 0f, 0f, 0f, 0f, 0f, 9f, 0f, 0f, 0f },
                suitcase = true,
                caseFrom = 12,
            };
            hallFlow.routes = new[]
            {
                Foreign(-5.25f),
                Foreign(-8.75f),
                new CrowdFlow.Route
                {
                    points = fromGate.Concat(new[] { V(1.5f, -9.6f), V(5.0f, -8.2f), V(4.0f, -6.6f), V(4.0f, 8.6f), V(11.4f, 8.6f), V(15.4f, 8.7f), V(15.4f, 10.9f) }).ToArray(),
                    waits = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 2f, 0f },
                    suitcase = true, weight = 0.7f,
                },
            };

            var arrivals = new GameObject("ArrivalsFlow").AddComponent<CrowdFlow>();
            arrivals.transform.SetParent(r.Root, false);
            arrivals.travellers = true;
            arrivals.count = 7;
            arrivals.seed = 44;
            arrivals.speed = 1.15f;
            arrivals.gap = new Vector2(2f, 8f);
            Vector3 H(float x, float z) => hall + new Vector3(x, 0, z);
            var outDoor = new[] { H(-14.6f, -0.6f), H(-12f, -0.6f) };
            var toStop5 = new[] { H(-6f, -7.6f), H(-6f, -9.6f), H(-6f, -10.9f), H(-12f, -11.1f), H(-17.4f, -11.1f) };
            var toStop3 = new[] { H(4f, -7.6f), H(4f, -9.6f), H(4f, -10.9f), H(10f, -11.1f), H(17.4f, -11.1f) };
            CrowdFlow.Route A(Vector3[] middle, Vector3[] end, float wait, int waitAt, float weight = 1f)
            {
                var pts = outDoor.Concat(middle).Concat(end).ToArray();
                var w = new float[pts.Length];
                if (waitAt >= 0) w[waitAt] = wait;
                return new CrowdFlow.Route { points = pts, waits = w, suitcase = true, weight = weight };
            }
            arrivals.routes = new[]
            {
                A(new[] { H(-6f, -0.9f) }, toStop5, 0, -1),
                A(new[] { H(-4f, 1.0f), H(1.6f, 6.9f), H(3.0f, 3.5f), H(2.6f, -6.5f) }, toStop3, 8f, 3, 0.8f),
                A(new[] { H(-2f, -1.5f), H(3.5f, -1.6f), H(8.6f, -1.0f), H(10.4f, 4.0f), H(8.5f, -2.0f), H(4.5f, -6.5f) }, toStop3, 7f, 5, 0.6f),
                A(new[] { H(-1f, -5.5f) }, toStop5, 6f, 2, 0.6f),
            };
        }
    }
}
