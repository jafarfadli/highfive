using System;
using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Dresses the plain OSM building extrusions the way Myeongdong looks. Every wall gets windows with
    /// a room behind the glass (Interior Window shader), frames, sills, slab bands and air-conditioner
    /// units. Walls facing 명동길 get lit shop fronts (fascia, LED strip, door, posters, blade signs) and,
    /// on the floors above, sign bands, lettering stuck on the windows and tall stacked signs. Geometry
    /// is batched into a few combined meshes (saved as assets) so hundreds of buildings stay cheap.
    /// </summary>
    public static class FacadeDresser
    {
        const string MeshFolder = "Assets/_Project/Art/Generated/Meshes";
        const float Storey = 3.3f;

        public struct Wall
        {
            public Vector3 Origin;  // bottom-left corner on the wall plane
            public Vector3 Tangent; // horizontal, along the wall
            public Vector3 Normal;  // horizontal, pointing out of the building
            public float Width, Height;
            public Transform Building;

            public Vector3 Center => Origin + Tangent * (Width / 2) + Vector3.up * (Height / 2);
            public Vector3 At(float u, float y) => Origin + Tangent * u + Vector3.up * y;
        }

        /// <summary>Groups a building mesh's vertical triangles into rectangular walls.</summary>
        public static List<Wall> ExtractWalls(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var tr = mf.transform;
            var verts = mesh.vertices.Select(tr.TransformPoint).ToArray();
            var tris = mesh.triangles;
            var groups = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = verts[tris[i]];
                var b = verts[tris[i + 1]];
                var c = verts[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-6f) continue;
                n.Normalize();
                if (Mathf.Abs(n.y) > 0.15f) continue;
                n.y = 0;
                n.Normalize();
                int angle = Mathf.RoundToInt(Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg);
                int dist = Mathf.RoundToInt(Vector3.Dot(n, a) * 5f);
                var key = (angle, dist);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<int>();
                list.Add(i);
            }

            var walls = new List<Wall>();
            foreach (var kv in groups)
            {
                var triList = kv.Value;
                // connected components by shared vertex positions
                var parent = Enumerable.Range(0, triList.Count).ToArray();
                int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
                var owner = new Dictionary<Vector3Int, int>();
                for (int k = 0; k < triList.Count; k++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var p = verts[tris[triList[k] + j]];
                        var key = new Vector3Int(Mathf.RoundToInt(p.x * 50), Mathf.RoundToInt(p.y * 50), Mathf.RoundToInt(p.z * 50));
                        if (owner.TryGetValue(key, out var other)) parent[Find(k)] = Find(other);
                        else owner[key] = k;
                    }
                }
                foreach (var comp in Enumerable.Range(0, triList.Count).GroupBy(Find))
                {
                    var first = verts[tris[triList[comp.First()]]];
                    var n0 = Vector3.Cross(verts[tris[triList[comp.First()] + 1]] - first, verts[tris[triList[comp.First()] + 2]] - first);
                    n0.y = 0;
                    n0.Normalize();
                    var t = Vector3.Cross(Vector3.up, n0).normalized;
                    float umin = float.MaxValue, umax = float.MinValue, ymin = float.MaxValue, ymax = float.MinValue;
                    foreach (var k in comp)
                    for (int j = 0; j < 3; j++)
                    {
                        var p = verts[tris[triList[k] + j]];
                        float u = Vector3.Dot(p, t);
                        umin = Mathf.Min(umin, u);
                        umax = Mathf.Max(umax, u);
                        ymin = Mathf.Min(ymin, p.y);
                        ymax = Mathf.Max(ymax, p.y);
                    }
                    float d = Vector3.Dot(first, n0);
                    walls.Add(new Wall
                    {
                        Origin = t * umin + n0 * d + Vector3.up * ymin,
                        Tangent = t,
                        Normal = n0,
                        Width = umax - umin,
                        Height = ymax - ymin,
                        Building = tr,
                    });
                }
            }
            return walls;
        }

        // ----------------------------------------------------------------- batched geometry

        public class Batch
        {
            public readonly string Name;
            public readonly Material Material;
            readonly List<Vector3> v = new();
            readonly List<Vector3> n = new();
            readonly List<Vector4> tan = new();
            readonly List<Vector2> uv = new();
            readonly List<Vector4> room = new(), extra = new();
            readonly List<int> t = new();
            public int Quads { get; private set; }

            public Batch(string name, Material material)
            {
                Name = name;
                Material = material;
            }

            /// <summary>Quad from origin along right (w) and up (h), facing normal; UVs 0..1 along right and up.</summary>
            public void Quad(Vector3 origin, Vector3 right, Vector3 up, Vector3 normal, float w, float h)
            {
                int i = v.Count;
                v.Add(origin);
                v.Add(origin + up * h);
                v.Add(origin + up * h + right * w);
                v.Add(origin + right * w);
                float sign = Vector3.Dot(Vector3.Cross(normal, right), up) >= 0 ? 1f : -1f;
                for (int k = 0; k < 4; k++)
                {
                    n.Add(normal);
                    tan.Add(new Vector4(right.x, right.y, right.z, sign));
                }
                uv.Add(new Vector2(0, 0));
                uv.Add(new Vector2(0, 1));
                uv.Add(new Vector2(1, 1));
                uv.Add(new Vector2(1, 0));
                // Unity treats Cross(v1 - v0, v2 - v0) as the front side; pick the winding that faces `normal`
                if (Vector3.Dot(Vector3.Cross(right, up), normal) > 0)
                    t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                else
                    t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                Quads++;
            }

            /// <summary>
            /// A pane of glass on a wall with a room behind it (Interior Window shader). u, y and w, h are
            /// wall coordinates; the pane is laid out so the room reads left to right for a viewer outside.
            /// </summary>
            public void Window(Wall wall, float u, float y, float w, float h, float offset, int cell, float lights, float depth, float blind)
            {
                Quad(wall.At(u + w, y) + wall.Normal * offset, -wall.Tangent, Vector3.up, wall.Normal, w, h);
                var r = new Vector4(cell, lights, w, h);
                var e = new Vector4(depth, blind, 0, 0);
                for (int k = 0; k < 4; k++)
                {
                    room.Add(r);
                    extra.Add(e);
                }
            }

            /// <summary>
            /// A window frame around the opening (u, y, w, h): a border of width b on the front plane at
            /// <paramref name="depth"/>, and the four reveals running back to the glass at <paramref name="glass"/>.
            /// </summary>
            public void Frame(Wall wall, float u, float y, float w, float h, float b, float depth, float glass)
            {
                var T = wall.Tangent;
                var U = Vector3.up;
                var N = wall.Normal;
                Vector3 F(float uu, float yy) => wall.At(uu, yy) + N * depth;
                Vector3 G(float uu, float yy) => wall.At(uu, yy) + N * glass;
                float d = depth - glass;
                Quad(F(u - b, y - b), T, U, N, b, h + 2 * b);            // left border
                Quad(F(u + w, y - b), T, U, N, b, h + 2 * b);            // right border
                Quad(F(u, y + h), T, U, N, w, b);                        // head
                Quad(F(u, y - b), T, U, N, w, b);                        // sill line
                Quad(G(u, y), N, U, T, d, h);                            // left reveal, facing the opening
                Quad(G(u + w, y), N, U, -T, d, h);                       // right reveal
                Quad(G(u, y + h), T, N, -U, w, d);                       // soffit
                Quad(G(u, y), T, N, U, w, d);                            // bottom reveal
            }

            /// <summary>A ledge on the wall: front, top and underside only (its ends are too small to see).</summary>
            public void Slab(Wall wall, float u, float y, float w, float h, float depth, float offset = 0f)
            {
                var o = wall.At(u, y) + wall.Normal * offset;
                var T = wall.Tangent;
                var U = Vector3.up;
                var N = wall.Normal;
                Quad(o + N * depth, T, U, N, w, h);
                Quad(o + U * h, T, N, U, w, depth);
                Quad(o + N * depth, T, -N, -U, w, depth);
            }

            /// <summary>Axis-aligned (in wall space) box: u,y at the wall surface, depth out along the normal.</summary>
            public void Box(Wall wall, float u, float y, float w, float h, float depth, float offset = 0f)
            {
                var o = wall.At(u, y) + wall.Normal * offset;
                var T = wall.Tangent;
                var U = Vector3.up;
                var N = wall.Normal;
                var f = o + N * depth;
                Quad(f, T, U, N, w, h);                                  // front
                Quad(o + U * h, T, N, U, w, depth);                      // top
                Quad(o + N * depth, T, -N, -U, w, depth);                // bottom
                Quad(o, N, U, -T, depth, h);                             // left end
                Quad(o + T * w + N * depth, -N, U, T, depth, h);         // right end
            }

            public GameObject Flush(Transform parent, string assetPrefix)
            {
                if (v.Count == 0) return null;
                var mesh = new Mesh { name = $"{assetPrefix}_{Name}", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetTangents(tan);
                mesh.SetUVs(0, uv);
                if (room.Count == v.Count)
                {
                    mesh.SetUVs(1, room);
                    mesh.SetUVs(2, extra);
                }
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                EnsureFolder(MeshFolder);
                var path = $"{MeshFolder}/{mesh.name}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(Name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                return go;
            }
        }

        // ----------------------------------------------------------------- what's on the upper floors

        // matches the cell order of RoomAtlas.Upper
        enum Use { Office, Home, Academy, Cafe, Storage, PcBang, Clinic, Karaoke }

        static readonly (Use use, float weight, float lights, string[] names)[] Uses =
        {
            (Use.Office, 0.22f, 0.85f, new[] { "세무회계", "여행사", "번역 · 공증", "무역상사" }),
            (Use.Home, 0.18f, 0.2f, null),
            (Use.Academy, 0.12f, 0.9f, new[] { "영어학원", "수학학원", "한국어학원", "미술학원" }),
            (Use.Cafe, 0.1f, 0.8f, new[] { "카페", "디저트카페", "브런치" }),
            (Use.Storage, 0.08f, 0f, new[] { "임대 문의" }),
            (Use.PcBang, 0.08f, 0.8f, new[] { "PC방", "PC CAFE" }),
            (Use.Clinic, 0.14f, 0.9f, new[] { "피부과", "치과", "한의원", "안과" }),
            (Use.Karaoke, 0.08f, 0.6f, new[] { "코인노래방", "노래연습장" }),
        };

        /// <summary>Shop-atlas cell (RoomAtlas.Shops) for a shop name.</summary>
        static int ShopCell(string name)
        {
            if (name.Contains("편의점")) return 1;
            if (name.Contains("카페") || name.Contains("빵") || name.Contains("베이커리") || name.Contains("빙수")) return 2;
            if (name.Contains("옷") || name.Contains("양말") || name.Contains("신발") || name.Contains("모자") || name.Contains("기념품")) return 3;
            if (name.Contains("휴대폰") || name.Contains("안경") || name.Contains("환전") || name.Contains("K-POP")) return 4;
            if (name.Contains("한식") || name.Contains("치킨") || name.Contains("우동") || name.Contains("김밥")) return 5;
            if (name.Contains("약국")) return 6;
            if (name.Contains("떡볶이") || name.Contains("분식") || name.Contains("호떡")) return 7;
            return 0;
        }

        static readonly string[] Promos = { "SALE\n50%", "1+1", "TAX\nFREE", "NEW", "OPEN", "마스크팩\n10+10", "할인", "BEST", "EVENT", "세일" };
        static readonly (string board, string text)[] PromoColors =
        {
            ("#E4405F", "#FFFFFF"), ("#FFD23F", "#1E1E22"), ("#FFFFFF", "#D7263D"), ("#12141C", "#FFD23F"), ("#FF7AA8", "#FFFFFF"),
        };
        static readonly string[] SignBoards = { "#E4405F", "#1B998B", "#3D7BFF", "#F7B32B", "#8E44AD", "#12141C", "#FFFFFF", "#D7263D", "#2BB673", "#FF7F11" };

        public class Options
        {
            public Func<Vector3, (float distance, Vector3 closest)> StreetDistance;
            public List<Vector3> Keepouts = new();
            public string[] ShopNames;
            public string[] ShopColors;
            public int Seed = 7;

            /// <summary>Corner sets (see <see cref="Corners"/>) of props against the facades: doors, signs, lamps.</summary>
            public List<Vector3[]> Occluders = new();
        }

        /// <summary>The 8 world-space corners of a mesh's bounds, following its rotation.</summary>
        public static Vector3[] Corners(MeshFilter mf)
        {
            var b = mf.sharedMesh.bounds;
            var t = mf.transform;
            var c = new Vector3[8];
            int i = 0;
            foreach (var x in new[] { -1f, 1f })
            foreach (var y in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                c[i++] = t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
            return c;
        }

        static Material WindowMaterial(string name, Texture2D atlas, float dim, float lit, float reflection, float grazing)
        {
            var m = TextureKit.MaterialAsset(name, "SeoulFirstWeek/Interior Window");
            m.SetTexture("_Atlas", atlas);
            m.SetVector("_Cells", new Vector4(RoomAtlas.Columns, RoomAtlas.Rows, 0, 0));
            m.SetFloat("_Dim", dim);
            m.SetFloat("_Lit", lit);
            m.SetFloat("_Reflection", reflection);
            m.SetFloat("_MaxReflection", grazing);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// A single pane with a lit room behind it, e.g. the glass doors of a building entrance. The pane
        /// spans (x0, y0) to (x0 + w, y0 + h) in the frame's local XY plane and faces the frame's +Z.
        /// </summary>
        public static GameObject GlassPane(Transform frame, string name, float x0, float y0, float w, float h, float offset, int shopCell, float depth)
        {
            var wall = new Wall
            {
                Origin = frame.TransformPoint(new Vector3(x0, 0, 0)),
                Tangent = frame.right,
                Normal = frame.forward,
                Width = w,
                Height = h,
                Building = frame,
            };
            var batch = new Batch(name, WindowMaterial("Win_Shops", RoomAtlas.Shops(), 0.6f, 1.0f, 0.05f, 0.4f));
            batch.Window(wall, 0, y0, w, h, offset, shopCell, 1f, depth, 0f);
            var go = batch.Flush(frame, frame.name);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go;
        }

        public static void Dress(Transform root, IEnumerable<MeshFilter> buildings, Options o)
        {
            var rng = new System.Random(o.Seed);
            float R() => (float)rng.NextDouble();

            var windows = new Batch("Windows", WindowMaterial("Win_Upper", RoomAtlas.Upper(), 0.3f, 1.15f, 0.1f, 0.7f));
            var shopGlass = new Batch("ShopWindows", WindowMaterial("Win_Shops", RoomAtlas.Shops(), 0.6f, 1.0f, 0.05f, 0.4f));
            var frameColors = new[] { "#3C4046", "#E6E6E1", "#6B5646", "#8C9096" };
            var frames = frameColors.Select(h => new Batch("Frames_" + h.TrimStart('#'), Mats.Lit(C(h), 0.35f, 0.3f))).ToArray();
            var sills = new Batch("Sills", Mats.Lit(C("#B9B4AA"), 0.2f));
            var bands = new[] { new Batch("Bands_Light", Mats.Lit(C("#CFC9BE"), 0.15f)), new Batch("Bands_Dark", Mats.Lit(C("#7E7A74"), 0.15f)) };
            var ac = new Batch("AirCon", Mats.Lit(C("#E4E4DF"), 0.3f));
            var acFans = new Batch("AirConFans", Mats.Lit(C("#34373C"), 0.2f));
            var mullions = new Batch("ShopFrames", Mats.Lit(C("#2B2D31"), 0.4f, 0.3f));
            var steel = new Batch("ShopHandles", Mats.Lit(C("#C9CED4"), 0.7f, 0.9f));
            var led = new Batch("ShopLeds", Mats.Emissive(C("#FFF4E0"), 2.6f));
            var fasciaBatches = new Dictionary<string, Batch>();
            Batch Fascia(string hex)
            {
                if (!fasciaBatches.TryGetValue(hex, out var b))
                    fasciaBatches[hex] = b = new Batch("Fascia_" + hex.TrimStart('#'), Mats.Lit(C(hex), 0.35f));
                return b;
            }
            var signRoot = Group("ShopSigns", root);
            int shops = 0, windowCount = 0, leftOut = 0, acUnits = 0, bandSigns = 0, towers = 0, letters = 0;

            var walls = buildings.SelectMany(ExtractWalls).Where(w => w.Width >= 2.2f && w.Height >= 3f).ToList();
            var storefront = new bool[walls.Count];
            var street = new bool[walls.Count];
            var occluders = new List<Vector3[]>(o.Occluders);

            // per building: window style, frame colour, slab bands; per building and storey: its use
            var style = new Dictionary<Transform, (int windows, int frame, int band)>();
            (int windows, int frame, int band) Style(Transform b)
            {
                if (!style.TryGetValue(b, out var s))
                {
                    float x = R();
                    style[b] = s = (x < 0.5f ? 0 : x < 0.8f ? 1 : 2, rng.Next(frameColors.Length), R() < 0.55f ? rng.Next(2) : -1);
                }
                return s;
            }
            var uses = new Dictionary<(Transform, int), (Use use, bool lights, string name)>();
            (Use use, bool lights, string name) FloorUse(Transform b, int storey)
            {
                if (!uses.TryGetValue((b, storey), out var f))
                {
                    float x = R() * Uses.Sum(u => u.weight);
                    var pick = Uses[0];
                    foreach (var u in Uses)
                    {
                        pick = u;
                        if (x < u.weight) break;
                        x -= u.weight;
                    }
                    uses[(b, storey)] = f = (pick.use, R() < pick.lights, pick.names?[rng.Next(pick.names.Length)]);
                }
                return f;
            }

            bool Pokes(Vector3 center, Vector3 half, Quaternion rot) =>
                Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore).Any(c => c.name.StartsWith("Bldg_"));

            // pass 1: shop fronts on the walls facing 명동길, with their blade and stacked signs (occluders for pass 2)
            for (int wi = 0; wi < walls.Count; wi++)
            {
                var wall = walls[wi];
                var center = wall.Center;
                var (dist, closest) = o.StreetDistance(center);
                var toStreet = closest - center;
                toStreet.y = 0;
                street[wi] = dist < 16f && toStreet.sqrMagnitude > 0.01f && Vector3.Dot(wall.Normal, toStreet.normalized) > 0.55f;
                bool nearKeepout = o.Keepouts.Any(k => Vector2.Distance(new Vector2(k.x, k.z), new Vector2(center.x, center.z)) < wall.Width / 2 + 2.5f);
                storefront[wi] = street[wi] && !nearKeepout && wall.Height > 4.2f;
                if (!storefront[wi]) continue;

                // split long walls into several shops
                int units = Mathf.Clamp(Mathf.RoundToInt(wall.Width / 7f), 1, 4);
                float unitW = wall.Width / units;
                for (int s = 0; s < units; s++)
                {
                    float u0 = s * unitW + 0.25f;
                    float w = unitW - 0.5f;
                    if (w < 2f) continue;
                    var name = o.ShopNames[rng.Next(o.ShopNames.Length)];
                    shopGlass.Window(wall, u0, 0.35f, w, 2.55f, 0.02f, ShopCell(name), 1f, 4.5f, 0f);

                    // frame: sill, head, mullions; a door in one of the panes
                    mullions.Box(wall, u0 - 0.06f, 0.25f, w + 0.12f, 0.1f, 0.12f);
                    mullions.Box(wall, u0 - 0.06f, 2.9f, w + 0.12f, 0.08f, 0.12f);
                    int panes = Mathf.Max(1, Mathf.RoundToInt(w / 2.2f));
                    for (int p = 0; p <= panes; p++)
                        mullions.Box(wall, u0 + w * p / panes - 0.04f, 0.35f, 0.08f, 2.55f, 0.1f);
                    int door = rng.Next(panes);
                    float du = u0 + w * door / panes, dw = w / panes;
                    mullions.Box(wall, du, 2.2f, dw, 0.06f, 0.1f);
                    steel.Box(wall, du + dw / 2f - 0.12f, 0.85f, 0.035f, 0.95f, 0.05f, 0.1f);
                    steel.Box(wall, du + dw / 2f + 0.085f, 0.85f, 0.035f, 0.95f, 0.05f, 0.1f);

                    // fascia with a LED strip under it
                    var hex = o.ShopColors[rng.Next(o.ShopColors.Length)];
                    Fascia(hex).Box(wall, u0 - 0.05f, 3.0f, w + 0.1f, 0.85f, 0.22f);
                    led.Box(wall, u0 - 0.05f, 2.96f, w + 0.1f, 0.035f, 0.2f);
                    var board = C(hex);
                    var textColor = board.grayscale > 0.7f ? C("#1E1E22") : Color.white;
                    var textPos = wall.At(u0 + w / 2, 3.425f) + wall.Normal * 0.235f;
                    var text = WorldText.Create(signRoot, "ShopSign", name, Vector3.zero, new Vector2(Mathf.Min(w * 0.9f, 4.5f), 0.7f), 64, textColor, true, TextAnchor.MiddleCenter, Quaternion.identity);
                    text.transform.parent.SetPositionAndRotation(textPos, Quaternion.LookRotation(-wall.Normal));
                    shops++;

                    // posters stuck on the glass: at most one per pane, never on the door
                    int posters = rng.Next(0, 3);
                    var usedPanes = new HashSet<int> { door };
                    for (int k = 0; k < posters && panes > 1; k++)
                    {
                        int pane = rng.Next(panes);
                        if (!usedPanes.Add(pane)) continue;
                        var (pb, pt) = PromoColors[rng.Next(PromoColors.Length)];
                        float pu = u0 + w * (pane + 0.5f) / panes + (R() - 0.5f) * 0.4f;
                        var poster = Sign(signRoot, "Poster", Vector3.zero, new Vector2(0.62f, 0.82f), C(pb), Promos[rng.Next(Promos.Length)], C(pt), 0, 64, 0.006f);
                        poster.SetPositionAndRotation(wall.At(pu, 1.55f) + wall.Normal * 0.03f, Quaternion.LookRotation(wall.Normal));
                    }

                    // one blade sign on some shops, at a fixed height
                    if (R() < 0.45f)
                    {
                        var hangul = name.Where(ch => ch >= '\uAC00' && ch <= '\uD7A3').Take(4).ToArray();
                        if (hangul.Length < 2) continue;
                        var bladeName = string.Join("\n", hangul);
                        var edge = wall.At(u0 + 0.35f, 5.4f) + wall.Normal * 0.62f;
                        float bladeHeight = 0.45f + 0.42f * bladeName.Split('\n').Length;
                        // skip signs that would stick into a neighbouring building
                        if (Pokes(edge, new Vector3(0.32f, bladeHeight / 2f + 0.1f, 0.1f), Quaternion.LookRotation(wall.Tangent))) continue;
                        var sign = Sign(signRoot, "BladeSign", Vector3.zero, new Vector2(0.56f, bladeHeight), board, bladeName, textColor, 0, 56, 0.1f);
                        sign.SetPositionAndRotation(edge, Quaternion.LookRotation(wall.Tangent));
                        var back = UnityEngine.Object.Instantiate(sign.Find("Text").gameObject, sign);
                        back.transform.localPosition = new Vector3(0, 0, -0.06f);
                        back.transform.localRotation = Quaternion.identity;
                        var arm = Box(signRoot, "BladeArm", edge + Vector3.up * (0.25f + 0.21f * bladeName.Split('\n').Length) - wall.Normal * 0.31f,
                            new Vector3(0.05f, 0.05f, 0.62f), C("#2B2D31"), false);
                        arm.transform.rotation = Quaternion.LookRotation(wall.Normal);
                        foreach (var mf in sign.GetComponentsInChildren<MeshFilter>()) occluders.Add(Corners(mf));
                        occluders.Add(Corners(arm.GetComponent<MeshFilter>()));
                    }
                }

                // a tall stacked sign listing the businesses on the floors above (돌출 간판)
                int storeys = Mathf.FloorToInt((wall.Height - 4.4f) / Storey);
                if (storeys >= 2 && R() < 0.4f)
                {
                    var names = new List<string>();
                    for (int f = 1; f <= Mathf.Min(storeys, 4); f++)
                    {
                        var fu = FloorUse(wall.Building, f);
                        if (fu.name != null && fu.use != Use.Storage) names.Add(fu.name.Replace(" ", ""));
                    }
                    if (names.Count >= 2)
                    {
                        float height = names.Count * 1.25f;
                        float end = R() < 0.5f ? 0.45f : wall.Width - 0.45f;
                        var pos = wall.At(end, 4.2f + height / 2f) + wall.Normal * 0.75f;
                        var rot = Quaternion.LookRotation(wall.Tangent);
                        var probe = new Vector3(0.4f, height / 2f + 0.1f, 0.12f);
                        bool clear = !Pokes(pos, probe, rot) && !Blockers(wall, occluders).Any(b => b.x < end + 0.3f && b.z > end - 0.3f && b.w > 4.2f && b.y < 4.2f + height);
                        if (clear)
                        {
                            names.Reverse(); // top floor first
                            var boardHex = SignBoards[rng.Next(SignBoards.Length)];
                            var bc = C(boardHex);
                            var tc = bc.grayscale > 0.7f ? C("#1E1E22") : Color.white;
                            var tower = Sign(signRoot, "StackedSign", Vector3.zero, new Vector2(0.7f, height), bc, string.Join("\n", names), tc, 0, 44, 0.16f);
                            tower.SetPositionAndRotation(pos, rot);
                            var back = UnityEngine.Object.Instantiate(tower.Find("Text").gameObject, tower);
                            back.transform.localPosition = new Vector3(0, 0, -0.09f);
                            back.transform.localRotation = Quaternion.identity;
                            foreach (var y in new[] { 0.3f, height - 0.3f })
                            {
                                var arm = Box(signRoot, "StackedSignArm", wall.At(end, 4.2f + y) + wall.Normal * 0.34f, new Vector3(0.06f, 0.06f, 0.7f), C("#2B2D31"), false);
                                arm.transform.rotation = Quaternion.LookRotation(wall.Normal);
                                occluders.Add(Corners(arm.GetComponent<MeshFilter>()));
                            }
                            foreach (var mf in tower.GetComponentsInChildren<MeshFilter>()) occluders.Add(Corners(mf));
                            towers++;
                        }
                    }
                }
            }

            // pass 2: windows on every storey, left out wherever a door, sign, prop or neighbouring building covers them.
            // Detail falls off with distance from 명동길: full frames, sills, AC units and bands near the street,
            // a flat frame further out, and only the glass on the far blocks seen over the rooftops.
            for (int wi = 0; wi < walls.Count; wi++)
            {
                var wall = walls[wi];
                if (wall.Height < 3.2f) continue;
                float away = o.StreetDistance(wall.Center).distance;
                int tier = street[wi] || away < 24f ? 0 : away < 55f ? 1 : 2;
                if (tier == 2 && wall.Height < 7f) continue;
                var st = Style(wall.Building);
                float winW, winH, pitchMin;
                switch (st.windows)
                {
                    case 1: winW = 2.1f; winH = 1.5f; pitchMin = 3.0f; break;   // wide sliding windows
                    case 2: winW = 0f; winH = 1.8f; pitchMin = 1.5f; break;     // ribbon of panes
                    default: winW = 1.3f; winH = 1.6f; pitchMin = 2.4f; break;  // punched windows
                }
                int cols = Mathf.FloorToInt((wall.Width - 0.8f) / pitchMin);
                if (cols < 1) continue;
                float pitch = (wall.Width - 0.8f) / cols;
                if (st.windows == 2) winW = pitch - 0.14f;
                var blockers = Blockers(wall, occluders);
                float firstFloor = storefront[wi] ? 4.4f : 1.2f;
                var frame = frames[st.frame];
                int storey0 = storefront[wi] ? 1 : 0;
                for (int row = 0; firstFloor + row * Storey + winH < wall.Height - 0.4f; row++)
                {
                    float y = firstFloor + row * Storey - (winH - 1.6f) / 2f;
                    int storey = storey0 + row;
                    var fu = FloorUse(wall.Building, storey);

                    // a sign band in the spandrel under this storey's windows (street side, storeys 1–4)
                    bool signed = false;
                    if (street[wi] && storey >= 1 && storey <= 4 && fu.name != null && R() < 0.45f)
                    {
                        float sw = Mathf.Min(wall.Width - 0.8f, 3f + R() * 5f);
                        float su = 0.4f + R() * (wall.Width - 0.8f - sw);
                        float sy = y - 1.5f;
                        var sc = wall.At(su + sw / 2f, sy + 0.35f) + wall.Normal * 0.08f;
                        if (sy > 2.9f && !Covered(wall, su, sy, sw, 0.7f, blockers) && !Pokes(sc, new Vector3(sw / 2f, 0.35f, 0.06f), Quaternion.LookRotation(wall.Normal)))
                        {
                            var boardHex = SignBoards[rng.Next(SignBoards.Length)];
                            var bc = C(boardHex);
                            var sign = Sign(signRoot, "FloorSign", Vector3.zero, new Vector2(sw, 0.7f), bc, fu.name, bc.grayscale > 0.7f ? C("#1E1E22") : Color.white, 0, 60, 0.12f);
                            sign.SetPositionAndRotation(sc - wall.Normal * 0.02f, Quaternion.LookRotation(wall.Normal));
                            blockers.Add(new Vector4(su, sy, su + sw, sy + 0.7f));
                            signed = true;
                            bandSigns++;
                        }
                    }
                    // lettering on the glass spells the business across this storey's windows
                    string spell = street[wi] && storey >= 1 && fu.lights && fu.name != null && fu.use != Use.Office && fu.use != Use.Storage && R() < 0.4f
                        ? fu.name.Replace(" ", "") : null;
                    int letter = 0;
                    bool lightsOn = fu.lights;

                    for (int c = 0; c < cols; c++)
                    {
                        float u = 0.4f + c * pitch + (pitch - winW) / 2f;
                        if (Covered(wall, u - 0.1f, y - 0.14f, winW + 0.2f, winH + 0.22f, blockers))
                        {
                            leftOut++;
                            continue;
                        }
                        bool on = R() < 0.88f ? lightsOn : !lightsOn;
                        float blind = fu.use == Use.Home ? (R() < 0.75f ? 0.2f + R() * 0.6f : 0f) : R() < 0.22f ? 0.1f + R() * 0.45f : 0f;
                        windowCount++;
                        if (tier == 2)
                        {
                            windows.Window(wall, u, y, winW, winH, 0.03f, (int)fu.use, on ? 1f : 0f, 4f, blind);
                            continue;
                        }
                        if (tier == 1)
                        {
                            frame.Quad(wall.At(u - 0.07f, y - 0.07f) + wall.Normal * 0.02f, wall.Tangent, Vector3.up, wall.Normal, winW + 0.14f, winH + 0.14f);
                            windows.Window(wall, u, y, winW, winH, 0.04f, (int)fu.use, on ? 1f : 0f, 4f, blind);
                            continue;
                        }
                        windows.Window(wall, u, y, winW, winH, 0.015f, (int)fu.use, on ? 1f : 0f, 3.5f + R() * 1.5f, blind);
                        frame.Frame(wall, u, y, winW, winH, 0.06f, 0.09f, 0.015f);
                        if (st.windows == 1) frame.Slab(wall, u + winW / 2f - 0.025f, y, 0.05f, winH, 0.06f, 0.015f);
                        sills.Slab(wall, u - 0.12f, y - 0.14f, winW + 0.24f, 0.08f, 0.17f);

                        if (spell != null && letter < spell.Length)
                        {
                            var ch = WorldText.Create(signRoot, "WindowLetter", spell[letter].ToString(), Vector3.zero, new Vector2(Mathf.Min(winW, 1.1f), 0.9f), 90,
                                fu.use == Use.Karaoke || fu.use == Use.PcBang ? C("#FFE45C") : Color.white, true, TextAnchor.MiddleCenter, Quaternion.identity);
                            ch.transform.parent.SetPositionAndRotation(wall.At(u + winW / 2f, y + winH * 0.55f) + wall.Normal * 0.03f, Quaternion.LookRotation(-wall.Normal));
                            letter++;
                            letters++;
                        }

                        // an air-conditioner unit hangs under some windows (not at street level)
                        if (tier == 0 && storey >= 1 && fu.use != Use.Storage && R() < 0.2f)
                        {
                            float au = u + (winW - 0.8f) * R(), ay = y - 0.8f;
                            var acCenter = wall.At(au + 0.4f, ay + 0.28f) + wall.Normal * 0.2f;
                            if (!Covered(wall, au, ay, 0.8f, 0.56f, blockers, 0.02f) && !Pokes(acCenter, new Vector3(0.4f, 0.28f, 0.15f), Quaternion.LookRotation(wall.Normal)))
                            {
                                ac.Box(wall, au, ay, 0.8f, 0.56f, 0.28f, 0.04f);
                                acFans.Quad(wall.At(au + 0.07f, ay + 0.08f) + wall.Normal * 0.325f, wall.Tangent, Vector3.up, wall.Normal, 0.4f, 0.4f);
                                blockers.Add(new Vector4(au, ay, au + 0.8f, ay + 0.56f));
                                acUnits++;
                            }
                        }
                    }

                    // slab band at this storey's floor level
                    if (tier == 0 && st.band >= 0 && !signed && storey >= (storefront[wi] ? 2 : 1))
                    {
                        float by = y - 1.32f;
                        if (!Covered(wall, 0.15f, by, wall.Width - 0.3f, 0.26f, blockers, 0f))
                            bands[st.band].Slab(wall, 0.1f, by, wall.Width - 0.2f, 0.26f, 0.05f);
                    }
                }
            }

            var dressing = Group("FacadeMeshes", root);
            foreach (var b in new[] { windows, shopGlass, sills, ac, acFans, mullions, steel, led }.Concat(frames).Concat(bands).Concat(fasciaBatches.Values))
                b.Flush(dressing, "Hub");
            Debug.Log($"[Facades] {windowCount} windows ({leftOut} left out where something covers the wall), {shops} shop fronts, " +
                      $"{bandSigns} floor signs, {towers} stacked signs, {letters} window letters, {acUnits} AC units");
        }

        /// <summary>
        /// Wall-space rectangles (u0, y0, u1, y1) of the occluders that sit against this wall:
        /// within 0.45 m in front of it, or poking into it.
        /// </summary>
        static List<Vector4> Blockers(Wall wall, List<Vector3[]> occluders)
        {
            var list = new List<Vector4>();
            float reach = wall.Width / 2f + wall.Height + 4f;
            var center = wall.Center;
            foreach (var corners in occluders)
            {
                if ((corners[0] - center).sqrMagnitude > reach * reach) continue;
                float u0 = float.MaxValue, u1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue, n0 = float.MaxValue, n1 = float.MinValue;
                foreach (var p in corners)
                {
                    var d = p - wall.Origin;
                    float u = Vector3.Dot(d, wall.Tangent), n = Vector3.Dot(d, wall.Normal);
                    u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                    y0 = Mathf.Min(y0, d.y); y1 = Mathf.Max(y1, d.y);
                    n0 = Mathf.Min(n0, n); n1 = Mathf.Max(n1, n);
                }
                if (n1 < -0.2f || n0 > 0.45f) continue;
                if (u1 < 0 || u0 > wall.Width || y1 < 0 || y0 > wall.Height) continue;
                list.Add(new Vector4(u0, y0, u1, y1));
            }
            return list;
        }

        /// <summary>True when a rectangle on the wall overlaps a blocker or another building cuts through it.</summary>
        static bool Covered(Wall wall, float u, float y, float w, float h, List<Vector4> blockers, float depth = 0.12f)
        {
            const float margin = 0.05f;
            foreach (var r in blockers)
                if (u - margin < r.z && u + w + margin > r.x && y - margin < r.w && y + h + margin > r.y)
                    return true;
            if (depth <= 0f) return false;
            var center = wall.At(u + w / 2f, y + h / 2f) + wall.Normal * 0.15f;
            return Physics.OverlapBox(center, new Vector3(w / 2f, h / 2f, depth), Quaternion.LookRotation(wall.Normal), ~0, QueryTriggerInteraction.Ignore)
                .Any(c => c.name.StartsWith("Bldg_"));
        }
    }
}
