using System.Collections;
using System.Collections.Generic;
using SeoulFirstWeek.World;
using UnityEngine;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// Other visitors: they wait in the chairs, walk up when their number is called,
    /// leave, and new people keep arriving. This is the queue pressure behind the player.
    /// </summary>
    public class QueueCrowd : MonoBehaviour
    {
        public Transform[] seats;
        public Transform[] counterSpots;
        public Transform entrance;
        public Transform machine;
        public float aisleZ;
        public float[] gaps = { 0f };
        public int initialSeated = 9;
        [Tooltip("Who the visitors are: locals at the clinic and bank, mostly foreign residents at immigration.")]
        public Crowd crowd = Crowd.Street;
        public int seed = 5;
        [Tooltip("Visitors sit down at the counter (low bank and office counters) instead of standing.")]
        public bool sitAtCounter;
        [Tooltip("Top of the chair seats, in metres.")]
        public float seatHeight = 0.44f;
        [Tooltip("Walk-in places (a clinic): new arrivals register at this counter spot before they sit down. Set by the director.")]
        public Transform reception;

        class Visitor
        {
            public HumanRig Rig;
            public int Seat = -1;
            public bool Busy;
            public float Seed;
        }

        readonly List<Visitor> visitors = new();
        readonly HashSet<int> taken = new();
        System.Random rng;

        public int Waiting
        {
            get
            {
                int n = 0;
                foreach (var v in visitors) if (!v.Busy && v.Seat >= 0) n++;
                return n;
            }
        }

        void Start()
        {
            rng = new System.Random(seed);
            for (int i = 0; i < initialSeated && i < seats.Length; i++)
            {
                var v = Spawn();
                var s = FreeSeat();
                if (s < 0) break;
                Sit(v, s);
            }
            StartCoroutine(Arrivals());
        }

        Visitor Spawn()
        {
            var rig = HumanBuilder.Build(transform, "Visitor", HumanLook.Random(rng, crowd));
            rig.seatHeight = seatHeight;
            var v = new Visitor { Rig = rig, Seed = (float)rng.NextDouble() * 100f };
            visitors.Add(v);
            return v;
        }

        int FreeSeat()
        {
            var free = new List<int>();
            for (int i = 0; i < seats.Length; i++) if (!taken.Contains(i)) free.Add(i);
            return free.Count == 0 ? -1 : free[rng.Next(free.Count)];
        }

        void Sit(Visitor v, int seat)
        {
            v.Seat = seat;
            taken.Add(seat);
            v.Rig.transform.SetPositionAndRotation(seats[seat].position, seats[seat].rotation);
            v.Rig.sitting = true;
            v.Rig.phone = rng.NextDouble() < 0.45;
            v.Rig.walkSpeed = 0;
        }

        /// <summary>A waiting visitor walks to the given counter (1-based) when their number is called.</summary>
        public void CallTo(int counter)
        {
            var candidates = visitors.FindAll(x => !x.Busy && x.Seat >= 0);
            if (candidates.Count == 0 || counterSpots.Length == 0) return;
            var v = candidates[rng.Next(candidates.Count)];
            var spot = counterSpots[Mathf.Clamp(counter - 1, 0, counterSpots.Length - 1)];
            StartCoroutine(Visit(v, spot));
        }

        IEnumerator Visit(Visitor v, Transform spot)
        {
            v.Busy = true;
            taken.Remove(v.Seat);
            var seatPos = seats[v.Seat].position;
            v.Seat = -1;
            v.Rig.sitting = false;
            v.Rig.phone = false;
            yield return new WaitForSeconds(0.6f);
            float gap = NearestGap(seatPos.x);
            yield return Walk(v.Rig, new Vector3(seatPos.x, 0, seatPos.z + 0.65f));
            yield return Walk(v.Rig, new Vector3(gap, 0, seatPos.z + 0.65f));
            yield return Walk(v.Rig, new Vector3(gap, 0, aisleZ));
            yield return Walk(v.Rig, new Vector3(spot.position.x, 0, aisleZ));
            yield return Walk(v.Rig, spot.position);
            v.Rig.transform.rotation = spot.rotation;
            v.Rig.sitting = sitAtCounter;
            float until = Time.time + 9f + (float)rng.NextDouble() * 8f;
            while (Time.time < until)
            {
                v.Rig.talking = Mathf.PerlinNoise(Time.time * 0.7f, v.Seed) > 0.55f;
                yield return null;
            }
            v.Rig.talking = false;
            v.Rig.sitting = false;
            yield return Walk(v.Rig, new Vector3(spot.position.x, 0, aisleZ));
            float exitGap = NearestGap(entrance.position.x);
            yield return Walk(v.Rig, new Vector3(exitGap, 0, aisleZ));
            yield return Walk(v.Rig, new Vector3(exitGap, 0, entrance.position.z + 1.2f));
            yield return Walk(v.Rig, entrance.position);
            visitors.Remove(v);
            Destroy(v.Rig.gameObject);
        }

        /// <summary>
        /// Walk-in places: a waiting patient answers a name call, walks to the treatment-room door and goes
        /// in. A while later the patient before them comes out, pays at <paramref name="payAt"/> and leaves.
        /// </summary>
        public void CallToRoom(Transform door, Transform payAt)
        {
            var candidates = visitors.FindAll(x => !x.Busy && x.Seat >= 0);
            if (candidates.Count > 0) StartCoroutine(GoIn(candidates[rng.Next(candidates.Count)], door));
            if (payAt) StartCoroutine(ComeOut(door, payAt, 3f + (float)rng.NextDouble() * 5f));
        }

        IEnumerator GoIn(Visitor v, Transform door)
        {
            v.Busy = true;
            taken.Remove(v.Seat);
            var seatPos = seats[v.Seat].position;
            v.Seat = -1;
            v.Rig.sitting = false;
            v.Rig.phone = false;
            yield return new WaitForSeconds(0.6f);
            float gap = NearestGap(seatPos.x);
            yield return Walk(v.Rig, new Vector3(seatPos.x, 0, seatPos.z + 0.65f));
            yield return Walk(v.Rig, new Vector3(gap, 0, seatPos.z + 0.65f));
            yield return Walk(v.Rig, new Vector3(gap, 0, aisleZ));
            yield return Walk(v.Rig, new Vector3(door.position.x, 0, aisleZ));
            yield return Walk(v.Rig, door.position);
            visitors.Remove(v);
            if (v.Rig) Destroy(v.Rig.gameObject);
        }

        IEnumerator ComeOut(Transform door, Transform payAt, float delay)
        {
            yield return new WaitForSeconds(delay);
            var v = Spawn();
            v.Busy = true;
            v.Rig.transform.SetPositionAndRotation(door.position, door.rotation);
            yield return Walk(v.Rig, new Vector3(door.position.x, 0, aisleZ));
            yield return Walk(v.Rig, new Vector3(payAt.position.x, 0, aisleZ));
            yield return Walk(v.Rig, payAt.position);
            v.Rig.transform.rotation = payAt.rotation;
            float until = Time.time + 5f + (float)rng.NextDouble() * 4f;
            while (Time.time < until)
            {
                v.Rig.talking = Mathf.PerlinNoise(Time.time * 0.7f, v.Seed) > 0.55f;
                yield return null;
            }
            v.Rig.talking = false;
            yield return Walk(v.Rig, new Vector3(payAt.position.x, 0, aisleZ));
            float exitGap = NearestGap(entrance.position.x);
            yield return Walk(v.Rig, new Vector3(exitGap, 0, aisleZ));
            yield return Walk(v.Rig, new Vector3(exitGap, 0, entrance.position.z + 1.2f));
            yield return Walk(v.Rig, entrance.position);
            visitors.Remove(v);
            if (v.Rig) Destroy(v.Rig.gameObject);
        }

        IEnumerator Arrivals()
        {
            while (true)
            {
                yield return new WaitForSeconds(12f + (float)rng.NextDouble() * 14f);
                if (visitors.Count >= seats.Length) continue;
                var seat = FreeSeat();
                if (seat < 0) continue;
                taken.Add(seat);
                var v = Spawn();
                v.Busy = true;
                v.Rig.transform.position = entrance.position;
                if (machine)
                {
                    yield return Walk(v.Rig, machine.position);
                    yield return new WaitForSeconds(1.2f);
                }
                if (reception)
                {
                    // at a walk-in clinic everyone registers at the reception first
                    float eg = NearestGap(entrance.position.x);
                    yield return Walk(v.Rig, new Vector3(eg, 0, entrance.position.z + 1.2f));
                    yield return Walk(v.Rig, new Vector3(eg, 0, aisleZ));
                    yield return Walk(v.Rig, new Vector3(reception.position.x, 0, aisleZ));
                    yield return Walk(v.Rig, reception.position);
                    v.Rig.transform.rotation = reception.rotation;
                    v.Rig.talking = true;
                    yield return new WaitForSeconds(4f + (float)rng.NextDouble() * 3f);
                    v.Rig.talking = false;
                    yield return Walk(v.Rig, new Vector3(reception.position.x, 0, aisleZ));
                }
                var sp = seats[seat].position;
                float g = NearestGap(sp.x);
                // from the reception or a kiosk by the counters people go back along the aisle and down a
                // lane; from a kiosk by the door, across the entrance and up a lane
                bool byCounters = reception || machine && Mathf.Abs(machine.position.z - aisleZ) < Mathf.Abs(machine.position.z - entrance.position.z);
                yield return Walk(v.Rig, new Vector3(g, 0, byCounters ? aisleZ : entrance.position.z + 1.2f));
                yield return Walk(v.Rig, new Vector3(g, 0, sp.z + 0.65f));
                yield return Walk(v.Rig, new Vector3(sp.x, 0, sp.z + 0.65f));
                yield return Walk(v.Rig, sp);
                taken.Remove(seat);
                Sit(v, seat);
                v.Busy = false;
            }
        }

        float NearestGap(float x)
        {
            float best = gaps[0];
            foreach (var g in gaps) if (Mathf.Abs(g - x) < Mathf.Abs(best - x)) best = g;
            return best;
        }

        static IEnumerator Walk(HumanRig rig, Vector3 target)
        {
            if (!rig) yield break;
            target.y = rig.transform.position.y;
            const float speed = 1.15f;
            while (rig && (rig.transform.position - target).sqrMagnitude > 0.01f)
            {
                var pos = rig.transform.position;
                var dir = target - pos;
                if (dir.sqrMagnitude > 0.0001f)
                    rig.transform.rotation = Quaternion.Slerp(rig.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                rig.transform.position = Vector3.MoveTowards(pos, target, speed * Time.deltaTime);
                rig.walkSpeed = speed;
                yield return null;
            }
            if (rig) rig.walkSpeed = 0;
        }
    }
}
