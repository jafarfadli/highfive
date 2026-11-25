using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// The camera look of each kind of scene: post-processing profiles (tonemapping, grading, bloom,
    /// vignette) and the sky. Scene builders call <see cref="Street"/>, <see cref="Interior"/> or
    /// <see cref="Menu"/> once per scene. No realtime reflection probes: URP's probe atlas throws in
    /// edit-mode renders, and the skybox reflection is enough for these materials.
    /// </summary>
    public static class LookKit
    {
        const string Folder = "Assets/_Project/Settings/Look";

        /// <summary>The Myeongdong street at golden hour: warm low light, cool shade, a toon sky with clouds.</summary>
        public static void Street(Transform root)
        {
            Volume(root, Profile("Street", p =>
            {
                Common(p, exposure: 0.1f, contrast: 20f, saturation: 18f, temperature: 12f, bloom: 0.75f, vignette: 0.28f, threshold: 0.95f);
                var smh = p.Add<ShadowsMidtonesHighlights>();
                smh.shadows.Override(new Vector4(0.93f, 0.96f, 1.1f, 0f));
                smh.highlights.Override(new Vector4(1.07f, 1.0f, 0.9f, 0f));
            }));
            RenderSettings.skybox = Sky();
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        /// <summary>Home and scenario select: the street, softly out of focus behind the UI.</summary>
        public static void Menu(Transform root)
        {
            foreach (var v in root.GetComponentsInChildren<Volume>(true)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            Volume(root, Profile("Menu", p =>
            {
                Common(p, exposure: 0.1f, contrast: 18f, saturation: 14f, temperature: 12f, bloom: 0.7f, vignette: 0.32f, threshold: 0.95f);
                var smh = p.Add<ShadowsMidtonesHighlights>();
                smh.shadows.Override(new Vector4(0.93f, 0.96f, 1.1f, 0f));
                smh.highlights.Override(new Vector4(1.07f, 1.0f, 0.9f, 0f));
                var dof = p.Add<DepthOfField>();
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(14f);
                dof.gaussianEnd.Override(70f);
                dof.gaussianMaxRadius.Override(1.2f);
                dof.highQualitySampling.Override(true);
            }));
        }

        /// <summary>Clinic, immigration office and bank: bright office light, a touch warmer and punchier than neutral.</summary>
        public static void Interior(Transform root)
        {
            Volume(root, Profile("Interior", p =>
                Common(p, exposure: 0.3f, contrast: 14f, saturation: 8f, temperature: 3f, bloom: 0.45f, vignette: 0.2f)));
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        static void Common(VolumeProfile p, float exposure, float contrast, float saturation, float temperature, float bloom, float vignette,
            float threshold = 1.05f)
        {
            var tone = p.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var color = p.Add<ColorAdjustments>();
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            var wb = p.Add<WhiteBalance>();
            wb.temperature.Override(temperature);
            var b = p.Add<Bloom>();
            b.threshold.Override(threshold);
            b.intensity.Override(bloom);
            b.scatter.Override(0.62f);
            b.highQualityFiltering.Override(true);
            var vig = p.Add<Vignette>();
            vig.intensity.Override(vignette);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.07f, 0.08f, 0.11f));
        }

        static VolumeProfile Profile(string name, Action<VolumeProfile> setup)
        {
            EnsureFolder(Folder);
            var path = $"{Folder}/{name}.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!p)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }
            // rebuild the overrides in place so scenes keep their reference to the asset
            foreach (var c in p.components.ToList())
            {
                p.components.Remove(c);
                UnityEngine.Object.DestroyImmediate(c, true);
            }
            setup(p);
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static void Volume(Transform root, VolumeProfile profile)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(root, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 0;
            v.sharedProfile = profile;
        }

        /// <summary>
        /// The toon sky (SeoulFirstWeek/Sky Toon): golden-hour gradient, the sun where the scene's sun is, and
        /// inked clouds. Every value is set here, so a stale material can't keep old ones.
        /// </summary>
        static Material Sky()
        {
            EnsureFolder(Folder);
            var path = $"{Folder}/Sky_Seoul.mat";
            var shader = Shader.Find("SeoulFirstWeek/Sky Toon");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            var sun = RenderSettings.sun;
            m.SetVector("_SunDirection", sun ? -sun.transform.forward : new Vector3(-0.4f, 0.5f, -0.75f));
            m.SetColor("_ZenithColor", C("#2C5FBE"));
            m.SetColor("_SkyColor", C("#6FA4E6"));
            m.SetColor("_HorizonColor", C("#F6C99B"));
            m.SetColor("_GroundColor", C("#8C7466"));
            m.SetColor("_SunColor", C("#FFE2B0"));
            m.SetFloat("_SunSize", 0.9991f);
            m.SetFloat("_SunGlow", 0.9f);
            m.SetColor("_CloudLit", C("#FFFCF4"));
            m.SetColor("_CloudWarm", C("#FFC88E"));
            m.SetColor("_CloudShade", C("#AAB4D8"));
            m.SetColor("_CloudInk", C("#3A2F3F"));
            m.SetFloat("_CloudCover", 0.47f);
            m.SetFloat("_CloudScale", 1.1f);
            m.SetFloat("_CloudSpeed", 0.006f);
            m.SetFloat("_InkWidth", 2.2f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
