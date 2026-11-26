using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Quest;
using SeoulFirstWeek.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// 명동역 exit 6 (2026-11-26): stairs in the pavement instead of a door in a facade. 명동역 lies under 퇴계로 at the
    /// south end of Myeongdong, and exit 6 is the one at the start of the shopping street, so the exit stands at the
    /// south end of the playable 명동길, facing up the street, its stairs going down southwards (towards 퇴계로) and
    /// on under the end of the street. A flight goes down through an opening the pavement leaves for it, between
    /// granite curbs with glass balustrades, under a glass canopy on a steel frame with the station's sign (Line 4's
    /// ④, 명동, exit 6 in its yellow square) and a totem; a landing, a second flight under the street and a lit hall
    /// below, where walking in takes the player into the station. For other levels a "this exit is closed for
    /// works" barrier stands on the landing; they start just up the street from it, as if off the train.
    /// </summary>
    public static partial class SeoulSceneBuilder
    {
        /// <summary>The station exit's opening, in street coordinates (the centre line's z, the offset across) and in the world.</summary>
        struct StationSite
        {
            public float Z0, Z1, U0, U1;
            public Vector3 Top;     // middle of the top step's edge, on the pavement
            public Vector3 Down;    // the way the flight runs (along the street, south)
        }

        // the top step on a whole z (a cross-section of the pavement lies on each, so the opening's ends fall on them)
        // 1.9 m of stairs between the curbs, a little west of the middle: the street's mouth is 5.3–6 m wide and
        // narrower on the east, so about 1.3–1.6 m stays free either side
        // 7 m of opening: the first flight and the landing; the second flight goes on under the street
        const float StationTopZ = -92f, StationVoidLen = 7f, StationVoidHalf = 0.95f, StationShift = -0.3f;

        /// <summary>Points over the station exit's footprint that street props keep 2.6 m from (set by BuildHub).</summary>
        static List<Vector3> stationClear = new();

        /// <summary>
        /// Where exit 6 goes: in the mouth of 명동길 where it meets the cross street (z −99 to −92, where the street
        /// runs dead straight, so the opening in the pavement lines up with the stairs), a way left free on both
        /// sides and no shop front hidden; the flight runs south and its covered part passes under the end of the street.
        /// </summary>
        static StationSite StationFootprint()
        {
            float z1 = StationTopZ, z0 = z1 - StationVoidLen;
            var top = StreetPoint(z1);
            top.y = 0;
            top += Vector3.Cross(Vector3.up, StreetDir(z1)) * StationShift;
            top.y = GroundY(StreetPoint(z1)) + 0.02f;
            for (float z = z0; z <= z1 + 0.01f; z += 2f)
            {
                var c = StreetPoint(z);
                c.y = GroundY(c) + 1.2f;
                var right = Vector3.Cross(Vector3.up, StreetDir(z));
                FacadeHit(c, -right, 30f, out var w);
                FacadeHit(c, right, 30f, out var e);
                Debug.Log($"[Builder] Station exit at z={z}: {w.distance - StationVoidHalf - 0.25f + StationShift:F1} m free on the west, {e.distance - StationVoidHalf - 0.25f - StationShift:F1} m on the east");
            }
            return new StationSite { Z0 = z0, Z1 = z1, U0 = StationShift - StationVoidHalf, U1 = StationShift + StationVoidHalf, Top = top, Down = -StreetDir((z0 + z1) / 2f) };
        }

        static BuildingEntrance BuildStationEntrance(Transform parent, EntranceSpec s, StationSite site)
        {
            // the entrance faces out of the stairs, at the people coming up 명동길 (the door shot, the way back out
            // and the beacon use that); the structure is built turned round, +z down the flight
            var root = Group($"Entrance_{s.Id}", parent);
            root.SetPositionAndRotation(site.Top, Quaternion.LookRotation(-site.Down));
            var st = Group("Structure", root);
            st.localRotation = Quaternion.Euler(0, 180, 0);
            var parts = Group("Parts", st);
            const float hv = StationVoidHalf, rise = 0.16f, run = 0.3f;

            var granite = TextureKit.Surface("Granite_StationExit", TextureKit.FloorGranite(), C("#8F8C86"), 0.5f);
            var tread = TextureKit.Surface("Passage_Floor", TextureKit.FloorStone(), C("#B9B4AA"));
            var tile = TextureKit.Surface("Wall_StationExit", TextureKit.Plastic(), C("#E8E6E1"), 0.6f);
            var steel = TextureKit.Surface("Steel_Canopy", TextureKit.Metal(), C("#5A6068"), 0.6f, 0.7f);

            // ---- the stairs: 16 steps, a landing, 10 more under the street, the hall at the bottom
            float bottomY = -4.8f;
            for (int i = 0; i < 16; i++)
            {
                float top = -rise * (i + 1), z = run * i + run / 2f;
                Slab(st, "Step", new Vector3(0, (top + bottomY) / 2f, z), new Vector3(hv * 2f, top - bottomY, run), tread, true, false);
                Box(parts, "Nosing", new Vector3(0, top + 0.004f, z - run / 2f + 0.04f), new Vector3(hv * 2f - 0.1f, 0.008f, 0.05f), C("#F2C230"), false);
            }
            float land1 = -rise * 16, l1z0 = run * 16, l1z1 = l1z0 + 1.8f;
            Slab(st, "Landing", new Vector3(0, (land1 + bottomY) / 2f, (l1z0 + l1z1) / 2f), new Vector3(hv * 2f, land1 - bottomY, l1z1 - l1z0), tread, true, false);
            for (int i = 0; i < 10; i++)
            {
                float top = land1 - rise * (i + 1), z = l1z1 + run * i + run / 2f;
                Slab(st, "Step", new Vector3(0, (top + bottomY) / 2f, z), new Vector3(hv * 2f, top - bottomY, run), tread, true, false);
                Box(parts, "Nosing", new Vector3(0, top + 0.004f, z - run / 2f + 0.04f), new Vector3(hv * 2f - 0.1f, 0.008f, 0.05f), C("#F2C230"), false);
            }
            float hallY = land1 - rise * 10, hz0 = l1z1 + run * 10, hz1 = hz0 + 3.4f;
            Slab(st, "Hall", new Vector3(0, (hallY + bottomY) / 2f, (hz0 + hz1) / 2f), new Vector3(hv * 2f + 0.8f, hallY - bottomY, hz1 - hz0), tread, true, false);
            // the shaft: walls down to the hall, the street's underside over the covered part, the end wall
            // the shaft's walls stop 4 cm under the pavement, so their tops never flicker through it
            foreach (var sx in new[] { -1f, 1f })
                Slab(st, "ShaftWall", new Vector3(sx * (hv + 0.1f), (bottomY - 0.04f) / 2f, hz1 / 2f), new Vector3(0.2f, -bottomY - 0.04f, hz1 + 0.2f), tile);
            Slab(st, "UnderStreet", new Vector3(0, -0.3f, (StationVoidLen + hz1) / 2f), new Vector3(hv * 2f + 0.4f, 0.1f, hz1 - StationVoidLen), tile, true, false);
            Slab(st, "ShaftEnd", new Vector3(0, (bottomY - 0.04f) / 2f, hz1 + 0.1f), new Vector3(hv * 2f + 0.4f, -bottomY - 0.04f, 0.2f), tile);
            // handrails along the walls, unbroken down both flights and along the landing, standing on an end post
            // at the top (on the pavement) and at the bottom (on the hall floor)
            var railColor = C("#B9BEC4");
            foreach (var sx in new[] { -1f, 1f })
            {
                float rx = sx * (hv - 0.06f);
                foreach (var (z0, z1, y0, y1) in new[] { (0f, l1z0, 0f, land1), (l1z1, hz0, land1, hallY) })
                {
                    float len = Mathf.Sqrt((z1 - z0) * (z1 - z0) + (y1 - y0) * (y1 - y0));
                    var rail = Cylinder(parts, "Handrail", new Vector3(rx, (y0 + y1) / 2f + 0.9f, (z0 + z1) / 2f), new Vector3(0.045f, len / 2f, 0.045f), railColor);
                    rail.transform.localRotation = Quaternion.Euler(Mathf.Atan2(z1 - z0, y1 - y0) * Mathf.Rad2Deg, 0, 0);
                }
                var landing = Cylinder(parts, "Handrail", new Vector3(rx, land1 + 0.9f, (l1z0 + l1z1) / 2f), new Vector3(0.045f, (l1z1 - l1z0) / 2f + 0.02f, 0.045f), railColor);
                landing.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                Cylinder(parts, "HandrailPost", new Vector3(rx, 0.45f, -0.03f), new Vector3(0.055f, 0.45f, 0.055f), railColor);
                Cylinder(parts, "HandrailPost", new Vector3(rx, hallY + 0.45f, hz0 + 0.03f), new Vector3(0.055f, 0.45f, 0.055f), railColor);
            }
            // lights: step lights low in the walls of both flights (the walls only rise above the steps once
            // they are under the pavement, so nothing hangs in the air beside the glass), a strip under the
            // street, the hall bright below
            foreach (var (z0, z1, yTop) in new[] { (0f, l1z0, 0f), (l1z1, hz0, land1) })
                for (float z = z0 + 0.9f; z < z1 - 0.2f; z += 1.2f)
                {
                    float y = yTop - rise * ((z - z0) / run + 1f) + 0.38f;
                    if (y > -0.14f) continue;
                    foreach (var sx in new[] { -1f, 1f })
                        Emissive(parts, "StepLight", new Vector3(sx * (hv - 0.01f), y, z), new Vector3(0.02f, 0.05f, 0.3f), C("#FFF4E0"), 1.6f)
                            .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            Emissive(parts, "StreetUndersideLight", new Vector3(0, -0.36f, (StationVoidLen + hz1) / 2f), new Vector3(0.2f, 0.02f, hz1 - StationVoidLen - 0.6f), C("#F6F9FF"), 1.8f)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(st, new Vector3(0, land1 + 2.0f, (l1z0 + l1z1) / 2f), 6f, 1.1f, C("#FFF4E6")).shadows = LightShadows.None;
            PointLight(st, new Vector3(0, hallY + 2.2f, (hz0 + hz1) / 2f), 7f, 1.5f, C("#F4F7FF")).shadows = LightShadows.None;
            Sign(st, "HallSign", new Vector3(0, hallY + 2.3f, hz1 - 0.02f), new Vector2(2.4f, 0.5f), C("#2B2E33"),
                "<color=#00A4E3>④</color> 명동역 · 타는 곳 →\n<size=26>Myeong-dong Station</size>", Color.white, 180, 40, 0.04f);
            Emissive(parts, "HallGlow", new Vector3(0, hallY + 1.2f, hz1 - 0.01f), new Vector3(hv * 2f, 1.6f, 0.01f), C("#E9F0FA"), 0.9f);

            // ---- above the street: curbs with glass balustrades, the canopy and its sign
            foreach (var sx in new[] { -1f, 1f })
            {
                Slab(parts, "Curb", new Vector3(sx * (hv + 0.125f), 0.175f, StationVoidLen / 2f + 0.06f), new Vector3(0.25f, 0.35f, StationVoidLen + 0.12f), granite, false);
                Glass(parts, "Balustrade", new Vector3(sx * (hv + 0.125f), 0.7f, StationVoidLen / 2f + 0.06f), new Vector3(0.02f, 0.7f, StationVoidLen), 0, false);
                Box(parts, "TopRail", new Vector3(sx * (hv + 0.125f), 1.07f, StationVoidLen / 2f + 0.06f), new Vector3(0.06f, 0.05f, StationVoidLen + 0.12f), C("#B9BEC4"), false);
                Solid(st, "Curb", new Vector3(sx * (hv + 0.125f), 0.55f, StationVoidLen / 2f + 0.06f), new Vector3(0.25f, 1.1f, StationVoidLen + 0.12f));
            }
            Slab(parts, "CurbEnd", new Vector3(0, 0.175f, StationVoidLen + 0.125f), new Vector3(hv * 2f + 0.5f, 0.35f, 0.25f), granite, false);
            Glass(parts, "BalustradeEnd", new Vector3(0, 0.7f, StationVoidLen + 0.125f), new Vector3(hv * 2f + 0.3f, 0.7f, 0.02f), 0, false);
            Box(parts, "TopRailEnd", new Vector3(0, 1.07f, StationVoidLen + 0.125f), new Vector3(hv * 2f + 0.5f, 0.05f, 0.06f), C("#B9BEC4"), false);
            Solid(st, "CurbEnd", new Vector3(0, 0.55f, StationVoidLen + 0.125f), new Vector3(hv * 2f + 0.5f, 1.1f, 0.25f));
            // the canopy: four posts on the curbs (the front pair ends the balustrades, so no rail runs on past
            // them), edge beams, ribs and a glass roof with a gentle ridge
            float cw = hv + 0.3f, eave = 2.95f, ridge = 3.25f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var pz in new[] { 0.06f, StationVoidLen - 0.3f })
                Box(parts, "Post", new Vector3(sx * (hv + 0.125f), (0.35f + eave) / 2f, pz), new Vector3(0.12f, eave - 0.35f, 0.12f), C("#5A6068"), false);
            foreach (var sx in new[] { -1f, 1f })
                Slab(parts, "EdgeBeam", new Vector3(sx * cw, eave, StationVoidLen / 2f), new Vector3(0.12f, 0.16f, StationVoidLen + 0.9f), steel, false);
            for (float z = -0.4f; z <= StationVoidLen + 0.45f; z += 1.42f)
                foreach (var sx in new[] { -1f, 1f })
                {
                    var rib = Box(parts, "Rib", new Vector3(sx * cw / 2f, (eave + ridge) / 2f, z), new Vector3(cw + 0.02f, 0.06f, 0.08f), C("#5A6068"), false);
                    rib.transform.localRotation = Quaternion.Euler(0, 0, -sx * Mathf.Atan2(ridge - eave, cw) * Mathf.Rad2Deg);
                }
            foreach (var sx in new[] { -1f, 1f })
            {
                var pane = Glass(parts, "Roof", new Vector3(sx * cw / 2f, (eave + ridge) / 2f + 0.04f, StationVoidLen / 2f), new Vector3(cw + 0.04f, 0.02f, StationVoidLen + 0.9f), 0, false);
                pane.transform.localRotation = Quaternion.Euler(0, 0, -sx * Mathf.Atan2(ridge - eave, cw) * Mathf.Rad2Deg);
            }
            Box(parts, "Ridge", new Vector3(0, ridge + 0.03f, StationVoidLen / 2f), new Vector3(0.1f, 0.08f, StationVoidLen + 0.9f), C("#5A6068"), false);
            Emissive(parts, "CanopyLight", new Vector3(0, eave - 0.1f, StationVoidLen / 2f), new Vector3(0.12f, 0.02f, StationVoidLen - 0.4f), C("#FFF8EC"), 1.6f)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // the sign over the way in: ④ 명동 and exit 6 in its yellow square
            var signs = Group("Signs", st);
            // the sign reaches a little past the canopy; the exit number in its yellow square at the right end
            float fw = cw * 2f + 0.5f;
            Sign(signs, "Fascia", new Vector3(0.22f, eave - 0.18f, -0.46f), new Vector2(fw - 0.44f, 0.42f), C("#2B2E33"),
                "<color=#00A4E3>④</color> 명동 <size=26>Myeong-dong</size>", Color.white, 180, 40, 0.08f);
            Box(signs, "FasciaEnd", new Vector3(-fw / 2f + 0.22f, eave - 0.18f, -0.46f), new Vector3(0.44f, 0.42f, 0.08f), C("#2B2E33"), false);
            Sign(signs, "ExitNumber", new Vector3(-fw / 2f + 0.22f, eave - 0.18f, -0.52f), new Vector2(0.38f, 0.38f), C("#F2C230"), "6", C("#1E1E1E"), 180, 70, 0.04f);
            // the totem on the street side of the way in
            var totem = Group("Totem", signs);
            totem.localPosition = new Vector3(-(hv + 0.125f), 0, -0.45f);
            Box(totem, "Post", new Vector3(0, 1.3f, 0), new Vector3(0.42f, 2.6f, 0.14f), C("#2B2E33"), true);
            Box(totem, "Cap", new Vector3(0, 2.52f, 0), new Vector3(0.44f, 0.18f, 0.16f), C("#00A4E3"), false);
            Sign(totem, "Face", new Vector3(0, 1.75f, -0.075f), new Vector2(0.38f, 1.2f), C("#2B2E33"),
                "<color=#00A4E3>④</color>\n명동\n<size=22>Myeong-dong</size>\n<size=30><color=#F2C230>6</color></size>", Color.white, 180, 40, 0.01f);
            // the warning strip in front of the top step
            Box(parts, "Tactile", new Vector3(0, 0.006f, -0.45f), new Vector3(hv * 2f, 0.012f, 0.5f), C("#F2C230"), false);

            // ---- closed for works (any other level): a barrier across the landing
            var barrier = Group("ClosedBarrier", st);
            barrier.localPosition = new Vector3(0, land1, l1z0 + 0.6f);
            foreach (var sx in new[] { -1f, 1f })
                Box(barrier, "Leg", new Vector3(sx * (hv - 0.2f), 0.5f, 0), new Vector3(0.06f, 1.0f, 0.5f), C("#E8E6E1"), false);
            for (int k = 0; k < 2; k++)
                Box(barrier, "Board", new Vector3(0, 0.55f + k * 0.35f, 0), new Vector3(hv * 2f - 0.2f, 0.2f, 0.04f), k == 0 ? C("#F2C230") : C("#1E1E1E"), false);
            Sign(barrier, "Notice", new Vector3(0, 1.55f, -0.02f), new Vector2(1.5f, 0.6f), C("#FFFFFF"),
                "출구 공사중\n<size=24>다른 출구를 이용해 주세요 · Exit closed</size>", C("#C8402E"), 180, 40, 0.02f);
            Solid(barrier, "Block", new Vector3(0, 1.0f, 0), new Vector3(hv * 2f, 2.0f, 0.3f));

            MergeByMaterial(parts, "Hub_StationExit");
            // people on the street's navmesh walk round the opening, never down it
            var mod = st.gameObject.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = 1;

            // walking into the hall at the bottom takes the player into the station
            var trigger = root.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, hallY + 1.2f, -((hz0 + hz1) / 2f));
            trigger.size = new Vector3(hv * 2f, 2.4f, hz1 - hz0);
            root.gameObject.layer = 2;

            // the subway level starts 40 m up the street, walking down it to the exit
            float approachZ = StationTopZ + ApproachDistance;
            var approachPos = StreetPoint(approachZ);
            approachPos.y = GroundY(approachPos) + 0.05f;
            var approach = Group($"Approach_{s.Id}", parent);
            approach.SetPositionAndRotation(approachPos, Quaternion.LookRotation(-StreetDir(approachZ)));
            var exit = Group("ExitPoint", root);
            exit.localPosition = new Vector3(0, 0.05f, 1.6f);
            exit.localRotation = Quaternion.identity;

            var beacon = Group("Beacon", root);
            beacon.localPosition = new Vector3(0, 0, 1.4f);
            var beam = Prims.Make(PrimitiveType.Cylinder, beacon, "Beam", new Vector3(0, 22f, 0), new Vector3(0.7f, 22f, 0.7f), Mats.UnlitTransparent(new Color(s.Color.r, s.Color.g, s.Color.b, 0.22f)));
            beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var ring = Prims.Make(PrimitiveType.Cylinder, beacon, "Ring", new Vector3(0, 0.03f, 0), new Vector3(2.2f, 0.01f, 2.2f), Mats.UnlitTransparent(new Color(s.Color.r, s.Color.g, s.Color.b, 0.5f)));
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var ob = beacon.gameObject.AddComponent<ObjectiveBeacon>();
            ob.beam = beam.GetComponent<Renderer>();
            ob.ring = ring.GetComponent<Renderer>();
            ob.color = s.Color;

            var entrance = root.gameObject.AddComponent<BuildingEntrance>();
            entrance.entranceId = s.Id;
            entrance.sceneName = s.Scene;
            entrance.nameKo = s.NameKo;
            entrance.color = s.Color;
            entrance.exitPoint = exit;
            entrance.approach = approach;
            entrance.beacon = ob;
            entrance.walkIn = true;
            entrance.closedBarrier = barrier.gameObject;
            return entrance;
        }

        /// <summary>
        /// Cuts the station exit's opening out of the city model's own ground (roads, paths, pedestrian areas and the
        /// ground plane lie a couple of centimetres under the pavement and would cover the stairs, and their colliders
        /// would stop the player at the top). Triangles across the opening's edge are split until they are a few
        /// centimetres across and those inside are dropped; the cut copies are saved as Hub_Cut_* meshes and every
        /// build starts again from the model's own meshes, so moving the exit leaves no old hole behind.
        /// </summary>
        static void CutGroundOpening(StationSite site)
        {
            const string model = "Assets/_Project/Art/Environment/Myeongdong/Myeongdong_Hub.fbx";
            var originals = AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
            var along = site.Down;
            var across = Vector3.Cross(Vector3.up, along);
            var centre = site.Top + along * (StationVoidLen / 2f);
            float halfLen = StationVoidLen / 2f, halfWide = StationVoidHalf + 0.1f;
            Vector2 Local(Vector3 p) { var d = p - centre; return new Vector2(Vector3.Dot(d, across), Vector3.Dot(d, along)); }
            bool Inside(Vector2 q) => Mathf.Abs(q.x) < halfWide && Mathf.Abs(q.y) < halfLen;
            int cutMeshes = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = mf.name;
                if (n == "paths_Pavement" || !(n.StartsWith("roads_") || n.StartsWith("paths_") || n.StartsWith("areas_") || n == "Ground")) continue;
                var current = mf.sharedMesh;
                if (!current) continue;
                var baseName = current.name.StartsWith("Hub_Cut_") ? current.name.Substring(8) : current.name;
                var mesh = originals.TryGetValue(baseName, out var o) ? o : current;
                var col = mf.GetComponent<MeshCollider>();
                // put the model's own mesh back first; cut it only if it reaches the opening
                mf.sharedMesh = mesh;
                if (col) col.sharedMesh = mesh;
                var t = mf.transform;
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                var w = verts.Select(v => t.TransformPoint(v)).ToArray();
                bool touches = false;
                for (int i = 0; i < tris.Length && !touches; i += 3)
                    touches = Overlaps(Local(w[tris[i]]), Local(w[tris[i + 1]]), Local(w[tris[i + 2]]), halfWide, halfLen);
                if (!touches) continue;
                var outVerts = new List<Vector3>();
                void Emit(Vector3 a, Vector3 b, Vector3 c)
                {
                    outVerts.Add(t.InverseTransformPoint(a));
                    outVerts.Add(t.InverseTransformPoint(b));
                    outVerts.Add(t.InverseTransformPoint(c));
                }
                void Split(Vector3 a, Vector3 b, Vector3 c, int depth)
                {
                    Vector2 la = Local(a), lb = Local(b), lc = Local(c);
                    int inside = (Inside(la) ? 1 : 0) + (Inside(lb) ? 1 : 0) + (Inside(lc) ? 1 : 0);
                    if (inside == 3) return;
                    if (inside == 0 && !Overlaps(la, lb, lc, halfWide, halfLen)) { Emit(a, b, c); return; }
                    float edge = Mathf.Max((la - lb).sqrMagnitude, (lb - lc).sqrMagnitude, (lc - la).sqrMagnitude);
                    if (depth > 12 || edge < 0.04f * 0.04f)
                    {
                        if (!Inside((la + lb + lc) / 3f)) Emit(a, b, c);
                        return;
                    }
                    Vector3 ab = (a + b) / 2f, bc = (b + c) / 2f, ca = (c + a) / 2f;
                    Split(a, ab, ca, depth + 1);
                    Split(ab, b, bc, depth + 1);
                    Split(ca, bc, c, depth + 1);
                    Split(ab, bc, ca, depth + 1);
                }
                for (int i = 0; i < tris.Length; i += 3)
                    Split(w[tris[i]], w[tris[i + 1]], w[tris[i + 2]], 0);
                var cut = new Mesh { name = $"Hub_Cut_{baseName}", indexFormat = IndexFormat.UInt32 };
                cut.SetVertices(outVerts);
                cut.SetTriangles(Enumerable.Range(0, outVerts.Count).ToArray(), 0);
                cut.RecalculateNormals();
                cut.RecalculateBounds();
                const string folder = "Assets/_Project/Art/Generated/Meshes";
                EnsureFolder(folder);
                AssetDatabase.DeleteAsset($"{folder}/{cut.name}.asset");
                AssetDatabase.CreateAsset(cut, $"{folder}/{cut.name}.asset");
                mf.sharedMesh = cut;
                if (col) col.sharedMesh = cut;
                cutMeshes++;
            }
            Debug.Log($"[Builder] Station exit: cut the opening out of {cutMeshes} ground meshes of the city model");
        }

        /// <summary>Whether a triangle (in the opening's own frame) overlaps the opening's rectangle: separating axes.</summary>
        static bool Overlaps(Vector2 a, Vector2 b, Vector2 c, float hx, float hy)
        {
            if (Mathf.Max(a.x, b.x, c.x) < -hx || Mathf.Min(a.x, b.x, c.x) > hx) return false;
            if (Mathf.Max(a.y, b.y, c.y) < -hy || Mathf.Min(a.y, b.y, c.y) > hy) return false;
            foreach (var (p, q, r) in new[] { (a, b, c), (b, c, a), (c, a, b) })
            {
                var e = q - p;
                var nrm = new Vector2(-e.y, e.x);
                float side = Vector2.Dot(r - p, nrm);
                // the rectangle's corners all on the far side of this edge from the triangle: separated
                bool separated = true;
                foreach (var corner in new[] { new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy) })
                {
                    float d = Vector2.Dot(corner - p, nrm);
                    if (side >= 0 ? d >= 0 : d <= 0) { separated = false; break; }
                }
                if (separated) return false;
            }
            return true;
        }

        /// <summary>Points over the station exit's footprint, for the street dressing to keep clear of.</summary>
        static IEnumerable<Vector3> StationKeepClear(StationSite site)
        {
            for (float t = -1.5f; t <= StationVoidLen + 0.6f; t += 1.5f)
            {
                var p = site.Top + site.Down * t;
                p.y = site.Top.y;
                yield return p;
            }
        }
    }
}
