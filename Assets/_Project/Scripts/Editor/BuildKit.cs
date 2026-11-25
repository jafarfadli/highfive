using System.IO;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>Geometry and object helpers shared by the scene builders.</summary>
    public static class BuildKit
    {
        public const string MaterialFolder = "Assets/_Project/Art/Generated/Materials";
        public const string ControlsPath = "Assets/_Project/Settings/SeoulControls.inputactions";

        public const string PeopleMeshFolder = "Assets/_Project/Art/Generated/Meshes/People";

        public static void BeginMaterials()
        {
            EnsureFolder(MaterialFolder);
            CharacterKit.Ensure();
            InkKit.Ensure();
            InkKit.EnsureOutline();
            EnsureFolder(PeopleMeshFolder);
            HumanBuilder.Persist = mesh =>
            {
                var path = AssetDatabase.GenerateUniqueAssetPath($"{PeopleMeshFolder}/{mesh.name}.asset");
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            };
            Mats.ClearCache();
            Mats.Persist = (mat, key) =>
            {
                var path = $"{MaterialFolder}/{key}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!existing)
                {
                    AssetDatabase.CreateAsset(mat, path);
                    return mat;
                }
                // a material saved before the shader changed (e.g. Lit → the cel Surface) follows the new one
                if (existing.shader != mat.shader)
                {
                    existing.shader = mat.shader;
                    existing.CopyPropertiesFromMaterial(mat);
                    existing.shaderKeywords = mat.shaderKeywords;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            };
        }

        public static void EndMaterials()
        {
            HumanBuilder.Persist = null;
            Mats.Persist = null;
            Mats.ClearCache();
            AssetDatabase.SaveAssets();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent) t.SetParent(parent, false);
            return t;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color, bool collider = true, float yaw = 0) =>
            Prims.Box(parent, name, pos, size, color, collider, Quaternion.Euler(0, yaw, 0));

        public static GameObject Glass(Transform parent, string name, Vector3 pos, Vector3 size, float yaw = 0, bool collider = true) =>
            Prims.Make(PrimitiveType.Cube, parent, name, pos, size, Mats.Glass(new Color(0.85f, 0.93f, 0.98f, 0.1f)), collider, Quaternion.Euler(0, yaw, 0));

        public static GameObject Emissive(Transform parent, string name, Vector3 pos, Vector3 size, Color color, float intensity = 1.5f) =>
            Prims.Make(PrimitiveType.Cube, parent, name, pos, size, Mats.Emissive(color, intensity));

        public static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 size, Color color, bool collider = false) =>
            Prims.Make(PrimitiveType.Cylinder, parent, name, pos, size, Mats.Lit(color), collider);

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 size, Color color) =>
            Prims.Make(PrimitiveType.Sphere, parent, name, pos, size, Mats.Lit(color));

        public static Text Label(Transform parent, string text, Vector3 localPos, Vector2 size, int fontSize, Color color, float yaw = 0, bool bold = true) =>
            WorldText.Create(parent, "Text", text, localPos, size, fontSize, color, bold, TextAnchor.MiddleCenter, Quaternion.Euler(0, yaw, 0));

        /// <summary>A sign board: coloured box with Hangul text on its front (+Z of the parent frame after yaw).</summary>
        public static Transform Sign(Transform parent, string name, Vector3 pos, Vector2 size, Color board, string text, Color textColor, float yaw = 0, int fontSize = 60, float depth = 0.08f)
        {
            var root = Group(name, parent);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(root, "Board", Vector3.zero, new Vector3(size.x, size.y, depth), board, false);
            WorldText.Create(root, "Text", text, new Vector3(0, 0, depth / 2 + 0.01f), size * 0.92f, fontSize, textColor, true, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            return root;
        }

        public static void Plant(Transform parent, Vector3 pos, float scale = 1f)
        {
            var g = Group("Plant", parent);
            g.localPosition = pos;
            Cylinder(g, "Pot", new Vector3(0, 0.25f * scale, 0), new Vector3(0.45f, 0.25f, 0.45f) * scale, C("#8A6A55"), true);
            Sphere(g, "Leaves", new Vector3(0, 0.85f * scale, 0), new Vector3(0.8f, 0.9f, 0.8f) * scale, C("#4F8A3C"));
            Sphere(g, "Leaves2", new Vector3(0.15f, 1.2f * scale, 0.05f), new Vector3(0.5f, 0.6f, 0.5f) * scale, C("#5E9E48"));
        }

        /// <summary>Player rig: CharacterController + first-person controller + camera with URP data.</summary>
        public static FirstPersonController CreatePlayer(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Player");
            if (parent) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.875f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50;
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0, 1.62f, 0);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 68f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var fps = go.AddComponent<FirstPersonController>();
            fps.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            fps.cameraPivot = pivot;
            go.AddComponent<PlayerInteractor>();
            return fps;
        }

        public static void Ambient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        public static Light Sun(Transform parent, Vector3 euler, float intensity, Color color, LightShadows shadows = LightShadows.Soft)
        {
            var go = new GameObject("Sun");
            if (parent) go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            RenderSettings.sun = l;
            return l;
        }

        public static Light PointLight(Transform parent, Vector3 pos, float range, float intensity, Color color)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
