using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// A field of print halftone dots that fades out across the rect: full dots up to <see cref="fadeStart"/>,
    /// then the dots shrink and thin out to nothing at the right edge. Drawn with the UI Ink material, so the
    /// dots sit on the same 45° screen as every other print surface. Used behind the Home title, so the
    /// street stays clear where the UI isn't.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class HalftoneField : MaskableGraphic
    {
        public Role role = Role.Ink;
        [Range(0, 1)] public float alpha = 0.7f;
        [Range(0.01f, 1)] public float density = 0.45f;
        [Tooltip("Where the fade begins, 0..1 across the rect from the left.")]
        [Range(0, 1)] public float fadeStart = 0.5f;

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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width < 1 || r.height < 1) return;
            var c = Theme.Get(role, alpha) * color;
            var clear = c;
            clear.a = 0;
            float x = Mathf.Lerp(r.xMin, r.xMax, fadeStart);
            Quad(vh, r.xMin, x, r.yMin, r.yMax, c, c, density, density);
            // the halftone branch treats a density of 0 as a solid fill, so the far edge keeps tiny dots and
            // fades its alpha instead
            Quad(vh, x, r.xMax, r.yMin, r.yMax, c, clear, density, 0.02f);
        }

        static void Quad(VertexHelper vh, float x0, float x1, float y0, float y1, Color c0, Color c1, float d0, float d1)
        {
            int i = vh.currentVertCount;
            Vert(vh, new Vector2(x0, y0), c0, d0);
            Vert(vh, new Vector2(x0, y1), c0, d0);
            Vert(vh, new Vector2(x1, y1), c1, d1);
            Vert(vh, new Vector2(x1, y0), c1, d1);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        static void Vert(VertexHelper vh, Vector2 p, Color c, float density)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            v.uv0 = new Vector4(p.x / 512f, p.y / 512f, 0, 0);
            v.uv1 = new Vector4(0, 0, 0, density);
            v.uv2 = new Vector2(0, 0.5f);
            vh.AddVert(v);
        }
    }
}
