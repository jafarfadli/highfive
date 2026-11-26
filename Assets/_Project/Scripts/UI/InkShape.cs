using System.Collections.Generic;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    public enum InkKind
    {
        /// <summary>A sheet of paper: rough rounded rectangle, ink outline, hard offset shadow.</summary>
        Panel,
        /// <summary>A dry-brush strip across the rect, painted left to right (reveal = how far).</summary>
        Strip,
        /// <summary>A rough ellipse (badges, dots, discs).</summary>
        Disc,
        /// <summary>A hand-drawn ring, the ○ that marks a right answer (reveal = how much is drawn).</summary>
        Ring,
        /// <summary>A pen stroke along <see cref="InkShape.points"/> (slashes, arrows, underlines, ticks).</summary>
        Stroke,
        /// <summary>A speech balloon: rounded rectangle with a tail pointing at <see cref="InkShape.tail"/>.</summary>
        Balloon,
        /// <summary>A thermal ticket with zigzag top and bottom edges.</summary>
        Ticket,
        /// <summary>A comic burst with spikes.</summary>
        Burst,
        /// <summary>A retro sunburst: <see cref="InkShape.spikes"/> rays from the centre out past the corners.</summary>
        Rays,
    }

    /// <summary>
    /// Procedural ink geometry for the retro-print UI: thick ink lines with only a trace of hand wobble,
    /// solid hard shadows, flat fills with a faint print grain, and brush strips that run only slightly dry
    /// at their ends (UI Ink shader). Sizes are free: nothing is a stretched sprite. Boiling redraws the
    /// wobble three ways at 8 fps; reveal draws strips, rings and strokes on.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class InkShape : MaskableGraphic
    {
        public InkKind kind = InkKind.Panel;

        [Header("Colour")]
        [Tooltip("Colours come from theme roles (and repaint on a theme switch); otherwise from the fixed colours.")]
        public bool themed = true;
        public bool hasFill = true;
        public Role fillRole = Role.Paper;
        [Range(0, 1)] public float fillAlpha = 1f;
        public Role lineRole = Role.Ink;
        [Range(0, 1)] public float lineAlpha = 1f;
        public Role shadowRole = Role.Ink;
        [Range(0, 1)] public float shadowAlpha = 1f;
        public Color fillColor = Color.white, lineColor = Color.black, shadowColor = new(0, 0, 0, 1f);

        [Header("Ink")]
        [Tooltip("Outline width in canvas pixels (strokes and rings: the stroke width).")]
        public float line = 4f;
        public Vector2 shadow = new(8, -9);
        [Tooltip("Low-frequency wobble of the edges, pixels.")]
        public float rough = 1.4f;
        [Tooltip("High-frequency raggedness of the edges, pixels (torn paper, brush hairs).")]
        public float tear = 0.6f;
        public float radius = 7f;
        public int seed = 1;
        public bool boil;
        [Range(0, 1)] public float reveal = 1f;

        [Header("Print")]
        [Range(0, 2)] public float grain = 1f;
        [Tooltip("Strips: how dry the brush runs at the ends.")]
        [Range(0, 1)] public float dry = 1f;
        [Tooltip("Strips: a crisp rectangle (square ends, straight edges, no dry brush) instead of a brush stroke.")]
        public bool square = true;
        [Tooltip("Missing ink in blotches (stamps).")]
        [Range(0, 1)] public float grunge;
        [Tooltip("0 = solid; above 0 the fill is printed as halftone dots of that density.")]
        [Range(0, 1)] public float halftone;

        [Header("Shape")]
        [Tooltip("Stroke path in normalized rect coordinates; a NaN point starts a new stroke.")]
        public Vector2[] points;
        [Tooltip("Strokes: width at the ends as a fraction of the full width.")]
        [Range(0, 1)] public float taper = 0.25f;
        public float startAngle = 215f, sweep = 390f;
        [Tooltip("Balloon tail tip in normalized rect coordinates (y > 1 is above the balloon).")]
        public Vector2 tail = new(0.42f, 1.45f);
        public float tailWidth = 42f;
        public float tooth = 10f;
        public int spikes = 14;

        // The retro print: confident lines. Whatever a shape asks for, its edges keep only this much of the
        // hand-inked wobble and raggedness, its fill this much paper grain, its strip ends this much dry brush.
        const float Wobble = 0.35f, Ragged = 0.15f, GrainScale = 0.2f, DryScale = 0.3f;

        int boilFrame;
        float nextBoil;

        static readonly List<Vector2> Pts = new(512), Nrm = new(512);
        static readonly List<float> Wid = new(512);

        public override Texture mainTexture
        {
            get
            {
                var style = InkStyle.Current;
                return style && style.noise ? style.noise : Texture2D.whiteTexture;
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                var style = InkStyle.Current;
                return style && style.material ? style.material : base.defaultMaterial;
            }
        }

        void Update()
        {
            if (!boil || Settings.ReducedMotion) return;
            if (Time.unscaledTime < nextBoil) return;
            nextBoil = Time.unscaledTime + 0.125f;
            boilFrame = (boilFrame + 1) % 3;
            SetVerticesDirty();
        }

        public void SetReveal(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(reveal, value)) return;
            reveal = value;
            SetVerticesDirty();
        }

        Color Paint(Role role, float alpha, Color fixedColor)
        {
            var c = themed ? Theme.Get(role) : fixedColor;
            if (themed) c.a = alpha;
            return c * color;
        }

        // ------------------------------------------------------------------ noise

        static float Hash(int i)
        {
            unchecked
            {
                uint h = (uint)i * 747796405u + 2891336453u;
                h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
                h = (h >> 22) ^ h;
                return h / 4294967295f * 2f - 1f;
            }
        }

        /// <summary>Smooth value noise in [-1, 1]; periodic with <paramref name="period"/> lattice cells when it is above 0.</summary>
        static float Noise(float x, int salt, int period = 0)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            int a = i, b = i + 1;
            if (period > 0)
            {
                a = ((a % period) + period) % period;
                b = ((b % period) + period) % period;
            }
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(a * 7919 + salt), Hash(b * 7919 + salt), u);
        }

        int Salt => seed * 131 + boilFrame * 977;

        // ------------------------------------------------------------------ mesh

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width < 1 || r.height < 1) return;
            switch (kind)
            {
                case InkKind.Panel:
                case InkKind.Disc:
                case InkKind.Ticket:
                case InkKind.Burst:
                    Outline(r, kind);
                    ClosedShape(vh, r, null);
                    break;
                case InkKind.Balloon:
                    BalloonShape(vh, r);
                    break;
                case InkKind.Strip:
                    StripShape(vh, r);
                    break;
                case InkKind.Ring:
                    RingShape(vh, r);
                    break;
                case InkKind.Stroke:
                    StrokeShape(vh, r);
                    break;
                case InkKind.Rays:
                    RaysShape(vh, r);
                    break;
            }
        }

        int Vert(VertexHelper vh, Vector2 p, Color c, float grainAmount, float dryness, Vector2 bristle)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            v.uv0 = new Vector4((p.x + seed * 97 % 512) / 512f, (p.y + seed * 61 % 512) / 512f, 0, 0);
            v.uv1 = new Vector4(grainAmount * grain * GrainScale, dryness, grunge, halftone);
            v.uv2 = bristle;
            vh.AddVert(v);
            return vh.currentVertCount - 1;
        }

        int Vert(VertexHelper vh, Vector2 p, Color c, float grainAmount) => Vert(vh, p, c, grainAmount, 0, new Vector2(0, 0.5f));

        /// <summary>Fills <see cref="Pts"/> and <see cref="Nrm"/> with the displaced outline of a closed shape.</summary>
        void Outline(Rect r, InkKind k)
        {
            Pts.Clear();
            switch (k)
            {
                case InkKind.Disc:
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt((r.width + r.height) * 0.12f), 24, 96);
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2f;
                        Pts.Add(r.center + new Vector2(Mathf.Cos(a) * r.width * 0.5f, Mathf.Sin(a) * r.height * 0.5f));
                    }
                    break;
                }
                case InkKind.Ticket:
                {
                    // zigzag bottom (left to right), straight right side, zigzag top (right to left), left side
                    int teeth = Mathf.Max(2, Mathf.RoundToInt(r.width / tooth));
                    float w = r.width / teeth, depth = tooth * 0.55f;
                    for (int i = 0; i <= teeth * 2; i++) Pts.Add(new Vector2(r.xMin + i * w * 0.5f, r.yMin + (i % 2 == 1 ? depth : 0)));
                    AddLine(new Vector2(r.xMax, r.yMin + depth), new Vector2(r.xMax, r.yMax - depth), 8f, false);
                    for (int i = teeth * 2; i >= 0; i--) Pts.Add(new Vector2(r.xMin + i * w * 0.5f, r.yMax - (i % 2 == 1 ? depth : 0)));
                    AddLine(new Vector2(r.xMin, r.yMax - depth), new Vector2(r.xMin, r.yMin + depth), 8f, false);
                    break;
                }
                case InkKind.Burst:
                {
                    int n = Mathf.Max(5, spikes);
                    for (int i = 0; i < n * 2; i++)
                    {
                        float a = (i + 0.5f * Hash(seed * 31 + i) * 0.3f) / (n * 2) * Mathf.PI * 2f;
                        float k2 = i % 2 == 0 ? 0.88f + 0.14f * Hash(seed * 17 + i) : 0.66f + 0.05f * Hash(seed * 23 + i);
                        Pts.Add(r.center + new Vector2(Mathf.Cos(a) * r.width * 0.5f * k2, Mathf.Sin(a) * r.height * 0.5f * k2));
                    }
                    break;
                }
                default:
                    RoundedRect(r, radius, null);
                    break;
            }
            Displace(r.center, k == InkKind.Burst || k == InkKind.Ticket ? 0.35f : 1f);
        }

        /// <summary>Appends a rounded rectangle, counter-clockwise from the bottom-left corner; optionally with a balloon tail.</summary>
        void RoundedRect(Rect r, float rad, System.Action<Vector2, Vector2> topEdgeHook)
        {
            rad = Mathf.Min(rad, r.width * 0.5f, r.height * 0.5f);
            const float step = 7f;
            int arc = Mathf.Max(2, Mathf.CeilToInt(rad * Mathf.PI * 0.5f / 4f));
            Corner(new Vector2(r.xMin + rad, r.yMin + rad), rad, 180f, arc);
            AddLine(new Vector2(r.xMin + rad, r.yMin), new Vector2(r.xMax - rad, r.yMin), step, true);
            Corner(new Vector2(r.xMax - rad, r.yMin + rad), rad, 270f, arc);
            AddLine(new Vector2(r.xMax, r.yMin + rad), new Vector2(r.xMax, r.yMax - rad), step, true);
            Corner(new Vector2(r.xMax - rad, r.yMax - rad), rad, 0f, arc);
            if (topEdgeHook != null) topEdgeHook(new Vector2(r.xMax - rad, r.yMax), new Vector2(r.xMin + rad, r.yMax));
            else AddLine(new Vector2(r.xMax - rad, r.yMax), new Vector2(r.xMin + rad, r.yMax), step, true);
            Corner(new Vector2(r.xMin + rad, r.yMax - rad), rad, 90f, arc);
            AddLine(new Vector2(r.xMin, r.yMax - rad), new Vector2(r.xMin, r.yMin + rad), step, true);
        }

        static void Corner(Vector2 c, float rad, float fromDeg, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = (fromDeg + 90f * i / n) * Mathf.Deg2Rad;
                Pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad);
            }
        }

        /// <summary>Points strictly between a and b (the ends belong to the neighbouring corners).</summary>
        static void AddLine(Vector2 a, Vector2 b, float step, bool interiorOnly)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int i = interiorOnly ? 1 : 0; i < (interiorOnly ? n : n + 1); i++) Pts.Add(Vector2.Lerp(a, b, i / (float)n));
        }

        /// <summary>Computes outward normals, then pushes every point along its normal by periodic noise.</summary>
        void Displace(Vector2 center, float amount)
        {
            int n = Pts.Count;
            Normals(center, true);
            float perimeter = 0;
            var s = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                s[i] = perimeter;
                perimeter += Vector2.Distance(Pts[i], Pts[(i + 1) % n]);
            }
            int coarse = Mathf.Max(3, Mathf.RoundToInt(perimeter / 36f));
            int fine = Mathf.Max(6, Mathf.RoundToInt(perimeter / 6f));
            for (int i = 0; i < n; i++)
            {
                float u = s[i] / perimeter;
                float d = rough * Wobble * Noise(u * coarse, Salt, coarse) + tear * Ragged * Noise(u * fine, Salt + 5, fine);
                Pts[i] += Nrm[i] * d * amount;
            }
            Normals(center, true);
        }

        static void Normals(Vector2 center, bool closed)
        {
            Nrm.Clear();
            int n = Pts.Count;
            for (int i = 0; i < n; i++)
            {
                var prev = Pts[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)];
                var next = Pts[closed ? (i + 1) % n : Mathf.Min(n - 1, i + 1)];
                var t = (next - prev).normalized;
                var nn = new Vector2(t.y, -t.x);
                if (closed && Vector2.Dot(nn, Pts[i] - center) < 0) nn = -nn;
                Nrm.Add(nn);
            }
        }

        /// <summary>Shadow, fill (fan from the centre) and outline of the current closed outline.</summary>
        void ClosedShape(VertexHelper vh, Rect r, List<Vector2> fillOverride)
        {
            var center = r.center;
            var fillPts = fillOverride ?? Pts;
            if (shadow != Vector2.zero && (themed ? shadowAlpha : shadowColor.a) > 0.001f)
                Fan(vh, fillPts, center, shadow, Paint(shadowRole, shadowAlpha, shadowColor), 0f, true);
            if (hasFill) Fan(vh, fillPts, center, Vector2.zero, Paint(fillRole, fillAlpha, fillColor), 1f, line <= 0.01f);
            if (line > 0.01f) Loop(vh);
        }

        void Fan(VertexHelper vh, List<Vector2> pts, Vector2 center, Vector2 offset, Color c, float grainAmount, bool feather)
        {
            int n = pts.Count;
            int ci = Vert(vh, center + offset, c, grainAmount);
            int first = vh.currentVertCount;
            for (int i = 0; i < n; i++) Vert(vh, pts[i] + offset, c, grainAmount);
            for (int i = 0; i < n; i++) vh.AddTriangle(ci, first + i, first + (i + 1) % n);
            if (!feather) return;
            var clear = c;
            clear.a = 0;
            int fo = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                var dir = (pts[i] - center).normalized;
                Vert(vh, pts[i] + offset + dir * 1.1f, clear, grainAmount);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                vh.AddTriangle(first + i, fo + i, fo + j);
                vh.AddTriangle(first + i, fo + j, first + j);
            }
        }

        /// <summary>The ink outline along the current closed outline, its width varying like a brush pen.</summary>
        void Loop(VertexHelper vh)
        {
            int n = Pts.Count;
            Wid.Clear();
            float total = 0;
            for (int i = 0; i < n; i++) total += Vector2.Distance(Pts[i], Pts[(i + 1) % n]);
            int cells = Mathf.Max(3, Mathf.RoundToInt(total / 70f));
            float acc = 0;
            for (int i = 0; i < n; i++)
            {
                float w = line * (0.93f + 0.12f * Noise(acc / total * cells, Salt + 11, cells));
                Wid.Add(w);
                acc += Vector2.Distance(Pts[i], Pts[(i + 1) % n]);
            }
            Ribbon(vh, Pts, Nrm, Wid, Paint(lineRole, lineAlpha, lineColor), true, null, 0.25f);
        }

        /// <summary>
        /// A band along a path: centre line plus/minus half the width, with a 1 px feather on both sides.
        /// <paramref name="dryness"/> (per point) runs the brush dry; null = solid.
        /// </summary>
        void Ribbon(VertexHelper vh, List<Vector2> pts, List<Vector2> nrm, List<float> width, Color c, bool closed, List<float> dryness, float grainAmount)
        {
            int n = pts.Count;
            if (n < 2) return;
            var clear = c;
            clear.a = 0;
            int first = vh.currentVertCount;
            float along = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) along += Vector2.Distance(pts[i], pts[i - 1]);
                float hw = width[i] * 0.5f;
                float d = dryness != null ? dryness[i] : 0f;
                float bu = along / 260f;
                Vert(vh, pts[i] - nrm[i] * (hw + 1f), clear, grainAmount, d, new Vector2(bu, 0f));
                Vert(vh, pts[i] - nrm[i] * hw, c, grainAmount, d, new Vector2(bu, 0.05f));
                Vert(vh, pts[i] + nrm[i] * hw, c, grainAmount, d, new Vector2(bu, 0.95f));
                Vert(vh, pts[i] + nrm[i] * (hw + 1f), clear, grainAmount, d, new Vector2(bu, 1f));
            }
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int a = first + i * 4, b = first + ((i + 1) % n) * 4;
                for (int k = 0; k < 3; k++)
                {
                    vh.AddTriangle(a + k, b + k, b + k + 1);
                    vh.AddTriangle(a + k, b + k + 1, a + k + 1);
                }
            }
        }

        // ------------------------------------------------------------------ balloon

        void BalloonShape(VertexHelper vh, Rect r)
        {
            var tip = new Vector2(r.xMin + tail.x * r.width, r.yMin + tail.y * r.height);
            bool top = tail.y >= 1f;
            float rad = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            float half = tailWidth * 0.5f;
            float bx = Mathf.Clamp(tip.x, r.xMin + rad + half + 4f, r.xMax - rad - half - 4f);
            var b1 = new Vector2(bx + half, top ? r.yMax : r.yMin);
            var b0 = new Vector2(bx - half, top ? r.yMax : r.yMin);

            // one outline with the tail, displaced once; the fill reuses it (body without the tail + a tail fan)
            Pts.Clear();
            int tailFrom = 0, tailTo = 0;
            if (top)
                RoundedRect(r, rad, (from, to) =>
                {
                    AddLine(from, b1, 7f, true);
                    tailFrom = Pts.Count;
                    TailPath(b1, tip, b0);
                    tailTo = Pts.Count;
                    AddLine(b0, to, 7f, true);
                });
            else
            {
                int arc = Mathf.Max(2, Mathf.CeilToInt(rad * Mathf.PI * 0.5f / 4f));
                Corner(new Vector2(r.xMin + rad, r.yMin + rad), rad, 180f, arc);
                AddLine(new Vector2(r.xMin + rad, r.yMin), b0, 7f, true);
                tailFrom = Pts.Count;
                TailPath(b0, tip, b1);
                tailTo = Pts.Count;
                AddLine(b1, new Vector2(r.xMax - rad, r.yMin), 7f, true);
                Corner(new Vector2(r.xMax - rad, r.yMin + rad), rad, 270f, arc);
                AddLine(new Vector2(r.xMax, r.yMin + rad), new Vector2(r.xMax, r.yMax - rad), 7f, true);
                Corner(new Vector2(r.xMax - rad, r.yMax - rad), rad, 0f, arc);
                AddLine(new Vector2(r.xMax - rad, r.yMax), new Vector2(r.xMin + rad, r.yMax), 7f, true);
                Corner(new Vector2(r.xMin + rad, r.yMax - rad), rad, 90f, arc);
                AddLine(new Vector2(r.xMin, r.yMax - rad), new Vector2(r.xMin, r.yMin + rad), 7f, true);
            }
            Displace(r.center, 1f);

            var body = new List<Vector2>(Pts.Count);
            TailPts.Clear();
            for (int i = 0; i < Pts.Count; i++)
            {
                bool inTail = i >= tailFrom && i < tailTo;
                if (inTail) TailPts.Add(Pts[i]);
                if (!inTail || i == tailFrom || i == tailTo - 1) body.Add(Pts[i]);
            }
            var baseMid = TailPts.Count > 0 ? (TailPts[0] + TailPts[^1]) * 0.5f : r.center;

            var center = r.center;
            if (shadow != Vector2.zero && (themed ? shadowAlpha : shadowColor.a) > 0.001f)
            {
                var sc = Paint(shadowRole, shadowAlpha, shadowColor);
                Fan(vh, body, center, shadow, sc, 0f, true);
                TailFan(vh, baseMid, shadow, sc);
            }
            if (hasFill)
            {
                var fc = Paint(fillRole, fillAlpha, fillColor);
                Fan(vh, body, center, Vector2.zero, fc, 1f, line <= 0.01f);
                TailFan(vh, baseMid, Vector2.zero, fc);
            }
            if (line > 0.01f) Loop(vh);
        }

        static readonly List<Vector2> TailPts = new();

        /// <summary>A slightly bent run from one base point out to the tip and back to the other base point.</summary>
        void TailPath(Vector2 from, Vector2 tip, Vector2 to)
        {
            var mid = (from + to) * 0.5f;
            var bend = Vector2.Perpendicular(tip - mid).normalized * (tip - mid).magnitude * 0.12f;
            const int n = 7;
            for (int i = 0; i <= n; i++) Pts.Add(Bezier(from, Vector2.Lerp(from, tip, 0.55f) + bend, tip, i / (float)n));
            for (int i = 1; i <= n; i++) Pts.Add(Bezier(tip, Vector2.Lerp(to, tip, 0.55f) + bend, to, i / (float)n));
        }

        void TailFan(VertexHelper vh, Vector2 baseMid, Vector2 offset, Color c)
        {
            if (TailPts.Count < 3) return;
            int ci = Vert(vh, baseMid + offset, c, 1f);
            int first = vh.currentVertCount;
            foreach (var p in TailPts) Vert(vh, p + offset, c, 1f);
            for (int i = 0; i < TailPts.Count - 1; i++) vh.AddTriangle(ci, first + i, first + i + 1);
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);

        // ------------------------------------------------------------------ strips

        void StripShape(VertexHelper vh, Rect r)
        {
            float length = r.width * reveal;
            if (length < 2f) return;
            float h = r.height;
            int n = square ? 1 : Mathf.Max(6, Mathf.CeilToInt(length / 9f));
            float startCap = square ? 0f : Mathf.Min(h * 0.45f, r.width * 0.2f);
            float endTaper = square ? 0f : Mathf.Min(h * 0.9f, r.width * 0.25f);
            float dryStart = Mathf.Min(70f, r.width * 0.12f), dryEnd = Mathf.Min(110f, r.width * 0.18f);
            int cells = Mathf.Max(2, Mathf.RoundToInt(r.width / 140f));

            var top = new List<Vector2>(n + 1);
            var bot = new List<Vector2>(n + 1);
            var dryness = new List<float>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float x = length * i / n;
                float u = x / r.width;
                float profile = 1f;
                if (x < startCap) profile = Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(Mathf.Clamp01(x / startCap)));
                float toEnd = length - x;
                if (toEnd < endTaper) profile *= Mathf.Lerp(0.42f, 1f, Mathf.Pow(Mathf.Clamp01(toEnd / endTaper), 0.6f));
                float wob = square ? 0f : rough * Wobble, rag = square ? 0f : tear * Ragged;
                float mid = r.center.y + wob * 0.6f * Noise(u * cells, Salt + 3);
                float half = h * 0.5f * profile;
                float upper = half + wob * Noise(u * cells * 2.3f, Salt + 7) + rag * Noise(x / 5f, Salt + 9);
                float lower = half + wob * Noise(u * cells * 2.1f, Salt + 13) + rag * Noise(x / 5f, Salt + 17);
                top.Add(new Vector2(r.xMin + x, mid + upper));
                bot.Add(new Vector2(r.xMin + x, mid - lower));
                float d = square ? 0f : Mathf.Max(1f - x / dryStart, 1f - toEnd / dryEnd);
                dryness.Add(Mathf.Clamp01(d) * dry * DryScale);
            }

            if (shadow != Vector2.zero && (themed ? shadowAlpha : shadowColor.a) > 0.001f)
                Band(vh, top, bot, dryness, shadow, Paint(shadowRole, shadowAlpha, shadowColor), 0f);
            Band(vh, top, bot, dryness, Vector2.zero, Paint(fillRole, fillAlpha, fillColor), 0.6f);
        }

        /// <summary>Columns from bottom to top edge: feather, bottom, middle, top, feather.</summary>
        void Band(VertexHelper vh, List<Vector2> top, List<Vector2> bot, List<float> dryness, Vector2 offset, Color c, float grainAmount)
        {
            var clear = c;
            clear.a = 0;
            int first = vh.currentVertCount;
            for (int i = 0; i < top.Count; i++)
            {
                float bu = (top[i].x - top[0].x) / 300f;
                float d = dryness[i];
                Vert(vh, bot[i] + offset + Vector2.down * 1.1f, clear, grainAmount, d, new Vector2(bu, 0f));
                Vert(vh, bot[i] + offset, c, grainAmount, d, new Vector2(bu, 0.02f));
                Vert(vh, (top[i] + bot[i]) * 0.5f + offset, c, grainAmount, d, new Vector2(bu, 0.5f));
                Vert(vh, top[i] + offset, c, grainAmount, d, new Vector2(bu, 0.98f));
                Vert(vh, top[i] + offset + Vector2.up * 1.1f, clear, grainAmount, d, new Vector2(bu, 1f));
            }
            for (int i = 0; i < top.Count - 1; i++)
            {
                int a = first + i * 5, b = a + 5;
                for (int k = 0; k < 4; k++)
                {
                    vh.AddTriangle(a + k, b + k, b + k + 1);
                    vh.AddTriangle(a + k, b + k + 1, a + k + 1);
                }
            }
        }

        // ------------------------------------------------------------------ rays

        /// <summary>
        /// Half of each sector is a ray, so the background shows between them; layer two Rays turned by half a
        /// sector for a two-tone sunburst. Both long edges get a one-pixel feather.
        /// </summary>
        void RaysShape(VertexHelper vh, Rect r)
        {
            int n = Mathf.Max(3, spikes);
            float reach = r.size.magnitude * 0.5f + 2f;
            var c = Paint(fillRole, fillAlpha, fillColor);
            var clear = c;
            clear.a = 0;
            var center = r.center;
            float sector = Mathf.PI * 2f / n;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * sector, a1 = a0 + sector * 0.5f;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                var out0 = new Vector2(d0.y, -d0.x) * 1.2f;   // outside of the ray's clockwise edge
                var out1 = new Vector2(-d1.y, d1.x) * 1.2f;   // outside of its counter-clockwise edge
                int ci = Vert(vh, center, c, 0.5f);
                int p0 = Vert(vh, center + d0 * reach, c, 0.5f);
                int p1 = Vert(vh, center + d1 * reach, c, 0.5f);
                vh.AddTriangle(ci, p0, p1);
                int f0 = Vert(vh, center + d0 * reach + out0, clear, 0.5f);
                int f0c = Vert(vh, center + out0, clear, 0.5f);
                vh.AddTriangle(ci, p0, f0);
                vh.AddTriangle(ci, f0, f0c);
                int f1 = Vert(vh, center + d1 * reach + out1, clear, 0.5f);
                int f1c = Vert(vh, center + out1, clear, 0.5f);
                vh.AddTriangle(ci, p1, f1);
                vh.AddTriangle(ci, f1, f1c);
            }
        }

        // ------------------------------------------------------------------ rings and strokes

        void RingShape(VertexHelper vh, Rect r)
        {
            float drawn = Mathf.Clamp01(reveal);
            if (drawn <= 0.001f) return;
            int n = Mathf.Max(24, Mathf.CeilToInt(Mathf.Abs(sweep) / 5f));
            float rx = r.width * 0.5f - line, ry = r.height * 0.5f - line;
            Pts.Clear();
            Wid.Clear();
            int last = Mathf.Max(1, Mathf.CeilToInt(n * drawn));
            for (int i = 0; i <= last; i++)
            {
                float t = Mathf.Min(i / (float)n, drawn);
                float a = (startAngle + sweep * t) * Mathf.Deg2Rad;
                float k = 1f + 0.055f * (t - 0.5f) * 2f + rough * Wobble * 0.012f * Noise(t * 5f, Salt + 21);
                Pts.Add(r.center + new Vector2(Mathf.Cos(a) * rx * k, Mathf.Sin(a) * ry * k));
                Wid.Add(line * Mathf.Lerp(taper, 1f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.04f))));
            }
            Normals(r.center, false);
            Ribbon(vh, Pts, Nrm, Wid, Paint(fillRole, fillAlpha, fillColor), false, null, 0.2f);
        }

        static readonly List<Vector2> Path = new();

        void StrokeShape(VertexHelper vh, Rect r)
        {
            if (points == null || points.Length < 2) return;
            int start = 0;
            for (int i = 0; i <= points.Length; i++)
            {
                if (i < points.Length && !float.IsNaN(points[i].x)) continue;
                if (i - start >= 2) OneStroke(vh, r, start, i);
                start = i + 1;
            }
        }

        void OneStroke(VertexHelper vh, Rect r, int from, int to)
        {
            Path.Clear();
            for (int i = from; i < to; i++) Path.Add(r.min + Vector2.Scale(points[i], r.size));
            // Catmull-Rom, 6 points per segment
            Pts.Clear();
            for (int i = 0; i < Path.Count - 1; i++)
            {
                var p0 = Path[Mathf.Max(0, i - 1)];
                var p1 = Path[i];
                var p2 = Path[i + 1];
                var p3 = Path[Mathf.Min(Path.Count - 1, i + 2)];
                for (int k = 0; k < 6; k++)
                {
                    float t = k / 6f, t2 = t * t, t3 = t2 * t;
                    Pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            Pts.Add(Path[^1]);

            // cut at the revealed length
            float total = 0;
            for (int i = 1; i < Pts.Count; i++) total += Vector2.Distance(Pts[i], Pts[i - 1]);
            float keep = total * Mathf.Clamp01(reveal);
            if (keep < 1f) return;
            float acc = 0;
            int cut = Pts.Count;
            for (int i = 1; i < Pts.Count; i++)
            {
                float seg = Vector2.Distance(Pts[i], Pts[i - 1]);
                if (acc + seg >= keep)
                {
                    Pts[i] = Vector2.Lerp(Pts[i - 1], Pts[i], (keep - acc) / Mathf.Max(seg, 1e-4f));
                    cut = i + 1;
                    break;
                }
                acc += seg;
            }
            if (cut < Pts.Count) Pts.RemoveRange(cut, Pts.Count - cut);

            // wobble and pressure: thin at both ends of the full stroke
            Normals(Vector2.zero, false);
            Wid.Clear();
            acc = 0;
            for (int i = 0; i < Pts.Count; i++)
            {
                if (i > 0) acc += Vector2.Distance(Pts[i], Pts[i - 1]);
                float u = acc / Mathf.Max(total, 1f);
                Pts[i] += Nrm[i] * (rough * Wobble * 0.5f * Noise(acc / 30f, Salt + 31));
                float press = Mathf.Min(Mathf.SmoothStep(0, 1, u / 0.18f), Mathf.SmoothStep(0, 1, (1f - u) / 0.22f));
                Wid.Add(line * Mathf.Lerp(taper, 1f, press));
            }
            Normals(Vector2.zero, false);
            Ribbon(vh, Pts, Nrm, Wid, Paint(fillRole, fillAlpha, fillColor), false, null, 0.2f);
        }
    }
}
