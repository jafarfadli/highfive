using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// A baggage carousel's belt: the bags on it ride round its loop (in its local space), turning with the bends.
    /// The player's own suitcase rides along with them, its pick-up point with it; a bag taken off (switched off)
    /// simply drops out of the loop.
    /// </summary>
    public class CarouselBelt : MonoBehaviour
    {
        [Tooltip("The closed path the bags follow, in local space, at belt height.")]
        public Vector3[] loop;
        [Tooltip("The bags; each keeps the distance along the loop it starts at.")]
        public Transform[] bags;
        public float[] startAt;
        [Tooltip("Each bag's own turn on the belt (degrees), so they don't all lie square.")]
        public float[] yaw;
        public float speed = 0.38f;

        float[] cumulative;
        float length;
        float travelled;

        void Awake()
        {
            if (loop == null || loop.Length < 2) return;
            cumulative = new float[loop.Length + 1];
            for (int i = 0; i < loop.Length; i++)
            {
                cumulative[i + 1] = cumulative[i] + Vector3.Distance(loop[i], loop[(i + 1) % loop.Length]);
            }
            length = cumulative[loop.Length];
            Place();
        }

        void Update()
        {
            if (length <= 0) return;
            travelled += speed * Time.deltaTime;
            Place();
        }

        void Place()
        {
            if (bags == null) return;
            for (int b = 0; b < bags.Length; b++)
            {
                var bag = bags[b];
                if (!bag || !bag.gameObject.activeSelf) continue;
                float d = Mathf.Repeat((startAt != null && b < startAt.Length ? startAt[b] : 0f) + travelled, length);
                int i = 0;
                while (i < loop.Length - 1 && cumulative[i + 1] < d) i++;
                var a = loop[i];
                var c = loop[(i + 1) % loop.Length];
                float seg = cumulative[i + 1] - cumulative[i];
                var p = seg > 0 ? Vector3.Lerp(a, c, (d - cumulative[i]) / seg) : a;
                var dir = c - a;
                dir.y = 0;
                bag.localPosition = new Vector3(p.x, bag.localPosition.y, p.z);
                if (dir.sqrMagnitude > 0.0001f)
                    bag.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, yaw != null && b < yaw.Length ? yaw[b] : 0f, 0);
            }
        }
    }
}
