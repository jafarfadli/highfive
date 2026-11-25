using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Gives the imported OSM buildings (no UVs, one wall and one roof submesh) textured facades:
    /// each mesh is copied with flat per-face normals and UVs in metres (walls: along the wall and up
    /// from the building's base; roofs: world XZ), and gets a cladding material (tile, plaster, stone,
    /// panel, concrete or brick) in its own tint, plus a green or concrete roof.
    /// </summary>
    public static class BuildingSkin
    {
        const string MeshAsset = "Assets/_Project/Art/Generated/Meshes/Hub_Buildings.asset";

        static readonly (TextureKit.Facade style, float weight)[] Styles =
        {
            (TextureKit.Facade.Tile, 0.27f), (TextureKit.Facade.Plaster, 0.2f), (TextureKit.Facade.Stone, 0.16f),
            (TextureKit.Facade.Panel, 0.14f), (TextureKit.Facade.Concrete, 0.12f), (TextureKit.Facade.Brick, 0.11f),
        };

        public static void Apply(IList<MeshRenderer> buildings, string[] wallPalette, string[] roofPalette, System.Random rng)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(MeshAsset)!.Replace('\\', '/'));
            AssetDatabase.DeleteAsset(MeshAsset);
            Mesh main = null;
            int count = 0;
            foreach (var r in buildings)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf) continue;
                // start from the imported mesh so rebuilding is repeatable
                if (PrefabUtility.IsPartOfPrefabInstance(mf)) PrefabUtility.RevertObjectOverride(mf, InteractionMode.AutomatedAction);
                var src = mf.sharedMesh;
                if (!src) continue;
                var mesh = Unwrap(src, mf.transform);
                if (!main)
                {
                    main = mesh;
                    AssetDatabase.CreateAsset(main, MeshAsset);
                }
                else AssetDatabase.AddObjectToAsset(mesh, main);
                mf.sharedMesh = mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(mf);

                var style = Pick(rng);
                var hex = wallPalette[rng.Next(wallPalette.Length)].TrimStart('#');
                var tint = C("#" + hex);
                if (style == TextureKit.Facade.Brick) tint = Color.Lerp(Color.white, tint, 0.2f);
                var wall = TextureKit.LitUV($"Facade_{style}_{hex}", TextureKit.FacadeTex(style), tint,
                    metallic: style == TextureKit.Facade.Panel ? 0.2f : 0f, bump: 0.8f);
                Material roof;
                if (rng.NextDouble() < 0.65) roof = TextureKit.LitUV("Roof_Green", TextureKit.RoofGreen(), Color.white, 1f);
                else
                {
                    var rh = roofPalette[rng.Next(roofPalette.Length)].TrimStart('#');
                    roof = TextureKit.LitUV($"Roof_Concrete_{rh}", TextureKit.RoofConcrete(), Color.Lerp(Color.white, C("#" + rh), 0.5f), 1f);
                }
                if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RevertObjectOverride(r, InteractionMode.AutomatedAction);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    if (mats[i].name == "wall") mats[i] = wall;
                    else if (mats[i].name == "roof") mats[i] = roof;
                }
                r.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Builder] Building skins: {count} buildings textured");
        }

        static TextureKit.Facade Pick(System.Random rng)
        {
            float x = (float)rng.NextDouble() * Styles.Sum(s => s.weight);
            foreach (var (style, weight) in Styles)
            {
                if (x < weight) return style;
                x -= weight;
            }
            return Styles[0].style;
        }

        /// <summary>Unwelded copy with face normals and UVs in metres (tangents for normal maps).</summary>
        static Mesh Unwrap(Mesh src, Transform tr)
        {
            var verts = src.vertices;
            float baseY = verts.Min(v => tr.TransformPoint(v).y);
            var v3 = new List<Vector3>();
            var n3 = new List<Vector3>();
            var uv = new List<Vector2>();
            var subs = new List<int>[src.subMeshCount];
            for (int s = 0; s < src.subMeshCount; s++)
            {
                subs[s] = new List<int>();
                var tris = src.GetTriangles(s);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                    Vector3 wa = tr.TransformPoint(a), wb = tr.TransformPoint(b), wc = tr.TransformPoint(c);
                    var wn = Vector3.Cross(wb - wa, wc - wa);
                    var ln = Vector3.Cross(b - a, c - a);
                    if (wn.sqrMagnitude < 1e-10f || ln.sqrMagnitude < 1e-10f) continue;
                    wn.Normalize();
                    ln.Normalize();
                    bool wall = Mathf.Abs(wn.y) < 0.6f;
                    var flat = new Vector3(wn.x, 0, wn.z).normalized;
                    var along = Vector3.Cross(Vector3.up, flat);
                    Vector2 Uv(Vector3 w) => wall ? new Vector2(Vector3.Dot(w, along), w.y - baseY) : new Vector2(w.x, w.z);
                    int k = v3.Count;
                    v3.Add(a);
                    v3.Add(b);
                    v3.Add(c);
                    n3.Add(ln);
                    n3.Add(ln);
                    n3.Add(ln);
                    uv.Add(Uv(wa));
                    uv.Add(Uv(wb));
                    uv.Add(Uv(wc));
                    subs[s].Add(k);
                    subs[s].Add(k + 1);
                    subs[s].Add(k + 2);
                }
            }
            var mesh = new Mesh { name = src.name + "_uv" };
            if (v3.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v3);
            mesh.SetNormals(n3);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Length;
            for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
