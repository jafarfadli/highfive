using System.Collections.Generic;
using UnityEngine;
using static SeoulFirstWeek.World.HumanMesh;

namespace SeoulFirstWeek.World
{
    /// <summary>Everything one build of a person needs: the look, the mesh being filled, detail and joint layout.</summary>
    public class HumanContext
    {
        public HumanLook L;
        public MeshBuilder Mb;
        public bool High;
        public float ShoulderX;
        public const float HipX = 0.095f;

        public int SegBody => High ? 28 : 14;
        public int SegLimb => High ? 16 : 9;
        public int SegSmall => High ? 8 : 6;

        public Vector3 HeadCenter => new(0, HumanRig.NeckY + FaceMap.CenterUp, FaceMap.CenterForward);

        /// <summary>Neck radii (side to side, front to back), shared by the neck and everything worn around it.</summary>
        public float NeckRx => (L.Female ? 0.055f : 0.062f) * Mathf.Lerp(0.95f, 1.08f, L.Build);
        public float NeckRz => (L.Female ? 0.052f : 0.059f) * Mathf.Lerp(0.95f, 1.08f, L.Build);

        /// <summary>Body (trunk) profile shared by every garment worn on it.</summary>
        public Profile Torso, Pelvis, Leg, Arm;

        public bool OuterHasSleeves => L.Outer is not (OuterKind.None or OuterKind.PaddedVest or OuterKind.UniformVest or OuterKind.Apron);
    }

    /// <summary>A smooth (Catmull-Rom) profile of elliptical cross-sections along y.</summary>
    public class Profile
    {
        readonly float[] y, rx, rz, f, b;

        public Profile(params float[] keys)
        {
            int n = keys.Length / 5;
            y = new float[n];
            rx = new float[n];
            rz = new float[n];
            f = new float[n];
            b = new float[n];
            for (int i = 0; i < n; i++)
            {
                y[i] = keys[i * 5];
                rx[i] = keys[i * 5 + 1];
                rz[i] = keys[i * 5 + 2];
                f[i] = keys[i * 5 + 3];
                b[i] = keys[i * 5 + 4];
            }
        }

        public Profile Scale(float sx, float sz)
        {
            var keys = new float[y.Length * 5];
            for (int i = 0; i < y.Length; i++)
            {
                keys[i * 5] = y[i];
                keys[i * 5 + 1] = rx[i] * sx;
                keys[i * 5 + 2] = rz[i] * sz;
                keys[i * 5 + 3] = f[i];
                keys[i * 5 + 4] = b[i];
            }
            return new Profile(keys);
        }

        public float Bottom => y[0];
        public float Top => y[^1];

        static float Cr(float[] a, int i, float t)
        {
            int n = a.Length;
            float p0 = a[Mathf.Clamp(i - 1, 0, n - 1)], p1 = a[i], p2 = a[Mathf.Clamp(i + 1, 0, n - 1)], p3 = a[Mathf.Clamp(i + 2, 0, n - 1)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
        }

        public Ring At(float yy, float grow = 0, float cx = 0, float cz = 0)
        {
            yy = Mathf.Clamp(yy, y[0], y[^1]);
            int i = 0;
            while (i < y.Length - 2 && yy > y[i + 1]) i++;
            float t = Mathf.InverseLerp(y[i], y[i + 1], yy);
            return new Ring(yy, Mathf.Max(0.001f, Cr(rx, i, t)) + grow, Mathf.Max(0.001f, Cr(rz, i, t)) + grow, cx, cz, Mathf.Max(0, Cr(f, i, t)), Mathf.Max(0, Cr(b, i, t)));
        }
    }

    /// <summary>Body, garments, shoes, hands and the things people carry or wear on the body.</summary>
    public static class HumanBody
    {
        // ------------------------------------------------------------------ proportions

        static readonly Profile TorsoM = new(
            0.98f, 0.15f, 0.1f, 0f, 0.05f,
            1.06f, 0.146f, 0.098f, 0f, 0.04f,
            1.16f, 0.15f, 0.102f, 0f, 0.02f,
            1.26f, 0.161f, 0.107f, 0.02f, 0f,
            1.34f, 0.172f, 0.11f, 0.05f, 0f,
            1.4f, 0.188f, 0.104f, 0.03f, 0f,
            1.442f, 0.214f, 0.092f, 0f, 0f,
            1.47f, 0.2f, 0.085f, 0f, 0f,
            1.5f, 0.14f, 0.074f, 0f, 0f,
            1.535f, 0.068f, 0.058f, 0f, 0f);

        static readonly Profile TorsoF = new(
            0.98f, 0.162f, 0.104f, 0f, 0.08f,
            1.06f, 0.136f, 0.092f, 0f, 0.04f,
            1.16f, 0.137f, 0.094f, 0f, 0f,
            1.25f, 0.146f, 0.103f, 0.1f, 0f,
            1.32f, 0.15f, 0.108f, 0.13f, 0f,
            1.385f, 0.162f, 0.098f, 0.04f, 0f,
            1.435f, 0.19f, 0.086f, 0f, 0f,
            1.465f, 0.176f, 0.08f, 0f, 0f,
            1.495f, 0.125f, 0.068f, 0f, 0f,
            1.53f, 0.06f, 0.053f, 0f, 0f);

        static readonly Profile PelvisM = new(
            0.84f, 0.14f, 0.088f, 0f, 0f,
            0.9f, 0.172f, 0.1f, 0f, 0.06f,
            0.97f, 0.17f, 0.103f, 0f, 0.06f,
            1.05f, 0.148f, 0.098f, 0f, 0.03f);

        static readonly Profile PelvisF = new(
            0.84f, 0.15f, 0.092f, 0f, 0f,
            0.9f, 0.182f, 0.105f, 0f, 0.1f,
            0.97f, 0.176f, 0.105f, 0f, 0.08f,
            1.05f, 0.138f, 0.093f, 0f, 0.03f);

        static readonly Profile LegM = new(
            0.04f, 0.034f, 0.036f, 0f, 0f,
            0.1f, 0.036f, 0.038f, 0f, 0f,
            0.22f, 0.046f, 0.05f, 0f, 0.04f,
            0.36f, 0.057f, 0.062f, 0f, 0.08f,
            0.46f, 0.056f, 0.058f, 0f, 0.02f,
            0.55f, 0.056f, 0.058f, 0f, 0f,
            0.66f, 0.064f, 0.068f, 0f, 0f,
            0.8f, 0.078f, 0.082f, 0f, 0f,
            0.92f, 0.088f, 0.092f, 0f, 0.04f,
            1f, 0.09f, 0.094f, 0f, 0f);

        static readonly Profile LegF = new(
            0.04f, 0.031f, 0.033f, 0f, 0f,
            0.1f, 0.033f, 0.035f, 0f, 0f,
            0.22f, 0.043f, 0.047f, 0f, 0.04f,
            0.36f, 0.053f, 0.058f, 0f, 0.08f,
            0.46f, 0.051f, 0.054f, 0f, 0.02f,
            0.55f, 0.052f, 0.055f, 0f, 0f,
            0.66f, 0.062f, 0.066f, 0f, 0f,
            0.8f, 0.078f, 0.082f, 0f, 0f,
            0.92f, 0.09f, 0.094f, 0f, 0.05f,
            1f, 0.092f, 0.096f, 0f, 0f);

        static readonly Profile ArmM = new(
            0.915f, 0.028f, 0.032f, 0f, 0f,
            1f, 0.034f, 0.038f, 0f, 0f,
            1.1f, 0.041f, 0.044f, 0f, 0f,
            1.18f, 0.042f, 0.043f, 0f, 0f,
            1.26f, 0.046f, 0.047f, 0f, 0f,
            1.36f, 0.051f, 0.052f, 0f, 0f,
            1.44f, 0.056f, 0.056f, 0f, 0f,
            1.49f, 0.05f, 0.05f, 0f, 0f);

        public static void Prepare(HumanContext c)
        {
            var l = c.L;
            float bx = Mathf.Lerp(0.92f, 1.14f, l.Build), bz = Mathf.Lerp(0.9f, 1.22f, l.Build);
            c.Torso = (l.Female ? TorsoF : TorsoM).Scale(bx, bz);
            c.Pelvis = (l.Female ? PelvisF : PelvisM).Scale(bx, Mathf.Lerp(0.95f, 1.12f, l.Build));
            c.Leg = (l.Female ? LegF : LegM).Scale(Mathf.Lerp(0.94f, 1.1f, l.Build), Mathf.Lerp(0.94f, 1.1f, l.Build));
            c.Arm = ArmM.Scale(l.Female ? 0.86f : Mathf.Lerp(0.95f, 1.08f, l.Build), l.Female ? 0.86f : Mathf.Lerp(0.95f, 1.08f, l.Build));
            c.ShoulderX = l.Female ? 0.21f : Mathf.Lerp(0.226f, 0.24f, l.Build);
        }

        /// <summary>The torso cross-section at y, widened over the hips below the waist (and over the belly for heavier builds).</summary>
        static Ring TrunkAt(HumanContext c, float y, float grow)
        {
            var r = c.Torso.At(Mathf.Max(y, c.Torso.Bottom), grow);
            if (y < 1.0f)
            {
                var p = c.Pelvis.At(y, grow);
                r.Rx = Mathf.Max(r.Rx, p.Rx);
                r.Rz = Mathf.Max(r.Rz, p.Rz);
                r.Back = Mathf.Max(r.Back, p.Back);
                r.Y = y;
            }
            float belly = (c.L.Build - 0.5f) * 0.3f * Mathf.Exp(-Mathf.Pow((y - 1.1f) / 0.08f, 2));
            if (belly > 0) r.Front += belly;
            return r;
        }

        // ------------------------------------------------------------------ skinning weights (rest pose, root space)

        public static BoneWeight Trunk(Vector3 p, HumanContext c)
        {
            var mix = new Bones.Mix();
            float tTorso = Smooth(1.0f, 1.12f, p.y);
            float side = Smooth(-0.04f, 0.04f, p.x); // 0 = left (-x), 1 = right
            float shoulder = Smooth(0.14f, 0.22f, Mathf.Abs(p.x)) * Smooth(1.36f, 1.46f, p.y) * 0.45f;
            int arm = p.x < 0 ? Bones.ArmL : Bones.ArmR;
            mix.Add(Bones.Torso, tTorso * (1 - shoulder));
            mix.Add(arm, tTorso * shoulder);
            float tLeg = Smooth(0.98f, 0.84f, p.y);
            float tKnee = Smooth(0.6f, 0.48f, p.y);
            float rest = 1 - tTorso;
            mix.Add(Bones.Body, rest * (1 - tLeg));
            mix.Add(Bones.LegL, rest * tLeg * (1 - side) * (1 - tKnee));
            mix.Add(Bones.KneeL, rest * tLeg * (1 - side) * tKnee);
            mix.Add(Bones.LegR, rest * tLeg * side * (1 - tKnee));
            mix.Add(Bones.KneeR, rest * tLeg * side * tKnee);
            return mix.Weight();
        }

        public static BoneWeight Leg(Vector3 p, bool left)
        {
            var mix = new Bones.Mix();
            float tLeg = Smooth(1.0f, 0.88f, p.y);
            float tKnee = Smooth(0.58f, 0.49f, p.y);
            mix.Add(Bones.Body, 1 - tLeg);
            mix.Add(left ? Bones.LegL : Bones.LegR, tLeg * (1 - tKnee));
            mix.Add(left ? Bones.KneeL : Bones.KneeR, tLeg * tKnee);
            return mix.Weight();
        }

        public static BoneWeight Arm(Vector3 p, bool left)
        {
            var mix = new Bones.Mix();
            float tArm = Smooth(1.5f, 1.4f, p.y);
            float tElbow = Smooth(1.22f, 1.14f, p.y);
            mix.Add(Bones.Torso, 1 - tArm);
            mix.Add(left ? Bones.ArmL : Bones.ArmR, tArm * (1 - tElbow));
            mix.Add(left ? Bones.ElbowL : Bones.ElbowR, tArm * tElbow);
            return mix.Weight();
        }

        public static BoneWeight Neck(Vector3 p)
        {
            var mix = new Bones.Mix();
            float t = Smooth(1.56f, 1.64f, p.y);
            mix.Add(Bones.Torso, 1 - t);
            mix.Add(Bones.Head, t);
            return mix.Weight();
        }

        // ------------------------------------------------------------------ the whole body

        public static void Build(HumanContext c)
        {
            Prepare(c);
            Trousers(c);
            Top(c);
            OuterGarment(c);
            Arms(c);
            Shoes(c);
            Neckwear(c);
            Carry(c);
        }

        static float TopThickness(TopKind k) => k switch
        {
            TopKind.Tee => 0.004f, TopKind.LongTee => 0.005f, TopKind.Shirt => 0.005f, TopKind.Blouse => 0.006f,
            TopKind.Hoodie => 0.016f, TopKind.Sweatshirt => 0.013f, TopKind.Knit => 0.012f, TopKind.Turtleneck => 0.011f,
            TopKind.Polo => 0.005f, _ => 0.008f,
        };

        static float OuterThickness(OuterKind k) => k switch
        {
            OuterKind.Cardigan => 0.012f, OuterKind.Blazer => 0.016f, OuterKind.Trench => 0.02f, OuterKind.WoolCoat => 0.024f,
            OuterKind.Puffer => 0.045f, OuterKind.DenimJacket => 0.016f, OuterKind.LeatherJacket => 0.014f, OuterKind.Varsity => 0.02f,
            OuterKind.Windbreaker => 0.016f, OuterKind.PaddedVest => 0.034f, OuterKind.Fleece => 0.022f, OuterKind.LabCoat => 0.014f,
            OuterKind.UniformVest => 0.008f, OuterKind.Apron => 0.01f, _ => 0.02f,
        };

        static bool Tucked(HumanLook l) =>
            l.Top is TopKind.Shirt or TopKind.Blouse && l.Bottom is not (BottomKind.LongSkirt or BottomKind.PleatedSkirt) || l.Top == TopKind.Shirt;

        static bool Skirt(HumanLook l) => l.Bottom is BottomKind.LongSkirt or BottomKind.PleatedSkirt;

        // ------------------------------------------------------------------ trousers, skirts, legs

        /// <summary>Hem height, minimum leg radius and ease (looseness) of each cut of trousers.</summary>
        static (float hem, float minR, float ease) TrouserCut(BottomKind b) => b switch
        {
            BottomKind.WideJeans or BottomKind.WideSlacks => (0.025f, 0.092f, 0.014f),
            BottomKind.Joggers => (0.13f, 0.05f, 0.014f),
            BottomKind.ScrubPants => (0.05f, 0.078f, 0.016f),
            BottomKind.Slacks => (0.055f, 0.07f, 0.01f),
            BottomKind.HikingPants => (0.06f, 0.068f, 0.012f),
            _ => (0.055f, 0.066f, 0.008f), // straight jeans
        };

        static float LegX(float y) => Mathf.Lerp(0.087f, HumanContext.HipX + 0.002f, Mathf.InverseLerp(0.05f, 1f, y));

        /// <summary>
        /// Half-widths of a trouser leg at height y: the leg plus ease, never narrower than the cut, and merging
        /// into the seat at the top instead of standing out over it.
        /// </summary>
        static (float rx, float rz) TrouserLeg(HumanContext c, float y)
        {
            var (_, minR, ease) = TrouserCut(c.L.Bottom);
            var leg = c.Leg.At(y, ease);
            float wide = minR * (y < 0.8f ? 1f : Mathf.Lerp(1f, 0.9f, Mathf.InverseLerp(0.8f, 1f, y)));
            float rx = Mathf.Max(leg.Rx, wide), rz = Mathf.Max(leg.Rz, wide * 1.04f);
            if (y > 0.93f)
            {
                var seat = c.Pelvis.At(y, 0.006f);
                float x = LegX(y), w = Smooth(0.93f, 1.0f, y);
                rx = Mathf.Lerp(rx, Mathf.Min(rx, seat.Rx - x), w);
                rz = Mathf.Lerp(rz, Mathf.Min(rz, seat.Rz * Mathf.Sqrt(Mathf.Max(0.1f, 1f - x * x / (seat.Rx * seat.Rx)))), w);
            }
            return (rx, rz);
        }

        /// <summary>The skirt's cross-section at height y (before pleats and folds).</summary>
        static Ring SkirtAt(HumanContext c, float y)
        {
            bool mini = c.L.Bottom == BottomKind.PleatedSkirt;
            float hem = mini ? 0.78f : 0.3f;
            var waist = c.Torso.At(1.07f, 0.008f);
            var hip = c.Pelvis.At(0.95f, 0.014f);
            float t = Mathf.InverseLerp(0.95f, hem, y);
            float rx = y > 0.95f ? Mathf.Lerp(waist.Rx, hip.Rx, Mathf.InverseLerp(1.075f, 0.95f, y)) : hip.Rx + t * (mini ? 0.05f : 0.06f);
            float rz = y > 0.95f ? Mathf.Lerp(waist.Rz, hip.Rz, Mathf.InverseLerp(1.075f, 0.95f, y)) : hip.Rz + t * (mini ? 0.07f : 0.08f);
            return new Ring(y, rx, rz, 0, 0, 0, y > 0.9f ? 0.06f : 0.02f);
        }

        /// <summary>Widens a garment ring that hangs over the hips or thighs so the legs (trousers with their folds,
        /// or bare thighs under a skirt) and the skirt itself never poke through it.</summary>
        static Ring OverLegs(HumanContext c, Ring r, float y, float t, bool overSkirt = true)
        {
            if (overSkirt && Skirt(c.L) && y <= 1.075f)
            {
                // the skirt with its pleats and folds
                var sk = SkirtAt(c, y);
                r.Rx = Mathf.Max(r.Rx, sk.Rx * 1.045f + t + 0.002f);
                r.Rz = Mathf.Max(r.Rz, sk.Rz * 1.045f + t + 0.002f);
                r.Back = Mathf.Max(r.Back, sk.Back);
            }
            if (y > 1.0f) return r;
            y = Mathf.Max(y, 0.05f);
            float x = LegX(y), rx, rz;
            if (Skirt(c.L))
            {
                var leg = c.Leg.At(y);
                rx = leg.Rx + 0.002f;
                rz = leg.Rz + 0.002f;
            }
            else
            {
                (rx, rz) = TrouserLeg(c, y);
                rx *= 1.04f; // the folds
                rz *= 1.04f;
            }
            r.Rx = Mathf.Max(r.Rx, x + rx + t + 0.002f);
            float k = Mathf.Sqrt(Mathf.Max(0.1f, 1f - x * x / (r.Rx * r.Rx)));
            r.Rz = Mathf.Max(r.Rz, (rz + t + 0.002f) / k);
            return r;
        }

        static void Trousers(HumanContext c)
        {
            var l = c.L;
            var cloth = Paint.Cloth(l.BottomFabric, l.BottomColor);
            Weigher trunk = p => Trunk(p, c);

            // pelvis: the seat of the trousers (or the lining under a skirt)
            var pelvis = new List<Ring>();
            for (float y = 1.07f; y >= 0.84f - 1e-4f; y -= 0.0575f)
                pelvis.Add(c.Pelvis.At(y, 0.006f));
            pelvis[^1] = new Ring(0.85f, pelvis[^1].Rx * 0.8f, pelvis[^1].Rz * 0.62f, 0, -0.012f) { Ao = 0.5f };
            Loft(c.Mb, pelvis, c.SegBody, Skirt(l) ? Paint.Cloth(Fabric.Jersey, new Color(0.12f, 0.12f, 0.13f)) : cloth, trunk,
                new LoftOptions { CapBottom = 0.35f });

            foreach (bool left in new[] { true, false })
            {
                float s = left ? -1 : 1;
                Weigher legW = p => Leg(p, left);
                float Cx(float y) => s * LegX(y);

                if (Skirt(l))
                {
                    // bare legs, tights or stockings under the skirt; socks for schoolkids
                    var legPaint = l.LegColor.a > 0 ? Paint.Cloth(Fabric.Satin, l.LegColor) : Paint.SkinOf(l.Skin);
                    var legs = new List<Ring>();
                    for (float y = 0.97f; y >= 0.05f; y -= 0.046f) legs.Add(c.Leg.At(y, l.LegColor.a > 0 ? 0.001f : 0f, Cx(y)));
                    Loft(c.Mb, legs, c.SegLimb, legPaint, legW);
                    if (l.Age == Age.Teen && l.LegColor.a <= 0)
                    {
                        var socks = new List<Ring>();
                        for (float y = 0.16f; y >= 0.06f; y -= 0.025f) socks.Add(c.Leg.At(y, 0.003f, Cx(y)));
                        Loft(c.Mb, socks, c.SegLimb, Paint.Cloth(Fabric.Knit, new Color(0.95f, 0.95f, 0.94f)), legW);
                    }
                    continue;
                }

                var (hem, minR, ease) = TrouserCut(l.Bottom);
                var leg = new List<Ring>();
                for (float y = 1.0f; y > hem + 1e-4f; y -= 0.04f)
                {
                    var r = c.Leg.At(y, ease, Cx(y));
                    (r.Rx, r.Rz) = TrouserLeg(c, y);
                    r.Back *= 0.5f;
                    leg.Add(r);
                }
                var last = c.Leg.At(hem, ease, Cx(hem));
                float hemR = Mathf.Max(last.Rx, minR);
                leg.Add(new Ring(hem, hemR, Mathf.Max(last.Rz, minR * 1.04f), Cx(hem)));
                Loft(c.Mb, leg, c.SegLimb, cloth, legW, new LoftOptions { Wrinkle = 0.035f, WrinkleAround = 2.6f, WrinkleAlong = 9f });
                // the hem: turned-up edge, or a ribbed cuff on joggers
                if (l.Bottom == BottomKind.Joggers)
                {
                    var cuff = new List<Ring>();
                    for (int k = 0; k <= 3; k++)
                    {
                        float y = Mathf.Lerp(hem + 0.005f, 0.075f, k / 3f);
                        cuff.Add(new Ring(y, 0.045f, 0.047f, Cx(y)));
                    }
                    Loft(c.Mb, cuff, c.SegLimb, Paint.Cloth(Fabric.Knit, l.BottomColor * 0.92f), legW);
                }
                else
                {
                    var lip = new List<Ring>
                    {
                        new(hem + 0.012f, hemR + 0.002f, Mathf.Max(last.Rz, minR * 1.04f) + 0.002f, Cx(hem)),
                        new(hem, hemR + 0.002f, Mathf.Max(last.Rz, minR * 1.04f) + 0.002f, Cx(hem)) { Ao = 0.7f },
                    };
                    Loft(c.Mb, lip, c.SegLimb, cloth, legW, new LoftOptions { DoubleSided = true });
                }
            }

            if (Skirt(l)) SkirtShell(c);

            // belt over tucked tops
            if (l.Belt && Tucked(l) && !Skirt(l))
            {
                var belt = new List<Ring> { c.Pelvis.At(1.066f, 0.012f), c.Pelvis.At(1.04f, 0.012f) };
                Loft(c.Mb, belt, c.SegBody, Paint.Cloth(Fabric.Leather, new Color(0.1f, 0.08f, 0.07f)), trunk);
                var front = c.Pelvis.At(1.053f, 0.014f);
                Box(c.Mb, new Vector3(0, 1.053f, front.Rz * (1 + front.Front)), new Vector3(0.032f, 0.026f, 0.006f), Quaternion.identity,
                    Paint.Cloth(Fabric.Satin, new Color(0.72f, 0.72f, 0.7f)), trunk);
            }
        }

        static void SkirtShell(HumanContext c)
        {
            var l = c.L;
            bool mini = l.Bottom == BottomKind.PleatedSkirt;
            float hem = mini ? 0.78f : 0.3f;
            var rings = new List<Ring>();
            var waist = c.Torso.At(1.07f, 0.008f);
            rings.Add(new Ring(1.075f, waist.Rx, waist.Rz));
            int steps = mini ? 5 : 10;
            for (int k = 0; k <= steps; k++)
            {
                float y = Mathf.Lerp(1.0f, hem, k / (float)steps);
                float t = Mathf.InverseLerp(0.95f, hem, y);
                var r = SkirtAt(c, y);
                r.Pleat = mini ? 0.035f * t : 0.012f * t;
                r.Ao = k == steps ? 0.85f : 1f;
                rings.Add(OverLegs(c, r, y, 0.004f, false));
            }
            Loft(c.Mb, rings, c.SegBody, Paint.Cloth(l.BottomFabric, l.BottomColor), p => Trunk(p, c),
                new LoftOptions { DoubleSided = true, PleatCount = mini ? 26 : 9, Wrinkle = mini ? 0.01f : 0.03f, WrinkleAround = 3.5f, WrinkleAlong = 2.5f });
        }

        // ------------------------------------------------------------------ tops

        static bool VNeck(HumanLook l) => l.Top is TopKind.Scrubs or TopKind.Blouse;

        static void Top(HumanContext c)
        {
            var l = c.L;
            float t = TopThickness(l.Top);
            var paint = Paint.Cloth(l.TopFabric, l.TopColor);
            Weigher trunk = p => Trunk(p, c);
            bool tucked = Tucked(l);
            float hem = tucked ? 1.0f : l.Top is TopKind.Hoodie or TopKind.Sweatshirt or TopKind.Knit ? 0.9f : 0.93f;
            float neckTop = l.Top == TopKind.Turtleneck ? 1.6f : 1.53f;

            // skin of the upper chest, seen in V-necks and open collars
            if (VNeck(l) || l.Top is TopKind.Shirt or TopKind.Polo)
            {
                var chest = new List<Ring>();
                for (float y = 1.535f; y >= 1.36f; y -= 0.035f) chest.Add(c.Torso.At(y, 0f));
                Loft(c.Mb, chest, c.SegBody, Paint.SkinOf(l.Skin), trunk);
            }

            var rings = new List<Ring>();
            for (float y = 1.53f; y >= hem - 1e-4f; y -= 0.035f)
            {
                var r = TrunkAt(c, y, t);
                if (y >= 1.52f)
                {
                    // the neckline sits close around the neck
                    r.Rx = Mathf.Lerp(r.Rx, c.NeckRx + 0.02f + t, 0.5f);
                }
                if (VNeck(l)) r.Gap = Mathf.Lerp(0f, 28f, Smooth(1.37f, 1.53f, y));
                else if (l.Top is TopKind.Shirt or TopKind.Polo) r.Gap = Mathf.Lerp(0f, 13f, Smooth(1.45f, 1.53f, y));
                if (y < hem + 0.04f) r.Ao = 0.85f;
                if (!tucked) r = OverLegs(c, r, y, t);
                rings.Add(r);
            }
            Loft(c.Mb, rings, c.SegBody, paint, trunk, new LoftOptions
            {
                DoubleSided = VNeck(l) || l.Top is TopKind.Shirt or TopKind.Polo,
                Wrinkle = l.Top is TopKind.Hoodie or TopKind.Sweatshirt or TopKind.Knit ? 0.02f : 0.012f, WrinkleAround = 3f, WrinkleAlong = 7f,
            });

            // neck rib, turtleneck, collars
            switch (l.Top)
            {
                case TopKind.Turtleneck:
                {
                    var tn = new List<Ring>();
                    for (float y = neckTop; y >= 1.5f; y -= 0.035f) tn.Add(new Ring(y, c.NeckRx + 0.005f + t * 0.5f, c.NeckRz + 0.005f + t * 0.5f, 0, 0.004f));
                    Loft(c.Mb, tn, c.SegLimb, paint, Neck);
                    break;
                }
                case TopKind.Tee:
                case TopKind.LongTee:
                case TopKind.Sweatshirt:
                case TopKind.Knit:
                case TopKind.Hoodie:
                {
                    var rib = new List<Ring>
                    {
                        new(1.548f, c.NeckRx + 0.006f, c.NeckRz + 0.005f, 0, 0.003f),
                        new(1.528f, Mathf.Max(0.074f, c.NeckRx + 0.013f) + t, Mathf.Max(0.066f, c.NeckRz + 0.01f) + t * 0.6f, 0, 0.003f),
                    };
                    Loft(c.Mb, rib, c.SegLimb, Paint.Cloth(Fabric.Knit, l.TopColor * 0.96f), trunk);
                    break;
                }
                case TopKind.Shirt:
                case TopKind.Polo:
                    Collar(c, paint, 0.012f, l.Top == TopKind.Polo ? 1.548f : 1.564f, l.Tie);
                    if (l.Top == TopKind.Polo)
                        for (int k = 0; k < 2; k++)
                            Button(c, 1.5f - k * 0.035f, t + 0.002f, new Color(0.92f, 0.92f, 0.9f), 0.004f);
                    else
                        for (int k = 0; k < 5; k++)
                            Button(c, 1.47f - k * 0.08f, t + 0.001f, new Color(0.94f, 0.94f, 0.92f), 0.0035f);
                    break;
            }

            // hem band of sweatshirts and hoodies
            if (l.Top is TopKind.Sweatshirt or TopKind.Hoodie)
            {
                var band = new List<Ring> { OverLegs(c, TrunkAt(c, hem + 0.045f, t + 0.002f), hem + 0.045f, t + 0.002f), OverLegs(c, TrunkAt(c, hem, t + 0.004f), hem, t + 0.004f) };
                Loft(c.Mb, band, c.SegBody, Paint.Cloth(Fabric.Knit, l.TopColor * 0.95f), trunk);
            }
            if (l.Top == TopKind.Hoodie && l.Outer is OuterKind.None or OuterKind.Varsity or OuterKind.DenimJacket or OuterKind.Windbreaker or OuterKind.LeatherJacket)
                Hood(c, l.TopColor, l.TopFabric, t);
            if (l.Top == TopKind.Hoodie && !c.OuterHasSleeves)
            {
                // drawstrings and the kangaroo pocket
                foreach (float s in new[] { -1f, 1f })
                {
                    var front = TrunkAt(c, 1.48f, t);
                    var path = new[] { new Vector3(s * 0.03f, 1.52f, front.Rz + 0.012f), new Vector3(s * 0.032f, 1.45f, front.Rz * 1.04f + 0.008f), new Vector3(s * 0.034f, 1.38f, front.Rz * 1.05f + 0.01f) };
                    Tube(c.Mb, path, _ => 0.0035f, c.SegSmall, Paint.Cloth(Fabric.Canvas, new Color(0.95f, 0.95f, 0.93f)), trunk);
                }
                var pocket = new List<Ring>();
                for (float y = 1.17f; y >= 1.0f; y -= 0.04f)
                {
                    var r = TrunkAt(c, y, t + 0.004f);
                    pocket.Add(r);
                }
                PartialShell(c, pocket, -38f, 38f, Paint.Cloth(l.TopFabric, l.TopColor * 0.97f), trunk);
            }
        }

        /// <summary>A shirt or polo collar: a band around the neck with the points lying on the chest.</summary>
        static void Collar(HumanContext c, Paint paint, float t, float top, bool tie)
        {
            float nx = c.NeckRx, nz = c.NeckRz;
            var band = new List<Ring>
            {
                new(top, nx + 0.006f, nz + 0.006f, 0, 0.006f) { Gap = tie ? 8f : 18f },
                new(top - 0.02f, nx + 0.011f, nz + 0.011f, 0, 0.006f) { Gap = tie ? 8f : 22f },
                new(1.53f, Mathf.Max(0.08f, nx + 0.019f), Mathf.Max(0.074f, nz + 0.017f), 0, 0.006f) { Gap = tie ? 10f : 26f, Ao = 0.85f },
            };
            Loft(c.Mb, band, c.SegLimb, paint, Neck, new LoftOptions { DoubleSided = true });
            foreach (float s in new[] { -1f, 1f })
            {
                var front = c.Torso.At(1.5f, t);
                var path = new[]
                {
                    new Vector3(s * 0.012f, top - 0.022f, nz + 0.019f),
                    new Vector3(s * 0.03f, 1.522f, Mathf.Max(front.Rz * (1 + front.Front), nz + 0.012f) + 0.006f),
                    new Vector3(s * 0.046f, 1.5f, front.Rz * (1 + front.Front) - 0.002f),
                };
                Tube(c.Mb, path, x => Mathf.Lerp(0.011f, 0.004f, x), c.SegSmall, paint, p => Trunk(p, c), false, 0.22f, Vector3.forward);
            }
        }

        static void Button(HumanContext c, float y, float grow, Color col, float r)
        {
            var front = TrunkAt(c, y, grow);
            float z = front.Cz + front.Rz * (1 + front.Front);
            Ellipsoid(c.Mb, new Vector3(0, y, z), new Vector3(r, r, r * 0.4f), Quaternion.identity, c.SegSmall, 4,
                Paint.Cloth(Fabric.Satin, col), p => Trunk(p, c));
        }

        /// <summary>A partial shell (front panels, pockets) over an angle range, with a lining side.</summary>
        static void PartialShell(HumanContext c, List<Ring> rings, float from, float to, Paint paint, Weigher weigh)
        {
            // rotate the range so it is centred on the front: build it as an open loft whose opening is at the back
            float half = (to - from) * 0.5f;
            var r2 = new List<Ring>();
            foreach (var r in rings)
            {
                var q = r;
                q.Gap = 180f - half; // opening at the back
                r2.Add(q);
            }
            // an open loft starts at Gap and runs to 360-Gap; shift by 180 so the panel faces front
            var rot = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 180, 0), Vector3.one);
            var mirrored = new List<Ring>();
            foreach (var q in r2)
            {
                var m = q;
                m.Cz = -q.Cz;
                (m.Front, m.Back) = (q.Back, q.Front);
                mirrored.Add(m);
            }
            Loft(c.Mb, mirrored, c.SegSmall + 4, paint, weigh, new LoftOptions { Transform = rot, DoubleSided = true });
        }

        static void Hood(HumanContext c, Color col, Fabric fabric, float t)
        {
            // the hood lies folded on the upper back behind the neck
            var rings = new List<Ring>
            {
                new(1.62f, c.NeckRx + 0.036f, 0.08f, 0, -0.065f) { Gap = 70f },
                new(1.585f, c.NeckRx + 0.058f, 0.098f, 0, -0.052f) { Gap = 62f },
                new(1.54f, 0.12f, 0.1f, 0, -0.04f) { Gap = 58f },
                new(1.48f, 0.11f, 0.105f, 0, -0.03f) { Gap = 70f },
                new(1.44f, 0.08f, 0.1f, 0, -0.03f) { Gap = 100f, Ao = 0.8f },
            };
            Loft(c.Mb, rings, c.SegLimb, Paint.Cloth(fabric, col), p => Trunk(p, c), new LoftOptions { DoubleSided = true, Lining = col * 0.6f });
        }

        // ------------------------------------------------------------------ jackets, coats and vests

        static void OuterGarment(HumanContext c)
        {
            var l = c.L;
            if (l.Outer == OuterKind.None) return;
            if (l.Outer == OuterKind.Apron)
            {
                Apron(c);
                return;
            }
            float t = TopThickness(l.Top) + OuterThickness(l.Outer);
            float hem = l.Outer switch
            {
                OuterKind.Cardigan => 0.92f, OuterKind.Blazer => 0.86f, OuterKind.Trench => 0.5f, OuterKind.WoolCoat => 0.45f,
                OuterKind.Puffer => 0.9f, OuterKind.DenimJacket => 0.97f, OuterKind.LeatherJacket => 0.96f, OuterKind.Varsity => 0.95f,
                OuterKind.Windbreaker => 0.92f, OuterKind.PaddedVest => 0.93f, OuterKind.Fleece => 0.92f, OuterKind.LabCoat => 0.5f,
                OuterKind.UniformVest => 1.0f, OuterKind.GuardJacket => 0.88f, _ => 0.9f,
            };
            bool closed = l.Outer is OuterKind.Puffer or OuterKind.Varsity or OuterKind.Windbreaker or OuterKind.PaddedVest or OuterKind.Fleece or OuterKind.GuardJacket;
            bool lapels = l.Outer is OuterKind.Blazer or OuterKind.Trench or OuterKind.WoolCoat or OuterKind.LabCoat;
            var paint = Paint.Cloth(l.OuterFabric, l.OuterColor);
            Weigher trunk = p => Trunk(p, c);

            float Gap(float y)
            {
                if (closed) return 0f;
                switch (l.Outer)
                {
                    case OuterKind.UniformVest:
                        return Mathf.Lerp(3f, 30f, Smooth(1.2f, 1.5f, y));
                    case OuterKind.Cardigan:
                        return Mathf.Lerp(8f, 30f, Smooth(1.15f, 1.52f, y));
                    case OuterKind.Blazer:
                        return y < 1.1f ? Mathf.Lerp(12f, 4f, Smooth(0.86f, 1.1f, y)) : Mathf.Lerp(4f, 34f, Smooth(1.12f, 1.5f, y));
                    case OuterKind.DenimJacket:
                    case OuterKind.LeatherJacket:
                        return Mathf.Lerp(12f, 20f, Smooth(1.2f, 1.5f, y));
                    default: // long coats hang open
                        return Mathf.Lerp(16f, 30f, Smooth(1.15f, 1.5f, y));
                }
            }

            var rings = new List<Ring>();
            float yTop = 1.515f;
            for (float y = yTop; y >= hem - 1e-4f; y -= 0.035f)
            {
                Ring r;
                if (y >= 0.98f) r = TrunkAt(c, y, t);
                else
                {
                    // below the hips coats hang straight down and flare a little
                    var hip = TrunkAt(c, 0.98f, t);
                    float drop = 0.98f - y;
                    float flare = l.Outer is OuterKind.Trench or OuterKind.WoolCoat or OuterKind.LabCoat ? 0.1f : 0.05f;
                    var p = c.Pelvis.At(Mathf.Max(y, c.Pelvis.Bottom), t);
                    r = new Ring(y, Mathf.Max(hip.Rx, p.Rx) + drop * flare, Mathf.Max(hip.Rz, p.Rz) + drop * flare * 1.2f, 0, 0, 0, 0.05f);
                }
                if (l.Outer == OuterKind.Puffer || l.Outer == OuterKind.PaddedVest) r.Rx += 0.006f; // puffy sides
                r = OverLegs(c, r, y, t);
                r.Gap = Gap(y);
                if (y < hem + 0.03f) r.Ao = 0.9f;
                // the yoke of two-tone hiking jackets and windbreakers
                if (l.Outer == OuterKind.Windbreaker && y > 1.36f) r.Color = l.OuterAccent;
                rings.Add(r);
            }
            if (l.Outer == OuterKind.Windbreaker)
            {
                // a crisp edge where the yoke colour changes
                for (int k = 0; k < rings.Count - 1; k++)
                    if (rings[k].Color.HasValue && !rings[k + 1].Color.HasValue)
                    {
                        var edge = rings[k + 1];
                        edge.Y = rings[k + 1].Y + 0.002f;
                        edge.Color = l.OuterAccent;
                        rings.Insert(k + 1, edge);
                        break;
                    }
            }
            Loft(c.Mb, rings, c.SegBody, paint, trunk, new LoftOptions
            {
                DoubleSided = true, Lining = l.OuterColor * (l.Outer == OuterKind.LabCoat ? 0.82f : 0.5f),
                Wrinkle = l.Outer is OuterKind.Puffer or OuterKind.PaddedVest ? 0.008f : 0.02f, WrinkleAround = 3.2f, WrinkleAlong = 3.5f,
            });


            // collars
            if (lapels) Lapels(c, t, paint, Gap);
            else if (closed && l.Outer != OuterKind.GuardJacket)
            {
                float collarTop = l.Outer is OuterKind.Puffer ? 1.6f : 1.572f;
                float collarR = Mathf.Max(l.Outer is OuterKind.Puffer ? 0.09f : 0.078f, c.NeckRx + (l.Outer is OuterKind.Puffer ? 0.03f : 0.017f));
                var stand = new List<Ring>();
                for (float y = collarTop; y >= 1.5f; y -= 0.028f) stand.Add(new Ring(y, collarR, collarR - 0.006f, 0, 0.004f) { Gap = 4f });
                var collarPaint = l.Outer == OuterKind.Varsity ? Paint.Cloth(Fabric.Knit, l.OuterAccent) : paint;
                Loft(c.Mb, stand, c.SegLimb, collarPaint, Neck, new LoftOptions { DoubleSided = true });
            }
            else if (l.Outer == OuterKind.GuardJacket)
                Collar(c, paint, t, 1.558f, true);

            // zips, snaps and buttons down the front
            if (l.Outer is OuterKind.Puffer or OuterKind.PaddedVest or OuterKind.Windbreaker or OuterKind.Fleece)
            {
                var zip = new List<Vector3>();
                for (float y = l.Outer is OuterKind.Puffer ? 1.6f : 1.56f; y >= hem; y -= 0.04f)
                {
                    var r = y > 1.5f ? new Ring(y, 0.09f, 0.084f + 0.004f) : TrunkAt(c, y, t);
                    zip.Add(new Vector3(0, y, r.Cz + r.Rz * (1 + r.Front) + 0.002f));
                }
                Tube(c.Mb, zip, _ => 0.0035f, 4, Paint.Cloth(Fabric.Satin, l.OuterColor * 0.6f), trunk, false, 0.5f, Vector3.forward);
            }
            if (l.Outer is OuterKind.Varsity or OuterKind.GuardJacket or OuterKind.UniformVest or OuterKind.Cardigan or OuterKind.LabCoat or OuterKind.Blazer)
            {
                int n = l.Outer switch { OuterKind.Blazer => 1, OuterKind.LabCoat => 3, OuterKind.Cardigan => 5, OuterKind.UniformVest => 4, _ => 4 };
                float y0 = l.Outer == OuterKind.Blazer ? 1.1f : l.Outer == OuterKind.LabCoat ? 1.2f : 1.38f;
                float dy = l.Outer == OuterKind.Cardigan ? 0.075f : l.Outer == OuterKind.UniformVest ? 0.065f : 0.09f;
                var col = l.Outer == OuterKind.GuardJacket ? l.OuterAccent : l.Outer == OuterKind.Varsity ? l.OuterAccent : l.OuterColor * 0.7f;
                for (int k = 0; k < n; k++) Button(c, y0 - k * dy, t + 0.002f, col, l.Outer == OuterKind.GuardJacket ? 0.0055f : 0.0045f);
            }
            // the trench belt
            if (l.Outer == OuterKind.Trench)
            {
                var b0 = TrunkAt(c, 1.09f, t + 0.004f);
                var b1 = TrunkAt(c, 1.06f, t + 0.004f);
                b0.Gap = b1.Gap = Gap(1.075f);
                var belt = new List<Ring> { b0, b1 };
                Loft(c.Mb, belt, c.SegBody, paint, trunk, new LoftOptions { DoubleSided = true });
            }
            // varsity: ribbed hem in the accent colour with stripes
            if (l.Outer == OuterKind.Varsity)
            {
                var band = new List<Ring>();
                foreach (float y in new[] { hem + 0.05f, hem + 0.032f, hem + 0.028f, hem })
                    band.Add(OverLegs(c, TrunkAt(c, y, t + 0.002f), y, t + 0.002f));
                band[1] = WithColor(band[1], l.OuterColor);
                band[2] = WithColor(band[2], l.OuterColor);
                Loft(c.Mb, band, c.SegBody, Paint.Cloth(Fabric.Knit, l.OuterAccent), trunk);
            }
            // guard: shoulder straps and a badge on the chest
            if (l.Outer == OuterKind.GuardJacket)
            {
                foreach (float s in new[] { -1f, 1f })
                    Box(c.Mb, new Vector3(s * (c.ShoulderX - 0.07f), 1.495f, 0), new Vector3(0.1f, 0.008f, 0.045f), Quaternion.Euler(0, 0, s * -12f),
                        paint, trunk);
                var chest = TrunkAt(c, 1.36f, t);
                Box(c.Mb, new Vector3(-0.08f, 1.36f, chest.Rz * (1 + chest.Front) * 0.96f), new Vector3(0.035f, 0.04f, 0.006f), Quaternion.Euler(0, -18, 0),
                    Paint.Cloth(Fabric.Satin, l.OuterAccent), trunk);
            }
        }

        static Ring WithColor(Ring r, Color col)
        {
            r.Color = col;
            return r;
        }

        static void Lapels(HumanContext c, float t, Paint paint, System.Func<float, float> gap)
        {
            // lapels: the edge of the opening folded back over the chest
            var l = c.L;
            var rings = new List<Ring>();
            for (float y = 1.5f; y >= (l.Outer == OuterKind.Blazer ? 1.15f : 1.22f); y -= 0.035f)
            {
                var r = TrunkAt(c, y, t + 0.004f);
                r.Gap = gap(y);
                rings.Add(r);
            }
            // only the first ~22 degrees after each edge of the opening: build two narrow shells
            foreach (bool right in new[] { true, false })
            {
                var lap = new List<Ring>();
                foreach (var r in rings)
                {
                    var q = r;
                    q.Gap = 0;
                    lap.Add(q);
                }
                int n = lap.Count;
                var pos = new Vector3[5, n];
                var outward = new Vector3[5, n];
                var uvs = new Vector2[5, n];
                for (int j = 0; j < n; j++)
                {
                    var r = lap[j];
                    float width = Mathf.Lerp(20f, 6f, Mathf.InverseLerp(1.5f, rings[^1].Y, r.Y));
                    for (int i = 0; i < 5; i++)
                    {
                        float th = (rings[j].Gap + width * i / 4f) * (right ? 1 : -1) * Mathf.Deg2Rad;
                        float bulge = 1f + r.Front * Mathf.Pow(Mathf.Max(0, Mathf.Cos(th)), 2);
                        var p = new Vector3(r.Cx + r.Rx * bulge * Mathf.Sin(th), r.Y, r.Cz + r.Rz * bulge * Mathf.Cos(th));
                        pos[i, j] = p;
                        outward[i, j] = new Vector3(p.x, 0, p.z);
                        uvs[i, j] = new Vector2(i * 0.01f / paint.Metres, r.Y / paint.Metres);
                    }
                }
                Grid(c.Mb, pos, uvs, outward, paint, p => Trunk(p, c), false, false, null, null, true);
            }
            // collar band behind the neck
            var back = new List<Ring>
            {
                new(1.572f, c.NeckRx + 0.012f, c.NeckRz + 0.012f, 0, 0.0f) { Gap = 55f },
                new(1.545f, c.NeckRx + 0.022f, c.NeckRz + 0.021f, 0, 0.0f) { Gap = 45f },
                new(1.51f, 0.1f, 0.092f, 0, 0.0f) { Gap = 38f },
            };
            Loft(c.Mb, back, c.SegLimb, paint, Neck, new LoftOptions { DoubleSided = true });
        }

        static void Apron(HumanContext c)
        {
            var l = c.L;
            var paint = Paint.Cloth(Fabric.Canvas, l.OuterColor);
            var rings = new List<Ring>();
            float t = TopThickness(l.Top) + 0.006f;
            for (float y = 1.42f; y >= 0.58f; y -= 0.04f)
            {
                Ring r = y >= 0.98f ? TrunkAt(c, y, t) : new Ring(y, TrunkAt(c, 0.98f, t).Rx + (0.98f - y) * 0.03f, TrunkAt(c, 0.98f, t).Rz + (0.98f - y) * 0.05f);
                rings.Add(OverLegs(c, r, y, t));
            }
            PartialShell(c, rings, -62f, 62f, paint, p => Trunk(p, c));
            // neck strap and waist ties
            var front = TrunkAt(c, 1.42f, t);
            var strap = new[] { new Vector3(-0.07f, 1.42f, front.Rz * 0.8f), new Vector3(-0.06f, 1.53f, 0.02f), new Vector3(0, 1.57f, -0.06f), new Vector3(0.06f, 1.53f, 0.02f), new Vector3(0.07f, 1.42f, front.Rz * 0.8f) };
            Tube(c.Mb, strap, _ => 0.006f, c.SegSmall, paint, p => Trunk(p, c), false, 0.35f, Vector3.forward);
            var waist = new List<Ring> { TrunkAt(c, 1.08f, t + 0.006f), TrunkAt(c, 1.06f, t + 0.006f) };
            Loft(c.Mb, waist, c.SegBody, paint, p => Trunk(p, c));
        }

        // ------------------------------------------------------------------ arms and hands

        static void Arms(HumanContext c)
        {
            var l = c.L;
            bool outerSleeves = c.OuterHasSleeves;
            bool longTop = l.Top is not (TopKind.Tee or TopKind.Polo or TopKind.Scrubs);
            foreach (bool left in new[] { true, false })
            {
                float s = left ? -1 : 1;
                float cx = s * c.ShoulderX;
                Weigher armW = p => Arm(p, left);

                if (outerSleeves)
                {
                    float t = TopThickness(l.Top) + OuterThickness(l.Outer) * (l.Outer == OuterKind.Puffer ? 0.5f : 0.7f);
                    var paint = l.Outer == OuterKind.Varsity ? Paint.Cloth(Fabric.Leather, l.OuterAccent) : Paint.Cloth(l.OuterFabric, l.OuterColor);
                    Sleeve(c, cx, t, 0.93f, paint, armW, true);
                    // ribbed cuffs on varsity, fleece and windbreakers; shirt cuffs peek out of tailored sleeves
                    if (l.Outer is OuterKind.Varsity or OuterKind.Puffer or OuterKind.Fleece or OuterKind.Windbreaker)
                        Cuff(c, cx, 0.93f, 0.9f, Paint.Cloth(Fabric.Knit, l.Outer == OuterKind.Varsity ? l.OuterColor : l.OuterColor * 0.85f), armW, 0.006f);
                    else if (l.Top is TopKind.Shirt or TopKind.Blouse)
                        Cuff(c, cx, 0.935f, 0.915f, Paint.Cloth(l.TopFabric, l.TopColor), armW, 0.004f);
                }
                else if (longTop)
                {
                    float t = TopThickness(l.Top);
                    Sleeve(c, cx, t, 0.925f, Paint.Cloth(l.TopFabric, l.TopColor), armW, true);
                    if (l.Top is TopKind.Sweatshirt or TopKind.Hoodie or TopKind.Knit or TopKind.Turtleneck)
                        Cuff(c, cx, 0.95f, 0.915f, Paint.Cloth(Fabric.Knit, l.TopColor * 0.96f), armW, 0.003f);
                    if (l.Top is TopKind.Shirt or TopKind.Blouse)
                        Cuff(c, cx, 0.95f, 0.918f, Paint.Cloth(l.TopFabric, l.TopColor), armW, 0.004f);
                }
                else
                {
                    // short sleeves over bare arms
                    float t = TopThickness(l.Top) + 0.006f;
                    Sleeve(c, cx, t, 1.3f, Paint.Cloth(l.TopFabric, l.TopColor), armW, true, true);
                    var skin = new List<Ring>();
                    for (float y = 1.33f; y >= 0.915f; y -= 0.04f) skin.Add(c.Arm.At(y, 0, cx));
                    Loft(c.Mb, skin, c.SegLimb, Paint.SkinOf(l.Skin), armW);
                }
                Hand(c, left, cx);
                if (l.Watch && left)
                {
                    var wrist = c.Arm.At(0.955f, 0.004f, cx);
                    var band = new List<Ring> { new(0.965f, wrist.Rx + 0.002f, wrist.Rz + 0.002f, cx), new(0.948f, wrist.Rx + 0.002f, wrist.Rz + 0.002f, cx) };
                    Loft(c.Mb, band, c.SegSmall, Paint.Cloth(Fabric.Leather, new Color(0.15f, 0.12f, 0.1f)), armW);
                    Box(c.Mb, new Vector3(cx - wrist.Rx - 0.003f, 0.957f, 0), new Vector3(0.006f, 0.022f, 0.022f), Quaternion.identity,
                        Paint.Cloth(Fabric.Satin, new Color(0.78f, 0.78f, 0.76f)), armW);
                }
            }
        }

        static void Sleeve(HumanContext c, float cx, float t, float end, Paint paint, Weigher w, bool cap, bool openEnd = false)
        {
            var rings = new List<Ring>();
            if (cap) rings.AddRange(SleeveHead(c, cx, t));
            for (float y = 1.38f; y >= end - 1e-4f; y -= 0.035f)
            {
                var r = c.Arm.At(y, t, cx);
                // sleeves hang a little looser below the elbow
                if (y < 1.18f) r = r.Grow(t * 0.3f);
                rings.Add(r);
            }
            if (rings[^1].Y > end + 0.002f) rings.Add(c.Arm.At(end, t, cx));
            var last = rings[^1];
            last.Ao = 0.8f;
            rings[^1] = last;
            Loft(c.Mb, rings, c.SegLimb, paint, w, new LoftOptions { CapTop = cap ? 0.6f : 0f, DoubleSided = true, Wrinkle = 0.03f, WrinkleAround = 2.4f, WrinkleAlong = 16f });
        }

        /// <summary>
        /// The sleeve head, top ring first: above the armpit the rings lean in towards the neck and narrow, so
        /// the sleeve rounds over the deltoid and meets the shoulder seam of the garment instead of standing up
        /// like a puff sleeve.
        /// </summary>
        static IEnumerable<Ring> SleeveHead(HumanContext c, float cx, float t)
        {
            const float y0 = 1.38f;
            float side = Mathf.Sign(cx);
            float seam = Mathf.Abs(cx) - 0.035f;
            // the seam sits where the torso is as wide as the seam line, near the top of the shoulder
            float y1 = 1.52f;
            while (y1 > 1.43f && c.Torso.At(y1, t).Rx < seam) y1 -= 0.002f;
            var arm = c.Arm.At(y0, t, cx);
            const int steps = 6;
            for (int k = steps; k >= 1; k--)
            {
                float a = k / (float)steps;
                float round = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a));
                float x = Mathf.Lerp(cx, side * seam, Smooth(0f, 1f, a));
                yield return new Ring(Mathf.Lerp(y0, y1, a), Mathf.Max(0.006f, arm.Rx * round), Mathf.Max(0.006f, arm.Rz * round), x);
            }
        }

        static void Cuff(HumanContext c, float cx, float yTop, float yBottom, Paint paint, Weigher w, float extra)
        {
            var top = c.Arm.At(yTop, extra + 0.004f, cx);
            var bottom = c.Arm.At(yBottom, extra + 0.004f, cx);
            Loft(c.Mb, new List<Ring> { top, bottom }, c.SegLimb, paint, w, new LoftOptions { DoubleSided = true });
        }

        static void Hand(HumanContext c, bool left, float cx)
        {
            var l = c.L;
            float k = l.Female ? 0.9f : 1f;
            var paint = Paint.SkinOf(l.Skin);
            int bone = left ? Bones.ElbowL : Bones.ElbowR;
            Weigher w = Bones.Fixed(bone);
            float s = left ? -1 : 1;
            // palm faces the thigh: thin across x, wide along z; fingers curl slightly forward
            var rings = new List<Ring>
            {
                new(0.925f, 0.018f * k, 0.026f * k, cx, 0.002f),
                new(0.9f, 0.017f * k, 0.033f * k, cx - s * 0.001f, 0.004f),
                new(0.872f, 0.0155f * k, 0.037f * k, cx - s * 0.002f, 0.005f),
                new(0.85f, 0.014f * k, 0.036f * k, cx - s * 0.003f, 0.007f),
                new(0.828f, 0.0125f * k, 0.031f * k, cx - s * 0.004f, 0.01f),
                new(0.812f, 0.011f * k, 0.024f * k, cx - s * 0.005f, 0.012f),
            };
            Loft(c.Mb, rings, c.SegLimb, paint, w, new LoftOptions { CapBottom = 0.6f });
            var thumb = new[]
            {
                new Vector3(cx - s * 0.006f, 0.9f, 0.022f * k),
                new Vector3(cx - s * 0.009f, 0.875f, 0.036f * k),
                new Vector3(cx - s * 0.01f, 0.855f, 0.041f * k),
            };
            Tube(c.Mb, thumb, x => Mathf.Lerp(0.0105f, 0.0075f, x) * k, c.SegSmall, paint, w);
        }

        // ------------------------------------------------------------------ shoes

        static void Shoes(HumanContext c)
        {
            var l = c.L;
            foreach (bool left in new[] { true, false })
            {
                float x = (left ? -1 : 1) * 0.087f;
                Weigher w = Bones.Fixed(left ? Bones.KneeL : Bones.KneeR);
                // lofts along the foot: Y = from heel to toe, X = width, Z = height (swapped into place)
                var swap = Matrix4x4.TRS(new Vector3(x, 0, 0), Quaternion.identity, Vector3.one) * new Matrix4x4(
                    new Vector4(1, 0, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 0, 1));
                float len = l.Female ? 0.92f : 1f;
                float wid = l.Female ? 0.9f : 1f;
                float height = l.Shoes switch
                {
                    ShoeKind.Flats or ShoeKind.Heels => 0.62f, ShoeKind.Loafers => 0.8f, ShoeKind.ChunkySneakers => 1.12f, _ => 1f,
                };
                var keys = new (float y, float rx, float rz)[]
                {
                    (-0.06f, 0.034f, 0.03f), (-0.045f, 0.04f, 0.04f), (-0.005f, 0.043f, 0.042f), (0.04f, 0.045f, 0.037f),
                    (0.09f, 0.048f, 0.029f), (0.14f, 0.047f, 0.022f), (0.172f, 0.04f, 0.018f), (0.186f, 0.03f, 0.014f),
                };
                var upper = new List<Ring>();
                foreach (var k in keys)
                {
                    float rz = k.rz * height;
                    if (l.Shoes == ShoeKind.Heels && k.y < 0.03f) rz += 0.012f * Mathf.InverseLerp(0.03f, -0.06f, k.y);
                    upper.Add(new Ring(k.y * len, k.rx * wid, rz, 0, rz + (l.Shoes == ShoeKind.Heels ? 0.01f * Mathf.InverseLerp(0.12f, -0.06f, k.y) : 0f)));
                }
                var upperPaint = Paint.Cloth(l.Shoes is ShoeKind.Sneakers or ShoeKind.ChunkySneakers or ShoeKind.WalkingShoes ? Fabric.Sneaker : Fabric.Leather, l.ShoeColor);
                Loft(c.Mb, upper, c.SegLimb, upperPaint, w, new LoftOptions { CapTop = 0.8f, CapBottom = 0.8f, Transform = swap });

                // sole
                float soleH = l.Shoes switch { ShoeKind.ChunkySneakers => 0.024f, ShoeKind.Sneakers or ShoeKind.WalkingShoes => 0.014f, ShoeKind.Boots => 0.012f, _ => 0.006f };
                var sole = new List<Ring>();
                foreach (var k in keys) sole.Add(new Ring(k.y * len, k.rx * wid + 0.003f, soleH * 0.5f + 0.001f, 0, soleH * 0.5f));
                Loft(c.Mb, sole, c.SegLimb, Paint.Cloth(Fabric.Poplin, l.SoleColor), w, new LoftOptions { CapTop = 0.9f, CapBottom = 0.9f, Transform = swap });

                if (l.Shoes == ShoeKind.Heels)
                    Box(c.Mb, new Vector3(x, 0.018f, -0.045f * len), new Vector3(0.022f, 0.036f, 0.022f), Quaternion.identity, Paint.Cloth(Fabric.Leather, l.SoleColor), w);
                if (l.Shoes == ShoeKind.Boots)
                {
                    var shaft = new List<Ring>();
                    for (float y = 0.19f; y >= 0.05f; y -= 0.035f) shaft.Add(new Ring(y, 0.045f, 0.048f, x, -0.004f));
                    Loft(c.Mb, shaft, c.SegLimb, upperPaint, w);
                }
                // laces on sneakers
                if (l.Shoes is ShoeKind.Sneakers or ShoeKind.ChunkySneakers or ShoeKind.WalkingShoes)
                    for (int k = 0; k < 4; k++)
                    {
                        float z = (0.01f + k * 0.025f) * len;
                        float y = Mathf.Lerp(0.083f, 0.056f, k / 3f) * height;
                        Box(c.Mb, new Vector3(x, y, z), new Vector3(0.05f * wid, 0.005f, 0.008f), Quaternion.Euler(-28, 0, 0),
                            Paint.Cloth(Fabric.Canvas, l.ShoeColor * 0.94f), w);
                    }
            }
        }

        // ------------------------------------------------------------------ ties, scarves, lanyards, stethoscopes

        static void Neckwear(HumanContext c)
        {
            var l = c.L;
            Weigher trunk = p => Trunk(p, c);
            float t = TopThickness(l.Top);
            if (l.Tie)
            {
                var paint = Paint.Cloth(Fabric.Satin, l.TieColor);
                Ellipsoid(c.Mb, new Vector3(0, 1.528f, 0.078f), new Vector3(0.011f, 0.012f, 0.008f), Quaternion.Euler(8, 0, 0), c.SegSmall, 5, paint, Neck);
                var path = new List<Vector3>();
                for (float y = 1.515f; y >= 1.21f; y -= 0.03f)
                {
                    var r = TrunkAt(c, y, t);
                    path.Add(new Vector3(0, y, r.Cz + r.Rz * (1 + r.Front) + 0.004f));
                }
                Tube(c.Mb, path, x => Mathf.Lerp(0.011f, 0.035f, Mathf.Pow(x, 0.7f)) * (x > 0.92f ? Mathf.Lerp(1f, 0.3f, (x - 0.92f) / 0.08f) : 1f), c.SegSmall, paint, trunk, false, 0.14f, Vector3.forward);
            }
            if (l.Scarf)
            {
                var paint = Paint.Cloth(Fabric.Satin, l.ScarfColor);
                Ellipsoid(c.Mb, new Vector3(0.018f, 1.52f, 0.082f), new Vector3(0.022f, 0.016f, 0.012f), Quaternion.Euler(0, 0, 20), c.SegSmall, 5, paint, trunk);
                foreach (float s in new[] { -1f, 1f })
                {
                    var tail = new[] { new Vector3(0.02f, 1.515f, 0.085f), new Vector3(0.02f + s * 0.02f, 1.47f, 0.093f), new Vector3(0.02f + s * 0.028f, 1.43f, 0.095f) };
                    Tube(c.Mb, tail, x => Mathf.Lerp(0.012f, 0.018f, x), c.SegSmall, paint, trunk, false, 0.2f, Vector3.forward);
                }
                var band = new List<Ring> { new(1.545f, c.NeckRx + 0.008f, c.NeckRz + 0.008f, 0, 0.004f), new(1.525f, c.NeckRx + 0.012f, c.NeckRz + 0.012f, 0, 0.004f) };
                Loft(c.Mb, band, c.SegLimb, paint, Neck);
            }
            if (l.Lanyard)
            {
                float tt = t + (c.OuterHasSleeves ? OuterThickness(l.Outer) : 0);
                var card = TrunkAt(c, 1.3f, tt);
                float zc = card.Rz * (1 + card.Front) + 0.006f;
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new[]
                    {
                        new Vector3(0, 1.545f, -(c.NeckRz + 0.008f)), new Vector3(s * (c.NeckRx + 0.006f), 1.545f, -0.02f), new Vector3(s * (c.NeckRx + 0.01f), 1.53f, 0.04f),
                        new Vector3(s * 0.04f, 1.42f, TrunkAt(c, 1.42f, tt).Rz * 1.05f + 0.004f), new Vector3(s * 0.008f, 1.335f, zc),
                    };
                    Tube(c.Mb, path, _ => 0.0035f, 4, Paint.Cloth(Fabric.Canvas, l.LanyardColor), trunk, false, 0.35f);
                }
                Box(c.Mb, new Vector3(0, 1.29f, zc + 0.002f), new Vector3(0.055f, 0.08f, 0.004f), Quaternion.Euler(-6, 0, 0), Paint.Cloth(Fabric.Satin, new Color(0.96f, 0.96f, 0.95f)), trunk);
                Box(c.Mb, new Vector3(0, 1.315f, zc + 0.0045f), new Vector3(0.056f, 0.022f, 0.002f), Quaternion.Euler(-6, 0, 0), Paint.Cloth(Fabric.Satin, l.LanyardColor), trunk);
            }
            if (l.NameTag)
            {
                float tt = t + (c.OuterHasSleeves || l.Outer is OuterKind.UniformVest or OuterKind.PaddedVest ? OuterThickness(l.Outer) : 0);
                var chest = TrunkAt(c, 1.36f, tt);
                float th = 22f * Mathf.Deg2Rad;
                var pos = new Vector3(-chest.Rx * Mathf.Sin(th) * 1.02f, 1.37f, chest.Rz * (1 + chest.Front * 0.9f) * Mathf.Cos(th) + 0.002f);
                Box(c.Mb, pos, new Vector3(0.06f, 0.018f, 0.004f), Quaternion.Euler(0, -22, 0), Paint.Cloth(Fabric.Satin, new Color(0.95f, 0.95f, 0.93f)), trunk);
            }
            if (l.Stethoscope)
            {
                float tt = t + OuterThickness(l.Outer);
                var paint = Paint.Cloth(Fabric.Leather, new Color(0.08f, 0.08f, 0.09f));
                var path = new List<Vector3>
                {
                    new(-0.05f, 1.33f, TrunkAt(c, 1.33f, tt).Rz * 1.06f), new(-0.06f, 1.42f, TrunkAt(c, 1.42f, tt).Rz * 1.06f),
                    new(-0.075f, 1.52f, 0.03f), new(-0.05f, 1.56f, -0.055f), new(0.05f, 1.56f, -0.055f), new(0.075f, 1.52f, 0.03f),
                    new(0.06f, 1.42f, TrunkAt(c, 1.42f, tt).Rz * 1.06f), new(0.05f, 1.3f, TrunkAt(c, 1.3f, tt).Rz * 1.07f),
                };
                Tube(c.Mb, path, _ => 0.0045f, c.SegSmall, paint, trunk);
                Ellipsoid(c.Mb, path[^1] + new Vector3(0, -0.012f, 0.004f), new Vector3(0.014f, 0.014f, 0.006f), Quaternion.identity, c.SegSmall, 4,
                    Paint.Cloth(Fabric.Satin, new Color(0.75f, 0.76f, 0.78f)), trunk);
            }
        }

        // ------------------------------------------------------------------ bags

        /// <summary>The height of the top of the shoulder (the garment with thickness t) at distance x from the middle.</summary>
        static float ShoulderTop(HumanContext c, float x, float t)
        {
            float y = 1.54f;
            while (y > 1.4f && c.Torso.At(y, t).Rx < x) y -= 0.002f;
            return y;
        }

        static void Carry(HumanContext c)
        {
            var l = c.L;
            if (l.Bag == BagKind.None) return;
            Weigher trunk = p => Trunk(p, c);
            float t = TopThickness(l.Top) + (l.Outer != OuterKind.None ? OuterThickness(l.Outer) : 0);
            var leather = Paint.Cloth(l.Bag is BagKind.Tote or BagKind.Backpack ? Fabric.Canvas : Fabric.Leather, l.BagColor);
            switch (l.Bag)
            {
                case BagKind.Crossbody:
                {
                    // strap from the right shoulder across the chest to a small bag on the left hip
                    var path = new List<Vector3>();
                    for (int k = 0; k <= 8; k++)
                    {
                        float a = k / 8f;
                        float y = Mathf.Lerp(1.49f, 1.0f, a);
                        float x = Mathf.Lerp(0.12f, -0.15f, a);
                        var r = TrunkAt(c, y, t + 0.004f);
                        float th = Mathf.Asin(Mathf.Clamp(x / Mathf.Max(0.01f, r.Rx), -1, 1));
                        float bulge = 1 + r.Front * Mathf.Pow(Mathf.Cos(th), 2);
                        path.Add(new Vector3(r.Rx * bulge * Mathf.Sin(th), y, r.Rz * bulge * Mathf.Cos(th) + 0.003f));
                    }
                    Tube(c.Mb, path, _ => 0.009f, 6, leather, trunk, false, 0.25f, Vector3.forward);
                    var hip = TrunkAt(c, 0.98f, t);
                    Box(c.Mb, new Vector3(-hip.Rx * 0.75f, 0.95f, hip.Rz * 0.75f + 0.03f), new Vector3(0.15f, 0.12f, 0.05f), Quaternion.Euler(0, -38, 0), leather, trunk);
                    break;
                }
                case BagKind.Tote:
                case BagKind.Shoulder:
                {
                    // on the right shoulder: the bag hangs from its handles against the side of the hip, under the
                    // arm (the rig holds that arm a little out and keeps it from swinging, HumanRig.shoulderBag)
                    bool tote = l.Bag == BagKind.Tote;
                    float th = tote ? 0.028f : 0.045f, depth = tote ? 0.3f : 0.24f;
                    float yTop = tote ? 1.15f : 1.19f, yBottom = tote ? 0.85f : 1.0f, zc = -0.015f;
                    bool longCoat = l.Outer is OuterKind.Trench or OuterKind.WoolCoat or OuterKind.LabCoat;
                    float Outside(float y)
                    {
                        float x = OverLegs(c, TrunkAt(c, y, t), y, t).Rx + 0.006f;
                        if (longCoat && y < 0.98f) x += (0.98f - y) * 0.12f;
                        return x + th * 0.5f;
                    }
                    // the top edge rests against the waist and the bottom leans out just enough to clear the hip
                    float xTop = Outside(yTop), xBottom = xTop;
                    for (float y = yBottom; y < yTop - 0.02f; y += 0.025f)
                        xBottom = Mathf.Max(xBottom, xTop + (Outside(y) - xTop) * (yTop - yBottom) / (yTop - y));
                    float lean = Mathf.Atan2(xBottom - xTop, yTop - yBottom) * Mathf.Rad2Deg;
                    var bagCenter = new Vector3((xTop + xBottom) * 0.5f, (yTop + yBottom) * 0.5f, zc);
                    Box(c.Mb, bagCenter, new Vector3(th, yTop - yBottom, depth), Quaternion.Euler(0, 0, lean), leather,
                        p => Trunk(new Vector3(p.x, 1.2f, p.z), c));

                    // handles: up from the top edge, over the shoulder and down the other side (a tote has two,
                    // one on each face; a shoulder bag one strap from corner to corner)
                    float xs = c.ShoulderX - 0.05f, topY = ShoulderTop(c, xs, t) + 0.006f;
                    float zEnd = depth * (tote ? 0.28f : 0.42f);
                    foreach (float face in tote ? new[] { -1f, 1f } : new[] { 0f })
                    {
                        float hx = face * th * 0.3f;
                        var path = new List<Vector3>();
                        foreach (float s in new[] { 1f, -1f })
                        {
                            var half = new List<Vector3>
                            {
                                new(xTop + hx, yTop - 0.01f, zc + s * zEnd),
                                new(xTop + hx, yTop + 0.035f, zc + s * zEnd),
                                new(Mathf.Lerp(xs, xTop, 0.5f) + hx, 1.3f, Mathf.Lerp(0.065f * s, zc + s * zEnd, 0.5f) + s * 0.006f),
                                new(xs + 0.003f + hx, 1.43f, s * 0.064f),
                                new(xs + hx, topY - 0.012f, s * 0.034f),
                            };
                            if (s < 0) half.Reverse();
                            path.AddRange(half);
                            if (s > 0) path.Add(new Vector3(xs + hx, topY, 0f));
                        }
                        Tube(c.Mb, path, _ => tote ? 0.01f : 0.008f, 5, leather, trunk, false, 0.28f, Vector3.forward);
                    }
                    break;
                }
                case BagKind.Backpack:
                {
                    var back = TrunkAt(c, 1.3f, t);
                    float z = -(back.Rz * (1 + back.Back)) - 0.065f;
                    Box(c.Mb, new Vector3(0, 1.3f, z), new Vector3(0.28f, 0.36f, 0.12f), Quaternion.Euler(4, 0, 0), leather, trunk);
                    Box(c.Mb, new Vector3(0, 1.22f, z - 0.065f), new Vector3(0.2f, 0.14f, 0.03f), Quaternion.Euler(4, 0, 0), leather.With(l.BagColor * 0.9f), trunk);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var path = new List<Vector3> { new(s * 0.07f, 1.47f, z + 0.05f) };
                        var sh = TrunkAt(c, 1.49f, t);
                        path.Add(new Vector3(s * 0.11f, 1.505f, -0.02f));
                        path.Add(new Vector3(s * 0.12f, 1.48f, sh.Rz * 0.9f + 0.01f));
                        foreach (float y in new[] { 1.38f, 1.26f, 1.16f })
                        {
                            var r = TrunkAt(c, y, t + 0.004f);
                            float th = 55f * Mathf.Deg2Rad;
                            path.Add(new Vector3(s * r.Rx * Mathf.Sin(th), y, r.Rz * (1 + r.Front * 0.3f) * Mathf.Cos(th)));
                        }
                        path.Add(new Vector3(s * 0.13f, 1.14f, z + 0.03f));
                        Tube(c.Mb, path, _ => 0.01f, 5, leather.With(l.BagColor * 0.85f), trunk, false, 0.3f);
                    }
                    break;
                }
            }
        }
    }
}
