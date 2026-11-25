using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Procedural surface textures: tileable albedo maps (smoothness in alpha) with matching normal
    /// maps, painted in code and saved as PNG assets, and the materials that use them. Every textured
    /// surface in the game comes from here so texel density, tone and wear stay consistent.
    /// Textures are generated once; delete the PNG or set <see cref="Force"/> to repaint.
    /// </summary>
    public static class TextureKit
    {
        public const string Folder = "Assets/_Project/Art/Generated/Textures";
        public const string SurfaceShader = "SeoulFirstWeek/Surface";

        /// <summary>Repaint textures even when their PNGs already exist.</summary>
        public static bool Force;

        /// <summary>An albedo + normal pair and the size in metres one repeat covers.</summary>
        public class Tex
        {
            public Texture2D Albedo, Normal;
            public float Metres;
        }

        static readonly Dictionary<string, Tex> Cache = new();

        public static void ClearCache() => Cache.Clear();

        // ------------------------------------------------------------------ noise (periodic, so textures tile)

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

        static float Noise(float x, float y, int period, int seed) => Noise(x, y, period, period, seed);

        /// <summary>Value noise on an integer lattice that wraps every px cells in x and py cells in y.</summary>
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

        /// <summary>Fractal noise over texture space (u, v in 0..1), repeating every 1.</summary>
        static float Fbm(float u, float v, int period, int octaves, int seed) => Fbm(u, v, period, period, octaves, seed);

        /// <summary>Fractal noise stretched along one axis: px and py cells per repeat in u and v.</summary>
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

        static float Smooth01(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));

        // ------------------------------------------------------------------ canvas

        class Canvas
        {
            public readonly int N;
            public readonly Color[] Col;
            public readonly float[] Smooth, Height;

            public Canvas(int n)
            {
                N = n;
                Col = new Color[n * n];
                Smooth = new float[n * n];
                Height = new float[n * n];
            }

            public int I(int x, int y) => (y % N + N) % N * N + (x % N + N) % N;
        }

        static void ForEach(Canvas c, Action<int, int, float, float> paint)
        {
            for (int y = 0; y < c.N; y++)
            for (int x = 0; x < c.N; x++)
                paint(x, y, (x + 0.5f) / c.N, (y + 0.5f) / c.N);
        }

        // ------------------------------------------------------------------ the inked look

        /// <summary>
        /// Turns a painted texture into an illustrated one before it is saved. Tone is cut into five flat
        /// steps around the average (so the material colour stays the same): faint noise flattens out while
        /// grains, chips, stains and tile-to-tile differences stay as crisp patches. Joints and seams in the
        /// height field are drawn as ink lines, the darkest patches get diagonal hatching, and close-range
        /// textures get a few drawn scuffs. Normal relief is calmed so the cel shading doesn't speckle.
        /// </summary>
        static void Ink(Canvas c, int seed, bool lines, float scuffs)
        {
            int n = c.N;
            var lum = new float[n * n];
            double sum = 0;
            for (int i = 0; i < lum.Length; i++)
            {
                var col = c.Col[i];
                lum[i] = Mathf.Max(1e-4f, col.r * 0.299f + col.g * 0.587f + col.b * 0.114f);
                sum += lum[i];
            }
            float mean = (float)(sum / lum.Length);
            var tone = BoxBlur(lum, n, Mathf.Max(1, n / 1024));

            float[] steps = { 0.6f, 0.8f, 0.93f, 1.0f, 1.08f };
            var stepped = new float[n * n];
            double sumNew = 0;
            for (int i = 0; i < lum.Length; i++)
            {
                float t = tone[i] / mean;
                float q = steps[0];
                for (int k = 1; k < steps.Length; k++)
                {
                    float mid = (steps[k - 1] + steps[k]) * 0.5f;
                    q = Mathf.Lerp(q, steps[k], Smooth01(mid - 0.008f, mid + 0.008f, t));
                }
                stepped[i] = q;
                sumNew += q;
            }
            float keep = (float)(lum.Length / sumNew);

            var h = c.Height;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = c.I(x, y);
                var col = c.Col[i] * (stepped[i] * keep * mean / lum[i]);

                // hatching over the darkest steps
                float dark = Smooth01(0.9f, 0.78f, stepped[i]);
                if (dark > 0 && ((x + y) % 6 + 6) % 6 < 1) col *= 1f - 0.16f * dark;

                // ink where the height drops into a joint or seam
                if (lines)
                {
                    float hx = h[c.I(x + 1, y)] - h[c.I(x - 1, y)], hy = h[c.I(x, y + 1)] - h[c.I(x, y - 1)];
                    float groove = Smooth01(0.16f, 0.45f, Mathf.Sqrt(hx * hx + hy * hy));
                    col = Color.Lerp(col, col * 0.3f, groove * 0.9f);
                }
                c.Col[i] = col;
            }

            // drawn scuffs and nicks: short curved pen strokes, only on textures seen up close
            if (scuffs > 0)
            {
                var rng = new System.Random(seed * 7919 + 17);
                int count = Mathf.RoundToInt(scuffs * n / 64f);
                float w = Mathf.Max(0.8f, n / 1200f);
                for (int k = 0; k < count; k++)
                {
                    float x0 = (float)rng.NextDouble() * n, y0 = (float)rng.NextDouble() * n;
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float len = n * (0.008f + (float)rng.NextDouble() * 0.03f);
                    float bend = ((float)rng.NextDouble() - 0.5f) * 1.6f;
                    float darkness = 0.12f + (float)rng.NextDouble() * 0.16f;
                    for (float t = 0; t <= 1f; t += 0.5f / len)
                    {
                        float a2 = ang + bend * t;
                        float px = x0 + Mathf.Cos(a2) * len * t, py = y0 + Mathf.Sin(a2) * len * t;
                        float press = Mathf.Sin(t * Mathf.PI);
                        for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            float dd = Mathf.Sqrt(ox * ox + oy * oy);
                            if (dd > w) continue;
                            int ii = c.I(Mathf.RoundToInt(px) + ox, Mathf.RoundToInt(py) + oy);
                            c.Col[ii] *= 1f - darkness * press * (1f - dd / (w + 1f));
                        }
                    }
                }
            }
        }

        static float[] BoxBlur(float[] src, int n, int r)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < n; y++)
            {
                float acc = 0;
                for (int k = -r; k <= r; k++) acc += src[y * n + ((k % n) + n) % n];
                for (int x = 0; x < n; x++)
                {
                    tmp[y * n + x] = acc * inv;
                    acc += src[y * n + (x + r + 1) % n] - src[y * n + ((x - r) % n + n) % n];
                }
            }
            for (int x = 0; x < n; x++)
            {
                float acc = 0;
                for (int k = -r; k <= r; k++) acc += tmp[(((k % n) + n) % n) * n + x];
                for (int y = 0; y < n; y++)
                {
                    dst[y * n + x] = acc * inv;
                    acc += tmp[((y + r + 1) % n) * n + x] - tmp[(((y - r) % n + n) % n) * n + x];
                }
            }
            return dst;
        }

        /// <summary>Textures whose height field is noise rather than joints get no ink lines.</summary>
        static bool LinesFor(string name) => name is not ("Roof_Concrete" or "Wall_Paint" or "Plastic" or "Fabric" or "Metal_Brushed");

        /// <summary>Close-range textures (pavement, floors, counters, walls, seats) get drawn scuffs.</summary>
        static float ScuffsFor(string name) =>
            name.StartsWith("Paving") ? 1.2f :
            name.StartsWith("Floor") ? 0.9f :
            name.StartsWith("Wood") ? 0.6f :
            name is "Wall_Paint" or "Plastic" ? 0.5f :
            name is "Metal_Brushed" ? 0.8f : 0f;

        static Tex Save(string name, Canvas c, float metres, float relief)
        {
            EnsureFolder(Folder);
            Ink(c, name.GetHashCode(), LinesFor(name), ScuffsFor(name));
            relief *= 0.7f;
            string albedoPath = $"{Folder}/{name}.png", normalPath = $"{Folder}/{name}_n.png";
            int n = c.N;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                var col = c.Col[i];
                px[i] = new Color(Mathf.Clamp01(col.r), Mathf.Clamp01(col.g), Mathf.Clamp01(col.b), Mathf.Clamp01(c.Smooth[i]));
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(albedoPath, tex.EncodeToPNG());

            // normals from the height field (central differences, wrapping at the edges)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = c.Height[c.I(x + 1, y)] - c.Height[c.I(x - 1, y)];
                float dy = c.Height[c.I(x, y + 1)] - c.Height[c.I(x, y - 1)];
                var nrm = new Vector3(-dx * relief, -dy * relief, 1f).normalized;
                px[y * n + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1);
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(normalPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            Import(albedoPath, false);
            Import(normalPath, true);
            return Load(name, metres);
        }

        static void Import(string path, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        static Tex Load(string name, float metres) => new()
        {
            Albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png"),
            Normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_n.png"),
            Metres = metres,
        };

        /// <summary>Returns the texture pair, painting it first if it doesn't exist yet.</summary>
        static Tex Get(string name, float metres, Func<Tex> paint)
        {
            if (Cache.TryGetValue(name, out var t) && t.Albedo) return t;
            if (!Force && File.Exists($"{Folder}/{name}.png") && File.Exists($"{Folder}/{name}_n.png"))
                t = Load(name, metres);
            else
                t = paint();
            Cache[name] = t;
            return t;
        }

        // ------------------------------------------------------------------ tiles, bricks, pavers, panels

        public class TileSpec
        {
            public int Size = 1024;
            public float Metres = 2f;                 // one repeat of the texture, in metres
            public float TileW = 0.4f, TileH = 0.2f;  // module size (adjusted to fit the repeat)
            public float Joint = 0.006f, Bevel = 0.003f;
            public float RowShift;                    // running bond: fraction of a tile each row shifts
            public Color[] Palette = { Color.gray };
            public float Jitter = 0.04f;              // brightness variation between tiles
            public float Grain = 0.05f;               // fine surface noise
            public int GrainPeriod = 96;
            public float Speckle;                     // granite grains (fraction of pixels)
            public Color SpeckleDark = new(0.18f, 0.18f, 0.19f), SpeckleLight = new(0.96f, 0.95f, 0.93f);
            public float Chips;                       // terrazzo aggregate
            public Color[] ChipColors;
            public float Veins;                       // marble veins
            public float TieHoles;                    // concrete form-tie holes, radius in metres
            public float Stains;                      // large blotches of dirt
            public Color JointColor = new(0.45f, 0.44f, 0.42f);
            public float Smooth = 0.3f, SmoothJitter = 0.05f, JointSmooth = 0.05f;
            public float Relief = 2f;
            public int Seed = 1;
        }

        static Tex Tiles(string name, TileSpec s) => Get(name, s.Metres, () =>
        {
            var c = new Canvas(s.Size);
            float m = s.Metres;
            int cols = Mathf.Max(1, Mathf.RoundToInt(m / s.TileW));
            int rows = Mathf.Max(1, Mathf.RoundToInt(m / s.TileH));
            if (s.RowShift != 0 && rows % 2 == 1) rows++;
            float tw = m / cols, th = m / rows, half = s.Joint / 2f;
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float px = u * m, py = v * m;
                int row = Mathf.FloorToInt(py / th);
                float ly = py - row * th;
                float sx = px + (row % 2) * s.RowShift * tw;
                int col = Mathf.FloorToInt(sx / tw);
                float lx = sx - col * tw;
                col = (col % cols + cols) % cols;
                row = (row % rows + rows) % rows;
                float edge = Mathf.Min(Mathf.Min(lx, tw - lx), Mathf.Min(ly, th - ly));
                float grain = Fbm(u, v, s.GrainPeriod, 3, s.Seed + 5) - 0.5f;
                float stain = s.Stains > 0 ? Smooth01(0.5f, 0.8f, Fbm(u, v, 4, 4, s.Seed + 9)) * s.Stains : 0f;

                if (edge < half)
                {
                    c.Col[i] = s.JointColor * (1f + grain * 0.4f) * (1f - stain * 0.5f);
                    c.Smooth[i] = s.JointSmooth;
                    c.Height[i] = 0.05f * grain;
                    return;
                }

                float h1 = Hash(col, row, s.Seed), h2 = Hash(col, row, s.Seed + 1), h3 = Hash(col, row, s.Seed + 2);
                var baseCol = s.Palette[Mathf.Min(s.Palette.Length - 1, (int)(h1 * s.Palette.Length))];
                var tint = baseCol * (1f + (h2 - 0.5f) * 2f * s.Jitter);
                tint *= 1f + grain * s.Grain * 2f;
                float smooth = s.Smooth + (h3 - 0.5f) * 2f * s.SmoothJitter;
                float height = Smooth01(half, half + s.Bevel, edge) * (0.9f + 0.1f * grain);

                if (s.Speckle > 0)
                {
                    // grains on a 2-pixel lattice so they survive mipmapping
                    float g = Hash(x / 2, y / 2, s.Seed + 11);
                    if (g < s.Speckle * 0.5f) tint = Color.Lerp(tint, s.SpeckleDark, 0.75f);
                    else if (g > 1f - s.Speckle * 0.5f) tint = Color.Lerp(tint, s.SpeckleLight, 0.6f);
                }
                if (s.Chips > 0 && s.ChipColors != null)
                {
                    const int period = 220;
                    float n = Noise(u * period, v * period, period, s.Seed + 21);
                    if (n > 1f - s.Chips)
                    {
                        int cell = (int)(Hash(Mathf.FloorToInt(u * period), Mathf.FloorToInt(v * period), s.Seed + 22) * s.ChipColors.Length);
                        tint = s.ChipColors[Mathf.Min(cell, s.ChipColors.Length - 1)] * (1f + grain * 0.3f);
                        smooth += 0.05f;
                    }
                }
                if (s.Veins > 0)
                {
                    float warp = Fbm(u + col * 0.37f, v + row * 0.61f, 3, 4, s.Seed + 31);
                    float vein = Mathf.Abs(Mathf.Sin((u * 3f + v + warp * 1.6f) * Mathf.PI * 2f));
                    tint = Color.Lerp(tint, tint * 0.72f, Smooth01(0.06f, 0f, vein) * s.Veins);
                }
                if (s.TieHoles > 0)
                {
                    foreach (var fx in new[] { 0.2f, 0.8f })
                    foreach (var fy in new[] { 0.25f, 0.75f })
                    {
                        float d = new Vector2(lx - fx * tw, ly - fy * th).magnitude;
                        if (d < s.TieHoles)
                        {
                            tint *= 0.55f;
                            height -= 0.5f * (1f - d / s.TieHoles);
                        }
                    }
                }
                c.Col[i] = tint * (1f - stain);
                c.Smooth[i] = Mathf.Clamp01(smooth - stain * 0.2f);
                c.Height[i] = height;
            });
            return Save(name, c, s.Metres, s.Relief);
        });

        // ------------------------------------------------------------------ continuous surfaces

        static Tex Plaster(string name, Color baseCol, float metres, float grain, float stains, float smooth, int seed, float relief = 3f,
            float streaks = 0f) => Get(name, metres, () =>
        {
            var c = new Canvas(name is "Wall_Paint" or "Plastic" ? 1024 : 512);
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float fine = Fbm(u, v, 128, 3, seed) - 0.5f;
                float blot = Smooth01(0.45f, 0.85f, Fbm(u, v, 3, 5, seed + 3));
                // rain streaks run down the wall
                float streak = streaks > 0 ? Smooth01(0.55f, 0.9f, Fbm(u, v, 24, 2, 2, seed + 7)) * streaks : 0f;
                c.Col[i] = baseCol * (1f + fine * grain * 2f) * (1f - blot * stains) * (1f - streak * 0.35f);
                c.Smooth[i] = smooth * (1f - blot * 0.3f);
                c.Height[i] = fine;
            });
            return Save(name, c, metres, relief);
        });

        static Tex Wood(string name, Color light, Color dark, float metres, float plank, float smooth, int seed) => Get(name, metres, () =>
        {
            var c = new Canvas(1024);
            int rows = Mathf.Max(1, Mathf.RoundToInt(metres / plank));
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                int row = Mathf.FloorToInt(v * rows);
                float lv = v * rows - row;
                // planks break at random points along their length
                float offset = Hash(row, 0, seed);
                float along = (u + offset) * 2f;
                int piece = Mathf.FloorToInt(along);
                float lu = along - piece;
                float pieceHash = Hash(row, piece % 2, seed + 1);
                float warp = Fbm(u * 1f, v, 8, 3, seed + 2 + row) * 0.08f;
                float ring = Mathf.Abs(Mathf.Sin((lv + warp * 6f + pieceHash) * Mathf.PI * (5f + pieceHash * 4f)));
                float fibre = Fbm(u, v * 1f, 64, 2, seed + 5) - 0.5f;
                var col = Color.Lerp(dark, light, Mathf.Pow(ring, 0.6f) * 0.7f + 0.3f * pieceHash);
                col *= 1f + fibre * 0.12f;
                bool seam = lv < 0.012f || lv > 0.988f || lu < 0.004f;
                c.Col[i] = seam ? col * 0.55f : col;
                c.Smooth[i] = seam ? smooth * 0.4f : smooth * (0.85f + 0.15f * ring);
                c.Height[i] = seam ? 0f : 0.6f + fibre * 0.2f;
            });
            return Save(name, c, metres, 1.5f);
        });

        static Tex Fabric(string name, Color baseCol, float metres, int threads, int seed) => Get(name, metres, () =>
        {
            var c = new Canvas(1024);
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float warp = Mathf.Sin(u * threads * Mathf.PI * 2f), weft = Mathf.Sin(v * threads * Mathf.PI * 2f);
                float over = (Mathf.Sign(Mathf.Sin(u * threads * Mathf.PI) * Mathf.Sin(v * threads * Mathf.PI)) + 1f) * 0.5f;
                float thread = over > 0.5f ? Mathf.Abs(warp) : Mathf.Abs(weft);
                float fuzz = Fbm(u, v, 64, 3, seed) - 0.5f;
                c.Col[i] = baseCol * (0.86f + 0.14f * thread + fuzz * 0.12f);
                c.Smooth[i] = 0.08f;
                c.Height[i] = thread * 0.5f + fuzz * 0.2f;
            });
            return Save(name, c, metres, 1.2f);
        });

        static Tex Brushed(string name, Color baseCol, float metres, float smooth, int seed) => Get(name, metres, () =>
        {
            var c = new Canvas(1024);
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float streak = Fbm(u, v, 2, 256, 3, seed) - 0.5f;
                float speck = Fbm(u, v, 32, 3, seed + 3) - 0.5f;
                c.Col[i] = baseCol * (1f + streak * 0.16f + speck * 0.05f);
                c.Smooth[i] = smooth * (1f + streak * 0.2f);
                c.Height[i] = streak * 0.3f;
            });
            return Save(name, c, metres, 1f);
        });

        static Tex Ceiling(string name) => Get(name, 1.2f, () =>
        {
            // 600 mm mineral fibre tiles ("텍스") on a white T-bar grid, with pinholes and fissures
            var c = new Canvas(1024);
            const float m = 1.2f, tile = 0.6f, bar = 0.012f;
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float px = u * m % tile, py = v * m % tile;
                float edge = Mathf.Min(Mathf.Min(px, tile - px), Mathf.Min(py, tile - py));
                if (edge < bar)
                {
                    c.Col[i] = new Color(0.93f, 0.93f, 0.92f);
                    c.Smooth[i] = 0.45f;
                    c.Height[i] = 1f;
                    return;
                }
                float fissure = Mathf.Abs(Fbm(u, v, 24, 3, 41) - 0.5f);
                float hole = Hash(x / 2, y / 2, 43);
                var col = new Color(0.9f, 0.9f, 0.88f) * (0.97f + (Fbm(u, v, 64, 2, 45) - 0.5f) * 0.08f);
                float h = 0.6f;
                if (fissure < 0.012f) { col *= 0.82f; h = 0.45f; }
                if (hole < 0.012f) { col *= 0.78f; h = 0.42f; }
                c.Col[i] = col;
                c.Smooth[i] = 0.04f;
                c.Height[i] = h - 0.25f * (1f - Smooth01(bar, bar + 0.006f, edge));
            });
            return Save(name, c, m, 2.5f);
        });

        static Tex RoofPaint(string name, Color paint, float metres, int seed) => Get(name, metres, () =>
        {
            // the urethane waterproofing paint on Seoul rooftops: smooth and a little glossy, bleached in
            // patches, with the slab joints showing through as lines
            var c = new Canvas(512);
            const float joint = 2f;
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float wear = Smooth01(0.35f, 0.75f, Fbm(u, v, 3, 4, seed));
                float fine = Fbm(u, v, 64, 2, seed + 8) - 0.5f;
                float px = u * metres % joint, py = v * metres % joint;
                bool seam = Mathf.Min(Mathf.Min(px, joint - px), Mathf.Min(py, joint - py)) < 0.012f;
                var col = Color.Lerp(paint, Color.Lerp(paint, new Color(0.72f, 0.76f, 0.68f), 0.35f), wear);
                col *= 1f + fine * 0.035f;
                c.Col[i] = seam ? col * 0.8f : col;
                c.Smooth[i] = seam ? 0.2f : 0.5f - wear * 0.2f;
                c.Height[i] = seam ? 0f : 0.5f + fine * 0.1f;
            });
            return Save(name, c, metres, 2f);
        });

        // ------------------------------------------------------------------ the catalogue

        static readonly Color Neutral = new(0.86f, 0.86f, 0.85f);

        /// <summary>Granite pavers of 명동길 in stretcher bond (mixed greys, speckled, gum-spotted).</summary>
        public static Tex Paving() => Tiles("Paving_Granite", new TileSpec
        {
            Size = 2048,
            Metres = 2.4f, TileW = 0.4f, TileH = 0.2f, Joint = 0.007f, Bevel = 0.004f, RowShift = 0.5f,
            Palette = new[] { new Color(0.72f, 0.71f, 0.68f), new Color(0.64f, 0.62f, 0.59f), new Color(0.7f, 0.66f, 0.61f), new Color(0.58f, 0.57f, 0.55f), new Color(0.75f, 0.74f, 0.71f) },
            Jitter = 0.06f, Grain = 0.08f, Speckle = 0.18f, Stains = 0.18f,
            JointColor = new Color(0.36f, 0.35f, 0.33f), Smooth = 0.22f, SmoothJitter = 0.06f, Relief = 2.2f, Seed = 11,
        });

        /// <summary>Asphalt of the side streets.</summary>
        public static Tex Asphalt() => Tiles("Asphalt", new TileSpec
        {
            Size = 512, Metres = 3f, TileW = 3f, TileH = 3f, Joint = 0f,
            Palette = new[] { new Color(0.25f, 0.25f, 0.26f) }, Grain = 0.25f, GrainPeriod = 160, Speckle = 0.25f,
            SpeckleDark = new Color(0.12f, 0.12f, 0.13f), SpeckleLight = new Color(0.5f, 0.5f, 0.5f), Stains = 0.3f,
            Smooth = 0.15f, SmoothJitter = 0f, Relief = 1.5f, Seed = 12,
        });

        public enum Facade { Tile, Brick, Stone, Concrete, Panel, Plaster }

        /// <summary>Facade cladding; light and neutral so each building's tint sets its colour (brick excepted).</summary>
        public static Tex FacadeTex(Facade f) => f switch
        {
            Facade.Tile => Tiles("Facade_Tile", new TileSpec
            {
                Metres = 1.8f, TileW = 0.227f, TileH = 0.06f, Joint = 0.006f, Bevel = 0.002f, RowShift = 0.5f,
                Palette = new[] { Neutral, Neutral * 0.95f, Neutral * 1.03f }, Jitter = 0.05f, Grain = 0.03f,
                JointColor = Neutral * 0.62f, Smooth = 0.45f, SmoothJitter = 0.08f, JointSmooth = 0.05f, Relief = 2.5f, Seed = 21,
            }),
            Facade.Brick => Tiles("Facade_Brick", new TileSpec
            {
                Metres = 1.6f, TileW = 0.2f, TileH = 0.067f, Joint = 0.01f, Bevel = 0.003f, RowShift = 0.5f,
                Palette = new[] { new Color(0.55f, 0.27f, 0.2f), new Color(0.62f, 0.33f, 0.24f), new Color(0.48f, 0.24f, 0.19f), new Color(0.58f, 0.36f, 0.28f), new Color(0.42f, 0.22f, 0.18f) },
                Jitter = 0.1f, Grain = 0.1f, Speckle = 0.08f, JointColor = new Color(0.66f, 0.64f, 0.6f),
                Smooth = 0.12f, SmoothJitter = 0.04f, Relief = 3f, Seed = 22,
            }),
            Facade.Stone => Tiles("Facade_Stone", new TileSpec
            {
                Metres = 2.4f, TileW = 0.6f, TileH = 0.8f, Joint = 0.006f, Bevel = 0.002f,
                Palette = new[] { Neutral, Neutral * 0.97f, Neutral * 1.02f }, Jitter = 0.03f, Grain = 0.05f, Speckle = 0.22f,
                SpeckleDark = new Color(0.35f, 0.35f, 0.36f), JointColor = Neutral * 0.55f, Smooth = 0.4f, Relief = 2f, Seed = 23,
            }),
            Facade.Concrete => Tiles("Facade_Concrete", new TileSpec
            {
                Metres = 2.4f, TileW = 1.2f, TileH = 0.6f, Joint = 0.008f, Bevel = 0.002f,
                Palette = new[] { Neutral * 0.92f, Neutral * 0.88f, Neutral * 0.95f }, Jitter = 0.04f, Grain = 0.1f, GrainPeriod = 48,
                TieHoles = 0.014f, Stains = 0.22f, JointColor = Neutral * 0.6f, Smooth = 0.18f, Relief = 2f, Seed = 24,
            }),
            Facade.Panel => Tiles("Facade_Panel", new TileSpec
            {
                Metres = 2.4f, TileW = 1.2f, TileH = 0.8f, Joint = 0.012f, Bevel = 0.003f,
                Palette = new[] { Neutral, Neutral * 0.98f }, Jitter = 0.02f, Grain = 0.015f,
                JointColor = new Color(0.2f, 0.2f, 0.21f), Smooth = 0.62f, SmoothJitter = 0.02f, Relief = 2f, Seed = 25,
            }),
            _ => Plaster("Facade_Plaster", Neutral, 2f, 0.05f, 0.1f, 0.12f, 26, 3f, 0.22f),
        };

        public static Tex RoofGreen() => RoofPaint("Roof_Green", new Color(0.3f, 0.52f, 0.36f), 6f, 31);
        public static Tex RoofConcrete() => Plaster("Roof_Concrete", new Color(0.6f, 0.6f, 0.58f), 4f, 0.12f, 0.3f, 0.15f, 32);

        // interiors
        public static Tex FloorVinyl() => Tiles("Floor_Vinyl", new TileSpec
        {
            Metres = 1.2f, TileW = 0.6f, TileH = 0.6f, Joint = 0.0015f, Bevel = 0.001f,
            Palette = new[] { Neutral, Neutral * 0.98f }, Jitter = 0.015f, Grain = 0.02f, Speckle = 0.08f,
            SpeckleDark = new Color(0.55f, 0.55f, 0.56f), SpeckleLight = new Color(0.98f, 0.98f, 0.97f),
            JointColor = Neutral * 0.8f, Smooth = 0.55f, SmoothJitter = 0.03f, JointSmooth = 0.3f, Relief = 1f, Seed = 41,
        });

        public static Tex FloorStone() => Tiles("Floor_Stone", new TileSpec
        {
            Metres = 1.2f, TileW = 0.6f, TileH = 0.6f, Joint = 0.002f, Bevel = 0.001f,
            Palette = new[] { Neutral, Neutral * 0.97f, Neutral * 1.02f }, Jitter = 0.03f, Grain = 0.04f, Veins = 0.8f,
            JointColor = Neutral * 0.7f, Smooth = 0.8f, SmoothJitter = 0.04f, JointSmooth = 0.3f, Relief = 1f, Seed = 42,
        });

        public static Tex FloorTerrazzo() => Tiles("Floor_Terrazzo", new TileSpec
        {
            Metres = 1.2f, TileW = 0.6f, TileH = 0.6f, Joint = 0.002f, Bevel = 0.001f,
            Palette = new[] { Neutral * 0.92f, Neutral * 0.9f }, Jitter = 0.02f, Grain = 0.03f, Chips = 0.2f,
            ChipColors = new[] { new Color(0.35f, 0.35f, 0.36f), new Color(0.97f, 0.97f, 0.96f), new Color(0.7f, 0.62f, 0.52f), new Color(0.2f, 0.2f, 0.21f) },
            JointColor = Neutral * 0.65f, Smooth = 0.7f, SmoothJitter = 0.03f, JointSmooth = 0.3f, Relief = 1f, Seed = 43,
        });

        /// <summary>500 mm carpet tiles with a fine loop pile (bank lounges, offices).</summary>
        public static Tex Carpet() => Tiles("Floor_Carpet", new TileSpec
        {
            Metres = 1f, TileW = 0.5f, TileH = 0.5f, Joint = 0.003f, Bevel = 0.001f,
            Palette = new[] { Neutral, Neutral * 0.95f }, Jitter = 0.025f, Grain = 0.22f, GrainPeriod = 220,
            JointColor = Neutral * 0.82f, Smooth = 0.03f, SmoothJitter = 0.01f, JointSmooth = 0.02f, Relief = 1.4f, Seed = 59,
        });

        /// <summary>Polished speckled granite in 600 mm slabs (bank halls).</summary>
        public static Tex FloorGranite() => Tiles("Floor_Granite", new TileSpec
        {
            Metres = 1.2f, TileW = 0.6f, TileH = 0.6f, Joint = 0.002f, Bevel = 0.001f,
            Palette = new[] { Neutral, Neutral * 0.96f, Neutral * 1.02f }, Jitter = 0.03f, Grain = 0.05f, Speckle = 0.3f,
            SpeckleDark = new Color(0.3f, 0.3f, 0.32f), SpeckleLight = new Color(0.98f, 0.97f, 0.95f),
            JointColor = Neutral * 0.62f, Smooth = 0.82f, SmoothJitter = 0.04f, JointSmooth = 0.3f, Relief = 1f, Seed = 60,
        });

        /// <summary>Perforated steel sheet of airport beam seating: round holes on a 10 mm grid.</summary>
        public static Tex Perforated() => Get("Metal_Perforated", 0.2f, () =>
        {
            var c = new Canvas(512);
            const float m = 0.2f, pitch = 0.01f, radius = 0.0033f;
            ForEach(c, (x, y, u, v) =>
            {
                int i = c.I(x, y);
                float px = u * m % pitch - pitch / 2f, py = v * m % pitch - pitch / 2f;
                float dist = Mathf.Sqrt(px * px + py * py);
                float streak = Fbm(u, v, 2, 64, 3, 61) - 0.5f;
                if (dist < radius)
                {
                    c.Col[i] = new Color(0.16f, 0.16f, 0.17f);
                    c.Smooth[i] = 0.05f;
                    c.Height[i] = 0f;
                    return;
                }
                c.Col[i] = Neutral * (1f + streak * 0.12f);
                c.Smooth[i] = 0.55f + streak * 0.1f;
                c.Height[i] = 0.6f - 0.3f * Smooth01(radius + 0.0012f, radius, dist);
            });
            return Save("Metal_Perforated", c, m, 1.5f);
        });

        public static Tex CeilingTiles() => Ceiling("Ceiling_Tex");
        public static Tex WallPaint() => Plaster("Wall_Paint", new Color(0.95f, 0.95f, 0.94f), 1f, 0.02f, 0.025f, 0.12f, 51, 2f);
        public static Tex WoodLight() => Wood("Wood_Light", new Color(0.84f, 0.68f, 0.48f), new Color(0.66f, 0.49f, 0.32f), 1.2f, 0.2f, 0.4f, 52);
        public static Tex WoodDark() => Wood("Wood_Dark", new Color(0.46f, 0.32f, 0.22f), new Color(0.3f, 0.2f, 0.14f), 1.2f, 0.2f, 0.45f, 53);
        public static Tex FabricWeave() => Fabric("Fabric", Neutral, 0.2f, 40, 54);
        public static Tex Metal() => Brushed("Metal_Brushed", Neutral, 0.5f, 0.7f, 55);
        public static Tex Plastic() => Plaster("Plastic", Neutral, 0.5f, 0.012f, 0f, 0.45f, 56, 1f);
        public static Tex Rubber() => Tiles("Mat_Rubber", new TileSpec
        {
            Size = 512, Metres = 0.4f, TileW = 0.02f, TileH = 0.4f, Joint = 0.006f, Bevel = 0.002f,
            Palette = new[] { new Color(0.24f, 0.24f, 0.25f) }, Jitter = 0f, Grain = 0.06f,
            JointColor = new Color(0.12f, 0.12f, 0.13f), Smooth = 0.15f, Relief = 3f, Seed = 57,
        });

        // ------------------------------------------------------------------ materials

        /// <summary>
        /// Triplanar lit material (<see cref="SurfaceShader"/>) for boxes, floors and props: the texture
        /// is projected in metres, so no UVs are needed. Updated in place so scenes keep their references.
        /// </summary>
        public static Material Surface(string name, Tex tex, Color tint, float smoothness = 1f, float metallic = 0f, bool objectSpace = false, float bump = 1f)
        {
            var m = MaterialAsset($"Surf_{name}", SurfaceShader);
            m.SetTexture("_BaseMap", tex.Albedo);
            m.SetTexture("_BumpMap", tex.Normal);
            m.SetTextureScale("_BaseMap", Vector2.one / tex.Metres);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_BumpScale", bump);
            m.SetFloat("_ObjectSpace", objectSpace ? 1 : 0);
            if (objectSpace) m.EnableKeyword("_TRIPLANAR_OBJECT");
            else m.DisableKeyword("_TRIPLANAR_OBJECT");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// The cel Surface shader on the mesh UVs (UVs in metres; tiling = 1 / metres): buildings and the
        /// pavement. The normal map shades the surface but stays out of the depth-normals texture, so the ink
        /// pass outlines the geometry and not every brick joint.
        /// </summary>
        public static Material LitUV(string name, Tex tex, Color tint, float smoothness = 1f, float metallic = 0f, float bump = 1f)
        {
            var m = MaterialAsset($"Lit_{name}", SurfaceShader);
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", tex.Albedo);
            m.SetTexture("_BumpMap", tex.Normal);
            m.SetFloat("_BumpScale", bump);
            m.SetTextureScale("_BaseMap", Vector2.one / tex.Metres);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_ObjectSpace", 0);
            m.SetFloat("_UVMapped", 1);
            m.EnableKeyword("_UV_MAPPED");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material MaterialAsset(string name, string shader)
        {
            EnsureFolder(MaterialFolder);
            var path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (!m)
            {
                m = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        [MenuItem("Seoul First Week/Repaint Textures")]
        public static void RepaintAll()
        {
            Force = true;
            ClearCache();
            try
            {
                Paving();
                Asphalt();
                foreach (Facade f in Enum.GetValues(typeof(Facade))) FacadeTex(f);
                RoofGreen();
                RoofConcrete();
                FloorVinyl();
                FloorStone();
                FloorTerrazzo();
                CeilingTiles();
                WallPaint();
                WoodLight();
                WoodDark();
                FabricWeave();
                Metal();
                Plastic();
                Rubber();
                RoomAtlas.Upper(true);
                RoomAtlas.Shops(true);
            }
            finally
            {
                Force = false;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
