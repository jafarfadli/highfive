using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.Quest;
using SeoulFirstWeek.UI;
using SeoulFirstWeek.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Rebuilds the game scenes from code: the Myeongdong hub (entrances, signs, stalls,
    /// pedestrians) and the three interiors. Re-run after changing layout code.
    /// </summary>
    public static partial class SeoulSceneBuilder
    {
        public const string SceneFolder = "Assets/_Project/Scenes";
        const string ProcedureFolder = "Assets/_Project/Data/Procedures";

        [MenuItem("Seoul First Week/Build All Scenes")]
        public static void BuildAll()
        {
            BuildHub();
            BuildMenus();
            BuildStreet119();
            BuildInteriors();
            BuildResults();
            ConfigureProject();
            PruneGenerated();
            EditorSceneManager.OpenScene($"{SceneFolder}/Home.unity");
        }

        /// <summary>
        /// Deletes generated materials, textures and meshes that no scene in the build uses any more
        /// (every rebuild writes new ones). Only run after all scenes have been rebuilt and saved.
        /// </summary>
        [MenuItem("Seoul First Week/Prune Generated Assets")]
        public static void PruneGenerated()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            var used = new HashSet<string>(AssetDatabase.GetDependencies(scenes, true));
            int removed = 0;
            foreach (var folder in new[] { MaterialFolder, TextureKit.Folder, "Assets/_Project/Art/Generated/Meshes" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path) || used.Contains(path)) continue;
                    AssetDatabase.DeleteAsset(path);
                    removed++;
                }
            }
            Debug.Log($"[Builder] Pruned {removed} unused generated assets");
        }

        [MenuItem("Seoul First Week/Build Interiors")]
        public static void BuildInteriors()
        {
            BuildClinic();
            BuildImmigration();
            BuildBank();
            BuildAirport();
            BuildSubway();
            BuildER();
        }

        public static void ConfigureProject()
        {
            EditorBuildSettings.scenes = new[] { "Home", "Select", "Hub", "Clinic", "Immigration", "Bank", "Airport", "Subway", "Street119", "ER", "Results" }
                .Select(n => new EditorBuildSettingsScene($"{SceneFolder}/{n}.unity", true)).ToArray();
            // the product name (and with it where PlayerPrefs live) is set in Project Settings: "highfive" keeps this
            // copy's saves apart from the original Seoul First Week project's
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        // =================================================================== HUB

        struct EntranceSpec
        {
            public string Id, Scene, NameKo, NameEn;
            public float StreetZ;
            public int Side; // -1 left (west), +1 right (east)
            public Color Color;
        }

        // Street centre line of 명동길 through the cropped block (world X,Z), south to north.
        // Measured by scanning road colliders (see git history for the scan command).
        static readonly Vector2[] Street =
        {
            new(15.75f, -92f), new(13f, -76f), new(11f, -60f), new(10.5f, -48f), new(7.25f, -36f), new(5.5f, -28f),
            new(4.5f, -20f), new(3f, -12f), new(2f, -4f), new(0.5f, 4f), new(-0.5f, 12f), new(-1.75f, 20f),
            new(-2.8f, 28f), new(-4f, 36f), new(-5.25f, 44f), new(-6.5f, 52f), new(-8f, 60f), new(-10f, 68f),
            new(-12f, 76f), new(-12.5f, 84f), new(-13.25f, 92f), new(-14.5f, 100f),
        };

        // The playable stretch of 명동길; invisible barriers close everything outside it.
        // the south end is where 명동길 opens onto the cross street, with 명동역's exit 6 at its mouth (2026-11-26; it was -72)
        const float CorridorMinZ = -99f, CorridorMaxZ = 72f;

        // A level starts this far down the street from its building, so the walk there is part of it.
        const float ApproachDistance = 40f;

        static Vector3 StreetPoint(float z)
        {
            for (int i = 0; i < Street.Length - 1; i++)
            {
                var a = Street[i];
                var b = Street[i + 1];
                if (z >= a.y && z <= b.y)
                {
                    float t = (z - a.y) / (b.y - a.y);
                    return new Vector3(Mathf.Lerp(a.x, b.x, t), 0, z);
                }
            }
            var last = z < Street[0].y ? Street[0] : Street[^1];
            return new Vector3(last.x, 0, z);
        }

        static Vector3 StreetDir(float z) => (StreetPoint(z + 2f) - StreetPoint(z - 2f)).normalized;

        static float GroundY(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 60f, Vector3.down, 120f).OrderBy(h => h.distance))
            {
                var n = h.collider.name;
                if (n.StartsWith("roads_") || n.StartsWith("paths_") || n.StartsWith("areas_") || n == "Ground") return h.point.y;
            }
            return 0.35f;
        }

        static bool FacadeHit(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit hit)
        {
            foreach (var h in Physics.RaycastAll(origin, dir, maxDist).OrderBy(h => h.distance))
            {
                if (h.collider.name.StartsWith("Bldg_"))
                {
                    hit = h;
                    return true;
                }
            }
            hit = default;
            return false;
        }

        [MenuItem("Seoul First Week/Build Hub")]
        public static void BuildHub()
        {
            var scene = EditorSceneManager.OpenScene($"{SceneFolder}/Hub.unity");
            BeginMaterials();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name != "Environment") Object.DestroyImmediate(go);
            Physics.SyncTransforms();

            var root = Group("[Hub]");
            // late-afternoon gold: a strong warm sun from the south-south-west, high enough to reach down into
            // 명동길 (the street is a canyon: a lower sun leaves it all in shade), so the pavers glow in pools
            // against cool blue shade, and the distance fades into warm haze
            var sun = Sun(root, new Vector3(50, 22, 0), 2f, C("#FFC88A"));
            sun.shadowStrength = 0.92f;
            Ambient(C("#8C9ED2"), C("#C2A690"), C("#5F5049"));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = C("#ECCBA9");
            RenderSettings.fogStartDistance = 60;
            RenderSettings.fogEndDistance = 340;
            LookKit.Street(root);

            // pave the playable street first: everything placed below stands on it; the subway exit's stairs go
            // down through an opening the pavement leaves for them
            var station = StationFootprint();
            BuildPavement(root, new List<Vector4> { new(station.Z0, station.Z1, station.U0, station.U1) });
            CutGroundOpening(station);
            Physics.SyncTransforms();

            // player
            // just up the street from 명동역's exit 6, where a student off the subway comes up into 명동길
            float spawnZ = -84f;
            var spawnPos = StreetPoint(spawnZ);
            spawnPos.y = GroundY(spawnPos) + 0.05f;
            var dir = StreetDir(spawnZ);
            var spawn = Group("PlayerSpawn", root);
            spawn.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(dir));
            var player = CreatePlayer(root, spawnPos, spawn.eulerAngles.y);

            // entrances
            var specs = new[]
            {
                new EntranceSpec { Id = "clinic", Scene = "Clinic", NameKo = "튼튼내과의원", NameEn = "TEUNTEUN CLINIC · 내과", StreetZ = -26f, Side = -1, Color = C("#1FA37A") },
                new EntranceSpec { Id = "immigration", Scene = "Immigration", NameKo = "출입국 민원센터", NameEn = "IMMIGRATION SERVICE CENTER", StreetZ = 6f, Side = 1, Color = C("#2F5FD0") },
                new EntranceSpec { Id = "bank", Scene = "Bank", NameKo = "새싹은행", NameEn = "SAESSAK BANK", StreetZ = 58f, Side = 1, Color = C("#5BAA2E") },
                // exit 6 of 명동역: stairs down from the pavement (see BuildStationEntrance)
                new EntranceSpec { Id = "subway", Scene = "Subway", NameKo = "명동역 · 4호선", NameEn = "MYEONGDONG STATION · LINE 4", StreetZ = -94f, Side = 0, Color = C("#00A4E3") },
            };
            var entrances = new List<BuildingEntrance>();
            var entranceSpots = new List<Vector3>();
            foreach (var s in specs)
            {
                var e = s.Id == "subway" ? BuildStationEntrance(root, s, station) : BuildEntrance(root, s);
                if (e)
                {
                    entrances.Add(e);
                    entranceSpots.Add(e.transform.position);
                }
            }

            var approaches = entrances.Select(e => e.approach.position).Append(spawnPos).ToList();
            // the station exit's footprint keeps props off it at a close range of its own, so it doesn't push the
            // food stalls (which keep 6 m from the spawn and the approaches) off the street
            stationClear = StationKeepClear(station).ToList();
            var decor = Group("Decor", root);
            BuildStreetDressing(decor, entranceSpots, approaches, entrances.Select(e => e.transform).ToList());
            BuildBarriers(root, entranceSpots);
            Physics.SyncTransforms();
            CaptureBackdrops(entrances);

            // pedestrians on a baked navmesh
            var navGo = new GameObject("NavMesh");
            navGo.transform.SetParent(root, false);
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0, 5, 10);
            surface.size = new Vector3(260, 30, 290);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            var navPath = $"{SceneFolder}/Hub_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);

            var peds = Group("Pedestrians", root);
            peds.position = new Vector3(0, 0.35f, 2);
            var spawner = peds.gameObject.AddComponent<PedestrianSpawner>();
            spawner.count = 38;
            spawner.radius = 118;
            spawner.surface = root.Find("paths_Pavement").GetComponent<Collider>();

            var director = new GameObject("HubDirector").AddComponent<HubDirector>();
            director.transform.SetParent(root, false);
            director.player = player;
            director.spawnPoint = spawn;
            director.entrances = entrances.ToArray();
            // street crowd (Resources/Audio/crowd); menus and interiors only have music
            var amb = director.gameObject.AddComponent<AmbientAudio>();
            amb.street = true;
            amb.volume = 0.35f;

            EndMaterials();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Builder] Hub built: {entrances.Count} entrances, spawn {spawnPos}");
        }

        /// <summary>
        /// Photographs 명동길 from each entrance (Backdrop_{id}.png next to the generated textures) so the
        /// interiors show the real street through their windows. A few passers-by stand in for the
        /// crowd, which only spawns at runtime; the objective beams are hidden for the shot.
        /// </summary>
        static void CaptureBackdrops(List<BuildingEntrance> entrances)
        {
            EnsureFolder(TextureKit.Folder);
            var extras = Group("BackdropExtras");
            var rng = new System.Random(99);
            foreach (var e in entrances)
            {
                var fwd = Flat(e.transform.forward);
                var along = Vector3.Cross(Vector3.up, fwd);
                for (int i = 0; i < 10; i++)
                {
                    // far enough out that their size matches the backdrop hung 5 m beyond the windows
                    var p = e.transform.position + fwd * (4.8f + (float)rng.NextDouble() * 2.5f) + along * ((float)rng.NextDouble() * 16f - 8f);
                    p.y = GroundY(p);
                    var rig = HumanBuilder.Build(extras, "Extra", HumanLook.Random(rng), false, false);
                    rig.transform.SetPositionAndRotation(p, Quaternion.LookRotation(rng.Next(2) == 0 ? along : -along));
                }
                if (e.beacon) e.beacon.gameObject.SetActive(false);
            }
            Physics.SyncTransforms();
            foreach (var e in entrances)
            {
                var fwd = Flat(e.transform.forward);
                var go = new GameObject("BackdropCam");
                var cam = go.AddComponent<Camera>();
                var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 800f;
                go.transform.SetPositionAndRotation(e.transform.position + fwd * 0.4f + Vector3.up * 1.55f, Quaternion.LookRotation(fwd));
                const int width = 2048, height = 1024;
                var rt = new RenderTexture(width, height, 24);
                cam.targetTexture = rt;
                cam.aspect = width / (float)height;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                var path = $"{TextureKit.Folder}/Backdrop_{e.entranceId}.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(go);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            foreach (var e in entrances)
                if (e.beacon) e.beacon.gameObject.SetActive(true);
            Object.DestroyImmediate(extras.gameObject);
            Physics.SyncTransforms();
        }

        // =================================================================== MENUS

        /// <summary>Home and scenario select: copies of the hub street with a drifting camera behind the UI.</summary>
        [MenuItem("Seoul First Week/Build Menus")]
        public static void BuildMenus()
        {
            BuildMenuScene("Home", -58f, -20f, 9f);
            BuildMenuScene("Select", 26f, -6f, 8.5f);
        }

        static void BuildMenuScene(string name, float zFrom, float zTo, float height)
        {
            var path = $"{SceneFolder}/{name}.unity";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset($"{SceneFolder}/Hub.unity", path);
            var scene = EditorSceneManager.OpenScene(path);
            var hub = scene.GetRootGameObjects().First(g => g.name == "[Hub]").transform;

            foreach (var t in new[] { "Player", "HubDirector", "Barriers", "PlayerSpawn" })
            {
                var child = hub.Find(t);
                if (child) Object.DestroyImmediate(child.gameObject);
            }
            foreach (var b in hub.GetComponentsInChildren<ObjectiveBeacon>(true)) Object.DestroyImmediate(b.gameObject);
            foreach (var e in hub.GetComponentsInChildren<BuildingEntrance>(true)) Object.DestroyImmediate(e);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(hub, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var drift = camGo.AddComponent<CameraDrift>();
            drift.from = StreetPoint(zFrom) + Vector3.up * height;
            drift.to = StreetPoint(zTo) + Vector3.up * height;
            drift.lookFrom = StreetPoint(zFrom + 40f) + Vector3.up * 3f;
            drift.lookTo = StreetPoint(zTo + 40f) + Vector3.up * 3f;
            camGo.transform.SetPositionAndRotation(drift.from, Quaternion.LookRotation(drift.lookFrom - drift.from));
            if (name == "Home") BuildHomeStage(hub, cam, drift);

            LookKit.Menu(hub);

            var ui = new GameObject(name == "Home" ? "HomeScreen" : "LevelSelectScreen");
            ui.transform.SetParent(hub, false);
            if (name == "Home") ui.AddComponent<HomeScreen>();
            else ui.AddComponent<LevelSelectScreen>().procedures = AllProcedures();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Builder] {name} built");
        }

        /// <summary>
        /// Home's key art: the people this game is about, standing together in 명동길 in front of a low camera
        /// that drifts a little: the student (the player, from Vietnam) talking with the clinic nurse, and the
        /// immigration officer and the bank teller beside them. They fill the right half of the frame; the
        /// title takes the left. HomeScreen finds them by name ("Cast_…") to hang speech balloons over them.
        /// </summary>
        static void BuildHomeStage(Transform hub, Camera cam, CameraDrift drift)
        {
            const float z = -46f;
            var f = StreetDir(z);
            var r = Vector3.Cross(Vector3.up, f);
            var c = StreetPoint(z);
            Vector3 At(float right, float forward)
            {
                var p = c + r * right + f * forward;
                p.y = GroundY(p);
                return p;
            }

            var eye = At(-1.6f, -4.2f) + Vector3.up * 1.62f;
            var look = At(1.1f, 6f) + Vector3.up * 1.55f;
            drift.from = eye;
            drift.to = eye + r * 0.45f + f * 0.35f;
            drift.lookFrom = look;
            drift.lookTo = look + r * 0.25f;
            drift.period = 36f;
            cam.fieldOfView = 44f;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));

            var stage = Group("Cast", hub);
            var rng = new System.Random(2026);
            var student = HumanLook.Student(rng, false);
            student.Skin = C("#C99B74");
            student.HairColor = C("#141110");
            student.Face = HumanLook.FaceFor(rng, false, student.Age, true);
            student.Bag = BagKind.Backpack;
            student.Hat = HatKind.None;
            student.Mask = false;
            student.Glasses = GlassesKind.None;

            HumanRig Person(string id, HumanLook look, Vector3 at, Vector3 facing, bool talking)
            {
                var rig = HumanBuilder.Build(stage, $"Cast_{id}", look);
                rig.transform.position = at;
                facing.y = 0;
                rig.transform.rotation = Quaternion.LookRotation(facing.normalized);
                rig.talking = talking;
                var obstacle = rig.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = 0.35f;
                obstacle.height = 1.8f;
                obstacle.center = new Vector3(0, 0.9f, 0);
                obstacle.carving = true;
                rig.Pose(0f);
                return rig;
            }

            // a staggered line from the front-left to the back-right, so nobody hides behind anybody: the
            // student nearest, half turned to the nurse; the staff turned to the camera
            Vector3 ToCam(Vector3 at) => (eye - at).normalized;
            var meAt = At(0.15f, 0.4f);
            var nurseAt = At(1.3f, 1.3f);
            var officerAt = At(2.6f, 2.1f);
            var tellerAt = At(3.8f, 2.6f);
            var me = Person("Student", student, meAt, ToCam(meAt) * 0.6f + (nurseAt - meAt).normalized * 0.5f, true);
            var nurse = Person("Nurse", HumanLook.Nurse(1), nurseAt, ToCam(nurseAt) * 0.8f + (meAt - nurseAt).normalized * 0.45f, true);
            var officer = Person("Officer", HumanLook.Officer(false, 0), officerAt, ToCam(officerAt), false);
            var teller = Person("Teller", HumanLook.Teller(true, 1), tellerAt, ToCam(tellerAt), false);
            me.lookTarget = nurse.head;
            nurse.lookTarget = me.head;
            officer.lookTarget = cam.transform;
            teller.lookTarget = cam.transform;
            foreach (var rig in new[] { me, nurse, officer, teller }) rig.Pose(0f);
        }

        /// <summary>Every procedure file, for the Logbook on the scenario select.</summary>
        static TextAsset[] AllProcedures() => AssetDatabase.FindAssets("t:TextAsset", new[] { ProcedureFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path)
            .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
            .Where(t => t)
            .ToArray();

        static BuildingEntrance BuildEntrance(Transform parent, EntranceSpec s)
        {
            var street = StreetPoint(s.StreetZ);
            var fwd = StreetDir(s.StreetZ);
            var side = s.Side < 0 ? Vector3.Cross(fwd, Vector3.up) : Vector3.Cross(Vector3.up, fwd);
            street.y = GroundY(street) + 1.2f;
            if (!FacadeHit(street, side, 30f, out var hit))
            {
                Debug.LogError($"[Builder] No facade found for {s.Id} at z={s.StreetZ}");
                return null;
            }
            var normal = hit.normal;
            normal.y = 0;
            normal.Normalize();
            var groundY = GroundY(hit.point + normal * 1.5f);
            var root = Group($"Entrance_{s.Id}", parent);
            root.SetPositionAndRotation(new Vector3(hit.point.x, groundY, hit.point.z) + normal * 0.06f, Quaternion.LookRotation(normal));

            var frame = C("#2B2E33");
            Box(root, "PostL", new Vector3(-1.25f, 1.4f, 0.1f), new Vector3(0.22f, 2.8f, 0.3f), frame);
            Box(root, "PostR", new Vector3(1.25f, 1.4f, 0.1f), new Vector3(0.22f, 2.8f, 0.3f), frame);
            Box(root, "Lintel", new Vector3(0, 2.9f, 0.1f), new Vector3(2.72f, 0.24f, 0.3f), frame);
            Glass(root, "DoorL", new Vector3(-0.58f, 1.3f, 0.02f), new Vector3(1.1f, 2.5f, 0.05f));
            Glass(root, "DoorR", new Vector3(0.58f, 1.3f, 0.02f), new Vector3(1.1f, 2.5f, 0.05f));
            // the lit lobby seen through the glass doors (a pharmacy-white room for the clinic and the bank,
            // a white-and-blue service hall for the immigration office)
            FacadeDresser.GlassPane(root, $"Lobby_{s.Id}", -1.15f, 0.05f, 2.3f, 2.5f, -0.05f, s.Id == "immigration" ? 4 : 6, 5f);
            Box(root, "Mat", new Vector3(0, 0.02f, 0.9f), new Vector3(2.4f, 0.04f, 1.3f), C("#3A3A3A"), false);
            Box(root, "Awning", new Vector3(0, 3.25f, 0.8f), new Vector3(3.4f, 0.12f, 1.5f), s.Color, false);
            var sign = Sign(root, "Sign", new Vector3(0, 4.3f, 0.2f), new Vector2(5.6f, 1.3f), s.Color, $"{s.NameKo}\n<size=36>{s.NameEn}</size>", Color.white, 0, 70, 0.3f);
            Emissive(sign, "Glow", new Vector3(0, -0.72f, 0.1f), new Vector3(5.4f, 0.06f, 0.06f), Color.white, 2f);

            var trigger = root.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 1.3f, 0.5f);
            trigger.size = new Vector3(2.6f, 2.6f, 1.2f);

            float approachZ = Mathf.Max(CorridorMinZ + 14f, s.StreetZ - ApproachDistance);
            var approachPos = StreetPoint(approachZ);
            approachPos.y = GroundY(approachPos) + 0.05f;
            var approach = Group($"Approach_{s.Id}", parent);
            approach.SetPositionAndRotation(approachPos, Quaternion.LookRotation(StreetDir(approachZ)));

            var exit = Group("ExitPoint", root);
            exit.localPosition = new Vector3(0, 0.05f, 2.6f);
            exit.localRotation = Quaternion.identity;

            // objective marker standing on the pavement in front of the door
            var beacon = Group("Beacon", root);
            beacon.localPosition = new Vector3(0, 0, 1.6f);
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
            return entrance;
        }

        // street-level shops of 명동: mostly cosmetics, with fashion, food, exchange and souvenirs
        static readonly string[] ShopNames =
        {
            "화장품", "마스크팩", "스킨케어", "COSMETICS", "화장품 할인", "뷰티", "편의점 24", "카페", "베이커리", "떡볶이",
            "환전 EXCHANGE", "기념품", "안경", "치킨", "분식", "옷가게", "신발", "모자", "양말", "K-POP 굿즈", "휴대폰",
            "한식당", "김밥", "약국", "호떡", "빙수",
        };

        static readonly string[] SignColors =
        {
            "#E4405F", "#F7B32B", "#2BB673", "#3D7BFF", "#8E44AD", "#FF7F11", "#12A4B6", "#D7263D", "#1B998B", "#F46036", "#FFFFFF",
        };

        static readonly string[] Foods = { "떡볶이", "호떡", "계란빵", "닭꼬치", "어묵", "붕어빵", "회오리감자", "군밤" };

        static readonly string[] WallPalette =
        {
            "#D8D2C8", "#CFC5B5", "#C4C7CB", "#E2DBCD", "#B9AC9C", "#CDBBA7", "#AEB7BF", "#D5CEC3",
            "#A7A9AB", "#E6E0D4", "#BFA58A", "#A8705A", "#8F6B5C", "#C9CFD4",
        };

        static readonly string[] RoofPalette = { "#6E6A66", "#5C5F63", "#7A6F66", "#4E5257", "#686C70" };

        /// <summary>Closest point on the 명동길 centre line (XZ) and the distance to it.</summary>
        static (float, Vector3) StreetDistance(Vector3 p)
        {
            float best = float.MaxValue;
            var bestPoint = Vector3.zero;
            for (int i = 0; i < Street.Length - 1; i++)
            {
                var a = new Vector3(Street[i].x, 0, Street[i].y);
                var b = new Vector3(Street[i + 1].x, 0, Street[i + 1].y);
                var q = new Vector3(p.x, 0, p.z);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude);
                var c = a + ab * t;
                float d = (q - c).magnitude;
                if (d < best)
                {
                    best = d;
                    bestPoint = new Vector3(c.x, p.y, c.z);
                }
            }
            return (best, bestPoint);
        }

        static void RecolorCity(System.Random rng)
        {
            // buildings: textured cladding and roofs (UVs are generated, the imports have none)
            // sorted, so every rebuild dresses the same building the same way
            var buildings = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.name.StartsWith("Bldg_"))
                .OrderBy(r => r.name).ThenBy(r => r.transform.position.x).ThenBy(r => r.transform.position.z).ToList();
            BuildingSkin.Apply(buildings, WallPalette, RoofPalette, rng);

            // streets and ground: triplanar textures, so the imported meshes need no UVs either
            const string folder = "Assets/_Project/Art/Environment/Myeongdong/Materials";
            void Skin(string name, TextureKit.Tex tex, string hex, float smoothness = 1f)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{folder}/{name}.mat");
                if (!m) return;
                m.shader = Shader.Find(TextureKit.SurfaceShader);
                m.SetTexture("_BaseMap", tex.Albedo);
                m.SetTexture("_BumpMap", tex.Normal);
                m.SetTextureScale("_BaseMap", Vector2.one / tex.Metres);
                m.SetTextureOffset("_BaseMap", Vector2.zero);
                m.SetColor("_BaseColor", C(hex));
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_BumpScale", 1f);
                EditorUtility.SetDirty(m);
            }
            var paving = TextureKit.Paving();
            var asphalt = TextureKit.Asphalt();
            foreach (var n in new[] { "roads_residential", "roads_service", "roads_pedestrian", "areas_pedestrian", "paths_footway", "paths_steps" }) Skin(n, paving, "#E4E0DA");
            foreach (var n in new[] { "roads_primary", "roads_secondary", "roads_tertiary" }) Skin(n, asphalt, "#FFFFFF");
            Skin("Ground", asphalt, "#D9D5CE");
            var veg = AssetDatabase.LoadAssetAtPath<Material>($"{folder}/vegetation.mat");
            if (veg)
            {
                veg.shader = Shader.Find(TextureKit.SurfaceShader);
                var grass = TextureKit.FabricWeave();
                veg.SetTexture("_BaseMap", grass.Albedo);
                veg.SetTexture("_BumpMap", grass.Normal);
                veg.SetTextureScale("_BaseMap", Vector2.one / 0.6f);
                veg.SetColor("_BaseColor", C("#6E9F52"));
                veg.SetFloat("_Smoothness", 0.3f);
                EditorUtility.SetDirty(veg);
            }
        }

        /// <summary>True when a box would poke into any building.</summary>
        static bool HitsBuilding(Vector3 center, Vector3 halfExtents, Quaternion rotation) =>
            Physics.OverlapBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore).Any(c => c.name.StartsWith("Bldg_"));

        /// <summary>
        /// The street cross-section at <paramref name="z"/>: facade hits on both sides, the midpoint
        /// between them and the street axis taken from the two wall normals (not the coarse centre line).
        /// </summary>
        static bool CrossSection(float z, float height, float reach, out Vector3 mid, out Vector3 along, out Vector3 across, out float width)
        {
            var c = StreetPoint(z);
            c.y = GroundY(c) + height;
            var fwd = StreetDir(z);
            var right = Vector3.Cross(Vector3.up, fwd);
            mid = along = across = Vector3.zero;
            width = 0;
            if (!FacadeHit(c, right, reach, out var r) || !FacadeHit(c, -right, reach, out var l)) return false;
            var nr = Flat(r.normal);
            var nl = Flat(l.normal);
            across = (nl - nr).normalized;
            if (across.sqrMagnitude < 0.5f || Vector3.Dot(across, right) < 0.7f) across = right;
            along = Vector3.Cross(across, Vector3.up);
            if (Vector3.Dot(along, fwd) < 0) along = -along;
            mid = (r.point + l.point) / 2f;
            width = Vector3.Dot(r.point - l.point, across);
            return true;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.normalized;
        }

        static void BuildStreetDressing(Transform decor, List<Vector3> entrances, List<Vector3> keepClear, List<Transform> entranceRoots)
        {
            var rng = new System.Random(2026);
            bool NearEntrance(Vector3 p, float r) => entrances.Any(e => (new Vector2(e.x, e.z) - new Vector2(p.x, p.z)).magnitude < r);
            bool NearSpawn(Vector3 p, float r) => keepClear.Any(e => (new Vector2(e.x, e.z) - new Vector2(p.x, p.z)).magnitude < r)
                || stationClear.Any(e => (new Vector2(e.x, e.z) - new Vector2(p.x, p.z)).magnitude < 2.6f);

            RecolorCity(rng);

            // street lamps and planters against the facades, alternating sides
            int lamps = 0, planters = 0, skipped = 0, slot = 0;
            for (float z = CorridorMinZ + 4; z <= CorridorMaxZ; z += 9f, slot++)
            {
                var c = StreetPoint(z);
                c.y = GroundY(c) + 1.2f;
                var fwd = StreetDir(z);
                var sideDir = slot % 2 == 0 ? Vector3.Cross(fwd, Vector3.up) : Vector3.Cross(Vector3.up, fwd);
                if (!FacadeHit(c, sideDir, 10f, out var hit)) continue;
                var n = Flat(hit.normal);
                // walls that don't face the street squarely are corners or side streets
                if (Vector3.Dot(n, -sideDir) < 0.85f) { skipped++; continue; }
                var basePos = hit.point + n * 0.6f;
                basePos.y = GroundY(basePos);
                var rot = Quaternion.LookRotation(n);
                if (NearEntrance(basePos, 4.5f) || NearSpawn(basePos, 2f)) continue;
                bool lamp = slot % 4 < 2;
                var probe = lamp ? new Vector3(0.3f, 1.9f, 0.55f) : new Vector3(0.8f, 0.45f, 0.35f);
                var probeCenter = basePos + Vector3.up * (probe.y + 0.1f) + n * (lamp ? 0.25f : 0f);
                if (HitsBuilding(probeCenter, probe, rot)) { skipped++; continue; }
                if (lamp)
                {
                    var l = Group("StreetLamp", decor);
                    l.SetPositionAndRotation(basePos, rot);
                    Cylinder(l, "Pole", new Vector3(0, 2.1f, 0), new Vector3(0.12f, 2.1f, 0.12f), C("#2F3237"), true);
                    Box(l, "Arm", new Vector3(0, 4.1f, 0.35f), new Vector3(0.06f, 0.06f, 0.75f), C("#2F3237"), false);
                    Box(l, "Head", new Vector3(0, 4.02f, 0.7f), new Vector3(0.28f, 0.1f, 0.4f), C("#2F3237"), false);
                    var glow = Emissive(l, "Glow", new Vector3(0, 3.96f, 0.7f), new Vector3(0.24f, 0.02f, 0.34f), C("#FFE9C2"), 2.2f);
                    glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    StreetProps.LampBanners(l, lamps);
                    lamps++;
                }
                else
                {
                    var planter = Group("Planter", decor);
                    planter.SetPositionAndRotation(basePos, rot);
                    Box(planter, "Box", new Vector3(0, 0.28f, 0), new Vector3(1.4f, 0.56f, 0.55f), C("#6B6F75"));
                    Box(planter, "Soil", new Vector3(0, 0.57f, 0), new Vector3(1.3f, 0.02f, 0.45f), C("#4A3B30"), false);
                    for (int i = 0; i < 3; i++)
                        Sphere(planter, "Shrub", new Vector3(-0.42f + i * 0.42f, 0.78f, 0), new Vector3(0.5f, 0.45f, 0.45f), C(i % 2 == 0 ? "#4F8A3C" : "#5E9E48"));
                    planters++;
                }
            }

            // street food stalls (명동 street food), squared to the facades on both sides
            int stalls = 0, side2 = 1;
            for (float z = -50; z <= CorridorMaxZ - 4; z += 11f)
            {
                if (!CrossSection(z, 1.2f, 16f, out var mid, out var along, out var across, out var width)) continue;
                // 명동길 is only 7–9 m wide: the cart goes just off the middle, leaving a lane on both sides
                if (width < 6.2f) continue;
                var p = mid + across * (side2 * Mathf.Min(0.9f, width / 2f - 2.4f));
                side2 = -side2;
                p.y = GroundY(p);
                if (NearEntrance(p, 9f) || NearSpawn(p, 6f)) continue;
                var rot = Quaternion.LookRotation(along);
                if (HitsBuilding(p + Vector3.up * 1.4f, new Vector3(0.9f, 1.2f, 1.5f), rot)) continue;
                BuildStall(decor, p, along, rng);
                stalls++;
            }

            // banners with string lights across the street
            int banners = 0;
            foreach (var z in new[] { -35f, 22f, 44f })
            {
                if (!CrossSection(z, 6.5f, 14f, out var mid, out _, out var across, out var width)) continue;
                var l = mid - across * (width / 2f);
                var r = mid + across * (width / 2f);
                var wire = Box(decor, "Wire", mid, new Vector3(0.02f, 0.02f, width), C("#333333"), false);
                wire.transform.rotation = Quaternion.LookRotation(across);
                // the banner hangs in the plane of the wire, readable from both directions
                var banner = Sign(decor, "Banner", mid + Vector3.down * 0.55f, new Vector2(Mathf.Min(width * 0.6f, 5.5f), 0.8f), C("#E4405F"), "명동 · MYEONG-DONG", Color.white, 0, 70, 0.04f);
                banner.rotation = Quaternion.LookRotation(Vector3.Cross(across, Vector3.up));
                var back = Object.Instantiate(banner.Find("Text").gameObject, banner);
                back.transform.localPosition = new Vector3(0, 0, -0.03f);
                back.transform.localRotation = Quaternion.identity;
                for (int i = 0; i < 14; i++)
                {
                    float t = (i + 0.5f) / 14f;
                    var pos = Vector3.Lerp(l, r, t) + Vector3.down * (0.1f + Mathf.Sin(t * Mathf.PI) * 0.35f);
                    var bulb = Prims.Make(PrimitiveType.Sphere, decor, "Bulb", pos, Vector3.one * 0.11f, Mats.Emissive(C(i % 2 == 0 ? "#FFD27A" : "#FF8FA3"), 2.5f));
                    bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                banners++;
            }
            // street furniture against the facades: A-frames and sales tables by the shops, bins, parked
            // delivery scooters and utility cabinets, alternating sides, never in front of a door
            Physics.SyncTransforms();
            var placed = decor.Cast<Transform>().Where(t => t.name is "StreetLamp" or "Planter" or "FoodStall").Select(t => t.position).ToList();
            bool Crowded(Vector3 p, float r) => placed.Any(q => (new Vector2(q.x, q.z) - new Vector2(p.x, p.z)).magnitude < r);
            int furniture = 0, slot3 = 0;
            for (float z = CorridorMinZ + 3f; z <= CorridorMaxZ - 2f; z += 2.4f, slot3++)
            {
                var c = StreetPoint(z);
                c.y = GroundY(c) + 1.2f;
                var fwd = StreetDir(z);
                var sideDir = slot3 % 2 == 0 ? Vector3.Cross(Vector3.up, fwd) : Vector3.Cross(fwd, Vector3.up);
                if (rng.NextDouble() < 0.3 || !FacadeHit(c, sideDir, 10f, out var hit)) continue;
                var n = Flat(hit.normal);
                if (Vector3.Dot(n, -sideDir) < 0.85f) continue;
                var along = Vector3.Cross(n, Vector3.up);
                double pick = rng.NextDouble();
                int kind = pick < 0.34 ? 0 : pick < 0.56 ? 1 : pick < 0.72 ? 2 : pick < 0.9 ? 3 : 4;
                float depth = kind switch { 0 => 0.75f, 1 => 0.55f, 2 => 0.35f, 3 => 0.8f, _ => 0.3f };
                var basePos = hit.point + n * depth;
                basePos.y = GroundY(basePos);
                if (NearEntrance(basePos, 3.6f) || NearSpawn(basePos, 2.5f) || Crowded(basePos, 2f)) continue;
                var rot = kind is 0 or 3 ? Quaternion.LookRotation(along) : Quaternion.LookRotation(n);
                var half = kind switch { 0 => new Vector3(0.3f, 0.45f, 0.25f), 1 => new Vector3(0.55f, 0.45f, 0.3f), 2 => new Vector3(0.52f, 0.5f, 0.22f), 3 => new Vector3(0.3f, 0.6f, 0.8f), _ => new Vector3(0.53f, 0.66f, 0.25f) };
                if (HitsBuilding(basePos + Vector3.up * (half.y + 0.05f), half, rot)) continue;
                switch (kind)
                {
                    case 0: StreetProps.AFrame(decor, basePos, rot, rng); break;
                    case 1: StreetProps.DisplayTable(decor, basePos, rot, rng); break;
                    case 2: StreetProps.Bins(decor, basePos, rot); break;
                    case 3: StreetProps.Scooter(decor, basePos, rot, rng); break;
                    default: StreetProps.UtilityBox(decor, basePos, rot); break;
                }
                placed.Add(basePos);
                furniture++;
            }
            // manhole covers in the paving
            for (float z = CorridorMinZ + 9f; z < CorridorMaxZ; z += 17f + (float)rng.NextDouble() * 8f)
            {
                var p = StreetPoint(z) + Vector3.Cross(Vector3.up, StreetDir(z)) * ((float)rng.NextDouble() * 2.4f - 1.2f);
                p.y = GroundY(p);
                if (!Crowded(p, 1.8f) && !NearSpawn(p, 1.5f)) StreetProps.Manhole(decor, p);
            }

            // windows everywhere and shop fronts along 명동길, last, so windows can steer clear of
            // the doors, signs and props already standing against the facades
            Physics.SyncTransforms();
            var occluders = new List<Vector3[]>();
            foreach (var mf in entranceRoots.SelectMany(e => e.GetComponentsInChildren<MeshFilter>()).Concat(decor.GetComponentsInChildren<MeshFilter>()))
                if (!mf.GetComponentInParent<ObjectiveBeacon>()) occluders.Add(FacadeDresser.Corners(mf));
            var buildings = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(m => m.name.StartsWith("Bldg_"))
                .OrderBy(m => m.name).ThenBy(m => m.transform.position.x).ThenBy(m => m.transform.position.z).ToList();
            FacadeDresser.Dress(decor, buildings, new FacadeDresser.Options
            {
                StreetDistance = StreetDistance,
                Keepouts = entrances,
                ShopNames = ShopNames,
                ShopColors = SignColors,
                Seed = 2026,
                Occluders = occluders,
            });
            Physics.SyncTransforms();
            Debug.Log($"[Builder] Street dressing: {lamps} lamps, {planters} planters ({skipped} skipped for clearance), {stalls} stalls, {banners} banners, {furniture} street furniture");
        }

        /// <summary>
        /// Distance from the centre line to the facade in <paramref name="dir"/>. Looks at two heights so
        /// arcades and awnings don't read as gaps; openings (side streets, deep recesses) reuse the last wall.
        /// </summary>
        static float FacadeDistance(Vector3 center, Vector3 dir, float reach, ref float last) => FacadeDistance(center, dir, reach, ref last, out _);

        static float FacadeDistance(Vector3 center, Vector3 dir, float reach, ref float last, out bool found)
        {
            found = FacadeHit(center, dir, reach, out var hit) || FacadeHit(center + Vector3.up * 2.5f, dir, reach, out hit);
            if (found) last = Mathf.Clamp(Vector3.Dot(hit.point - center, dir), 2.2f, reach);
            return last;
        }

        /// <summary>
        /// Paving over the whole playable street, from facade to facade and a little under the buildings.
        /// The imported roads float 0.4 m above the ground and stop short of some buildings, which left
        /// trenches along the walls where door mats and other props seemed to hover.
        /// </summary>
        static void BuildPavement(Transform root, List<Vector4> holes = null)
        {
            // cross-sections every metre along the street, 64 strips across it; a cell inside one of the openings
            // (x..y along the centre line's z, z..w across it) is left out
            const float step = 1f, reach = 7.5f, under = 1.4f, lift = 0.02f;
            const int across = 64;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float lastL = 4f, lastR = 4f, along = 0f, prevZ = 0f;
            float[] prevU = null;
            var prev = StreetPoint(CorridorMinZ - 8f);
            for (float z = CorridorMinZ - 8f; z <= CorridorMaxZ + 8.01f; z += step)
            {
                var c = StreetPoint(z);
                along += (c - prev).magnitude;
                prev = c;
                float top = GroundY(c) + lift;
                c.y = top + 1.2f;
                var right = Vector3.Cross(Vector3.up, StreetDir(z));
                float dl = FacadeDistance(c, -right, reach, ref lastL) + under, dr = FacadeDistance(c, right, reach, ref lastR) + under;
                int i0 = verts.Count;
                var us = new float[across + 1];
                for (int j = 0; j <= across; j++)
                {
                    float u = -dl + (dl + dr) * j / across;
                    us[j] = u;
                    var v = c + right * u;
                    v.y = top;
                    verts.Add(v);
                    // UVs in metres across and along the street, so the pavers follow its bends
                    uvs.Add(new Vector2(u, along));
                }
                if (prevU != null)
                {
                    int a0 = i0 - (across + 1);
                    for (int j = 0; j < across; j++)
                    {
                        if (InOpening(holes, prevZ, z, Mathf.Min(prevU[j], us[j]), Mathf.Max(prevU[j + 1], us[j + 1]))) continue;
                        tris.AddRange(new[] { a0 + j, i0 + j, a0 + j + 1, a0 + j + 1, i0 + j, i0 + j + 1 });
                    }
                }
                prevU = us;
                prevZ = z;
            }

            var mesh = new Mesh { name = "Hub_Pavement", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            const string folder = "Assets/_Project/Art/Generated/Meshes";
            EnsureFolder(folder);
            AssetDatabase.DeleteAsset($"{folder}/{mesh.name}.asset");
            AssetDatabase.CreateAsset(mesh, $"{folder}/{mesh.name}.asset");

            // named like the imported paths so GroundY treats it as walkable ground
            var go = new GameObject("paths_Pavement");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = TextureKit.LitUV("Pavement", TextureKit.Paving(), C("#F4F1EC"));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            Debug.Log($"[Builder] Pavement: {verts.Count / (across + 1)} cross-sections");
        }

        static bool InOpening(List<Vector4> holes, float z0, float z1, float u0, float u1)
        {
            if (holes == null) return false;
            const float eps = 0.01f;
            foreach (var h in holes)
                if (z0 >= h.x - eps && z1 <= h.y + eps && u1 > h.z + eps && u0 < h.w - eps) return true;
            return false;
        }

        /// <summary>
        /// Invisible walls that keep the player on the playable stretch of 명동길. A continuous, thick wall
        /// runs along both sides with its inner face on the facade line (the imported building meshes are
        /// single-sided and a pushing capsule can slip through them), closes side streets and deep
        /// recesses at the street edge, and caps both ends.
        /// </summary>
        static void BuildBarriers(Transform root, List<Vector3> doors)
        {
            var group = Group("Barriers", root);
            // anything deeper than this off the centre line (side streets, plazas, deep recesses) is closed off
            const float step = 1f, reach = 7.5f, height = 6f, thick = 1.2f;
            int segments = 0;

            void Wall(Vector3 center, Quaternion rot, Vector3 size)
            {
                var go = new GameObject("Barrier");
                go.layer = 2; // Ignore Raycast: interaction rays pass through
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(center, rot);
                go.AddComponent<BoxCollider>().size = size;
                go.isStatic = true;
                segments++;
            }

            // bollards show where the invisible wall closes a side street or a deep recess
            var bollards = Group("Bollards", root);
            var lastBollard = new Vector3(0, -99, 0);
            int posts = 0;
            void Post(Vector3 p, float spacing)
            {
                if ((p - lastBollard).sqrMagnitude < spacing * spacing) return;
                p.y = GroundY(p);
                StreetProps.Bollard(bollards, p);
                lastBollard = p;
                posts++;
            }

            foreach (var side in new[] { -1f, 1f })
            {
                float lastDist = 4f, prevDist = -1f;
                for (float z = CorridorMinZ; z <= CorridorMaxZ; z += step)
                {
                    var c = StreetPoint(z);
                    float ground = GroundY(c);
                    c.y = ground + 1.2f;
                    var fwd = StreetDir(z);
                    var dir = Vector3.Cross(Vector3.up, fwd) * side;
                    float dist = FacadeDistance(c, dir, reach, ref lastDist, out bool facade);
                    if (!facade) Post(c + dir * (dist - 0.3f), 1.5f);
                    // keep the space in front of a door open: the wall steps back behind the door plane
                    foreach (var door in doors)
                    {
                        var toDoor = door - c;
                        float along = Mathf.Abs(Vector3.Dot(toDoor, fwd)), across = Vector3.Dot(toDoor, dir);
                        if (along < 2.4f && across > 0) dist = Mathf.Max(dist, across + 0.15f);
                    }
                    var rot = Quaternion.LookRotation(fwd);
                    var p = c + dir * (dist + thick / 2f);
                    p.y = ground + height / 2f - 0.5f;
                    Wall(p, rot, new Vector3(thick, height, step + 0.4f));
                    // a setback between two samples leaves a step in the wall: close it
                    if (prevDist > 0 && Mathf.Abs(dist - prevDist) > 0.6f)
                    {
                        float a = Mathf.Min(dist, prevDist), b = Mathf.Max(dist, prevDist) + thick;
                        var q = c - fwd * (step / 2f) + dir * ((a + b) / 2f);
                        q.y = p.y;
                        Wall(q, rot, new Vector3(b - a, height, 0.5f));
                    }
                    prevDist = dist;
                }
            }

            foreach (var z in new[] { CorridorMinZ, CorridorMaxZ })
            {
                var c = StreetPoint(z);
                c.y = GroundY(c) + height / 2f - 0.5f;
                var fwd = StreetDir(z);
                Wall(c + fwd * (z < 0 ? -0.6f : 0.6f), Quaternion.LookRotation(fwd), new Vector3(40f, height, thick));
                // a row of bollards across the street marks the end of the pedestrian zone
                var e = StreetPoint(z);
                e.y = GroundY(e) + 1.2f;
                var right = Vector3.Cross(Vector3.up, fwd);
                float l = 4f, r = 4f;
                l = FacadeDistance(e, -right, reach, ref l);
                r = FacadeDistance(e, right, reach, ref r);
                for (float x = -l + 0.5f; x <= r - 0.5f; x += 1.3f)
                {
                    // not on the station exit's curbs, at the south end
                    var post = e + right * x;
                    if (stationClear.Any(p => (new Vector2(p.x, p.z) - new Vector2(post.x, post.z)).magnitude < 1.9f)) continue;
                    Post(post, 1f);
                }
            }
            Debug.Log($"[Builder] Barriers: {segments} segments, {posts} bollards");
        }

        static void BuildStall(Transform parent, Vector3 pos, Vector3 fwd, System.Random rng)
        {
            var stall = Group("FoodStall", parent);
            stall.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd));
            var color = C(SignColors[rng.Next(SignColors.Length - 1)]);
            var steel = C("#C9CED4");
            var dark = C("#2F3237");
            // cart body with coloured skirt, wheels and a steel counter
            Box(stall, "Cart", new Vector3(0, 0.55f, 0), new Vector3(0.95f, 0.8f, 1.8f), steel);
            Box(stall, "Skirt", new Vector3(0, 0.82f, 0), new Vector3(0.97f, 0.16f, 1.82f), color, false);
            foreach (var z in new[] { -0.6f, 0.6f })
            foreach (var x in new[] { -0.5f, 0.5f })
            {
                var wheel = Cylinder(stall, "Wheel", new Vector3(x, 0.16f, z), new Vector3(0.3f, 0.04f, 0.3f), dark);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            Box(stall, "Top", new Vector3(0, 0.97f, 0), new Vector3(1.05f, 0.05f, 1.9f), C("#9AA0A6"), false);
            Box(stall, "Griddle", new Vector3(0, 1.01f, 0.25f), new Vector3(0.75f, 0.03f, 1.0f), C("#3A3C40"), false);
            // food on the griddle
            var foodColor = C(rng.NextDouble() < 0.5 ? "#D8452B" : "#E3A64B");
            for (int i = 0; i < 14; i++)
            {
                var piece = Prims.Make(PrimitiveType.Capsule, stall, "Food",
                    new Vector3(-0.28f + (i % 7) * 0.09f, 1.05f, -0.05f + (i / 7) * 0.4f + (float)rng.NextDouble() * 0.2f),
                    new Vector3(0.05f, 0.07f, 0.05f), Mats.Lit(foodColor, 0.5f), false, Quaternion.Euler(90, (float)rng.NextDouble() * 180f, 0));
                piece.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // striped canopy on four poles
            foreach (var z in new[] { -0.9f, 0.9f })
            foreach (var x in new[] { -0.5f, 0.5f })
                Box(stall, "Pole", new Vector3(x, 1.7f, z), new Vector3(0.04f, 1.4f, 0.04f), dark, false);
            for (int i = 0; i < 5; i++)
                Box(stall, "Canopy", new Vector3(-0.64f + i * 0.32f, 2.44f, 0), new Vector3(0.32f, 0.06f, 2.3f), i % 2 == 0 ? color : Color.white, false);
            // name boards front and back
            var food = Foods[rng.Next(Foods.Length)];
            foreach (var dir in new[] { 1f, -1f })
            {
                var sign = Sign(stall, "Sign", new Vector3(0, 2.72f, 1.12f * dir), new Vector2(1.5f, 0.42f), Color.white, food, color, dir > 0 ? 0 : 180, 60, 0.05f);
                sign.GetChild(0).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // menu with prices on the customers' side, bare bulbs under the canopy, steam off the griddle
            var price = food switch { "어묵" => "1,000원", "호떡" or "붕어빵" or "계란빵" => "2,000원", "군밤" => "5,000원", _ => "4,000원" };
            Sign(stall, "Menu", new Vector3(0.53f, 1.35f, 0.1f), new Vector2(0.62f, 0.44f), C("#12141C"), $"{food}\n<color=#FFD23F>{price}</color>", Color.white, 90, 44, 0.02f);
            foreach (var z in new[] { -0.45f, 0.45f })
            {
                Cylinder(stall, "BulbWire", new Vector3(0.1f, 2.33f, z), new Vector3(0.01f, 0.08f, 0.01f), dark);
                var bulb = Prims.Make(PrimitiveType.Sphere, stall, "Bulb", new Vector3(0.1f, 2.22f, z), Vector3.one * 0.09f, Mats.Emissive(C("#FFD58A"), 3f));
                bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            StreetProps.Steam(stall, new Vector3(0, 1.08f, 0.25f), new Vector3(0.6f, 0.02f, 0.9f));
            Cylinder(stall, "Cups", new Vector3(0.38f, 1.08f, -0.75f), new Vector3(0.08f, 0.08f, 0.08f), C("#F4F1EA"));

            var vendor = HumanBuilder.Build(stall, "Vendor", HumanLook.Vendor(new System.Random(rng.Next())));
            vendor.transform.localPosition = new Vector3(-0.85f, 0, 0.2f);
            vendor.transform.localRotation = Quaternion.Euler(0, 90, 0);
            vendor.typing = true;
            vendor.Pose(0f);
            Cylinder(stall, "StoolLeg", new Vector3(-1.05f, 0.2f, -0.95f), new Vector3(0.05f, 0.2f, 0.05f), dark, false);
            Cylinder(stall, "StoolSeat", new Vector3(-1.05f, 0.42f, -0.95f), new Vector3(0.3f, 0.03f, 0.3f), C("#2F6FD0"), true);
        }

        // =================================================================== INTERIORS
        //
        // Three different places on one skeleton (see CLAUDE.md, Interiors). The shell gives every room its
        // walls, street glazing, automatic doors, lights and exit, and the counters; the place builders add
        // everything that makes it that place: a small warm 내과 with sofas and treatment-room doors, a big
        // cool immigration hall with steel beam seats, six numbered counters and an LED ticker, and a polished
        // bank branch with its 365 ATM corner, brand benches, wood slats and a VIP room.

        /// <summary>
        /// High: a clinic reception (접수대) where the patient stands at a 1.05 m ledge and the nurse sits
        /// behind it at a desk. Low: a bank or public-office counter at desk height with a chair for the
        /// customer, the way Korean tellers and officers serve people.
        /// </summary>
        enum CounterStyle { High, Low }

        /// <summary>Waiting seats: moulded shells on a beam, perforated steel beam seats, or upholstered sofas.</summary>
        enum SeatKind { Shell, Steel, Sofa }

        struct RoomSpec
        {
            public string Name, Backdrop;
            public float W, D, H;
            public CounterStyle Style;
            public float CounterX0, CounterX1;
            public float[] WindowX;
            public string[] NamePlates;
            public Color Wall, Accent, Light;
            public Material Floor, Ceiling, CounterFront, Cabinet;
            public Material Partition;  // solid partitions between windows (null: a glass divider on the desk)
            public bool Screens;        // tall acrylic screens on each window
            // no street front: a solid south wall with an open passage (an underground station, an airport hall)
            // instead of the glazed shop front, the sliding doors and the captured street behind them
            public bool Enclosed;
            public string PassageSign;  // the sign over the passage, e.g. "출구 Exit"
            public Color PassageGlow;   // the light at the far end of the passage (daylight up the stairs)
        }

        class Room
        {
            public Transform Root;
            public float W, D, H;
            public float CounterFrontZ, AisleZ, DeskBack;
            public float[] WindowX;
            public CounterStyle Style;
            public Color Accent;
            public Material WallMat, TopMat, SteelMat;
            public readonly List<Transform> Seats = new();
            public readonly List<float> SeatGaps = new();
            public readonly List<Transform> CounterSpots = new();
            public readonly List<Transform> StaffSeats = new();
            public readonly List<Transform> Anchors = new();
            public readonly List<NpcActor> Actors = new();
            public readonly List<QueueBoard> Boards = new();
            public Transform Entrance, MachineSpot;
            public TicketMachine Machine;
            public readonly List<CounterSpot> Counters = new();
            public ExitDoor Exit;
            public SeoulFirstWeek.Player.FirstPersonController Player;
        }

        static Scene NewScene(string name)
        {
            var path = $"{SceneFolder}/{name}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        /// <summary>Box with a given material (Surface materials project their texture in metres).</summary>
        static GameObject Slab(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = true, bool shadows = true)
        {
            var go = Prims.Make(PrimitiveType.Cube, parent, name, pos, size, m, collider);
            if (!shadows) go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// Merges every mesh under a group (all sharing one material) into one static mesh: slat walls and
        /// slat ceilings would otherwise cost a renderer per slat.
        /// </summary>
        static void Merge(Transform group, string assetName)
        {
            var filters = group.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return;
            var material = filters[0].GetComponent<MeshRenderer>().sharedMaterial;
            var parts = filters.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = group.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray();
            var mesh = new Mesh { name = assetName, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, true, true);
            const string folder = "Assets/_Project/Art/Generated/Meshes";
            EnsureFolder(folder);
            var path = $"{folder}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            foreach (var f in filters) Object.DestroyImmediate(f.gameObject);
            group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = group.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(group.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// The street outside the entrance, captured from the hub (see <see cref="CaptureBackdrops"/>) and
        /// hung as a flat unlit backdrop a few metres beyond the glass.
        /// </summary>
        static void Backdrop(Transform root, string id, float z, float width)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureKit.Folder}/Backdrop_{id}.png");
            var m = TextureKit.MaterialAsset($"Backdrop_{id}", "Universal Render Pipeline/Unlit");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tex ? new Color(1.08f, 1.08f, 1.08f) : C("#DCE6F0"));
            EditorUtility.SetDirty(m);
            var quad = Prims.Make(PrimitiveType.Quad, root, "StreetBackdrop", new Vector3(0, 1.55f, z), new Vector3(width, width / 2f, 1), m);
            quad.transform.localRotation = Quaternion.Euler(0, 180, 0);
            quad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Walls, floor and ceiling; the street side with its windows and automatic sliding doors; the six
        /// ceiling lights in the room's colour temperature; the exit, the player and the counters.
        /// </summary>
        static Room Shell(RoomSpec s)
        {
            float w = s.W, d = s.D, h = s.H;
            var r = new Room { Root = Group($"[{s.Name}]"), W = w, D = d, H = h, Style = s.Style, Accent = s.Accent, WindowX = s.WindowX };
            var root = r.Root;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            // a weak sun through the (shadowless) ceiling, like daylight from the windows: the trilight ambient of
            // LookKit.Interior does most of the soft lighting, so the halftone doesn't dot every wall
            var sun = Sun(root, new Vector3(62, 24, 0), 0.24f, C("#FFF1DE"));
            sun.shadowStrength = 0.5f;
            LookKit.Interior(root);

            r.WallMat = TextureKit.Surface($"Wall_{s.Name}", TextureKit.WallPaint(), s.Wall);
            r.TopMat = TextureKit.Surface("CounterTop", TextureKit.Plastic(), C("#F3F1EB"), 0.9f);
            r.SteelMat = TextureKit.Surface("Steel", TextureKit.Metal(), C("#C9CDD2"), 0.8f, 0.85f, true);
            var frameMat = Mats.Lit(C("#3A3D42"), 0.4f, 0.4f);
            float side = (w - 2.6f) / 2f;
            // a low clinic ceiling gets a lower door; windows line up with the door head
            float door = Mathf.Min(2.6f, h - 0.45f), sill = 0.9f, head = Mathf.Min(2.45f, door), zS = -d / 2 - 0.1f;

            Slab(root, "Floor", new Vector3(0, -0.05f, 0), new Vector3(w, 0.1f, d), s.Floor);
            Slab(root, "Ceiling", new Vector3(0, h + 0.05f, 0), new Vector3(w, 0.1f, d), s.Ceiling, true, false);
            Slab(root, "WallN", new Vector3(0, h / 2, d / 2 + 0.1f), new Vector3(w + 0.4f, h, 0.2f), r.WallMat);
            Slab(root, "WallE", new Vector3(w / 2 + 0.1f, h / 2, 0), new Vector3(0.2f, h, d), r.WallMat);
            Slab(root, "WallW", new Vector3(-w / 2 - 0.1f, h / 2, 0), new Vector3(0.2f, h, d), r.WallMat);
            if (s.Enclosed)
            {
                Passage(r, s, door, frameMat);
                return r;
            }
            foreach (var sx in new[] { -1f, 1f })
            {
                float cx = sx * (1.3f + side / 2f);
                Slab(root, "WallS_Low", new Vector3(cx, sill / 2f, zS), new Vector3(side, sill, 0.2f), r.WallMat);
                Slab(root, "WallS_High", new Vector3(cx, (head + h) / 2f, zS), new Vector3(side, h - head, 0.2f), r.WallMat);
                // piers at the door and at the corner, glazing between them
                Slab(root, "WallS_Pier", new Vector3(sx * (1.3f + 0.2f), (sill + head) / 2f, zS), new Vector3(0.4f, head - sill, 0.2f), r.WallMat);
                Slab(root, "WallS_Pier", new Vector3(sx * (w / 2f - 0.25f), (sill + head) / 2f, zS), new Vector3(0.5f, head - sill, 0.2f), r.WallMat);
                float g0 = 1.7f, g1 = w / 2f - 0.5f;
                Glass(root, "StreetWindow", new Vector3(sx * (g0 + g1) / 2f, (sill + head) / 2f, zS), new Vector3(g1 - g0, head - sill, 0.03f));
                int mullions = Mathf.Max(1, Mathf.RoundToInt((g1 - g0) / 1.3f));
                for (int m = 0; m <= mullions; m++)
                    Slab(root, "Mullion", new Vector3(sx * Mathf.Lerp(g0, g1, m / (float)mullions), (sill + head) / 2f, zS + 0.02f), new Vector3(0.05f, head - sill, 0.08f), frameMat, false);
                Slab(root, "WindowSill", new Vector3(sx * (g0 + g1) / 2f, sill + 0.02f, zS + 0.12f), new Vector3(g1 - g0 + 0.1f, 0.04f, 0.22f), r.TopMat, false);
                Slab(root, "WindowHead", new Vector3(sx * (g0 + g1) / 2f, head, zS + 0.02f), new Vector3(g1 - g0, 0.05f, 0.08f), frameMat, false);
            }
            Slab(root, "Lintel", new Vector3(0, door + (h - door) / 2, zS), new Vector3(2.6f, h - door, 0.2f), r.WallMat);

            // automatic sliding doors (자동문): the two leaves slide in front of the piers; a blocker on Ignore
            // Raycast keeps the doorway closed to the player however wide they open
            var doors = Group("AutoDoor", root);
            doors.position = new Vector3(0, 0, -d / 2 + 0.05f);
            var band = Mats.Lit(s.Accent, 0.3f);
            Transform Leaf(string name, float x)
            {
                var leaf = Group(name, doors);
                leaf.localPosition = new Vector3(x, 0, 0);
                Glass(leaf, "Glass", new Vector3(0, door / 2f, 0), new Vector3(1.24f, door - 0.02f, 0.02f), 0, false);
                Slab(leaf, "Bottom", new Vector3(0, 0.05f, 0), new Vector3(1.24f, 0.1f, 0.04f), frameMat, false);
                Slab(leaf, "Top", new Vector3(0, door - 0.04f, 0), new Vector3(1.24f, 0.08f, 0.04f), frameMat, false);
                Slab(leaf, "Stile", new Vector3(-Mathf.Sign(x) * 0.6f, door / 2f, 0), new Vector3(0.04f, door, 0.045f), frameMat, false);
                // the band at eye level that keeps people from walking into the glass
                Slab(leaf, "Band", new Vector3(0, 1.45f, 0.012f), new Vector3(1.2f, 0.07f, 0.003f), band, false, false);
                return leaf;
            }
            var auto = doors.gameObject.AddComponent<AutoDoor>();
            auto.left = Leaf("LeafL", -0.62f);
            auto.right = Leaf("LeafR", 0.62f);
            Slab(root, "DoorHeader", new Vector3(0, door + 0.07f, -d / 2 + 0.08f), new Vector3(5.0f, 0.14f, 0.14f), frameMat, false);
            Slab(root, "DoorSensor", new Vector3(0, door - 0.03f, -d / 2 + 0.12f), new Vector3(0.3f, 0.06f, 0.06f), Mats.Lit(C("#2B2E33"), 0.4f), false);
            var blocker = new GameObject("DoorBlocker") { layer = 2 };
            blocker.transform.SetParent(root, false);
            blocker.transform.position = new Vector3(0, door / 2f, zS);
            blocker.AddComponent<BoxCollider>().size = new Vector3(2.6f, door, 0.2f);
            Slab(root, "DoorMat", new Vector3(0, 0.006f, -d / 2 + 0.75f), new Vector3(2.2f, 0.012f, 1.3f), TextureKit.Surface("DoorMat", TextureKit.Rubber(), Color.white, 0.5f), false, false);
            Backdrop(root, s.Backdrop, -d / 2 - 5f, Mathf.Max(24f, w * 1.6f));
            InteriorKit.ExitSign(root, new Vector3(0, door + 0.3f, -d / 2 + 0.02f), 0);

            // exit
            var exitGo = new GameObject("ExitDoor");
            exitGo.transform.SetParent(root, false);
            exitGo.transform.position = new Vector3(0, 1.2f, -d / 2 + 0.35f);
            var ecol = exitGo.AddComponent<BoxCollider>();
            ecol.isTrigger = true;
            ecol.size = new Vector3(2.4f, 2.4f, 0.5f);
            r.Exit = exitGo.AddComponent<ExitDoor>();
            r.Entrance = Group("EntrancePoint", root);
            r.Entrance.position = new Vector3(0, 0, -d / 2 + 0.6f);
            InteriorKit.Sanitizer(root, new Vector3(1.75f, 0, -d / 2 + 0.55f));
            InteriorKit.UmbrellaWrapper(root, new Vector3(-1.85f, 0, -d / 2 + 0.4f), 0, s.Accent);

            foreach (var x in new[] { -w / 3f, 0f, w / 3f })
            foreach (var z in new[] { -d / 4f, d / 4f })
                PointLight(root, new Vector3(x, h - 0.4f, z), 8f, 1.05f, s.Light);

            if (s.WindowX != null && s.WindowX.Length > 0) Counters(r, s);
            r.Player = CreatePlayer(root, new Vector3(0, 0.05f, -d / 2 + 1.4f), 0);
            return r;
        }

        /// <summary>
        /// The south side of an enclosed room (<see cref="RoomSpec.Enclosed"/>): a solid wall with a 2.6 m
        /// opening into a short passage. With a <see cref="RoomSpec.PassageGlow"/> the passage climbs as a
        /// flight of stairs towards that light (daylight at the street, for a station); otherwise it runs
        /// level and ends in a closed door. The exit trigger, the entrance point, the lights, the counters
        /// and the player are set up as in <see cref="Shell"/>.
        /// </summary>
        static void Passage(Room r, RoomSpec s, float door, Material frameMat)
        {
            var root = r.Root;
            float w = r.W, d = r.D, h = r.H, zS = -d / 2 - 0.1f, side = (w - 2.6f) / 2f;
            foreach (var sx in new[] { -1f, 1f })
                Slab(root, "WallS", new Vector3(sx * (1.3f + side / 2f), h / 2, zS), new Vector3(side, h, 0.2f), r.WallMat);
            Slab(root, "Lintel", new Vector3(0, door + (h - door) / 2, zS), new Vector3(2.6f, h - door, 0.2f), r.WallMat);
            foreach (var sx in new[] { -1f, 1f })
                Slab(root, "Jamb", new Vector3(sx * 1.32f, door / 2f, zS + 0.12f), new Vector3(0.06f, door, 0.08f), frameMat, false);

            // the passage: 5 m long, walls in the room's paint, a strip light down the middle
            float len = 5f, z0 = -d / 2 - 0.2f, z1 = z0 - len;
            bool stairs = s.PassageGlow.a > 0f;
            var tread = TextureKit.Surface("Passage_Floor", TextureKit.FloorStone(), C("#B9B4AA"));
            foreach (var sx in new[] { -1f, 1f })
                Slab(root, "PassageWall", new Vector3(sx * 1.4f, h / 2 + 0.8f, (z0 + z1) / 2f), new Vector3(0.2f, h + 1.6f, len), r.WallMat);
            if (stairs)
            {
                // a flight up towards the street: 16 cm risers, 30 cm treads, nosing strips in yellow
                int steps = 14;
                for (int i = 0; i < steps; i++)
                {
                    float z = z0 - 0.3f * i - 0.15f, top = 0.16f * (i + 1);
                    Slab(root, "Step", new Vector3(0, top / 2f, z), new Vector3(2.6f, top, 0.3f), tread, true, false);
                    Box(root, "Nosing", new Vector3(0, top + 0.004f, z + 0.12f), new Vector3(2.5f, 0.008f, 0.05f), C("#F2C230"), false);
                }
                Slab(root, "PassageCeiling", new Vector3(0, h + 1.2f, (z0 + z1) / 2f), new Vector3(2.8f, 0.1f, len), s.Ceiling, true, false);
                foreach (var sx in new[] { -1f, 1f })
                    Cylinder(root, "Handrail", new Vector3(sx * 1.22f, 1.0f + 1.1f, z0 - 2.1f), new Vector3(0.04f, 2.6f, 0.04f), C("#B9BEC4")).transform.localRotation = Quaternion.Euler(62f, 0, 0);
                // daylight at the top of the stairs
                var glow = Emissive(root, "Daylight", new Vector3(0, h + 0.4f, z1 + 0.05f), new Vector3(2.6f, 2.8f, 0.05f), s.PassageGlow, 2.4f);
                glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(root, new Vector3(0, h, z1 + 1.2f), 6f, 1.4f, s.PassageGlow);
            }
            else
            {
                Slab(root, "PassageFloor", new Vector3(0, -0.05f, (z0 + z1) / 2f), new Vector3(2.8f, 0.1f, len), tread);
                Slab(root, "PassageCeiling", new Vector3(0, h + 0.05f, (z0 + z1) / 2f), new Vector3(2.8f, 0.1f, len), s.Ceiling, true, false);
                Slab(root, "PassageEnd", new Vector3(0, h / 2, z1 - 0.1f), new Vector3(2.8f, h, 0.2f), r.WallMat);
                Slab(root, "PassageDoor", new Vector3(0, 1.05f, z1 + 0.02f), new Vector3(1.6f, 2.1f, 0.05f), frameMat, false);
                Emissive(root, "PassageLight", new Vector3(0, h - 0.02f, (z0 + z1) / 2f), new Vector3(0.2f, 0.03f, len - 1f), s.Light, 1.2f)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            if (!string.IsNullOrEmpty(s.PassageSign))
                Sign(root, "PassageSign", new Vector3(0, door + 0.28f, -d / 2 + 0.02f), new Vector2(1.9f, 0.34f), C("#22252B"), s.PassageSign, C("#FFFFFF"), 0, 46, 0.06f);

            // leaving: the passage is the way out
            var exitGo = new GameObject("ExitDoor");
            exitGo.transform.SetParent(root, false);
            exitGo.transform.position = new Vector3(0, 1.2f, -d / 2 - 0.4f);
            var ecol = exitGo.AddComponent<BoxCollider>();
            ecol.isTrigger = true;
            ecol.size = new Vector3(2.4f, 2.4f, 0.5f);
            r.Exit = exitGo.AddComponent<ExitDoor>();
            // the player may not walk up the stairs or down the passage: the way out is the exit prompt
            var blocker = new GameObject("PassageBlocker") { layer = 2 };
            blocker.transform.SetParent(root, false);
            blocker.transform.position = new Vector3(0, door / 2f, -d / 2 - 0.9f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(2.6f, door, 0.2f);
            r.Entrance = Group("EntrancePoint", root);
            r.Entrance.position = new Vector3(0, 0, -d / 2 + 0.6f);

            // the big enclosed halls (airport, station, ER) get a grid of lights for their size and height
            HallLights(root, Vector3.zero, w, d, h, s.Light);

            if (s.WindowX != null && s.WindowX.Length > 0) Counters(r, s);
            r.Player = CreatePlayer(root, new Vector3(0, 0.05f, -d / 2 + 1.4f), 0);
        }

        /// <summary>
        /// 600 mm mineral-fibre tiles (텍스) with rows of lights: square LED panels or long fluorescent
        /// troffers. Lights within 0.9 m of a <paramref name="keepClear"/> point are left out (air conditioners).
        /// </summary>
        static void GridCeiling(Room r, bool troffers, Color tint, params Vector2[] keepClear)
        {
            float w = r.W, d = r.D, h = r.H;
            bool Clear(float x, float z) => keepClear.All(p => (new Vector2(x, z) - p).magnitude > 0.9f);
            var lit = Mats.Emissive(tint, troffers ? 1.4f : 1.5f);
            if (troffers)
            {
                // 1200 × 300 troffers: centred on a grid line along x, in the middle of a tile row along z
                for (float z = -d / 2 + 1.5f; z < d / 2 - 0.9f; z += 1.8f)
                for (float x = -w / 2 + 1.8f; x < w / 2 - 1.2f; x += 2.4f)
                {
                    float gx = Mathf.Round(x / 0.6f) * 0.6f, gz = Snap(z);
                    if (Clear(gx, gz)) InteriorKit.LinearLight(r.Root, new Vector3(gx, 0, gz), h, 1.18f, 0, lit);
                }
                return;
            }
            for (float x = -w / 2 + 1.5f; x < w / 2 - 1f; x += 2.4f)
            for (float z = -d / 2 + 1.5f; z < d / 2 - 1f; z += 2.4f)
            {
                float gx = Snap(x), gz = Snap(z);
                if (!Clear(gx, gz)) continue;
                var p = Prims.Make(PrimitiveType.Cube, r.Root, "LedPanel", new Vector3(gx, h - 0.005f, gz), new Vector3(0.58f, 0.012f, 0.58f), lit);
                p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static float Snap(float v) => Mathf.Round((v - 0.3f) / 0.6f) * 0.6f + 0.3f;

        /// <summary>
        /// Flat gypsum-board ceiling with round downlights on a grid, except inside <paramref name="skip"/>
        /// (a cove or a slat field has its own light) and near <paramref name="keepClear"/> points.
        /// </summary>
        static void Downlights(Room r, float spacing, Color tint, Rect skip, params Vector2[] keepClear)
        {
            var lit = Mats.Emissive(tint, 1.7f);
            for (float x = -r.W / 2 + spacing / 2f; x < r.W / 2; x += spacing)
            for (float z = -r.D / 2 + spacing / 2f; z < r.D / 2; z += spacing)
            {
                var p = new Vector2(x, z);
                if (skip.Contains(p) || keepClear.Any(k => (p - k).magnitude < 0.8f)) continue;
                InteriorKit.Downlight(r.Root, new Vector3(x, 0, z), r.H, lit);
            }
        }

        /// <summary>
        /// A cove: a rectangle of ceiling framed by a dropped gypsum soffit, with an LED strip along the
        /// inside of the soffit that washes the recess with light.
        /// </summary>
        static void Cove(Room r, Rect c, Material gypsum, Color tint)
        {
            const float drop = 0.14f, lip = 0.32f;
            var g = Group("Cove", r.Root);
            var strip = Mats.Emissive(tint, 2.4f);
            float y = r.H - drop / 2f;
            var sides = new[]
            {
                (new Vector3(c.center.x, y, c.yMax + lip / 2f), new Vector3(c.width + 2 * lip, drop, lip), new Vector3(c.center.x, r.H - drop + 0.03f, c.yMax + 0.01f), new Vector3(c.width, 0.03f, 0.02f)),
                (new Vector3(c.center.x, y, c.yMin - lip / 2f), new Vector3(c.width + 2 * lip, drop, lip), new Vector3(c.center.x, r.H - drop + 0.03f, c.yMin - 0.01f), new Vector3(c.width, 0.03f, 0.02f)),
                (new Vector3(c.xMax + lip / 2f, y, c.center.y), new Vector3(lip, drop, c.height), new Vector3(c.xMax + 0.01f, r.H - drop + 0.03f, c.center.y), new Vector3(0.02f, 0.03f, c.height)),
                (new Vector3(c.xMin - lip / 2f, y, c.center.y), new Vector3(lip, drop, c.height), new Vector3(c.xMin - 0.01f, r.H - drop + 0.03f, c.center.y), new Vector3(0.02f, 0.03f, c.height)),
            };
            foreach (var (pos, size, lightPos, lightSize) in sides)
            {
                Slab(g, "Soffit", pos, size, gypsum, false, false);
                Slab(g, "CoveLight", lightPos, lightSize, strip, false, false);
            }
        }

        /// <summary>Wainscot panels with a rail and a skirting on the three solid walls (no panel: skirting only).</summary>
        static void Wainscot(Room r, Material panel, Material rail, float height, Color skirting)
        {
            float w = r.W, d = r.D;
            var skirt = Mats.Lit(skirting, 0.3f);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0, 0, d / 2 - 0.015f), new Vector3(w, 0, 0.03f)),
                (new Vector3(w / 2 - 0.015f, 0, 0), new Vector3(0.03f, 0, d)),
                (new Vector3(-w / 2 + 0.015f, 0, 0), new Vector3(0.03f, 0, d)),
            })
            {
                var grow = new Vector3(size.x > 0.1f ? 0 : 0.02f, 0, size.z > 0.1f ? 0 : 0.02f);
                if (panel)
                {
                    Slab(r.Root, "Wainscot", pos + Vector3.up * height / 2f, size + Vector3.up * height, panel, false);
                    if (rail) Slab(r.Root, "Rail", pos + Vector3.up * (height + 0.02f), size + Vector3.up * 0.05f + grow, rail, false);
                }
                Slab(r.Root, "Skirting", pos + Vector3.up * 0.05f, size + Vector3.up * 0.1f + grow, skirt, false);
            }
        }

        /// <summary>The service counter along the north wall, its windows, staff seats and customer spots.</summary>
        static void Counters(Room r, RoomSpec s)
        {
            var root = r.Root;
            float d = r.D, x0 = s.CounterX0, x1 = s.CounterX1, len = x1 - x0, cx = (x0 + x1) / 2f;
            bool low = s.Style == CounterStyle.Low;
            float F = r.CounterFrontZ = d / 2 - (low ? 2.5f : 2.35f);
            r.AisleZ = F - (low ? 1.55f : 1.3f);
            var dark = Mats.Lit(C("#3A3D42"), 0.35f);
            var accentMat = Mats.Lit(s.Accent, 0.35f);
            float deskFront = low ? F : F + 0.36f, deskBack = r.DeskBack = low ? F + 0.72f : F + 1.0f;
            float staffZ = deskBack + (low ? 0.31f : 0.3f);

            // the work desk the staff sit at (open underneath on their side)
            Slab(root, "DeskTop", new Vector3(cx, InteriorKit.DeskTop - 0.02f, (deskFront + deskBack) / 2f), new Vector3(len, 0.04f, deskBack - deskFront), r.TopMat);
            if (low)
            {
                // desk-height counter: the customer sits facing the front panel with an accent band
                Slab(root, "CounterFront", new Vector3(cx, 0.36f, F + 0.03f), new Vector3(len, 0.72f, 0.06f), s.CounterFront);
                Slab(root, "CounterBand", new Vector3(cx, 0.6f, F - 0.003f), new Vector3(len, 0.06f, 0.01f), accentMat, false);
                Slab(root, "CounterKick", new Vector3(cx, 0.05f, F + 0.005f), new Vector3(len, 0.1f, 0.02f), dark, false);
            }
            else
            {
                // reception: a raised ledge in front of the nurses' desk, floating on a lit recess
                Slab(root, "LedgeFront", new Vector3(cx, 0.565f, F + 0.03f), new Vector3(len, 0.97f, 0.06f), s.CounterFront);
                Slab(root, "LedgeTop", new Vector3(cx, 1.07f, F + 0.17f), new Vector3(len + 0.1f, 0.04f, 0.4f), r.TopMat);
                Slab(root, "LedgeBack", new Vector3(cx, 0.9f, F + 0.345f), new Vector3(len, 0.32f, 0.03f), s.Cabinet, false);
                Slab(root, "CounterBand", new Vector3(cx, 0.92f, F - 0.003f), new Vector3(len, 0.06f, 0.01f), accentMat, false);
                Slab(root, "CounterKick", new Vector3(cx, 0.04f, F + 0.08f), new Vector3(len, 0.08f, 0.02f), dark, false);
                Slab(root, "UnderGlow", new Vector3(cx, 0.075f, F + 0.06f), new Vector3(len - 0.1f, 0.012f, 0.02f), Mats.Emissive(C("#FFF2DC"), 2.2f), false, false);
            }
            foreach (var ex in new[] { x0 + 0.03f, x1 - 0.03f })
                Slab(root, "CounterEnd", new Vector3(ex, (low ? 0.72f : 1.05f) / 2f, (F + deskBack) / 2f), new Vector3(0.06f, low ? 0.72f : 1.05f, deskBack - F), s.Cabinet);

            // storage along the wall behind the staff
            Slab(root, "BackCabinet", new Vector3(cx, 0.45f, d / 2 - 0.25f), new Vector3(len, 0.9f, 0.45f), s.Cabinet);
            Slab(root, "BackCabinetTop", new Vector3(cx, 0.915f, d / 2 - 0.25f), new Vector3(len + 0.02f, 0.03f, 0.47f), r.TopMat, false);
            for (float bx = x0 + 0.6f; bx < x1 - 0.5f; bx += 1.6f)
                Box(root, "Binder", new Vector3(bx, 1.05f, d / 2 - 0.3f), new Vector3(0.9f, 0.24f, 0.26f), Color.Lerp(s.Accent, Color.white, 0.5f), false);
            Box(root, "WaitLine", new Vector3(cx, 0.004f, r.AisleZ + 0.4f), new Vector3(len - 0.6f, 0.008f, 0.08f), C("#F2C230"), false);

            int n = s.WindowX.Length;
            for (int i = 0; i < n; i++)
            {
                float x = s.WindowX[i];
                int k = i + 1;
                GameObject customerChair = null;

                if (low)
                {
                    if (s.Screens)
                    {
                        // tall acrylic screen with a gap underneath for documents
                        Glass(root, $"Screen_{k}", new Vector3(x, 1.2f, F + 0.32f), new Vector3(1.3f, 0.76f, 0.012f), 0, false);
                        Box(root, $"ScreenFoot_{k}", new Vector3(x - 0.6f, 0.8f, F + 0.32f), new Vector3(0.04f, 0.12f, 0.1f), C("#9AA0A6"), false);
                        Box(root, $"ScreenFoot_{k}", new Vector3(x + 0.6f, 0.8f, F + 0.32f), new Vector3(0.04f, 0.12f, 0.1f), C("#9AA0A6"), false);
                    }
                    // the customer's chair (the director switches its colliders off when the player sits here)
                    customerChair = InteriorKit.WaitingChair(root, new Vector3(x, 0, F - 0.52f), 0, Mats.Lit(Color.Lerp(s.Accent, C("#2B2E33"), 0.35f), 0.35f)).gameObject;
                    var spot = Group($"CounterSpot_{k}", root);
                    spot.SetPositionAndRotation(new Vector3(x, 0, F - 0.58f), Quaternion.identity);
                    r.CounterSpots.Add(spot);
                    var plate = Group($"NamePlate_{k}", root);
                    plate.position = new Vector3(x - 0.38f, InteriorKit.DeskTop, F + 0.14f);
                    var plateBox = Box(plate, "Plate", new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.1f, 0.03f), C("#2B2D31"), false, 0);
                    plateBox.transform.localRotation = Quaternion.Euler(-12, 0, 0);
                    if (s.NamePlates != null && i < s.NamePlates.Length)
                        WorldText.Create(plate, "Name", s.NamePlates[i], new Vector3(0, 0.05f, -0.018f), new Vector2(0.27f, 0.07f), 30, Color.white, true, TextAnchor.MiddleCenter,
                            Quaternion.Euler(-12, 0, 0));
                }
                else
                {
                    // glass on the ledge with a speaking gap in the middle of each window
                    Glass(root, $"FrontL_{k}", new Vector3(x - 0.55f, 1.45f, F + 0.08f), new Vector3(0.4f, 0.72f, 0.02f));
                    Glass(root, $"FrontR_{k}", new Vector3(x + 0.55f, 1.45f, F + 0.08f), new Vector3(0.4f, 0.72f, 0.02f));
                    Box(root, $"GlassRail_{k}", new Vector3(x, 1.82f, F + 0.08f), new Vector3(1.54f, 0.03f, 0.05f), C("#9AA0A6"), false);
                    Box(root, $"Tray_{k}", new Vector3(x + 0.15f, 1.1f, F + 0.2f), new Vector3(0.36f, 0.03f, 0.26f), C("#2B2D31"), false);
                    Box(root, $"Paper_{k}", new Vector3(x + 0.15f, 1.12f, F + 0.2f), new Vector3(0.21f, 0.01f, 0.3f), Color.white, false, 6);
                    InteriorKit.CardTerminal(root, new Vector3(x - 0.45f, 1.09f, F + 0.22f), 180);
                    var spot = Group($"CounterSpot_{k}", root);
                    spot.SetPositionAndRotation(new Vector3(x, 0, F - 0.45f), Quaternion.identity);
                    r.CounterSpots.Add(spot);
                }
                Cylinder(root, $"PenCup_{k}", new Vector3(x + 0.45f, low ? 0.79f : 1.13f, F + 0.22f), new Vector3(0.07f, 0.06f, 0.07f), C("#3B3F46"));

                // the staff side: swivel chair, monitor and keyboard facing them
                InteriorKit.OfficeChair(root, new Vector3(x, 0, staffZ), 180, C("#2B2E33"));
                var seat = Group($"StaffSeat_{k}", root);
                seat.SetPositionAndRotation(new Vector3(x, 0, staffZ + 0.07f), Quaternion.Euler(0, 180, 0));
                r.StaffSeats.Add(seat);
                InteriorKit.Workstation(root, seat.position, deskBack, i % 2 == 0 ? 1f : -1f, C("#9CC7FF"));
                if (i < n - 1)
                {
                    float mx = (x + s.WindowX[i + 1]) / 2f;
                    if (s.Partition)
                    {
                        // a privacy partition between windows: solid below, frosted above, its end towards the customer
                        Slab(root, $"Partition_{k}", new Vector3(mx, 0.62f, (F - 0.1f + deskBack) / 2f), new Vector3(0.05f, 1.24f, deskBack - F + 0.1f), s.Partition);
                        Prims.Make(PrimitiveType.Cube, root, $"PartitionGlass_{k}", new Vector3(mx, 1.44f, (F - 0.1f + deskBack) / 2f), new Vector3(0.03f, 0.4f, deskBack - F + 0.1f),
                            Mats.Glass(new Color(0.92f, 0.95f, 0.97f, 0.55f)), false);
                    }
                    else
                        Glass(root, $"Divider_{k}", new Vector3(mx, (low ? 0.74f : 1.09f) + 0.3f, (F + deskBack) / 2f), new Vector3(0.03f, 0.6f, deskBack - F), 0, false);
                }

                // every window can be the player's: its trigger, and where the player sits or stands to talk
                var counterGo = new GameObject($"Counter_{k}");
                counterGo.transform.SetParent(root, false);
                counterGo.transform.position = new Vector3(x, 1.0f, F - 0.2f);
                var box = counterGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.4f, 1.4f, 0.7f);
                var counter = counterGo.AddComponent<CounterSpot>();
                counter.counterNumber = k;
                counter.chair = customerChair;
                r.Counters.Add(counter);
                var mySeat = Group($"Seat_counter_{k}", root);
                mySeat.SetPositionAndRotation(new Vector3(x, 0.05f, low ? F - 0.58f : F - 0.5f), Quaternion.identity);
                if (low) mySeat.gameObject.AddComponent<PlayerSeat>();
                r.Anchors.Add(mySeat);
            }
        }

        /// <summary>
        /// A row of waiting seats facing the counters (+Z) at depth z: blocks starting at x with n places
        /// each. The walking lanes between blocks are the room's <see cref="Room.SeatGaps"/>.
        /// </summary>
        static void SeatRow(Room r, float z, SeatKind kind, Material shell, Material arms, params (float x, int n)[] blocks)
        {
            const float pitch = 0.62f;
            var row = Group($"SeatRow_{r.Seats.Count}", r.Root);
            foreach (var (bx, n) in blocks)
            {
                float last = bx + pitch * (n - 1), cx = (bx + last) / 2f;
                // one collider per block keeps people out of the seats; the seat parts themselves are merged below
                var block = new GameObject("Block");
                block.transform.SetParent(row, false);
                var col = block.AddComponent<BoxCollider>();
                col.center = new Vector3(cx, 0.46f, z - 0.02f);
                col.size = new Vector3(last - bx + (kind == SeatKind.Sofa ? 0.8f : 0.5f), 0.92f, kind == SeatKind.Sofa ? 0.72f : 0.56f);
                if (kind == SeatKind.Sofa)
                {
                    r.Seats.AddRange(InteriorKit.Sofa(row, new Vector3(cx, 0, z), 0, n, shell, arms, pitch));
                    continue;
                }
                // gang seating: one steel beam under the block, a T-leg at each end
                Prims.Make(PrimitiveType.Cube, row, "SeatBeam", new Vector3(cx, 0.34f, z - 0.02f), new Vector3(last - bx + 0.4f, 0.05f, 0.08f), r.SteelMat);
                foreach (var lx in new[] { bx - 0.1f, last + 0.1f })
                {
                    Prims.Make(PrimitiveType.Cube, row, "SeatLeg", new Vector3(lx, 0.17f, z - 0.02f), new Vector3(0.06f, 0.34f, 0.06f), r.SteelMat);
                    Prims.Make(PrimitiveType.Cube, row, "SeatFoot", new Vector3(lx, 0.015f, z - 0.02f), new Vector3(0.08f, 0.03f, 0.5f), r.SteelMat);
                }
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector3(bx + i * pitch, 0, z);
                    var chair = kind == SeatKind.Steel ? InteriorKit.BeamSeat(row, p, shell, i < n - 1) : InteriorKit.WaitingChair(row, p, 0, shell, false);
                    // people sit a little back on the seat, against the backrest
                    var anchor = Group("SeatAnchor", chair);
                    anchor.localPosition = new Vector3(0, 0, -0.06f);
                    r.Seats.Add(anchor);
                }
            }
            MergeByMaterial(row, $"{r.Root.name.Trim('[', ']')}_{row.name}");
        }

        /// <summary>
        /// Merges every mesh under a group into one static mesh per material and drops the part objects
        /// (and their colliders); empty transforms such as seat anchors stay where they are.
        /// </summary>
        static void MergeByMaterial(Transform group, string assetName)
        {
            var filters = group.GetComponentsInChildren<MeshFilter>();
            int k = 0;
            const string folder = "Assets/_Project/Art/Generated/Meshes";
            EnsureFolder(folder);
            foreach (var set in filters.GroupBy(f => f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var parts = set.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = group.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray();
                var mesh = new Mesh { name = $"{assetName}_{k++}", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts, true, true);
                var path = $"{folder}/{mesh.name}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject($"Merged_{set.Key.name}");
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = set.Key;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var f in filters) Object.DestroyImmediate(f.gameObject);
        }

        /// <summary>
        /// The 번호표 kiosk, the director's ticket machine; visitors stop at the spot in front of it.
        /// </summary>
        static Transform Ticket(Room r, Vector3 pos, float yaw, string header, string screen)
        {
            var slot = InteriorKit.TicketKiosk(r.Root, pos, yaw, r.Accent, header, screen, out var machine);
            r.Machine = machine.gameObject.AddComponent<TicketMachine>();
            r.Machine.slot = slot;
            var trigger = machine.gameObject.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0, 0.9f, 0.25f);
            trigger.size = new Vector3(0.8f, 1.8f, 0.9f);
            trigger.isTrigger = true;
            var solid = new GameObject("Solid");
            solid.transform.SetParent(machine, false);
            var sc = solid.AddComponent<BoxCollider>();
            sc.center = new Vector3(0, 0.85f, 0);
            sc.size = new Vector3(0.56f, 1.7f, 0.44f);
            r.MachineSpot = Group("MachineSpot", r.Root);
            r.MachineSpot.position = machine.position + machine.forward * 0.85f;
            return machine;
        }

        /// <summary>
        /// The number board (front +Z of its yaw): "0047 → 2번 창구" in red and amber, and with
        /// <paramref name="rows"/> the last number of every counter beside it. Hung from the ceiling or on a wall.
        /// </summary>
        static QueueBoard MainBoard(Room r, Vector3 pos, float yaw, float width, int rows, bool hanging)
        {
            var board = Group("NumberBoard", r.Root);
            board.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            int perCol = Mathf.CeilToInt(rows / 2f);
            float height = rows > 0 ? Mathf.Max(0.62f, 0.26f + 0.18f * perCol) : width * 0.3f;
            Box(board, "Frame", Vector3.zero, new Vector3(width, height, 0.1f), C("#111216"), false);
            if (hanging)
            {
                float rod = r.H - pos.y - height / 2f;
                foreach (var x in new[] { -width * 0.38f, width * 0.38f })
                    Box(board, "Rod", new Vector3(x, height / 2f + rod / 2f, 0), new Vector3(0.015f, rod, 0.015f), C("#9AA0A6"), false);
            }
            var flip = Quaternion.Euler(0, 180, 0);
            var amber = C("#FFB547");
            var q = board.gameObject.AddComponent<QueueBoard>();
            // the viewer's left is local +X
            float leftX = rows > 0 ? width / 4f : width * 0.17f;
            float leftW = rows > 0 ? width / 2f - 0.1f : width * 0.6f;
            if (rows > 0)
                WorldText.Create(board, "Caption", "호출 번호 · NOW SERVING", new Vector3(leftX, height / 2f - 0.08f, 0.055f), new Vector2(leftW, 0.1f), 30, C("#9AA0A6"), true,
                    TextAnchor.MiddleCenter, flip);
            q.numberText = WorldText.Create(board, "Number", "0000", new Vector3(leftX, rows > 0 ? 0.0f : 0, 0.055f), new Vector2(leftW, height * (rows > 0 ? 0.5f : 0.8f)), 90,
                C("#FF3B30"), true, TextAnchor.MiddleCenter, flip);
            float counterX = rows > 0 ? leftX : -width * 0.3f;
            float counterY = rows > 0 ? -height / 2f + 0.1f : 0;
            q.counterText = WorldText.Create(board, "Counter", "→ 1번 창구", new Vector3(counterX, counterY, 0.055f), new Vector2(rows > 0 ? leftW : width * 0.38f, rows > 0 ? 0.14f : height * 0.7f),
                56, amber, true, TextAnchor.MiddleCenter, flip);
            if (rows > 0)
            {
                Box(board, "Divider", new Vector3(0, 0, 0.051f), new Vector3(0.01f, height - 0.08f, 0.002f), C("#2B2D31"), false);
                q.rows = new UnityEngine.UI.Text[rows];
                for (int i = 0; i < rows; i++)
                {
                    // two columns in the right half of the board (local -X)
                    int col = i / perCol, row = i % perCol;
                    float cxl = -(col == 0 ? width * 0.125f : width * 0.375f);
                    float y = (perCol - 1) * 0.09f - row * 0.18f;
                    WorldText.Create(board, "Window", $"{i + 1}", new Vector3(cxl + width * 0.07f, y, 0.055f), new Vector2(0.14f, 0.15f), 50, amber, true, TextAnchor.MiddleCenter, flip);
                    q.rows[i] = WorldText.Create(board, "Row", "0000", new Vector3(cxl - width * 0.03f, y, 0.055f), new Vector2(width * 0.15f, 0.15f), 50, C("#FF3B30"), true,
                        TextAnchor.MiddleCenter, flip);
                }
            }
            r.Boards.Add(q);
            return q;
        }

        /// <summary>A sign hung from the ceiling on two rods (front +Z of its yaw).</summary>
        static Transform HangingSign(Room r, string name, Vector3 pos, float yaw, Vector2 size, Color board, string text, Color textColor, int font)
        {
            var s = Sign(r.Root, name, pos, size, board, text, textColor, yaw, font, 0.04f);
            float rod = r.H - pos.y - size.y / 2f;
            foreach (var x in new[] { -size.x * 0.38f, size.x * 0.38f })
                Box(s, "Rod", new Vector3(x, size.y / 2f + rod / 2f, 0), new Vector3(0.015f, rod, 0.015f), C("#9AA0A6"), false);
            return s;
        }

        /// <summary>A visitor who stays put (at the ATM, on the wall bench, at the form table) so the room never looks staged around the player.</summary>
        static HumanRig Extra(Room r, string name, HumanLook look, Vector3 pos, float yaw, bool sitting = false, bool phone = false, bool talking = false,
            Transform lookAt = null, float seat = InteriorKit.ChairSeat, Transform parent = null)
        {
            var rig = HumanBuilder.Build(parent ? parent : r.Root, name, look);
            rig.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            rig.sitting = sitting;
            rig.phone = phone;
            rig.talking = talking;
            rig.seatHeight = seat;
            rig.lookTarget = lookAt;
            rig.Pose(0f);
            return rig;
        }

        /// <summary>A staff member; seated ones sit on their swivel chair and type between lines.</summary>
        static NpcActor Staff(Room r, string staffId, Transform seat, HumanLook look, bool seated = true)
        {
            var rig = HumanBuilder.Build(r.Root, $"Staff_{staffId}", look);
            rig.transform.SetPositionAndRotation(seat.position, seat.rotation);
            rig.sitting = seated;
            rig.typing = seated;
            rig.seatHeight = InteriorKit.OfficeSeat;
            rig.Pose(0f);
            var actor = rig.gameObject.AddComponent<NpcActor>();
            actor.staffId = staffId;
            actor.rig = rig;
            actor.face = rig.head;
            // the thinking bubble floats over the head, wherever the head is (sitting or standing)
            var bubbleRoot = Group("Bubble", rig.head);
            bubbleRoot.localPosition = new Vector3(0, 0.62f, 0);
            bubbleRoot.gameObject.AddComponent<Billboard>();
            var bubble = WorldText.Create(bubbleRoot, "Dots", "• • •", Vector3.zero, new Vector2(0.8f, 0.35f), 60, Color.white, true, TextAnchor.MiddleCenter, Quaternion.identity);
            bubble.gameObject.AddComponent<UnityEngine.UI.Outline>().effectDistance = new Vector2(2, -2);
            bubbleRoot.gameObject.SetActive(false);
            actor.bubble = bubbleRoot;
            r.Actors.Add(actor);
            return actor;
        }

        static NpcActor CounterStaff(Room r, int window, string staffId, HumanLook look) => Staff(r, staffId, r.StaffSeats[window - 1], look);

/// <summary>
        /// Ceilings give back some of the room's light (the Surface shader's self light, a share of their own
        /// colour), as a white ceiling over a bright floor does: without it a ceiling is lit only in pools right
        /// around its lamps, which reads as a dark lid over a high hall.
        /// </summary>
        static void GlowCeilings(Transform root, float amount = 0.22f)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Ceiling")) continue;
                foreach (var m in r.sharedMaterials)
                    if (m && m.shader && m.shader.name == "SeoulFirstWeek/Surface")
                    {
                        m.SetColor("_EmissionColor", new Color(amount, amount, amount, 0));
                        EditorUtility.SetDirty(m);
                    }
            }
        }

        static ScenarioDirector Director(Room r, string[] procedures, int crowdSeed, int seated, params FetchStation[] stations)
        {
            // every interior ends here: its ceilings are built by now
            GlowCeilings(r.Root);
            // a place with no waiting seats (an immigration hall's queue lanes, a station) has no visitor crowd
            QueueCrowd crowd = null;
            if (r.Seats.Count > 0)
            {
            var crowdGo = new GameObject("Crowd");
            crowdGo.transform.SetParent(r.Root, false);
            crowd = crowdGo.AddComponent<QueueCrowd>();
            crowd.seats = r.Seats.ToArray();
            crowd.counterSpots = r.CounterSpots.ToArray();
            crowd.sitAtCounter = r.Style == CounterStyle.Low;
            crowd.seatHeight = InteriorKit.ChairSeat;
            crowd.entrance = r.Entrance;
            crowd.machine = r.MachineSpot;
            crowd.aisleZ = r.AisleZ;
            crowd.gaps = r.SeatGaps.Count > 0 ? r.SeatGaps.ToArray() : new[] { r.Seats.Max(t => t.position.x) + 0.8f };
            crowd.initialSeated = Mathf.Min(seated, r.Seats.Count - 3);
            crowd.seed = crowdSeed;
            // who waits here: an immigration hall and an arrivals hall are mostly foreigners; a station or a police
            // box gets the street's mix
            var p0 = procedures[0];
            crowd.crowd = p0.StartsWith("immigration") || p0.StartsWith("airport") ? Crowd.Immigration
                : p0.StartsWith("bank") ? Crowd.Bank
                : p0.StartsWith("clinic") ? Crowd.Clinic
                : Crowd.Street;
            }

            var go = new GameObject("ScenarioDirector");
            go.transform.SetParent(r.Root, false);
            var d = go.AddComponent<ScenarioDirector>();
            d.procedures = procedures.Select(id => AssetDatabase.LoadAssetAtPath<TextAsset>($"{ProcedureFolder}/{id}.json")).ToArray();
            d.player = r.Player;
            d.ticketMachine = r.Machine;
            d.boards = r.Boards.ToArray();
            d.counters = r.Counters.ToArray();
            d.crowd = crowd;
            d.actors = r.Actors.ToArray();
            d.anchors = r.Anchors.ToArray();
            d.stations = stations;
            d.waypoints = r.Root.GetComponentsInChildren<Waypoint>(true);
            if (r.Exit) r.Exit.director = d;
            foreach (var id in procedures)
                if (!d.procedures.Any(t => t && t.name == id)) Debug.LogError($"[Builder] Missing procedure file {ProcedureFolder}/{id}.json");
            return d;
        }

        static void Poster(Transform root, Vector3 pos, float yaw, Vector2 size, string text, string bg = "#FFFFFF", string fg = "#222222", int font = 34)
        {
            var p = Sign(root, "Poster", pos, size, C(bg), text, C(fg), yaw, font, 0.02f);
            // a thin frame around the poster board
            Box(p, "Frame", new Vector3(0, 0, -0.012f), new Vector3(size.x + 0.05f, size.y + 0.05f, 0.012f), C("#9AA0A6"), false);
        }

        // ------------------------------------------------------------------- clinic

        /// <summary>
        /// 튼튼내과의원: a small neighbourhood clinic, run as patients really use one: no ticket machine and
        /// no number board; you register at the reception, wait for your name on the masked-name monitor
        /// and the call, and walk into 1진료실 yourself. Ivory walls with light-oak panels and a mint rail,
        /// a beige porcelain floor, a gypsum ceiling with a lit cove over the sofas, the reception counter
        /// floating on its glow strip in front of the oak logo wall, three treatment-room doors, and
        /// 1진료실 in the corner.
        /// </summary>
        [MenuItem("Seoul First Week/Build Clinic")]
        public static void BuildClinic()
        {
            var scene = NewScene("Clinic");
            BeginMaterials();
            float w = 14, d = 10, h = 2.7f;
            var mint = C("#1FA37A");
            var oak = TextureKit.Surface("Wood_Clinic", TextureKit.WoodLight(), C("#F4E7D4"));
            var gypsum = TextureKit.Surface("Gypsum", TextureKit.WallPaint(), Color.white, 0.5f);
            var r = Shell(new RoomSpec
            {
                Name = "Clinic", Backdrop = "clinic", W = w, D = d, H = h, Style = CounterStyle.High,
                CounterX0 = -4.9f, CounterX1 = 0.7f, WindowX = new[] { -3.4f, -0.8f },
                Wall = C("#F7F4EE"), Accent = mint, Light = C("#FFEBD2"),
                Floor = TextureKit.Surface("Floor_Clinic", TextureKit.FloorVinyl(), C("#EBE3D5")),
                Ceiling = gypsum,
                CounterFront = TextureKit.Surface("Counter_Clinic", TextureKit.FloorStone(), C("#F6F4F0"), 0.8f),
                Cabinet = oak,
            });
            var root = r.Root;
            var rng = new System.Random(11);
            float F = r.CounterFrontZ;

            // ceiling: a lit cove over the sofas, downlights around it, two cassette air conditioners
            var cove = new Rect(-5.7f, -2.3f, 8.0f, 3.0f);
            Cove(r, cove, gypsum, C("#FFE7C4"));
            var ac = new[] { new Vector2(4.6f, 1.6f), new Vector2(-4.4f, -3.6f) };
            Downlights(r, 1.6f, C("#FFF1DE"), new Rect(cove.xMin - 0.4f, cove.yMin - 0.4f, cove.width + 0.8f, cove.height + 0.8f), ac);
            foreach (var a in ac) InteriorKit.CeilingCassette(root, new Vector3(a.x, 0, a.y), h);
            InteriorKit.Cctv(root, new Vector3(-w / 2 + 0.5f, h, -d / 2 + 0.5f));
            Wainscot(r, oak, Mats.Lit(mint, 0.35f), 0.95f, C("#C9B79C"));

            // the oak logo wall behind reception, backlit, with the designation plaques beside it
            Slab(root, "LogoWallPanel", new Vector3(-2.1f, (0.93f + h) / 2f, d / 2 - 0.02f), new Vector3(5.6f, h - 0.93f, 0.02f), oak, false, false);
            var slats = Group("LogoWallSlats", root);
            for (float x = -4.8f; x < 0.66f; x += 0.2f)
                Slab(slats, "Slat", new Vector3(x, (0.95f + h) / 2f, d / 2 - 0.05f), new Vector3(0.07f, h - 0.95f, 0.03f), oak, false, false);
            Merge(slats, "Clinic_LogoSlats");
            var logo = Group("Logo", root);
            logo.SetPositionAndRotation(new Vector3(-2.1f, 1.95f, d / 2 - 0.07f), Quaternion.Euler(0, 180, 0));
            // a backlit acrylic sign: the plate itself glows softly
            Emissive(logo, "Plate", Vector3.zero, new Vector3(2.4f, 0.58f, 0.02f), C("#F2FFF8"), 0.6f).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box(logo, "CrossV", new Vector3(0.95f, 0.02f, 0.012f), new Vector3(0.09f, 0.3f, 0.01f), mint, false);
            Box(logo, "CrossH", new Vector3(0.95f, 0.02f, 0.012f), new Vector3(0.3f, 0.09f, 0.01f), mint, false);
            WorldText.Create(logo, "Name", "튼튼내과의원", new Vector3(-0.2f, 0.07f, 0.014f), new Vector2(1.7f, 0.34f), 80, C("#16614A"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(logo, "En", "TEUNTEUN INTERNAL MEDICINE", new Vector3(-0.2f, -0.18f, 0.014f), new Vector2(1.7f, 0.12f), 34, C("#6B7A76"), true, TextAnchor.MiddleCenter,
                Quaternion.Euler(0, 180, 0));
            InteriorKit.Certificate(root, new Vector3(-4.3f, 1.6f, d / 2 - 0.07f), 180, "국가건강검진 지정기관", rng);
            InteriorKit.Certificate(root, new Vector3(-3.85f, 1.6f, d / 2 - 0.07f), 180, "예방접종 위탁의료기관", rng);
            InteriorKit.Certificate(root, new Vector3(0.1f, 1.6f, d / 2 - 0.07f), 180, "의료기관 개설신고증", rng);

            // reception: two nurses, counter signs, the ID notice, orchids from the opening, leaflets
            CounterStaff(r, 1, "nurse", HumanLook.Nurse(0));
            CounterStaff(r, 2, "nurse2", HumanLook.Nurse(1));
            InteriorKit.DeskSign(root, new Vector3(-3.4f - 0.55f, 1.09f, F + 0.0f), 0, "1 접수·수납", mint, 0.4f);
            InteriorKit.DeskSign(root, new Vector3(-0.8f - 0.55f, 1.09f, F + 0.0f), 0, "2 서류·예약", mint, 0.4f);
            InteriorKit.DeskSign(root, new Vector3(-2.1f, 1.09f, F + 0.02f), 0, "진료 시 본인 확인 필수", C("#C8402E"), 0.62f);
            InteriorKit.Orchid(root, new Vector3(-4.65f, 1.09f, F + 0.2f), 0, rng);
            InteriorKit.Orchid(root, new Vector3(0.45f, 1.09f, F + 0.2f), 40, rng);
            // no number board: a small clinic calls patients by name. The waiting monitor beside the
            // counter lists them masked (김*수); the director adds the player once they have registered
            var monitor = InteriorKit.WallTv(root, new Vector3(1.3f, 1.8f, d / 2 - 0.05f), 180, C("#16324A"),
                // six lines, so the text canvas is fine enough for a queue that wraps (see WorldText)
                "진료 대기 현황\n1진료실 · 박준호 원장\n진료중  김*수\n대기  -\n\n", width: 1.2f);
            var waiting = monitor.gameObject.AddComponent<WaitingBoard>();
            waiting.text = monitor.Find("Caption").GetComponentInChildren<UnityEngine.UI.Text>();
            waiting.otherPages = new[]
            {
                "진료 대기 현황\n2진료실 · 정혜원 원장\n진료중  한*희\n대기  오*진 · 윤*서",
                "진료 대기 현황\n주사실 · 수액실\n수액 중  3명\n잠시만 기다려 주세요",
            };
            Plant(root, new Vector3(1.3f, 0, d / 2 - 0.4f), 0.85f);

            // treatment-room doors off the waiting room
            var door = TextureKit.Surface("Door_Clinic", TextureKit.Plastic(), C("#E6D6BE"), 0.5f);
            InteriorKit.RoomDoor(root, new Vector3(3.0f, 0, d / 2 - 0.03f), 180, "2진료실", "정혜원 원장", door, mint, "진료중");
            InteriorKit.RoomDoor(root, new Vector3(4.6f, 0, d / 2 - 0.03f), 180, "주사실", "수액실", door, mint);
            InteriorKit.RoomDoor(root, new Vector3(6.2f, 0, d / 2 - 0.03f), 180, "초음파실", "내시경실", door, mint, "검사중", C("#E58B2B"));

            // waiting area: sofa islands facing the reception and the TV, a central lane from the door
            var sofa = TextureKit.Surface("Sofa_Clinic", TextureKit.FabricWeave(), C("#DDD6CA"), 0.3f);
            float row1 = r.AisleZ - 1.3f, row2 = row1 - 1.5f;
            SeatRow(r, row1, SeatKind.Sofa, sofa, oak, (-5.3f, 3), (-2.54f, 3), (1.7f, 2));
            SeatRow(r, row2, SeatKind.Sofa, sofa, oak, (-5.3f, 3), (-2.54f, 3), (1.7f, 2));
            r.SeatGaps.AddRange(new[] { -3.3f, 0.2f });

            // the west wall: a built-in bench under the TV, the scale, the BP machine and the water purifier
            var leather = TextureKit.Surface("Leather_Clinic", TextureKit.Plastic(), C("#5E4334"), 0.45f);
            var white = TextureKit.Surface("Bench_White", TextureKit.Plastic(), C("#F4F2EC"), 0.6f);
            var bench = InteriorKit.Sofa(root, new Vector3(-w / 2 + 0.38f, 0, -2.9f), 90, 3, leather, white, 0.62f, true);
            Extra(r, "Patient_IV", HumanLook.SeniorPerson(new System.Random(71), false), bench[0].position, 90, sitting: true);
            InteriorKit.IvPole(root, new Vector3(-w / 2 + 0.45f, 0, -1.62f));
            Extra(r, "Patient_Phone", HumanLook.Ajumma(new System.Random(72)), bench[2].position, 90, sitting: true, phone: true);
            InteriorKit.WallTv(root, new Vector3(-w / 2 + 0.05f, 1.85f, -2.9f), 90, C("#2B5E8C"), "건강 토크쇼", new[]
            {
                "건강 토크쇼\n<size=28>환절기 감기, 이렇게 이겨요</size>",
                "독감 예방접종\n<size=28>10월 집중 기간 · 접수처 문의</size>",
                "기침할 땐\n<size=28>옷소매로 가려 주세요</size>",
                "뉴스\n<size=28>내일 아침 기온 뚝, 일교차 커요</size>",
            }, new[] { C("#2B5E8C"), C("#1B7A5E"), C("#7A4B2A"), C("#303A4A") }, 1.3f, 3f);
            InteriorKit.HeightScale(root, new Vector3(-w / 2 + 0.3f, 0, -0.7f), 90, mint);
            InteriorKit.BloodPressure(root, new Vector3(-w / 2 + 0.33f, 0, 3.3f), 90);
            Poster(root, new Vector3(-w / 2 + 0.03f, 1.95f, 3.3f), 90, new Vector2(0.6f, 0.36f), "혈압 측정 안내\n<size=22>팔을 넣고 버튼을 누르세요</size>", "#FFFFFF", "#16614A", 30);
            InteriorKit.WaterPurifier(root, new Vector3(-w / 2 + 0.3f, 0, 4.4f), 90);
            Poster(root, new Vector3(-5.9f, 1.6f, d / 2 - 0.035f), 180, new Vector2(1.0f, 0.62f), "진료 시 본인 확인 필수\n<size=22>신분증 · 여권 · 외국인등록증을\n꼭 보여 주세요</size>", "#FFF6E8", "#A3301E", 30);
            InteriorKit.Clock(root, new Vector3(-5.9f, 2.32f, d / 2 - 0.035f), 180, 0.34f);

            // the east wall by the treatment rooms: a bench with a parent and a child, the check-up notice
            var eastBench = InteriorKit.Sofa(root, new Vector3(w / 2 - 0.38f, 0, 2.1f), -90, 3, leather, white, 0.62f, true);
            Extra(r, "Parent", HumanLook.Office(new System.Random(73), true), eastBench[0].position, -90, sitting: true, talking: true);
            Extra(r, "Child", HumanLook.Schoolkid(new System.Random(74), false), eastBench[1].position, -90, sitting: true, phone: true);
            Poster(root, new Vector3(w / 2 - 0.03f, 1.75f, 2.1f), -90, new Vector2(1.1f, 0.62f), "국가건강검진 지정기관\n<size=22>위내시경 · 초음파 · 혈액검사\n예약은 접수처에서</size>", "#FFFFFF", "#16614A", 30);
            Box(root, "WallSanitizer", new Vector3(w / 2 - 0.06f, 1.25f, 3.6f), new Vector3(0.1f, 0.24f, 0.12f), C("#F4F4F1"), false);
            Box(root, "WallSanitizerPump", new Vector3(w / 2 - 0.12f, 1.1f, 3.6f), new Vector3(0.04f, 0.04f, 0.05f), mint, false);
            HangingSign(r, "ToiletSign", new Vector3(4.4f, 2.42f, 3.6f), 180, new Vector2(1.5f, 0.24f), mint, "← 정수기 · 혈압계", Color.white, 40);

            // by the entrance: the flu-shot banner, the leaflet rack, a tall plant
            InteriorKit.XBanner(root, new Vector3(-2.6f, 0, -d / 2 + 1.0f), 180, "독감\n예방접종", "10월 접종 중\n\n만 65세 이상 무료\n일반 접수 가능\n\n접수처에\n문의하세요", mint, C("#FFFFFF"));
            InteriorKit.LeafletRack(root, new Vector3(-4.2f, 0, -d / 2 + 0.25f), 0, rng);
            Plant(root, new Vector3(-5.7f, 0, -d / 2 + 0.55f), 1.15f);
            Plant(root, new Vector3(2.45f, 0, -d / 2 + 0.6f), 0.95f);
            InteriorKit.Extinguisher(root, new Vector3(-w / 2 + 0.28f, 0, -d / 2 + 0.4f), 90);

            // ---- 1진료실, the consulting room in the south-east corner (the director seats the player here)
            float rx0 = 3.0f, rz1 = -0.6f, rz0 = -d / 2;
            var roomWall = TextureKit.Surface("Wall_Consult", TextureKit.WallPaint(), C("#F6F4EF"));
            Slab(root, "RoomWallN", new Vector3((rx0 + w / 2) / 2f, h / 2, rz1), new Vector3(w / 2 - rx0, h, 0.15f), roomWall);
            Slab(root, "RoomWallW", new Vector3(rx0, h / 2, (rz0 + rz1) / 2f), new Vector3(0.15f, h, rz1 - rz0), roomWall);
            InteriorKit.RoomDoor(root, new Vector3(3.75f, 0, rz1 + 0.075f), 0, "1진료실", "박준호 원장", door, mint, "진료중");
            // the player walks in here when their name is called, and comes back out facing the reception
            var entranceGo = new GameObject("Room_doctor");
            entranceGo.transform.SetParent(root, false);
            entranceGo.transform.position = new Vector3(3.75f, 1.0f, rz1 + 0.35f);
            var roomCol = entranceGo.AddComponent<BoxCollider>();
            roomCol.isTrigger = true;
            roomCol.size = new Vector3(1.2f, 2.0f, 0.6f);
            var consult = entranceGo.AddComponent<RoomEntrance>();
            consult.location = "doctor";
            consult.label = "1진료실";
            consult.approach = Group("DoorApproach_doctor", root);
            consult.approach.SetPositionAndRotation(new Vector3(3.75f, 0, rz1 + 0.12f), Quaternion.identity);
            consult.outside = Group("Outside_doctor", root);
            consult.outside.SetPositionAndRotation(new Vector3(3.75f, 0.05f, rz1 + 1.15f), Quaternion.LookRotation(new Vector3(-5.85f, 0, 2.1f)));
            Slab(root, "ConsultDoorInside", new Vector3(3.75f, 1.05f, rz1 - 0.095f), new Vector3(0.95f, 2.1f, 0.04f), door, false);
            Poster(root, new Vector3(rx0 - 0.085f, 1.55f, -2.6f), -90, new Vector2(0.62f, 0.86f), "기침 예절\n\n<size=24>기침할 땐\n옷소매로\n가려 주세요</size>", "#E8F6F1", "#16614A", 30);

            float dx = 5.0f, deskZ = -2.4f;
            InteriorKit.Desk(root, new Vector3(dx, 0, deskZ), 180, new Vector3(1.6f, InteriorKit.DeskTop, 0.75f), r.TopMat, oak);
            var doctorSeat = Group("DoctorSeat", root);
            doctorSeat.SetPositionAndRotation(new Vector3(dx, 0, deskZ + 0.77f), Quaternion.Euler(0, 180, 0));
            InteriorKit.Workstation(root, doctorSeat.position, deskZ + 0.375f, 1f, C("#DDEBFF"));
            var second = new Vector3(dx - 0.55f, InteriorKit.DeskTop, deskZ - 0.05f);
            var toDoc = doctorSeat.position - second;
            InteriorKit.Monitor(root, second, Mathf.Atan2(toDoc.x, toDoc.z) * Mathf.Rad2Deg, C("#E6F2FF"));
            InteriorKit.OfficeChair(root, new Vector3(dx, 0, deskZ + 0.7f), 180, C("#2B2E33"));
            Staff(r, "doctor", doctorSeat, HumanLook.Doctor());
            Box(root, "Chart", new Vector3(dx - 0.15f, InteriorKit.DeskTop + 0.005f, deskZ - 0.15f), new Vector3(0.22f, 0.01f, 0.3f), C("#FBF8EF"), false, -8);
            Box(root, "TongueDepressors", new Vector3(dx - 0.7f, InteriorKit.DeskTop + 0.06f, deskZ + 0.25f), new Vector3(0.06f, 0.12f, 0.06f), C("#D8DDE2"), false);
            for (int i = 0; i < 7; i++)
                Box(root, "SpineModel", new Vector3(dx + 0.72f, InteriorKit.DeskTop + 0.02f + i * 0.03f, deskZ + 0.25f), new Vector3(0.035f - i * 0.0015f, 0.02f, 0.03f), C("#EFE7D6"), false);
            InteriorKit.Stool(root, new Vector3(dx, 0, deskZ - 0.9f), C("#2B2E33"));
            var docSeat = Group("Seat_doctor", root);
            docSeat.SetPositionAndRotation(new Vector3(dx, 0.05f, deskZ - 0.96f), Quaternion.identity);
            docSeat.gameObject.AddComponent<PlayerSeat>();
            r.Anchors.Add(docSeat);
            for (int i = 0; i < 6; i++)
                InteriorKit.Certificate(root, new Vector3(dx - 0.5f + (i % 3) * 0.42f, 1.42f + (i / 3) * 0.52f, rz1 - 0.09f), 180,
                    new[] { "의사 면허증", "내과 전문의", "의학 박사", "소화기내과 분과", "대한내과학회", "건강검진 수료" }[i], rng);

            // examination bed with its paper roll and a curtain drawn back, the sink, the x-ray viewer, the cabinet
            var bed = Group("ExamBed", root);
            bed.position = new Vector3(w / 2 - 0.45f, 0, -3.65f);
            foreach (var z in new[] { -0.8f, 0.8f })
            foreach (var x in new[] { -0.3f, 0.3f })
                Box(bed, "Leg", new Vector3(x, 0.3f, z), new Vector3(0.05f, 0.6f, 0.05f), C("#B9BEC4"), false);
            Box(bed, "Pad", new Vector3(0, 0.66f, 0), new Vector3(0.68f, 0.12f, 1.9f), C("#5E8F86"));
            Box(bed, "Paper", new Vector3(0, 0.725f, 0.05f), new Vector3(0.52f, 0.005f, 1.7f), C("#FBFBF8"), false);
            Cylinder(bed, "PaperRoll", new Vector3(0, 0.62f, -1.0f), new Vector3(0.1f, 0.3f, 0.1f), C("#FBFBF8")).transform.localRotation = Quaternion.Euler(0, 0, 90);
            Box(bed, "Pillow", new Vector3(0, 0.77f, 0.72f), new Vector3(0.48f, 0.1f, 0.3f), Color.white, false);
            Box(root, "CurtainRail", new Vector3(w / 2 - 1.0f, h - 0.08f, -3.6f), new Vector3(0.03f, 0.03f, 2.4f), C("#B9BEC4"), false);
            for (int i = 0; i < 4; i++)
                Box(root, "Curtain", new Vector3(w / 2 - 1.0f + (i % 2) * 0.04f, 1.42f, -4.75f + i * 0.13f), new Vector3(0.03f, 2.4f, 0.14f), C("#9FD3C7"), false);
            Box(root, "SinkCabinet", new Vector3(rx0 + 0.36f, 0.43f, -4.3f), new Vector3(0.55f, 0.86f, 0.8f), C("#E9EAEA"));
            Box(root, "Sink", new Vector3(rx0 + 0.36f, 0.875f, -4.3f), new Vector3(0.38f, 0.03f, 0.5f), C("#D8DDE2"), false);
            Box(root, "Faucet", new Vector3(rx0 + 0.16f, 1.0f, -4.3f), new Vector3(0.03f, 0.22f, 0.03f), C("#B9BEC4"), false);
            Box(root, "TowelBox", new Vector3(rx0 + 0.1f, 1.45f, -4.3f), new Vector3(0.1f, 0.24f, 0.28f), C("#F4F4F1"), false);
            var xray = Emissive(root, "XrayViewer", new Vector3(rx0 + 0.09f, 1.65f, -2.2f), new Vector3(0.03f, 0.5f, 0.7f), C("#E6F2FF"), 1.2f);
            xray.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var cabinet = Group("MedicineCabinet", root);
            cabinet.SetPositionAndRotation(new Vector3(6.25f, 0, rz1 - 0.3f), Quaternion.Euler(0, 180, 0));
            // a glass-fronted upper case on a closed base, the boxes on its shelves showing through
            var cabinetWhite = TextureKit.Surface("Cabinet_White", TextureKit.Plastic(), C("#F4F4F1"), 0.8f);
            Slab(cabinet, "Base", new Vector3(0, 0.425f, 0), new Vector3(0.9f, 0.85f, 0.4f), cabinetWhite);
            Slab(cabinet, "Back", new Vector3(0, 1.325f, -0.18f), new Vector3(0.9f, 0.95f, 0.04f), cabinetWhite, false);
            foreach (var sx in new[] { -0.435f, 0.435f })
                Slab(cabinet, "Side", new Vector3(sx, 1.325f, 0), new Vector3(0.03f, 0.95f, 0.4f), cabinetWhite, false);
            Slab(cabinet, "Top", new Vector3(0, 1.785f, 0), new Vector3(0.9f, 0.03f, 0.4f), cabinetWhite, false);
            Glass(cabinet, "Doors", new Vector3(0, 1.32f, 0.19f), new Vector3(0.84f, 0.9f, 0.01f), 0, false);
            for (int i = 0; i < 3; i++)
            {
                Slab(cabinet, "Shelf", new Vector3(0, 0.86f + i * 0.3f, 0), new Vector3(0.84f, 0.02f, 0.34f), cabinetWhite, false, false);
                for (int k = 0; k < 6; k++)
                    Box(cabinet, "Box", new Vector3(-0.33f + k * 0.13f, 0.94f + i * 0.3f, 0.02f), new Vector3(0.1f, 0.14f, 0.12f),
                        C(new[] { "#FFFFFF", "#DFF1D2", "#8FD3F4", "#F7F3E3", "#E76F51" }[rng.Next(5)]), false);
            }
            InteriorKit.Clock(root, new Vector3(6.25f, 2.22f, rz1 - 0.08f), 180, 0.3f);
            // a roller blind half down over the street window
            Box(root, "RollerBlind", new Vector3(4.75f, 1.85f, -d / 2 + 0.04f), new Vector3(3.4f, 0.8f, 0.01f), C("#EDE7DA"), false);
            Box(root, "BlindRoll", new Vector3(4.75f, 2.22f, -d / 2 + 0.05f), new Vector3(3.45f, 0.06f, 0.06f), C("#D9D2C4"), false);

            var director = Director(r, new[] { "clinic", "clinic_certificate" }, 11, 9);
            director.rooms = new[] { consult };
            director.waitingBoard = waiting;
            EndMaterials();
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------- immigration

        /// <summary>
        /// 출입국 민원센터: a big civil-affairs hall. Cool troffer light on a 600 grid, terrazzo, beige
        /// laminate with a navy rail, six counters behind tall acrylic with blue signs and red LED numbers,
        /// the main board over the aisle, an LED ticker with the four steps, perforated steel beam seats,
        /// the information desk and kiosks by the door, the form table, and the self-service wall.
        /// </summary>
        [MenuItem("Seoul First Week/Build Immigration")]
        public static void BuildImmigration()
        {
            var scene = NewScene("Immigration");
            BeginMaterials();
            float w = 22, d = 15, h = 3.0f;
            var blue = C("#2F5FD0");
            var navy = C("#1F2F55");
            var laminate = TextureKit.Surface("Laminate_Beige", TextureKit.Plastic(), C("#D9CCB2"), 0.5f);
            var r = Shell(new RoomSpec
            {
                Name = "Immigration", Backdrop = "immigration", W = w, D = d, H = h, Style = CounterStyle.Low, Screens = true,
                CounterX0 = -w / 2 + 0.6f, CounterX1 = w / 2 - 0.6f, WindowX = new[] { -8.75f, -5.25f, -1.75f, 1.75f, 5.25f, 8.75f },
                NamePlates = new[] { "김도현 주무관", "한지민 주무관", "이서연 주무관", "박성우 주무관", "최유나 주무관", "정민재 주무관" },
                Wall = C("#EEF1F5"), Accent = blue, Light = C("#F2F6FF"),
                Floor = TextureKit.Surface("Floor_Immigration", TextureKit.FloorTerrazzo(), C("#E2E4E6")),
                Ceiling = TextureKit.Surface("Ceiling", TextureKit.CeilingTiles(), Color.white, 0.6f),
                CounterFront = laminate, Cabinet = laminate, Partition = laminate,
            });
            var root = r.Root;
            var rng = new System.Random(23);
            float F = r.CounterFrontZ;

            var ac = new[] { new Vector2(-5.4f, 0.0f), new Vector2(5.4f, 0.0f) };
            GridCeiling(r, true, C("#F4F8FF"), ac);
            foreach (var a in ac) InteriorKit.CeilingCassette(root, new Vector3(a.x, 0, a.y), h);
            InteriorKit.Cctv(root, new Vector3(w / 2 - 0.6f, h, -d / 2 + 0.6f));
            InteriorKit.Cctv(root, new Vector3(-w / 2 + 0.6f, h, d / 2 - 2.8f));
            Wainscot(r, laminate, Mats.Lit(navy, 0.35f), 1.1f, C("#4A4D52"));

            // six counters: staff, blue hanging signs with their own LED numbers, scanners and paperwork
            var labels = new[]
            {
                ("외국인 등록", "Registration"), ("체류기간 연장", "Extension of stay"), ("외국인 등록", "Registration"),
                ("체류자격 변경", "Change of status"), ("증명 발급", "Certificates"), ("사증 · 기타", "Visa · Others"),
            };
            CounterStaff(r, 1, "clerk1", HumanLook.Officer(false, 0));
            CounterStaff(r, 2, "clerk2", HumanLook.Officer(true, 1));
            CounterStaff(r, 3, "officer", HumanLook.Officer(true, 2));
            CounterStaff(r, 4, "clerk4", HumanLook.Officer(false, 3));
            CounterStaff(r, 5, "clerk5", HumanLook.Officer(true, 4));
            CounterStaff(r, 6, "clerk6", HumanLook.Officer(false, 5));
            for (int i = 0; i < r.WindowX.Length; i++)
            {
                float x = r.WindowX[i];
                float side = i % 2 == 0 ? 1f : -1f;
                r.Boards.Add(InteriorKit.HangingCounterSign(root, new Vector3(x, 2.32f, F + 0.3f), 180, i + 1, labels[i].Item1, labels[i].Item2, blue, h));
                Box(root, "Scanner", new Vector3(x + 0.32f, 0.78f, F + 0.18f), new Vector3(0.2f, 0.07f, 0.16f), C("#2B2E33"), false);
                Emissive(root, "ScannerGlass", new Vector3(x + 0.32f, 0.818f, F + 0.18f), new Vector3(0.12f, 0.006f, 0.09f), C("#39FF88"), 1.5f);
                // document bundles on the staff side, some piled high
                int piles = 1 + rng.Next(3);
                for (int p = 0; p < piles; p++)
                {
                    float ph = 0.04f + (float)rng.NextDouble() * (p == 0 ? 0.32f : 0.12f);
                    Box(root, "Paperwork", new Vector3(x - side * (0.45f + p * 0.26f), InteriorKit.DeskTop + ph / 2f, r.DeskBack - 0.2f), new Vector3(0.23f, ph, 0.31f),
                        C(p % 2 == 0 ? "#F7F6F1" : "#EDE6D3"), false, (float)rng.NextDouble() * 10f - 5f);
                }
                Box(root, "Folder", new Vector3(x + side * 0.1f, InteriorKit.DeskTop + 0.008f, F + 0.5f), new Vector3(0.24f, 0.012f, 0.32f), C(new[] { "#2F5FD0", "#E5484D", "#F2C230" }[i % 3]), false, 8);
            }
            InteriorKit.DeskSign(root, new Vector3(8.75f + 0.5f, InteriorKit.DeskTop, F + 0.06f), 0, "점심시간 12:00~13:00", navy, 0.46f);
            MainBoard(r, new Vector3(0, 2.4f, r.AisleZ - 0.25f), 180, 3.6f, 6, true);
            InteriorKit.TickerStrip(root, new Vector3(0, 2.8f, d / 2 - 0.03f), 180, 12f,
                "Please have the following ready   ① Check the reservation list   ② Fill out the application form   ③ Prepare your documents   ④ Purchase a revenue stamp      " +
                "① 예약 확인   ② 신청서 작성   ③ 서류 준비   ④ 수입인지 구입      번호표를 뽑고 기다려 주세요 · Please take a number and wait",
                C("#FFB547"), 70f);

            // waiting area: four rows of perforated steel beam seats, lanes at the door and between the blocks
            var sheet = TextureKit.Surface("Seat_Perforated", TextureKit.Perforated(), C("#B7BEC6"), 0.6f, 0.5f);
            float row = r.AisleZ - 1.3f;
            for (int i = 0; i < 4; i++, row -= 1.35f)
                SeatRow(r, row, SeatKind.Steel, sheet, null, (-7.48f, 5), (-3.28f, 5), (0.8f, 5), (5.0f, 5));
            r.SeatGaps.AddRange(new[] { -4.14f, 0f, 4.14f });

            // the entrance: two ticket kiosks, the information desk, a guide in a yellow vest, the process board
            Ticket(r, new Vector3(-2.9f, 0, -d / 2 + 0.6f), 0, "번호표 · Ticket", "업무를 선택하세요\n<size=20>외국인 등록 · 체류 연장\n증명 발급 · 사증</size>");
            InteriorKit.TicketKiosk(root, new Vector3(-3.65f, 0, -d / 2 + 0.6f), 0, blue, "번호표 · Ticket", "Select a service\n<size=20>Registration · Extension\nCertificates · Visa</size>", out _);
            var desk = InteriorKit.InfoDesk(root, new Vector3(2.4f, 0, -d / 2 + 2.9f), 180, r.TopMat, laminate, blue, "안내 · Information");
            InteriorKit.OfficeChair(root, desk.TransformPoint(new Vector3(0, 0, -0.35f)), 180, C("#2B2E33"));
            Extra(r, "InfoStaff", HumanLook.Officer(true, 6), desk.TransformPoint(new Vector3(0, 0, -0.42f)), 180, sitting: true, talking: true, lookAt: r.Player.cameraPivot,
                seat: InteriorKit.OfficeSeat);
            var guideLook = HumanLook.Officer(false, 7);
            guideLook.Outer = OuterKind.UniformVest;
            guideLook.OuterColor = C("#F2C230");
            Extra(r, "Guide", guideLook, new Vector3(-3.2f, 0, -d / 2 + 2.8f), 200, lookAt: r.Player.cameraPivot);
            InteriorKit.Easel(root, new Vector3(-5.6f, 0, -d / 2 + 2.9f), 180, "업무처리 순서 · Process", new[]
            {
                "번호표 · Take a number", "신청서 작성 · Fill in the form", "창구 접수 · Go to the counter", "수수료 · Pay the fee",
            }, new[] { C("#2F5FD0"), C("#1FA37A"), C("#E58B2B"), C("#C8402E") });
            Sign(root, "Welcome", new Vector3(w / 4f + 0.7f, 2.72f, -d / 2 + 0.05f), new Vector2(4.6f, 0.34f), blue,
                "환영합니다 · Welcome · Chào mừng · Selamat datang", Color.white, 0, 40, 0.02f);
            Sign(root, "HallName", new Vector3(-(w / 4f + 0.7f), 2.72f, -d / 2 + 0.05f), new Vector2(4.6f, 0.34f), navy,
                "외국인 민원실 · Foreign Resident Services", Color.white, 0, 40, 0.02f);

            // the west wall: the form table with its samples, the notice board, water and a TV
            var table = Group("FormTable", root);
            table.position = new Vector3(-w / 2 + 0.9f, 0, 0.2f);
            Slab(table, "Top", new Vector3(0, 0.95f, 0), new Vector3(0.9f, 0.06f, 3.2f), r.TopMat);
            Slab(table, "Base", new Vector3(0, 0.46f, 0), new Vector3(0.7f, 0.92f, 3.0f), laminate);
            for (int i = 0; i < 5; i++)
            {
                Box(table, "Form", new Vector3(0.05f, 0.99f, -1.2f + i * 0.6f), new Vector3(0.3f, 0.01f, 0.42f), Color.white, false, 8 * (i % 2));
                Cylinder(table, "Pen", new Vector3(0.25f, 1.0f, -1.1f + i * 0.6f), new Vector3(0.015f, 0.08f, 0.015f), C("#1F2F55"));
                if (i % 2 == 0) Cylinder(table, "GlueStick", new Vector3(-0.2f, 1.02f, -1.0f + i * 0.6f), new Vector3(0.03f, 0.05f, 0.03f), C("#E5484D"));
            }
            var sample = Group("Sample", table);
            sample.localPosition = new Vector3(-0.25f, 0.98f, -0.2f);
            sample.localRotation = Quaternion.Euler(-10, 90, 0);
            Box(sample, "Stand", new Vector3(0, 0.17f, 0), new Vector3(0.3f, 0.34f, 0.01f), C("#FFFFFF"), false);
            WorldText.Create(sample, "Text", "작성 예시\nSAMPLE\n<size=12>통합신청서 (신고서)\nAPPLICATION FORM</size>", new Vector3(0, 0.17f, 0.007f), new Vector2(0.27f, 0.3f), 30,
                C("#C8402E"), true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            Sign(table, "Sign", new Vector3(-0.87f, 2.62f, 0), new Vector2(1.9f, 0.42f), blue, "신청서 작성대 · Forms", Color.white, 90, 44, 0.04f)
                .localRotation = Quaternion.Euler(0, 90, 0);
            var tcol = table.gameObject.AddComponent<BoxCollider>();
            tcol.center = new Vector3(0, 1.0f, 0);
            tcol.size = new Vector3(1.2f, 1.2f, 3.3f);
            tcol.isTrigger = true;
            var station = table.gameObject.AddComponent<FetchStation>();
            station.stationId = "FormTable";
            station.document = "arc_form";
            var writing = Group("WritingSpot", table);
            writing.localPosition = new Vector3(0, 0.98f, 1.25f);
            Extra(r, "Writer", HumanLook.Abroad(new System.Random(81), true), new Vector3(-w / 2 + 1.75f, 0, 1.45f), -90, lookAt: writing);
            InteriorKit.NoticeBoard(root, new Vector3(-w / 2 + 0.03f, 1.78f, 0.2f), 90, new Vector2(2.7f, 0.95f), "공지사항 · Notice", navy, new[]
            {
                "방문 예약 안내\n\n하이코리아에서\n방문 예약 후\n오세요",
                "외국인 등록\n수수료 안내\n\n2025. 1. 1.부터\n35,000원",
                "Reservation\nrequired\n\nBook a visit on\nHiKorea first",
                "1345\n외국인종합안내\n\n20개 언어\n전화 상담",
                "점심시간\nLUNCH BREAK\n\n12:00 ~ 13:00",
                "사진 규격\n3.5 × 4.5 cm\n\n6개월 이내\n촬영",
                "체류기간 연장은\n만료 전에\n신청하세요",
                "Lost your card?\n\nReport within\n14 days",
                "통역 서비스\n\nVietnamese\nIndonesian\nEnglish",
            }, rng);
            InteriorKit.WaterPurifier(root, new Vector3(-w / 2 + 0.3f, 0, -2.6f), 90);
            InteriorKit.WallTv(root, new Vector3(-w / 2 + 0.05f, 2.05f, -2.6f), 90, C("#1F3F8C"), "HiKorea", new[]
            {
                "HiKorea 방문 예약\n<size=28>www.hikorea.go.kr</size>",
                "체류기간 연장\n<size=28>만료일 전에 신청하세요</size>",
                "1345 외국인종합안내센터\n<size=28>20개 언어로 상담해 드려요</size>",
            }, new[] { C("#1F3F8C"), C("#1B6A5E"), C("#3A2F6B") });
            InteriorKit.Clock(root, new Vector3(-w / 2 + 0.03f, 2.5f, -5.2f), 90, 0.5f);
            InteriorKit.Extinguisher(root, new Vector3(-w / 2 + 0.3f, 0, -d / 2 + 0.9f), 90);

            // the east wall: photo booth, certificate kiosk, copier and the 수입인지 machine
            InteriorKit.PhotoBooth(root, new Vector3(w / 2 - 0.55f, 0, -d / 2 + 1.6f), -90);
            Extra(r, "PhotoWaiter", HumanLook.Abroad(new System.Random(82), false), new Vector3(w / 2 - 1.7f, 0, -d / 2 + 1.9f), 90, phone: true);
            InteriorKit.Kiosk(root, new Vector3(w / 2 - 0.4f, 0, -d / 2 + 3.2f), -90, "무인민원발급기");
            InteriorKit.Copier(root, new Vector3(w / 2 - 0.45f, 0, -d / 2 + 4.3f), -90);
            InteriorKit.Kiosk(root, new Vector3(w / 2 - 0.4f, 0, -d / 2 + 5.4f), -90, "수입인지 · 수수료");
            Poster(root, new Vector3(w / 2 - 0.03f, 1.75f, -d / 2 + 6.5f), -90, new Vector2(0.8f, 0.9f), "수입인지\nRevenue stamp\n\n<size=22>현금 · 카드\nCash · Card</size>", "#FFFFFF", "#1F2F55", 30);
            InteriorKit.WallTv(root, new Vector3(w / 2 - 0.05f, 2.05f, 0.6f), -90, C("#1F3F8C"), "체류 안내", new[]
            {
                "외국인 등록\n<size=28>입국 후 90일 이내</size>",
                "Residence card\n<size=28>Apply within 90 days of entry</size>",
                "Đăng ký người nước ngoài\n<size=28>trong vòng 90 ngày</size>",
            }, new[] { C("#1F3F8C"), C("#2F4A7A"), C("#1B6A5E") }, 1.3f, 2f);
            InteriorKit.Clock(root, new Vector3(w / 2 - 0.03f, 2.5f, 3.2f), -90, 0.5f);
            Plant(root, new Vector3(w / 2 - 0.6f, 0, r.CounterFrontZ - 0.7f));
            Plant(root, new Vector3(-w / 2 + 0.6f, 0, r.CounterFrontZ - 0.7f));

            Director(r, new[] { "immigration", "immigration_extension" }, 23, 18, station);
            EndMaterials();
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------- bank

        /// <summary>
        /// 새싹은행 명동지점: a polished branch. Speckled granite, a gypsum ceiling with linear lights and a
        /// wood-slat field over the hall, four teller desks with brand partitions, hanging LED counter signs,
        /// the slatted logo wall, green benches in rows, the lit 365 ATM corner by the door, the guard and
        /// the ticket kiosk, exchange and deposit rate boards, the coffee corner, and the VIP room.
        /// </summary>
        [MenuItem("Seoul First Week/Build Bank")]
        public static void BuildBank()
        {
            var scene = NewScene("Bank");
            BeginMaterials();
            float w = 18, d = 13, h = 3.2f;
            var green = C("#5BAA2E");
            var deep = C("#2F6B1E");
            var wood = TextureKit.Surface("Wood_Bank", TextureKit.WoodDark(), C("#E8DCCB"));
            var gypsum = TextureKit.Surface("Gypsum", TextureKit.WallPaint(), Color.white, 0.5f);
            var r = Shell(new RoomSpec
            {
                Name = "Bank", Backdrop = "bank", W = w, D = d, H = h, Style = CounterStyle.Low,
                CounterX0 = -7.9f, CounterX1 = 5.3f, WindowX = new[] { -6.3f, -3.0f, 0.3f, 3.6f },
                NamePlates = new[] { "김하늘 계장", "최민수 대리", "정우진 과장", "윤서영 대리" },
                Wall = C("#F5F3EE"), Accent = green, Light = C("#F6F8FF"),
                Floor = TextureKit.Surface("Floor_Bank", TextureKit.FloorGranite(), C("#D6D5D1")),
                Ceiling = gypsum, CounterFront = wood, Cabinet = wood,
                Partition = TextureKit.Surface("Partition_Bank", TextureKit.Plastic(), green, 0.6f),
            });
            var root = r.Root;
            var rng = new System.Random(37);
            float F = r.CounterFrontZ;

            // ceiling: a wood-slat field over the benches, linear lights over the counters, downlights elsewhere
            var field = new Rect(-7.2f, -2.3f, 11.6f, 4.0f);
            var slatMat = TextureKit.Surface("Slat_Bank", TextureKit.WoodLight(), C("#F3E4CC"));
            var ceilingSlats = Group("CeilingSlats", root);
            // slats run towards the counters, so from the door one looks along them and sees the ceiling between
            for (float x = field.xMin; x <= field.xMax + 0.01f; x += 0.24f)
                Slab(ceilingSlats, "Slat", new Vector3(x, h - 0.1f, field.center.y), new Vector3(0.05f, 0.06f, field.height), slatMat, false, false);
            foreach (var z in new[] { field.yMin + 0.3f, field.center.y, field.yMax - 0.3f })
                Slab(ceilingSlats, "Bearer", new Vector3(field.center.x, h - 0.035f, z), new Vector3(field.width + 0.1f, 0.07f, 0.05f), slatMat, false, false);
            Merge(ceilingSlats, "Bank_CeilingSlats");
            var strip = Mats.Emissive(C("#F7F9FF"), 1.8f);
            for (float x = field.xMin + 1.44f; x < field.xMax - 1.0f; x += 2.88f)
                Slab(root, "SlatLight", new Vector3(x + 0.12f, h - 0.04f, field.center.y), new Vector3(0.08f, 0.02f, field.height - 0.4f), strip, false, false);
            for (float x = -7.2f; x < 5.0f; x += 1.6f)
                InteriorKit.LinearLight(root, new Vector3(x + 0.6f, 0, r.AisleZ + 0.2f), h, 1.4f, 0, strip);
            var ac = new[] { new Vector2(-7.4f, 1.0f), new Vector2(7.4f, -1.0f) };
            Downlights(r, 1.8f, C("#F7F9FF"), new Rect(field.xMin - 0.3f, field.yMin - 0.3f, field.width + 0.6f, field.height + 0.6f + 1.6f), ac);
            foreach (var a in ac) InteriorKit.CeilingCassette(root, new Vector3(a.x, 0, a.y), h);
            InteriorKit.Cctv(root, new Vector3(-w / 2 + 3.9f, h, -d / 2 + 0.6f));
            InteriorKit.Cctv(root, new Vector3(w / 2 - 0.6f, h, d / 2 - 4.6f));
            Wainscot(r, null, null, 0, C("#7E7C78"));

            // the logo wall: vertical oak slats behind the tellers, the backlit logo and the call screen
            var wallSlats = Group("LogoWallSlats", root);
            for (float x = -7.86f; x < 5.28f; x += 0.14f)
                Slab(wallSlats, "Slat", new Vector3(x, (0.93f + h) / 2f, d / 2 - 0.03f), new Vector3(0.05f, h - 0.93f, 0.04f), slatMat, false, false);
            Merge(wallSlats, "Bank_LogoSlats");
            var logo = Group("Logo", root);
            logo.SetPositionAndRotation(new Vector3(-4.65f, 2.82f, d / 2 - 0.07f), Quaternion.Euler(0, 180, 0));
            Emissive(logo, "Plate", Vector3.zero, new Vector3(1.84f, 0.66f, 0.02f), C("#F4FFEC"), 0.6f).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Sphere(logo, "LeafA", new Vector3(0.66f, 0.06f, 0.02f), new Vector3(0.24f, 0.14f, 0.04f), green);
            Sphere(logo, "LeafB", new Vector3(0.5f, 0.14f, 0.02f), new Vector3(0.2f, 0.12f, 0.04f), C("#7CC242"));
            WorldText.Create(logo, "Name", "새싹은행", new Vector3(-0.12f, 0.06f, 0.014f), new Vector2(1.2f, 0.36f), 80, deep, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            WorldText.Create(logo, "En", "SAESSAK BANK · 명동지점", new Vector3(-0.12f, -0.2f, 0.014f), new Vector2(1.3f, 0.12f), 32, C("#6B6F75"), true, TextAnchor.MiddleCenter,
                Quaternion.Euler(0, 180, 0));
            MainBoard(r, new Vector3(1.95f, 2.86f, d / 2 - 0.08f), 180, 1.7f, 0, false);

            // four teller desks
            var labels = new[]
            {
                ("빠른창구 · 입출금", "Deposits · Withdrawals"), ("개인 상담 · 신규", "New accounts"),
                ("외환 · 해외송금", "Exchange · Remittance"), ("대출 상담", "Loans"),
            };
            CounterStaff(r, 1, "teller1", HumanLook.Teller(true, 0));
            CounterStaff(r, 2, "clerk", HumanLook.Teller(false, 1));
            CounterStaff(r, 3, "teller3", HumanLook.Teller(false, 2));
            CounterStaff(r, 4, "teller4", HumanLook.Teller(true, 3));
            for (int i = 0; i < r.WindowX.Length; i++)
            {
                float x = r.WindowX[i];
                float side = i % 2 == 0 ? 1f : -1f;
                r.Boards.Add(InteriorKit.HangingCounterSign(root, new Vector3(x, 2.22f, F + 0.3f), 180, i + 1, labels[i].Item1, labels[i].Item2, green, h));
                // the PIN pad faces the customer; the banknote counter and passbook printer sit by the teller
                Box(root, "PinPad", new Vector3(x + 0.1f, 0.79f, F + 0.15f), new Vector3(0.16f, 0.06f, 0.2f), C("#2B2E33"), false, 10);
                Emissive(root, "PinScreen", new Vector3(x + 0.1f, 0.83f, F + 0.1f), new Vector3(0.1f, 0.01f, 0.05f), C("#9CC7FF"), 1.2f);
                var counterPos = new Vector3(x - side * 0.5f, InteriorKit.DeskTop, r.DeskBack - 0.22f);
                Box(root, "NoteCounter", counterPos + new Vector3(0, 0.09f, 0), new Vector3(0.28f, 0.18f, 0.26f), C("#3A3D42"), false);
                Box(root, "NoteHopper", counterPos + new Vector3(0, 0.2f, -0.04f), new Vector3(0.18f, 0.05f, 0.1f), C("#2B2E33"), false);
                Box(root, "Notes", counterPos + new Vector3(0, 0.235f, -0.04f), new Vector3(0.15f, 0.02f, 0.07f), C("#C9D9A6"), false);
                Box(root, "PassbookPrinter", new Vector3(x - side * 0.95f, InteriorKit.DeskTop + 0.08f, r.DeskBack - 0.25f), new Vector3(0.42f, 0.16f, 0.32f), C("#E3E5E7"), false);
                Box(root, "PrinterSlot", new Vector3(x - side * 0.95f, InteriorKit.DeskTop + 0.1f, r.DeskBack - 0.41f), new Vector3(0.3f, 0.02f, 0.01f), C("#1E2024"), false);
                InteriorKit.Stool(root, new Vector3(x + 0.62f, 0, F - 0.55f), deep);
                if (i % 2 == 0) InteriorKit.Orchid(root, new Vector3(x - side * 0.62f, InteriorKit.DeskTop, F + 0.22f), 30f * i, rng);
                else
                {
                    var stand = Group("LeafletStand", root);
                    stand.position = new Vector3(x - side * 0.62f, InteriorKit.DeskTop, F + 0.2f);
                    Box(stand, "Acrylic", new Vector3(0, 0.1f, 0), new Vector3(0.16f, 0.2f, 0.06f), new Color(0.9f, 0.95f, 1f), false);
                    Box(stand, "Leaflet", new Vector3(0, 0.11f, -0.01f), new Vector3(0.14f, 0.19f, 0.01f), C("#DFF1D2"), false);
                }
            }

            // the hall: green benches in rows facing the counters
            var bench = TextureKit.Surface("Bench_Bank", TextureKit.FabricWeave(), C("#7DAA62"), 0.3f);
            var dark = TextureKit.Surface("BenchArm_Bank", TextureKit.WoodDark(), C("#C9B49A"));
            float row = r.AisleZ - 1.3f;
            for (int i = 0; i < 3; i++, row -= 1.35f)
                SeatRow(r, row, SeatKind.Sofa, bench, dark, (-6.6f, 3), (-3.65f, 3), (-0.55f, 3), (2.4f, 3));
            r.SeatGaps.AddRange(new[] { -4.5f, -1.48f, 1.55f });

            // 365 자동화코너: the lit ATM corner behind glass by the door, open after hours
            float vx = -5.4f, vz = -3.4f;
            InteriorKit.GlassPartition(root, new Vector3(-w / 2, 0, vz), new Vector3(vx, 0, vz), h, C("#DFF1D2"));
            InteriorKit.GlassPartition(root, new Vector3(vx, 0, vz), new Vector3(vx, 0, -4.15f), h, C("#DFF1D2"));
            InteriorKit.GlassPartition(root, new Vector3(vx, 0, -5.25f), new Vector3(vx, 0, -d / 2), h, C("#DFF1D2"));
            Slab(root, "AtmDoorHead", new Vector3(vx, 2.3f, -4.7f), new Vector3(0.06f, 0.06f, 1.1f), Mats.Lit(C("#3A3D42"), 0.4f, 0.4f), false);
            Glass(root, "AtmTransom", new Vector3(vx, 2.33f + (h - 2.33f) / 2f, -4.7f), new Vector3(0.012f, h - 2.33f, 1.1f), 0, false);
            Sign(root, "AtmSign", new Vector3(vx + 0.08f, 2.62f, -4.7f), new Vector2(1.8f, 0.36f), green, "365 자동화코너 · 24시간", Color.white, 90, 40, 0.03f);
            Slab(root, "AtmSurround", new Vector3(-w / 2 + 0.02f, 1.2f, (vz - d / 2) / 2f), new Vector3(0.04f, 2.4f, vz + d / 2 - 0.1f), Mats.Lit(green, 0.4f), false);
            Emissive(root, "AtmGlow", new Vector3(-w / 2 + 0.05f, 2.32f, (vz - d / 2) / 2f), new Vector3(0.02f, 0.1f, vz + d / 2 - 0.2f), C("#B6F08C"), 2f);
            for (int i = 0; i < 3; i++)
            {
                var atm = Group("ATM", root);
                atm.SetPositionAndRotation(new Vector3(-w / 2 + 0.36f, 0, -5.95f + i * 0.95f), Quaternion.Euler(0, 90, 0));
                Box(atm, "Body", new Vector3(0, 0.85f, 0), new Vector3(0.82f, 1.7f, 0.6f), C("#DCDFE3"));
                Box(atm, "Hood", new Vector3(0, 1.62f, 0.08f), new Vector3(0.82f, 0.16f, 0.5f), green, false);
                Emissive(atm, "Screen", new Vector3(0, 1.25f, 0.31f), new Vector3(0.46f, 0.34f, 0.01f), C("#9FE07A"), 1.1f);
                Box(atm, "Keys", new Vector3(0, 0.95f, 0.32f), new Vector3(0.3f, 0.12f, 0.05f), C("#555A60"), false);
                Box(atm, "CardSlot", new Vector3(0.28f, 1.05f, 0.31f), new Vector3(0.1f, 0.03f, 0.02f), C("#1E2024"), false);
                if (i < 2) Box(atm, "Divider", new Vector3(0.45f, 1.0f, 0.15f), new Vector3(0.04f, 1.9f, 0.7f), C("#C9CDD2"), false);
                var flavor = atm.gameObject.AddComponent<FlavorInteractable>();
                flavor.promptKey = "atm_prompt";
                flavor.messageKey = "atm";
            }
            Extra(r, "AtmUser", HumanLook.Office(new System.Random(91), false), new Vector3(-w / 2 + 1.15f, 0, -5.0f), -90);

            // the entrance: ticket kiosk and the guard (청원경찰) who greets everyone coming in
            Ticket(r, new Vector3(2.5f, 0, -d / 2 + 0.8f), 270, "번호표", "업무를 선택하세요\n<size=22>입출금 · 상담 · 외환</size>");
            var guard = HumanBuilder.Build(root, "SecurityGuard", HumanLook.Guard());
            guard.transform.SetPositionAndRotation(new Vector3(3.6f, 0, -d / 2 + 1.3f), Quaternion.Euler(0, 250, 0));
            guard.lookTarget = r.Player.cameraPivot;
            guard.Pose(0f);
            InteriorKit.XBanner(root, new Vector3(-2.4f, 0, -d / 2 + 1.1f), 180, "점심시간\n집중상담", "12:00 ~ 13:00\n\n모든 창구에서\n상담해 드립니다\n\n번호표를\n뽑아 주세요", deep, C("#FFFFFF"));
            InteriorKit.BrochureStand(root, new Vector3(4.6f, 0, -d / 2 + 0.3f), 0, green, rng);
            InteriorKit.BrochureStand(root, new Vector3(5.6f, 0, -d / 2 + 0.3f), 0, green, rng);
            InteriorKit.Extinguisher(root, new Vector3(w / 2 - 0.3f, 0, -2.4f), -90);

            // the east wall: rates, slips, coffee and water, newspapers
            InteriorKit.RateBoard(root, new Vector3(w / 2 - 0.04f, 1.98f, -0.4f), -90, new Vector2(2.0f, 1.25f), "오늘의 환율 · Exchange rates", new[] { "통화", "살 때", "팔 때" }, new[]
            {
                new[] { "USD 미국", "1,402.60", "1,354.40" }, new[] { "EUR 유럽", "1,632.10", "1,568.70" }, new[] { "CNY 중국", "195.20", "176.60" },
                new[] { "VND 100", "5.72", "5.06" }, new[] { "IDR 100", "8.79", "8.23" },
            }, deep);
            InteriorKit.TickerStrip(root, new Vector3(w / 2 - 0.035f, 1.22f, -0.4f), -90, 2.0f,
                "해외송금 수수료 우대 · 외국인 고객 계좌 개설 안내 · 체크카드 즉시 발급 · Overseas remittance · Foreign customers welcome", C("#FFB547"), 45f);
            InteriorKit.SlipDesk(root, new Vector3(w / 2 - 0.5f, 0, 1.45f), -90, r.TopMat, wood);
            var slipTop = Group("SlipTop", root);
            slipTop.position = new Vector3(w / 2 - 0.5f, 1.02f, 1.45f);
            Extra(r, "SlipWriter", HumanLook.SeniorPerson(new System.Random(92), true), new Vector3(w / 2 - 1.2f, 0, 1.55f), 90, lookAt: slipTop);
            InteriorKit.NewspaperRack(root, new Vector3(w / 2 - 0.25f, 0, -3.1f), -90);
            InteriorKit.WaterPurifier(root, new Vector3(w / 2 - 0.25f, 0, -4.1f), -90);
            InteriorKit.CoffeeCorner(root, new Vector3(w / 2 - 0.27f, 0, -5.4f), -90, r.TopMat, wood, deep);

            // the west wall: deposit rates, the market TV, the phishing warning
            InteriorKit.RateBoard(root, new Vector3(-w / 2 + 0.04f, 1.9f, 2.2f), 90, new Vector2(1.7f, 1.0f), "예금 금리 안내", new[] { "상품", "기간", "금리" }, new[]
            {
                new[] { "새싹 정기예금", "12개월", "연 3.10%" }, new[] { "새싹 적금", "12개월", "연 3.40%" }, new[] { "입출금 통장", "-", "연 0.10%" },
            }, deep);
            InteriorKit.WallTv(root, new Vector3(-w / 2 + 0.05f, 2.15f, -0.5f), 90, C("#1E3A2A"), "경제 뉴스", new[]
            {
                "경제 뉴스\n<size=28>코스피 2,610.35 ▲ 0.42%</size>",
                "새싹 체크카드\n<size=28>외국인 고객도 바로 발급</size>",
                "해외송금\n<size=28>수수료 50% 우대 이벤트</size>",
                "환율 동향\n<size=28>원·달러 1,378.50</size>",
            }, new[] { C("#1E3A2A"), C("#2F6B1E"), C("#1F3F5C"), C("#3A2F4A") }, 1.3f, 1.5f);
            Poster(root, new Vector3(-w / 2 + 0.03f, 1.6f, -2.4f), 90, new Vector2(0.9f, 0.9f), "보이스피싱 주의!\n\n<size=22>수사기관 · 금융기관은\n절대 돈을\n요구하지 않습니다</size>", "#FFF3D6", "#A3301E", 30);
            InteriorKit.Clock(root, new Vector3(-w / 2 + 0.03f, 2.5f, 3.6f), 90, 0.42f);
            Plant(root, new Vector3(-w / 2 + 0.6f, 0, F - 0.7f));

            // VIP 상담실: a glass room with a carpet, armchairs and a round table in the north-east corner
            float px = 5.9f, pz = 2.4f;
            InteriorKit.GlassPartition(root, new Vector3(px, 0, d / 2), new Vector3(px, 0, pz), h, C("#DFF1D2"));
            InteriorKit.GlassPartition(root, new Vector3(px, 0, pz), new Vector3(6.3f, 0, pz), h, C("#DFF1D2"));
            InteriorKit.GlassPartition(root, new Vector3(7.3f, 0, pz), new Vector3(w / 2, 0, pz), h, C("#DFF1D2"));
            Slab(root, "VipDoorHead", new Vector3(6.8f, 2.3f, pz), new Vector3(1.0f, 0.06f, 0.06f), Mats.Lit(C("#3A3D42"), 0.4f, 0.4f), false);
            Glass(root, "VipTransom", new Vector3(6.8f, 2.33f + (h - 2.33f) / 2f, pz), new Vector3(1.0f, h - 2.33f, 0.012f), 0, false);
            Sign(root, "VipSign", new Vector3(8.15f, 1.3f, pz - 0.02f), new Vector2(1.2f, 0.3f), deep, "VIP 상담실 · PB", Color.white, 180, 40, 0.01f);
            Slab(root, "VipCarpet", new Vector3((px + w / 2) / 2f, 0.006f, (pz + d / 2) / 2f), new Vector3(w / 2 - px - 0.05f, 0.012f, d / 2 - pz - 0.05f),
                TextureKit.Surface("Carpet_Vip", TextureKit.Carpet(), C("#6E6A66"), 0.2f), false, false);
            Cylinder(root, "VipTable", new Vector3(7.6f, 0.36f, 4.7f), new Vector3(0.8f, 0.36f, 0.8f), C("#4A3226"), true);
            var armchair = TextureKit.Surface("Armchair_Vip", TextureKit.Plastic(), C("#7A5A43"), 0.45f);
            InteriorKit.Sofa(root, new Vector3(7.6f, 0, 5.75f), 180, 1, armchair, dark, 0.7f);
            InteriorKit.Sofa(root, new Vector3(7.6f, 0, 3.6f), 0, 1, armchair, dark, 0.7f);
            Cylinder(root, "LampPole", new Vector3(8.6f, 0.8f, 6.1f), new Vector3(0.03f, 0.8f, 0.03f), C("#2B2D31"));
            Emissive(root, "LampShade", new Vector3(8.6f, 1.68f, 6.1f), new Vector3(0.36f, 0.26f, 0.36f), C("#FFE3B8"), 1.4f);
            Plant(root, new Vector3(6.3f, 0, 6.1f), 0.9f);

            Director(r, new[] { "bank", "bank_remittance" }, 37, 12);
            EndMaterials();
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------- results

        [MenuItem("Seoul First Week/Build Results")]
        public static void BuildResults()
        {
            var scene = NewScene("Results");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = C("#141A2A");
            camGo.AddComponent<AudioListener>();
            var screen = new GameObject("ResultsScreen").AddComponent<ResultsScreen>();
            screen.backdropIds = new[] { "clinic", "immigration", "bank" };
            screen.backdrops = screen.backdropIds.Select(id => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureKit.Folder}/Backdrop_{id}.png")).ToArray();
            EditorSceneManager.SaveScene(scene);
        }
    }
}
