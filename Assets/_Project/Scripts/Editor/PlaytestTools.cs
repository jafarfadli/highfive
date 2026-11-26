using System.Collections.Generic;
using System.IO;
using SeoulFirstWeek.Procedure;
using SeoulFirstWeek.UI;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Helpers for automated play-tests driven from editor scripts (e.g. through MCP).
    /// When the editor window is in the background the player loop stalls, so frames
    /// are advanced explicitly with <see cref="EditorApplication.Step"/>.
    /// </summary>
    public static class PlaytestTools
    {
        public static readonly List<string> Log = new();

        public static void Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                EditorApplication.Step();
                System.Threading.Thread.Sleep(10);
            }
        }

        public static void Release() => EditorApplication.isPaused = false;

        /// <summary>
        /// Renders the main camera including screen-space UI to a PNG. The UI is drawn through the camera, so the
        /// menu depth of field blurs it; pass <paramref name="postProcessing"/> false to check UI text.
        /// </summary>
        public static void Capture(string path, int width = 1600, int height = 900, Camera cam = null, bool postProcessing = true)
        {
            cam = cam ? cam : Camera.main;
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post = camData && camData.renderPostProcessing;
            if (camData) camData.renderPostProcessing = post && postProcessing;
            var changed = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isRootCanvas) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.02f + (1000 - c.sortingOrder) * 0.0004f;
                changed.Add(c);
            }
            var rt = new RenderTexture(width, height, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            foreach (var c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
            if (camData) camData.renderPostProcessing = post;
        }

        /// <summary>Renders from an arbitrary pose without UI (edit or play mode).</summary>
        public static void Shot(string path, Vector3 pos, Vector3 lookAt, float fov = 68, int width = 1600, int height = 900)
        {
            var go = new GameObject("TmpShotCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.farClipPlane = 900;
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Play mode only: makes <paramref name="levelId"/> the current level and loads its building
        /// directly (skipping the story card and the street), then waits for the scene.
        /// </summary>
        public static void StartLevel(string levelId)
        {
            var level = Core.Levels.Find(levelId);
            Core.GameSession.I.CurrentLevel = level;
            Core.SceneFlow.Load(level.scene);
            for (int i = 0; i < 200 && SceneManager.GetActiveScene().name == level.scene; i++) Frames(1);
            WaitScene(level.scene);
        }

        public static void WaitScene(string name, int max = 1500)
        {
            for (int i = 0; i < max && SceneManager.GetActiveScene().name != name; i++) Frames(1);
            Frames(60);
        }

        public static ScenarioDirector Director() =>
            Object.FindAnyObjectByType<ScenarioDirector>(FindObjectsInactive.Include);

        public static ConversationPanel Panel() =>
            Object.FindAnyObjectByType<ConversationPanel>(FindObjectsInactive.Include);

        /// <summary>
        /// Clears the story card, takes a ticket (walk-in places have none), does any errands in the world first
        /// (the `go` steps before the first conversation), fast-forwards the queue and steps up to the counter.
        /// </summary>
        public static ScenarioDirector ToCounter()
        {
            var d = Director();
            if (StoryCard.Current) SkipStory();
            if (d.ticketMachine)
            {
                for (int i = 0; i < 300 && !d.ticketMachine.Armed; i++) Frames(1);
                d.ticketMachine.Interact(null);
            }
            Time.timeScale = 4f;
            for (int i = 0; i < 3000 && !(d.counter && d.counter.Armed) && !(Panel() && Panel().IsWaiting); i++)
            {
                CompleteTarget(d);
                Frames(1);
            }
            Time.timeScale = 1f;
            if (d.counter && d.counter.Armed) d.counter.Interact(null);
            return d;
        }

        /// <summary>Uses the waypoint the current `go` step wants (or, with <paramref name="wrongFirst"/>, a wrong one).</summary>
        public static bool CompleteTarget(ScenarioDirector d, bool wrongFirst = false)
        {
            if (string.IsNullOrEmpty(d.CurrentTarget) || d.waypoints == null) return false;
            string id = wrongFirst && d.CurrentWrong.Length > 0 ? d.CurrentWrong[0] : d.CurrentTarget;
            foreach (var w in d.waypoints)
                if (w && w.Armed && w.stationId == id)
                {
                    Log.Add($"   (goes to {id})");
                    w.Complete();
                    return true;
                }
            return false;
        }

        public static bool WaitForInput(int max = 3000)
        {
            for (int i = 0; i < max; i++)
            {
                var p = Panel();
                if (p && p.IsWaiting) return true;
                Frames(1);
            }
            return false;
        }

        /// <summary>
        /// Plays the current level to the end: queue, counter, every answer, form fetches.
        /// With <paramref name="mistakes"/> it first picks a wrong answer wherever one exists.
        /// Prefers advance, then fetch, then block, so a missing requirement ends in "blocked".
        /// </summary>
        public static void PlayLevel(bool mistakes, int maxFrames = 40000)
        {
            ToCounter();
            FinishLevel(mistakes, maxFrames);
        }

        /// <summary>Same as <see cref="PlayLevel"/> but starts from wherever the level currently is.</summary>
        public static void FinishLevel(bool mistakes, int maxFrames = 40000)
        {
            var d = Director();
            string lastStep = null;
            for (int f = 0; f < maxFrames && !d.Finished; f++)
            {
                foreach (var st in d.stations ?? new FetchStation[0])
                    if (st && st.Armed)
                    {
                        Log.Add($"   (fills in {st.document} at {st.stationId})");
                        st.Complete();
                    }
                if (d.counter && d.counter.Armed) d.counter.Interact(null);
                // world steps: a wrong turn once when asked for mistakes, then the right way
                if (!string.IsNullOrEmpty(d.CurrentTarget))
                {
                    bool wrong = mistakes && lastStep != "go:" + d.CurrentTarget && d.CurrentWrong.Length > 0;
                    if (CompleteTarget(d, wrong)) lastStep = "go:" + d.CurrentTarget;
                    Frames(2);
                    continue;
                }
                // walk-in places: the patient walks into the treatment room when called
                foreach (var room in d.rooms ?? new RoomEntrance[0])
                    if (room && room.Armed)
                    {
                        Log.Add($"   (walks into {room.label})");
                        room.Interact(null);
                    }

                var p = Panel();
                if (p && p.IsWaiting)
                {
                    if (p.WaitingForPin)
                    {
                        var code = string.IsNullOrEmpty(d.CurrentExpect) ? "2580" : d.CurrentExpect;
                        Log.Add($"NPC> {p.CurrentLine}\n   YOU> [PIN {code}]");
                        p.EnterPin(code);
                    }
                    else
                    {
                        var shown = d.ShownChoices;
                        int pick = -1;
                        if (mistakes && p.CurrentLine != lastStep)
                            pick = shown.FindIndex(c => c.result == "retry");
                        lastStep = p.CurrentLine;
                        foreach (var want in new[] { "advance", "fetch", "block" })
                            if (pick < 0) pick = shown.FindIndex(c => c.result == want);
                        if (pick < 0) pick = 0;
                        Log.Add($"NPC> {p.CurrentLine}\n   YOU> {shown[pick].ko} ({shown[pick].result})");
                        p.Pick(pick);
                    }
                    Frames(3);
                    continue;
                }
                Frames(1);
            }
            Log.Add(d.Finished ? "== finished" : "== timeout");
        }

        /// <summary>Waits for the level's story card and clicks through it.</summary>
        /// <summary>The option play-tests pick for each question on a personalised visit's story card (missing: the first).</summary>
        public static int[] Situations = new int[0];

        public static bool SkipStory(int max = 600)
        {
            for (int i = 0; i < max; i++)
            {
                var card = StoryCard.Current;
                if (card && card.Picking)
                {
                    card.Pick(card.Asking < Situations.Length ? Situations[card.Asking] : 0);
                    Frames(30);
                    continue;
                }
                if (card) card.Begin();
                else if (i > 30) return true;
                Frames(1);
            }
            return StoryCard.Current == null;
        }

        /// <summary>Clicks the first visible button whose label contains <paramref name="label"/>.</summary>
        public static bool Click(string label)
        {
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude))
            {
                var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
                if (t && t.text.Contains(label) && b.interactable)
                {
                    b.onClick.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Photographs people in a throwaway studio scene (interior look, warm key light, grey backdrop):
        /// a row of full figures, or head-and-shoulders close-ups. Poses: "stand", "walk", "sit", "type", "talk".
        /// Opens a new unsaved scene, so save or rebuild the open scene first.
        /// </summary>
        public static void Studio(string path, IList<HumanLook> looks, bool closeUp, string pose = "stand", float yaw = 180f, int width = 1600, int height = 900, bool detailed = true)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32f, 145f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 12f;
            fill.intensity = 2.2f;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.transform.position = new Vector3(-2.5f, 2.6f, -3f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.47f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.26f, 0.24f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(30, 0.1f, 30);
            floor.transform.position = new Vector3(0, -0.05f, 0);
            floor.GetComponent<Renderer>().sharedMaterial = Mats.Lit(new Color(0.55f, 0.55f, 0.56f), 0.2f);
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.transform.localScale = new Vector3(30, 8, 0.1f);
            back.transform.position = new Vector3(0, 4, 2.5f);
            back.GetComponent<Renderer>().sharedMaterial = Mats.Lit(new Color(0.62f, 0.63f, 0.65f), 0.1f);
            var volume = new GameObject("Look").AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Project/Settings/Look/Interior.asset");

            float spacing = closeUp ? 0.42f : 0.78f;
            int n = looks.Count;
            for (int i = 0; i < n; i++)
            {
                var rig = HumanBuilder.Build(null, $"Person_{i}", looks[i], false, detailed);
                rig.transform.SetPositionAndRotation(new Vector3((i - (n - 1) * 0.5f) * spacing, 0, 0), Quaternion.Euler(0, yaw, 0));
                rig.sitting = pose is "sit" or "type";
                rig.typing = pose == "type";
                rig.talking = pose == "talk";
                rig.seatHeight = 0.46f;
                if (pose == "walk")
                {
                    rig.walkSpeed = 1.2f;
                    rig.Pose(0.17f + i * 0.05f);
                }
                else rig.Pose(0f);
                if (rig.mouth && pose == "talk") rig.mouth.localScale = new Vector3(1, 0.6f, 1);
            }
            var camGo = new GameObject("StudioCam");
            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.backgroundColor = Color.gray;
            float rowWidth = Mathf.Max(1f, (n - 1) * spacing + 0.6f);
            bool seated = pose is "sit" or "type";
            if (closeUp)
            {
                float h = (seated ? 1.25f : 1.62f);
                cam.fieldOfView = 28f;
                float dist = rowWidth * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad * width / height) + 0.6f;
                camGo.transform.position = new Vector3(0, h + 0.03f, -dist);
                camGo.transform.LookAt(new Vector3(0, h - 0.02f, 0));
            }
            else
            {
                cam.fieldOfView = 34f;
                float dist = Mathf.Max(rowWidth * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad * width / height), 1.1f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) + 0.5f;
                camGo.transform.position = new Vector3(0, seated ? 0.85f : 1.0f, -dist);
                camGo.transform.LookAt(new Vector3(0, seated ? 0.7f : 0.9f, 0));
            }
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Renders a piece of UI built by <paramref name="build"/> on a 1920 × 1080 canvas (edit or play mode),
        /// over an optional backdrop image. Static: no animation runs.
        /// </summary>
        public static void UIShot(string path, System.Action<RectTransform> build, Texture backdrop = null,
            int width = 1920, int height = 1080)
        {
            var camGo = new GameObject("UIShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Get(Role.Ground);
            cam.cullingMask = 1 << 5;
            bool hadEventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            var canvas = UIKit.CreateCanvas("UIShot", 0);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            canvas.gameObject.layer = 5;
            var root = (RectTransform)canvas.transform;
            if (backdrop)
            {
                var img = UIKit.Rect("Backdrop", root).gameObject.AddComponent<UnityEngine.UI.RawImage>();
                img.texture = backdrop;
                img.rectTransform.Stretch();
                img.raycastTarget = false;
            }
            build(root);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.aspect = width / (float)height;
            for (int i = 0; i < 2; i++)
            {
                Canvas.ForceUpdateCanvases();
                cam.Render();
            }
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(canvas.gameObject);
            Object.DestroyImmediate(camGo);
            var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es && !hadEventSystem && !Application.isPlaying) Object.DestroyImmediate(es.gameObject);
        }

        public static string TakeLog()
        {
            var s = string.Join("\n", Log);
            Log.Clear();
            return s;
        }
    }
}
