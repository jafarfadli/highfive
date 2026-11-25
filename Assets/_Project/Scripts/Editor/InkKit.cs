using System.IO;
using SeoulFirstWeek.UI;
using UnityEditor;
using UnityEngine;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Paints the print texture of the inked UI and wires up its lettering. One tileable RGBA noise holds
    /// paper grain (R), dry-brush bristles (G) and stamp grunge (B); the UI Ink material samples it per vertex
    /// (see InkShape). The display faces get font fallbacks: Bangers falls back to Black Han Sans for Hangul,
    /// and both to Noto Sans KR. Saved as Resources/InkStyle.asset.
    /// Generated once; *Seoul First Week → Repaint UI Ink* paints it again.
    /// </summary>
    public static class InkKit
    {
        public const string Folder = "Assets/_Project/Art/Generated/UI";
        const string StylePath = "Assets/_Project/Resources/InkStyle.asset";
        const string FontFolder = "Assets/_Project/Resources/Fonts";
        const int Size = 512;

        [MenuItem("Seoul First Week/Repaint UI Ink")]
        public static void Repaint() => Ensure(true);

        public static InkStyle Ensure(bool force = false)
        {
            EnsureFolder(Folder);
            string noisePath = $"{Folder}/InkNoise.png";
            bool paint = force || !File.Exists(noisePath);
            if (paint)
            {
                File.WriteAllBytes(noisePath, PaintNoise().EncodeToPNG());
                AssetDatabase.ImportAsset(noisePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(noisePath);
            if (imp.sRGBTexture || imp.wrapMode != TextureWrapMode.Repeat || imp.textureCompression != TextureImporterCompression.Uncompressed)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = false;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath);

            var sh = Shader.Find("SeoulFirstWeek/UI Ink");
            string matPath = $"{Folder}/UIInk.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat)
            {
                mat = new Material(sh) { name = "UIInk" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != sh) mat.shader = sh;
            mat.SetTexture("_MainTex", noise);
            mat.SetFloat("_Grain", 1f);
            mat.SetFloat("_DotPitch", 9f);
            EditorUtility.SetDirty(mat);

            var bangers = Font("Bangers-Regular");
            var bhs = Font("BlackHanSans-Regular");
            var noto = Font("NotoSansKR-Bold");
            Fallbacks("Bangers-Regular", bhs, noto);
            Fallbacks("BlackHanSans-Regular", noto);

            var style = AssetDatabase.LoadAssetAtPath<InkStyle>(StylePath);
            if (!style)
            {
                style = ScriptableObject.CreateInstance<InkStyle>();
                AssetDatabase.CreateAsset(style, StylePath);
            }
            style.material = mat;
            style.noise = noise;
            style.display = Font("Bangers-Regular");
            style.displayKo = Font("BlackHanSans-Regular");
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            return style;
        }

        static Font Font(string file) => AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{file}.ttf");

        // ------------------------------------------------------------------ the 3D ink pass

        const string OutlineMaterialPath = Folder + "/InkOutline.mat";
        const string FeatureName = "Ink";

        /// <summary>
        /// Makes sure every URP renderer draws the comic ink pass: a Full Screen Pass renderer feature with the
        /// Ink Outline material (depth + normal edges, paper grain), after the opaque world and the sky and before
        /// anything transparent, so glass, world-space text and UI drawn by a camera stay clean of the lines.
        /// </summary>
        public static void EnsureOutline()
        {
            Ensure();
            var sh = Shader.Find("SeoulFirstWeek/Ink Outline");
            if (!sh)
            {
                Debug.LogWarning("[InkKit] Ink Outline shader not found");
                return;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (!mat)
            {
                mat = new Material(sh) { name = "InkOutline" };
                AssetDatabase.CreateAsset(mat, OutlineMaterialPath);
            }
            else if (mat.shader != sh) mat.shader = sh;
            mat.SetTexture("_GrainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/InkNoise.png"));
            // the line weights are owned here, so an older material can't keep stale values
            mat.SetColor("_InkColor", new Color(0.09f, 0.07f, 0.06f));
            mat.SetFloat("_InkStrength", 0.95f);
            mat.SetFloat("_LineWidth", 2.2f);
            mat.SetFloat("_DepthThreshold", 0.28f);
            mat.SetFloat("_NormalThreshold", 0.32f);
            mat.SetFloat("_FadeStart", 30f);
            mat.SetFloat("_FadeEnd", 110f);
            mat.SetFloat("_Grain", 0.05f);
            EditorUtility.SetDirty(mat);

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
                if (!data) continue;
                UnityEngine.Rendering.Universal.FullScreenPassRendererFeature feature = null;
                foreach (var f in data.rendererFeatures)
                    if (f is UnityEngine.Rendering.Universal.FullScreenPassRendererFeature fs && f.name == FeatureName)
                        feature = fs;
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
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
                feature.injectionPoint = UnityEngine.Rendering.Universal.FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
                feature.fetchColorBuffer = true;
                feature.requirements = UnityEngine.Rendering.Universal.ScriptableRenderPassInput.Depth | UnityEngine.Rendering.Universal.ScriptableRenderPassInput.Normal;
                feature.passMaterial = mat;
                feature.passIndex = 0;
                feature.bindDepthStencilAttachment = false;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Sets a dynamic font's fallbacks. Unity keeps the list only when the font's own family name comes
        /// first in fontNames, followed by the fallback families, with the fallback assets as references.
        /// </summary>
        static void Fallbacks(string file, params Font[] fallbacks)
        {
            var path = $"{FontFolder}/{file}.ttf";
            var imp = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            var own = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (!imp || !own) return;
            bool same = imp.fontReferences != null && imp.fontReferences.Length == fallbacks.Length && imp.includeFontData
                        && imp.fontTextureCase == FontTextureCase.Dynamic;
            if (same)
                for (int i = 0; i < fallbacks.Length; i++)
                    if (imp.fontReferences[i] != fallbacks[i]) same = false;
            if (same) return;
            var names = new System.Collections.Generic.List<string> { own.fontNames.Length > 0 ? own.fontNames[0] : own.name };
            foreach (var f in fallbacks)
                if (f) names.Add(f.fontNames.Length > 0 ? f.fontNames[0] : f.name);
            imp.fontTextureCase = FontTextureCase.Dynamic;
            imp.includeFontData = true;
            imp.fontNames = names.ToArray();
            imp.fontReferences = fallbacks;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ noise

        static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Tileable value noise: a lattice of <paramref name="cells"/> per side, smooth interpolation.</summary>
        static float Value(float x, float y, int cellsX, int cellsY, int salt)
        {
            float fx = x / Size * cellsX, fy = y / Size * cellsY;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            int ax = ((x0 % cellsX) + cellsX) % cellsX, bx = (ax + 1) % cellsX;
            int ay = ((y0 % cellsY) + cellsY) % cellsY, by = (ay + 1) % cellsY;
            float a = Mathf.Lerp(Hash(ax, ay, salt), Hash(bx, ay, salt), tx);
            float b = Mathf.Lerp(Hash(ax, by, salt), Hash(bx, by, salt), tx);
            return Mathf.Lerp(a, b, ty);
        }

        static float Fbm(float x, float y, int cells, int octaves, int salt) => Fbm(x, y, cells, cells, octaves, salt);

        /// <summary>Tileable fBm with its own lattice density along x and y (few along x = long streaks).</summary>
        static float Fbm(float x, float y, int cellsX, int cellsY, int octaves, int salt)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(x, y, cellsX << o, cellsY << o, salt + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static Texture2D PaintNoise()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            var px = new Color32[Size * Size];
            var fibre = new float[Size * Size];

            // paper fibres: short soft strokes in every direction, wrapped so the tile repeats
            var rng = new System.Random(4021);
            for (int f = 0; f < 2600; f++)
            {
                float x = (float)rng.NextDouble() * Size, y = (float)rng.NextDouble() * Size;
                float ang = (float)rng.NextDouble() * Mathf.PI;
                float len = 4f + (float)rng.NextDouble() * 14f;
                float dark = 0.04f + (float)rng.NextDouble() * 0.09f;
                for (float s = -len * 0.5f; s <= len * 0.5f; s += 0.5f)
                {
                    int ix = ((Mathf.RoundToInt(x + Mathf.Cos(ang) * s) % Size) + Size) % Size;
                    int iy = ((Mathf.RoundToInt(y + Mathf.Sin(ang) * s) % Size) + Size) % Size;
                    float taperK = 1f - Mathf.Abs(s) / (len * 0.5f);
                    fibre[iy * Size + ix] = Mathf.Max(fibre[iy * Size + ix], dark * (0.4f + 0.6f * taperK));
                }
            }

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // R: mottled paper with fibres and the odd speck
                float mottle = Fbm(x, y, 4, 5, 11);
                float fine = Value(x, y, 128, 128, 77);
                float speck = Hash(x, y, 909) > 0.9985f ? 0.22f : 0f;
                float paper = 1f - (0.07f * Mathf.Clamp01((mottle - 0.35f) * 2f) + 0.035f * fine + fibre[y * Size + x] + speck);

                // G: bristle streaks running along x; rows vary fast in y, slowly in x
                float rows = Fbm(x, y, 3, 48, 3, 201);
                float wander = Fbm(x, y, 2, 3, 233);
                float hairs = Value(x, y, 6, 192, 251);
                float bristle = Mathf.Clamp01((rows * 0.62f + hairs * 0.28f + wander * 0.25f - 0.08f) * 1.25f);

                // B: grunge blotches for stamps
                float grunge = Fbm(x, y, 16, 4, 307);
                grunge = Mathf.Clamp01((grunge - 0.5f) * 2.4f + 0.5f);

                px[y * Size + x] = new Color32((byte)(Mathf.Clamp01(paper) * 255), (byte)(bristle * 255), (byte)(grunge * 255), 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
