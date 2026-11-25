using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// Lettering effect for display text: a thick ink outline (copies of the glyphs around a circle) and a
    /// hard offset shadow behind it, like the lettered titles of a comic. With <see cref="extrude"/> the
    /// letters become retro block lettering instead: a solid extruded block along the shadow direction,
    /// outlined in ink like the letters.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class InkText : BaseMeshEffect
    {
        public Role outlineRole = Role.Ink;
        [Range(0, 1)] public float outlineAlpha = 1f;
        [Tooltip("Outline thickness in canvas pixels (0 = none).")]
        public float outline = 4f;
        [Range(4, 16)] public int directions = 10;
        public Role shadowRole = Role.Ink;
        [Range(0, 1)] public float shadowAlpha = 1f;
        public Vector2 shadow = new(5, -5);
        [Tooltip("Retro block lettering: depth of the extruded block along the shadow direction, in canvas pixels (0 = a flat shadow).")]
        public float extrude;
        public Role extrudeRole = Role.BrushDeep;

        static readonly List<UIVertex> Source = new(1024), Result = new(16384);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            Source.Clear();
            vh.GetUIVertexStream(Source);
            if (Source.Count == 0) return;
            Result.Clear();

            float tint = graphic ? graphic.color.a : 1f;
            if (extrude > 0f)
            {
                // the block is drawn back to front: its ink outline, then its body, so every step joins up
                var dir = shadow != Vector2.zero ? shadow.normalized : new Vector2(0.6f, -0.8f);
                int steps = Mathf.Max(2, Mathf.CeilToInt(extrude / 1.5f));
                var ink = Theme.Get(outlineRole, outlineAlpha * tint);
                var body = Theme.Get(extrudeRole, tint);
                if (outline > 0f)
                    for (int s = steps; s >= 1; s--)
                        for (int d = 0; d < directions; d++)
                        {
                            float a = d / (float)directions * Mathf.PI * 2f;
                            Copy(dir * (extrude * s / steps) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outline, ink);
                        }
                for (int s = steps; s >= 1; s--) Copy(dir * (extrude * s / steps), body);
            }
            else if (shadow != Vector2.zero && shadowAlpha > 0f)
            {
                var c = Theme.Get(shadowRole, shadowAlpha * tint);
                // the shadow is the outlined shape moved, so it reads as a solid block behind the letters
                Copy(shadow, c);
                if (outline > 0f)
                    for (int d = 0; d < directions; d++)
                    {
                        float a = d / (float)directions * Mathf.PI * 2f;
                        Copy(shadow + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outline, c);
                    }
            }
            if (outline > 0f && outlineAlpha > 0f)
            {
                var c = Theme.Get(outlineRole, outlineAlpha * tint);
                for (int d = 0; d < directions; d++)
                {
                    float a = d / (float)directions * Mathf.PI * 2f;
                    Copy(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outline, c);
                }
            }
            Result.AddRange(Source);
            vh.Clear();
            vh.AddUIVertexTriangleStream(Result);
        }

        static void Copy(Vector2 offset, Color32 color)
        {
            for (int i = 0; i < Source.Count; i++)
            {
                var v = Source[i];
                v.position += (Vector3)offset;
                var c = color;
                c.a = (byte)(c.a * v.color.a / 255);
                v.color = c;
                Result.Add(v);
            }
        }
    }
}
