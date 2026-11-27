using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.Quest;
using SeoulFirstWeek.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SeoulFirstWeek.EditorTools
{
    // TEMPORARY: records the raw takes of the presentation trailer (Recordings/Trailer/raw/<take>/). Delete after use.
    // Unity -batchmode -projectPath . -executeMethod SeoulFirstWeek.EditorTools.TrailerRecorder.Run
    // Recordings/Trailer/only.txt (take ids, one per line) records only those.
    [InitializeOnLoad]
    public static class TrailerRecorder
    {
        const string Stage = "trailer.stage";
        static readonly string Out = Path.GetFullPath("Recordings/Trailer");
        // SFW_REC_RAW names the folder under Recordings/Trailer (default raw), SFW_REC_SIZE the frame size (2560x1440),
        // SFW_REC_CRISPUI=1 lays the UI over the finished frame, SFW_REC_KEEP (take names, comma separated) keeps only
        // those takes: the others still play, so every take starts from the same state, but their frames are thrown away.
        static string Raw => Path.Combine(Out, Env("SFW_REC_RAW") ?? "raw");
        static string Env(string name) => System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
        static HashSet<string> keep;
        static bool skipping;
        static bool rec;
        const float Dt = 1f / 30f;

        static TrailerRecorder()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                int stage = SessionState.GetInt(Stage, 0);
                if (s == PlayModeStateChange.EnteredPlayMode && stage == 1)
                {
                    SessionState.SetInt(Stage, 2);
                    EditorApplication.delayCall += Play;
                }
                else if (s == PlayModeStateChange.EnteredEditMode && stage == 2)
                {
                    SessionState.SetInt(Stage, 0);
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
            };
        }

        public static void Run()
        {
            Directory.CreateDirectory(Raw);
            File.WriteAllText(Path.Combine(Out, "log.txt"), "");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
            SessionState.SetInt(Stage, 1);
            EditorApplication.EnterPlaymode();
        }

        static void Note(string s) => File.AppendAllText(Path.Combine(Out, "log.txt"), s + "\n");

        static string Only => Path.Combine(Out, "only.txt");

        static void Play()
        {
            string csv = Path.Combine(Application.persistentDataPath, "telemetry", "levels.csv");
            string progress = PlayerPrefs.HasKey("sfw.progress") ? PlayerPrefs.GetString("sfw.progress") : null;
            string csvBefore = File.Exists(csv) ? File.ReadAllText(csv) : null;
            var sits = Levels.All.Where(l => PlayerPrefs.HasKey("sfw.sit." + l.id)).ToDictionary(l => l.id, l => PlayerPrefs.GetString("sfw.sit." + l.id));
            float music = Settings.Music;
            var lang = Settings.Language;
            var diff = Settings.Difficulty;
            var only = File.Exists(Only) ? File.ReadAllLines(Only).Select(l => l.Trim()).Where(l => l.Length > 0).ToList() : null;
            if (Env("SFW_REC_SIZE") is string size)
            {
                var wh = size.Split('x');
                ClipRecorder.Width = int.Parse(wh[0]);
                ClipRecorder.Height = int.Parse(wh[1]);
            }
            ClipRecorder.UiAfterPost = Env("SFW_REC_CRISPUI") == "1";
            keep = Env("SFW_REC_KEEP") is string k ? new HashSet<string>(k.Split(',').Select(s => s.Trim())) : null;
            Note($"size {ClipRecorder.Width}x{ClipRecorder.Height}, ui after post {ClipRecorder.UiAfterPost}, into {Raw}, keep {(keep == null ? "all" : string.Join(" ", keep))}");
            try
            {
                EditorApplication.isPaused = true;
                Conversation.Speech.PretendMic = true;
                // SFW_REC_LANG=id or vi records the hints in Indonesian or Vietnamese (default English)
                Settings.Language = Env("SFW_REC_LANG") switch
                {
                    "id" => HintLanguage.Indonesian,
                    "vi" => HintLanguage.Vietnamese,
                    _ => HintLanguage.English,
                };
                Settings.Difficulty = Difficulty.Easy;
                // demo progress: a few parts done (stamps on the select screen, Logbooks open), the ARC in hand
                PlayerPrefs.SetString("sfw.progress", "done:clinic-1|done:immigration-1|done:airport-1|done:subway-1|doc:arc");
                PlayerPrefs.SetString("sfw.sit.clinic-1", "cold,uninsured");
                PlayerPrefs.Save();
                GameSession.I.SendMessage("LoadProgress", SendMessageOptions.DontRequireReceiver);
                PlaytestTools.Frames(120);
                Conversation.Speech.Warm();

                var takes = new (string id, System.Action act)[]
                {
                    ("home", HomeTake), ("select", SelectTake), ("clinic", ClinicChain), ("immigration", ImmigrationTake),
                    ("bank", BankTake), ("airport", AirportTake), ("subway", SubwayTake), ("emergency", EmergencyTake),
                    ("er", ErTake), ("logbook", LogbookTake), ("street", StreetShowTake), ("exit6", Exit6ShowTake),
                    ("phone", PhoneTake), ("subway2", SubwayFixTake), ("levels", LevelsTake), ("vr", VrTake), ("vrclick", VrClickTest),
                };
                foreach (var (id, act) in takes)
                {
                    if (only != null && !only.Contains(id)) continue;
                    Note("== " + id);
                    try { act(); }
                    catch (System.Exception e)
                    {
                        Note($"FAIL {id}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                        if (rec) End();
                    }
                    Time.timeScale = 1f;
                    // let a scene change that a take started finish before the next take loads its own
                    Fast(90);
                }
            }
            catch (System.Exception e) { Note("FATAL " + e); }
            finally
            {
                if (progress != null) PlayerPrefs.SetString("sfw.progress", progress);
                else PlayerPrefs.DeleteKey("sfw.progress");
                foreach (var l in Levels.All) PlayerPrefs.DeleteKey("sfw.sit." + l.id);
                foreach (var kv in sits) PlayerPrefs.SetString("sfw.sit." + kv.Key, kv.Value);
                Settings.Music = music;
                Settings.Language = lang;
                Settings.Difficulty = diff;
                PlayerPrefs.Save();
                if (csvBefore != null) File.WriteAllText(csv, csvBefore);
                else if (File.Exists(csv)) File.Delete(csv);
                if (File.Exists(Only)) File.Delete(Only);
                Time.timeScale = 1;
                EditorApplication.isPaused = false;
                EditorApplication.isPlaying = false;
            }
        }

        // ------------------------------------------------------------------ recording helpers

        static void Begin(string name)
        {
            // a lesson note left over from an earlier part must not ride along into this shot
            Overlay.I.ClearToasts();
            skipping = keep != null && !keep.Contains(name);
            ClipRecorder.Begin(skipping ? Path.Combine(Out, "_skip") : Path.Combine(Raw, name));
            rec = true;
            perFrame = () => HideNear(null, 0.95f);
            Note($"  rec {name}");
        }

        static void End()
        {
            int n = ClipRecorder.End();
            rec = false;
            perFrame = null;
            Unhide();
            Note($"  end ({n} frames{(skipping ? ", thrown away" : "")})");
            if (skipping && Directory.Exists(Path.Combine(Out, "_skip"))) Directory.Delete(Path.Combine(Out, "_skip"), true);
            skipping = false;
        }

        static System.Action perFrame;

        static void Step()
        {
            perFrame?.Invoke();
            VrPose();
            if (rec) ClipRecorder.Step();
            else Fast(1);
        }

        static void R(int frames)
        {
            for (int i = 0; i < frames; i++) Step();
        }

        static void Mark(string what) => Note($"    @{ClipRecorder.FrameCount} {what}");

        static void Fast(int frames) => PlaytestTools.Frames(frames);

        static FirstPersonController Me() => Object.FindAnyObjectByType<FirstPersonController>();

        static void HideHelp()
        {
            var hud = Object.FindAnyObjectByType<Hud>();
            if (hud) hud.HideHelp();
        }

        static readonly List<Renderer> hidden = new();

        /// <summary>Hides people right in front of the camera or between it and <paramref name="target"/> (this frame).</summary>
        static void HideNear(Vector3? target = null, float near = 1.6f, float lane = 0.7f)
        {
            Unhide();
            var me = Me();
            if (!me || !me.Camera) return;
            var cam = me.Camera.transform.position;
            foreach (var rig in Object.FindObjectsByType<World.HumanRig>(FindObjectsInactive.Exclude))
            {
                var p = rig.transform.position + Vector3.up * 1.0f;
                bool hide = Flat(p - cam).magnitude < near;
                if (!hide && target.HasValue)
                {
                    var a = Flat(cam);
                    var b = Flat(target.Value);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector3.Dot(Flat(p) - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                    hide = t > 0.02f && t < 0.92f && (Flat(p) - (a + ab * t)).magnitude < lane;
                }
                if (!hide) continue;
                foreach (var r in rig.GetComponentsInChildren<Renderer>())
                    if (r.enabled)
                    {
                        r.enabled = false;
                        hidden.Add(r);
                    }
            }
        }

        static void Unhide()
        {
            foreach (var r in hidden) if (r) r.enabled = true;
            hidden.Clear();
        }

        static void Load(string scene)
        {
            SceneFlow.Load(scene);
            for (int i = 0; i < 200 && SceneManager.GetActiveScene().name != scene; i++) Fast(1);
            PlaytestTools.WaitScene(scene);
        }

        static void StartPart(string id, params int[] situations)
        {
            PlaytestTools.Situations = situations;
            var level = Levels.Find(id);
            string scene = level.StartsInside ? level.scene : SceneFlow.Hub;
            for (int tries = 0; tries < 3 && SceneManager.GetActiveScene().name != scene; tries++)
            {
                SceneFlow.Play(level);
                for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != scene; i++) Fast(1);
            }
            PlaytestTools.WaitScene(scene);
        }

        // ------------------------------------------------------------------ showcase shots: a film camera, no HUD

        static Camera showCam, hiddenPlayerCam;
        static readonly List<Canvas> hiddenUi = new();

        /// <summary>Starts a showcase shot: its own camera (the player's look, post-processing on), the HUD hidden.</summary>
        static void ShowBegin(string name, float fov = 48f)
        {
            var me = Me();
            hiddenPlayerCam = me ? me.Camera : Camera.main;
            var go = new GameObject("ShowcaseCam") { tag = "MainCamera" };
            showCam = go.AddComponent<Camera>();
            if (hiddenPlayerCam) showCam.CopyFrom(hiddenPlayerCam);
            showCam.targetTexture = null;
            showCam.fieldOfView = fov;
            showCam.nearClipPlane = 0.05f;
            showCam.farClipPlane = 1500f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var src = hiddenPlayerCam ? hiddenPlayerCam.GetComponent<UniversalAdditionalCameraData>() : null;
            if (src) data.volumeLayerMask = src.volumeLayerMask;
            data.volumeTrigger = go.transform;
            if (hiddenPlayerCam) hiddenPlayerCam.enabled = false;
            var keep = Overlay.I ? Overlay.I.GetComponentInParent<Canvas>()?.rootCanvas : null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.isRootCanvas && c.enabled && c != keep && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.enabled = false;
                    hiddenUi.Add(c);
                }
            Begin(name);
            perFrame = null;
        }

        static void ShowEnd()
        {
            End();
            if (showCam) Object.DestroyImmediate(showCam.gameObject);
            if (hiddenPlayerCam) hiddenPlayerCam.enabled = true;
            foreach (var c in hiddenUi) if (c) c.enabled = true;
            hiddenUi.Clear();
        }

        static float Ease(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI);

        /// <summary>A dolly / crane move: position and aim eased from start to end (sine in-out), optional zoom.</summary>
        static void Dolly(Vector3 p0, Vector3 p1, Vector3 look0, Vector3 look1, float seconds, float fov0 = 48f, float fov1 = -1f)
        {
            if (fov1 < 0) fov1 = fov0;
            int n = Mathf.Max(2, Mathf.RoundToInt(seconds * 30));
            for (int i = 0; i < n; i++)
            {
                float e = Ease(i / (float)(n - 1));
                var pos = Vector3.Lerp(p0, p1, e);
                var look = Vector3.Lerp(look0, look1, e);
                showCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                showCam.fieldOfView = Mathf.Lerp(fov0, fov1, e);
                Step();
            }
        }

        /// <summary>Circles <paramref name="center"/> (degrees, 0 = +z), eased, aiming at it.</summary>
        static void Orbit(Vector3 center, float radius, float height, float a0, float a1, float seconds, float aimHeight = 0.5f, float fov = 46f)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(seconds * 30));
            showCam.fieldOfView = fov;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, Ease(i / (float)(n - 1))) * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius + Vector3.up * height;
                showCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(center + Vector3.up * aimHeight - pos));
                Step();
            }
        }

        static Vector3 Floor(Vector3 p) => new Vector3(p.x, Me() ? Me().transform.position.y : p.y, p.z);

        /// <summary>Showcase in a hall: pushes in from where the player stands while panning across <paramref name="focus"/>.</summary>
        static void HallShot(string name, Vector3 focus, float push = 4f, float pan = 5f, float seconds = 6f, float height = 2.3f)
        {
            var spawn = Me().transform.position;
            var dir = Flat(focus - spawn).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Me().transform.forward;
            var right = Vector3.Cross(Vector3.up, dir);
            float dist = Flat(focus - spawn).magnitude;
            push = Mathf.Min(push, Mathf.Max(0.5f, dist - 3f));
            ShowBegin(name, 54);
            Dolly(spawn - dir * 0.6f + Vector3.up * height, spawn + dir * push + Vector3.up * (height - 0.4f),
                focus - right * pan + Vector3.up * 0.2f, focus + right * pan * 0.7f, seconds, 56, 48);
            ShowEnd();
        }

        static Vector3? BoardOrCounter(ScenarioDirector d)
        {
            var board = (d.boards ?? new QueueBoard[0]).FirstOrDefault(b => b);
            if (board) return board.transform.position;
            var c = (d.counters ?? new CounterSpot[0]).FirstOrDefault(x => x) ?? d.counter;
            return c ? c.transform.position + Vector3.up * 1.4f : (Vector3?)null;
        }

        static Vector3 Mean(IEnumerable<Vector3> pts)
        {
            var list = pts.ToList();
            var sum = Vector3.zero;
            foreach (var p in list) sum += p;
            return list.Count > 0 ? sum / list.Count : Vector3.zero;
        }

        // ------------------------------------------------------------------ the camera: a player walking, filmed well

        static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly FieldInfo FYaw = typeof(FirstPersonController).GetField("yaw", Priv);
        static readonly FieldInfo FViewYaw = typeof(FirstPersonController).GetField("viewYaw", Priv);
        static readonly FieldInfo FPitch = typeof(FirstPersonController).GetField("pitch", Priv);
        static readonly FieldInfo FViewPitch = typeof(FirstPersonController).GetField("viewPitch", Priv);

        static float curYaw, curPitch, yawVel, pitchVel;

        /// <summary>Takes over the view from wherever the player looks now.</summary>
        static void GrabView()
        {
            var me = Me();
            curYaw = VR ? me.transform.eulerAngles.y : (float)FViewYaw.GetValue(me);
            curPitch = VR ? vrPitch : (float)FViewPitch.GetValue(me);
            yawVel = pitchVel = 0;
        }

        static void SetView(float yaw, float pitch)
        {
            var me = Me();
            FYaw.SetValue(me, yaw);
            FViewYaw.SetValue(me, yaw);
            FPitch.SetValue(me, pitch);
            FViewPitch.SetValue(me, pitch);
            me.transform.rotation = Quaternion.Euler(0, yaw, 0);
            // in the headset the body turns and the head nods (VrPose adds the head's own small drift)
            if (VR) vrPitch = Mathf.Clamp(pitch, -15f, 28f);
        }

        static Vector3 Eye() => VR && VRRig.Current ? VRRig.Current.Head.position : Me().transform.position + Vector3.up * 1.6f;

        /// <summary>Eases the view one frame towards <paramref name="point"/> (critically damped, like a calm head turn).</summary>
        static void LookToward(Vector3 point, float turnTime = 0.5f, float pitchBias = -2f)
        {
            var d = point - Eye();
            float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float wantPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg + pitchBias, -30f, 20f);
            curYaw = Mathf.SmoothDampAngle(curYaw, wantYaw, ref yawVel, turnTime, Mathf.Infinity, Dt);
            curPitch = Mathf.SmoothDamp(curPitch, wantPitch, ref pitchVel, turnTime * 1.2f, Mathf.Infinity, Dt);
            SetView(curYaw, curPitch);
        }

        /// <summary>Holds (recording) while the view settles on <paramref name="point"/>.</summary>
        static void Hold(Vector3 point, int frames, float turnTime = 0.6f, System.Func<Vector3> moving = null)
        {
            GrabView();
            for (int i = 0; i < frames; i++)
            {
                LookToward(moving != null ? moving() : point, turnTime);
                Step();
            }
        }

        /// <summary>Faces <paramref name="point"/> at once (between shots, never on camera).</summary>
        static void Face(Vector3 point)
        {
            var d = point - Eye();
            curYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            curPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg - 2f, -30f, 20f);
            yawVel = pitchVel = 0;
            SetView(curYaw, curPitch);
        }

        /// <summary>
        /// Walks the player along <paramref name="path"/> with the CharacterController (so steps, slopes, head bob and
        /// footsteps are the game's own): eases in, walks at <paramref name="speed"/>, eases out
        /// <paramref name="stopShort"/> metres before the end. The path is kept <paramref name="clearance"/> off the
        /// walkable edge where there is a navmesh and smoothed, so it never hugs a wall or cuts a corner. The view
        /// follows a point a few metres ahead on the path, or <paramref name="lookAt"/> (by distance walked) when it
        /// returns one; every turn is damped.
        /// </summary>
        static void Walk(List<Vector3> path, float speed, System.Func<float, float, Vector3?> lookAt = null, float stopShort = 0f,
            float clearance = 1.1f, System.Func<bool> stop = null, float turnTime = 0.55f)
        {
            var me = Me();
            var cc = me.GetComponent<CharacterController>();
            var pts = Shape(path, clearance);
            float total = Length(pts);
            float end = Mathf.Max(0.05f, total - stopShort);
            float s = 0, v = 0, accel = speed / 0.8f, decel = Mathf.Max(1.2f, speed * 0.9f);
            int blocked = 0;
            GrabView();
            for (int f = 0; f < 1500; f++)
            {
                if (stop != null && stop()) break;
                float remaining = end - s;
                if (remaining <= 0.01f) break;
                float target = remaining < decel ? Mathf.Max(0.25f, speed * Mathf.Sqrt(remaining / decel)) : speed;
                v = Mathf.MoveTowards(v, target, accel * Dt);
                s = Mathf.Min(end, s + v * Dt);
                var want = PointAt(pts, s);
                var before = me.transform.position;
                var delta = new Vector3(want.x - before.x, 0, want.z - before.z);
                cc.Move(delta);
                var moved = Flat(me.transform.position - before).magnitude;
                // a collider in the way (a gate flap a frame late, a bin): slip past it rather than stall on camera
                blocked = moved < delta.magnitude * 0.3f && delta.magnitude > 0.01f ? blocked + 1 : 0;
                if (blocked > 4)
                {
                    cc.enabled = false;
                    me.transform.position = new Vector3(want.x, me.transform.position.y, want.z);
                    cc.enabled = true;
                }
                var look = lookAt?.Invoke(s, end) ?? Ahead(pts, s, 4f) + Vector3.up * 1.5f;
                LookToward(look, turnTime);
                Step();
            }
        }

        /// <summary>A point <paramref name="ahead"/> metres on along the path (continuing straight past its end).</summary>
        static Vector3 Ahead(List<Vector3> pts, float s, float ahead)
        {
            float total = Length(pts);
            if (s + ahead <= total) return PointAt(pts, s + ahead);
            var last = pts[pts.Count - 1];
            var dir = Flat(last - PointAt(pts, Mathf.Max(0, total - 1f)));
            if (dir.sqrMagnitude < 0.0001f) dir = Me().transform.forward;
            return last + dir.normalized * (s + ahead - total);
        }

        /// <summary>Resamples, pushes off the navmesh edge and smooths a path; the end stays where it was.</summary>
        static List<Vector3> Shape(List<Vector3> path, float clearance)
        {
            var dense = new List<Vector3> { path[0] };
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Flat(path[i] - path[i - 1]).magnitude;
                int n = Mathf.Max(1, Mathf.CeilToInt(seg / 0.25f));
                for (int k = 1; k <= n; k++) dense.Add(Vector3.Lerp(path[i - 1], path[i], k / (float)n));
            }
            bool nav = NavMesh.SamplePosition(dense[0], out _, 3f, NavMesh.AllAreas);
            if (nav && clearance > 0)
                for (int i = 1; i < dense.Count - 1; i++)
                    for (int it = 0; it < 3; it++)
                    {
                        if (!NavMesh.FindClosestEdge(dense[i], out var edge, NavMesh.AllAreas)) break;
                        if (edge.distance >= clearance) break;
                        var away = Flat(dense[i] - edge.position);
                        if (away.sqrMagnitude < 0.0001f) away = Flat(edge.normal);
                        var moved = dense[i] + away.normalized * (clearance - edge.distance) * 0.8f;
                        if (!NavMesh.SamplePosition(moved, out var on, 0.6f, NavMesh.AllAreas)) break;
                        dense[i] = on.position;
                    }
            // moving average over about two metres, ends pinned
            var smooth = new List<Vector3>(dense);
            int w = 4;
            for (int pass = 0; pass < 2; pass++)
            {
                var src = new List<Vector3>(smooth);
                for (int i = 1; i < src.Count - 1; i++)
                {
                    int a = Mathf.Max(0, i - w), b = Mathf.Min(src.Count - 1, i + w);
                    var sum = Vector3.zero;
                    for (int k = a; k <= b; k++) sum += src[k];
                    smooth[i] = sum / (b - a + 1);
                }
            }
            return smooth;
        }

        static float Length(List<Vector3> pts)
        {
            float total = 0;
            for (int i = 1; i < pts.Count; i++) total += Flat(pts[i] - pts[i - 1]).magnitude;
            return total;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        static Vector3 PointAt(IList<Vector3> pts, float dist)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Flat(pts[i] - pts[i - 1]).magnitude;
                if (dist <= seg) return Vector3.Lerp(pts[i - 1], pts[i], seg > 0 ? dist / seg : 0);
                dist -= seg;
            }
            return pts[pts.Count - 1];
        }

        static List<Vector3> NavPath(Vector3 from, Vector3 to)
        {
            if (NavMesh.SamplePosition(from, out var a, 4f, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out var b, 4f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
                    return path.corners.ToList();
            }
            return new List<Vector3> { from, to };
        }

        /// <summary>Puts the player <paramref name="back"/> metres from a point, facing it (between shots).</summary>
        static void StandBefore(Vector3 target, float back, Vector3? from = null)
        {
            var player = Me();
            var p = from ?? player.transform.position;
            var dir = Flat(p - target);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            var at = target + dir.normalized * back;
            at.y = player.transform.position.y;
            var look = Flat(target - at);
            player.Teleport(at, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        }

        static Waypoint Wp(ScenarioDirector d, string id, Vector3? near = null)
        {
            var list = (d.waypoints ?? new Waypoint[0]).Where(w => w && w.stationId == id).ToList();
            var p = near ?? Me().transform.position;
            return list.OrderBy(w => Flat(w.transform.position - p).sqrMagnitude).FirstOrDefault();
        }

        /// <summary>Plays the part on (not recording) until <paramref name="until"/> holds: right answers, errands, PINs.</summary>
        static bool Advance(System.Func<bool> until, int max = 20000, bool fast = true)
        {
            var d = PlaytestTools.Director();
            if (fast) Time.timeScale = 4f;
            try
            {
                for (int f = 0; f < max && !d.Finished; f++)
                {
                    if (until()) return true;
                    foreach (var st in d.stations ?? new FetchStation[0])
                        if (st && st.Armed) st.Complete();
                    if (d.ticketMachine && d.ticketMachine.Armed) d.ticketMachine.Interact(null);
                    if (d.counter && d.counter.Armed) d.counter.Interact(null);
                    if (!string.IsNullOrEmpty(d.CurrentTarget))
                    {
                        PlaytestTools.CompleteTarget(d);
                        Fast(2);
                        continue;
                    }
                    foreach (var room in d.rooms ?? new RoomEntrance[0])
                        if (room && room.Armed) room.Interact(null);
                    var p = PlaytestTools.Panel();
                    if (p && p.IsWaiting)
                    {
                        if (p.WaitingForPin) p.EnterPin(string.IsNullOrEmpty(d.CurrentExpect) ? "2580" : d.CurrentExpect);
                        else p.Pick(RightIndex(d));
                        Fast(3);
                        continue;
                    }
                    Fast(1);
                }
                return until();
            }
            finally { Time.timeScale = 1f; }
        }

        static int RightIndex(ScenarioDirector d)
        {
            var shown = d.ShownChoices;
            int pick = -1;
            foreach (var want in new[] { "advance", "fetch" })
                if (pick < 0) pick = shown.FindIndex(c => c.result == want);
            return Mathf.Max(0, pick);
        }

        static bool WaitInput(int max = 900)
        {
            for (int i = 0; i < max; i++)
            {
                var p = PlaytestTools.Panel();
                if (p && p.IsWaiting) return true;
                Step();
            }
            return false;
        }

        static string Transcribe(AudioClip clip, string prompt)
        {
            var t = Conversation.Speech.TranscribeAsync(clip, prompt);
            for (int i = 0; i < 4000 && !t.IsCompleted; i++) Fast(1);
            return t.IsCompleted && !t.IsFaulted ? t.Result : null;
        }

        /// <summary>Says the right answer out loud: SPACE held (listening), the student's voice, then the pick with what was heard.</summary>
        static void SayRight(int afterFrames = 90) => SayChoice(RightIndex(PlaytestTools.Director()), afterFrames);

        /// <summary>Says one of the answers on screen out loud (right or wrong), the way <see cref="SayRight"/> does.</summary>
        static void SayChoice(int index, int afterFrames = 90)
        {
            var d = PlaytestTools.Director();
            var p = PlaytestTools.Panel();
            var right = d.ShownChoices[index];
            var clip = Conversation.BakedVoice.Load("player", right.ko, false);
            var prompt = string.Join(" ", d.ShownChoices.Select(c => c.ko).Where(Conversation.KoreanMatch.Speakable));
            ClipRecorder.Pause();
            var heard = clip ? Transcribe(clip, prompt) : right.ko;
            if (string.IsNullOrWhiteSpace(heard)) heard = right.ko;
            ClipRecorder.Resume();
            Mark($"listening ({right.ko} heard {heard})");
            var label = typeof(ConversationPanel).GetField("speakLabel", Priv)?.GetValue(p) as UnityEngine.UI.Text;
            var ring = typeof(ConversationPanel).GetField("speakRing", Priv)?.GetValue(p) as InkShape;
            string idle = label ? label.text : null;
            if (label) label.text = Loc.T("listening");
            if (ring)
            {
                ring.gameObject.SetActive(true);
                _ = SeoulFirstWeek.UI.Motion.Draw(ring, 0.2f);
            }
            R(8);
            if (clip) Audio.Sfx.Play(clip, 1f / Mathf.Max(0.05f, Settings.Sfx));
            R(Mathf.RoundToInt((clip ? clip.length : 1.2f) * 30) + 8);
            if (label) label.text = "…";
            R(10);
            if (label) label.text = idle;
            if (ring) ring.gameObject.SetActive(false);
            Mark("heard");
            p.Heard(heard);
            R(afterFrames);
        }

        static void PressKeys(string keys, int gap = 9)
        {
            var p = PlaytestTools.Panel();
            var m = typeof(ConversationPanel).GetMethod("KeypadPress", Priv);
            foreach (var k in keys)
            {
                m.Invoke(p, new object[] { k.ToString() });
                R(gap);
            }
        }

        // ------------------------------------------------------------------ VR: the headset's view (SFW_VR_SIM=1)
        // The game runs as it does in a headset (panels, the laser, the controller's button names, no screen-fixed
        // patterns), but nothing tracks a head: the body turns with SetView, the head nods (vrPitch) and drifts a
        // little, the right hand points at vrAim and the left one rests low. Each frame is one eye's view, 16:9.

        static bool VR => Core.VRMode.Simulated;
        const float VrFov = 58f;
        static float vrPitch;
        static Vector3? vrAim;
        static bool vrAimUi, vrAimSet;
        static Vector3 vrAimAt, vrAimVel;
        static int vrFrame;
        static VRRig vrRigSeen;
        static InkHover vrHover;

        static void VrPose()
        {
            if (!VR) return;
            vrFrame++;
            float t = vrFrame * Dt;
            var rig = VRRig.Current;
            var head = rig ? rig.Head : (VRUI.Head ? VRUI.Head.transform : null);
            if (!head) return;
            var cam = head.GetComponent<Camera>();
            if (cam) cam.fieldOfView = VrFov;
            // a real head is never quite still: a slow drift of a degree or less
            float nx = (Mathf.PerlinNoise(t * 0.31f, 1.7f) - 0.5f) * 1.2f;
            float ny = (Mathf.PerlinNoise(4.2f, t * 0.27f) - 0.5f) * 1.6f;
            float nz = (Mathf.PerlinNoise(t * 0.22f, 9.1f) - 0.5f) * 0.9f;
            head.localRotation = Quaternion.Euler((rig ? vrPitch : 0f) + nx, ny, nz);
            // the right hand: on the rig's grip, or on a menu where a hand would be in the tracking space
            var space = rig ? rig.Pivot : head.parent;
            if (!space) return;
            if (rig != vrRigSeen)
            {
                vrRigSeen = rig;
                vrAimSet = false;
            }

            Vector3 Sway(float seed) => new Vector3(Mathf.PerlinNoise(t * 0.6f, seed) - 0.5f,
                Mathf.PerlinNoise(seed, t * 0.5f) - 0.5f, Mathf.PerlinNoise(t * 0.4f, seed + 3f) - 0.5f) * 0.012f;
            var grip = space.TransformPoint(new Vector3(0.16f, -0.22f, 0.40f) + Sway(5.3f));
            // held low, pointing ahead, unless it points at something
            var want = vrAim ?? grip + space.rotation * new Vector3(0.05f, -0.45f, 1f).normalized * 3f;
            if (!vrAimSet)
            {
                vrAimAt = want;
                vrAimVel = Vector3.zero;
                vrAimSet = true;
            }
            vrAimAt = Vector3.SmoothDamp(vrAimAt, want, ref vrAimVel, 0.14f, Mathf.Infinity, Dt);
            if (!rig)
            {
                VRUI.SimulatedPointer = vrAim.HasValue ? new Ray(grip, (vrAimAt - grip).normalized) : (Ray?)null;
                VRUI.SimulatedPointerEnd = vrAim.HasValue ? vrAimAt : (Vector3?)null;
                return;
            }
            VRUI.SimulatedPointer = null;
            rig.LeftGrip.localPosition = new Vector3(-0.2f, -0.27f, 0.36f) + Sway(2.1f);
            rig.LeftGrip.localRotation = Quaternion.Euler(30f, 14f, -10f);
            var look = Quaternion.LookRotation(vrAimAt - grip, space.up);
            rig.RightGrip.SetPositionAndRotation(grip, look);
            var tip = grip + look * new Vector3(0, 0.01f, 0.05f);
            rig.RightAim.SetPositionAndRotation(tip, Quaternion.LookRotation(vrAimAt - tip, space.up));
            VRRig.SimulatedHand = true;
            VRRig.SimulatedLaserEnd = vrAim.HasValue && vrAimUi ? vrAimAt : (Vector3?)null;
        }

        /// <summary>Points at the world-space label that contains <paramref name="text"/> (a menu's button).</summary>
        static void AimText(string text)
        {
            foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                if (t.text != null && t.text.Contains(text) && t.canvas && t.canvas.renderMode == RenderMode.WorldSpace)
                {
                    var r = t.rectTransform;
                    vrAim = r.TransformPoint(r.rect.center);
                    vrAimUi = true;
                    return;
                }
        }

        /// <summary>The face of the staff member nearest <paramref name="near"/>: the top of the figure, less half a head.</summary>
        static Vector3 StaffFace(Vector3 near, string staffId = null)
        {
            NpcActor best = null;
            float bestD = float.MaxValue;
            foreach (var n in Object.FindObjectsByType<NpcActor>(FindObjectsInactive.Exclude))
            {
                if (staffId != null && n.staffId != staffId) continue;
                float dd = Flat(n.transform.position - near).sqrMagnitude;
                if (dd < bestD)
                {
                    bestD = dd;
                    best = n;
                }
            }
            if (!best) return near + Vector3.up * 1.3f;
            var rs = best.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (rs.Length == 0) return best.transform.position + Vector3.up * 1.3f;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.max.y - 0.14f, b.center.z);
        }

        /// <summary>A point on the panels (they all hang in one place): u, v from the bottom left of the 1920 × 1080 layout.</summary>
        static Vector3? PanelPoint(float u, float v)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!c.isRootCanvas || !c.enabled || c.renderMode != RenderMode.WorldSpace || c.gameObject.layer != VRUI.Layer) continue;
                var rt = (RectTransform)c.transform;
                if (Mathf.Abs(rt.sizeDelta.x - 1920f) > 1f) continue;
                return rt.TransformPoint(new Vector3((u - 0.5f) * 1920f, (v - 0.5f) * 1080f, 0));
            }
            return null;
        }

        static void AimPanel(float u, float v)
        {
            vrAim = PanelPoint(u, v);
            vrAimUi = vrAim.HasValue;
        }

        static void AimWorld(Vector3 p)
        {
            vrAim = p;
            vrAimUi = false;
        }

        static void AimDown()
        {
            vrAim = null;
            vrAimUi = false;
            Hover(null);
        }

        static readonly FieldInfo FAnswers = typeof(ConversationPanel).GetField("answers", Priv);

        static RectTransform AnswerRoot(int index)
        {
            var p = PlaytestTools.Panel();
            if (!p || !(FAnswers?.GetValue(p) is System.Collections.IList list) || index < 0 || index >= list.Count) return null;
            var a = list[index];
            return a?.GetType().GetField("Root")?.GetValue(a) as RectTransform;
        }

        static void Hover(InkHover h)
        {
            if (h == vrHover) return;
            if (vrHover) vrHover.OnPointerExit(null);
            vrHover = h;
            if (h) h.OnPointerEnter(null);
        }

        /// <summary>Points the laser at an answer, which lights up as under a pointer, for <paramref name="frames"/> frames.</summary>
        static void PointAnswer(int index, int frames)
        {
            var root = AnswerRoot(index);
            if (root)
            {
                // a little left of the bar's middle, on the Korean line
                var r = root.rect;
                vrAim = root.TransformPoint(new Vector3(r.center.x - r.width * 0.18f, r.center.y, 0));
                vrAimUi = true;
                Hover(root.GetComponent<InkHover>());
            }
            R(frames);
        }

        /// <summary>Runs the laser over the other answers, then rests it on <paramref name="pick"/>.</summary>
        static void SweepAnswers(int pick)
        {
            var d = PlaytestTools.Director();
            for (int i = 0; i < d.ShownChoices.Count; i++)
                if (i != pick) PointAnswer(i, 14);
            PointAnswer(pick, 26);
        }

        /// <summary>Pulls the trigger on an answer that is an action ([brackets]): the bar presses down, then the pick.</summary>
        static void TriggerAnswer(int index, int afterFrames)
        {
            PointAnswer(index, 24);
            var root = AnswerRoot(index);
            var h = root ? root.GetComponent<InkHover>() : null;
            if (h) h.OnPointerDown(null);
            R(5);
            if (h) h.OnPointerUp(null);
            Mark("trigger");
            PlaytestTools.Panel().Pick(index);
            Hover(null);
            R(afterFrames);
        }

        static void VrTake()
        {
            if (!VR)
            {
                Note("  vr: start the recorder with SFW_VR_SIM=1");
                return;
            }
            var up = Vector3.up;
            vrPitch = 0;
            AimDown();

            // the home screen in the headset: the street at eye height, the menu as a panel in front, the laser on Start
            Begin("V01_home");
            R(45);
            AimText("시작");
            R(120);
            End();
            AimDown();

            // the story card on the street, its questions answered with the trigger
            PlaytestTools.Situations = new[] { 0, 0 };
            SceneFlow.Play(Levels.Find("clinic-1"));
            for (int i = 0; i < 400 && !StoryCard.Current; i++) Fast(1);
            Fast(20);
            Begin("V02_story");
            R(30);
            for (int g = 0; g < 4 && StoryCard.Current && StoryCard.Current.Picking; g++)
            {
                AimPanel(0.40f, 0.29f);
                R(45);
                Mark("pick");
                StoryCard.Current.Pick(0);
                R(30);
            }
            AimPanel(0.52f, 0.45f);
            R(100);
            if (StoryCard.Current) StoryCard.Current.Begin();
            AimDown();
            R(45);
            End();

            // down 명동길 on the left stick, then the laser on the clinic's door and the trigger
            var door = Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Exclude).First(e => e.entranceId == "clinic");
            var target = door.exitPoint ? door.exitPoint.position : door.transform.position;
            var sign = door.transform.position + up * 2.6f;
            var pts = NavPath(Me().transform.position, target);
            float pathLen = Length(pts);
            if (pathLen > 18f)
            {
                var start = PointAt(pts, pathLen - 16f);
                Me().Teleport(start + up * 0.05f, 0);
                Fast(5);
                pts = NavPath(Me().transform.position, target);
                Face(Ahead(Shape(pts, 1.6f), 0, 4f) + up * 1.5f);
                Fast(3);
            }
            Begin("V03_street");
            Walk(pts, 1.6f, (s, end) => end - s < 8f ? sign : (Vector3?)null, 3.0f, 1.6f);
            Hold(sign, 20);
            AimWorld(door.transform.position + up * 1.1f);
            Hold(door.transform.position + up * 1.5f, 40);
            Mark("trigger");
            door.Interact(null);
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != SceneFlow.Clinic; i++) Step();
            AimDown();
            R(40);
            End();

            // the reception: walk up, the trigger on the counter, then answers said out loud (A held)
            var d = PlaytestTools.Director();
            Begin("V04_clinic");
            for (int i = 0; i < 120 && !(d.counter && d.counter.Armed); i++) Step();
            R(10);
            if (d.counter)
            {
                var c = d.counter.transform.position;
                var nurse = StaffFace(c);
                var from = Me().transform.position;
                var stop = c + Flat(from - c).normalized * 1.3f;
                Walk(new List<Vector3> { from, stop }, 1.3f, (s, end) => nurse, 0f, 0f, null, 0.7f);
                Hold(nurse, 12);
                AimWorld(nurse - up * 0.45f);
                Hold(nurse, 26);
                Mark("trigger");
                if (d.counter.Armed) d.counter.Interact(null);
            }
            AimDown();
            WaitInput();
            Mark("answers");
            R(40);
            int right = RightIndex(d);
            SweepAnswers(right);
            SayChoice(right, 80);
            WaitInput();
            Mark("answers 2");
            R(36);
            int wrong = d.ShownChoices.FindIndex(ch => ch.result == "retry" && Conversation.KoreanMatch.Speakable(ch.ko));
            if (wrong >= 0)
            {
                SweepAnswers(wrong);
                Mark("wrong");
                SayChoice(wrong, 20);
                AimDown();
                R(150);
            }
            AimDown();
            End();

            // the doctor: seated across the desk; "open your mouth", then an action picked with the trigger
            bool AtDoctor()
            {
                var p = PlaytestTools.Panel();
                return p && p.IsWaiting && d.ShownChoices != null &&
                       d.ShownChoices.Any(ch => ch.ko != null && (ch.ko.StartsWith("알레르기") || ch.ko.StartsWith("아~")));
            }
            if (Advance(AtDoctor, 30000))
            {
                var face = StaffFace(Eye(), "doctor");
                Face(face);
                Fast(2);
                Begin("V05_doctor");
                Hold(face, 30);
                for (int k = 0; k < 2; k++)
                {
                    var p = PlaytestTools.Panel();
                    if (!p || !p.IsWaiting) break;
                    int pick = RightIndex(d);
                    var choice = d.ShownChoices[pick];
                    if (!Conversation.KoreanMatch.Speakable(choice.ko))
                    {
                        // an action: show the realistic mistake first if there is one, then the right one
                        int slip = d.ShownChoices.FindIndex(ch => ch.result == "retry" && !Conversation.KoreanMatch.Speakable(ch.ko));
                        if (slip >= 0)
                        {
                            TriggerAnswer(slip, 150);
                            if (!WaitInput()) break;
                            R(20);
                            pick = RightIndex(d);
                        }
                        TriggerAnswer(pick, 90);
                        break;
                    }
                    SweepAnswers(pick);
                    SayChoice(pick, 30);
                    AimDown();
                    if (k == 1) break;
                    if (!WaitInput(600)) break;
                    R(30);
                }
                AimDown();
                R(110);
                End();
            }

            // the results, as a panel over the room
            Advance(() => SceneManager.GetActiveScene().name == SceneFlow.Results || d == null || d.Finished, 30000);
            for (int i = 0; i < 900 && SceneManager.GetActiveScene().name != SceneFlow.Results; i++) Fast(1);
            Fast(10);
            Begin("V06_results");
            R(70);
            AimText("Scenarios");
            R(170);
            End();
            AimDown();
            VRRig.SimulatedHand = false;
            VRRig.SimulatedLaserEnd = null;
        }

        /// <summary>Checks the panels' pointer in a simulated headset: a trigger pull on Home starts, one on a part card opens it.</summary>
        static void VrClickTest()
        {
            if (!VR)
            {
                Note("  vrclick: start the recorder with SFW_VR_SIM=1");
                return;
            }
            void Pull(Vector3 at)
            {
                var head = VRUI.Head ? VRUI.Head.transform.position : Vector3.zero;
                var from = head + Vector3.down * 0.25f + Vector3.right * 0.1f;
                VRUI.SimulatedPointer = new Ray(from, (at - from).normalized);
                VRUI.SimulatedTrigger = false;
                Fast(6);
                VRUI.SimulatedTrigger = true;
                Fast(4);
                VRUI.SimulatedTrigger = false;
                Fast(4);
            }
            Load(SceneFlow.Home);
            Fast(90);
            var p = PanelPoint(0.5f, 0.45f);
            Note($"  vrclick: home panel point {(p.HasValue ? "found" : "missing")}, over UI before pull {VRUI.PointerOverUI}");
            if (p.HasValue) Pull(p.Value);
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != SceneFlow.Select; i++) Fast(1);
            Note($"  vrclick: after the pull on Home the scene is {SceneManager.GetActiveScene().name}");
            Fast(90);
            Vector3? card = null;
            foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                if (t.text != null && t.text.Contains("See a doctor") && t.canvas && t.canvas.renderMode == RenderMode.WorldSpace)
                {
                    var r = t.rectTransform;
                    card = r.TransformPoint(r.rect.center);
                    break;
                }
            Note($"  vrclick: part card {(card.HasValue ? "found" : "missing")}");
            if (card.HasValue) Pull(card.Value);
            for (int i = 0; i < 400 && SceneManager.GetActiveScene().name == SceneFlow.Select; i++) Fast(1);
            Note($"  vrclick: after the pull on the card the scene is {SceneManager.GetActiveScene().name}, story card {(StoryCard.Current ? "up" : "none")}");
            VRUI.SimulatedPointer = null;
        }

        // ------------------------------------------------------------------ takes

        static void HomeTake()
        {
            Load(SceneFlow.Select);
            Fast(30);
            Begin("01_home");
            SceneFlow.Load(SceneFlow.Home);
            R(330);
            End();
        }

        static void SelectTake()
        {
            Begin("02_select");
            SceneFlow.Load(SceneFlow.Select);
            R(210);
            var sel = Object.FindAnyObjectByType<LevelSelectScreen>();
            Mark("page 2");
            if (sel) sel.SendMessage("GoTo", 1, SendMessageOptions.DontRequireReceiver);
            R(120);
            End();
        }

        static void ClinicChain()
        {
            // the story card on the street: the two questions, then the story
            PlaytestTools.Situations = new[] { 0, 0 };
            Begin("03_story");
            SceneFlow.Play(Levels.Find("clinic-1"));
            for (int i = 0; i < 400 && !StoryCard.Current; i++) Step();
            R(60);
            for (int g = 0; g < 4 && StoryCard.Current && StoryCard.Current.Picking; g++)
            {
                Mark("question");
                R(45);
                StoryCard.Current.Pick(0);
                R(30);
            }
            Mark("story");
            R(110);
            if (StoryCard.Current) StoryCard.Current.Begin();
            R(45);
            End();
            HideHelp();

            var door = Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Exclude).First(e => e.entranceId == "clinic");
            var target = door.exitPoint ? door.exitPoint.position : door.transform.position;
            var sign = door.transform.position + Vector3.up * 2.6f;
            var up = Vector3.up;

            // showcase: a crane down into 명동길, then a slow truck along the shop fronts
            {
                var a = Floor(door.approach ? door.approach.position : Me().transform.position);
                var axis = Flat(door.transform.position - a).normalized;
                var side = Vector3.Cross(Vector3.up, axis);
                float sideSign = Mathf.Sign(Vector3.Dot(door.transform.position - a, side));
                ShowBegin("S01_street_crane", 52);
                Dolly(a - axis * 8f + up * 18f, a + axis * 3f + up * 2.3f, a + axis * 34f + up * 1f, a + axis * 30f + up * 2.4f, 6.5f, 54, 46);
                ShowEnd();
                var m = a + axis * 12f;
                ShowBegin("S02_street_truck", 40);
                Dolly(m - side * sideSign * 1.2f + up * 1.7f, m + axis * 8f - side * sideSign * 1.2f + up * 1.7f,
                    m + axis * 3f + side * sideSign * 8f + up * 3.0f, m + axis * 11f + side * sideSign * 8f + up * 3.0f, 6f, 42);
                ShowEnd();
            }

            // the walk down 명동길 to the clinic, looking along the street, then up at the clinic's sign
            var pts = NavPath(Me().transform.position, target);
            float pathLen = Length(pts);
            // start the shot about 22 m out: the rest of the walk is the showcase's job
            if (pathLen > 24f)
            {
                var start = PointAt(pts, pathLen - 22f);
                Me().Teleport(start + Vector3.up * 0.05f, 0);
                Fast(5);
                pts = NavPath(Me().transform.position, target);
                Face(Ahead(Shape(pts, 1.6f), 0, 4f) + up * 1.5f);
                Fast(3);
            }
            Begin("04_street");
            Walk(pts, 1.9f, (s, end) => end - s < 9f ? sign : (Vector3?)null, 2.6f, 1.6f);
            Hold(sign, 20);
            Mark("enter");
            door.Interact(null);
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != SceneFlow.Clinic; i++) Step();
            R(40);
            HideHelp();
            Mark("inside");
            R(20);
            End();

            // showcase: a slow dolly across the waiting room to the reception
            var d = PlaytestTools.Director();
            var desk = (d.counters ?? new CounterSpot[0]).FirstOrDefault(c => c) ?? d.counter;
            if (desk)
            {
                var spawn = Me().transform.position;
                var c = desk.transform.position;
                var dir = Flat(c - spawn).normalized;
                var right = Vector3.Cross(Vector3.up, dir);
                float dist = Flat(c - spawn).magnitude;
                ShowBegin("S03_clinic_dolly", 50);
                Dolly(spawn - dir * 0.6f + up * 2.2f, spawn + dir * Mathf.Max(1f, dist - 4.5f) + up * 1.75f,
                    c - right * 2.5f + up * 1.3f, c + right * 0.6f + up * 1.4f, 6f, 54, 44);
                ShowEnd();
            }

            // up to the reception, eyes on the nurse
            Begin("04b_clinic_walk");
            for (int i = 0; i < 120 && !(d.counter && d.counter.Armed); i++) Step();
            R(10);
            if (d.counter)
            {
                var c = d.counter.transform.position;
                var from = Me().transform.position;
                var stop = c + Flat(from - c).normalized * 1.3f;
                Walk(new List<Vector3> { from, stop }, 1.4f, (s, end) => c + Vector3.up * 1.35f, 0f, 0f, null, 0.7f);
                Hold(c + Vector3.up * 1.35f, 12);
            }
            End();

            Begin("05_clinic_talk");
            if (d.counter && d.counter.Armed) d.counter.Interact(null);
            WaitInput();
            Mark("answers");
            R(75);
            SayRight(100);
            // a mistake: the nurse's reply and the lesson
            WaitInput();
            Mark("answers 2");
            R(45);
            var p = PlaytestTools.Panel();
            int wrong = d.ShownChoices.FindIndex(ch => ch.result == "retry");
            if (wrong >= 0)
            {
                Mark("wrong");
                p.Pick(wrong);
                R(170);
            }
            End();

            // the rest of the visit, then the results
            Advance(() => SceneManager.GetActiveScene().name == SceneFlow.Results || d == null || d.Finished, 30000);
            for (int i = 0; i < 900 && SceneManager.GetActiveScene().name != SceneFlow.Results; i++) Fast(1);
            Begin("06_results");
            R(270);
            End();
        }

        static void ImmigrationTake()
        {
            PlaytestTools.StartLevel("immigration-1");
            if (StoryCard.Current) PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            var d = PlaytestTools.Director();
            var hallFocus = BoardOrCounter(d);
            if (hallFocus.HasValue) HallShot("S04_imm_hall", hallFocus.Value, 4.5f, 6f, 6.5f);
            Begin("07_imm_ticket");
            if (d.ticketMachine)
            {
                var m = d.ticketMachine.transform.position;
                var from = Me().transform.position;
                R(15);
                Walk(new List<Vector3> { from, m + Flat(from - m).normalized * 1.1f }, 1.4f,
                    (s, end) => end - s < 3f ? m + Vector3.up * 1.2f : (Vector3?)null, 0f, 0f);
                Hold(m + Vector3.up * 1.2f, 15);
                for (int i = 0; i < 120 && !d.ticketMachine.Armed; i++) Step();
                Mark("ticket");
                d.ticketMachine.Interact(null);
                R(110);
            }
            End();
            // to the counter (the queue fast-forwarded), then a hand-over
            Time.timeScale = 4f;
            for (int i = 0; i < 4000 && !(d.counter && d.counter.Armed); i++)
            {
                PlaytestTools.CompleteTarget(d);
                Fast(1);
            }
            Time.timeScale = 1f;
            Begin("08_imm_counter");
            if (d.counter && d.counter.Armed) d.counter.Interact(null);
            WaitInput();
            R(50);
            SayRight(70);
            End();
            Advance(() =>
            {
                var p = PlaytestTools.Panel();
                return p && p.IsWaiting && !p.WaitingForPin && d.ShownChoices.Any(c => c.result == "advance" && !string.IsNullOrEmpty(c.give));
            });
            Begin("09_imm_handover");
            R(60);
            SayRight(100);
            End();
        }

        static void BankTake()
        {
            PlaytestTools.StartLevel("bank-1");
            if (StoryCard.Current) PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            var bankFocus = BoardOrCounter(PlaytestTools.Director());
            if (bankFocus.HasValue) HallShot("S05_bank_hall", bankFocus.Value, 4f, 5f, 6f);
            if (!Advance(() => PlaytestTools.Panel() && PlaytestTools.Panel().WaitingForPin)) { Note("  no PIN pad"); return; }
            Begin("10_bank_pin");
            R(30);
            PressKeys("2580");
            R(40);
            End();
        }

        static void AirportTake()
        {
            StartPart("airport-1", 0);
            for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
            PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            var d = PlaytestTools.Director();
            var lane = Wp(d, "lane_foreign");
            if (lane) HallShot("S06_airport_hall", lane.transform.position + Vector3.up * 1.2f, 5f, 6f, 6.5f, 2.8f);
            Begin("11_airport_lanes");
            if (lane)
            {
                var laneAt = lane.transform.position;
                perFrame = () => HideNear(laneAt, 1.9f, 0.8f);
                var from = Me().transform.position;
                R(10);
                // walk up towards the lanes, glancing up at the arrival steps sign and then the lane itself
                Walk(new List<Vector3> { from, from + Flat(laneAt - from) * 0.6f }, 1.5f,
                    (s, end) => s < end * 0.45f ? (Vector3?)null : laneAt + Vector3.up * 1.6f, 0f, 0f);
            }
            R(15);
            End();
            Advance(() => d.CurrentTarget == "carousel");
            var bag = Wp(d, "carousel");
            if (!bag) { Note("  no bag"); return; }
            // showcase: round the carousel on the hall side while the bags ride
            var belt = Object.FindAnyObjectByType<World.CarouselBelt>();
            if (belt && belt.loop != null && belt.bags != null && belt.bags.Length > 0 && belt.bags[0])
            {
                var parent = belt.bags[0].parent;
                var center = Mean(belt.loop.Select(p => parent ? parent.TransformPoint(p) : p));
                center.y = Me().transform.position.y;
                var toHall = lane ? Flat(lane.transform.position - center).normalized : Vector3.right;
                float baseAngle = Mathf.Atan2(toHall.x, toHall.z) * Mathf.Rad2Deg;
                ShowBegin("S07_carousel_orbit", 46);
                Orbit(center, 3.6f, 1.9f, baseAngle - 55f, baseAngle + 45f, 7f, 0.5f, 48f);
                ShowEnd();
            }
            // stand by the belt, a few metres off the suitcase as it comes round
            StandBefore(bag.transform.position, 3.3f);
            Face(bag.transform.position);
            Fast(10);
            Begin("12_airport_carousel");
            perFrame = () => HideNear(bag.transform.position, 1.5f, 1.0f);
            Hold(bag.transform.position, 150, 0.8f, () => bag.transform.position + Vector3.up * 0.2f);
            Mark("take bag");
            bag.Complete();
            R(80);
            End();
        }

        static void SubwayTake()
        {
            StartPart("subway-1", 0);
            for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
            PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            // down to the end of 명동길 and round the corner to exit 6: the canopy, then down into the stairs
            var exit = Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Exclude).FirstOrDefault(e => e.walkIn && e.Open);
            if (exit)
            {
                var exitAt = exit.transform.position;
                var pts = NavPath(Me().transform.position, exitAt);
                float total = Length(pts);
                {
                    // showcase: a crane down onto exit 6, ending on the stairs going under the street
                    var back = Flat(PointAt(pts, Mathf.Max(0, total - 6f)) - exitAt).normalized;
                    if (back.sqrMagnitude < 0.01f) back = Vector3.forward;
                    ShowBegin("S08_exit6_crane", 50);
                    Dolly(exitAt + back * 9f + Vector3.up * 8f, exitAt + back * 3.2f + Vector3.up * 2.2f,
                        exitAt + Vector3.up * 1.2f, exitAt - back * 2.5f + Vector3.down * 1.2f, 6.5f, 52, 46);
                    ShowEnd();
                }
                var start = PointAt(pts, Mathf.Max(0, total - 16f));
                Me().Teleport(start + Vector3.up * 0.05f, 0);
                Fast(5);
                pts = NavPath(Me().transform.position, exitAt);
                Face(Ahead(Shape(pts, 1.4f), 0, 4f) + Vector3.up * 1.5f);
                Fast(5);
                Begin("13_subway_exit");
                perFrame = () => HideNear(exitAt, 1.6f, 0.6f);
                // looking ahead down the street, then at the canopy and its sign, then down the stairs
                Walk(pts, 1.8f, (s, end) =>
                {
                    float left = end - s;
                    if (left > 10f) return null;
                    return left > 3.5f ? exitAt + Vector3.up * 2.6f : exitAt + Flat(exitAt - Eye()).normalized * 3f + Vector3.down * 1.5f;
                }, 0.4f, 1.4f, null, 0.7f);
                Mark("walk in");
                exit.Interact(null);
                for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != SceneFlow.Subway; i++) Step();
                R(60);
                HideHelp();
                R(30);
                End();
            }
            else
            {
                Note("  no exit 6, loading the station");
                Load(SceneFlow.Subway);
            }
            var d = PlaytestTools.Director();
            Fast(30);
            var gates = (d.waypoints ?? new Waypoint[0]).Where(w => w && w.stationId == "gate").Select(w => w.transform.position).ToList();
            if (gates.Count > 0) HallShot("S09_concourse", Mean(gates) + Vector3.up * 1.0f, 4f, 5f, 6.5f, 2.2f);
            Advance(() => d.CurrentTarget == "gate");
            // through a gate lane: straight down its middle, the card reader beeping, the flaps folding away
            var gate = Wp(d, "gate");
            if (gate)
            {
                var g = gate.transform.position;
                var away = Flat(g - Me().transform.position).normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                away = Mathf.Abs(away.x) > Mathf.Abs(away.z) ? new Vector3(Mathf.Sign(away.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(away.z));
                var y = Me().transform.position.y;
                var from = new Vector3(g.x, y, g.z) - away * 5f;
                var to = new Vector3(g.x, y, g.z) + away * 4f;
                Me().Teleport(from, Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg);
                Face(from + away * 8f + Vector3.up * 1.4f);
                Fast(10);
                Begin("14_subway_gate");
                R(10);
                Walk(new List<Vector3> { from, to }, 1.3f, (s, end) => from + away * (s + 7f) + Vector3.up * 1.45f, 0f, 0f);
                R(20);
                End();
            }
            Advance(() => d.CurrentTarget == "train");
            // the train at the platform: doors open, people off and on, then the player steps in
            var door = Wp(d, "train");
            var doors = (d.waypoints ?? new Waypoint[0]).Where(w => w && w.stationId == "train").Select(w => w.transform.position).ToList();
            if (door && doors.Count > 1)
            {
                // showcase: along the platform beside the cars
                var a0 = doors.OrderBy(p => p.x + p.z).First();
                var a1 = doors.OrderBy(p => p.x + p.z).Last();
                var axis = Flat(a1 - a0).normalized;
                var dp = door.transform.position;
                var off = Flat(Me().transform.position - dp);
                var sideV = (off - axis * Vector3.Dot(off, axis)).normalized;
                if (sideV.sqrMagnitude < 0.01f) sideV = Vector3.Cross(Vector3.up, axis);
                var y = Me().transform.position.y;
                var basePos = new Vector3(dp.x, y, dp.z) + sideV * 2.6f;
                ShowBegin("S10_platform", 46);
                Dolly(basePos - axis * 7f + Vector3.up * 1.75f, basePos + axis * 4f + Vector3.up * 1.6f,
                    new Vector3(dp.x, y, dp.z) - axis * 3f + Vector3.up * 1.4f, new Vector3(dp.x, y, dp.z) + axis * 8f + Vector3.up * 1.4f, 6.5f, 50, 44);
                ShowEnd();
            }
            if (door)
            {
                var dAt = door.transform.position;
                StandBefore(dAt, 4.2f);
                var me = Me();
                var into = Flat(dAt - me.transform.position).normalized;
                Face(dAt + Vector3.up * 1.5f);
                Fast(5);
                Begin("15_subway_train");
                perFrame = () => HideNear(dAt, 1.0f, 0.5f);
                Hold(dAt + Vector3.up * 1.5f, 150, 0.8f);
                Mark("board");
                var from = me.transform.position;
                var inside = new Vector3(dAt.x, from.y, dAt.z) + into * 1.8f;
                Walk(new List<Vector3> { from, inside }, 1.25f, (s, end) => inside + into * 6f + Vector3.up * 1.4f, 0f, 0f,
                    () => d.CurrentTarget != "train");
                for (int i = 0; i < 60 && d.CurrentTarget == "train"; i++) Step();
                if (d.CurrentTarget == "train") door.Complete();
                R(150);
                End();
            }
        }

        // ------------------------------------------------------------------ demo video (multi-device cut)

        /// <summary>Distance along <paramref name="pts"/> of the point nearest to <paramref name="p"/>.</summary>
        static float NearestS(List<Vector3> pts, Vector3 p)
        {
            float best = float.MaxValue, at = 0, run = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = Flat(pts[i - 1]);
                var ab = Flat(pts[i]) - a;
                float len = ab.magnitude;
                float t = len > 0 ? Mathf.Clamp01(Vector3.Dot(Flat(p) - a, ab) / (len * len)) : 0;
                float dist = (Flat(p) - (a + ab * t)).sqrMagnitude;
                if (dist < best) { best = dist; at = run + t * len; }
                run += len;
            }
            return at;
        }

        /// <summary>
        /// The phone: the touch controls at a phone's 20:9 (run with SFW_TOUCH=1). The thumb stick walks the player
        /// down 명동길 through the game's own controller (the knob shows it), the door is used, then at the reception
        /// the answer is said with the dialogue box held.
        /// </summary>
        static void PhoneTake()
        {
            int w = ClipRecorder.Width, h = ClipRecorder.Height;
            ClipRecorder.Width = 2400;
            ClipRecorder.Height = 1080;
            try
            {
                StartPart("clinic-1", 0, 0);
                for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
                PlaytestTools.SkipStory();
                Fast(30);
                var door = Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Exclude).First(e => e.entranceId == "clinic");
                var target = door.exitPoint ? door.exitPoint.position : door.transform.position;
                var sign = door.transform.position + Vector3.up * 2.6f;
                var pts = NavPath(Me().transform.position, target);
                float total = Length(pts);
                if (total > 16f)
                {
                    Me().Teleport(PointAt(pts, total - 15f) + Vector3.up * 0.05f, 0);
                    Fast(5);
                    pts = NavPath(Me().transform.position, target);
                }
                var shaped = Shape(pts, 1.6f);
                float len = Length(shaped);
                Face(Ahead(shaped, 0, 4f) + Vector3.up * 1.5f);
                Fast(5);
                Begin("21_phone_street");
                R(15);
                Mark("stick");
                GrabView();
                for (int f = 0; f < 420; f++)
                {
                    float s = NearestS(shaped, Me().transform.position);
                    float left = len - s;
                    if (left < 3.0f) break;
                    TouchControls.SimulatedMove = new Vector2(0, left < 6f ? 0.45f : 0.7f);
                    LookToward(left < 8f ? sign : Ahead(shaped, s, 4f) + Vector3.up * 1.5f, 0.55f);
                    Step();
                }
                TouchControls.SimulatedMove = Vector2.zero;
                Mark("stick off");
                Hold(sign, 25);
                Mark("tap door");
                door.Interact(null);
                for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != SceneFlow.Clinic; i++) Step();
                R(30);
                End();

                var d = PlaytestTools.Director();
                for (int i = 0; i < 600 && !(d.counter && d.counter.Armed); i++) Fast(1);
                if (!d.counter) { Note("  no counter"); return; }
                var c = d.counter.transform.position;
                StandBefore(c, 1.3f);
                Face(c + Vector3.up * 1.35f);
                Fast(5);
                Begin("22_phone_talk");
                d.counter.Interact(null);
                WaitInput();
                Mark("answers");
                R(60);
                Mark("hold box");
                SayRight(90);
                End();
            }
            finally
            {
                TouchControls.SimulatedMove = Vector2.zero;
                ClipRecorder.Width = w;
                ClipRecorder.Height = h;
            }
        }

        /// <summary>
        /// The station again, without the bumps: the gate lane is tapped (opened) a step before the player reaches it
        /// and walked straight down its middle; the train is boarded square to its door, from the platform's side.
        /// </summary>
        static void SubwayFixTake()
        {
            PlaytestTools.Situations = new[] { 0 };
            PlaytestTools.StartLevel("subway-1");
            if (StoryCard.Current) PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            var d = PlaytestTools.Director();
            Advance(() => d.CurrentTarget == "gate");
            var gate = Wp(d, "gate");
            if (gate)
            {
                var lane = gate.gate;
                var g = lane ? lane.Position : gate.transform.position;
                // the tap point sits a step before its lane, on the side the player comes from: through the lane is
                // from the tap point to the lane's middle (the gate line itself runs across that)
                var away = Flat(g - gate.transform.position).normalized;
                if (away.sqrMagnitude < 0.01f) away = Flat(g - Me().transform.position).normalized;
                away = Mathf.Abs(away.x) > Mathf.Abs(away.z) ? new Vector3(Mathf.Sign(away.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(away.z));
                var y = Me().transform.position.y;
                var from = new Vector3(g.x, y, g.z) - away * 5f;
                var to = new Vector3(g.x, y, g.z) + away * 4f;
                Me().Teleport(from, Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg);
                Face(from + away * 8f + Vector3.up * 1.4f);
                Fast(10);
                Note($"  lane at {g}, gate wp at {gate.transform.position}");
                Begin("14b_subway_gate");
                R(10);
                bool tapped = false;
                Walk(new List<Vector3> { from, to }, 1.2f, (s, end) =>
                {
                    // the card goes on the reader a step before the lane: the flaps fold and the lane is open
                    if (!tapped && s > 3.6f)
                    {
                        tapped = true;
                        Mark("tap");
                        // the card on the reader, as the game takes it: the step is done and the lane opens
                        gate.Complete();
                        if (lane) lane.Open(2.2f, true);
                    }
                    return from + away * (s + 7f) + Vector3.up * 1.45f;
                }, 0f, 0f);
                R(20);
                End();
            }
            Advance(() => d.CurrentTarget == "train");
            var door = Wp(d, "train");
            var doors = (d.waypoints ?? new Waypoint[0]).Where(w => w && w.stationId == "train").Select(w => w.transform.position).ToList();
            if (door && doors.Count > 1)
            {
                var dAt = door.transform.position;
                var a0 = doors.OrderBy(p => p.x + p.z).First();
                var a1 = doors.OrderBy(p => p.x + p.z).Last();
                var axis = Flat(a1 - a0).normalized;
                var off = Flat(Me().transform.position - dAt);
                var side = (off - axis * Vector3.Dot(off, axis)).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(Vector3.up, axis);
                var y = Me().transform.position.y;
                var at = new Vector3(dAt.x, y, dAt.z);
                var from = at + side * 3.8f;
                Me().Teleport(from, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg);
                Face(at + Vector3.up * 1.5f);
                Fast(5);
                Begin("15b_subway_train");
                perFrame = () => HideNear(at, 1.0f, 0.5f);
                Hold(at + Vector3.up * 1.5f, 150, 0.8f);
                Mark("board");
                var inside = at - side * 1.9f;
                Walk(new List<Vector3> { from, inside }, 1.2f, (s, end) => inside - side * 6f + Vector3.up * 1.4f, 0f, 0f,
                    () => d.CurrentTarget != "train");
                for (int i = 0; i < 60 && d.CurrentTarget == "train"; i++) Step();
                if (d.CurrentTarget == "train") door.Complete();
                R(150);
                End();
            }
        }

        /// <summary>The same reception question at Medium and at Hard (Easy is in 05_clinic_talk).</summary>
        static void LevelsTake()
        {
            foreach (var level in new[] { Difficulty.Medium, Difficulty.Hard })
            {
                Settings.Difficulty = level;
                PlaytestTools.Situations = new[] { 0, 0 };
                PlaytestTools.StartLevel("clinic-1");
                if (StoryCard.Current) PlaytestTools.SkipStory();
                HideHelp();
                Fast(20);
                var d = PlaytestTools.Director();
                for (int i = 0; i < 600 && !(d.counter && d.counter.Armed); i++) Fast(1);
                if (!d.counter) continue;
                var c = d.counter.transform.position;
                StandBefore(c, 1.3f);
                Face(c + Vector3.up * 1.35f);
                Fast(5);
                Begin("20_level_" + level.ToString().ToLowerInvariant());
                d.counter.Interact(null);
                WaitInput();
                R(120);
                End();
            }
            Settings.Difficulty = Difficulty.Easy;
        }

        static void EmergencyTake()
        {
            StartPart("emergency-1");
            for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
            PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            var d = PlaytestTools.Director();
            var man = Wp(d, "patient");
            Begin("16_119_street");
            if (man)
            {
                var manAt = man.transform.position;
                var pts = NavPath(Me().transform.position, manAt);
                R(10);
                Walk(pts, 1.9f, (s, end) => end - s < 8f ? manAt + Vector3.up * 0.4f : (Vector3?)null, 2.2f, 1.2f);
                Hold(manAt + Vector3.up * 0.4f, 15);
                if (d.CurrentTarget == "patient") man.Complete();
            }
            // the dial pad: 1, 1, 9
            var p = PlaytestTools.Panel();
            for (int i = 0; i < 200 && !(p && p.WaitingForPin); i++) { p = PlaytestTools.Panel(); Step(); }
            if (p && p.WaitingForPin)
            {
                R(25);
                PressKeys("119", 12);
                R(60);
            }
            End();
            // the end of the call (hold the line), then the ambulance drives up
            Advance(() => { var q = PlaytestTools.Panel(); return q && q.IsWaiting && q.CurrentLine.Contains("구급차가 가고"); });
            var amb = Object.FindAnyObjectByType<World.Ambulance>();
            Begin("17_119_call_end");
            R(30);
            var p2 = PlaytestTools.Panel();
            if (p2 && p2.IsWaiting && !p2.WaitingForPin) SayRight(30);
            End();
            if (amb)
            {
                // showcase: low on the street ahead of where it stops, the ambulance driving up into the frame
                var drive = Flat(amb.to - amb.from).normalized;
                var sideA = Vector3.Cross(Vector3.up, drive);
                var camA = amb.to + drive * 9f + sideA * 2.2f + Vector3.up * 0.9f;
                var camB = amb.to + drive * 7.5f + sideA * 2.6f + Vector3.up * 1.1f;
                ShowBegin("S11_ambulance", 44);
                int n = 0;
                for (int i = 0; i < 420 && !d.Finished; i++, n++)
                {
                    float e = Ease(i / 300f);
                    var pos = Vector3.Lerp(camA, camB, e);
                    var aim = amb.body && amb.body.gameObject.activeInHierarchy ? amb.body.position + Vector3.up * 1.2f : amb.from + Vector3.up * 1.5f;
                    var cur = showCam.transform.forward;
                    var want = (aim - pos).normalized;
                    var fwd = i == 0 ? want : Vector3.Slerp(cur, want, 0.12f);
                    showCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd));
                    if (!string.IsNullOrEmpty(d.CurrentTarget) && d.CurrentTarget != "ambulance") PlaytestTools.CompleteTarget(d);
                    var q = PlaytestTools.Panel();
                    if (q && q.IsWaiting && !q.WaitingForPin) q.Pick(RightIndex(d));
                    Step();
                }
                R(30);
                ShowEnd();
            }
        }

        static void ErTake()
        {
            StartPart("emergency-2", 0);
            for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
            PlaytestTools.SkipStory();
            HideHelp();
            Fast(20);
            var d = PlaytestTools.Director();
            var erDesk = (d.counters ?? new CounterSpot[0]).FirstOrDefault(c => c) ?? d.counter;
            if (erDesk) HallShot("S12_er", erDesk.transform.position + Vector3.up * 1.3f, 3.5f, 4f, 6f, 2.1f);
            Begin("18_er");
            R(15);
            for (int i = 0; i < 200 && !(d.counter && d.counter.Armed); i++) Step();
            if (d.counter)
            {
                var c = d.counter.transform.position;
                var from = Me().transform.position;
                Walk(new List<Vector3> { from, c + Flat(from - c).normalized * 1.3f }, 1.5f,
                    (s, end) => end - s < 4f ? c + Vector3.up * 1.35f : (Vector3?)null, 0f, 0f);
                Hold(c + Vector3.up * 1.35f, 12);
                if (d.counter.Armed) d.counter.Interact(null);
            }
            WaitInput();
            R(50);
            SayRight(90);
            End();
        }


        /// <summary>A film move that follows a path: the camera at <paramref name="pos"/>(point, tangent, side, eased t) aiming at <paramref name="aim"/>.</summary>
        static void PathShot(List<Vector3> pts, float s0, float s1, float seconds,
            System.Func<Vector3, Vector3, Vector3, float, Vector3> pos, System.Func<Vector3, Vector3, Vector3, float, Vector3> aim, float fov0 = 48f, float fov1 = -1f)
        {
            if (fov1 < 0) fov1 = fov0;
            int n = Mathf.Max(2, Mathf.RoundToInt(seconds * 30));
            for (int i = 0; i < n; i++)
            {
                float e = Ease(i / (float)(n - 1));
                float s = Mathf.Lerp(s0, s1, e);
                var p = PointAt(pts, s);
                var tan = Flat(PointAt(pts, s + 1.5f) - PointAt(pts, Mathf.Max(0, s - 1.5f))).normalized;
                var side = Vector3.Cross(Vector3.up, tan);
                var c = pos(p, tan, side, e);
                showCam.transform.SetPositionAndRotation(c, Quaternion.LookRotation(aim(p, tan, side, e) - c));
                showCam.fieldOfView = Mathf.Lerp(fov0, fov1, e);
                Step();
            }
        }

        /// <summary>명동길 itself: a crane down onto the street and a slow truck past the shop fronts, both on the street's middle line.</summary>
        static void StreetShowTake()
        {
            StartPart("clinic-1", 0, 0);
            for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
            PlaytestTools.SkipStory();
            Fast(30);
            var door = Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Exclude).First(e => e.entranceId == "clinic");
            var a = door.approach ? door.approach.position : Me().transform.position;
            var target = door.exitPoint ? door.exitPoint.position : door.transform.position;
            var pts = Shape(NavPath(a, target), 3.2f);
            float total = Length(pts);
            var up = Vector3.up;
            ShowBegin("S01b_street_crane", 52);
            PathShot(pts, 0f, 7f, 6.5f,
                (p, t, sd, e) => p + up * Mathf.Lerp(17f, 2.3f, e) - t * Mathf.Lerp(4f, 0f, e),
                (p, t, sd, e) => PointAt(pts, Mathf.Min(total, 30f)) + up * Mathf.Lerp(0.5f, 2.4f, e), 54, 46);
            ShowEnd();
            ShowBegin("S02b_street_truck", 40);
            PathShot(pts, Mathf.Min(10f, total * 0.3f), Mathf.Min(19f, total * 0.55f), 6.5f,
                (p, t, sd, e) => p - sd * 1.6f + up * 1.7f,
                (p, t, sd, e) => p + t * 3.5f + sd * 7f + up * 3.0f, 42);
            ShowEnd();
            ShowBegin("S02c_street_truck_left", 40);
            PathShot(pts, Mathf.Min(20f, total * 0.55f), Mathf.Min(29f, total * 0.8f), 6.5f,
                (p, t, sd, e) => p + sd * 1.6f + up * 1.7f,
                (p, t, sd, e) => p + t * 3.5f - sd * 7f + up * 3.0f, 42);
            ShowEnd();
        }

        /// <summary>Exit 6 from above the street, down onto the canopy and the stairs, along the walking line.</summary>
        static void Exit6ShowTake()
        {
            StartPart("subway-1", 0);
            for (int i = 0; i < 300 && !StoryCard.Current; i++) Fast(1);
            PlaytestTools.SkipStory();
            Fast(30);
            var exit = Object.FindObjectsByType<BuildingEntrance>(FindObjectsInactive.Exclude).FirstOrDefault(e => e.walkIn && e.Open);
            if (!exit) { Note("  no exit 6"); return; }
            var exitAt = exit.transform.position;
            var pts = Shape(NavPath(Me().transform.position, exitAt), 2.0f);
            float total = Length(pts);
            var up = Vector3.up;
            ShowBegin("S08b_exit6_crane", 50);
            PathShot(pts, Mathf.Max(0, total - 12f), Mathf.Max(0, total - 3.5f), 7f,
                (p, t, sd, e) => p + up * Mathf.Lerp(7.5f, 2.2f, e),
                (p, t, sd, e) => Vector3.Lerp(exitAt + up * 1.5f, exitAt + Flat(exitAt - p).normalized * 2.5f + Vector3.down * 1.2f, e), 52, 46);
            ShowEnd();
        }

        static bool ClickAny(params string[] labels) => labels.Any(PlaytestTools.Click);

        static UnityEngine.UI.ScrollRect PageScroll()
        {
            var book = Object.FindAnyObjectByType<Logbook>();
            return book ? book.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().FirstOrDefault(s => s.name == "Scroll") ?? book.GetComponentInChildren<UnityEngine.UI.ScrollRect>() : null;
        }

        static void Scroll(UnityEngine.UI.ScrollRect s, float from, float to, int frames)
        {
            for (int i = 0; i <= frames; i++)
            {
                if (s) s.verticalNormalizedPosition = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, i / (float)frames));
                Step();
            }
        }

        static bool Listen(UnityEngine.UI.ScrollRect s, int skip)
        {
            if (!s || !s.content) return false;
            var b = s.content.GetComponentsInChildren<UnityEngine.UI.Button>().Where(x => x.name == "Listen").Skip(skip).FirstOrDefault();
            if (!b) return false;
            b.onClick.Invoke();
            return true;
        }

        static void LogbookTake()
        {
            Load(SceneFlow.Select);
            Fast(60);
            var sel = Object.FindAnyObjectByType<LevelSelectScreen>();
            Begin("19_logbook");
            R(15);
            Logbook.Show(sel.procedures, Levels.Find("clinic-1"), null);
            R(70);
            var page = PageScroll();
            Scroll(page, 1f, 0.35f, 90);
            R(10);
            Listen(page, 0);
            R(60);
            Mark("words");
            ClickAny("Key words", "KEY WORDS");
            R(30);
            page = PageScroll();
            Listen(page, 1);
            R(70);
            Mark("talk");
            ClickAny("Conversation", "CONVERSATION");
            R(30);
            page = PageScroll();
            Scroll(page, 1f, 0.85f, 60);
            Listen(page, 0);
            R(100);
            End();
        }
    }
}
