using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.Quest;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Can the player get everywhere the game sends them, and through every narrow place on the way? For every
    /// playable scene it:
    /// - opens what opens in play: the station exit's works barrier (down in the subway parts), the train's and
    ///   the screen doors' leaves, and the gate lanes after a tap (first checking that with every lane shut the
    ///   paid side can't be reached);
    /// - bakes a navmesh for the player's capsule from the scene's solid colliders (radius 0.3 plus the
    ///   controller's 0.08 skin, 1.75 m tall, steps up to 0.45 m), so any gap narrower than about 0.76 m is shut;
    /// - finds a path from where the player starts (the spawn, the Start_ and Approach_ points, the places a
    ///   step teleports to) to every place a part needs them at (counters, ticket machines, form tables, room
    ///   doors, waypoints, exits, the building doors and the station's walk-in);
    /// - walks a CharacterController of the player's size along each path, so a lip or a low edge the
    ///   navmesh lets through but the capsule can't take is caught too.
    /// Writes Recordings/WalkCheck/report.txt, and a close-up map of every place that failed.
    /// Scenes are only read: nothing is saved.
    /// </summary>
    public static class WalkCheck
    {
        const string Out = "Recordings/WalkCheck";
        const float Radius = 0.3f, Skin = 0.08f, Height = 1.75f, Climb = 0.45f, Slope = 50f, Reach = 1.8f;
        static readonly string[] Scenes = { "Hub", "Street119", "Clinic", "Immigration", "Bank", "ER", "Airport", "Subway" };

        struct Target
        {
            public string Name;
            public Bounds Box;
            public bool WalkIn;
            public Vector3? Stand;    // where the player stands to use it, when the scene says
        }

        [MenuItem("Seoul First Week/Play-test/Check Walkways")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            foreach (var f in Directory.GetFiles(Out, "*.png")) File.Delete(f);
            var report = new StringBuilder();
            int fails = 0, checks = 0;
            foreach (var name in Scenes)
            {
                EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{name}.unity");
                var (f, c) = CheckScene(name, report);
                fails += f;
                checks += c;
            }
            NavMesh.RemoveAllNavMeshData();
            report.Insert(0, fails == 0 ? $"ALL OK: {checks} checks\n\n" : $"{fails} FAIL of {checks} checks\n\n");
            File.WriteAllText($"{Out}/report.txt", report.ToString());
            Debug.Log($"[WalkCheck] {(fails == 0 ? $"all {checks} walkway checks OK" : $"{fails} of {checks} checks failed")}: {Out}/report.txt");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        }

        static (int fails, int checks) CheckScene(string scene, StringBuilder report)
        {
            report.AppendLine($"== {scene}");
            int fails = 0, checks = 0;

            // what opens in play: the station exit's "closed for works" barrier is down in the subway parts
            foreach (var e in Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (e.closedBarrier)
                {
                    e.closedBarrier.SetActive(false);
                    report.AppendLine($"  (the {e.entranceId} exit's works barrier is taken down, as in its own parts)");
                }
            // the train's and the screen doors' leaves
            foreach (var train in Object.FindObjectsByType<StationTrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var l in train.leavesLeft ?? new Transform[0]) if (l) l.localPosition -= new Vector3(train.doorTravel, 0, 0);
                foreach (var r in train.leavesRight ?? new Transform[0]) if (r) r.localPosition += new Vector3(train.doorTravel, 0, 0);
            }

            // where the player starts: the spawn, Start_ and Approach_ points, the places a step teleports to
            var starts = new List<(string name, Vector3 at)>();
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player)
            {
                starts.Add(("spawn", player.transform.position));
                player.gameObject.SetActive(false);
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Start_") || t.name.StartsWith("Approach_") || t.name == "PlatformArrival" || t.name == "OfficeArrival")
                    starts.Add((t.name, t.position));
            foreach (var w in Object.FindObjectsByType<Waypoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (w.moveTo && !starts.Any(s => (s.at - w.moveTo.position).sqrMagnitude < 0.01f))
                    starts.Add(($"after {w.stationId}", w.moveTo.position));

            var targets = Targets();
            var area = new Bounds(starts.Count > 0 ? starts[0].at : Vector3.zero, Vector3.one);
            foreach (var s in starts) area.Encapsulate(s.at);
            foreach (var t in targets) area.Encapsulate(t.Box);
            area.Expand(new Vector3(24f, 16f, 24f));
            float voxel = scene == "Hub" || scene == "Street119" ? 0.1f : 0.05f;

            // the gate holds: with every lane shut the paid side (the stairs down) can't be reached
            var lanes = Object.FindObjectsByType<GateLane>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (lanes.Length > 0)
            {
                Bake(area, voxel);
                checks++;
                var paid = targets.Where(t => t.Name.StartsWith("wp:stairs_sadang") || t.Name.StartsWith("wp:stairs_jinjeop")).ToList();
                bool leaks = paid.Any(t => Find(starts, t, out _, out _, out _));
                report.AppendLine(leaks ? "  FAIL gate: the stairs down can be reached without tapping a card"
                    : $"  ok   gate: with all {lanes.Length} lanes shut the stairs down can't be reached");
                if (leaks) fails++;
                foreach (var l in lanes) if (l.blocker) l.blocker.enabled = false;
                Physics.SyncTransforms();
            }

            Bake(area, voxel);
            var walker = MakeWalker();
            foreach (var t in targets)
            {
                checks++;
                if (!Find(starts, t, out var from, out var to, out var path))
                {
                    fails++;
                    report.AppendLine($"  FAIL {t.Name}: no way there from {string.Join(", ", starts.Select(s => s.name))} (at {t.Box.center:F1})");
                    Map($"{scene}-{Safe(t.Name)}-nopath", t.Box.center, null);
                    continue;
                }
                var (ok, at, note) = Walk(walker, path, t);
                if (ok)
                    report.AppendLine($"  ok   {t.Name}: walked {Length(path):F1} m from {from.name}{note}");
                else
                {
                    fails++;
                    report.AppendLine($"  FAIL {t.Name}: walking from {from.name} stuck at {at:F2}{note}");
                    Map($"{scene}-{Safe(t.Name)}-stuck", at, path);
                }
            }
            Object.DestroyImmediate(walker.gameObject);
            report.AppendLine();
            return (fails, checks);
        }

        // ------------------------------------------------------------------ targets

        static List<Target> Targets()
        {
            var list = new List<Target>();
            var counts = new Dictionary<string, int>();
            string Unique(string n)
            {
                counts[n] = counts.TryGetValue(n, out var k) ? k + 1 : 1;
                return counts[n] == 1 ? n : $"{n}#{counts[n]}";
            }
            Bounds BoxOf(Component c)
            {
                var col = c.GetComponent<Collider>();
                return col ? col.bounds : new Bounds(c.transform.position, Vector3.one * 0.5f);
            }
            foreach (var w in Object.FindObjectsByType<Waypoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(w => w.stationId).ThenBy(w => w.transform.position.x))
                list.Add(new Target { Name = Unique("wp:" + w.stationId), Box = BoxOf(w), WalkIn = w.walkThrough });
            foreach (var a in Object.FindObjectsByType<ArmedInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (a is Waypoint) continue;
                var t = new Target { Name = Unique($"{a.GetType().Name}:{a.name}"), Box = BoxOf(a) };
                if (a is RoomEntrance room && room.approach) t.Stand = room.approach.position;
                list.Add(t);
            }
            foreach (var e in Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                list.Add(new Target { Name = Unique($"door:{e.entranceId}"), Box = BoxOf(e), WalkIn = e.walkIn });
            foreach (var x in Object.FindObjectsByType<ExitDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                list.Add(new Target { Name = Unique($"exit:{x.name}"), Box = BoxOf(x) });
            return list;
        }

        /// <summary>A start with a complete path to a spot from which the target is used (inside it, for a walk-in).</summary>
        static bool Find(List<(string name, Vector3 at)> starts, Target t, out (string name, Vector3 at) from, out Vector3 to, out NavMeshPath path)
        {
            from = default;
            to = default;
            path = new NavMeshPath();
            var spots = new List<Vector3>();
            if (t.Stand.HasValue) spots.Add(t.Stand.Value);
            var c = t.Box.center;
            spots.Add(c);
            if (!t.WalkIn)
            {
                float r0 = Mathf.Max(t.Box.extents.x, t.Box.extents.z);
                foreach (var r in new[] { r0 + 0.5f, r0 + 1.1f })
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6f;
                        spots.Add(c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                    }
            }
            foreach (var spot in spots)
            {
                if (!NavMesh.SamplePosition(spot, out var hit, t.WalkIn ? 1.6f : 2.4f, NavMesh.AllAreas)) continue;
                var p = hit.position;
                if (t.WalkIn ? !InsideFlat(t.Box, p, 0.05f) : FlatDistance(t.Box, p) > Reach) continue;
                foreach (var s in starts)
                {
                    if (!NavMesh.SamplePosition(s.at, out var sh, 1.6f, NavMesh.AllAreas)) continue;
                    if (NavMesh.CalculatePath(sh.position, p, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        from = s;
                        to = p;
                        return true;
                    }
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ the navmesh

        static void Bake(Bounds area, float voxel)
        {
            NavMesh.RemoveAllNavMeshData();
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            // every layer but the UI's: the invisible barriers on Ignore Raycast stop the capsule too
            int layers = ~((1 << 5) | (1 << 31));
            NavMeshBuilder.CollectSources(area, layers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            // triggers don't stop the capsule; disabled colliders aren't there
            sources.RemoveAll(s => s.component is Collider col && (col.isTrigger || !col.enabled || col is CharacterController || !col.gameObject.activeInHierarchy));
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = Radius + Skin;
            settings.agentHeight = Height;
            settings.agentClimb = Climb;
            settings.agentSlope = Slope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = voxel;
            settings.minRegionArea = 0.2f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, area, Vector3.zero, Quaternion.identity);
            NavMesh.AddNavMeshData(data);
        }

        // ------------------------------------------------------------------ walking it

        static CharacterController MakeWalker()
        {
            var go = new GameObject("WalkCheckCapsule");
            var cc = go.AddComponent<CharacterController>();
            cc.height = Height;
            cc.radius = Radius;
            cc.center = new Vector3(0, Height / 2f, 0);
            cc.stepOffset = Climb;
            cc.slopeLimit = Slope;
            return cc;
        }

        /// <summary>Walks the capsule along the path; returns whether it got there, and where it stopped if not.</summary>
        static (bool ok, Vector3 at, string note) Walk(CharacterController cc, NavMeshPath path, Target t)
        {
            var corners = path.corners;
            var go = cc.gameObject;
            cc.enabled = false;
            go.transform.position = corners[0] + Vector3.up * 0.05f;
            cc.enabled = true;
            Physics.SyncTransforms();
            const float step = 0.08f;
            for (int i = 1; i < corners.Length; i++)
            {
                var goal = corners[i];
                float best = Flat(goal - go.transform.position);
                int stall = 0;
                for (int n = 0; n < 4000 && Flat(goal - go.transform.position) > 0.12f; n++)
                {
                    var dir = goal - go.transform.position;
                    dir.y = 0;
                    var move = dir.normalized * Mathf.Min(step, dir.magnitude) + Vector3.down * 0.06f;
                    cc.Move(move);
                    Physics.SyncTransforms();
                    float d = Flat(goal - go.transform.position);
                    if (d < best - 0.02f)
                    {
                        best = d;
                        stall = 0;
                    }
                    else if (++stall > 40)
                        return (false, go.transform.position, $" (corner {i} of {corners.Length - 1}; blocked by {Blockers(go.transform.position)})");
                }
            }
            var feet = go.transform.position;
            bool there = t.WalkIn ? InsideFlat(t.Box, feet, 0.05f) && feet.y < t.Box.max.y && feet.y + Height > t.Box.min.y
                : FlatDistance(t.Box, feet) <= Reach + 0.1f;
            return there ? (true, feet, "") : (false, feet, " (reached the end of the path but not the target)");
        }

        static string Blockers(Vector3 feet)
        {
            var hits = Physics.OverlapCapsule(feet + Vector3.up * (Radius + 0.05f), feet + Vector3.up * (Height - Radius), Radius + Skin + 0.12f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !(c is CharacterController)).Select(c => Path(c.transform)).Distinct().Take(4).ToList();
            return hits.Count > 0 ? string.Join("; ", hits) : "nothing solid (a step or a slope?)";
        }

        // ------------------------------------------------------------------ maps

        /// <summary>A top-down close-up (16 m across) of a failed spot: the walkable area in green, the path in yellow.</summary>
        static void Map(string file, Vector3 at, NavMeshPath path)
        {
            var tri = NavMesh.CalculateTriangulation();
            var root = new GameObject("WalkCheckOverlay").transform;
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(tri.vertices.Select(v => v + Vector3.up * 0.06f).ToArray());
            mesh.SetTriangles(tri.indices, 0);
            var nav = new GameObject("Nav");
            nav.transform.SetParent(root, false);
            nav.AddComponent<MeshFilter>().sharedMesh = mesh;
            nav.AddComponent<MeshRenderer>().sharedMaterial = Mats.UnlitTransparent(new Color(0.2f, 0.95f, 0.45f, 0.4f));
            if (path != null)
                for (int i = 0; i + 1 < path.corners.Length; i++)
                {
                    var a = path.corners[i];
                    var b = path.corners[i + 1];
                    var seg = Prims.Make(PrimitiveType.Cube, root, "Path", (a + b) / 2f + Vector3.up * 0.1f, new Vector3(0.08f, 0.02f, Vector3.Distance(a, b)), Mats.Unlit(new Color(1f, 0.85f, 0.1f)));
                    seg.transform.rotation = Quaternion.LookRotation(b - a == Vector3.zero ? Vector3.forward : b - a);
                }
            Prims.Make(PrimitiveType.Sphere, root, "Spot", at + Vector3.up * 0.3f, Vector3.one * 0.5f, Mats.Unlit(new Color(1f, 0.1f, 0.3f)));
            var hidden = new List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.enabled && r.bounds.min.y > at.y + 2.4f) { r.enabled = false; hidden.Add(r); }
            var cam = new GameObject("WalkCheckCam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.transform.SetPositionAndRotation(at + Vector3.up * 60f, Quaternion.Euler(90, 0, 0));
            PlaytestTools.Capture($"{Out}/{file}.png", 1000, 1000, cam, false);
            PlaytestTools.Capture($"{Out}/{file}.png", 1000, 1000, cam, false);
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(root.gameObject);
            foreach (var r in hidden) if (r) r.enabled = true;
        }

        // ------------------------------------------------------------------ helpers

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static bool InsideFlat(Bounds b, Vector3 p, float margin) =>
            p.x > b.min.x - margin && p.x < b.max.x + margin && p.z > b.min.z - margin && p.z < b.max.z + margin;

        static float FlatDistance(Bounds b, Vector3 p)
        {
            float dx = Mathf.Max(b.min.x - p.x, 0, p.x - b.max.x), dz = Mathf.Max(b.min.z - p.z, 0, p.z - b.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float Length(NavMeshPath p)
        {
            float l = 0;
            for (int i = 0; i + 1 < p.corners.Length; i++) l += Vector3.Distance(p.corners[i], p.corners[i + 1]);
            return l;
        }

        static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p && s.Length < 90; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        static string Safe(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
    }
}
