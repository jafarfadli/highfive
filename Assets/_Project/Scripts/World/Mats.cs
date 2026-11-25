using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// Shared URP materials keyed by colour. In the editor, scene builders set
    /// <see cref="Persist"/> so the materials are saved as assets.
    /// </summary>
    public static class Mats
    {
        public static Func<Material, string, Material> Persist;
        static readonly Dictionary<string, Material> Cache = new();

        static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");
        // the cel-shaded surface (flat tones, halftone shade) so small parts match the textured world
        static Shader CelShader => Shader.Find("SeoulFirstWeek/Surface") ?? LitShader;
        static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");

        public static Material Lit(Color c, float smoothness = 0.15f, float metallic = 0f)
        {
            var key = $"Lit_{ColorUtility.ToHtmlStringRGB(c)}_s{Mathf.RoundToInt(smoothness * 100)}_m{Mathf.RoundToInt(metallic * 100)}";
            return Get(key, () =>
            {
                var m = new Material(CelShader);
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
                return m;
            });
        }

        public static Material Emissive(Color c, float intensity = 2f)
        {
            var key = $"Emit_{ColorUtility.ToHtmlStringRGB(c)}_{Mathf.RoundToInt(intensity * 10)}";
            return Get(key, () =>
            {
                var m = new Material(LitShader);
                m.SetColor("_BaseColor", c);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return m;
            });
        }

        public static Material Glass(Color c)
        {
            var key = $"Glass2_{ColorUtility.ToHtmlStringRGBA(c)}";
            return Get(key, () =>
            {
                var m = new Material(LitShader);
                m.SetColor("_BaseColor", c);
                // low smoothness: a glossy pane mirrors the bright ceiling and washes out the view
                m.SetFloat("_Smoothness", 0.3f);
                MakeTransparent(m);
                return m;
            });
        }

        public static Material UnlitTransparent(Color c)
        {
            var key = $"UnlitT_{ColorUtility.ToHtmlStringRGBA(c)}";
            return Get(key, () =>
            {
                var m = new Material(UnlitShader);
                m.SetColor("_BaseColor", c);
                MakeTransparent(m);
                m.SetFloat("_Cull", (float)CullMode.Off);
                return m;
            });
        }

        public static Material Unlit(Color c)
        {
            var key = $"Unlit_{ColorUtility.ToHtmlStringRGB(c)}";
            return Get(key, () =>
            {
                var m = new Material(UnlitShader);
                m.SetColor("_BaseColor", c);
                return m;
            });
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        static Material Get(string key, Func<Material> make)
        {
            if (Cache.TryGetValue(key, out var m) && m) return m;
            m = make();
            m.name = key;
            if (Persist != null) m = Persist(m, key);
            Cache[key] = m;
            return m;
        }

        public static void ClearCache() => Cache.Clear();
    }

    public static class Prims
    {
        /// <summary>Primitive without its collider (unless asked), coloured and parented.</summary>
        public static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 size,
            Material material, bool keepCollider = false, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Kill(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Color color,
            bool collider = true, Quaternion? rotation = null) =>
            Make(PrimitiveType.Cube, parent, name, localPos, size, Mats.Lit(color), collider, rotation);

        public static void Kill(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
