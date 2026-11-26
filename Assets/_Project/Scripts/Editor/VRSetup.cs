using System.Linq;
using Unity.XR.MockHMD;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// VR for the Windows build (Meta Quest 2 over Quest Link): OpenXR is the only XR loader of the Standalone
    /// target and is not started on launch, because <see cref="Core.VRMode"/> starts it only when a headset
    /// answers (so the same build still plays on a plain screen); Oculus Touch and Quest Touch Plus
    /// controllers; single-pass instanced stereo. The Mock HMD loader stands in for a headset in the editor
    /// for play-tests on a Mac (<see cref="UseMockHmd"/>), and never goes into a build.
    /// </summary>
    public static class VRSetup
    {
        const string SettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        const string MockSettingsPath = "Assets/XR/Settings/MockHMDBuildSettings.asset";
        const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string MockLoaderType = "Unity.XR.MockHMD.MockHMDLoader";

        [MenuItem("Seoul First Week/VR/Configure OpenXR")]
        public static void Configure()
        {
            // a play-test with the pretend headset may have been stopped half-way: put OpenXR back alone
            UseMockHmd(false);
            var manager = StandaloneManager(out var general);
            general.InitManagerOnStart = false;

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            foreach (var f in openxr.GetFeatures<OpenXRInteractionFeature>())
                f.enabled = f is OculusTouchControllerProfile || f is MetaQuestTouchPlusControllerProfile;
            EditorUtility.SetDirty(openxr);

            // Quest Link keeps the headset's picture coming only while the game renders, and the desktop
            // window loses focus as soon as the player puts the headset on
            PlayerSettings.runInBackground = true;
            NameLayer(UI.VRUI.Layer, "VR UI");
            ConfigureUIPass();
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VRSetup] Standalone loaders: {string.Join(", ", manager.activeLoaders.Select(l => l.GetType().Name))}; " +
                      $"start on launch {general.InitManagerOnStart}; render mode {openxr.renderMode}; " +
                      $"controllers {string.Join(", ", openxr.GetFeatures<OpenXRInteractionFeature>().Where(f => f.enabled).Select(f => f.GetType().Name))}");
        }

        /// <summary>
        /// For play-tests in the editor without a headset: the Mock HMD loader alone (on) or OpenXR alone
        /// (off). <paramref name="singlePass"/> picks the Mock HMD's stereo mode.
        /// </summary>
        public static void UseMockHmd(bool on, bool singlePass = true)
        {
            var manager = StandaloneManager(out _);
            if (on)
            {
                var mock = AssetDatabase.LoadAssetAtPath<MockHMDBuildSettings>(MockSettingsPath);
                if (!mock)
                {
                    mock = ScriptableObject.CreateInstance<MockHMDBuildSettings>();
                    AssetDatabase.CreateAsset(mock, MockSettingsPath);
                }
                mock.renderMode = singlePass ? MockHMDBuildSettings.RenderMode.SinglePassInstanced : MockHMDBuildSettings.RenderMode.MultiPass;
                EditorUtility.SetDirty(mock);
                EditorBuildSettings.AddConfigObject(MockHMDBuildSettings.BuildSettingsKey, mock, true);
                XRPackageMetadataStore.RemoveLoader(manager, OpenXRLoaderType, BuildTargetGroup.Standalone);
                XRPackageMetadataStore.AssignLoader(manager, MockLoaderType, BuildTargetGroup.Standalone);
            }
            else
            {
                XRPackageMetadataStore.RemoveLoader(manager, MockLoaderType, BuildTargetGroup.Standalone);
                EditorBuildSettings.RemoveConfigObject(MockHMDBuildSettings.BuildSettingsKey);
                // the settings and the loader asset the XR package wrote when it was assigned go too
                foreach (var guid in AssetDatabase.FindAssets("t:MockHMDBuildSettings t:MockHMDLoader", new[] { "Assets/XR" }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                if (!manager.activeLoaders.Any(l => l && l.GetType().FullName == OpenXRLoaderType))
                    XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, BuildTargetGroup.Standalone);
            }
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The VR panels' own pass: every renderer leaves their layer out of its opaque and transparent passes and
        /// draws it after post-processing (so the street's grade doesn't tint the UI) with no depth test (so nothing
        /// in the world can hide a panel). On a plain screen nothing is on that layer and the pass costs nothing.
        /// </summary>
        static void ConfigureUIPass()
        {
            int bit = 1 << UI.VRUI.Layer;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (!data) continue;
                data.opaqueLayerMask &= ~bit;
                data.transparentLayerMask &= ~bit;
                RenderObjects feature = null;
                foreach (var f in data.rendererFeatures)
                    if (f is RenderObjects ro && f.name == "VR UI") feature = ro;
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<RenderObjects>();
                    feature.name = "VR UI";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                    var so = new SerializedObject(data);
                    var list = so.FindProperty("m_RendererFeatures");
                    var map = so.FindProperty("m_RendererFeatureMap");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var s = feature.settings;
                s.passTag = "VR UI";
                s.Event = RenderPassEvent.AfterRenderingPostProcessing;
                s.filterSettings.RenderQueueType = RenderQueueType.Transparent;
                s.filterSettings.LayerMask = bit;
                s.filterSettings.PassNames = new string[0];
                s.overrideDepthState = true;
                s.depthCompareFunction = UnityEngine.Rendering.CompareFunction.Always;
                s.enableWrite = false;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
        }

        static void NameLayer(int layer, string name)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            var slot = layers.GetArrayElementAtIndex(layer);
            if (slot.stringValue == name) return;
            slot.stringValue = name;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>True when a build would carry the Mock HMD loader (the player builds refuse to go on).</summary>
        public static bool MockHmdAssigned()
        {
            var manager = StandaloneManager(out _);
            return manager.activeLoaders.Any(l => l && l.GetType().FullName == MockLoaderType);
        }

        static XRManagerSettings StandaloneManager(out XRGeneralSettings general)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (!perTarget)
            {
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsPath);
                if (!perTarget)
                {
                    System.IO.Directory.CreateDirectory("Assets/XR");
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, SettingsPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
            general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            return perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
        }
    }
}
