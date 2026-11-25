using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Painted back walls of the rooms seen through windows, for the "SeoulFirstWeek/Interior Window"
    /// shader. Each atlas is 4 x 2 cells. In every cell the bottom band (below 14%) is the floor colour
    /// and the top band (above 94%) the ceiling; the shader stretches those bands and the side columns
    /// into floor, ceiling and side walls, so keep furniture away from the cell edges.
    /// </summary>
    public static class RoomAtlas
    {
        public const int Columns = 4, Rows = 2;

        /// <summary>Upper floors: office, home, 학원 classroom, café, storage, PC방, clinic, 노래방.</summary>
        public static Texture2D Upper(bool force = false) => Make("Rooms_Upper", 256, force, PaintUpper);

        /// <summary>Street-level shops: cosmetics, convenience store, café, clothing, phones, restaurant, pharmacy, 분식.</summary>
        public static Texture2D Shops(bool force = false) => Make("Rooms_Shops", 512, force, PaintShop);

        static Texture2D Make(string name, int cell, bool force, Action<Painter, int> paint)
        {
            EnsureFolder(TextureKit.Folder);
            var path = $"{TextureKit.Folder}/{name}.png";
            if (!force && !TextureKit.Force && File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var p = new Painter(cell * Columns, cell * Rows, cell);
            for (int i = 0; i < Columns * Rows; i++)
            {
                p.Cell = i;
                p.Rng = new System.Random(name.Length * 97 + i * 13);
                paint(p, i);
            }
            var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, false);
            tex.SetPixels(p.Px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Draws in cell space: (0,0) bottom left, (1,1) top right of the current cell.</summary>
        class Painter
        {
            public readonly int W, H, Size;
            public readonly Color[] Px;
            public int Cell;
            public System.Random Rng;

            public Painter(int w, int h, int size)
            {
                W = w;
                H = h;
                Size = size;
                Px = new Color[w * h];
            }

            int Ox => Cell % Columns * Size;
            int Oy => Cell / Columns * Size;

            public float R() => (float)Rng.NextDouble();
            public float R(float a, float b) => Mathf.Lerp(a, b, R());

            public void Rect(float x0, float y0, float x1, float y1, Color c)
            {
                int ax = Mathf.Clamp(Mathf.RoundToInt(x0 * Size), 0, Size), bx = Mathf.Clamp(Mathf.RoundToInt(x1 * Size), 0, Size);
                int ay = Mathf.Clamp(Mathf.RoundToInt(y0 * Size), 0, Size), by = Mathf.Clamp(Mathf.RoundToInt(y1 * Size), 0, Size);
                for (int y = ay; y < by; y++)
                for (int x = ax; x < bx; x++)
                    Px[(Oy + y) * W + Ox + x] = c;
            }

            public void Gradient(float x0, float y0, float x1, float y1, Color bottom, Color top)
            {
                int ay = Mathf.RoundToInt(y0 * Size), by = Mathf.RoundToInt(y1 * Size);
                for (int y = ay; y < by; y++)
                {
                    float t = Mathf.InverseLerp(ay, by - 1, y);
                    Rect(x0, y / (float)Size, x1, (y + 1) / (float)Size, Color.Lerp(bottom, top, t));
                }
            }

            public void Frame(float x0, float y0, float x1, float y1, float t, Color c)
            {
                Rect(x0, y0, x1, y0 + t, c);
                Rect(x0, y1 - t, x1, y1, c);
                Rect(x0, y0, x0 + t, y1, c);
                Rect(x1 - t, y0, x1, y1, c);
            }

            public void Circle(float cx, float cy, float r, Color c)
            {
                for (float y = cy - r; y <= cy + r; y += 1f / Size)
                {
                    float half = Mathf.Sqrt(Mathf.Max(0, r * r - (y - cy) * (y - cy)));
                    Rect(cx - half, y, cx + half, y + 1f / Size, c);
                }
            }

            /// <summary>Rows of short dashes that read as text from a distance.</summary>
            public void Text(float x0, float y0, float x1, float y1, int lines, Color c, float weight = 0.45f)
            {
                float lh = (y1 - y0) / lines;
                for (int l = 0; l < lines; l++)
                {
                    float y = y1 - (l + 1) * lh + lh * (1 - weight) / 2f;
                    float x = x0;
                    float end = Mathf.Lerp(x0 + (x1 - x0) * 0.5f, x1, R());
                    while (x < end)
                    {
                        float w = R(0.015f, 0.05f) * (x1 - x0) * 3f;
                        Rect(x, y, Mathf.Min(x + w, end), y + lh * weight, c);
                        x += w + lh * 0.3f;
                    }
                }
            }

            /// <summary>Shelf with a row of packed products in the given colours.</summary>
            public void Shelf(float x0, float x1, float y, float height, Color board, Color[] goods, float minW = 0.012f, float maxW = 0.03f)
            {
                Rect(x0, y - 0.012f, x1, y, board);
                float x = x0 + 0.004f;
                while (x < x1 - 0.01f)
                {
                    float w = R(minW, maxW), h = height * R(0.5f, 1f);
                    var c = goods[Rng.Next(goods.Length)] * R(0.85f, 1.05f);
                    c.a = 1;
                    Rect(x, y, Mathf.Min(x + w, x1), y + h, c);
                    x += w + R(0.001f, 0.006f);
                }
            }

            /// <summary>Floor band, ceiling band (with a light strip when <paramref name="light"/> is set) and wall.</summary>
            public void Room(Color wall, Color floor, Color ceiling, Color? light, float grain = 0.04f)
            {
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float v = y / (float)Size;
                    float n = (Mathf.PerlinNoise((Ox + x) * 0.08f, (Oy + y) * 0.08f) - 0.5f) * grain;
                    var c = v < 0.14f ? floor : v > 0.94f ? ceiling : wall * Mathf.Lerp(1.04f, 0.94f, v);
                    c *= 1 + n;
                    c.a = 1;
                    Px[(Oy + y) * W + Ox + x] = c;
                }
                // skirting and the shadow line under the ceiling
                Rect(0, 0.14f, 1, 0.155f, floor * 0.7f);
                Rect(0, 0.925f, 1, 0.94f, wall * 0.8f);
                if (light is Color l) Rect(0.38f, 0.94f, 0.62f, 1f, l);
            }
        }


        static readonly Color[] Bright = { C("#FF6FA8"), C("#6FD6C3"), C("#B89CFF"), C("#FFFFFF"), C("#FFD36F"), C("#FF8A65"), C("#7FB7FF"), C("#222428") };

        static void PaintUpper(Painter p, int cell)
        {
            switch (cell)
            {
                case 0: // office
                    p.Room(C("#D8DBDC"), C("#8E9296"), C("#E8E9E8"), C("#FFFFFF"));
                    p.Rect(0.06f, 0.14f, 0.4f, 0.46f, C("#A5AAAE"));
                    for (float y = 0.22f; y < 0.46f; y += 0.08f) p.Rect(0.06f, y, 0.4f, y + 0.008f, C("#7E8388"));
                    p.Rect(0.45f, 0.14f, 0.94f, 0.33f, C("#8FA0B0"));
                    p.Rect(0.45f, 0.33f, 0.94f, 0.345f, C("#55606B"));
                    foreach (var x in new[] { 0.52f, 0.68f, 0.84f }) p.Rect(x - 0.05f, 0.345f, x + 0.05f, 0.43f, C("#2A2D33"));
                    p.Rect(0.5f, 0.55f, 0.9f, 0.78f, C("#F4F4F2"));
                    p.Frame(0.5f, 0.55f, 0.9f, 0.78f, 0.01f, C("#9A9EA3"));
                    p.Text(0.54f, 0.6f, 0.86f, 0.74f, 4, C("#3D6FD8"), 0.25f);
                    p.Circle(0.2f, 0.75f, 0.05f, C("#2B2D31"));
                    p.Circle(0.2f, 0.75f, 0.04f, C("#FAFAF7"));
                    break;
                case 1: // home
                    p.Room(C("#E6D9C3"), C("#9C7552"), C("#EEE8DC"), C("#FFF1D6"));
                    for (float x = 0.05f; x < 0.95f; x += 0.06f) p.Rect(x, 0.16f, x + 0.004f, 0.92f, C("#DDCDB3"));
                    p.Rect(0.58f, 0.14f, 0.94f, 0.82f, C("#8A6647"));
                    p.Rect(0.759f, 0.16f, 0.763f, 0.8f, C("#5E432E"));
                    p.Rect(0.72f, 0.46f, 0.735f, 0.54f, C("#D9C29A"));
                    p.Rect(0.785f, 0.46f, 0.8f, 0.54f, C("#D9C29A"));
                    p.Rect(0.06f, 0.14f, 0.5f, 0.3f, C("#6E8FB0"));
                    p.Rect(0.06f, 0.3f, 0.5f, 0.38f, C("#5B7797"));
                    p.Rect(0.15f, 0.5f, 0.38f, 0.68f, C("#C9A36B"));
                    p.Rect(0.165f, 0.515f, 0.365f, 0.665f, C("#3F5E78"));
                    break;
                case 2: // 학원 classroom
                    p.Room(C("#EEEEEA"), C("#B8AE9E"), C("#F2F2EE"), C("#FFFFFF"));
                    p.Rect(0.12f, 0.46f, 0.88f, 0.8f, C("#7A6A58"));
                    p.Rect(0.13f, 0.47f, 0.87f, 0.79f, C("#2F5B4D"));
                    p.Text(0.17f, 0.52f, 0.8f, 0.75f, 5, C("#DDE7E2"), 0.22f);
                    for (float x = 0.08f; x < 0.9f; x += 0.21f)
                    {
                        p.Rect(x, 0.2f, x + 0.16f, 0.235f, C("#C7B08A"));
                        p.Rect(x + 0.02f, 0.14f, x + 0.03f, 0.2f, C("#555A60"));
                        p.Rect(x + 0.13f, 0.14f, x + 0.14f, 0.2f, C("#555A60"));
                    }
                    break;
                case 3: // café
                    p.Room(C("#5B4535"), C("#7A5A40"), C("#3E3029"), null);
                    for (float x = 0.03f; x < 0.97f; x += 0.08f) p.Rect(x, 0.16f, x + 0.005f, 0.92f, C("#4C392C"));
                    foreach (var y in new[] { 0.55f, 0.7f })
                        p.Shelf(0.1f, 0.9f, y, 0.07f, C("#8A6A4F"), new[] { C("#F2EEE6"), C("#6B8F71"), C("#C8553D"), C("#E9C46A") }, 0.02f, 0.04f);
                    foreach (var x in new[] { 0.25f, 0.5f, 0.75f })
                    {
                        p.Rect(x - 0.002f, 0.84f, x + 0.002f, 0.94f, C("#222222"));
                        p.Circle(x, 0.82f, 0.035f, C("#FFD58A"));
                    }
                    p.Rect(0.06f, 0.14f, 0.94f, 0.35f, C("#2E2520"));
                    p.Rect(0.06f, 0.35f, 0.94f, 0.37f, C("#B09070"));
                    break;
                case 4: // storage
                    p.Room(C("#8F8D88"), C("#6F6D69"), C("#7F7D79"), null, 0.1f);
                    for (int i = 0; i < 6; i++)
                    {
                        float x = 0.08f + (i % 3) * 0.12f, y = 0.14f + (i / 3) * 0.17f;
                        p.Rect(x, y, x + 0.115f, y + 0.165f, C("#A5835A") * p.R(0.85f, 1.05f));
                        p.Rect(x + 0.05f, y, x + 0.065f, y + 0.165f, C("#C9B48E"));
                    }
                    p.Rect(0.6f, 0.14f, 0.88f, 0.66f, C("#5D6166"));
                    for (float y = 0.3f; y < 0.66f; y += 0.16f) p.Rect(0.6f, y, 0.88f, y + 0.012f, C("#3F4246"));
                    break;
                case 5: // PC방
                    p.Room(C("#2A2B38"), C("#3A3B48"), C("#1D1E28"), C("#D94BFF"));
                    for (float x = 0.08f; x < 0.9f; x += 0.14f)
                    {
                        p.Rect(x, 0.3f, x + 0.12f, 0.42f, C("#1A1A22"));
                        p.Rect(x + 0.01f, 0.31f, x + 0.11f, 0.41f, p.R() < 0.5f ? C("#5AA0FF") : C("#7CFFB4"));
                        p.Rect(x + 0.02f, 0.14f, x + 0.1f, 0.29f, C("#C2323A"));
                    }
                    p.Rect(0.05f, 0.87f, 0.95f, 0.885f, C("#FF4FA3"));
                    break;
                case 6: // clinic / 치과 upstairs
                    p.Room(C("#D4EAE3"), C("#C9CFCB"), C("#EEF3F1"), C("#FFFFFF"));
                    p.Rect(0.08f, 0.14f, 0.42f, 0.6f, C("#F2F4F3"));
                    for (float y = 0.26f; y < 0.6f; y += 0.12f) p.Rect(0.08f, y, 0.42f, y + 0.006f, C("#B9C4C0"));
                    p.Rect(0.55f, 0.5f, 0.75f, 0.75f, C("#FFFFFF"));
                    p.Rect(0.55f, 0.7f, 0.75f, 0.75f, C("#1FA37A"));
                    p.Text(0.57f, 0.53f, 0.73f, 0.68f, 4, C("#6B7A75"), 0.3f);
                    p.Rect(0.8f, 0.14f, 0.94f, 0.85f, C("#9FD3C7"));
                    break;
                default: // 노래방
                    p.Room(C("#3B2448"), C("#2A1C33"), C("#24162D"), C("#4AC6FF"));
                    p.Rect(0.1f, 0.14f, 0.9f, 0.3f, C("#8C2F45"));
                    p.Rect(0.1f, 0.3f, 0.9f, 0.4f, C("#6E2437"));
                    p.Rect(0.38f, 0.5f, 0.62f, 0.66f, C("#111111"));
                    p.Rect(0.39f, 0.51f, 0.61f, 0.65f, C("#4AC6FF"));
                    p.Rect(0.08f, 0.8f, 0.3f, 0.815f, C("#FF4FA3"));
                    p.Rect(0.7f, 0.74f, 0.92f, 0.755f, C("#FFE45C"));
                    break;
            }
        }

        static void PaintShop(Painter p, int cell)
        {
            switch (cell)
            {
                case 0: // cosmetics (화장품)
                    p.Room(C("#FAFAF8"), C("#EDEBE6"), C("#FFFFFF"), C("#FFFFFF"), 0.02f);
                    foreach (var y in new[] { 0.2f, 0.33f, 0.46f, 0.59f, 0.72f })
                    {
                        p.Shelf(0f, 0.36f, y, 0.08f, C("#E4E4E4"), Bright);
                        p.Shelf(0.64f, 1f, y, 0.08f, C("#E4E4E4"), Bright);
                    }
                    p.Rect(0.38f, 0.42f, 0.62f, 0.86f, C("#F7C6D9"));
                    p.Circle(0.5f, 0.68f, 0.075f, C("#FFFFFF"));
                    p.Text(0.41f, 0.46f, 0.59f, 0.56f, 2, C("#D6336C"), 0.5f);
                    p.Rect(0.3f, 0.14f, 0.7f, 0.26f, C("#FFFFFF"));
                    p.Shelf(0.32f, 0.68f, 0.26f, 0.05f, C("#F0F0F0"), Bright, 0.01f, 0.02f);
                    break;
                case 1: // convenience store (편의점)
                    p.Room(C("#F4F6F8"), C("#D9DCDF"), C("#FFFFFF"), C("#FFFFFF"), 0.02f);
                    var snacks = new[] { C("#E63946"), C("#F4A261"), C("#2A9D8F"), C("#E9C46A"), C("#457B9D"), C("#FFFFFF") };
                    foreach (var y in new[] { 0.18f, 0.32f, 0.46f, 0.6f }) p.Shelf(0f, 0.56f, y, 0.1f, C("#B8BEC4"), snacks, 0.02f, 0.045f);
                    p.Rect(0.62f, 0.14f, 0.95f, 0.82f, C("#9FB3C4"));
                    for (float x = 0.62f; x < 0.95f; x += 0.11f)
                    {
                        p.Rect(x + 0.005f, 0.16f, x + 0.105f, 0.8f, C("#E8F4FF"));
                        foreach (var y in new[] { 0.2f, 0.36f, 0.52f, 0.66f })
                            p.Shelf(x + 0.01f, x + 0.1f, y, 0.1f, C("#C9D6E2"), new[] { C("#3A86FF"), C("#FFBE0B"), C("#FB5607"), C("#8AC926"), C("#FFFFFF") }, 0.012f, 0.02f);
                    }
                    p.Rect(0.05f, 0.84f, 0.95f, 0.9f, C("#6A2C91"));
                    break;
                case 2: // café
                    p.Room(C("#E9DFD2"), C("#7E6049"), C("#3B302A"), null);
                    p.Rect(0.02f, 0.14f, 0.98f, 0.45f, C("#8A6A4F"));
                    p.Rect(0.3f, 0.6f, 0.7f, 0.82f, C("#1E1E1E"));
                    p.Text(0.33f, 0.63f, 0.67f, 0.79f, 6, C("#F2EEE6"), 0.35f);
                    p.Rect(0.06f, 0.14f, 0.94f, 0.4f, C("#4A3A30"));
                    p.Rect(0.06f, 0.4f, 0.94f, 0.42f, C("#D8CFC4"));
                    p.Rect(0.15f, 0.42f, 0.28f, 0.53f, C("#C0C4C8"));
                    foreach (var x in new[] { 0.2f, 0.5f, 0.8f })
                    {
                        p.Rect(x - 0.002f, 0.86f, x + 0.002f, 0.94f, C("#111111"));
                        p.Circle(x, 0.84f, 0.03f, C("#FFD58A"));
                    }
                    break;
                case 3: // clothing (옷가게)
                    p.Room(C("#EDEDED"), C("#CFC7BD"), C("#FAFAFA"), C("#FFFFFF"), 0.02f);
                    p.Rect(0f, 0.7f, 0.75f, 0.71f, C("#8A8F95"));
                    for (float x = 0f; x < 0.73f;)
                    {
                        float w = p.R(0.03f, 0.055f), h = p.R(0.24f, 0.36f);
                        var c = new[] { C("#F2E8DA"), C("#2B2D42"), C("#8D99AE"), C("#EF476F"), C("#FFD166"), C("#06D6A0"), C("#FFFFFF") }[p.Rng.Next(7)];
                        p.Rect(x, 0.7f - h, x + w, 0.7f, c);
                        p.Rect(x + w * 0.45f, 0.7f, x + w * 0.55f, 0.73f, C("#8A8F95"));
                        x += w + 0.004f;
                    }
                    p.Rect(0.8f, 0.2f, 0.94f, 0.8f, C("#DDE6EC"));
                    p.Frame(0.8f, 0.2f, 0.94f, 0.8f, 0.008f, C("#1E1E1E"));
                    p.Rect(0.2f, 0.14f, 0.6f, 0.26f, C("#C9B79C"));
                    break;
                case 4: // phone shop (휴대폰)
                    p.Room(C("#FFFFFF"), C("#E6E9EE"), C("#FFFFFF"), C("#FFFFFF"), 0.02f);
                    p.Rect(0f, 0.78f, 1f, 0.86f, C("#2F6BFF"));
                    foreach (var x in new[] { 0.1f, 0.4f, 0.7f })
                    {
                        p.Rect(x, 0.42f, x + 0.2f, 0.75f, C("#111318"));
                        p.Gradient(x + 0.015f, 0.44f, x + 0.185f, 0.73f, C("#3A0CA3"), C("#4CC9F0"));
                    }
                    p.Rect(0.15f, 0.14f, 0.85f, 0.3f, C("#F0F0F0"));
                    for (float x = 0.2f; x < 0.8f; x += 0.08f) p.Rect(x, 0.3f, x + 0.035f, 0.31f, C("#1E1F24"));
                    break;
                case 5: // restaurant (한식당)
                    p.Room(C("#E8D9BF"), C("#8C6F55"), C("#F3E9D8"), C("#FFF1D6"));
                    p.Rect(0.08f, 0.62f, 0.92f, 0.8f, C("#6B4E36"));
                    for (float x = 0.1f; x < 0.9f; x += 0.1f) p.Text(x, 0.64f, x + 0.07f, 0.78f, 3, C("#F7EBD3"), 0.4f);
                    foreach (var x in new[] { 0.2f, 0.5f, 0.8f })
                    {
                        p.Rect(x - 0.1f, 0.26f, x + 0.1f, 0.29f, C("#6B4E36"));
                        p.Rect(x - 0.08f, 0.14f, x - 0.07f, 0.26f, C("#4A3626"));
                        p.Rect(x + 0.07f, 0.14f, x + 0.08f, 0.26f, C("#4A3626"));
                        p.Circle(x, 0.31f, 0.025f, C("#C0C4C8"));
                    }
                    p.Circle(0.9f, 0.88f, 0.03f, C("#D83A3A"));
                    break;
                case 6: // pharmacy (약국)
                    p.Room(C("#F5FAF7"), C("#DDE3DF"), C("#FFFFFF"), C("#FFFFFF"), 0.02f);
                    var boxes = new[] { C("#FFFFFF"), C("#1FA37A"), C("#8FD3F4"), C("#F7F3E3"), C("#E76F51") };
                    foreach (var y in new[] { 0.36f, 0.48f, 0.6f, 0.72f })
                    {
                        p.Shelf(0f, 0.4f, y, 0.08f, C("#DCE5E0"), boxes, 0.015f, 0.03f);
                        p.Shelf(0.6f, 1f, y, 0.08f, C("#DCE5E0"), boxes, 0.015f, 0.03f);
                    }
                    p.Rect(0.47f, 0.76f, 0.53f, 0.9f, C("#1FA37A"));
                    p.Rect(0.43f, 0.8f, 0.57f, 0.86f, C("#1FA37A"));
                    p.Rect(0.3f, 0.14f, 0.7f, 0.34f, C("#FFFFFF"));
                    p.Rect(0.3f, 0.3f, 0.7f, 0.33f, C("#1FA37A"));
                    break;
                default: // 분식
                    p.Room(C("#FFD76A"), C("#BFB6AA"), C("#FFF3D6"), C("#FFFFFF"));
                    p.Rect(0.1f, 0.62f, 0.9f, 0.82f, C("#D8452B"));
                    p.Text(0.13f, 0.65f, 0.87f, 0.79f, 4, C("#FFF3D6"), 0.4f);
                    foreach (var x in new[] { 0.15f, 0.35f, 0.55f, 0.75f }) p.Rect(x, 0.45f, x + 0.12f, 0.57f, C("#F4A261"));
                    p.Rect(0.06f, 0.14f, 0.94f, 0.35f, C("#C0C4C8"));
                    p.Rect(0.06f, 0.35f, 0.94f, 0.37f, C("#9AA0A6"));
                    p.Rect(0.2f, 0.37f, 0.45f, 0.41f, C("#D8452B"));
                    break;
            }
        }
    }
}
