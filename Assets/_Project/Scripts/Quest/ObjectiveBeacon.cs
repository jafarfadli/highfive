using UnityEngine;

namespace SeoulFirstWeek.Quest
{
    /// <summary>
    /// Light pillar standing on the pavement in front of an errand's door, with a pulsing
    /// ring on the ground. The next errand is bright, later ones are faint, finished ones vanish.
    /// </summary>
    public class ObjectiveBeacon : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public Renderer beam;
        public Renderer ring;
        public Color color = new(1f, 0.35f, 0.37f, 1f);

        MaterialPropertyBlock block;
        float strength = 1f;

        public void SetState(bool done, bool isNext)
        {
            if (beam) beam.enabled = !done;
            if (ring) ring.enabled = !done;
            strength = isNext ? 1f : 0.45f;
            Apply(1f);
        }

        void Update()
        {
            if (ring && ring.enabled) Apply(0.75f + 0.25f * Mathf.Sin(Time.time * 3f));
        }

        void Apply(float pulse)
        {
            block ??= new MaterialPropertyBlock();
            if (beam)
            {
                beam.GetPropertyBlock(block);
                block.SetColor(BaseColor, new Color(color.r, color.g, color.b, 0.22f * strength));
                beam.SetPropertyBlock(block);
            }
            if (ring)
            {
                ring.GetPropertyBlock(block);
                block.SetColor(BaseColor, new Color(color.r, color.g, color.b, 0.55f * strength * pulse));
                ring.SetPropertyBlock(block);
            }
        }
    }
}
