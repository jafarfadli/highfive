using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// 명동역 in detail (2026-11-26): the gate line where any lane takes a card, the shops of the unpaid side, the
    /// paid side's columns and restrooms, the people coming and going, and the Line 4 train: three cars with real
    /// door openings, windows you can see into, benches, poles, straps and racks, a floor to step onto, and the
    /// people who get off and on when it opens. The ride itself happens in one more car built the same way.
    /// </summary>
    public static partial class SeoulSceneBuilder
    {
        // ------------------------------------------------------------------- small shared helpers

        /// <summary>An invisible box the player can't walk through (the visible parts are merged without colliders).</summary>
        static GameObject Solid(Transform parent, string name, Vector3 center, Vector3 size, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>Like <see cref="MergeByMaterial"/>, for a group that moves (the train): no static flags.</summary>
        static void MergeMoving(Transform group, string asset)
        {
            MergeByMaterial(group, asset);
            foreach (Transform c in group)
                if (c.name.StartsWith("Merged_")) GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);
        }

        // ------------------------------------------------------------------- stairs you can walk

        /// <summary>
        /// A real flight of stairs in its own shaft: <paramref name="steps"/> steps of 30 cm going down (rise &lt; 0) or up
        /// from <paramref name="start"/> (the edge of the floor it leaves, at the middle of its width) towards local +z
        /// after <paramref name="yaw"/>, then a landing. Granite treads with yellow nosing, walls either side with
        /// handrails, a ceiling that follows the flight 2.5 m over it, strip lights, and a light at the landing.
        /// Returns the point in the middle of the flight, for the trigger that answers "you took these stairs".
        /// </summary>
        static Vector3 Stairwell(Transform parent, string name, Vector3 start, float yaw, int steps, float rise, float width,
            Material tread, Material wall, Color light, string landingSign = null)
        {
            var g = Group(name, parent);
            g.SetPositionAndRotation(start, Quaternion.Euler(0, yaw, 0));
            var parts = Group("Parts", g);
            const float run = 0.3f, landing = 1.8f, head = 2.5f;
            float drop = rise * steps, len = run * steps;
            float low = Mathf.Min(0, drop) - 0.35f;
            for (int i = 0; i < steps; i++)
            {
                float top = rise * (i + 1), z = run * i + run / 2f;
                float bottom = rise < 0 ? low : -0.05f;
                Slab(g, "Step", new Vector3(0, (top + bottom) / 2f, z), new Vector3(width, top - bottom, run), tread, true, false);
                Box(parts, "Nosing", new Vector3(0, top + 0.004f, z - run / 2f + 0.04f), new Vector3(width - 0.1f, 0.008f, 0.05f), C("#F2C230"), false);
            }
            // the landing at the far end, and the wall that closes the shaft
            float landTop = drop;
            Slab(g, "Landing", new Vector3(0, (landTop + low) / 2f, len + landing / 2f), new Vector3(width, landTop - low, landing), tread, true, false);
            float shaftLow = Mathf.Min(0, drop) - 0.35f, shaftHigh = Mathf.Max(0, drop) + head + 0.3f;
            foreach (var sx in new[] { -1f, 1f })
            {
                Slab(g, "ShaftWall", new Vector3(sx * (width / 2f + 0.1f), (shaftLow + shaftHigh) / 2f, (len + landing) / 2f),
                    new Vector3(0.2f, shaftHigh - shaftLow, len + landing + 0.2f), wall);
                // the handrail along the flight, 90 cm over the nosings
                var rail = Cylinder(parts, "Handrail", new Vector3(sx * (width / 2f - 0.06f), drop / 2f + 0.9f, len / 2f), new Vector3(0.045f, Mathf.Sqrt(len * len + drop * drop) / 2f, 0.045f), C("#B9BEC4"));
                rail.transform.localRotation = Quaternion.Euler(Mathf.Atan2(len, drop) * Mathf.Rad2Deg, 0, 0);
                for (float z = 0.3f; z < len; z += 1.5f)
                    Box(parts, "RailBracket", new Vector3(sx * (width / 2f - 0.03f), drop * (z / len) + 0.82f, z), new Vector3(0.06f, 0.06f, 0.04f), C("#9AA0A6"), false);
            }
            Slab(g, "ShaftEnd", new Vector3(0, (shaftLow + shaftHigh) / 2f, len + landing + 0.1f), new Vector3(width + 0.4f, shaftHigh - shaftLow, 0.2f), wall);
            // the ceiling follows the flight, 2.5 m over the nosings, then runs flat over the landing
            float slopeLen = Mathf.Sqrt(len * len + drop * drop);
            var ceil = Slab(g, "ShaftCeiling", new Vector3(0, drop / 2f + head + 0.05f, len / 2f), new Vector3(width + 0.2f, 0.1f, slopeLen), wall, false, false);
            ceil.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(drop, len) * Mathf.Rad2Deg, 0, 0);
            Slab(g, "LandingCeiling", new Vector3(0, drop + head + 0.05f, len + landing / 2f), new Vector3(width + 0.2f, 0.1f, landing + 0.1f), wall, false, false);
            var strip = Emissive(parts, "StripLight", new Vector3(0, drop / 2f + head - 0.01f, len / 2f), new Vector3(0.18f, 0.02f, slopeLen - 0.4f), C("#F6F9FF"), 1.5f);
            strip.transform.localRotation = ceil.transform.localRotation;
            strip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(g, new Vector3(0, drop + 2.0f, len + landing / 2f), 6f, 1.2f, light).shadows = LightShadows.None;
            PointLight(g, new Vector3(0, drop / 2f + 2.0f, len / 2f), 6f, 0.9f, C("#F4F7FF")).shadows = LightShadows.None;
            if (!string.IsNullOrEmpty(landingSign))
                Sign(g, "LandingSign", new Vector3(0, drop + 1.9f, len + landing - 0.02f), new Vector2(width - 0.4f, 0.45f), C("#F2C230"), landingSign, C("#1E1E1E"), 180, 40, 0.04f);
            MergeByMaterial(parts, $"{parent.root.name}_{name}");
            return g.TransformPoint(new Vector3(0, drop * 0.55f + 1.0f, len * 0.55f));
        }

        /// <summary>A wall with door-sized openings cut into it (to replace a room's solid wall where stairs leave it).</summary>
        static void WallWithOpenings(Transform parent, string name, Vector3 center, float yaw, float length, float height, float thick, Material m,
            float openingTop, params (float at, float width)[] openings)
        {
            var g = Group(name, parent);
            g.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
            float x = -length / 2f;
            foreach (var (at, wdt) in openings.OrderBy(o => o.at))
            {
                float a = at - wdt / 2f;
                if (a > x) Slab(g, "Wall", new Vector3((x + a) / 2f, height / 2f, 0), new Vector3(a - x, height, thick), m);
                Slab(g, "Lintel", new Vector3(at, (openingTop + height) / 2f, 0), new Vector3(wdt, height - openingTop, thick), m);
                x = at + wdt / 2f;
            }
            if (x < length / 2f) Slab(g, "Wall", new Vector3((x + length / 2f) / 2f, height / 2f, 0), new Vector3(length / 2f - x, height, thick), m);
        }

        // ------------------------------------------------------------------- the gate line

        /// <summary>
        /// The ticket gates across the concourse at <paramref name="gz"/>: twelve cabinets with eleven lanes between
        /// them, each with its reader, its red flaps and its own `gate` waypoint, so the player taps at whichever lane
        /// they walk up to. Commuters use the same lanes both ways (Seoul's gates take cards in either direction).
        /// </summary>
        static GateLane[] BuildGateLine(Room r, float gz)
        {
            var root = r.Root;
            var line = Group("GateLine", root);
            var cabinet = TextureKit.Surface("Gate_Cabinet", TextureKit.Metal(), C("#AEB4BA"), 0.7f, 0.8f, true);
            var top = TextureKit.Surface("Gate_Top", TextureKit.Plastic(), C("#2B2E33"), 0.5f);
            var lanes = new List<GateLane>();
            for (int i = 0; i < 12; i++)
            {
                float x = -5.5f + i * 1.0f;
                Slab(line, "GateCabinet", new Vector3(x, 0.5f, gz), new Vector3(0.3f, 1.0f, 1.7f), cabinet);
                Slab(line, "GateTop", new Vector3(x, 1.01f, gz), new Vector3(0.32f, 0.03f, 1.72f), top, false);
                // the arrows at both ends: green, a card is taken this way
                foreach (var sz in new[] { -1f, 1f })
                    Emissive(line, "GateArrow", new Vector3(x, 0.86f, gz + sz * 0.86f), new Vector3(0.14f, 0.1f, 0.01f), C("#3DDC84"), 1.4f)
                        .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var red = C("#D8343B");
            for (int j = 0; j < 11; j++)
            {
                float cx = -5.0f + j * 1.0f;
                var lane = Group($"GateLane_{j}", line);
                lane.localPosition = new Vector3(cx, 0, gz);
                var flapL = Box(lane, "FlapL", new Vector3(-0.17f, 0.72f, 0), new Vector3(0.36f, 0.48f, 0.04f), red, true);
                var flapR = Box(lane, "FlapR", new Vector3(0.17f, 0.72f, 0), new Vector3(0.36f, 0.48f, 0.04f), red, true);
                // the reader for this lane on the cabinet to its right, at both ends (a ring of light around it)
                var reader = Emissive(lane, "Reader", new Vector3(0.38f, 1.03f, -0.58f), new Vector3(0.16f, 0.012f, 0.16f), C("#3DDC84"), 1.3f);
                Emissive(lane, "ReaderOut", new Vector3(-0.38f, 1.03f, 0.58f), new Vector3(0.16f, 0.012f, 0.16f), C("#3DDC84"), 1.3f);
                var gl = lane.gameObject.AddComponent<GateLane>();
                gl.flapLeft = flapL.transform;
                gl.flapRight = flapR.transform;
                gl.reader = reader.GetComponent<Renderer>();
                lanes.Add(gl);
                var wp = MakeWaypoint(root, "gate", new Vector3(cx, 1f, gz - 1.0f), new Vector3(0.8f, 1.6f, 0.6f), "wp_tap_card", "");
                wp.gate = gl;
            }
            // glass railings close the rest of the line
            foreach (var (x0, x1) in new[] { (-r.W / 2, -5.65f), (5.65f, r.W / 2) })
            {
                Glass(line, "GateRail", new Vector3((x0 + x1) / 2f, 0.55f, gz), new Vector3(x1 - x0, 1.1f, 0.03f));
                Box(line, "GateRailTop", new Vector3((x0 + x1) / 2f, 1.11f, gz), new Vector3(x1 - x0, 0.04f, 0.06f), C("#9AA0A6"), false);
                for (float x = x0 + 1.2f; x < x1 - 0.3f; x += 1.6f)
                    Box(line, "GateRailPost", new Vector3(x, 0.56f, gz), new Vector3(0.05f, 1.12f, 0.06f), C("#9AA0A6"), false);
            }
            return lanes.ToArray();
        }

        // ------------------------------------------------------------------- the shops of the unpaid side

        enum ShopKind { Cosmetics, Bakery, Phone, Socks, Flowers }

        /// <summary>
        /// A small station shop built into the wall: partitions to the ceiling, a lit fascia with its name, a glass
        /// front with an open door (or an open counter front), shelves packed with goods, a counter and its clerk.
        /// Local frame: the wall at z = 0, the shop's front at z = <paramref name="depth"/>, facing +z.
        /// </summary>
        static void StationShop(Room r, ShopKind kind, string name, string sub, Color fascia, Vector3 wallPos, float yaw, float width, float depth,
            System.Random rng)
        {
            var g = Group($"Shop_{kind}", r.Root);
            g.SetPositionAndRotation(wallPos, Quaternion.Euler(0, yaw, 0));
            var parts = Group("Parts", g);
            var solids = Group("Solids", g);
            float h = r.H, hw = width / 2f;
            var partition = TextureKit.Surface("Shop_Partition", TextureKit.Plastic(), C("#E8E6E1"), 0.6f, 0f, true);
            var shelfMat = TextureKit.Surface("Shop_Shelf", TextureKit.WoodLight(), C("#E8DCC8"), 0.5f, 0f, true);
            var counterMat = TextureKit.Surface("Shop_Counter", TextureKit.Plastic(), C("#F4F2EE"), 0.6f, 0f, true);
            var floorMat = TextureKit.Surface("Shop_Floor", TextureKit.FloorVinyl(), kind == ShopKind.Bakery ? C("#C9A77E") : C("#ECEAE6"), 0.6f, 0f, true);
            var back = kind switch
            {
                ShopKind.Cosmetics => C("#F7E1E8"), ShopKind.Bakery => C("#F3E6D2"), ShopKind.Phone => C("#E3ECF5"),
                ShopKind.Socks => C("#F1EDE4"), _ => C("#EAF2E4"),
            };

            // shell: floor, back wall, partitions, the low ceiling with its light, the fascia
            Slab(parts, "Floor", new Vector3(0, 0.006f, depth / 2f), new Vector3(width, 0.012f, depth), floorMat, false);
            Box(parts, "Back", new Vector3(0, 1.35f, 0.04f), new Vector3(width - 0.1f, 2.7f, 0.02f), back, false);
            foreach (var sx in new[] { -1f, 1f })
            {
                Slab(parts, "Partition", new Vector3(sx * (hw - 0.05f), h / 2f, depth / 2f), new Vector3(0.1f, h, depth), partition, false);
                Solid(solids, "Partition", new Vector3(sx * (hw - 0.05f), h / 2f, depth / 2f), new Vector3(0.1f, h, depth));
            }
            Slab(parts, "Soffit", new Vector3(0, 2.72f, depth / 2f), new Vector3(width - 0.2f, 0.04f, depth), counterMat, false, false);
            Emissive(parts, "CeilingLight", new Vector3(0, 2.69f, depth / 2f), new Vector3(width - 0.8f, 0.02f, depth * 0.6f), C("#FFF8EC"), 1.3f)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box(parts, "Bulkhead", new Vector3(0, (2.74f + h) / 2f, depth - 0.06f), new Vector3(width, h - 2.74f, 0.12f), C("#2B2E33"), false);
            var texts = Group("Texts", g);
            var sign = Sign(texts, "Fascia", new Vector3(0, 3.02f, depth + 0.03f), new Vector2(width - 0.3f, 0.5f), fascia,
                string.IsNullOrEmpty(sub) ? name : $"{name}  <size=30>{sub}</size>", Color.white, 0, 46, 0.06f);
            Emissive(parts, "LedStrip", new Vector3(0, 2.78f, depth + 0.02f), new Vector3(width - 0.2f, 0.025f, 0.02f), C("#FFF4E0"), 1.4f);

            string[] palette = kind switch
            {
                ShopKind.Cosmetics => new[] { "#F4A7B9", "#E26A9B", "#FFFFFF", "#F2C9A0", "#C7A1E0", "#9ED4C7", "#E8D3A3", "#D8343B" },
                ShopKind.Bakery => new[] { "#C98A4B", "#B5733A", "#E0B27A", "#8C5A2B", "#F0D5A0", "#7A4A23" },
                ShopKind.Phone => new[] { "#2B2E33", "#FFFFFF", "#2F5FD0", "#E26A9B", "#1FA37A", "#F2C230", "#9AA0A6", "#C7A1E0" },
                ShopKind.Socks => new[] { "#FFFFFF", "#2B2E33", "#F2C230", "#E26A9B", "#2F5FD0", "#1FA37A", "#D8343B", "#C9A77E", "#9ED4C7" },
                _ => new[] { "#E5484D", "#F2C230", "#F4A7B9", "#FFFFFF", "#C7A1E0", "#F28C28", "#E26A9B" },
            };
            Color P() => C(palette[rng.Next(palette.Length)]);

            // goods on the walls: shelves of bottles, packs on pegboard, socks in rows, buckets of flowers
            void ShelfRun(float x0, float x1, float z, bool sideWall, float sx)
            {
                for (int s = 0; s < 5; s++)
                {
                    float y = 0.35f + s * 0.42f;
                    if (sideWall)
                        Slab(parts, "Shelf", new Vector3(sx * (hw - 0.3f), y, (x0 + x1) / 2f), new Vector3(0.36f, 0.03f, x1 - x0), shelfMat, false, false);
                    else
                        Slab(parts, "Shelf", new Vector3((x0 + x1) / 2f, y, z), new Vector3(x1 - x0, 0.03f, 0.36f), shelfMat, false, false);
                    for (float t = x0 + 0.1f; t < x1 - 0.08f; t += kind == ShopKind.Cosmetics ? 0.09f : 0.16f)
                    {
                        float ih = kind == ShopKind.Cosmetics ? 0.08f + (float)rng.NextDouble() * 0.12f : 0.14f + (float)rng.NextDouble() * 0.12f;
                        var pos = sideWall ? new Vector3(sx * (hw - 0.3f), y + 0.015f + ih / 2f, t) : new Vector3(t, y + 0.015f + ih / 2f, z);
                        if (kind == ShopKind.Cosmetics && rng.Next(3) > 0)
                            Cylinder(parts, "Bottle", pos, new Vector3(0.05f, ih / 2f, 0.05f), P());
                        else
                            Box(parts, "Item", pos, new Vector3(sideWall ? 0.2f : 0.11f, ih, sideWall ? 0.11f : 0.2f), P(), false);
                    }
                }
            }
            switch (kind)
            {
                case ShopKind.Cosmetics:
                    ShelfRun(-hw + 0.25f, hw - 0.25f, 0.25f, false, 0);
                    ShelfRun(0.5f, depth - 0.7f, 0, true, -1);
                    // a tester table in the middle
                    Slab(parts, "Tester", new Vector3(0.2f, 0.45f, depth / 2f), new Vector3(1.0f, 0.9f, 0.6f), counterMat, false);
                    for (int i = 0; i < 10; i++)
                        Cylinder(parts, "Tester", new Vector3(-0.2f + (i % 5) * 0.2f, 0.95f, depth / 2f - 0.15f + (i / 5) * 0.3f), new Vector3(0.05f, 0.05f, 0.05f), P());
                    Solid(solids, "Tester", new Vector3(0.2f, 0.45f, depth / 2f), new Vector3(1.0f, 0.9f, 0.6f));
                    break;
                case ShopKind.Phone:
                    // pegboards of cases and cables on the back and side walls
                    foreach (var (px, pz, rot) in new[] { (0f, 0.08f, 0f), (-(hw - 0.12f), depth / 2f - 0.2f, 90f) })
                    {
                        var board = Group("Peg", parts);
                        board.localPosition = new Vector3(px, 0, pz);
                        board.localRotation = Quaternion.Euler(0, rot, 0);
                        float bw = rot == 0 ? width - 0.4f : depth - 0.8f;
                        Box(board, "Board", new Vector3(0, 1.45f, 0), new Vector3(bw, 1.9f, 0.03f), C("#F4F4F2"), false);
                        for (float y = 0.75f; y < 2.3f; y += 0.3f)
                        for (float x = -bw / 2 + 0.15f; x < bw / 2 - 0.1f; x += 0.16f)
                            Box(board, "Pack", new Vector3(x, y, 0.04f), new Vector3(0.11f, 0.2f, 0.025f), P(), false);
                    }
                    break;
                case ShopKind.Socks:
                    for (float y = 0.5f; y < 2.4f; y += 0.22f)
                    for (float x = -hw + 0.25f; x < hw - 0.2f; x += 0.13f)
                        Box(parts, "Sock", new Vector3(x, y, 0.09f), new Vector3(0.08f, 0.18f, 0.02f), P(), false);
                    // a bin of three-pair packs out front, the price on a card
                    Slab(parts, "Bin", new Vector3(0, 0.35f, depth - 0.35f), new Vector3(width - 0.6f, 0.7f, 0.5f), shelfMat, false);
                    for (float x = -hw + 0.45f; x < hw - 0.4f; x += 0.14f)
                    for (int k = 0; k < 2; k++)
                        Box(parts, "Pack", new Vector3(x, 0.73f, depth - 0.47f + k * 0.22f), new Vector3(0.1f, 0.04f, 0.18f), P(), false, rng.Next(-15, 15));
                    Solid(solids, "Bin", new Vector3(0, 0.35f, depth - 0.35f), new Vector3(width - 0.6f, 0.7f, 0.5f));
                    Sign(texts, "Price", new Vector3(hw - 0.5f, 1.0f, depth - 0.12f), new Vector2(0.5f, 0.24f), C("#F2C230"), "3켤레 5,000", C("#1E1E1E"), 0, 34, 0.01f);
                    break;
                case ShopKind.Bakery:
                    // racks of bread behind, a glass case of pastries along the front, the coffee machine
                    for (int s = 0; s < 4; s++)
                    {
                        float y = 0.6f + s * 0.45f;
                        Slab(parts, "Rack", new Vector3(0, y, 0.3f), new Vector3(width - 0.4f, 0.03f, 0.45f), shelfMat, false, false);
                        for (float x = -hw + 0.35f; x < hw - 0.3f; x += 0.24f)
                            Sphere(parts, "Bread", new Vector3(x, y + 0.07f, 0.3f + ((float)rng.NextDouble() - 0.5f) * 0.15f),
                                new Vector3(0.2f, 0.11f, 0.14f + (float)rng.NextDouble() * 0.06f), P());
                    }
                    Slab(parts, "Case", new Vector3(-0.35f, 0.45f, depth - 0.35f), new Vector3(width - 1.2f, 0.9f, 0.55f), counterMat, false);
                    Glass(parts, "CaseGlass", new Vector3(-0.35f, 1.12f, depth - 0.35f), new Vector3(width - 1.25f, 0.42f, 0.5f), 0, false);
                    for (float x = -hw + 0.35f; x < hw - 1.0f; x += 0.17f)
                        Sphere(parts, "Pastry", new Vector3(x, 0.96f, depth - 0.38f), new Vector3(0.13f, 0.06f, 0.1f), P());
                    Solid(solids, "Case", new Vector3(-0.35f, 0.6f, depth - 0.35f), new Vector3(width - 1.2f, 1.2f, 0.55f));
                    Box(parts, "CoffeeMachine", new Vector3(hw - 0.5f, 1.12f, 0.75f), new Vector3(0.55f, 0.45f, 0.45f), C("#B8BDC3"), false);
                    Slab(parts, "BackCounter", new Vector3(hw - 0.5f, 0.45f, 0.75f), new Vector3(0.7f, 0.9f, 0.6f), counterMat, false);
                    Sign(texts, "Menu", new Vector3(0, 2.3f, 0.06f), new Vector2(1.8f, 0.5f), C("#2B2E33"),
                        "아메리카노 2,500 · 소금빵 3,000\n<size=26>Americano · Salt bread</size>", Color.white, 0, 34, 0.02f);
                    break;
                case ShopKind.Flowers:
                    // stepped stands of buckets, bunches wrapped in paper on the counter
                    for (int s = 0; s < 3; s++)
                    {
                        float y = 0.25f + s * 0.35f, z = 0.35f + s * 0.0f;
                        Slab(parts, "Step", new Vector3(0, y - 0.1f, z + s * 0.38f), new Vector3(width - 0.4f, 0.2f, 0.38f), shelfMat, false);
                        for (float x = -hw + 0.35f; x < hw - 0.3f; x += 0.3f)
                        {
                            var bucket = new Vector3(x, y + 0.12f, z + s * 0.38f);
                            Cylinder(parts, "Bucket", bucket, new Vector3(0.22f, 0.12f, 0.22f), C("#9AA0A6"));
                            var col = P();
                            for (int k = 0; k < 5; k++)
                                Sphere(parts, "Bloom", bucket + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.18f, 0.3f + (float)rng.NextDouble() * 0.12f,
                                    ((float)rng.NextDouble() - 0.5f) * 0.18f), Vector3.one * 0.09f, col);
                            Box(parts, "Leaves", bucket + new Vector3(0, 0.2f, 0), new Vector3(0.16f, 0.16f, 0.16f), C("#3A7D44"), false);
                        }
                    }
                    break;
            }

            // the front: glass with an open door, or an open counter front for the bakery and the flower stall
            if (kind == ShopKind.Cosmetics || kind == ShopKind.Phone)
            {
                float door = 1.1f, dx = hw - 0.15f - door / 2f;
                float gw = width - 0.2f - door;
                Glass(parts, "Front", new Vector3(-hw + 0.1f + gw / 2f, 1.37f, depth), new Vector3(gw, 2.74f, 0.03f), 0, false);
                Solid(solids, "Front", new Vector3(-hw + 0.1f + gw / 2f, 1.37f, depth), new Vector3(gw, 2.74f, 0.05f));
                Box(parts, "Mullion", new Vector3(dx - door / 2f, 1.37f, depth), new Vector3(0.05f, 2.74f, 0.06f), C("#3A3D42"), false);
                Box(parts, "Sill", new Vector3(0, 0.03f, depth), new Vector3(width - 0.2f, 0.06f, 0.08f), C("#3A3D42"), false);
                // the open sliding leaf parked in front of the glass
                Glass(parts, "DoorLeaf", new Vector3(dx - door, 1.2f, depth + 0.05f), new Vector3(door, 2.4f, 0.02f), 0, false);
                // a poster in the window
                Sign(texts, "Poster", new Vector3(-hw + 0.45f, 1.0f, depth - 0.03f), new Vector2(0.55f, 0.7f),
                    kind == ShopKind.Cosmetics ? C("#E26A9B") : C("#2F5FD0"), kind == ShopKind.Cosmetics ? "1+1\n<size=26>마스크팩</size>" : "충전기\n<size=26>5,000~</size>",
                    Color.white, 0, 44, 0.01f);
            }
            else if (kind == ShopKind.Socks || kind == ShopKind.Flowers)
                Box(parts, "Sill", new Vector3(0, 0.02f, depth), new Vector3(width - 0.2f, 0.04f, 0.06f), C("#3A3D42"), false);

            // the counter and the person behind it
            var look = kind switch
            {
                ShopKind.Bakery => HumanLook.Vendor(rng),
                ShopKind.Socks => HumanLook.SeniorPerson(rng, true),
                _ => HumanLook.Student(rng, rng.Next(3) > 0),
            };
            Vector3 staff;
            if (kind == ShopKind.Bakery) staff = new Vector3(hw - 0.5f, 0, 1.3f);
            else if (kind == ShopKind.Socks) staff = new Vector3(-hw + 0.45f, 0, depth - 0.9f);
            else
            {
                float cx = kind == ShopKind.Flowers ? hw - 0.55f : hw - 0.6f, cz = kind == ShopKind.Flowers ? depth - 0.55f : 0.9f;
                Slab(parts, "Counter", new Vector3(cx, 0.5f, cz), new Vector3(0.9f, 1.0f, 0.5f), counterMat, false);
                Solid(solids, "Counter", new Vector3(cx, 0.5f, cz), new Vector3(0.9f, 1.0f, 0.5f));
                Box(parts, "Terminal", new Vector3(cx - 0.15f, 1.06f, cz), new Vector3(0.16f, 0.1f, 0.22f), C("#2B2E33"), false);
                staff = new Vector3(cx, 0, cz - 0.55f);
                if (kind == ShopKind.Flowers) staff = new Vector3(cx, 0, cz - 0.6f);
            }
            MergeByMaterial(parts, $"Subway_Shop_{kind}");
            var person = Extra(r, $"Clerk_{kind}", look, g.TransformPoint(staff), yaw + (kind == ShopKind.Socks ? 60 : 0), phone: kind == ShopKind.Socks);
            if (kind == ShopKind.Bakery) person.talking = false;
        }

        /// <summary>The unpaid side's shops, the ATMs and the photo booth (the south and west walls, clear of the walk to the gates).</summary>
        static void BuildStationShops(Room r, System.Random rng)
        {
            float d = r.D, w = r.W;
            // south wall, west of the ticket machines
            StationShop(r, ShopKind.Cosmetics, "봄날화장품", "COSMETICS", C("#E26A9B"), new Vector3(-12.25f, 0, -d / 2), 0, 3.0f, 2.2f, rng);
            StationShop(r, ShopKind.Bakery, "보리빵집", "BAKERY · COFFEE", C("#8C5A2B"), new Vector3(-9.15f, 0, -d / 2), 0, 3.0f, 2.2f, rng);
            // south wall, east of the stairs up
            StationShop(r, ShopKind.Phone, "폰마켓", "PHONE ACCESSORIES", C("#2F5FD0"), new Vector3(4.9f, 0, -d / 2), 0, 3.0f, 2.2f, rng);
            StationShop(r, ShopKind.Socks, "양말가게", "SOCKS", C("#1FA37A"), new Vector3(7.6f, 0, -d / 2), 0, 2.2f, 1.6f, rng);
            // west wall by the gate line, a flower stall
            StationShop(r, ShopKind.Flowers, "꽃길", "FLOWERS", C("#3A7D44"), new Vector3(-w / 2, 0, -1.1f), 90, 2.6f, 1.6f, rng);

            // the photo booth in the corner east of the lockers
            InteriorKit.PhotoBooth(r.Root, new Vector3(12.9f, 0, -d / 2 + 0.75f), 0);
            // two ATMs of the game's own bank on the east wall
            for (int i = 0; i < 2; i++)
            {
                var atm = Group("Atm", r.Root);
                atm.SetPositionAndRotation(new Vector3(w / 2 - 0.35f, 0, -7.9f + i * 0.85f), Quaternion.Euler(0, -90, 0));
                Box(atm, "Body", new Vector3(0, 0.8f, 0), new Vector3(0.72f, 1.6f, 0.6f), C("#E8EAED"), true);
                Box(atm, "Fascia", new Vector3(0, 1.72f, 0.02f), new Vector3(0.72f, 0.24f, 0.62f), C("#5BAA2E"), false);
                Emissive(atm, "Screen", new Vector3(0, 1.25f, 0.31f), new Vector3(0.36f, 0.28f, 0.01f), C("#BFE3FF"), 1.1f);
                Box(atm, "Keypad", new Vector3(0, 0.98f, 0.33f), new Vector3(0.3f, 0.04f, 0.16f), C("#2B2E33"), false, 0);
                Box(atm, "Slot", new Vector3(0.18f, 1.08f, 0.31f), new Vector3(0.12f, 0.02f, 0.01f), C("#1E1E1E"), false);
            }
            Sign(r.Root, "AtmSign", new Vector3(w / 2 - 0.06f, 2.35f, -7.45f), new Vector2(1.7f, 0.36f), C("#5BAA2E"), "새싹은행 ATM · 365", Color.white, -90, 40, 0.04f);
        }

        /// <summary>The paid side: square columns with ads, the restrooms on the west wall, a drinks machine and the AED.</summary>
        static void BuildPaidSide(Room r, Color line4, System.Random rng)
        {
            var root = r.Root;
            var clad = TextureKit.Surface("Column_Station", TextureKit.Plastic(), C("#E4E6E8"), 0.6f);
            foreach (var x in new[] { -10f, -3.5f, 3.5f, 10f })
            {
                var col = Group("Column", root);
                col.localPosition = new Vector3(x, 0, 4.5f);
                Slab(col, "Shaft", new Vector3(0, r.H / 2f, 0), new Vector3(0.7f, r.H, 0.7f), clad);
                Box(col, "Band", new Vector3(0, 2.55f, 0), new Vector3(0.72f, 0.18f, 0.72f), line4, false);
                Box(col, "Base", new Vector3(0, 0.06f, 0), new Vector3(0.74f, 0.12f, 0.74f), C("#6E6C68"), false);
                string[] ads = { "#E26A9B", "#1F2F55", "#1FA37A", "#F28C28" };
                string[] words = { "K-POP 콘서트\n<size=26>12.20 · 잠실</size>", "서울 야경 투어\n<size=26>Night Tour</size>", "한강 자전거\n<size=26>Han River</size>", "떡볶이 축제\n<size=26>Tteokbokki fest</size>" };
                int k = rng.Next(ads.Length);
                // one ad on the side facing the gates, one facing the stairs
                LightBox(col, new Vector3(0, 1.55f, -0.37f), 180, new Vector2(0.62f, 1.0f), C(ads[k]), words[k], Color.white, 40);
                LightBox(col, new Vector3(0, 1.55f, 0.37f), 0, new Vector2(0.62f, 1.0f), C(ads[(k + 1) % ads.Length]), words[(k + 1) % ads.Length], Color.white, 40);
            }

            // restrooms in the west wall
            var wc = Group("Restrooms", root);
            wc.SetPositionAndRotation(new Vector3(-r.W / 2, 0, 5.2f), Quaternion.Euler(0, 90, 0));
            Box(wc, "Recess", new Vector3(0, 1.2f, 0.02f), new Vector3(1.8f, 2.4f, 0.04f), C("#2B3440"), false);
            Box(wc, "Frame", new Vector3(0, 2.43f, 0.06f), new Vector3(1.9f, 0.08f, 0.08f), C("#9AA0A6"), false);
            foreach (var sx in new[] { -0.93f, 0.93f })
                Box(wc, "Jamb", new Vector3(sx, 1.2f, 0.06f), new Vector3(0.06f, 2.4f, 0.08f), C("#9AA0A6"), false);
            // the corridor in, turning out of sight behind a privacy wall
            Box(wc, "Privacy", new Vector3(0.2f, 1.1f, -0.9f), new Vector3(1.3f, 2.2f, 0.06f), C("#DADCDE"), false);
            Sign(wc, "Sign", new Vector3(0, 2.75f, 0.06f), new Vector2(1.9f, 0.42f), C("#2B2E33"), "화장실 · Restrooms   <color=#6FA8DC>남</color> · <color=#E26A9B>여</color>", Color.white, 0, 40, 0.05f);
            Sign(wc, "Nursing", new Vector3(1.45f, 1.7f, 0.04f), new Vector2(0.5f, 0.5f), C("#F7E1E8"), "수유실\n<size=24>Nursing</size>", C("#8C3B6B"), 0, 34, 0.02f);
            var blocker = new GameObject("RestroomBlocker") { layer = 2 };
            blocker.transform.SetParent(wc, false);
            blocker.transform.localPosition = new Vector3(0, 1.2f, -0.1f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(1.8f, 2.4f, 0.1f);

            // a drinks machine and the AED cabinet on the east wall
            VendingMachine(root, new Vector3(r.W / 2 - 0.45f, 0, 3.0f), -90, C("#D8343B"), "음료 · Drinks", rng);
            var aed = Group("Aed", root);
            aed.SetPositionAndRotation(new Vector3(r.W / 2 - 0.08f, 1.3f, 4.6f), Quaternion.Euler(0, -90, 0));
            Box(aed, "Cabinet", Vector3.zero, new Vector3(0.55f, 0.7f, 0.16f), C("#F4F4F2"), true);
            Box(aed, "Band", new Vector3(0, 0.28f, 0.085f), new Vector3(0.55f, 0.14f, 0.01f), C("#1FA37A"), false);
            Glass(aed, "Door", new Vector3(0, -0.05f, 0.085f), new Vector3(0.45f, 0.45f, 0.01f), 0, false);
            Sign(aed, "Label", new Vector3(0, 0.55f, 0.04f), new Vector2(0.7f, 0.3f), C("#1FA37A"), "AED\n<size=22>자동심장충격기</size>", Color.white, 0, 34, 0.02f);
        }

        /// <summary>
        /// The people of the concourse: down the stairs from 명동길, through the gates (any lane, tapped) and down to
        /// the platform for either direction, and the other way; some stop at a shop or the machines on the way.
        /// </summary>
        static void BuildConcourseFlow(Room r, GateLane[] lanes)
        {
            var flow = new GameObject("ConcourseFlow").AddComponent<CrowdFlow>();
            flow.transform.SetParent(r.Root, false);
            flow.lanes = lanes;
            flow.count = 11;
            flow.seed = 52;
            flow.speed = 1.3f;
            flow.gap = new Vector2(1.5f, 7f);
            Vector3 V(float x, float z, float y = 0f) => new(x, y, z);
            var stairTop = V(0, -11.15f, 1.12f);
            var stairFoot = V(0, -9.3f);
            float gS = -0.35f, gN = 1.55f;
            // part-way down a flight to the platform (out of sight round the shaft's walls)
            Vector3 Down(float sx) => V(sx * 6f, 12.6f, -2.0f);
            var routes = new List<CrowdFlow.Route>();
            foreach (var sx in new[] { -1f, 1f })
            {
                // in from the street, to the stairs for this direction
                routes.Add(new CrowdFlow.Route
                {
                    points = new[] { stairTop, stairFoot, V(sx * 0.6f, -6.5f), V(sx * 2.8f, -2.2f), V(0, gS), V(0, gN), V(sx * 5f, 3.4f), V(sx * 6f, 7.3f), V(sx * 6f, 8.9f), Down(sx) },
                    gateAt = 4,
                });
                // up from the platform and out to the street
                routes.Add(new CrowdFlow.Route
                {
                    points = new[] { Down(sx), V(sx * 6f, 8.9f), V(sx * 6f, 7.3f), V(sx * 5f, 3.4f), V(sx * 2.8f, 2.6f), V(0, gN), V(0, gS), V(sx * 0.9f, -5.0f), stairFoot, stairTop },
                    gateAt = 5,
                });
            }
            // a stop at the bakery or the ticket machines on the way in
            routes.Add(new CrowdFlow.Route
            {
                points = new[] { stairTop, stairFoot, V(-2.0f, -7.6f), V(-9.1f, -6.2f), V(-4.0f, -2.4f), V(0, gS), V(0, gN), V(-5f, 3.4f), V(-6f, 7.3f), V(-6f, 8.9f), Down(-1) },
                waits = new[] { 0f, 0f, 0f, 9f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                gateAt = 5, weight = 0.6f,
            });
            routes.Add(new CrowdFlow.Route
            {
                points = new[] { stairTop, stairFoot, V(-3.4f, -8.0f), V(-6.5f, -7.75f), V(-2.5f, -2.4f), V(0, gS), V(0, gN), V(5f, 3.4f), V(6f, 7.3f), V(6f, 8.9f), Down(1) },
                waits = new[] { 0f, 0f, 0f, 7f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                gateAt = 5, weight = 0.6f,
            });
            // out via the phone shop, or a look at the socks
            routes.Add(new CrowdFlow.Route
            {
                points = new[] { Down(1), V(6f, 8.9f), V(6f, 7.3f), V(5f, 3.4f), V(3.2f, 2.6f), V(0, gN), V(0, gS), V(4.6f, -6.1f), V(1.2f, -8.4f), stairFoot, stairTop },
                waits = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 8f, 0f, 0f, 0f },
                gateAt = 5, weight = 0.5f,
            });
            routes.Add(new CrowdFlow.Route
            {
                points = new[] { stairTop, stairFoot, V(3.2f, -7.4f), V(7.4f, -6.7f), V(3.0f, -6.0f), stairFoot, stairTop },
                waits = new[] { 0f, 0f, 0f, 10f, 0f, 0f, 0f },
                weight = 0.35f,
            });
            // to the restrooms on the paid side
            routes.Add(new CrowdFlow.Route
            {
                points = new[] { Down(-1), V(-6f, 8.9f), V(-6f, 7.3f), V(-9.0f, 6.2f), V(-12.8f, 5.2f), V(-13.95f, 5.2f) },
                weight = 0.3f,
            });
            flow.routes = routes.ToArray();
        }

        // ------------------------------------------------------------------- the Line 4 train

        const float CarW = 3.12f, CarLen = 19.5f;
        static readonly float[] CarDoors = { -7.5f, -2.5f, 2.5f, 7.5f };

        class TrainMats
        {
            public Material Steel, Lining, Floor, Seat, Priority, SeatBase, Roof, Under, Rail, Rack;
            public Color Line;
        }

        static TrainMats MakeTrainMats(Color line4) => new()
        {
            Steel = TextureKit.Surface("Car_Steel", TextureKit.Metal(), C("#D3D8DD"), 0.8f, 0.85f, true),
            Lining = TextureKit.Surface("Car_Lining", TextureKit.Plastic(), C("#EEF0F2"), 0.7f, 0f, true),
            Floor = TextureKit.Surface("Car_Floor", TextureKit.FloorVinyl(), C("#8E9399"), 0.5f, 0f, true),
            Seat = TextureKit.Surface("Car_Seat", TextureKit.FabricWeave(), C("#3E6FB5"), 0.3f, 0f, true),
            Priority = TextureKit.Surface("Car_SeatPriority", TextureKit.FabricWeave(), C("#D8586A"), 0.3f, 0f, true),
            SeatBase = TextureKit.Surface("Car_SeatBase", TextureKit.Metal(), C("#B9BEC4"), 0.6f, 0.7f, true),
            Roof = TextureKit.Surface("Car_Roof", TextureKit.Metal(), C("#A9AEB4"), 0.5f, 0.6f, true),
            Under = TextureKit.Surface("Car_Under", TextureKit.Metal(), C("#3A3D42"), 0.3f, 0.4f, true),
            Rail = Mats.Lit(C("#C9CED4"), 0.7f, 0.8f),
            Rack = Mats.Lit(C("#B9BEC4"), 0.6f, 0.7f),
            Line = line4,
        };

        /// <summary>
        /// One Line 4 car in its own frame (x along the car, z across, the platform side at -z, floor top at y = 0):
        /// a stainless body with four door openings a side, windows, the sky-blue band, roof and underframe with
        /// two bogies; inside, long benches (priority seats at the ends), poles, grab rails with straps, racks, ads,
        /// door-top displays and lights; invisible walls and a floor to stand on. The doors on the platform side
        /// whose index is in <paramref name="opening"/> get sliding leaves of their own (listed for the train);
        /// the rest are part of the body. Returns the spots just inside those doors.
        /// </summary>
        static List<Transform> BuildCar(Transform car, TrainMats m, int number, bool[] opening, bool frontCab, bool displays, System.Random rng,
            List<Transform> leftLeaves, List<Transform> rightLeaves)
        {
            var shell = Group("Shell", car);
            var solids = Group("Solids", car);
            var texts = Group("Texts", car);
            float half = CarLen / 2f, hw = CarW / 2f;
            const float doorW = 1.3f, doorH = 1.95f, winLo = 0.98f, winHi = 1.86f, bodyTop = 2.42f, skirt = -0.32f;
            var insideSpots = new List<Transform>();

            // ---- floor, ceiling, ends
            Slab(shell, "Floor", new Vector3(0, -0.05f, 0), new Vector3(CarLen, 0.1f, CarW - 0.1f), m.Floor, false);
            Solid(solids, "Floor", new Vector3(0, -0.06f, 0), new Vector3(CarLen, 0.12f, CarW));
            Slab(shell, "Ceiling", new Vector3(0, 2.27f, 0), new Vector3(CarLen - 0.1f, 0.04f, CarW - 0.16f), m.Lining, false, false);
            foreach (var sz in new[] { -0.62f, 0.62f })
                Emissive(shell, "LightStrip", new Vector3(0, 2.245f, sz), new Vector3(CarLen - 0.8f, 0.012f, 0.14f), C("#F6F9FF"), 1.6f)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box(shell, "AirDiffuser", new Vector3(0, 2.24f, 0), new Vector3(CarLen - 1.0f, 0.02f, 0.34f), C("#DADDE1"), false);
            foreach (var ex in new[] { -1f, 1f })
            {
                float x = ex * (half - 0.04f);
                Slab(shell, "EndWall", new Vector3(x, (skirt + bodyTop) / 2f, 0), new Vector3(0.08f, bodyTop - skirt, CarW), m.Steel, false);
                Box(shell, "EndLining", new Vector3(x - ex * 0.045f, 1.1f, 0), new Vector3(0.01f, 2.2f, CarW - 0.12f), C("#EEF0F2"), false);
                // the gangway door to the next car, glazed
                Box(shell, "GangwayDoor", new Vector3(x - ex * 0.052f, 0.98f, 0), new Vector3(0.012f, 1.95f, 0.9f), C("#B9BEC4"), false);
                Emissive(shell, "GangwayGlass", new Vector3(x - ex * 0.06f, 1.35f, 0), new Vector3(0.008f, 0.8f, 0.6f), C("#8FA3B5"), 0.4f);
                Solid(solids, "EndWall", new Vector3(x, 1.2f, 0), new Vector3(0.1f, 2.5f, CarW));
            }

            // ---- the two sides
            foreach (var s in new[] { -1f, 1f })
            {
                float zo = s * hw;              // the outer skin
                float zi = s * (hw - 0.075f);   // the inner lining
                var cuts = CarDoors.Select(o => (a: o - doorW / 2f, b: o + doorW / 2f)).ToList();
                // wall pieces between the door openings and the ends
                var pieces = new List<(float a, float b)>();
                float at = -half;
                foreach (var c in cuts)
                {
                    pieces.Add((at, c.a));
                    at = c.b;
                }
                pieces.Add((at, half));
                foreach (var (a, b) in pieces)
                {
                    float len = b - a, cx = (a + b) / 2f;
                    Slab(shell, "Lower", new Vector3(cx, (skirt + winLo) / 2f, zo), new Vector3(len, winLo - skirt, 0.05f), m.Steel, false);
                    Slab(shell, "Upper", new Vector3(cx, (winHi + bodyTop) / 2f, zo), new Vector3(len, bodyTop - winHi, 0.05f), m.Steel, false);
                    Box(shell, "LiningLow", new Vector3(cx, winLo / 2f, zi), new Vector3(len, winLo, 0.02f), C("#EEF0F2"), false);
                    Box(shell, "LiningHigh", new Vector3(cx, (winHi + 2.25f) / 2f, zi), new Vector3(len, 2.25f - winHi, 0.02f), C("#EEF0F2"), false);
                    // windows: one or two panes per piece, pillars between them
                    int panes = len > 2.6f ? 2 : 1;
                    float pillar = 0.24f, paneW = (len - pillar * (panes + 1)) / panes;
                    for (int k = 0; k <= panes; k++)
                    {
                        float px = a + pillar / 2f + k * (paneW + pillar);
                        Slab(shell, "Pillar", new Vector3(px, (winLo + winHi) / 2f, zo), new Vector3(pillar, winHi - winLo, 0.05f), m.Steel, false);
                    }
                    for (int k = 0; k < panes; k++)
                    {
                        float px = a + pillar + paneW / 2f + k * (paneW + pillar);
                        Glass(shell, "Window", new Vector3(px, (winLo + winHi) / 2f, zo), new Vector3(paneW, winHi - winLo, 0.02f), 0, false);
                        // the dark rubber round the glass
                        Box(shell, "WindowSeal", new Vector3(px, winHi - 0.02f, zo + s * 0.03f), new Vector3(paneW, 0.03f, 0.01f), C("#2B2E33"), false);
                        Box(shell, "WindowSeal", new Vector3(px, winLo + 0.02f, zo + s * 0.03f), new Vector3(paneW, 0.03f, 0.01f), C("#2B2E33"), false);
                    }
                    // the sky-blue band below the windows and the thin dark line under it
                    Box(shell, "Band", new Vector3(cx, 0.82f, zo + s * 0.03f), new Vector3(len, 0.16f, 0.01f), m.Line, false);
                    Box(shell, "BandLine", new Vector3(cx, 0.72f, zo + s * 0.03f), new Vector3(len, 0.03f, 0.01f), C("#1F2F55"), false);
                    Solid(solids, "Wall", new Vector3(cx, 1.1f, s * (hw - 0.02f)), new Vector3(len, 2.6f, 0.1f));
                }
                // over and round each door opening
                for (int di = 0; di < cuts.Count; di++)
                {
                    float o = CarDoors[di];
                    Slab(shell, "DoorHeader", new Vector3(o, (doorH + bodyTop) / 2f, zo), new Vector3(doorW, bodyTop - doorH, 0.05f), m.Steel, false);
                    Box(shell, "HeaderLining", new Vector3(o, (doorH + 2.25f) / 2f, zi), new Vector3(doorW, 2.25f - doorH, 0.02f), C("#EEF0F2"), false);
                    Slab(shell, "Sill", new Vector3(o, skirt / 2f, zo), new Vector3(doorW, -skirt, 0.05f), m.Steel, false);
                    Box(shell, "SillPlate", new Vector3(o, -0.005f, zo - s * 0.08f), new Vector3(doorW, 0.012f, 0.18f), C("#B9BEC4"), false);
                    Box(shell, "SillEdge", new Vector3(o, 0.002f, zo - s * 0.2f), new Vector3(doorW, 0.006f, 0.05f), C("#F2C230"), false);
                    foreach (var e in new[] { -1f, 1f })
                        Box(shell, "DoorSeal", new Vector3(o + e * doorW / 2f, doorH / 2f, zo + s * 0.02f), new Vector3(0.04f, doorH, 0.03f), C("#2B2E33"), false);
                    Emissive(shell, "DoorLamp", new Vector3(o + doorW / 2f + 0.12f, 2.1f, zo + s * 0.03f), new Vector3(0.06f, 0.06f, 0.01f), C("#FF5A3C"), 1.1f);
                    // inside over the door: the display, the route map beside it
                    Box(shell, "Display", new Vector3(o, 2.1f, zi - s * 0.04f), new Vector3(0.95f, 0.2f, 0.05f), C("#1E1E1E"), false);
                    Emissive(shell, "DisplayGlow", new Vector3(o, 2.1f, zi - s * 0.07f), new Vector3(0.86f, 0.12f, 0.005f), C("#3A2A12"), 0.8f);
                    if (displays && s < 0)
                        WorldText.Create(texts, "DisplayText", "이번 역 <color=#FFB547>명동</color> Myeong-dong", new Vector3(o, 2.1f, zi - s * 0.075f), new Vector2(0.84f, 0.11f), 40,
                            Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, s < 0 ? 180 : 0, 0));
                    // the poles either side of the door, floor to ceiling
                    foreach (var e in new[] { -1f, 1f })
                        Cylinder(shell, "Pole", new Vector3(o + e * (doorW / 2f + 0.12f), 1.12f, s * (hw - 0.42f)), new Vector3(0.035f, 1.13f, 0.035f), C("#C9CED4"));

                    bool opens = s < 0 && opening != null && di < opening.Length && opening[di];
                    // the leaves: stainless, a tall window each, the band across them
                    foreach (var e in new[] { -1f, 1f })
                    {
                        var leaf = Group(e < 0 ? "LeafL" : "LeafR", opens ? car : shell);
                        leaf.localPosition = new Vector3(o + e * doorW / 4f, 0, zo - s * 0.045f);
                        float lw = doorW / 2f - 0.01f;
                        Box(leaf, "Low", new Vector3(0, 0.5f, 0), new Vector3(lw, 1.0f, 0.03f), C("#CDD2D7"), false);
                        Box(leaf, "High", new Vector3(0, 1.86f, 0), new Vector3(lw, 0.18f, 0.03f), C("#CDD2D7"), false);
                        Box(leaf, "Side", new Vector3(e * (lw / 2f - 0.05f), 1.38f, 0), new Vector3(0.1f, 0.76f, 0.03f), C("#CDD2D7"), false);
                        Box(leaf, "Side", new Vector3(-e * (lw / 2f - 0.05f), 1.38f, 0), new Vector3(0.1f, 0.76f, 0.03f), C("#CDD2D7"), false);
                        Glass(leaf, "Glass", new Vector3(0, 1.38f, 0), new Vector3(lw - 0.2f, 0.76f, 0.012f), 0, false);
                        Box(leaf, "Band", new Vector3(0, 0.82f, s * 0.02f), new Vector3(lw, 0.16f, 0.01f), m.Line, false);
                        if (opens)
                        {
                            MergeMoving(leaf, $"Car{number}_{leaf.name}_{di}");
                            (e < 0 ? leftLeaves : rightLeaves).Add(leaf);
                        }
                    }
                    if (opens)
                    {
                        var spot = Group($"Inside_{di}", car);
                        spot.localPosition = new Vector3(o, 0.02f, s * (hw - 0.55f));
                        spot.localRotation = Quaternion.Euler(0, s < 0 ? 180 : 0, 0);
                        insideSpots.Add(spot);
                    }
                }

                // ---- the benches between the doors and the priority seats at the ends
                var bays = new List<(float a, float b, bool priority)>();
                bays.Add((-half + 0.1f, CarDoors[0] - doorW / 2f - 0.18f, true));
                for (int i = 0; i < CarDoors.Length - 1; i++)
                    bays.Add((CarDoors[i] + doorW / 2f + 0.18f, CarDoors[i + 1] - doorW / 2f - 0.18f, false));
                bays.Add((CarDoors[^1] + doorW / 2f + 0.18f, half - 0.1f, true));
                foreach (var (a, b, priority) in bays)
                {
                    float len = b - a, cx = (a + b) / 2f;
                    var seat = priority ? m.Priority : m.Seat;
                    Slab(shell, "Cushion", new Vector3(cx, 0.405f, s * (hw - 0.31f)), new Vector3(len, 0.08f, 0.46f), seat, false);
                    Slab(shell, "Backrest", new Vector3(cx, 0.72f, s * (hw - 0.1f)), new Vector3(len, 0.5f, 0.07f), seat, false);
                    Slab(shell, "SeatBase", new Vector3(cx, 0.18f, s * (hw - 0.27f)), new Vector3(len, 0.36f, 0.34f), m.SeatBase, false);
                    Solid(solids, "Bench", new Vector3(cx, 0.22f, s * (hw - 0.3f)), new Vector3(len, 0.44f, 0.5f));
                    // seat dividers every place, a pole splitting the long benches 3 + 4
                    int places = Mathf.Max(1, Mathf.RoundToInt(len / 0.48f));
                    for (int k = 1; k < places; k++)
                        Box(shell, "Divider", new Vector3(a + k * len / places, 0.455f, s * (hw - 0.31f)), new Vector3(0.012f, 0.02f, 0.4f), C("#2C4F86"), false);
                    if (!priority)
                        Cylinder(shell, "Pole", new Vector3(a + 3 * len / 7f, 1.12f, s * (hw - 0.6f)), new Vector3(0.035f, 1.13f, 0.035f), C("#C9CED4"));
                    // the grab rail and its straps, the rack over the seats, ads along the cove
                    var rail = Cylinder(shell, "GrabRail", new Vector3(cx, 1.86f, s * (hw - 0.62f)), new Vector3(0.03f, len / 2f, 0.03f), C("#C9CED4"));
                    rail.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    for (float sx = a + 0.22f; sx < b - 0.1f; sx += 0.42f)
                    {
                        Box(shell, "Strap", new Vector3(sx, 1.76f, s * (hw - 0.62f)), new Vector3(0.025f, 0.18f, 0.012f), C("#E9E4D8"), false);
                        Box(shell, "Handle", new Vector3(sx, 1.64f, s * (hw - 0.62f)), new Vector3(0.11f, 0.08f, 0.02f), C("#F2C230"), false);
                    }
                    Box(shell, "Rack", new Vector3(cx, 1.96f, s * (hw - 0.27f)), new Vector3(len, 0.025f, 0.4f), C("#B9BEC4"), false);
                    for (float ax = a + 0.6f; ax < b - 0.5f; ax += 1.1f)
                        Box(shell, "Ad", new Vector3(ax, 2.12f, s * (hw - 0.1f)), new Vector3(0.8f, 0.2f, 0.01f),
                            C(new[] { "#E26A9B", "#2F5FD0", "#1FA37A", "#F28C28", "#FFFFFF", "#C7A1E0" }[rng.Next(6)]), false);
                    if (priority)
                        Box(shell, "PrioritySign", new Vector3(cx, 1.15f, s * (hw - 0.075f)), new Vector3(0.5f, 0.16f, 0.01f), C("#D8586A"), false);
                }
            }

            // ---- roof and underframe
            Slab(shell, "Roof", new Vector3(0, bodyTop + 0.08f, 0), new Vector3(CarLen, 0.16f, CarW), m.Roof, false);
            Slab(shell, "RoofUpper", new Vector3(0, bodyTop + 0.22f, 0), new Vector3(CarLen, 0.12f, CarW - 0.5f), m.Roof, false);
            Slab(shell, "RoofTop", new Vector3(0, bodyTop + 0.32f, 0), new Vector3(CarLen, 0.08f, CarW - 1.2f), m.Roof, false);
            foreach (var x in new[] { -5.5f, 5.5f })
                Slab(shell, "AirConditioner", new Vector3(x, bodyTop + 0.5f, 0), new Vector3(2.6f, 0.3f, 1.7f), m.Steel, false);
            Box(shell, "Gutter", new Vector3(0, bodyTop + 0.005f, -hw - 0.01f), new Vector3(CarLen, 0.03f, 0.02f), C("#2B2E33"), false);
            Slab(shell, "Underframe", new Vector3(0, skirt - 0.2f, 0), new Vector3(CarLen - 1.0f, 0.4f, CarW - 0.5f), m.Under, false);
            for (float x = -4.2f; x <= 4.2f; x += 2.1f)
                Slab(shell, "Equipment", new Vector3(x, skirt - 0.32f, -0.4f + (x % 2 == 0 ? 0.6f : 0)), new Vector3(1.6f, 0.36f, 1.2f), m.Under, false);
            foreach (var bx in new[] { -6.9f, 6.9f })
            {
                Slab(shell, "Bogie", new Vector3(bx, -0.55f, 0), new Vector3(2.5f, 0.28f, 2.1f), m.Under, false);
                foreach (var wx in new[] { -1.05f, 1.05f })
                foreach (var wz in new[] { -0.72f, 0.72f })
                {
                    var wheel = Cylinder(shell, "Wheel", new Vector3(bx + wx, -0.47f, wz), new Vector3(0.86f, 0.035f, 0.86f), C("#4A4D52"));
                    wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }
            // a side destination display on the platform side
            Box(shell, "SideSign", new Vector3(-half + 2.4f, 2.12f, -hw - 0.01f), new Vector3(0.9f, 0.18f, 0.02f), C("#141414"), false);
            WorldText.Create(texts, "SideSignText", "<color=#FFB547>④ 사당</color> Sadang", new Vector3(-half + 2.4f, 2.12f, -hw - 0.025f), new Vector2(0.84f, 0.14f), 40,
                Color.white, true, TextAnchor.MiddleCenter, Quaternion.identity);
            Box(shell, "CarNumber", new Vector3(half - 1.4f, 2.18f, -hw - 0.01f), new Vector3(0.36f, 0.12f, 0.01f), C("#1F2F55"), false);

            // ---- the cab at the front of the train
            if (frontCab)
            {
                float fx = half;
                Slab(shell, "CabNose", new Vector3(fx + 0.3f, (skirt + bodyTop) / 2f, 0), new Vector3(0.6f, bodyTop - skirt, CarW), m.Steel, false);
                Box(shell, "CabBand", new Vector3(fx + 0.605f, 0.82f, 0), new Vector3(0.01f, 0.3f, CarW), m.Line, false);
                Emissive(shell, "Windshield", new Vector3(fx + 0.61f, 1.55f, 0), new Vector3(0.01f, 0.75f, CarW - 0.5f), C("#141B26"), 0.35f);
                Box(shell, "WindshieldPillar", new Vector3(fx + 0.615f, 1.55f, 0.3f), new Vector3(0.01f, 0.78f, 0.06f), C("#2B2E33"), false);
                foreach (var hz in new[] { -1.15f, 1.15f })
                {
                    Emissive(shell, "Headlight", new Vector3(fx + 0.615f, 0.55f, hz), new Vector3(0.01f, 0.12f, 0.22f), C("#FFFBEA"), 3f);
                    Emissive(shell, "Taillight", new Vector3(fx + 0.615f, 0.55f, hz * 0.75f), new Vector3(0.01f, 0.1f, 0.12f), C("#5A1212"), 0.6f);
                }
                Box(shell, "DestinationBox", new Vector3(fx + 0.612f, 2.15f, 0), new Vector3(0.01f, 0.22f, 1.3f), C("#141414"), false);
                WorldText.Create(texts, "Destination", "<color=#FFB547>④ 사당</color>  Sadang", new Vector3(fx + 0.62f, 2.15f, 0), new Vector2(1.2f, 0.18f), 48,
                    Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, -90, 0));
                Slab(shell, "AntiClimber", new Vector3(fx + 0.68f, -0.1f, 0), new Vector3(0.18f, 0.25f, CarW - 0.4f), m.Under, false);
                Box(shell, "Coupler", new Vector3(fx + 0.85f, -0.35f, 0), new Vector3(0.4f, 0.16f, 0.2f), C("#2B2E33"), false);
            }
            else
            {
                Box(shell, "Gangway", new Vector3(half + 0.25f, 1.05f, 0), new Vector3(0.52f, 2.1f, 1.3f), C("#2B2E33"), false);
            }
            Box(shell, "GangwayRear", new Vector3(-half - 0.25f, 1.05f, 0), new Vector3(0.52f, 2.1f, 1.3f), C("#2B2E33"), false);

            // lights inside, then everything that never moves on its own merged into a few meshes
            // low in the car, so the ceiling doesn't flare round them
            PointLight(car, new Vector3(-4.5f, 1.3f, 0), 6.5f, 0.6f, C("#F4F7FF")).shadows = LightShadows.None;
            PointLight(car, new Vector3(4.5f, 1.3f, 0), 6.5f, 0.6f, C("#F4F7FF")).shadows = LightShadows.None;
            MergeMoving(shell, $"Car{number}");
            return insideSpots;
        }

        /// <summary>
        /// The train at the platform: three cars of a Line 4 set (the third, fourth and fifth, the ones along the
        /// platform), its doors lined up with the screen doors, coming in from the tunnel at the west end. Returns
        /// the spots inside its opening doors (in the train's final, stopped position).
        /// </summary>
        static (StationTrain train, List<Transform> inside, Transform body) BuildPlatformTrain(Transform p, float z0, float[] psdX, Color line4,
            List<Transform> psdLeft, List<Transform> psdRight, System.Random rng)
        {
            var m = MakeTrainMats(line4);
            var train = Group("Train", p);
            var body = Group("Body", train);
            float tz = z0 + 0.15f + CarW / 2f;
            var carLeft = new List<Transform>();
            var carRight = new List<Transform>();
            var inside = new List<Transform>();
            foreach (var (number, cx) in new[] { (3, -22.5f), (4, -2.5f), (5, 17.5f) })
            {
                var car = Group($"Car{number}", body);
                car.localPosition = new Vector3(cx, 0, tz);
                var opening = CarDoors.Select(o => psdX.Any(x => Mathf.Abs(cx + o - x) < 0.1f)).ToArray();
                inside.AddRange(BuildCar(car, m, number, opening, number == 5, number == 4, rng, carLeft, carRight));
                // a few people already on board, sitting
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0 ? -1f : 1f);
                    float bx = new[] { -5.0f, -0.4f, 4.6f, 0.9f }[i] + (float)rng.NextDouble() * 0.6f;
                    var look = HumanLook.Random(rng, Crowd.Street);
                    var pos = car.TransformPoint(new Vector3(bx, 0, sx * (CarW / 2f - 0.22f)));
                    var rig = Extra(null, $"Rider{number}_{i}", look, pos, sx < 0 ? 0 : 180, sitting: true, phone: rng.Next(3) > 0, seat: 0.445f, parent: car);
                }
            }
            var set = train.gameObject.AddComponent<StationTrain>();
            set.body = body;
            set.stopPosition = Vector3.zero;
            set.runIn = 90f;
            set.leavesLeft = psdLeft.Concat(carLeft).ToArray();
            set.leavesRight = psdRight.Concat(carRight).ToArray();
            return (set, inside, body);
        }

        /// <summary>
        /// The car the player rides in after boarding, a space of its own further east: the same car seen from
        /// inside, doors closed, the dark tunnel going past the windows with its lamps and ribs, the car swaying a
        /// little, the next stop on the displays. Returns where the player stands.
        /// </summary>
        static Transform BuildCarRide(Room r, Color line4, System.Random rng)
        {
            var m = MakeTrainMats(line4);
            var origin = new Vector3(PlatformX + 70f, 0, 0);
            var ride = Group("CarRide", r.Root);
            ride.position = origin;
            var car = Group("Car4_Ride", ride);
            car.localPosition = Vector3.zero;
            var unusedL = new List<Transform>();
            var unusedR = new List<Transform>();
            BuildCar(car, m, 40, null, false, false, rng, unusedL, unusedR);
            // the next stop on every display over the doors
            foreach (var s in new[] { -1f, 1f })
                foreach (var o in CarDoors)
                    WorldText.Create(car, "DisplayText", "이번 역 <color=#FFB547>동작</color> Dongjak", new Vector3(o, 2.1f, s * (CarW / 2f - 0.16f)), new Vector2(0.84f, 0.11f), 40,
                        Color.white, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, s < 0 ? 180 : 0, 0));
            // the tunnel: dark walls a metre out, lamps and ribs that slide past
            var tunnel = Group("Tunnel", ride);
            var wall = TextureKit.Surface("Tunnel", TextureKit.WallPaint(), C("#4A4846"), 0.3f);
            foreach (var s in new[] { -1f, 1f })
                Slab(tunnel, "TunnelWall", new Vector3(0, 1.0f, s * 2.7f), new Vector3(60f, 5f, 0.2f), wall, false);
            var passing = new List<Transform>();
            for (int i = 0; i < 16; i++)
            {
                var g = Group("Passing", tunnel);
                g.localPosition = new Vector3(-20f + i * 2.5f, 0, 0);
                foreach (var s in new[] { -1f, 1f })
                {
                    if (i % 4 == 0)
                        Emissive(g, "Lamp", new Vector3(0, 1.9f, s * 2.58f), new Vector3(0.5f, 0.12f, 0.04f), C("#FFE9C2"), 3.2f)
                            .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Box(g, "Rib", new Vector3(0, 1.0f, s * 2.6f), new Vector3(0.18f, 4.6f, 0.04f), C("#3A3836"), false);
                    Box(g, "Cable", new Vector3(0, 1.2f + (i % 3) * 0.1f, s * 2.57f), new Vector3(2.5f, 0.05f, 0.04f), C("#2B2A28"), false);
                }
                passing.Add(g);
            }
            var tl = tunnel.gameObject.AddComponent<TunnelLights>();
            tl.passing = passing.ToArray();
            tl.span = 40f;
            tl.speed = 15f;
            tl.car = car;
            // other passengers, seated and standing
            float[] seatsX = { -6.0f, -4.0f, -1.2f, 0.9f, 3.9f, 5.9f, -9.0f };
            for (int i = 0; i < seatsX.Length; i++)
            {
                float sz = i % 2 == 0 ? -1f : 1f;
                var pos = car.TransformPoint(new Vector3(seatsX[i], 0, sz * (CarW / 2f - 0.22f)));
                Extra(r, $"Passenger{i}", HumanLook.Random(rng, Crowd.Street), pos, sz < 0 ? 0 : 180, sitting: true, phone: i % 3 != 1, seat: 0.445f);
            }
            Extra(r, "Standing", HumanLook.Random(rng, Crowd.Street), car.TransformPoint(new Vector3(-0.9f, 0, 0.35f)), 90, phone: true);
            Extra(r, "Standing2", HumanLook.Student(rng, true), car.TransformPoint(new Vector3(5.3f, 0, -0.2f)), 260);
            return Anchor(r, "InCar", car.TransformPoint(new Vector3(1.6f, 0.05f, -0.45f)), 20);
        }
    }
}
