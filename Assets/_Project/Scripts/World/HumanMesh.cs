using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeoulFirstWeek.World
{
    /// <summary>What a vertex is made of: material slot, texture slice, lighting type, tint and texture scale.</summary>
    public struct Paint
    {
        public const int CharacterSlot = 0, HairSlot = 1, LensSlot = 2;

        public int Slot;
        public float Slice, Type;
        public Color Color;   // sRGB
        public float Metres;  // metres one texture repeat covers
        public float Ao;      // 1 = open, lower = occluded

        public static Paint Cloth(Fabric f, Color c) => new()
        {
            Slot = CharacterSlot, Slice = (int)f, Type = HumanStyle.Sheen[(int)f] ? HumanStyle.TypeSheen : HumanStyle.TypeCloth,
            Color = c, Metres = HumanStyle.Metres[(int)f], Ao = 1,
        };

        public static Paint SkinOf(Color c) => new()
        {
            Slot = CharacterSlot, Slice = (int)Fabric.Skin, Type = HumanStyle.TypeSkin, Color = c, Metres = HumanStyle.Metres[0], Ao = 1,
        };

        public static Paint Face(int slot, Color skin) => new() { Slot = CharacterSlot, Slice = slot, Type = HumanStyle.TypeFace, Color = skin, Metres = 1, Ao = 1 };

        public static Paint HairOf(HairTexture t, Color c) => new() { Slot = HairSlot, Slice = (int)t, Color = c, Metres = 0.16f, Ao = 1 };

        public static Paint Glass => new() { Slot = LensSlot, Color = Color.white, Metres = 1, Ao = 1 };

        public Paint With(Color c)
        {
            var p = this;
            p.Color = c;
            return p;
        }

        public Paint Shaded(float ao)
        {
            var p = this;
            p.Ao = ao;
            return p;
        }
    }

    public delegate BoneWeight Weigher(Vector3 p);

    /// <summary>Bones of the person, in the order the skinned mesh lists them.</summary>
    public static class Bones
    {
        public const int Body = 0, LegL = 1, KneeL = 2, LegR = 3, KneeR = 4, Torso = 5, ArmL = 6, ElbowL = 7, ArmR = 8, ElbowR = 9, Head = 10, Count = 11;

        public static BoneWeight One(int b) => new() { boneIndex0 = b, weight0 = 1 };

        public static Weigher Fixed(int b)
        {
            var w = One(b);
            return _ => w;
        }

        /// <summary>Collects up to four bone weights and normalises them.</summary>
        public struct Mix
        {
            int b0, b1, b2, b3;
            float w0, w1, w2, w3;

            public void Add(int bone, float w)
            {
                if (w <= 1e-4f) return;
                if (bone == b0 && w0 > 0) { w0 += w; return; }
                if (bone == b1 && w1 > 0) { w1 += w; return; }
                if (bone == b2 && w2 > 0) { w2 += w; return; }
                if (bone == b3 && w3 > 0) { w3 += w; return; }
                // replace the smallest slot if this weight is larger
                if (w0 <= 0) { b0 = bone; w0 = w; return; }
                if (w1 <= 0) { b1 = bone; w1 = w; return; }
                if (w2 <= 0) { b2 = bone; w2 = w; return; }
                if (w3 <= 0) { b3 = bone; w3 = w; return; }
                float min = Mathf.Min(Mathf.Min(w0, w1), Mathf.Min(w2, w3));
                if (w <= min) return;
                if (w0 == min) { b0 = bone; w0 = w; }
                else if (w1 == min) { b1 = bone; w1 = w; }
                else if (w2 == min) { b2 = bone; w2 = w; }
                else { b3 = bone; w3 = w; }
            }

            public BoneWeight Weight()
            {
                // BoneWeight wants the weights sorted, largest first
                var pairs = new[] { (b0, w0), (b1, w1), (b2, w2), (b3, w3) };
                Array.Sort(pairs, (a, b) => b.Item2.CompareTo(a.Item2));
                float sum = pairs[0].Item2 + pairs[1].Item2 + pairs[2].Item2 + pairs[3].Item2;
                if (sum <= 0) return One(Body);
                return new BoneWeight
                {
                    boneIndex0 = pairs[0].Item1, weight0 = pairs[0].Item2 / sum,
                    boneIndex1 = pairs[1].Item1, weight1 = pairs[1].Item2 / sum,
                    boneIndex2 = pairs[2].Item1, weight2 = pairs[2].Item2 / sum,
                    boneIndex3 = pairs[3].Item1, weight3 = pairs[3].Item2 / sum,
                };
            }
        }
    }

    /// <summary>Accumulates the vertices of one person and turns them into a skinned mesh.</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> V = new();
        readonly List<Vector3> n = new();
        readonly List<Color> c = new();
        readonly List<Vector2> uv = new();
        readonly List<Vector2> mat = new();
        readonly List<BoneWeight> w = new();
        readonly List<int>[] sub = { new(), new(), new() };

        public int Count => V.Count;

        public int Add(Vector3 p, Vector3 normal, Color srgb, float ao, Vector2 texUv, Paint paint, BoneWeight weight)
        {
            V.Add(p);
            n.Add(normal);
            var lin = srgb.linear;
            c.Add(new Color(lin.r, lin.g, lin.b, Mathf.Clamp01(ao)));
            uv.Add(texUv);
            mat.Add(new Vector2(paint.Slice, paint.Type));
            w.Add(weight);
            return V.Count - 1;
        }

        public void Tri(int slot, int a, int b, int cc)
        {
            sub[slot].Add(a);
            sub[slot].Add(b);
            sub[slot].Add(cc);
        }

        /// <summary>Builds the mesh; <paramref name="slots"/> lists the material slot of each submesh.</summary>
        public Mesh ToMesh(string name, Matrix4x4[] bindposes, out List<int> slots)
        {
            var mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, mat);
            mesh.boneWeights = w.ToArray();
            mesh.bindposes = bindposes;
            slots = new List<int>();
            for (int s = 0; s < sub.Length; s++)
                if (sub[s].Count > 0)
                    slots.Add(s);
            mesh.subMeshCount = slots.Count;
            for (int i = 0; i < slots.Count; i++) mesh.SetTriangles(sub[slots[i]], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A plain (unskinned) mesh for a rigid part such as the mouth or an eyelid.</summary>
        public Mesh ToRigidMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(V);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, mat);
            mesh.subMeshCount = 1;
            var all = new List<int>();
            foreach (var s in sub) all.AddRange(s);
            mesh.SetTriangles(all, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Smooth-shaded building blocks for people: grids, lofts, tubes, ellipsoids and boxes.</summary>
    public static class HumanMesh
    {
        public static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));

        /// <summary>
        /// A grid of vertices (columns i, rows j) turned into triangles, with normals from the neighbouring
        /// vertices, oriented along <paramref name="outward"/>. Wrapping grids repeat the first column (row) as the
        /// last one so the texture seam has its own vertices.
        /// </summary>
        public static void Grid(MeshBuilder mb, Vector3[,] pos, Vector2[,] uvs, Vector3[,] outward, Paint paint, Weigher weigh,
            bool wrapU = false, bool wrapV = false, Color[,] colors = null, float[,] ao = null, bool doubleSided = false, Color? lining = null)
        {
            int nu = pos.GetLength(0), nv = pos.GetLength(1);
            var normals = new Vector3[nu, nv];
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                Vector3 P(int ii, int jj)
                {
                    if (wrapU) ii = ii < 0 ? nu - 2 : ii >= nu ? 1 : ii;
                    else ii = Mathf.Clamp(ii, 0, nu - 1);
                    if (wrapV) jj = jj < 0 ? nv - 2 : jj >= nv ? 1 : jj;
                    else jj = Mathf.Clamp(jj, 0, nv - 1);
                    return pos[ii, jj];
                }
                var du = P(i + 1, j) - P(i - 1, j);
                var dv = P(i, j + 1) - P(i, j - 1);
                var nrm = Vector3.Cross(du, dv);
                var o = outward[i, j];
                if (nrm.sqrMagnitude < 1e-14f) nrm = o;
                nrm.Normalize();
                if (Vector3.Dot(nrm, o) < 0) nrm = -nrm;
                normals[i, j] = nrm;
            }

            var idx = new int[nu, nv];
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                var p = pos[i, j];
                var col = colors != null ? colors[i, j] : paint.Color;
                float a = (ao != null ? ao[i, j] : 1f) * paint.Ao;
                idx[i, j] = mb.Add(p, normals[i, j], col, a, uvs[i, j], paint, weigh(p));
            }
            Quads(mb, paint.Slot, pos, normals, idx, false);

            if (!doubleSided) return;
            var inner = new int[nu, nv];
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                var p = pos[i, j] - normals[i, j] * 0.0015f;
                var col = lining ?? (colors != null ? colors[i, j] : paint.Color) * 0.55f;
                col.a = 1;
                inner[i, j] = mb.Add(p, -normals[i, j], col, 0.6f * paint.Ao, uvs[i, j], paint, weigh(pos[i, j]));
            }
            Quads(mb, paint.Slot, pos, normals, inner, true);
        }

        static void Quads(MeshBuilder mb, int slot, Vector3[,] pos, Vector3[,] normals, int[,] idx, bool flip)
        {
            int nu = pos.GetLength(0), nv = pos.GetLength(1);
            for (int i = 0; i < nu - 1; i++)
            for (int j = 0; j < nv - 1; j++)
            {
                var p00 = pos[i, j];
                var p10 = pos[i + 1, j];
                var p11 = pos[i + 1, j + 1];
                var p01 = pos[i, j + 1];
                var g = Vector3.Cross(p10 - p00, p11 - p00);
                if (g.sqrMagnitude < 1e-16f) g = Vector3.Cross(p11 - p00, p01 - p00);
                var avg = normals[i, j] + normals[i + 1, j + 1] + normals[i + 1, j] + normals[i, j + 1];
                bool forward = Vector3.Dot(g, avg) >= 0;
                if (flip) forward = !forward;
                int a = idx[i, j], b = idx[i + 1, j], c = idx[i + 1, j + 1], d = idx[i, j + 1];
                if (forward)
                {
                    mb.Tri(slot, a, b, c);
                    mb.Tri(slot, a, c, d);
                }
                else
                {
                    mb.Tri(slot, a, c, b);
                    mb.Tri(slot, a, d, c);
                }
            }
        }

        // ------------------------------------------------------------------ lofts

        /// <summary>
        /// One cross-section of a loft: an ellipse at height Y around (Cx, Cz), pushed out at the front or back,
        /// optionally open at the front (Gap = half-angle of the opening in degrees) and pleated.
        /// </summary>
        public struct Ring
        {
            public float Y, Rx, Rz, Cx, Cz, Front, Back, Gap, Pleat, Ao;
            public Color? Color;

            public Ring(float y, float rx, float rz, float cx = 0, float cz = 0, float front = 0, float back = 0)
            {
                Y = y;
                Rx = rx;
                Rz = rz;
                Cx = cx;
                Cz = cz;
                Front = front;
                Back = back;
                Gap = 0;
                Pleat = 0;
                Ao = 1;
                Color = null;
            }

            public Ring Grow(float d) => new(Y, Rx + d, Rz + d, Cx, Cz, Front, Back) { Gap = Gap, Pleat = Pleat, Ao = Ao, Color = Color };
        }

        public struct LoftOptions
        {
            public float CapTop, CapBottom;  // dome height as a fraction of the end radius (0 = open end)
            public bool DoubleSided;
            public Color? Lining;
            public int PleatCount;
            public Matrix4x4? Transform;     // applied after building (e.g. shoes lie along z)
            public float UStart;             // texture offset around (hair partings line up across pieces)
            public float Wrinkle;            // cloth folds: radius change as a fraction (0.02-0.04)
            public float WrinkleAround;      // fold frequency around the tube (per radian)
            public float WrinkleAlong;       // fold frequency along it (per metre); low = long hanging folds
        }

        /// <summary>A tube through a list of rings (ordered top to bottom), smooth around and along.</summary>
        public static void Loft(MeshBuilder mb, IList<Ring> ringsIn, int seg, Paint paint, Weigher weigh, LoftOptions o = default)
        {
            var rings = new List<Ring>(ringsIn);
            if (o.CapTop > 0) Cap(rings, true, o.CapTop);
            if (o.CapBottom > 0) Cap(rings, false, o.CapBottom);
            bool open = false;
            foreach (var r in rings)
                if (r.Gap > 0.01f)
                    open = true;

            int nu = seg + 1, nv = rings.Count;
            var pos = new Vector3[nu, nv];
            var outward = new Vector3[nu, nv];
            var uvs = new Vector2[nu, nv];
            var cols = new Color[nu, nv];
            var ao = new float[nu, nv];

            float avgR = 0;
            foreach (var r in rings) avgR += (r.Rx + r.Rz) * 0.5f;
            avgR /= Mathf.Max(1, rings.Count);
            float repeats = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * avgR / paint.Metres));
            float v = 0;
            for (int j = 0; j < nv; j++)
            {
                var r = rings[j];
                if (j > 0)
                {
                    var q = rings[j - 1];
                    float dr = (r.Rx + r.Rz - q.Rx - q.Rz) * 0.5f;
                    v += Mathf.Sqrt((r.Y - q.Y) * (r.Y - q.Y) + dr * dr);
                }
                for (int i = 0; i < nu; i++)
                {
                    float t = i / (float)seg;
                    float th = open ? Mathf.Lerp(r.Gap, 360f - r.Gap, t) : 360f * t;
                    float a = th * Mathf.Deg2Rad;
                    float s = Mathf.Sin(a), cs = Mathf.Cos(a);
                    float bulge = 1f + r.Front * Mathf.Pow(Mathf.Max(0, cs), 2) + r.Back * Mathf.Pow(Mathf.Max(0, -cs), 2);
                    if (o.PleatCount > 0) bulge += r.Pleat * Mathf.Sin(a * o.PleatCount);
                    if (o.Wrinkle > 0)
                    {
                        float seed = paint.Slice * 3.1f + paint.Color.r * 17f + paint.Color.g * 7f;
                        float fold = Mathf.PerlinNoise(a * o.WrinkleAround + seed, r.Y * o.WrinkleAlong + seed * 0.7f) - 0.5f;
                        float fine = Mathf.PerlinNoise(a * o.WrinkleAround * 2.3f + seed * 1.9f, r.Y * o.WrinkleAlong * 2.1f) - 0.5f;
                        bulge += (fold * 1.6f + fine * 0.6f) * o.Wrinkle;
                    }
                    var p = new Vector3(r.Cx + r.Rx * bulge * s, r.Y, r.Cz + r.Rz * bulge * cs);
                    pos[i, j] = p;
                    outward[i, j] = new Vector3(p.x - r.Cx, 0, p.z - r.Cz);
                    float u = open ? a * avgR / paint.Metres : t * repeats;
                    uvs[i, j] = new Vector2(u + o.UStart, v / paint.Metres);
                    cols[i, j] = r.Color ?? paint.Color;
                    ao[i, j] = r.Ao;
                }
            }
            // caps: poles point up or down
            if (o.CapTop > 0)
                for (int i = 0; i < nu; i++)
                    outward[i, 0] += Vector3.up * 0.5f;
            if (o.CapBottom > 0)
                for (int i = 0; i < nu; i++)
                    outward[i, nv - 1] += Vector3.down * 0.5f;
            for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
                if (outward[i, j].sqrMagnitude < 1e-12f)
                    outward[i, j] = j < nv / 2 ? Vector3.up : Vector3.down;

            if (o.Transform.HasValue)
            {
                var m = o.Transform.Value;
                for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    pos[i, j] = m.MultiplyPoint3x4(pos[i, j]);
                    outward[i, j] = m.MultiplyVector(outward[i, j]);
                }
            }
            Grid(mb, pos, uvs, outward, paint, weigh, !open, false, cols, ao, o.DoubleSided, o.Lining);
        }

        /// <summary>Adds dome rings beyond the first (top) or last (bottom) ring, closing the tube in a pole.</summary>
        static void Cap(List<Ring> rings, bool top, float height)
        {
            var end = top ? rings[0] : rings[^1];
            float h = Mathf.Min(end.Rx, end.Rz) * height;
            var extra = new List<Ring>();
            const int k = 4;
            for (int s = 1; s <= k; s++)
            {
                float a = s / (float)k * Mathf.PI * 0.5f;
                var r = end;
                r.Rx = end.Rx * Mathf.Cos(a);
                r.Rz = end.Rz * Mathf.Cos(a);
                r.Y = end.Y + (top ? 1 : -1) * h * Mathf.Sin(a);
                if (s == k)
                {
                    r.Rx = r.Rz = 0;
                }
                extra.Add(r);
            }
            if (top)
            {
                extra.Reverse();
                rings.InsertRange(0, extra);
            }
            else rings.AddRange(extra);
        }

        // ------------------------------------------------------------------ tubes

        /// <summary>A round tube along a path (straps, cords, frames), rounded at open ends.</summary>
        public static void Tube(MeshBuilder mb, IList<Vector3> path, Func<float, float> radius, int seg, Paint paint, Weigher weigh,
            bool loop = false, float flatten = 1f, Vector3? up = null)
        {
            var pts = new List<Vector3>(path);
            int count = pts.Count;
            if (loop) pts.Add(pts[0]);
            int nu = pts.Count;
            int nv = seg + 1;
            var pos = new Vector3[nu, nv];
            var outward = new Vector3[nu, nv];
            var uvs = new Vector2[nu, nv];

            // parallel-transport frames
            var tangents = new Vector3[nu];
            for (int i = 0; i < nu; i++)
            {
                Vector3 a = i > 0 ? pts[i - 1] : loop ? pts[count - 1] : pts[i];
                Vector3 b = i < nu - 1 ? pts[i + 1] : loop ? pts[1] : pts[i];
                tangents[i] = (b - a).normalized;
            }
            var refUp = up ?? Vector3.up;
            var normal = Vector3.Cross(tangents[0], refUp);
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(tangents[0], Vector3.right);
            normal.Normalize();
            float len = 0;
            for (int i = 0; i < nu; i++)
            {
                if (i > 0)
                {
                    var rot = Quaternion.FromToRotation(tangents[i - 1], tangents[i]);
                    normal = (rot * normal).normalized;
                    len += (pts[i] - pts[i - 1]).magnitude;
                }
                var bin = Vector3.Cross(tangents[i], normal).normalized;
                float t = i / (float)(nu - 1);
                float r = radius(t);
                for (int j = 0; j < nv; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2f;
                    var off = normal * (Mathf.Cos(a) * r) + bin * (Mathf.Sin(a) * r * flatten);
                    pos[i, j] = pts[i] + off;
                    outward[i, j] = off.sqrMagnitude > 1e-12f ? off : tangents[i];
                    uvs[i, j] = new Vector2(j / (float)seg, len / paint.Metres);
                }
            }
            Grid(mb, pos, uvs, outward, paint, weigh, loop, true);
        }

        // ------------------------------------------------------------------ ellipsoids and boxes

        /// <summary>An ellipsoid (optionally a latitude band of one), with an optional per-point deformation.</summary>
        public static void Ellipsoid(MeshBuilder mb, Vector3 center, Vector3 radii, Quaternion rot, int lon, int lat, Paint paint, Weigher weigh,
            Func<Vector3, Vector3> deform = null, float latFrom = -90f, float latTo = 90f)
        {
            int nu = lon + 1, nv = lat + 1;
            var pos = new Vector3[nu, nv];
            var outward = new Vector3[nu, nv];
            var uvs = new Vector2[nu, nv];
            float avg = (radii.x + radii.y + radii.z) / 3f;
            float repeats = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * avg / paint.Metres));
            for (int j = 0; j < nv; j++)
            {
                float phi = Mathf.Lerp(latFrom, latTo, j / (float)lat) * Mathf.Deg2Rad;
                for (int i = 0; i < nu; i++)
                {
                    float lam = i / (float)lon * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(phi) * Mathf.Sin(lam), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Cos(lam));
                    var local = Vector3.Scale(d, radii);
                    if (deform != null) local = deform(local);
                    pos[i, j] = center + rot * local;
                    outward[i, j] = rot * new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
                    uvs[i, j] = new Vector2(i / (float)lon * repeats, (phi + Mathf.PI * 0.5f) * avg / paint.Metres);
                }
            }
            Grid(mb, pos, uvs, outward, paint, weigh, true);
        }

        /// <summary>A flat-shaded box (badges, buttons, buckles, phone).</summary>
        public static void Box(MeshBuilder mb, Vector3 center, Vector3 size, Quaternion rot, Paint paint, Weigher weigh)
        {
            var h = size * 0.5f;
            var w = weigh(center);
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nrm in dirs)
            {
                var a = Mathf.Abs(nrm.x) > 0.5f ? Vector3.forward : Vector3.right;
                var b = Vector3.Cross(nrm, a);
                var ea = Vector3.Scale(a, h);
                var eb = Vector3.Scale(b, h);
                var c = center + rot * Vector3.Scale(nrm, h);
                var corners = new[] { c + rot * (-ea - eb), c + rot * (ea - eb), c + rot * (ea + eb), c + rot * (-ea + eb) };
                var wn = rot * nrm;
                int i0 = mb.Count;
                for (int k = 0; k < 4; k++)
                {
                    var uv = new Vector2((k == 1 || k == 2 ? size.magnitude : 0) / paint.Metres, (k >= 2 ? size.magnitude : 0) / paint.Metres);
                    mb.Add(corners[k], wn, paint.Color, paint.Ao, uv, paint, w);
                }
                var g = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
                if (Vector3.Dot(g, wn) >= 0)
                {
                    mb.Tri(paint.Slot, i0, i0 + 1, i0 + 2);
                    mb.Tri(paint.Slot, i0, i0 + 2, i0 + 3);
                }
                else
                {
                    mb.Tri(paint.Slot, i0, i0 + 2, i0 + 1);
                    mb.Tri(paint.Slot, i0, i0 + 3, i0 + 2);
                }
            }
        }
    }
}
