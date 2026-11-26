using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// The people at a train's doors. Before it comes they wait beside the screen doors, as Seoul platforms ask
    /// (on the painted queue lines either side, leaving the middle free). When the doors open, passengers step
    /// off and walk to the stairs, and then the people waiting get on and stand inside.
    /// </summary>
    public class TrainCrowd : MonoBehaviour
    {
        public StationTrain train;
        [Tooltip("On the platform in front of each screen door, facing the train.")]
        public Transform[] doors;
        [Tooltip("Just inside the car behind each door, facing back out.")]
        public Transform[] inside;
        [Tooltip("From the doors along the platform to the stairs up, where those getting off go.")]
        public Vector3[] toStairs;
        public int waiting = 7;
        public int gettingOff = 6;
        public int seed = 3;
        public float speed = 1.2f;

        readonly List<HumanRig> queue = new();
        readonly List<int> queueDoor = new();
        System.Random rng;
        bool done;

        void Start()
        {
            rng = new System.Random(seed);
            if (doors == null || doors.Length == 0) return;
            for (int i = 0; i < waiting; i++)
            {
                int d = rng.Next(doors.Length);
                var door = doors[d];
                // either side of the door, a step back, a little out of line
                float side = queueDoor.FindAll(x => x == d).Count % 2 == 0 ? -1f : 1f;
                int nth = queueDoor.FindAll(x => x == d).Count / 2;
                var pos = door.position + door.right * side * (0.62f + (float)rng.NextDouble() * 0.12f) - door.forward * (0.35f + nth * 0.6f);
                var rig = HumanBuilder.Build(transform, $"Waiting{i}", HumanLook.Random(rng, Crowd.Street), false, false);
                rig.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(door.forward) * Quaternion.Euler(0, side * -12f, 0));
                rig.phone = rng.NextDouble() < 0.6;
                queue.Add(rig);
                queueDoor.Add(d);
            }
        }

        void Update()
        {
            if (done || !train || !train.DoorsOpen) return;
            done = true;
            StartCoroutine(GetOff());
            StartCoroutine(GetOn());
        }

        IEnumerator GetOff()
        {
            if (inside == null || inside.Length == 0) yield break;
            for (int i = 0; i < gettingOff; i++)
            {
                int d = rng.Next(Mathf.Min(inside.Length, doors.Length));
                var rig = HumanBuilder.Build(transform, $"Leaving{i}", HumanLook.Random(rng, Crowd.Street), false, false);
                rig.transform.SetPositionAndRotation(inside[d].position, inside[d].rotation);
                StartCoroutine(Leave(rig, d));
                yield return new WaitForSeconds(0.35f + (float)rng.NextDouble() * 0.5f);
            }
        }

        IEnumerator Leave(HumanRig rig, int d)
        {
            var door = doors[d];
            yield return Walk(rig, door.position + door.forward * 0.1f);
            yield return Walk(rig, door.position - door.forward * 1.3f);
            // along the platform to the stairs, from the nearest point of the way out
            int start = 0;
            float best = float.MaxValue;
            for (int i = 0; i < (toStairs?.Length ?? 0); i++)
            {
                float dist = (toStairs[i] - rig.transform.position).sqrMagnitude;
                if (dist < best) { best = dist; start = i; }
            }
            for (int i = start; i < (toStairs?.Length ?? 0); i++)
                yield return Walk(rig, toStairs[i]);
            Destroy(rig.gameObject);
        }

        IEnumerator GetOn()
        {
            // let people off first
            yield return new WaitForSeconds(2.2f);
            for (int i = 0; i < queue.Count; i++)
            {
                StartCoroutine(Board(queue[i], queueDoor[i]));
                yield return new WaitForSeconds(0.4f + (float)rng.NextDouble() * 0.6f);
            }
        }

        IEnumerator Board(HumanRig rig, int d)
        {
            if (!rig) yield break;
            rig.phone = false;
            var door = doors[d];
            yield return Walk(rig, door.position);
            if (inside != null && d < inside.Length)
            {
                var spot = inside[d].position - inside[d].forward * (0.4f + (float)rng.NextDouble() * 0.7f) + inside[d].right * ((float)rng.NextDouble() - 0.5f) * 1.2f;
                yield return Walk(rig, spot);
                rig.transform.rotation = Quaternion.LookRotation(inside[d].right * (rng.Next(2) == 0 ? 1 : -1));
            }
            rig.phone = rng.NextDouble() < 0.5;
        }

        IEnumerator Walk(HumanRig rig, Vector3 target)
        {
            if (!rig) yield break;
            var t = rig.transform;
            target.y = t.position.y;
            while (rig && (t.position - target).sqrMagnitude > 0.0025f)
            {
                var dir = target - t.position;
                if (dir.sqrMagnitude > 0.0001f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
                rig.walkSpeed = speed;
                yield return null;
            }
            if (rig) rig.walkSpeed = 0;
        }
    }
}
