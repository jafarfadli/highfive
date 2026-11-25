using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>Keeps a graphic's colour on a palette role (and its alpha), so code can switch roles by name.</summary>
    [RequireComponent(typeof(Graphic))]
    public class Themed : MonoBehaviour
    {
        public Role role = Role.Ink;
        [Range(0, 1)] public float alpha = 1f;

        Graphic graphic;

        public static Themed On(Graphic g, Role role, float alpha = 1f)
        {
            var t = g.GetComponent<Themed>();
            if (!t) t = g.gameObject.AddComponent<Themed>();
            t.role = role;
            t.alpha = alpha;
            t.Apply();
            return t;
        }

        public void Set(Role newRole, float newAlpha = 1f)
        {
            role = newRole;
            alpha = newAlpha;
            Apply();
        }

        void OnEnable() => Apply();

        public void Apply()
        {
            if (!graphic) graphic = GetComponent<Graphic>();
            if (graphic) graphic.color = Theme.Get(role, alpha);
        }
    }
}
