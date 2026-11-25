using System;
using System.IO;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEngine;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Paints everything people are made of and saves it as texture arrays: 16 tileable fabrics (albedo with
    /// smoothness in alpha, plus normals), 16 faces (painted front projections; alpha says how much the skin
    /// tint applies) and 4 hair strand patterns. Builds the Character and Hair materials and the
    /// <see cref="HumanStyle"/> asset in Resources that the runtime builder loads.
    /// Generated once; *Seoul First Week → Repaint Characters* paints everything again.
    /// </summary>
    public static class CharacterKit
    {
        public const string Folder = "Assets/_Project/Art/Generated/Characters";
        const string StylePath = "Assets/_Project/Resources/HumanStyle.asset";
        const int Tile = 512;

        [MenuItem("Seoul First Week/Repaint Characters")]
        public static void Repaint() => Ensure(true);

        /// <summary>Makes sure the character textures, materials and style asset exist.</summary>
        public static HumanStyle Ensure(bool force = false)
        {
            EnsureFolder(Folder);
            string fabrics = $"{Folder}/Fabrics.png", normals = $"{Folder}/FabricNormals.png";
            string faces = $"{Folder}/Faces.png", hair = $"{Folder}/HairStrands.png";
            bool paintFabrics = force || !File.Exists(fabrics) || !File.Exists(normals);
            bool paintFaces = force || !File.Exists(faces);
            bool paintHair = force || !File.Exists(hair);
            if (paintFabrics) PaintFabrics(fabrics, normals);
            if (paintFaces) PaintFaces(faces);
            if (paintHair) PaintHair(hair);
            var fabricArr = ImportArray(fabrics, paintFabrics, true, 4, 4, TextureWrapMode.Repeat);
            var normalArr = ImportArray(normals, paintFabrics, false, 4, 4, TextureWrapMode.Repeat);
            var faceArr = ImportArray(faces, paintFaces, true, 4, 4, TextureWrapMode.Clamp);
            var hairArr = ImportArray(hair, paintHair, false, 2, 2, TextureWrapMode.Repeat);

            var character = MaterialAt($"{Folder}/Character.mat", "SeoulFirstWeek/Character");
            character.SetTexture("_FabricArray", fabricArr);
            character.SetTexture("_NormalArray", normalArr);
            character.SetTexture("_FaceArray", faceArr);
            character.SetFloat("_BumpScale", 1f);
            character.SetFloat("_Smoothness", 1f);
            character.enableInstancing = false;
            EditorUtility.SetDirty(character);

            var hairMat = MaterialAt($"{Folder}/Hair.mat", "SeoulFirstWeek/Hair");
            hairMat.SetTexture("_HairArray", hairArr);
            hairMat.SetFloat("_Smoothness", 1f);
            EditorUtility.SetDirty(hairMat);

            var lens = MaterialAt($"{Folder}/Lens.mat", "Universal Render Pipeline/Lit");
            lens.SetColor("_BaseColor", new Color(0.82f, 0.9f, 1f, 0.18f));
            lens.SetFloat("_Smoothness", 0.92f);
            lens.SetFloat("_Surface", 1);
            lens.SetFloat("_Blend", 0);
            lens.SetOverrideTag("RenderType", "Transparent");
            lens.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lens.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lens.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            lens.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lens.SetFloat("_ZWrite", 0);
            lens.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            lens.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            lens.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(lens);

            EnsureFolder(Path.GetDirectoryName(StylePath)!.Replace('\\', '/'));
            var style = AssetDatabase.LoadAssetAtPath<HumanStyle>(StylePath);
            if (!style)
            {
                style = ScriptableObject.CreateInstance<HumanStyle>();
                AssetDatabase.CreateAsset(style, StylePath);
            }
            style.character = character;
            style.hair = hairMat;
            style.lens = lens;
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            return style;
        }

        static Material MaterialAt(string path, string shader)
        {
            var sh = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        /// <summary>Imports a grid atlas as a texture array (slices left to right, top to bottom).</summary>
        static Texture2DArray ImportArray(string path, bool repainted, bool srgb, int cols, int rows, TextureWrapMode wrap)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp || repainted)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                imp = (TextureImporter)AssetImporter.GetAtPath(path);
            }
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            bool changed = s.textureShape != TextureImporterShape.Texture2DArray || s.flipbookColumns != cols || s.flipbookRows != rows
                           || imp.sRGBTexture != srgb || imp.wrapMode != wrap || imp.textureType != TextureImporterType.Default;
            if (changed)
            {
                imp.textureType = TextureImporterType.Default;
                s.textureType = TextureImporterType.Default;
                s.textureShape = TextureImporterShape.Texture2DArray;
                s.flipbookColumns = cols;
                s.flipbookRows = rows;
                imp.SetTextureSettings(s);
                imp.sRGBTexture = srgb;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = false;
                imp.wrapMode = wrap;
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 4;
                imp.maxTextureSize = 4096;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        }

        // ================================================================== noise (periodic, so every tile repeats)

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Noise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            int Wx(int v) => (v % px + px) % px;
            int Wy(int v) => (v % py + py) % py;
            float a = Hash(Wx(x0), Wy(y0), seed), b = Hash(Wx(x0 + 1), Wy(y0), seed);
            float c = Hash(Wx(x0), Wy(y0 + 1), seed), d = Hash(Wx(x0 + 1), Wy(y0 + 1), seed);
            float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Fractal noise over a tile (u, v in 0..1) that repeats every 1; px, py lattice cells per repeat.</summary>
        static float Fbm(float u, float v, int px, int py, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u * px, v * py, px, py, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                px *= 2;
                py *= 2;
            }
            return sum / norm;
        }

        static float Fbm(float u, float v, int period, int octaves, int seed) => Fbm(u, v, period, period, octaves, seed);

        /// <summary>Periodic cellular noise: distance to the nearest feature point (f1) and to the second one (f2), in cells.</summary>
        static void Worley(float u, float v, int cells, int seed, out float f1, out float f2)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            f1 = f2 = 9f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = (gx % cells + cells) % cells, wy = (gy % cells + cells) % cells;
                float px = gx + Hash(wx, wy, seed), py = gy + Hash(wx, wy, seed + 7);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                }
                else if (d < f2) f2 = d;
            }
        }

        static float S(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
        static float Frac(float x) => x - Mathf.Floor(x);
        static float Tri(float x) => 1f - Mathf.Abs(Frac(x) * 2f - 1f); // 0..1..0 triangle wave

        // ================================================================== fabrics

        struct Texel
        {
            public float Albedo, Smooth, Height;
            public Color Tint; // multiplies the albedo; white for most fabrics

            public Texel(float albedo, float smooth, float height, Color? tint = null)
            {
                Albedo = albedo;
                Smooth = smooth;
                Height = height;
                Tint = tint ?? Color.white;
            }
        }

        // the skin slice and the faces share this base so the face texture meets the rest of the skin without a seam
        const float SkinBase = 0.92f;

        static Texel FabricTexel(Fabric f, float u, float v)
        {
            switch (f)
            {
                case Fabric.Skin:
                {
                    float mottle = Fbm(u, v, 6, 4, 11) - 0.5f;
                    Worley(u, v, 90, 12, out float pore, out _);
                    float pores = 1f - S(0.0f, 0.22f, pore);
                    float fine = Fbm(u, v, 64, 2, 13) - 0.5f;
                    float a = SkinBase + mottle * 0.03f - pores * 0.025f + fine * 0.01f;
                    var red = new Color(1f, 1f - mottle * 0.03f, 1f - mottle * 0.04f);
                    return new Texel(a, 0.42f - pores * 0.15f + fine * 0.05f, -pores * 0.6f + fine * 0.25f, red);
                }
                case Fabric.Jersey:
                {
                    // fine knit: stitch columns and courses
                    float wales = Tri(u * 56f);
                    float courses = Tri(v * 72f + Tri(u * 56f) * 0.5f);
                    float n = Fbm(u, v, 24, 3, 21) - 0.5f;
                    float h = wales * 0.6f + courses * 0.4f;
                    return new Texel(0.84f + h * 0.06f + n * 0.04f, 0.1f, h + n * 0.4f);
                }
                case Fabric.Knit:
                {
                    // rib knit: raised ribs made of V-shaped loops
                    float rib = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * u * 14f)), 0.6f);
                    float loopX = Frac(u * 28f);
                    float loop = Tri(v * 36f + Mathf.Abs(loopX - 0.5f) * 1.2f);
                    float n = Fbm(u, v, 16, 3, 22) - 0.5f;
                    float h = rib * 0.75f + loop * 0.3f * rib;
                    return new Texel(0.74f + h * 0.16f + n * 0.05f, 0.06f, h + n * 0.3f);
                }
                case Fabric.Denim:
                {
                    // twill diagonals, white weft flecks and vertical fading
                    float twill = Tri((u + v) * 64f);
                    float fleck = Fbm(u, v, 160, 2, 31);
                    float streak = Fbm(u, v, 6, 48, 3, 32) - 0.5f;
                    float wear = Fbm(u, v, 3, 2, 33) - 0.5f;
                    float a = 0.68f + twill * 0.11f + Mathf.Max(0, fleck - 0.6f) * 0.55f + streak * 0.12f + wear * 0.1f;
                    return new Texel(a, 0.12f, twill * 0.8f + fleck * 0.2f);
                }
                case Fabric.Wool:
                {
                    float felt = Fbm(u, v, 10, 5, 41) - 0.5f;
                    float fibre = Fbm(u, v, 96, 2, 42) - 0.5f;
                    float weave = Tri((u - v) * 40f) * 0.15f;
                    return new Texel(0.8f + felt * 0.1f + fibre * 0.06f + weave * 0.1f, 0.04f, felt * 0.8f + fibre * 0.5f + weave);
                }
                case Fabric.Twill:
                {
                    float twill = Tri((u + v) * 70f);
                    float n = Fbm(u, v, 20, 3, 51) - 0.5f;
                    return new Texel(0.8f + twill * 0.05f + n * 0.03f, 0.22f, twill * 0.6f + n * 0.3f);
                }
                case Fabric.Puffer:
                {
                    // quilted baffles: rounded channels with a stitched seam between them
                    float c = Frac(v * 4f);
                    float bulge = Mathf.Pow(Mathf.Sin(Mathf.PI * c), 0.55f);
                    float seam = 1f - S(0.0f, 0.06f, Mathf.Min(c, 1f - c));
                    float stitch = seam * (Tri(u * 90f) > 0.4f ? 1f : 0.4f);
                    float crease = (Fbm(u, v, 8, 3, 61) - 0.5f) * (1f - bulge) * 0.6f;
                    float a = 0.78f + bulge * 0.14f - seam * 0.2f + crease * 0.2f;
                    return new Texel(a, 0.32f + bulge * 0.3f, bulge * 2.2f - stitch * 0.4f + crease);
                }
                case Fabric.Leather:
                {
                    Worley(u, v, 46, 71, out float f1, out float f2);
                    float pebble = S(0f, 0.08f, f2 - f1);
                    float crease = Fbm(u, v, 5, 4, 72) - 0.5f;
                    return new Texel(0.8f + pebble * 0.05f + crease * 0.06f, 0.42f + pebble * 0.1f, pebble * 0.7f + crease * 0.4f);
                }
                case Fabric.Sneaker:
                {
                    float grain = Fbm(u, v, 40, 2, 81) - 0.5f;
                    float crease = Fbm(u, v, 4, 3, 82) - 0.5f;
                    return new Texel(0.9f + grain * 0.035f + crease * 0.03f, 0.35f, grain * 0.3f + crease * 0.5f);
                }
                case Fabric.Canvas:
                {
                    float wu = Mathf.Sin(Mathf.PI * 2f * u * 36f), wv = Mathf.Sin(Mathf.PI * 2f * v * 36f);
                    float weave = (wu * wv) * 0.5f + 0.5f;
                    float n = Fbm(u, v, 18, 3, 91) - 0.5f;
                    return new Texel(0.82f + weave * 0.08f + n * 0.06f, 0.1f, weave * 0.8f + n * 0.3f);
                }
                case Fabric.Poplin:
                {
                    float weave = Tri(u * 64f) * Tri(v * 64f);
                    float n = Fbm(u, v, 12, 3, 101) - 0.5f;
                    return new Texel(0.9f + weave * 0.03f + n * 0.025f, 0.2f, weave * 0.4f + n * 0.4f);
                }
                case Fabric.Corduroy:
                {
                    float wale = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * u * 12f)), 0.45f);
                    float n = Fbm(u, v, 12, 3, 111) - 0.5f;
                    return new Texel(0.68f + wale * 0.22f + n * 0.04f, 0.1f, wale + n * 0.2f);
                }
                case Fabric.Check:
                {
                    // tartan in greys: the tint gives the colour
                    float Band(float x) =>
                        S(0.02f, 0.0f, Mathf.Abs(Frac(x) - 0.25f) - 0.11f) * 0.5f + S(0.008f, 0f, Mathf.Abs(Frac(x) - 0.7f) - 0.015f) * 0.35f;
                    float Thin(float x) => S(0.004f, 0f, Mathf.Abs(Frac(x) - 0.52f) - 0.004f);
                    float dark = Band(u) + Band(v);
                    float light = Mathf.Max(Thin(u), Thin(v));
                    float twill = Tri((u + v) * 96f);
                    float a = 0.88f - dark * 0.28f + light * 0.1f + twill * 0.04f;
                    return new Texel(a, 0.15f, twill * 0.5f);
                }
                case Fabric.Nylon:
                {
                    float n = Fbm(u, v, 6, 5, 131);
                    float ridge = 1f - Mathf.Abs(n * 2f - 1f);
                    float fine = Fbm(u, v, 48, 2, 132) - 0.5f;
                    return new Texel(0.84f + ridge * 0.07f + fine * 0.02f, 0.5f + ridge * 0.1f, ridge * 1.2f + fine * 0.2f);
                }
                case Fabric.Satin:
                {
                    float fold = Fbm(u, v, 3, 4, 141) - 0.5f;
                    float sheen = Fbm(u, v, 2, 24, 2, 142) - 0.5f;
                    return new Texel(0.88f + fold * 0.06f + sheen * 0.03f, 0.62f, fold * 0.8f);
                }
                default: // Fleece: bouclé curls
                {
                    Worley(u, v, 34, 151, out float f1, out float f2);
                    float curl = 1f - S(0f, 0.7f, f1);
                    float fibre = Fbm(u, v, 64, 2, 152) - 0.5f;
                    return new Texel(0.8f + curl * 0.1f + fibre * 0.05f, 0.04f, curl * 1.2f + fibre * 0.4f);
                }
            }
        }

        static readonly float[] Relief =
        {
            1.2f, 1.6f, 3.2f, 1.4f, 1.4f, 0.9f, 3.4f, 1.4f,
            0.8f, 1.6f, 0.6f, 3f, 0.6f, 2.4f, 1.2f, 3f,
        };

        static void PaintFabrics(string albedoPath, string normalPath)
        {
            int n = Tile * 4;
            var albedo = new Color[n * n];
            var normal = new Color[n * n];
            var height = new float[Tile * Tile];
            for (int k = 0; k < 16; k++)
            {
                var f = (Fabric)k;
                int x0 = k % 4 * Tile, y0 = (3 - k / 4) * Tile;
                for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    var t = FabricTexel(f, (x + 0.5f) / Tile, (y + 0.5f) / Tile);
                    float a = Mathf.Clamp01(t.Albedo);
                    albedo[(y0 + y) * n + x0 + x] = new Color(a * t.Tint.r, a * t.Tint.g, a * t.Tint.b, Mathf.Clamp01(t.Smooth));
                    height[y * Tile + x] = t.Height;
                }
                float relief = Relief[k] * Tile / 512f;
                for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float H(int xx, int yy) => height[(yy + Tile) % Tile * Tile + (xx + Tile) % Tile];
                    float dx = H(x + 1, y) - H(x - 1, y);
                    float dy = H(x, y + 1) - H(x, y - 1);
                    var nrm = new Vector3(-dx * relief * 0.25f, -dy * relief * 0.25f, 1f).normalized;
                    normal[(y0 + y) * n + x0 + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
                }
            }
            Write(albedoPath, albedo, n, n);
            Write(normalPath, normal, n, n);
        }

        static void Write(string path, Color[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ================================================================== hair strands

        static void PaintHair(string path)
        {
            int n = Tile * 2;
            var px = new Color[n * n];
            for (int k = 0; k < 4; k++)
            {
                int x0 = k % 2 * Tile, y0 = (1 - k / 2) * Tile;
                for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float u = (x + 0.5f) / Tile, v = (y + 0.5f) / Tile;
                    float r, g;
                    switch ((HairTexture)k)
                    {
                        case HairTexture.Straight:
                        case HairTexture.Wavy:
                        {
                            float wave = k == 1 ? Mathf.Sin(v * Mathf.PI * 2f * 5f) * 0.012f : 0f;
                            float drift = (Fbm(u, v, 3, 2, 2, 201) - 0.5f) * 0.03f;
                            float su = u + wave + drift;
                            float strand = Fbm(su, v, 220, 4, 1, 202);
                            float clump = Fbm(su, v, 26, 2, 2, 203);
                            float fine = Fbm(su, v, 600, 6, 1, 204);
                            r = 0.35f + strand * 0.4f + clump * 0.25f + (fine - 0.5f) * 0.2f;
                            g = Fbm(su, v, 18, 3, 2, 205);
                            break;
                        }
                        case HairTexture.Curly:
                        {
                            Worley(u, v, 20, 211, out float f1, out float f2);
                            float ring = Mathf.Abs(Mathf.Sin(f1 * 9f));
                            float n2 = Fbm(u, v, 40, 3, 212);
                            r = 0.3f + ring * 0.45f + (n2 - 0.5f) * 0.3f + S(0f, 0.15f, f2 - f1) * 0.15f;
                            g = Fbm(u, v, 10, 3, 213);
                            break;
                        }
                        default: // buzz: short stubble, scalp showing between
                        {
                            float dots = Fbm(u, v, 260, 1, 221);
                            float n2 = Fbm(u, v, 30, 3, 222);
                            r = 0.25f + S(0.35f, 0.7f, dots) * 0.55f + (n2 - 0.5f) * 0.2f;
                            g = Fbm(u, v, 12, 2, 223);
                            break;
                        }
                    }
                    px[(y0 + y) * n + x0 + x] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), 0, 1);
                }
            }
            Write(path, px, n, n);
        }

        // ================================================================== faces

        enum Eye { Mono, InnerDouble, OuterDouble, Deep }

        class FaceSpec
        {
            public bool Female;
            public Eye Eye;
            public float EyeScale = 1f, Tilt = 0.06f;
            public float Brow = 1f, BrowArch;
            public Color BrowColor = new(0.12f, 0.09f, 0.075f);
            public bool Tattoo;
            public Color Lip = new(0.72f, 0.45f, 0.42f);
            public bool Gradient;
            public float LipFull = 1f;
            public bool Liner, Shadow, Aegyo, Blush;
            public float Stubble, Wrinkles;
            public int Moles;
            public int Seed;
        }

        static readonly FaceSpec[] Faces =
        {
            // 0-3: young and adult women
            new() { Female = true, Eye = Eye.InnerDouble, Liner = true, Aegyo = true, Blush = true, Gradient = true, Lip = new(0.8f, 0.3f, 0.35f), Brow = 0.9f, Seed = 1 },
            new() { Female = true, Eye = Eye.Mono, Liner = true, Blush = true, Lip = new(0.86f, 0.46f, 0.4f), Seed = 2 },
            new() { Female = true, Eye = Eye.OuterDouble, Shadow = true, Blush = true, Lip = new(0.74f, 0.41f, 0.42f), Brow = 0.95f, BrowArch = 0.15f, Seed = 3 },
            new() { Female = true, Eye = Eye.InnerDouble, Shadow = true, Lip = new(0.76f, 0.4f, 0.4f), Seed = 4, Moles = 1 },
            // 4: a woman from abroad (deeper-set eyes)
            new() { Female = true, Eye = Eye.Deep, Liner = true, Lip = new(0.72f, 0.42f, 0.4f), Brow = 1.15f, BrowArch = 0.4f, EyeScale = 1.1f, Seed = 5 },
            // 5: ajumma with tattooed brows; 6: grandmother
            new() { Female = true, Eye = Eye.InnerDouble, Tattoo = true, Lip = new(0.74f, 0.28f, 0.32f), Wrinkles = 0.45f, Brow = 1.1f, BrowColor = new(0.2f, 0.12f, 0.1f), Seed = 6, Moles = 1 },
            new() { Female = true, Eye = Eye.Mono, Tilt = -0.02f, Lip = new(0.7f, 0.48f, 0.46f), LipFull = 0.75f, Wrinkles = 1f, Brow = 0.7f, BrowColor = new(0.46f, 0.44f, 0.42f), Seed = 7, Moles = 2 },
            // 7: schoolgirl
            new() { Female = true, Eye = Eye.InnerDouble, Aegyo = true, Lip = new(0.8f, 0.5f, 0.48f), Brow = 0.95f, Seed = 8 },
            // 8-11: young and adult men
            new() { Eye = Eye.Mono, Brow = 1.25f, Lip = new(0.7f, 0.46f, 0.43f), Seed = 9 },
            new() { Eye = Eye.InnerDouble, Brow = 1.15f, Lip = new(0.72f, 0.47f, 0.44f), Seed = 10, Moles = 1 },
            new() { Eye = Eye.OuterDouble, Brow = 1.3f, Stubble = 0.5f, Lip = new(0.68f, 0.44f, 0.42f), Seed = 11 },
            new() { Eye = Eye.Mono, Brow = 1.2f, Lip = new(0.7f, 0.45f, 0.43f), Seed = 12 },
            // 12: ajeossi; 13: grandfather; 14: schoolboy; 15: a man from abroad
            new() { Eye = Eye.InnerDouble, Brow = 1.3f, Stubble = 0.35f, Wrinkles = 0.5f, Lip = new(0.66f, 0.43f, 0.41f), Seed = 13, Moles = 1 },
            new() { Eye = Eye.Mono, Tilt = -0.03f, Brow = 1.05f, BrowColor = new(0.5f, 0.48f, 0.46f), Wrinkles = 1f, LipFull = 0.8f, Lip = new(0.66f, 0.46f, 0.44f), Seed = 14, Moles = 2 },
            new() { Eye = Eye.InnerDouble, Brow = 1.1f, Lip = new(0.74f, 0.5f, 0.47f), Seed = 15 },
            new() { Eye = Eye.Deep, Brow = 1.4f, BrowArch = 0.25f, Stubble = 0.4f, EyeScale = 1.1f, Lip = new(0.66f, 0.43f, 0.41f), Seed = 16 },
        };

        /// <summary>A face canvas in metric coordinates (rig units relative to the head centre).</summary>
        class FaceCanvas
        {
            public readonly Color[] Px;
            public readonly int N;
            public readonly float Aa; // anti-aliasing width, rig units

            public FaceCanvas(int n)
            {
                N = n;
                Px = new Color[n * n];
                Aa = 2f * FaceMap.Rx / n;
            }

            /// <summary>Visits every pixel whose centre falls in the metric box.</summary>
            public void Region(float x0, float x1, float y0, float y1, Action<int, float, float> f)
            {
                var a = FaceMap.ToUV(x0, y0);
                var b = FaceMap.ToUV(x1, y1);
                int px0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) * N) - 1, 0, N - 1);
                int px1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) * N) + 1, 0, N - 1);
                int py0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) * N) - 1, 0, N - 1);
                int py1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) * N) + 1, 0, N - 1);
                for (int y = py0; y <= py1; y++)
                for (int x = px0; x <= px1; x++)
                {
                    var m = FaceMap.FromUV((x + 0.5f) / N, (y + 0.5f) / N);
                    f(y * N + x, m.x, m.y);
                }
            }

            /// <summary>Darkens or tints the skin (stays under the skin tint).</summary>
            public void Multiply(int i, Color c, float cov)
            {
                if (cov <= 0) return;
                var p = Px[i];
                Px[i] = new Color(Mathf.Lerp(p.r, p.r * c.r, cov), Mathf.Lerp(p.g, p.g * c.g, cov), Mathf.Lerp(p.b, p.b * c.b, cov), p.a);
            }

            /// <summary>Paints a colour; tint is how much the skin tint still multiplies it (0 = exact colour).</summary>
            public void Paint(int i, Color c, float tint, float cov)
            {
                if (cov <= 0) return;
                var p = Px[i];
                Px[i] = new Color(Mathf.Lerp(p.r, c.r, cov), Mathf.Lerp(p.g, c.g, cov), Mathf.Lerp(p.b, c.b, cov), Mathf.Lerp(p.a, tint, cov));
            }

            public float Cov(float sdf) => Mathf.Clamp01(0.5f - sdf / Aa);
        }

        static float Gauss(float dx, float dy, float sx, float sy) => Mathf.Exp(-(dx * dx) / (2 * sx * sx) - (dy * dy) / (2 * sy * sy));

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Distance to a polyline and the position along it (0..1).</summary>
        static float PolyDist(Vector2 p, Vector2[] pts, out float along)
        {
            float best = 9f, total = 0, bestAt = 0;
            var lens = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++)
            {
                lens[i] = total += (pts[i] - pts[i - 1]).magnitude;
            }
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1];
                var ab = pts[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
                float d = (p - (a + ab * t)).magnitude;
                if (d < best)
                {
                    best = d;
                    bestAt = (lens[i - 1] + ab.magnitude * t) / Mathf.Max(1e-9f, total);
                }
            }
            along = bestAt;
            return best;
        }

        static Vector2[] Curve(Func<float, Vector2> f, int n = 24)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = f(i / (float)n);
            return pts;
        }

        /// <summary>Strokes a polyline with a width that varies along it.</summary>
        static void Stroke(FaceCanvas c, Vector2[] pts, Func<float, float> halfWidth, Action<int, float, float> apply, float pad = 0.004f)
        {
            float x0 = 9, x1 = -9, y0 = 9, y1 = -9;
            foreach (var p in pts)
            {
                x0 = Mathf.Min(x0, p.x);
                x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, p.y);
                y1 = Mathf.Max(y1, p.y);
            }
            c.Region(x0 - pad, x1 + pad, y0 - pad, y1 + pad, (i, x, y) =>
            {
                float d = PolyDist(new Vector2(x, y), pts, out float along);
                apply(i, c.Cov(d - halfWidth(along)), along);
            });
        }

        static void PaintFaces(string path)
        {
            int n = Tile * 4;
            var atlas = new Color[n * n];
            for (int k = 0; k < Faces.Length; k++)
            {
                var c = PaintFace(Faces[k]);
                int x0 = k % 4 * Tile, y0 = (3 - k / 4) * Tile;
                for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                    atlas[(y0 + y) * n + x0 + x] = c.Px[y * Tile + x];
            }
            Write(path, atlas, n, n);
        }

        static FaceCanvas PaintFace(FaceSpec f)
        {
            var c = new FaceCanvas(Tile);
            int seed = 300 + f.Seed * 17;

            // base skin: the same tone and grain as the skin fabric, with warmer cheeks, nose and lips area
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float u = (x + 0.5f) / Tile, v = (y + 0.5f) / Tile;
                var m = FaceMap.FromUV(u, v);
                float mottle = Fbm(u * 2f, v * 2f, 6, 4, seed) - 0.5f;
                float fine = Fbm(u, v, 96, 2, seed + 1) - 0.5f;
                float a = SkinBase + mottle * 0.025f + fine * 0.012f;
                float warm = Gauss(m.x - 0.045f, m.y + 0.018f, 0.016f, 0.012f) + Gauss(m.x + 0.045f, m.y + 0.018f, 0.016f, 0.012f)
                             + Gauss(m.x, m.y - FaceMap.NoseTipY, 0.008f, 0.01f) * 0.7f;
                c.Px[y * Tile + x] = new Color(a, a * (1f - warm * 0.02f), a * (1f - warm * 0.03f), 1f);
            }

            // ---- planes of the face as drawn shadow shapes (hard-edged, like ink on a comic page, never a
            // gradient): the far sides of the face, the eye sockets and the shadow under the chin
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                var m = FaceMap.FromUV((x + 0.5f) / Tile, (y + 0.5f) / Tile);
                float ax = Mathf.Abs(m.x);
                float sides = S(0.071f, 0.074f, ax);
                float socket = S(0.55f, 0.6f, Gauss(ax - FaceMap.EyeX, m.y - FaceMap.EyeY - 0.004f, 0.016f, 0.009f));
                float underChin = S(-0.104f, -0.108f, m.y);
                float shade = sides * 0.05f + socket * 0.035f + underChin * 0.07f;
                var p = c.Px[y * Tile + x];
                c.Px[y * Tile + x] = new Color(p.r * (1 - shade), p.g * (1 - shade * 1.08f), p.b * (1 - shade * 1.1f), p.a);
            }

            // ---- brows
            foreach (float s in new[] { -1f, 1f })
            {
                float thick = 0.0029f * f.Brow * (f.Tattoo ? 1.2f : 1f);
                var pts = Curve(t =>
                {
                    float x = Mathf.Lerp(FaceMap.BrowInnerX, FaceMap.BrowOuterX, t);
                    float y = FaceMap.BrowY - 0.0012f + Mathf.Sin(t * Mathf.PI * 0.85f) * 0.0028f * (0.4f + f.BrowArch) - t * t * 0.0025f;
                    return new Vector2(s * x, y);
                });
                Stroke(c, pts, t => thick * (t < 0.15f ? Mathf.Lerp(0.85f, 1f, t / 0.15f) : Mathf.Lerp(1f, 0.38f, (t - 0.15f) / 0.85f)), (i, cov, t) =>
                {
                    if (cov <= 0) return;
                    // an inked brow: one solid tapered stroke of flat ink with a crisp edge, no hairs or smudge
                    c.Paint(i, f.BrowColor, 0f, S(0.25f, 0.6f, cov));
                });
            }

            // ---- eyes
            foreach (float s in new[] { -1f, 1f }) PaintEye(c, f, s, seed);

            // ---- nose
            c.Region(-0.02f, 0.02f, FaceMap.NoseBaseY - 0.008f, FaceMap.BridgeTopY, (i, x, y) =>
            {
                float side = Gauss(Mathf.Abs(x) - Mathf.Lerp(0.0105f, 0.0065f, S(FaceMap.NoseTipY, 0.012f, y)), 0, 0.0042f, 1)
                             * S(0.014f, -0.004f, y) * S(FaceMap.NoseBaseY + 0.002f, FaceMap.NoseTipY, y);
                c.Multiply(i, new Color(0.9f, 0.86f, 0.85f), side * 0.4f);
                float tip = Gauss(x, y - FaceMap.NoseTipY - 0.003f, 0.004f, 0.004f);
                c.Paint(i, new Color(0.97f, 0.95f, 0.94f), 1f, tip * 0.25f);
                float under = Gauss(x, y - FaceMap.NoseBaseY + 0.0035f, 0.009f, 0.0025f);
                c.Multiply(i, new Color(0.8f, 0.72f, 0.7f), under * 0.55f);
            });
            foreach (float s in new[] { -1f, 1f })
            {
                // nostrils and the crease of the nose wings
                c.Region(s * 0.0075f - 0.005f, s * 0.0075f + 0.005f, FaceMap.NoseBaseY - 0.003f, FaceMap.NoseBaseY + 0.004f, (i, x, y) =>
                {
                    float dx = (x - s * 0.0074f) / 0.0033f, dy = (y - FaceMap.NoseBaseY - 0.0012f) / 0.0015f;
                    float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * 0.0015f;
                    c.Paint(i, new Color(0.34f, 0.19f, 0.17f), 0.5f, c.Cov(d) * 0.9f);
                });
                var wing = Curve(t => new Vector2(s * (0.0105f + Mathf.Sin(t * Mathf.PI) * 0.0028f), Mathf.Lerp(FaceMap.NoseBaseY + 0.007f, FaceMap.NoseBaseY - 0.0005f, t)), 12);
                Stroke(c, wing, _ => 0.0005f, (i, cov, _) => c.Multiply(i, new Color(0.78f, 0.68f, 0.66f), cov * 0.7f));
            }

            // ---- philtrum, mouth and chin
            foreach (float s in new[] { -1f, 1f })
            {
                var ph = Curve(t => new Vector2(s * Mathf.Lerp(0.0028f, 0.0042f, t), Mathf.Lerp(FaceMap.NoseBaseY - 0.0055f, FaceMap.MouthY + 0.0068f, t)), 8);
                Stroke(c, ph, t => 0.0009f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 0.85f, t)), (i, cov, _) => c.Multiply(i, new Color(0.93f, 0.9f, 0.89f), cov * 0.3f));
            }
            PaintMouth(c, f);
            c.Region(-0.03f, 0.03f, FaceMap.MouthY - 0.03f, FaceMap.MouthY - 0.006f, (i, x, y) =>
            {
                float under = Gauss(x, y - FaceMap.MouthY + 0.0125f, 0.009f, 0.0025f);
                c.Multiply(i, new Color(0.88f, 0.84f, 0.83f), under * 0.6f);
            });

            // ---- cheeks
            if (f.Blush || f.Female)
                c.Region(-0.07f, 0.07f, -0.045f, 0.005f, (i, x, y) =>
                {
                    float b = Gauss(Mathf.Abs(x) - 0.046f, y + 0.017f, 0.012f, 0.009f);
                    c.Multiply(i, new Color(1f, 0.88f, 0.88f), b * (f.Blush ? 0.4f : 0.12f));
                });

            // ---- stubble along the jaw, chin and upper lip
            if (f.Stubble > 0)
                c.Region(-0.075f, 0.075f, -0.123f, FaceMap.NoseBaseY - 0.002f, (i, x, y) =>
                {
                    float ax = Mathf.Abs(x);
                    // the cheek line: high at the sideburns, dropping to the corners of the mouth
                    float cheekLine = Mathf.Lerp(FaceMap.MouthY + 0.002f, -0.012f, S(0.022f, 0.072f, ax));
                    float beard = S(cheekLine + 0.003f, cheekLine - 0.007f, y) * S(0.083f, 0.07f, ax);
                    float moustache = S(FaceMap.NoseBaseY - 0.003f, FaceMap.NoseBaseY - 0.007f, y) * S(FaceMap.MouthY + 0.004f, FaceMap.MouthY + 0.0075f, y) * S(0.025f, 0.015f, ax);
                    // keep the lips clear
                    float lips = S(0.006f, 0.0f, Mathf.Abs(y - FaceMap.MouthY + 0.0005f) - 0.006f) * S(FaceMap.MouthHalfW + 0.003f, FaceMap.MouthHalfW - 0.001f, ax);
                    float region = Mathf.Max(beard, moustache) * (1f - lips);
                    var uv0 = FaceMap.ToUV(x, y);
                    region *= Mathf.Lerp(0.6f, 1f, Fbm(uv0.x, uv0.y, 8, 3, seed + 10));
                    var uv = FaceMap.ToUV(x, y);
                    float dots = S(0.45f, 0.75f, Fbm(uv.x, uv.y, 180, 1, seed + 9));
                    c.Multiply(i, new Color(0.8f, 0.82f, 0.86f), region * f.Stubble * (0.35f + dots * 0.5f));
                });

            // ---- wrinkles: forehead lines, crow's feet, smile lines
            if (f.Wrinkles > 0)
            {
                for (int l = 0; l < 3; l++)
                {
                    float yl = 0.052f + l * 0.011f;
                    var line = Curve(t => new Vector2(Mathf.Lerp(-0.034f, 0.034f, t), yl + Mathf.Sin(t * Mathf.PI) * 0.0025f + Mathf.Sin(t * 13f + l) * 0.0006f), 20);
                    Stroke(c, line, t => 0.0005f * Mathf.Sin(t * Mathf.PI), (i, cov, _) => c.Multiply(i, new Color(0.86f, 0.82f, 0.81f), cov * f.Wrinkles * 0.8f));
                }
                foreach (float s in new[] { -1f, 1f })
                {
                    for (int l = -1; l <= 1; l++)
                    {
                        var crow = Curve(t => new Vector2(s * (FaceMap.EyeX + FaceMap.EyeHalfW + 0.002f + t * 0.007f), FaceMap.EyeY + l * 0.0025f + t * l * 0.002f), 6);
                        Stroke(c, crow, t => 0.00045f * (1f - t), (i, cov, _) => c.Multiply(i, new Color(0.84f, 0.8f, 0.79f), cov * f.Wrinkles));
                    }
                    var fold = Curve(t => new Vector2(s * (0.013f + t * t * 0.014f + t * 0.002f), Mathf.Lerp(FaceMap.NoseBaseY + 0.003f, FaceMap.MouthY - 0.01f, t)), 12);
                    Stroke(c, fold, t => 0.0011f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, t)), (i, cov, _) => c.Multiply(i, new Color(0.86f, 0.81f, 0.8f), cov * (0.15f + f.Wrinkles * 0.4f)));
                    var bag = Curve(t => new Vector2(s * Mathf.Lerp(FaceMap.EyeX - 0.01f, FaceMap.EyeX + 0.012f, t), FaceMap.EyeY - 0.0085f - Mathf.Sin(t * Mathf.PI) * 0.0022f), 10);
                    Stroke(c, bag, t => 0.0006f * Mathf.Sin(t * Mathf.PI), (i, cov, _) => c.Multiply(i, new Color(0.86f, 0.8f, 0.8f), cov * f.Wrinkles * 0.8f));
                }
            }

            // ---- moles
            var rng = new System.Random(seed);
            for (int m = 0; m < f.Moles; m++)
            {
                float mx = (float)(rng.NextDouble() * 0.09 - 0.045), my = (float)(rng.NextDouble() * 0.08 - 0.07);
                if (Mathf.Abs(mx) < 0.02f && my > FaceMap.MouthY - 0.01f) mx += 0.03f * Mathf.Sign(mx + 0.001f);
                c.Region(mx - 0.002f, mx + 0.002f, my - 0.002f, my + 0.002f, (i, x, y) =>
                    c.Paint(i, new Color(0.36f, 0.25f, 0.2f), 0.4f, c.Cov(new Vector2(x - mx, y - my).magnitude - 0.0006f) * 0.85f));
            }
            return c;
        }

        static void PaintEye(FaceCanvas c, FaceSpec f, float s, int seed)
        {
            float hw = FaceMap.EyeHalfW * f.EyeScale;
            float up = f.Eye switch { Eye.Mono => 0.0052f, Eye.Deep => 0.007f, _ => 0.0062f } * f.EyeScale;
            float lo = (f.Eye == Eye.Deep ? 0.0045f : 0.0038f) * f.EyeScale;
            float ex0 = s * FaceMap.EyeX, ey0 = FaceMap.EyeY;

            // eye-local x runs from the inner (-1) to the outer (+1) corner; the outer corner sits a little higher
            float Upper(float t) => up * Mathf.Pow(Mathf.Max(0, 1 - t * t), 0.75f) * (t < 0 ? Mathf.Lerp(1f, 0.8f, -t) : 1f) + f.Tilt * t * hw;
            float Lower(float t) => -lo * Mathf.Pow(Mathf.Max(0, 1 - t * t), 0.9f) + f.Tilt * t * hw * 0.55f - (t < -0.7f ? (t + 0.7f) * 0.004f : 0);
            float Local(float x) => (x - ex0) * s / hw;

            float irisR = 0.0056f * f.EyeScale, pupilR = 0.0023f * f.EyeScale;
            var irisC = new Vector2(ex0 - s * 0.0004f, ey0 + 0.0003f);
            var hl = new Vector2(irisC.x + 0.0016f, irisC.y + 0.0019f);
            var irisCol = new Color(0.27f, 0.16f, 0.095f);

            // eyeshadow and the lid fold
            if (f.Shadow || f.Liner)
                c.Region(ex0 - hw * 1.5f, ex0 + hw * 1.5f, ey0, ey0 + 0.012f, (i, x, y) =>
                {
                    float t = Local(x);
                    float above = y - (ey0 + Upper(Mathf.Clamp(t, -1, 1)));
                    float g = S(0.007f, 0f, above) * S(-0.0f, 0.001f, above) * S(1.4f, 0.9f, Mathf.Abs(t));
                    c.Multiply(i, f.Shadow ? new Color(0.93f, 0.85f, 0.82f) : new Color(0.95f, 0.9f, 0.88f), g * 0.8f);
                });
            if (f.Eye == Eye.Mono)
                c.Region(ex0 - hw * 1.3f, ex0 + hw * 1.3f, ey0, ey0 + 0.009f, (i, x, y) =>
                {
                    float t = Local(x);
                    float above = y - (ey0 + Upper(Mathf.Clamp(t, -1, 1)));
                    c.Multiply(i, new Color(0.93f, 0.9f, 0.89f), S(0.005f, 0f, above) * S(0f, 0.0008f, above) * S(1.2f, 0.8f, Mathf.Abs(t)) * 0.7f);
                });
            else
            {
                // the double-eyelid crease: joins the lash line at the inner corner for the in-line type
                float off = f.Eye == Eye.Deep ? 0.0042f : 0.0028f;
                var crease = Curve(t =>
                {
                    float lt = Mathf.Lerp(-0.9f, 1.02f, t);
                    float o = f.Eye == Eye.InnerDouble ? Mathf.Lerp(0.0007f, off, S(-0.9f, 0.1f, lt)) : off * Mathf.Lerp(0.85f, 1f, t);
                    return new Vector2(ex0 + s * lt * hw, ey0 + Upper(lt) + o);
                }, 20);
                Stroke(c, crease, t => 0.0005f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.1f, 0.95f, t)), (i, cov, _) => c.Multiply(i, new Color(0.8f, 0.72f, 0.7f), cov * 0.85f));
                if (f.Eye == Eye.Deep)
                    c.Region(ex0 - hw * 1.3f, ex0 + hw * 1.3f, ey0, ey0 + 0.012f, (i, x, y) =>
                    {
                        float t = Local(x);
                        float above = y - (ey0 + Upper(Mathf.Clamp(t, -1, 1)) + off);
                        c.Multiply(i, new Color(0.88f, 0.83f, 0.82f), S(0.006f, 0f, Mathf.Abs(above - 0.002f)) * S(1.3f, 0.8f, Mathf.Abs(t)) * 0.6f);
                    });
            }

            // the eye itself: white, iris, pupil, lid shadow, catchlight
            c.Region(ex0 - hw * 1.1f, ex0 + hw * 1.1f, ey0 - lo - 0.002f, ey0 + up + 0.002f + hw * f.Tilt, (i, x, y) =>
            {
                float t = Local(x);
                if (Mathf.Abs(t) > 1.02f) return;
                float yy = y - ey0;
                float dUp = Upper(t) - yy, dLo = yy - Lower(t);
                float inside = Mathf.Min(c.Cov(-dUp), c.Cov(-dLo)) * c.Cov((Mathf.Abs(t) - 1f) * hw);
                if (inside <= 0) return;
                var p = new Vector2(x, y);
                float rIris = (p - irisC).magnitude;
                var col = new Color(0.86f, 0.84f, 0.82f);
                if (t < -0.75f) col = Color.Lerp(col, new Color(0.84f, 0.6f, 0.58f), S(-0.75f, -0.95f, t)); // inner corner
                float iris = c.Cov(rIris - irisR);
                float rim = S(irisR * 0.65f, irisR, rIris);
                var ic = Color.Lerp(irisCol * 1.25f, irisCol * 0.55f, rim);
                ic = Color.Lerp(ic, new Color(0.04f, 0.03f, 0.03f), c.Cov(rIris - pupilR));
                col = Color.Lerp(col, ic, iris);
                // the upper lid shades the top of the eye
                float lidShadow = S(0.0035f, 0f, dUp);
                col *= Mathf.Lerp(1f, 0.62f, lidShadow);
                col = Color.Lerp(col, new Color(0.98f, 0.98f, 0.98f), c.Cov((p - hl).magnitude - 0.00085f));
                c.Paint(i, col, 0f, inside);
            });

            // upper lash line (with a short wing for eyeliner), lower lid line
            var lash = Curve(t =>
            {
                float lt = Mathf.Lerp(-0.98f, f.Liner ? 1.22f : 1.03f, t);
                float y = lt <= 1f ? Upper(lt) : Upper(1f) + (lt - 1f) * hw * 0.25f;
                return new Vector2(ex0 + s * lt * hw, ey0 + y + 0.0003f);
            }, 28);
            float lashW = (f.Female ? 0.0011f : 0.0008f) * (f.Liner ? 1.25f : 1f);
            Stroke(c, lash, t => lashW * (t < 0.3f ? Mathf.Lerp(0.45f, 1f, t / 0.3f) : t > 0.88f ? Mathf.Lerp(1.2f, 0.2f, (t - 0.88f) / 0.12f) : Mathf.Lerp(1f, 1.2f, (t - 0.3f) / 0.58f)),
                (i, cov, _) => c.Paint(i, new Color(0.06f, 0.045f, 0.045f), 0f, cov * 0.95f));
            var lower = Curve(t =>
            {
                float lt = Mathf.Lerp(-0.85f, 0.98f, t);
                return new Vector2(ex0 + s * lt * hw, ey0 + Lower(lt) - 0.0002f);
            }, 20);
            Stroke(c, lower, t => 0.0004f * S(0f, 0.4f, t), (i, cov, _) => c.Multiply(i, new Color(0.62f, 0.52f, 0.5f), cov * 0.7f));

            // aegyo-sal: the soft roll under the eye, lit on top and shaded beneath
            if (f.Aegyo)
                c.Region(ex0 - hw, ex0 + hw, ey0 - lo - 0.006f, ey0 - lo, (i, x, y) =>
                {
                    float t = Local(x);
                    float yy = y - ey0 - Lower(Mathf.Clamp(t, -1, 1));
                    float band = Gauss(0, yy + 0.0022f, 1, 0.001f) * S(1f, 0.5f, Mathf.Abs(t));
                    c.Paint(i, new Color(0.97f, 0.94f, 0.93f), 1f, band * 0.4f);
                    float shade = Gauss(0, yy + 0.0045f, 1, 0.0008f) * S(1f, 0.4f, Mathf.Abs(t));
                    c.Multiply(i, new Color(0.88f, 0.82f, 0.82f), shade * 0.5f);
                });
        }

        static void PaintMouth(FaceCanvas c, FaceSpec f)
        {
            float hw = FaceMap.MouthHalfW * (f.Female ? 0.92f : 1.05f);
            float upH = (f.Female ? 0.0062f : 0.0052f) * f.LipFull, loH = (f.Female ? 0.0076f : 0.0064f) * f.LipFull;
            float my = FaceMap.MouthY;
            float Line(float x) => my + 0.0004f * Mathf.Cos(x / hw * Mathf.PI) - 0.0004f * Mathf.Pow(Mathf.Abs(x / hw), 3f);
            float Top(float x)
            {
                float t = x / hw;
                float body = upH * Mathf.Pow(Mathf.Max(0, 1 - t * t), 0.55f);
                float bow = -0.0011f * Gauss(x, 0, 0.0026f, 1) + 0.0005f * (Gauss(Mathf.Abs(x) - 0.0045f, 0, 0.002f, 1));
                return Line(x) + body + bow;
            }
            float Bottom(float x)
            {
                float t = x / hw;
                return Line(x) - loH * Mathf.Pow(Mathf.Max(0, 1 - t * t), 0.5f);
            }
            c.Region(-hw - 0.002f, hw + 0.002f, my - loH - 0.002f, my + upH + 0.002f, (i, x, y) =>
            {
                float inside = Mathf.Min(c.Cov(y - Top(x)), c.Cov(Bottom(x) - y)) * c.Cov(Mathf.Abs(x) - hw);
                if (inside <= 0) return;
                float t = Mathf.Abs(x) / hw;
                var col = f.Lip;
                if (f.Gradient) col = Color.Lerp(new Color(f.Lip.r * 0.95f, f.Lip.g * 0.75f, f.Lip.b * 0.8f), new Color(0.84f, 0.6f, 0.56f), S(0.15f, 0.95f, t));
                bool upper = y > Line(x);
                col *= upper ? 0.9f : 1f;
                // a soft highlight on the lower lip
                float hl = Gauss(x - 0.003f, y - (my - loH * 0.45f), 0.004f, 0.0014f);
                col = Color.Lerp(col, Color.white, hl * 0.25f);
                c.Paint(i, col, 0.3f, inside * Mathf.Lerp(0.95f, 0.75f, S(0.7f, 1f, t)));
            });
            var line = Curve(t => new Vector2(Mathf.Lerp(-hw * 1.02f, hw * 1.02f, t), Line(Mathf.Lerp(-hw, hw, t))), 24);
            Stroke(c, line, t => 0.0005f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI)), (i, cov, _) => c.Paint(i, new Color(0.32f, 0.15f, 0.14f), 0.3f, cov * 0.85f));
            foreach (float s in new[] { -1f, 1f })
                c.Region(s * hw - 0.003f, s * hw + 0.003f, my - 0.003f, my + 0.003f, (i, x, y) =>
                    c.Multiply(i, new Color(0.78f, 0.7f, 0.68f), Gauss(x - s * hw, y - my, 0.0012f, 0.0012f) * 0.6f));
        }
    }
}
