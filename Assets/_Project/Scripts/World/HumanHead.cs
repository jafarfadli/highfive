using System.Collections.Generic;
using UnityEngine;
using static SeoulFirstWeek.World.HumanMesh;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// Head, face, neck, ears, hair styles, hats, masks, glasses and earphones. Everything is built in "face
    /// space" (rig units relative to the head centre, the space of <see cref="FaceMap"/>) and then placed on
    /// the body, so the sculpted features line up with the painted face.
    /// </summary>
    public static class HumanHead
    {
        static float Scale(HumanContext c) => c.L.Female ? 1.0f : 1.04f;
        static Vector3 ToRoot(HumanContext c, Vector3 f) => c.HeadCenter + f * Scale(c);

        static float G(float dx, float dy, float sx, float sy) => Mathf.Exp(-(dx * dx) / (2 * sx * sx) - (dy * dy) / (2 * sy * sy));

        static Vector3 Dir(float phiDeg, float lamDeg)
        {
            float phi = phiDeg * Mathf.Deg2Rad, lam = lamDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(phi) * Mathf.Sin(lam), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Cos(lam));
        }

        // width of the jaw at the chin relative to the skull, set per person while their head is built
        static float jaw = 0.8f;

        static float Jaw(HumanLook l) => (l.Female ? 0.8f : 0.87f) + (l.Build - 0.5f) * 0.06f;

        /// <summary>The head surface in face space for a direction (latitude phi, longitude lambda from the front, degrees).</summary>
        public static Vector3 Surface(float phiDeg, float lamDeg, bool details)
        {
            var d = Dir(phiDeg, lamDeg);
            var p = new Vector3(FaceMap.Rx * d.x, FaceMap.Ry * d.y, FaceMap.Rz * d.z);
            if (p.z < 0) p.z *= 1.07f; // fuller back of the skull
            float low = Smooth(0.1f, -0.9f, d.y);
            float front = Smooth(-0.35f, 0.55f, d.z);
            // the jaw stays wide down to its angle and narrows towards the chin
            p.x *= Mathf.Lerp(1f, jaw, Smooth(-0.25f, -1f, d.y) * front);
            float backLow = low * Smooth(0.15f, -0.5f, d.z);
            p.z = Mathf.Lerp(p.z, p.z * 0.55f, backLow); // the back of the jaw tucks into the neck
            p.x *= Mathf.Lerp(1f, 0.82f, backLow);
            float f = Smooth(0.25f, 0.75f, d.z);
            p.z += 0.009f * G(p.x, p.y + 0.104f, 0.018f, 0.014f) * f; // chin
            if (!details) return p;

            float x = p.x, y = p.y, ax = Mathf.Abs(x);
            float dz = 0;
            // eye sockets and brow ridge
            dz -= 0.011f * G(ax - FaceMap.EyeX, y - FaceMap.EyeY, 0.0135f, 0.0085f);
            dz += 0.006f * G(x, y - 0.036f, 0.048f, 0.009f);
            dz -= 0.004f * G(ax - 0.074f, y - 0.03f, 0.01f, 0.022f); // temples
            // nose: a narrow bridge widening into the tip, with the wings beside it
            float bridge = Smooth(0.028f, -0.026f, y) * Smooth(-0.047f, -0.033f, y);
            float width = Mathf.Lerp(0.005f, 0.0105f, Smooth(0f, -0.034f, y));
            dz += 0.027f * bridge * G(x, 0, width, 1);
            dz += 0.008f * G(ax - 0.0118f, y + 0.0345f, 0.0048f, 0.0042f);
            // lips, the groove under them, the chin
            dz += 0.0078f * G(x, y - (FaceMap.MouthY + 0.0045f), 0.016f, 0.0042f);
            dz += 0.0084f * G(x, y - (FaceMap.MouthY - 0.0062f), 0.014f, 0.0042f);
            dz -= 0.0022f * G(x, y - FaceMap.MouthY, 0.018f, 0.0011f);
            dz -= 0.0036f * G(x, y - (FaceMap.MouthY - 0.0175f), 0.014f, 0.0038f);
            dz += 0.004f * G(x, y + 0.104f, 0.015f, 0.011f); // chin
            // cheekbones and the hollow under them
            dz += 0.006f * G(ax - 0.052f, y + 0.004f, 0.017f, 0.012f);
            dz -= 0.0025f * G(ax - 0.055f, y + 0.05f, 0.014f, 0.016f);
            p.z += dz * f;
            p.x += Mathf.Sign(x) * 0.0035f * G(ax - 0.066f, y + 0.008f, 0.012f, 0.016f) * front;
            return p;
        }

        /// <summary>Outward direction of the (undeformed) skull, used to lift hair and hats off the head.</summary>
        static Vector3 SkullNormal(float phiDeg, float lamDeg)
        {
            var d = Dir(phiDeg, lamDeg);
            return new Vector3(d.x / FaceMap.Rx, d.y / FaceMap.Ry, d.z / FaceMap.Rz).normalized;
        }

        public static void Build(HumanContext c)
        {
            jaw = Jaw(c.L);
            Neck(c);
            HeadMesh(c);
            Ears(c);
            Hair(c);
            Hat(c);
            if (c.L.Mask) Mask(c);
            if (c.L.Glasses != GlassesKind.None) Glasses(c);
            if (c.L.Earphones) Earphones(c);
        }

        // ------------------------------------------------------------------ neck and head

        static void Neck(HumanContext c)
        {
            float rx = c.NeckRx, rz = c.NeckRz;
            var top = c.HeadCenter.y - 0.075f * Scale(c);
            var rings = new List<Ring>();
            for (int k = 0; k <= 5; k++)
            {
                float y = Mathf.Lerp(top, 1.495f, k / 5f);
                var ring = new Ring(y, rx * Mathf.Lerp(0.95f, 1.05f, k / 5f), rz * Mathf.Lerp(0.95f, 1.05f, k / 5f), 0, Mathf.Lerp(0.004f, -0.004f, k / 5f));
                ring.Ao = k == 0 ? 0.65f : 1f;
                rings.Add(ring);
            }
            Loft(c.Mb, rings, c.SegLimb, Paint.SkinOf(c.L.Skin), HumanBody.Neck);
        }

        static void HeadMesh(HumanContext c)
        {
            var l = c.L;
            int rows = c.High ? 44 : 18, faceCols = c.High ? 40 : 18, backCols = c.High ? 24 : 12;
            Weigher head = Bones.Fixed(Bones.Head);

            // face: a front patch with the painted face projected onto it
            var pos = new Vector3[faceCols + 1, rows + 1];
            var outward = new Vector3[faceCols + 1, rows + 1];
            var uvs = new Vector2[faceCols + 1, rows + 1];
            for (int j = 0; j <= rows; j++)
            {
                float phi = Mathf.Asin(Mathf.Lerp(-1f, 1f, j / (float)rows)) * Mathf.Rad2Deg;
                for (int i = 0; i <= faceCols; i++)
                {
                    float t = Mathf.Lerp(-1f, 1f, i / (float)faceCols);
                    float lam = 100f * Mathf.Sign(t) * Mathf.Pow(Mathf.Abs(t), 1.35f);
                    var f = Surface(phi, lam, true);
                    pos[i, j] = ToRoot(c, f);
                    outward[i, j] = f;
                    uvs[i, j] = FaceMap.ToUV(f.x, f.y);
                }
            }
            Grid(c.Mb, pos, uvs, outward, Paint.Face(l.Face, l.Skin), head);

            // back of the head: plain skin (mostly under hair)
            pos = new Vector3[backCols + 1, rows + 1];
            outward = new Vector3[backCols + 1, rows + 1];
            uvs = new Vector2[backCols + 1, rows + 1];
            var skin = Paint.SkinOf(l.Skin);
            for (int j = 0; j <= rows; j++)
            {
                float phi = Mathf.Asin(Mathf.Lerp(-1f, 1f, j / (float)rows)) * Mathf.Rad2Deg;
                for (int i = 0; i <= backCols; i++)
                {
                    float lam = Mathf.Lerp(100f, 260f, i / (float)backCols);
                    var f = Surface(phi, lam, false);
                    pos[i, j] = ToRoot(c, f);
                    outward[i, j] = f;
                    uvs[i, j] = new Vector2(lam / 360f * 2f * Mathf.PI * FaceMap.Rx / skin.Metres, (phi + 90f) / 180f * Mathf.PI * FaceMap.Ry / skin.Metres);
                }
            }
            Grid(c.Mb, pos, uvs, outward, skin, head);
        }

        static void Ears(HumanContext c)
        {
            var paint = Paint.SkinOf(c.L.Skin);
            foreach (float s in new[] { -1f, 1f })
            {
                var center = new Vector3(s * (FaceMap.Rx - 0.003f), FaceMap.EyeY - 0.012f, -0.012f);
                var rot = Quaternion.Euler(0, s * 18f, s * -6f);
                Ellipsoid(c.Mb, ToRoot(c, center), new Vector3(0.011f, 0.029f, 0.019f) * Scale(c), rot, c.High ? 12 : 6, c.High ? 8 : 4,
                    paint, Bones.Fixed(Bones.Head),
                    p => new Vector3(p.x + s * 0.004f * Mathf.Max(0, p.z / 0.019f) - s * 0.003f * Mathf.Max(0, -p.y / 0.029f), p.y, p.z));
            }
        }

        // ------------------------------------------------------------------ hair

        static float Natural(float lam)
        {
            float a = Mathf.Abs(lam);
            float[] k = { 0, 54, 30, 51, 55, 36, 80, 16, 100, 6, 125, -12, 150, -26, 180, -34 };
            for (int i = 0; i < k.Length - 2; i += 2)
                if (a <= k[i + 2])
                    return Mathf.Lerp(k[i + 1], k[i + 3], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(k[i], k[i + 2], a)));
            return k[^1];
        }

        static bool Long(HairStyle s) => s is HairStyle.LongStraight or HairStyle.Layered or HairStyle.Bob;

        static float Hairline(HumanLook l, float lam)
        {
            float a = Mathf.Abs(lam);
            float nat = Natural(lam);
            switch (l.Hair)
            {
                case HairStyle.Receding:
                    return Mathf.Max(nat, a < 30f ? 63f : Mathf.Lerp(54f, nat, Smooth(30f, 70f, a)));
                case HairStyle.SidePart:
                    return Mathf.Min(nat, Mathf.Lerp(44f, nat, Smooth(25f, 60f, a)));
                case HairStyle.ShortPerm:
                    return Mathf.Max(nat, a > 140 ? -22f : nat);
                case HairStyle.TwoBlock:
                case HairStyle.Comma:
                case HairStyle.MiddlePart:
                    // the long top layer falls over the temples
                    return nat - 11f * G(a - 50f, 0, 13f, 1);
                default:
                    return nat;
            }
        }

        static float Thickness(HumanLook l, float phi, float lam, float hairline, int seed)
        {
            float a = Mathf.Abs(lam);
            float edge = Smooth(hairline, hairline + 10f, phi); // thin right at the hairline
            float t;
            switch (l.Hair)
            {
                case HairStyle.TwoBlock:
                case HairStyle.Comma:
                {
                    // a long, full top layer over a short undercut: the top layer comes down over the upper sides
                    // and the back of the skull, the undercut only shows behind the ears and at the nape
                    float block = a < 50f ? hairline - 2f : BlockEdge(a);
                    float f = Smooth(block - 3f, block + 8f, phi);
                    float top = l.Hair == HairStyle.Comma ? 0.035f : 0.032f;
                    // fullest over the crown; at the sides the hair lies flatter down to the block's edge
                    float body = Mathf.Lerp(0.8f, 1f, Smooth(5f, 35f, phi)) * Mathf.Lerp(1f, 0.88f, Smooth(72f, 90f, phi));
                    if (a >= 50f) body *= Mathf.Lerp(0.45f, 1f, Smooth(block, block + 30f, phi));
                    // the fringe locks cover the forehead, so the shell steps back there
                    float front = a < 45f ? Mathf.Lerp(0.45f, 1f, Smooth(58f, 76f, phi)) : 1f;
                    t = Mathf.Lerp(0.0045f, top * body * front, f);
                    break;
                }
                case HairStyle.SidePart:
                    // a 2:8 parting with volume swept up from it, full at the sides
                    t = Mathf.Lerp(0.013f, 0.03f, Smooth(8f, 45f, phi)) * (1f - 0.45f * G(lam + 32f, 0, 4.5f, 1) * Smooth(38f, 50f, phi)) * Mathf.Lerp(0.6f, 1f, edge);
                    break;
                case HairStyle.MiddlePart:
                    // 가르마펌: soft waves parted in the middle, long enough to cover the tops of the ears
                    t = Mathf.Lerp(0.014f, 0.033f, Smooth(0f, 45f, phi)) * (1f - 0.6f * G(lam, 0, 3.5f, 1) * Smooth(40f, 52f, phi)) * Mathf.Lerp(0.55f, 1f, edge);
                    if (a < 45f) t *= Mathf.Lerp(0.5f, 1f, Smooth(58f, 76f, phi));
                    break;
                case HairStyle.Buzz:
                    t = 0.0028f;
                    break;
                case HairStyle.Receding:
                    t = Mathf.Lerp(0.008f, 0.013f, Smooth(20f, 60f, phi)) * Mathf.Lerp(0.6f, 1f, edge);
                    break;
                case HairStyle.Ponytail:
                case HairStyle.LowBun:
                    // pulled back, but with some lift over the crown and fullness above the ears
                    t = Mathf.Lerp(0.008f, 0.016f, Smooth(25f, 75f, phi)) + 0.004f * G(a - 95f, 0, 30f, 1) * Smooth(0f, 30f, phi);
                    t *= Mathf.Lerp(0.6f, 1f, edge);
                    break;
                case HairStyle.ShortPerm:
                {
                    float curl = Mathf.PerlinNoise(lam * 0.16f + seed, phi * 0.2f + seed * 0.3f) - 0.5f;
                    float curl2 = Mathf.PerlinNoise(lam * 0.4f + seed * 2f, phi * 0.45f) - 0.5f;
                    t = 0.028f + 0.014f * Smooth(30f, 85f, phi) + curl * 0.016f + curl2 * 0.006f;
                    t *= Mathf.Lerp(0.55f, 1f, edge);
                    break;
                }
                default: // long hair and bobs: a thin cap under the locks, with a parting
                {
                    t = Mathf.Lerp(0.006f, 0.009f, Smooth(20f, 70f, phi));
                    if (!l.Bangs) t *= 1f - 0.5f * G(lam, 0, 3.2f, 1) * Smooth(42f, 54f, phi);
                    t *= Mathf.Lerp(0.6f, 1f, edge);
                    break;
                }
            }
            // under a hat the hair lies flat
            if (l.Hat is HatKind.Cap or HatKind.BucketHat or HatKind.Beanie or HatKind.GuardCap) t = Mathf.Min(t, Mathf.Lerp(0.009f, 0.004f, Smooth(0f, 25f, phi)));
            return t;
        }

        /// <summary>Latitude of a two-block's top-layer edge: above the ears at the sides, over the occiput at the back.</summary>
        static float BlockEdge(float a) => Mathf.Lerp(18f, 2f, Smooth(70f, 160f, a));

        /// <summary>The hair shell's thickness at a point, fading out below the hairline (for locks laid over it).</summary>
        static float ShellAt(HumanContext c, float phi, float lam)
        {
            float hl = Hairline(c.L, lam);
            return Thickness(c.L, Mathf.Max(phi, hl), lam, hl, 0) * Smooth(hl - 14f, hl, phi);
        }

        static HairTexture StrandTexture(HairStyle s) => s switch
        {
            HairStyle.ShortPerm => HairTexture.Curly,
            HairStyle.Buzz => HairTexture.Buzz,
            HairStyle.Layered or HairStyle.MiddlePart => HairTexture.Wavy,
            _ => HairTexture.Straight,
        };

        static void Hair(HumanContext c)
        {
            var l = c.L;
            var paint = Paint.HairOf(StrandTexture(l.Hair), l.HairColor);
            // the light version keeps enough rows that its flat quads don't dip into the skull under thin hair
            int cols = c.High ? 48 : 28, rows = c.High ? 14 : 10;
            float allowance = c.High ? 0f : 0.0015f;
            int seed = Mathf.Abs(l.HairColor.GetHashCode() % 97) + (int)l.Hair * 13;
            Weigher head = Bones.Fixed(Bones.Head);

            // the shell: rows from the hairline (plus a tucked-in lip) up to the crown
            var pos = new Vector3[cols + 1, rows + 2];
            var outward = new Vector3[cols + 1, rows + 2];
            var uvs = new Vector2[cols + 1, rows + 2];
            var colors = new Color[cols + 1, rows + 2];
            var ao = new float[cols + 1, rows + 2];
            for (int i = 0; i <= cols; i++)
            {
                float lam = Mathf.Lerp(-180f, 180f, i / (float)cols);
                float hl = Hairline(l, lam);
                for (int j = 0; j <= rows + 1; j++)
                {
                    float s = j == 0 ? 0 : (j - 1) / (float)rows;
                    float phi = Mathf.Lerp(hl, 90f, Mathf.Pow(s, 0.85f));
                    float t = j == 0 ? -0.002f : Thickness(l, phi, lam, hl, seed);
                    var f = Surface(phi, lam, false) + SkullNormal(phi, lam) * (j == 0 ? t : t + allowance);
                    pos[i, j] = ToRoot(c, f);
                    outward[i, j] = f;
                    uvs[i, j] = new Vector2((lam + 180f) / 360f * 4f, (90f - phi) * Mathf.Deg2Rad * 0.11f / paint.Metres);
                    var col = l.HairColor;
                    if (l.Hair is HairStyle.TwoBlock or HairStyle.Comma && Mathf.Abs(lam) > 40f && t < 0.008f)
                        col = Color.Lerp(l.HairColor * 0.85f, l.Skin * 0.6f, 0.3f); // the shaved undercut shows some scalp
                    if (l.Hair == HairStyle.Buzz) col = Color.Lerp(l.HairColor, l.Skin * 0.6f, 0.3f);
                    col.a = 1;
                    colors[i, j] = col;
                    ao[i, j] = j == 0 ? 0.55f : Mathf.Lerp(0.8f, 1f, Smooth(0, 3, j));
                }
            }
            Grid(c.Mb, pos, uvs, outward, paint, head, true, false, colors, ao);

            if (Long(l.Hair)) LongHair(c, paint);
            if (l.Bangs && l.Hair is HairStyle.LongStraight or HairStyle.Layered or HairStyle.Ponytail) Bangs(c, paint, false);
            if (l.Bangs && l.Hair == HairStyle.Bob) Bangs(c, paint, true);
            // men's cuts get their length and volume from locks over the shell (flattened under a hat instead)
            bool hatted = l.Hat is HatKind.Cap or HatKind.BucketHat or HatKind.Beanie or HatKind.GuardCap;
            if (!hatted && l.Hair is HairStyle.TwoBlock or HairStyle.Comma or HairStyle.MiddlePart)
            {
                Fringe(c, paint);
                SideLocks(c, paint);
            }
            if (!hatted && l.Hair == HairStyle.SidePart) Sweep(c, paint);
            if (l.Hair == HairStyle.Ponytail) Ponytail(c, paint);
            if (l.Hair == HairStyle.LowBun) Bun(c, paint);
        }

        // ------------------------------------------------------------------ hair cards

        /// <summary>
        /// A hair card: a ribbon of strands along a path (root space), slightly bowed so it reads as a lock,
        /// tapering to its tip, double-sided. Long hair, bangs and fringes are many overlapping cards.
        /// </summary>
        static void Card(HumanContext c, Paint paint, List<Vector3> path, float rootWidth, float tipWidth, Weigher w, float uOffset)
        {
            int n = path.Count;
            var pos = new Vector3[3, n];
            var outward = new Vector3[3, n];
            var uvs = new Vector2[3, n];
            var ao = new float[3, n];
            float len = 0;
            var head = c.HeadCenter;
            for (int j = 0; j < n; j++)
            {
                var p = path[j];
                if (j > 0) len += (p - path[j - 1]).magnitude;
                var tan = (path[Mathf.Min(n - 1, j + 1)] - path[Mathf.Max(0, j - 1)]).normalized;
                var rel = p - head;
                var outDir = new Vector3(rel.x, p.y > head.y - 0.05f ? rel.y : 0f, rel.z).normalized;
                var across = Vector3.Cross(tan, outDir);
                if (across.sqrMagnitude < 1e-8f) across = Vector3.Cross(tan, Vector3.forward);
                across.Normalize();
                float s = j / (float)(n - 1);
                float width = Mathf.Lerp(rootWidth, tipWidth, s) * (s > 0.8f ? Mathf.Lerp(1f, 0.3f, (s - 0.8f) / 0.2f) : 1f);
                for (int i = 0; i < 3; i++)
                {
                    float a = (i - 1) * 0.5f;
                    pos[i, j] = p + across * (a * width) + outDir * (0.0022f * (1 - Mathf.Abs(i - 1)));
                    outward[i, j] = outDir;
                    uvs[i, j] = new Vector2(uOffset + a * width / paint.Metres * 3f, len / paint.Metres);
                    ao[i, j] = Mathf.Lerp(0.75f, 1f, Smooth(0f, 0.25f, s));
                }
            }
            Grid(c.Mb, pos, uvs, outward, paint, w, false, false, null, ao, true, c.L.HairColor * 0.5f);
        }

        /// <summary>A point on the scalp (root space), lifted by t, at latitude phi and longitude lam.</summary>
        static Vector3 Scalp(HumanContext c, float phi, float lam, float t) => ToRoot(c, Surface(phi, lam, false) + SkullNormal(phi, lam) * t);

        /// <summary>Keeps hanging hair behind the shoulders and off the back.</summary>
        static Vector3 ClearOfBody(HumanContext c, Vector3 p, float extra)
        {
            if (p.y > HumanRig.NeckY + 0.05f || p.y < c.Torso.Bottom) return p;
            var ring = c.Torso.At(p.y, extra + 0.014f);
            float u = p.x / ring.Rx;
            if (Mathf.Abs(u) >= 1f) return p;
            float back = ring.Cz - ring.Rz * (1 + ring.Back) * Mathf.Sqrt(1 - u * u);
            float blend = Smooth(HumanRig.NeckY + 0.05f, HumanRig.NeckY - 0.03f, p.y);
            if (p.z > back) p.z = Mathf.Lerp(p.z, back, blend);
            return p;
        }

        static float OuterThickness(HumanLook l) =>
            l.Outer is OuterKind.None or OuterKind.Apron ? 0.012f : l.Outer is OuterKind.Puffer or OuterKind.PaddedVest ? 0.06f : 0.03f;

        /// <summary>A lock from its root near the parting, over the skull to just below the ear, then hanging down.</summary>
        static List<Vector3> HangPath(HumanContext c, float lam, float lamRoot, float phiRoot, float exitPhi, float t0, float t1, float endFaceY, float flare, float curl)
        {
            var pts = new List<Vector3>();
            int skull = c.High ? 7 : 4, drop = c.High ? 9 : 5;
            for (int k = 0; k <= skull; k++)
            {
                float s = k / (float)skull;
                float e = Mathf.SmoothStep(0, 1, s);
                pts.Add(Scalp(c, Mathf.Lerp(phiRoot, exitPhi, s), Mathf.Lerp(lamRoot, lam, Mathf.Min(1f, e * 1.4f)), Mathf.Lerp(t0, t1, s)));
            }
            var exit = pts[^1];
            var outH = new Vector3(exit.x, 0, exit.z - c.HeadCenter.z).normalized;
            float endY = c.HeadCenter.y + endFaceY * Scale(c);
            float extra = OuterThickness(c.L);
            for (int m = 1; m <= drop; m++)
            {
                float s = m / (float)drop;
                var p = new Vector3(exit.x, Mathf.Lerp(exit.y, endY, s), exit.z) + outH * (flare * Mathf.Sin(s * Mathf.PI * 0.6f));
                if (s > 0.7f) p -= outH * curl * Smooth(0.7f, 1f, s);
                pts.Add(ClearOfBody(c, p, extra));
            }
            // relax the hanging part so locks pushed behind the shoulders bend smoothly
            for (int it = 0; it < 2; it++)
                for (int k = skull + 1; k < pts.Count - 1; k++)
                    pts[k] = Vector3.Lerp(pts[k], (pts[k - 1] + pts[k + 1]) * 0.5f, 0.5f);
            return pts;
        }

        static Weigher HairWeight(HumanContext c)
        {
            float topY = c.HeadCenter.y;
            return p =>
            {
                var mix = new Bones.Mix();
                float tt = Smooth(topY - 0.12f, topY - 0.24f, p.y);
                mix.Add(Bones.Head, 1 - tt);
                mix.Add(Bones.Torso, tt);
                return mix.Weight();
            };
        }

        /// <summary>Long straight hair, a C-curl bob or a layered hush cut, made of locks around the back and sides.</summary>
        static void LongHair(HumanContext c, Paint paint)
        {
            var l = c.L;
            float endY = l.Hair switch { HairStyle.Bob => -0.135f, HairStyle.Layered => -0.29f, _ => -0.46f };
            float flare = l.Hair switch { HairStyle.Bob => 0.012f, HairStyle.Layered => 0.022f, _ => 0.014f };
            float curl = l.Hair switch { HairStyle.Bob => 0.02f, HairStyle.Layered => -0.012f, _ => 0f };
            var rng = new System.Random(l.Face * 31 + (int)l.Hair);
            var w = HairWeight(c);
            int count = c.High ? 30 : 12;
            float front = l.Hair == HairStyle.Bob ? 50f : 58f;
            for (int layer = 0; layer < 2; layer++)
            {
                int n = layer == 0 ? count : count / 2;
                for (int k = 0; k < n; k++)
                {
                    float lam = Mathf.Lerp(front, 360f - front, (k + (layer == 0 ? 0f : 0.5f)) / Mathf.Max(1, n - 1 + (layer == 0 ? 0 : 1)));
                    if (lam > 180f) lam -= 360f;
                    bool side = Mathf.Abs(lam) < 125f;
                    float lamRoot = side ? Mathf.Sign(lam) * (l.Bangs ? 22f : 7f) : lam;
                    float phiRoot = side ? (l.Bangs ? 66f : 72f) : 82f;
                    float exitPhi = side ? -4f : Mathf.Lerp(-4f, -26f, Smooth(125f, 180f, Mathf.Abs(lam)));
                    float jitter = (float)rng.NextDouble();
                    float t0 = 0.008f + layer * 0.0035f + (k % 2) * 0.0015f;
                    float t1 = 0.014f + layer * 0.004f + (k % 2) * 0.002f;
                    float len = endY * (layer == 0 ? Mathf.Lerp(0.95f, 1.04f, jitter) : Mathf.Lerp(0.82f, 0.92f, jitter));
                    var path = HangPath(c, lam, lamRoot, phiRoot, exitPhi, t0, t1, len, flare + layer * 0.004f, curl);
                    float width = (c.High ? 0.042f : 0.07f) * Scale(c);
                    Card(c, paint, path, width, width * 0.8f, w, k * 0.37f);
                }
            }
            // face-framing locks in front of the ears
            foreach (float sgn in new[] { -1f, 1f })
                for (int k = 0; k < (c.High ? 2 : 1); k++)
                {
                    float lam = sgn * (front - 6f - k * 6f);
                    var path = HangPath(c, lam, sgn * (l.Bangs ? 24f : 9f), l.Bangs ? 62f : 66f, 6f, 0.009f, 0.012f,
                        l.Hair == HairStyle.Bob ? -0.13f : l.Hair == HairStyle.Layered ? -0.2f : -0.32f, 0.006f, l.Hair == HairStyle.Bob ? 0.012f : 0f);
                    Card(c, paint, path, 0.03f * Scale(c), 0.022f * Scale(c), w, 0.2f + k);
                }
        }

        /// <summary>Bangs: full (a bob's straight fringe) or see-through (시스루뱅), locks lying on the forehead.</summary>
        static void Bangs(HumanContext c, Paint paint, bool full)
        {
            int n = full ? (c.High ? 12 : 6) : (c.High ? 9 : 5);
            float span = full ? 40f : 33f;
            var rng = new System.Random(c.L.Face * 7 + 3);
            var w = Bones.Fixed(Bones.Head);
            for (int k = 0; k < n; k++)
            {
                float lam = Mathf.Lerp(-span, span, (k + 0.5f) / n);
                float tip = (full ? 22f : 21f) + (float)rng.NextDouble() * 3f;
                var path = new List<Vector3>();
                int steps = c.High ? 7 : 4;
                for (int j = 0; j <= steps; j++)
                {
                    float s = j / (float)steps;
                    path.Add(Scalp(c, Mathf.Lerp(68f, tip, s), Mathf.Lerp(lam * 0.55f, lam, s), Mathf.Lerp(0.011f, 0.0062f, s)));
                }
                float width = (full ? 0.024f : 0.017f) * Scale(c);
                Card(c, paint, path, width, width * (full ? 0.9f : 0.6f), w, k * 0.31f);
            }
        }

        /// <summary>
        /// Men's fringes, the long top layer falling forward from the crown: a dandy two-block's bangs down to just
        /// above the brows, comma hair swept to one side with its tips curling in, or the curtain bangs of a middle
        /// part opening at the centre and falling to the cheekbones.
        /// </summary>
        static void Fringe(HumanContext c, Paint paint)
        {
            var l = c.L;
            bool comma = l.Hair == HairStyle.Comma, curtain = l.Hair == HairStyle.MiddlePart;
            var rng = new System.Random(l.Face * 13 + (int)l.Hair * 5);
            int n = c.High ? (curtain ? 22 : 20) : 9;
            float span = curtain ? 58f : 60f;
            var w = Bones.Fixed(Bones.Head);
            for (int k = 0; k < n; k++)
            {
                float lam = Mathf.Lerp(-span, span, (k + 0.5f) / n);
                float a = Mathf.Abs(lam), side = Mathf.Sign(lam);
                float jitter = (float)rng.NextDouble();
                float phiRoot = 70f - a * 0.12f + jitter * 4f;
                float tip = curtain ? Mathf.Lerp(28f, 5f, Smooth(3f, 56f, a)) + jitter * 4f
                    : comma ? (lam < 8f ? 20f : 28f) - Smooth(30f, 52f, a) * 6f + jitter * 3f
                    : 22f - Smooth(26f, 52f, a) * 8f + jitter * 4f;
                var path = new List<Vector3>();
                int steps = c.High ? 8 : 4;
                for (int j = 0; j <= steps; j++)
                {
                    float s = j / (float)steps;
                    float e = Mathf.SmoothStep(0f, 1f, s);
                    float lamS = curtain ? Mathf.Lerp(side * 3f, lam + side * 12f * s, e)
                        : comma ? Mathf.Lerp(lam * 0.75f - 10f, lam + 4f, e)
                        : Mathf.Lerp(lam * 0.75f, lam * (1.04f + 0.1f * Smooth(36f, 60f, a)), e);
                    float phi = Mathf.Lerp(phiRoot, tip, s);
                    // rooted in the hair, lifting off it over the hairline, lying on the forehead at the tip
                    float extra = Mathf.Lerp(0.002f, 0.0055f, s) + 0.007f * Mathf.Sin(Mathf.Min(1f, s * 1.6f) * Mathf.PI);
                    // comma hair: the swept locks curl in at their tips
                    if (comma && lam > 8f && s > 0.62f)
                    {
                        float curl = Smooth(0.62f, 1f, s);
                        lamS -= curl * 11f;
                        phi += curl * 7f;
                        extra += curl * 0.006f;
                    }
                    path.Add(Scalp(c, phi, lamS, ShellAt(c, phi, lamS) + extra));
                }
                float width = (c.High ? 0.024f : 0.036f) * Scale(c);
                Card(c, paint, path, width, width * 0.55f, w, k * 0.29f + jitter);
            }
        }

        /// <summary>
        /// The ends of a man's long top layer round the sides and back: locks hanging over the undercut of a
        /// two-block, or over the tops of the ears and the nape of a middle-part perm, so the edge of the hair is
        /// ragged and full instead of a helmet's rim.
        /// </summary>
        static void SideLocks(HumanContext c, Paint paint)
        {
            var l = c.L;
            bool perm = l.Hair == HairStyle.MiddlePart;
            var rng = new System.Random(l.Face * 31 + (int)l.Hair * 7);
            int n = c.High ? 22 : 9;
            var w = Bones.Fixed(Bones.Head);
            for (int k = 0; k < n; k++)
            {
                float lam = Mathf.Lerp(44f, 316f, (k + 0.5f) / n);
                if (lam > 180f) lam -= 360f;
                float a = Mathf.Abs(lam);
                float jitter = (float)rng.NextDouble();
                float end = (perm ? Mathf.Lerp(14f, -18f, Smooth(115f, 170f, a)) : BlockEdge(a) - 5f) - jitter * 4f;
                if (a > 72f && a < 124f) end = Mathf.Max(end, 15f); // above the ears
                float root = end + 28f;
                // close to the head at the temples, standing off a little more over the undercut behind
                float tRoot = ShellAt(c, root, lam) + 0.002f, tEnd = (perm ? 0.014f : 0.012f) * Mathf.Lerp(0.35f, 1f, Smooth(60f, 110f, a));
                var path = new List<Vector3>();
                int steps = c.High ? 5 : 3;
                for (int j = 0; j <= steps; j++)
                {
                    float s = j / (float)steps;
                    float phi = Mathf.Lerp(root, end, s);
                    // the lock lies on the hair, then hangs on over the edge instead of following it in
                    float t = Mathf.Max(ShellAt(c, phi, lam) + Mathf.Lerp(0.003f, 0.006f, s), Mathf.Lerp(tRoot, tEnd, s));
                    path.Add(Scalp(c, phi, lam + Mathf.Sign(lam) * 3f * s, t));
                }
                float width = (c.High ? 0.03f : 0.052f) * Scale(c) * (a < 80f ? 1.3f : 1f);
                Card(c, paint, path, width, width * 0.6f, w, k * 0.23f + jitter);
            }
        }

        /// <summary>A 2:8 side part: locks rising from the parting and sweeping over the top to the far side, and the
        /// short side combed down from it towards the temple.</summary>
        static void Sweep(HumanContext c, Paint paint)
        {
            var rng = new System.Random(c.L.Face * 17 + 11);
            int n = c.High ? 12 : 5;
            var w = Bones.Fixed(Bones.Head);
            int shortSide = c.High ? 6 : 3;
            for (int k = 0; k < shortSide; k++)
            {
                float u = (k + 0.5f) / shortSide;
                float phiRoot = Mathf.Lerp(50f, 74f, u);
                float jitter = (float)rng.NextDouble();
                var path = new List<Vector3>();
                int steps = c.High ? 5 : 3;
                for (int j = 0; j <= steps; j++)
                {
                    float s = j / (float)steps;
                    float lam = Mathf.Lerp(-34f, -Mathf.Lerp(70f, 100f, u) - jitter * 6f, s);
                    float phi = phiRoot - Mathf.Lerp(20f, 36f, u) * s;
                    path.Add(Scalp(c, phi, lam, ShellAt(c, phi, lam) + Mathf.Lerp(0.005f, 0.003f, s)));
                }
                float width = (c.High ? 0.03f : 0.05f) * Scale(c);
                Card(c, paint, path, width, width * 0.7f, w, k * 0.31f + jitter);
            }
            for (int k = 0; k < n; k++)
            {
                float u = (k + 0.5f) / n;
                float phiRoot = Mathf.Lerp(52f, 80f, u); // along the parting, from the forehead back to the crown
                float jitter = (float)rng.NextDouble();
                float lamEnd = Mathf.Lerp(70f, 96f, u) + jitter * 8f;
                var path = new List<Vector3>();
                int steps = c.High ? 7 : 4;
                for (int j = 0; j <= steps; j++)
                {
                    float s = j / (float)steps;
                    float lam = Mathf.Lerp(-30f, lamEnd, s);
                    float phi = phiRoot - Mathf.Lerp(14f, 40f, u) * s * s;
                    // lifted off the parting, lying on the side; the front locks arch a little higher
                    float t = ShellAt(c, phi, lam) + Mathf.Lerp(0.007f, 0.003f, s) + 0.012f * Mathf.Sin(s * Mathf.PI) * (1f - u) * (1f - u);
                    path.Add(Scalp(c, phi, lam, t));
                }
                float width = (c.High ? 0.03f : 0.05f) * Scale(c);
                Card(c, paint, path, width, width * 0.7f, w, k * 0.27f + jitter);
            }
            // younger men wear it with a side-swept fringe falling across the forehead from the parting
            if (c.L.Age < Age.Middle)
            {
                int bangs = c.High ? 7 : 3;
                for (int k = 0; k < bangs; k++)
                {
                    float u = (k + 0.5f) / bangs;
                    float jitter = (float)rng.NextDouble();
                    float lamRoot = -30f + u * 12f, phiRoot = Mathf.Lerp(56f, 68f, u);
                    float lamTip = Mathf.Lerp(24f, 64f, u) + jitter * 6f, phiTip = Mathf.Lerp(31f, 24f, u) + jitter * 3f;
                    var path = new List<Vector3>();
                    int steps = c.High ? 7 : 4;
                    for (int j = 0; j <= steps; j++)
                    {
                        float s = j / (float)steps;
                        float lam = Mathf.Lerp(lamRoot, lamTip, Mathf.SmoothStep(0f, 1f, s));
                        float phi = Mathf.Lerp(phiRoot, phiTip, s);
                        path.Add(Scalp(c, phi, lam, ShellAt(c, phi, lam) + 0.0055f + 0.007f * Mathf.Sin(s * Mathf.PI)));
                    }
                    float width = (c.High ? 0.026f : 0.04f) * Scale(c);
                    Card(c, paint, path, width, width * 0.55f, w, k * 0.33f + jitter);
                }
            }
        }

        static void Ponytail(HumanContext c, Paint paint)
        {
            var path = new[]
            {
                new Vector3(0, 0.055f, -0.098f), new Vector3(0, 0.03f, -0.126f), new Vector3(0, -0.04f, -0.136f),
                new Vector3(0, -0.12f, -0.122f), new Vector3(0, -0.2f, -0.108f),
            };
            var root = new List<Vector3>();
            foreach (var p in path) root.Add(ToRoot(c, p));
            float[] r = { 0.017f, 0.025f, 0.024f, 0.018f, 0.006f };
            float topY = c.HeadCenter.y;
            float Radius(float t)
            {
                int i0 = Mathf.Min(3, Mathf.FloorToInt(t * 4f));
                return Mathf.Lerp(r[i0], r[i0 + 1], t * 4f - i0) * Scale(c);
            }
            Tube(c.Mb, root, Radius,
                c.High ? 12 : 6, paint, p =>
                {
                    var mix = new Bones.Mix();
                    float tt = Smooth(topY - 0.06f, topY - 0.2f, p.y);
                    mix.Add(Bones.Head, 1 - tt * 0.6f);
                    mix.Add(Bones.Torso, tt * 0.6f);
                    return mix.Weight();
                });
            HairTie(c, new Vector3(0, 0.05f, -0.104f), 0.016f);
        }

        static void Bun(HumanContext c, Paint paint)
        {
            var center = ToRoot(c, new Vector3(0, -0.048f, -0.108f));
            Ellipsoid(c.Mb, center, new Vector3(0.042f, 0.035f, 0.03f) * Scale(c), Quaternion.Euler(-15, 0, 0), c.High ? 16 : 8, c.High ? 10 : 5, paint, Bones.Fixed(Bones.Head),
                p => p * (1f + 0.06f * Mathf.Sin(Mathf.Atan2(p.x, p.y) * 5f)));
            HairTie(c, new Vector3(0, -0.035f, -0.096f), 0.02f);
        }

        static void HairTie(HumanContext c, Vector3 at, float r)
        {
            var center = ToRoot(c, at);
            var loop = new List<Vector3>();
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                loop.Add(center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.9f, -0.002f) * Scale(c));
            }
            Tube(c.Mb, loop, _ => 0.0038f, 6, Paint.Cloth(Fabric.Satin, new Color(0.12f, 0.11f, 0.11f)), Bones.Fixed(Bones.Head), true);
        }

        // ------------------------------------------------------------------ hats

        /// <summary>A shell over the skull from a band line up to the crown, lifted by <paramref name="lift"/>.</summary>
        static void Crown(HumanContext c, Paint paint, System.Func<float, float> band, float lift, float topExtra, bool closedTop = true)
        {
            int cols = c.High ? 40 : 18, rows = c.High ? 9 : 5;
            var pos = new Vector3[cols + 1, rows + 1];
            var outward = new Vector3[cols + 1, rows + 1];
            var uvs = new Vector2[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
            {
                float lam = Mathf.Lerp(-180f, 180f, i / (float)cols);
                float b = band(lam);
                for (int j = 0; j <= rows; j++)
                {
                    float phi = Mathf.Lerp(b, closedTop ? 90f : b + 9f, j / (float)rows);
                    var f = Surface(phi, lam, false) + SkullNormal(phi, lam) * (lift + topExtra * 0.5f * Smooth(15f, 75f, phi))
                        + Vector3.up * topExtra * 0.5f * Smooth(40f, 90f, phi);
                    pos[i, j] = ToRoot(c, f);
                    outward[i, j] = f;
                    uvs[i, j] = new Vector2((lam + 180f) / 360f * 2f * Mathf.PI * 0.1f / paint.Metres, (phi - b) * Mathf.Deg2Rad * 0.11f / paint.Metres);
                }
            }
            Grid(c.Mb, pos, uvs, outward, paint, Bones.Fixed(Bones.Head), true, false, null, null, !closedTop, paint.Color * 0.5f);
        }

        /// <summary>A brim sticking out from the band over a longitude range, sloping down.</summary>
        static void Brim(HumanContext c, Paint paint, float lamFrom, float lamTo, System.Func<float, float> band, float lift, float length, float droop, float curve = 0f)
        {
            int cols = c.High ? 24 : 10, rows = 3;
            var pos = new Vector3[cols + 1, rows + 1];
            var outward = new Vector3[cols + 1, rows + 1];
            var uvs = new Vector2[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
            {
                float t = i / (float)cols;
                float lam = Mathf.Lerp(lamFrom, lamTo, t);
                float b = band(lam);
                var basePt = Surface(b, lam, false) + SkullNormal(b, lam) * lift;
                var outDir = new Vector3(basePt.x, 0, basePt.z).normalized;
                // the brim is shorter at its sides when it only covers the front
                float len = length * (lamTo - lamFrom < 300f ? Mathf.Lerp(0.35f, 1f, Mathf.Sin(t * Mathf.PI)) : 1f);
                for (int j = 0; j <= rows; j++)
                {
                    float d = len * j / rows;
                    var f = basePt + outDir * d + Vector3.down * (d * droop + curve * d * d / Mathf.Max(0.01f, length) * Mathf.Abs(t - 0.5f) * 2f);
                    pos[i, j] = ToRoot(c, f);
                    outward[i, j] = Vector3.up;
                    uvs[i, j] = new Vector2(t * 4f, d / paint.Metres);
                }
            }
            Grid(c.Mb, pos, uvs, outward, paint, Bones.Fixed(Bones.Head), false, false, null, null, true, paint.Color * 0.8f);
        }

        static void Hat(HumanContext c)
        {
            var l = c.L;
            if (l.Hat == HatKind.None) return;
            var canvas = Paint.Cloth(Fabric.Canvas, l.HatColor);
            switch (l.Hat)
            {
                case HatKind.Cap:
                {
                    float Band(float lam) => Mathf.Lerp(25f, -6f, Smooth(20f, 160f, Mathf.Abs(lam)));
                    Crown(c, canvas, Band, 0.012f, 0.01f);
                    Brim(c, canvas, -60f, 60f, Band, 0.012f, 0.075f, 0.18f, 0.02f);
                    Ellipsoid(c.Mb, ToRoot(c, new Vector3(0, FaceMap.Ry + 0.02f, -0.004f)), new Vector3(0.006f, 0.004f, 0.006f), Quaternion.identity, 6, 4, canvas, Bones.Fixed(Bones.Head));
                    break;
                }
                case HatKind.BucketHat:
                {
                    float Band(float lam) => Mathf.Lerp(22f, 0f, Smooth(40f, 170f, Mathf.Abs(lam)));
                    Crown(c, canvas, Band, 0.014f, 0.012f);
                    Brim(c, canvas, -180f, 180f, Band, 0.014f, 0.05f, 0.38f);
                    break;
                }
                case HatKind.Beanie:
                {
                    var knit = Paint.Cloth(Fabric.Knit, l.HatColor);
                    float Band(float lam) => Mathf.Lerp(16f, -6f, Smooth(30f, 160f, Mathf.Abs(lam)));
                    Crown(c, knit, Band, 0.011f, 0.022f);
                    Crown(c, knit.With(l.HatColor * 0.95f), Band, 0.017f, 0f, false);
                    break;
                }
                case HatKind.Visor:
                {
                    var plastic = Paint.Cloth(Fabric.Satin, l.HatColor);
                    float Band(float lam) => 28f - 8f * Smooth(30f, 120f, Mathf.Abs(lam));
                    float hairT = 0.013f;
                    Crown(c, Paint.Cloth(Fabric.Canvas, l.HatColor), Band, hairT + 0.004f, 0f, false);
                    Brim(c, plastic, -72f, 72f, Band, hairT + 0.004f, 0.09f, 0.32f, 0.02f);
                    break;
                }
                case HatKind.GuardCap:
                {
                    // a peaked uniform cap: a band round the forehead (higher in front, low at the back), a crown
                    // that flares out to a flat top larger than the head and raised at the front, a black visor
                    float Band(float lam) => Mathf.Lerp(27f, 2f, Smooth(40f, 170f, Mathf.Abs(lam)));
                    int cols = c.High ? 40 : 18;
                    var pos = new Vector3[cols + 1, 5];
                    var outward = new Vector3[cols + 1, 5];
                    var uvs = new Vector2[cols + 1, 5];
                    for (int i = 0; i <= cols; i++)
                    {
                        float lam = Mathf.Lerp(-180f, 180f, i / (float)cols);
                        var band = Surface(Band(lam), lam, false) + SkullNormal(Band(lam), lam) * 0.012f;
                        var dir = new Vector3(band.x, 0, band.z).normalized;
                        float front = Mathf.Max(0f, Mathf.Cos(lam * Mathf.Deg2Rad));
                        var bandTop = band + Vector3.up * 0.034f;
                        var edge = new Vector3(dir.x * 0.106f, FaceMap.Ry + 0.008f + 0.026f * front, dir.z * 0.122f + 0.012f);
                        var f = new[] { band, bandTop, Vector3.Lerp(bandTop, edge, 0.55f) + Vector3.up * 0.01f, edge, new Vector3(0, FaceMap.Ry + 0.014f, 0.012f) };
                        for (int j = 0; j < 5; j++)
                        {
                            pos[i, j] = ToRoot(c, f[j]);
                            outward[i, j] = j == 4 ? Vector3.up : j == 3 ? dir + Vector3.up * 0.6f : dir;
                            uvs[i, j] = new Vector2((lam + 180f) / 360f * 0.7f / canvas.Metres, j * 0.03f / canvas.Metres);
                        }
                    }
                    Grid(c.Mb, pos, uvs, outward, canvas, Bones.Fixed(Bones.Head), true, false, null, null, true, l.HatColor * 0.5f);
                    // a darker band of braid round the bottom
                    Crown(c, canvas.With(l.HatColor * 0.6f), Band, 0.0135f, 0f, false);
                    var leather = Paint.Cloth(Fabric.Leather, new Color(0.05f, 0.05f, 0.06f));
                    Brim(c, leather, -58f, 58f, Band, 0.013f, 0.062f, 0.4f);
                    var badgeAt = Surface(Band(0), 0, false) + SkullNormal(Band(0), 0) * 0.012f + new Vector3(0, 0.052f, 0.012f);
                    Ellipsoid(c.Mb, ToRoot(c, badgeAt), new Vector3(0.013f, 0.015f, 0.004f), Quaternion.Euler(-20f, 0, 0), 8, 5,
                        Paint.Cloth(Fabric.Satin, new Color(0.82f, 0.68f, 0.3f)), Bones.Fixed(Bones.Head));
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ mask, glasses, earphones

        static void Mask(HumanContext c)
        {
            var l = c.L;
            var paint = Paint.Cloth(Fabric.Canvas, l.MaskColor);
            int cols = c.High ? 18 : 10, rows = c.High ? 10 : 6;
            var pos = new Vector3[cols + 1, rows + 1];
            var outward = new Vector3[cols + 1, rows + 1];
            var uvs = new Vector2[cols + 1, rows + 1];
            float phiTop = Mathf.Asin(-0.004f / FaceMap.Ry) * Mathf.Rad2Deg;
            for (int i = 0; i <= cols; i++)
            {
                float lam = Mathf.Lerp(-80f, 80f, i / (float)cols);
                for (int j = 0; j <= rows; j++)
                {
                    float phi = Mathf.Lerp(phiTop, -76f, j / (float)rows);
                    var s = Surface(phi, lam, false);
                    float front = Smooth(0.2f, 0.8f, Dir(phi, lam).z);
                    float lift = 0.006f + 0.013f * G(s.x, s.y + 0.05f, 0.035f, 0.04f) * front + 0.018f * G(s.x, s.y + 0.02f, 0.012f, 0.02f) * front
                                 + 0.004f * G(s.x, 0, 0.004f, 1) * front;
                    var f = s + SkullNormal(phi, lam) * lift;
                    pos[i, j] = ToRoot(c, f);
                    outward[i, j] = f;
                    uvs[i, j] = new Vector2(lam / 360f * 3f, j / (float)rows * 1.5f);
                }
            }
            Grid(c.Mb, pos, uvs, outward, paint, Bones.Fixed(Bones.Head), false, false, null, null, true);
            // ear loops
            foreach (float s in new[] { -1f, 1f })
            {
                var pts = new[]
                {
                    Surface(phiTop - 2f, s * 80f, false) + SkullNormal(0, s * 80f) * 0.006f,
                    Surface(6f, s * 100f, false) + SkullNormal(6f, s * 100f) * 0.008f,
                    Surface(-12f, s * 102f, false) + SkullNormal(-12f, s * 102f) * 0.008f,
                    Surface(-40f, s * 80f, false) + SkullNormal(-40f, s * 80f) * 0.006f,
                };
                var root = new List<Vector3>();
                foreach (var p in pts) root.Add(ToRoot(c, p));
                Tube(c.Mb, root, _ => 0.0013f, 4, paint, Bones.Fixed(Bones.Head));
            }
        }

        static void Glasses(HumanContext c)
        {
            var l = c.L;
            bool round = l.Glasses == GlassesKind.Round;
            var frame = Paint.Cloth(round ? Fabric.Satin : Fabric.Leather, l.FrameColor);
            float r = round ? 0.0016f : 0.0024f;
            float zEye = Surface(Mathf.Asin(FaceMap.EyeY / FaceMap.Ry) * Mathf.Rad2Deg, Mathf.Asin(FaceMap.EyeX / FaceMap.Rx) * Mathf.Rad2Deg, true).z;
            float z0 = zEye + 0.017f;
            float hw = round ? 0.0195f : 0.0235f, hh = round ? 0.0195f : 0.0165f;
            Weigher head = Bones.Fixed(Bones.Head);
            foreach (float s in new[] { -1f, 1f })
            {
                var center = new Vector3(s * FaceMap.EyeX, FaceMap.EyeY + 0.001f, z0);
                var loop = new List<Vector3>();
                int n = c.High ? 24 : 12;
                for (int k = 0; k < n; k++)
                {
                    float a = k / (float)n * Mathf.PI * 2f;
                    Vector2 p;
                    if (round) p = new Vector2(Mathf.Cos(a) * hw, Mathf.Sin(a) * hh);
                    else
                    {
                        // a rounded rectangle via a superellipse
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        p = new Vector2(Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 0.45f) * hw, Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), 0.45f) * hh);
                    }
                    loop.Add(ToRoot(c, center + new Vector3(p.x, p.y, -0.0015f * (p.x * s) / hw - p.y * 0.08f)));
                }
                Tube(c.Mb, loop, _ => r, 5, frame, head, true);

                // lens
                int lr = 3, lc = n;
                var pos = new Vector3[lc + 1, lr + 1];
                var outward = new Vector3[lc + 1, lr + 1];
                var uvs = new Vector2[lc + 1, lr + 1];
                for (int i = 0; i <= lc; i++)
                for (int j = 0; j <= lr; j++)
                {
                    var edge = loop[i % lc];
                    var mid = ToRoot(c, center + new Vector3(0, 0, 0.0015f));
                    var p = Vector3.Lerp(mid, edge, j / (float)lr);
                    pos[i, j] = p;
                    outward[i, j] = Vector3.forward;
                    uvs[i, j] = Vector2.zero;
                }
                Grid(c.Mb, pos, uvs, outward, Paint.Glass, head, true);

                // temple arm back to the ear
                var temple = new[]
                {
                    center + new Vector3(s * hw, 0.003f, -0.002f),
                    new Vector3(s * (FaceMap.Rx + 0.005f), FaceMap.EyeY + 0.003f, 0.02f),
                    new Vector3(s * (FaceMap.Rx + 0.004f), FaceMap.EyeY - 0.002f, -0.03f),
                    new Vector3(s * (FaceMap.Rx - 0.004f), FaceMap.EyeY - 0.022f, -0.044f),
                };
                var tr = new List<Vector3>();
                foreach (var p in temple) tr.Add(ToRoot(c, p));
                Tube(c.Mb, tr, _ => r * 0.9f, 4, frame, head);
            }
            var bridge = new[]
            {
                new Vector3(-(FaceMap.EyeX - hw * 0.95f), FaceMap.EyeY + 0.006f, z0),
                new Vector3(0, FaceMap.EyeY + 0.008f, z0 + 0.002f),
                new Vector3(FaceMap.EyeX - hw * 0.95f, FaceMap.EyeY + 0.006f, z0),
            };
            var br = new List<Vector3>();
            foreach (var p in bridge) br.Add(ToRoot(c, p));
            Tube(c.Mb, br, _ => r * 0.9f, 4, frame, head);
        }

        static void Earphones(HumanContext c)
        {
            var white = Paint.Cloth(Fabric.Satin, new Color(0.96f, 0.96f, 0.95f));
            foreach (float s in new[] { -1f, 1f })
            {
                var at = new Vector3(s * (FaceMap.Rx + 0.002f), FaceMap.EyeY - 0.014f, 0.002f);
                Ellipsoid(c.Mb, ToRoot(c, at), new Vector3(0.0055f, 0.006f, 0.0055f), Quaternion.identity, 8, 5, white, Bones.Fixed(Bones.Head));
                var stem = new List<Vector3> { ToRoot(c, at + new Vector3(0, -0.004f, 0.004f)), ToRoot(c, at + new Vector3(0, -0.02f, 0.006f)) };
                Tube(c.Mb, stem, _ => 0.0022f, 5, white, Bones.Fixed(Bones.Head));
            }
        }

        // ------------------------------------------------------------------ rigid face parts (mouth, eyelids)

        /// <summary>The mouth opening, a dark ellipse with a hint of teeth, scaled open while talking. Pivot at its centre.</summary>
        public static Mesh MouthMesh(HumanContext c, out Vector3 pivot)
        {
            float sc = Scale(c);
            float y0 = FaceMap.MouthY + 0.0003f;
            float zLip = Surface(Mathf.Asin(y0 / FaceMap.Ry) * Mathf.Rad2Deg, 0, true).z;
            var centerFace = new Vector3(0, y0, zLip + 0.0014f);
            pivot = ToRoot(c, centerFace);
            var mb = new MeshBuilder();
            int cols = 12, rows = 6;
            float hw = FaceMap.MouthHalfW * 0.82f, hh = 0.0065f;
            var pos = new Vector3[cols + 1, rows + 1];
            var outward = new Vector3[cols + 1, rows + 1];
            var uvs = new Vector2[cols + 1, rows + 1];
            var colors = new Color[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
            for (int j = 0; j <= rows; j++)
            {
                float tx = Mathf.Lerp(-1f, 1f, i / (float)cols), ty = Mathf.Lerp(1f, -1f, j / (float)rows);
                float w = Mathf.Sqrt(Mathf.Max(0, 1 - ty * ty));
                var p = new Vector3(tx * hw * w, ty * hh, -0.5f * (tx * hw) * (tx * hw) / 0.05f);
                pos[i, j] = p * sc;
                outward[i, j] = Vector3.forward;
                uvs[i, j] = new Vector2(tx, ty);
                colors[i, j] = ty > 0.45f ? new Color(0.86f, 0.84f, 0.8f) : new Color(0.16f, 0.06f, 0.06f);
            }
            Grid(mb, pos, uvs, outward, Paint.Cloth(Fabric.Satin, Color.white), _ => default, false, false, colors);
            return mb.ToRigidMesh("Mouth");
        }

        /// <summary>An eyelid that slides down over the painted eye when blinking. Pivot at the upper lid line.</summary>
        public static Mesh LidMesh(HumanContext c, float side, out Vector3 pivot)
        {
            float sc = Scale(c);
            float ex = side * FaceMap.EyeX, ey = FaceMap.EyeY;
            float top = ey + 0.0064f;
            pivot = ToRoot(c, new Vector3(ex, top, 0) + Vector3.forward * (Surface(Mathf.Asin(ey / FaceMap.Ry) * Mathf.Rad2Deg, Mathf.Asin(ex / FaceMap.Rx) * Mathf.Rad2Deg, true).z + 0.0012f));
            var mb = new MeshBuilder();
            int cols = 10, rows = 4;
            float hw = FaceMap.EyeHalfW * 1.12f;
            var pos = new Vector3[cols + 1, rows + 1];
            var outward = new Vector3[cols + 1, rows + 1];
            var uvs = new Vector2[cols + 1, rows + 1];
            var colors = new Color[cols + 1, rows + 1];
            float zRef = Surface(Mathf.Asin(ey / FaceMap.Ry) * Mathf.Rad2Deg, Mathf.Asin(ex / FaceMap.Rx) * Mathf.Rad2Deg, true).z;
            for (int i = 0; i <= cols; i++)
            for (int j = 0; j <= rows; j++)
            {
                float t = Mathf.Lerp(-1f, 1f, i / (float)cols);
                float x = ex + side * t * hw;
                float upper = ey + 0.0064f * Mathf.Pow(Mathf.Max(0, 1 - t * t), 0.75f) + 0.06f * t * FaceMap.EyeHalfW;
                float lower = ey - 0.0042f * Mathf.Pow(Mathf.Max(0, 1 - t * t), 0.9f) + 0.033f * t * FaceMap.EyeHalfW;
                float y = Mathf.Lerp(upper + 0.0006f, lower - 0.0008f, j / (float)rows);
                float z = Surface(Mathf.Asin(Mathf.Clamp(y / FaceMap.Ry, -1, 1)) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(x / FaceMap.Rx, -1, 1)) * Mathf.Rad2Deg, true).z + 0.0012f;
                var local = new Vector3(x - ex, y - top, z - zRef - 0.0012f);
                pos[i, j] = local * sc;
                outward[i, j] = Vector3.forward;
                uvs[i, j] = new Vector2(t, j / (float)rows);
                colors[i, j] = j == rows ? new Color(0.08f, 0.06f, 0.06f) : c.L.Skin * 0.93f;
            }
            Grid(mb, pos, uvs, outward, Paint.SkinOf(c.L.Skin), _ => default, false, false, colors);
            return mb.ToRigidMesh("Eyelid");
        }
    }
}
