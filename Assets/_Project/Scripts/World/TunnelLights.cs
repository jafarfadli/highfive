using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// The tunnel going past a moving train's windows: lamps and wall ribs slide by along local X and wrap round,
    /// and the car sways a little on the track.
    /// </summary>
    public class TunnelLights : MonoBehaviour
    {
        [Tooltip("Things on the tunnel wall that pass the windows (lamps, ribs, cable trays), children spaced along X.")]
        public Transform[] passing;
        [Tooltip("How long a stretch of tunnel they cover before wrapping round (metres).")]
        public float span = 40f;
        public float speed = 16f;
        [Tooltip("The car body that sways (optional).")]
        public Transform car;
        public float sway = 0.006f;

        Vector3 carRest;

        void Awake()
        {
            if (car) carRest = car.localPosition;
        }

        void Update()
        {
            float dx = speed * Time.deltaTime;
            foreach (var p in passing)
            {
                if (!p) continue;
                var lp = p.localPosition;
                lp.x -= dx;
                if (lp.x < -span / 2f) lp.x += span;
                p.localPosition = lp;
            }
            if (car)
                car.localPosition = carRest + new Vector3(0, Mathf.Sin(Time.time * 7.1f) * sway * 0.5f, Mathf.Sin(Time.time * 2.3f) * sway);
        }
    }
}
