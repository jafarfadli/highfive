using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace SeoulFirstWeek.Core
{
    /// <summary>
    /// Whether the game runs in a headset. VR starts only when the game is launched with <c>-vr</c> (the Windows
    /// build's "Ready Set Go (VR)" shortcut does that) and an OpenXR runtime answers with a headset, such as a
    /// Meta Quest 2 over Quest Link; otherwise the game plays on the screen as always. <c>-vr-multipass</c> renders
    /// the two eyes one after the other instead of in one instanced pass (a fallback if a shader misbehaves).
    /// In the editor, <c>-vr</c> on the editor's command line (or the SFW_VR environment variable) does the same,
    /// so play-tests can run against the Mock HMD.
    /// </summary>
    public static class VRMode
    {
        /// <summary>True while a headset shows the game.</summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// Editor recordings only (SFW_VR_SIM=1): the game runs as it does in a headset (panels, the laser, the
        /// controller's button names, no screen-fixed patterns), but no XR runtime starts and the recorder poses the
        /// head and the hands itself. Never true in a build.
        /// </summary>
        public static bool Simulated { get; private set; }

        /// <summary>Raised once VR has started (before the first scene loads).</summary>
        public static event System.Action Started;

        public static bool Wanted =>
            HasArg("-vr") || HasArg("-vr-multipass") || System.Environment.GetEnvironmentVariable("SFW_VR") == "1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Active = false;
            Simulated = false;
            // shaders leave out patterns fixed to the screen in a headset (InkLighting, Sky Toon, Ink Outline)
            Shader.SetGlobalFloat(GlobalFlag, 0f);
#if UNITY_EDITOR
            if (System.Environment.GetEnvironmentVariable("SFW_VR_SIM") == "1")
            {
                Active = Simulated = true;
                Shader.SetGlobalFloat(GlobalFlag, 1f);
                Debug.Log("[VR] simulated headset for a recording (no XR runtime)");
                Started?.Invoke();
                return;
            }
#endif
            if (!Wanted) return;
            var general = XRGeneralSettings.Instance;
            var manager = general ? general.Manager : null;
            if (!manager)
            {
                Debug.LogWarning("[VR] no XR settings in this build: playing on the screen");
                return;
            }
            if (HasArg("-vr-multipass")) SetMultiPass();
            if (manager.activeLoader == null) manager.InitializeLoaderSync();
            if (manager.activeLoader == null)
            {
                Debug.LogWarning("[VR] no headset answered (is Quest Link on and the headset connected?): playing on the screen");
                return;
            }
            manager.StartSubsystems();
            // positions are measured from where the head was when tracking started, so whoever wears the headset,
            // sitting or standing, sees the street from the game's own eye height
            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputs);
            foreach (var s in inputs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
            Active = true;
            Shader.SetGlobalFloat(GlobalFlag, 1f);
#if !UNITY_EDITOR
            // in a headset the desktop window is often not the focused one (the Link dashboard takes the focus), and
            // by default the Input System stops reading devices then: keep reading the controllers whatever has focus
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior =
                UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            // -vr-scale 0.8 renders each eye smaller, for a slower graphics card
            float scale = ArgFloat("-vr-scale", 1f);
            if (scale > 0.3f && scale <= 2f) XRSettings.eyeTextureResolutionScale = scale;
            LightenForVR();
#if UNITY_EDITOR
            // the editor's game view shows both eyes, so a play-test can photograph what each one gets
            XRSettings.gameViewRenderMode = GameViewRenderMode.BothEyes;
#endif
            Application.quitting += Stop;
            Debug.Log($"[VR] started with {manager.activeLoader.name}, eye scale {XRSettings.eyeTextureResolutionScale:0.00}");
            Started?.Invoke();
        }

        const string GlobalFlag = "_SFW_VR";

        /// <summary>
        /// Both eyes at 72–90 frames a second: ambient occlusion off and shorter shadows. Only in a build: in the
        /// editor these are the project's own assets, and a play-test must not rewrite them.
        /// </summary>
        static void LightenForVR()
        {
#if !UNITY_EDITOR
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, 35f);
                urp.shadowCascadeCount = Mathf.Min(urp.shadowCascadeCount, 2);
                foreach (var renderer in urp.rendererDataList)
                    if (renderer)
                        foreach (var f in renderer.rendererFeatures)
                            if (f is UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion) f.SetActive(false);
            }
#endif
        }

        static float ArgFloat(string name, float fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, System.StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return v;
            return fallback;
        }

        static void SetMultiPass()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.Instance;
            if (settings) settings.renderMode = UnityEngine.XR.OpenXR.OpenXRSettings.RenderMode.MultiPass;
        }

        static void Stop()
        {
            var manager = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
            if (manager && manager.activeLoader)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            Active = false;
            Shader.SetGlobalFloat(GlobalFlag, 0f);
        }

        static bool HasArg(string arg)
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (string.Equals(a, arg, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
