using UnityEngine;
using UnityEngine.Rendering;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// The way inside a building: a glow-yellow ▼ with an ink edge, bobbing over the thing the current step waits
    /// for (the player's suitcase riding the carousel, the nearest gate lane, a train door, the counter called, the
    /// ticket machine), turned to face the player. Steps that test a choice (a lane, a customs exit, which stairs)
    /// get none, so reading the signs stays the lesson. Made at runtime while the thing is armed; it rides along with
    /// it (the suitcase on the belt).
    /// </summary>
    public class GuideMarker : MonoBehaviour
    {
        const float Size = 0.36f, Bob = 0.06f;

        float baseY;
        static Material glow, ink;

        public static GuideMarker Attach(Component target)
        {
            var go = new GameObject("GuideMarker");
            go.transform.SetParent(target.transform, false);
            var m = go.AddComponent<GuideMarker>();
            m.Place(target);
            return m;
        }

        void Place(Component target)
        {
            // over the thing itself where it is small and has a body of its own (the suitcase the waypoint rides
            // on), otherwise over the box that stands for it (a lane's reader, a counter's trigger), never far
            // above head height
            var col = target.GetComponent<Collider>();
            var b = col ? col.bounds : new Bounds(target.transform.position, Vector3.one);
            var body = target.transform.parent;
            if (body && body.GetComponent<Renderer>() == null)
            {
                Bounds? rb = null;
                foreach (var r in body.GetComponentsInChildren<Renderer>())
                {
                    if (r.transform.IsChildOf(target.transform)) continue;
                    if (rb == null) rb = r.bounds;
                    else { var e = rb.Value; e.Encapsulate(r.bounds); rb = e; }
                }
                if (rb != null && rb.Value.size.magnitude < 2.5f) b = rb.Value;
            }
            float y = Mathf.Min(b.max.y, b.min.y + 1.9f) + 0.35f;
            transform.position = new Vector3(b.center.x, y, b.center.z);
            baseY = transform.localPosition.y;

            // the glow lit from within (an emissive material every scene already uses), the edge in ink
            if (!glow) glow = Mats.Emissive(new Color(0.914f, 0.706f, 0.271f), 1.3f);
            if (!ink) ink = Mats.Lit(new Color(0.102f, 0.114f, 0.149f));
            Triangle("Edge", Size + 0.08f, 0.012f, ink);
            Triangle("Face", Size, 0f, glow);
        }

        void Triangle(string name, float size, float back, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0, back);
            float h = size * 0.9f;
            // one face, towards the viewer (the marker turns to face the camera, its +z away from it): a face
            // drawn both ways would share its normals with its back and light as black
            var mesh = new Mesh { name = "GuideTriangle" };
            mesh.vertices = new[] { new Vector3(-size / 2f, h / 2f, 0), new Vector3(size / 2f, h / 2f, 0), new Vector3(0, -h / 2f, 0) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void LateUpdate()
        {
            var p = transform.localPosition;
            p.y = baseY + Mathf.Sin(Time.time * 2.6f) * Bob;
            transform.localPosition = p;
            var cam = Camera.main;
            if (!cam) return;
            // stand upright and face the viewer
            var to = cam.transform.position - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-to);
        }
    }
}
