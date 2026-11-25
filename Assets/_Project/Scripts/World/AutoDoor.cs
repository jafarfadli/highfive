using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// Automatic sliding glass doors (자동문): the two leaves slide apart when the player or anyone else
    /// comes near the sensor, and close again a moment after. The doorway keeps its own invisible
    /// blocker, so open doors never let the player walk out of the room.
    /// </summary>
    public class AutoDoor : MonoBehaviour
    {
        public Transform left, right;
        [Tooltip("How far each leaf slides, in metres along the door's local X.")]
        public float travel = 1.1f;
        public float radius = 1.9f;
        public float speed = 1.6f;

        Vector3 leftClosed, rightClosed;
        float open, holdUntil, nextScan;
        HumanRig[] people = new HumanRig[0];
        Transform listener;

        void Start()
        {
            if (left) leftClosed = left.localPosition;
            if (right) rightClosed = right.localPosition;
        }

        void Update()
        {
            if (Time.time > nextScan)
            {
                // a handful of people per room: a cheap scan a few times a second is enough
                nextScan = Time.time + 0.25f;
                people = FindObjectsByType<HumanRig>(FindObjectsInactive.Exclude);
                if (!listener && Camera.main) listener = Camera.main.transform;
            }
            var c = transform.position;
            bool near = listener && Flat(listener.position - c) < radius;
            foreach (var p in people)
                if (p && Flat(p.transform.position - c) < radius) { near = true; break; }
            if (near) holdUntil = Time.time + 1.2f;
            float target = Time.time < holdUntil ? 1f : 0f;
            open = Mathf.MoveTowards(open, target, Time.deltaTime * speed);
            float e = open * open * (3f - 2f * open);
            if (left) left.localPosition = leftClosed + Vector3.left * travel * e;
            if (right) right.localPosition = rightClosed + Vector3.right * travel * e;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
