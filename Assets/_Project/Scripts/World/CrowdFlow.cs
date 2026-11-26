using System.Collections;
using System.Collections.Generic;
using SeoulFirstWeek.Player;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// People passing through a place on set routes: commuters coming down from the street, tapping through the
    /// gates and going down to their train (and the other way), travellers walking from the plane to the queue
    /// lanes and on to baggage and customs, arrivals pulling their suitcases out to the buses. Each walker follows
    /// one route at a time and is hidden while off stage (on the stairs, through a door); where a route crosses a
    /// gate line it picks a lane near its way, taps and walks through as the flaps fold back. Walkers stop short of
    /// the player instead of walking through them. They are made when the scene starts, in the light version.
    /// </summary>
    public class CrowdFlow : MonoBehaviour
    {
        [System.Serializable]
        public class Route
        {
            [Tooltip("World positions, walked in order; the walker appears at the first and is hidden at the last.")]
            public Vector3[] points;
            [Tooltip("Seconds to stand at each point (empty, or one per point).")]
            public float[] waits;
            [Tooltip("The point in front of the gate line (-1: no gate). It and the next point take the x of a lane.")]
            public int gateAt = -1;
            [Tooltip("Walkers on this route pull a suitcase.")]
            public bool suitcase;
            [Tooltip("With a suitcase: the point from which they have it (picked up at the carousel).")]
            public int caseFrom;
            [Tooltip("Talks while standing at a point that has a wait (at a booth or a counter).")]
            public bool talkAtStops;
            public float weight = 1f;
        }

        public Route[] routes;
        [Tooltip("The gate lanes a route may cross, any order.")]
        public GateLane[] lanes;
        public int count = 8;
        public Crowd crowd = Crowd.Street;
        public int seed = 1;
        public float speed = 1.25f;
        [Tooltip("Seconds a walker stays off stage before setting off again (min, max).")]
        public Vector2 gap = new(2f, 9f);
        [Tooltip("Half the people look like travellers from abroad (an airport).")]
        public bool travellers;

        class Walker
        {
            public HumanRig Rig;
            public Renderer[] Renderers;
            public Transform Case;
            public float Speed;
        }

        static readonly Color[] CaseColors =
        {
            new(0.23f, 0.36f, 0.55f), new(0.72f, 0.23f, 0.23f), new(0.23f, 0.24f, 0.26f), new(0.79f, 0.64f, 0.15f),
            new(0.42f, 0.48f, 0.55f), new(0.55f, 0.23f, 0.42f), new(0.18f, 0.42f, 0.35f), new(0.85f, 0.85f, 0.82f),
        };

        System.Random rng;
        Transform player;
        float totalWeight;

        void Start()
        {
            if (routes == null || routes.Length == 0) return;
            rng = new System.Random(seed);
            foreach (var r in routes) totalWeight += Mathf.Max(0.01f, r.weight);
            var fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc) player = fpc.transform;
            for (int i = 0; i < count; i++)
            {
                var look = travellers && rng.Next(2) == 0 ? HumanLook.Abroad(rng, rng.Next(2) == 0) : HumanLook.Random(rng, crowd);
                // the bags of a light-version walker stay on: they are seen walking, not sitting
                var rig = HumanBuilder.Build(transform, $"Walker{i}", look, false, false);
                var w = new Walker
                {
                    Rig = rig,
                    Renderers = rig.GetComponentsInChildren<Renderer>(true),
                    Speed = speed * (0.88f + (float)rng.NextDouble() * 0.24f),
                };
                // the first round starts part-way along, so the place is busy from the start
                StartCoroutine(Life(w, (float)rng.NextDouble()));
            }
        }

        Route Pick()
        {
            double x = rng.NextDouble() * totalWeight;
            foreach (var r in routes)
            {
                x -= Mathf.Max(0.01f, r.weight);
                if (x <= 0) return r;
            }
            return routes[routes.Length - 1];
        }

        IEnumerator Life(Walker w, float startAt)
        {
            bool first = true;
            while (true)
            {
                var route = Pick();
                if (route.points == null || route.points.Length < 2)
                {
                    yield return null;
                    continue;
                }
                var pts = (Vector3[])route.points.Clone();
                GateLane lane = null;
                if (route.gateAt >= 0 && route.gateAt + 1 < pts.Length && lanes != null && lanes.Length > 0)
                {
                    lane = NearLane(pts[Mathf.Max(0, route.gateAt - 1)].x);
                    pts[route.gateAt].x = lane.Position.x;
                    pts[route.gateAt + 1].x = lane.Position.x;
                }
                int from = 0;
                if (first)
                {
                    // start somewhere along a route that has no gate ahead or already behind
                    from = Mathf.Clamp((int)(startAt * (pts.Length - 1)), 0, pts.Length - 2);
                    if (lane != null && from <= route.gateAt) from = 0;
                    first = false;
                    if (from == 0) yield return new WaitForSeconds(startAt * gap.y);
                }
                SetCase(w, route.suitcase && from >= route.caseFrom);
                Place(w, pts[from], pts[from + 1]);
                Show(w, true);
                for (int i = from + 1; i < pts.Length; i++)
                {
                    yield return Walk(w, pts[i]);
                    if (lane != null && i == route.gateAt)
                    {
                        // face the lane, tap the card and walk on as the flaps fold back
                        yield return Turn(w, pts[i + 1] - pts[i]);
                        yield return new WaitForSeconds(0.25f);
                        lane.Open();
                        yield return new WaitForSeconds(0.3f);
                    }
                    float wait = route.waits != null && i < route.waits.Length ? route.waits[i] : 0f;
                    if (wait > 0f)
                    {
                        w.Rig.talking = route.talkAtStops;
                        yield return new WaitForSeconds(wait * (0.8f + (float)rng.NextDouble() * 0.4f));
                        w.Rig.talking = false;
                    }
                    // the case comes off the belt once they have waited for it
                    if (route.suitcase && i == route.caseFrom && i > from) SetCase(w, true);
                }
                Show(w, false);
                yield return new WaitForSeconds(Mathf.Lerp(gap.x, gap.y, (float)rng.NextDouble()));
            }
        }

        GateLane NearLane(float x)
        {
            // one of the three lanes nearest the way the walker came
            var sorted = new List<GateLane>(lanes);
            sorted.RemoveAll(l => !l);
            sorted.Sort((a, b) => Mathf.Abs(a.Position.x - x).CompareTo(Mathf.Abs(b.Position.x - x)));
            return sorted[rng.Next(Mathf.Min(3, sorted.Count))];
        }

        static void Place(Walker w, Vector3 at, Vector3 next)
        {
            var dir = next - at;
            dir.y = 0;
            w.Rig.transform.SetPositionAndRotation(at, dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : w.Rig.transform.rotation);
        }

        static void Show(Walker w, bool on)
        {
            foreach (var r in w.Renderers) if (r) r.enabled = on;
            if (!on) w.Rig.walkSpeed = 0;
        }

        IEnumerator Turn(Walker w, Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) yield break;
            var goal = Quaternion.LookRotation(dir);
            while (Quaternion.Angle(w.Rig.transform.rotation, goal) > 3f)
            {
                w.Rig.transform.rotation = Quaternion.RotateTowards(w.Rig.transform.rotation, goal, 360f * Time.deltaTime);
                yield return null;
            }
        }

        IEnumerator Walk(Walker w, Vector3 target)
        {
            var t = w.Rig.transform;
            while ((Flat(t.position) - Flat(target)).sqrMagnitude > 0.0025f)
            {
                var dir = target - t.position;
                dir.y = 0;
                // stop short of the player rather than walk through them
                bool blocked = false;
                if (player)
                {
                    var toPlayer = player.position - t.position;
                    toPlayer.y = 0;
                    blocked = toPlayer.sqrMagnitude < 0.9f * 0.9f && Vector3.Dot(toPlayer.normalized, dir.normalized) > 0.5f;
                }
                if (dir.sqrMagnitude > 0.0001f)
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 7f);
                if (blocked)
                {
                    w.Rig.walkSpeed = 0;
                    yield return null;
                    continue;
                }
                var p = Vector3.MoveTowards(t.position, target, w.Speed * Time.deltaTime);
                t.position = p;
                w.Rig.walkSpeed = w.Speed;
                yield return null;
            }
            t.position = target;
            w.Rig.walkSpeed = 0;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        /// <summary>A hard-shell suitcase pulled behind the right hand, handle up, rolling on two wheels.</summary>
        void SetCase(Walker w, bool on)
        {
            if (on && !w.Case)
            {
                var g = new GameObject("Suitcase").transform;
                g.SetParent(w.Rig.transform, false);
                // behind and beside the right hand, tipped back onto its wheels
                g.localPosition = new Vector3(0.3f, 0f, -0.42f);
                g.localRotation = Quaternion.Euler(-24f, 0, 0);
                var shell = CaseColors[rng.Next(CaseColors.Length)];
                Prims.Box(g, "Shell", new Vector3(0, 0.36f, 0), new Vector3(0.4f, 0.6f, 0.25f), shell, false, Quaternion.identity);
                Prims.Box(g, "Rib", new Vector3(0, 0.36f, -0.128f), new Vector3(0.34f, 0.54f, 0.01f), Color.Lerp(shell, Color.black, 0.18f), false, Quaternion.identity);
                foreach (var sx in new[] { -0.11f, 0.11f })
                    Prims.Box(g, "Rod", new Vector3(sx, 0.86f, 0.1f), new Vector3(0.02f, 0.42f, 0.02f), new Color(0.17f, 0.18f, 0.2f), false, Quaternion.identity);
                Prims.Box(g, "Grip", new Vector3(0, 1.07f, 0.1f), new Vector3(0.26f, 0.03f, 0.04f), new Color(0.17f, 0.18f, 0.2f), false, Quaternion.identity);
                foreach (var sx in new[] { -0.15f, 0.15f })
                    Prims.Box(g, "Wheel", new Vector3(sx, 0.03f, 0.08f), new Vector3(0.04f, 0.06f, 0.06f), new Color(0.1f, 0.1f, 0.1f), false, Quaternion.identity);
                w.Case = g;
                w.Renderers = w.Rig.GetComponentsInChildren<Renderer>(true);
            }
            if (w.Case) w.Case.gameObject.SetActive(on);
            // the right arm rests out over the handle instead of swinging
            w.Rig.shoulderBag = on;
        }
    }
}
