using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SeoulFirstWeek.World
{
    /// <summary>Spawns passers-by (the light build of <see cref="HumanBuilder"/>) on the baked NavMesh around the hub street.</summary>
    public class PedestrianSpawner : MonoBehaviour
    {
        public int count = 40;
        public float radius = 110f;
        public int seed = 42;

        [Tooltip("Optional walkable surface. When set, people only spawn and wander on top of it.")]
        public Collider surface;

        IEnumerator Start()
        {
            var rng = new System.Random(seed);
            // candidate points: street-level navmesh vertices near this spawner
            var tri = NavMesh.CalculateTriangulation();
            var candidates = new List<Vector3>();
            foreach (var v in tri.vertices)
                if (v.y < transform.position.y + 1.2f && (new Vector2(v.x - transform.position.x, v.z - transform.position.z)).magnitude < radius && OnSurface(v))
                    candidates.Add(v);
            if (candidates.Count == 0) yield break;
            Pedestrian.Candidates = candidates;
            for (int spawned = 0; spawned < count; spawned++)
            {
                var p = candidates[rng.Next(candidates.Count)];
                if (!NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas)) continue;
                var rig = HumanBuilder.Build(transform, $"Pedestrian_{spawned}", HumanLook.Random(rng), true, false);
                rig.transform.position = hit.position;
                var agent = rig.gameObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.28f;
                agent.height = 1.7f;
                agent.speed = 1.05f + (float)rng.NextDouble() * 0.5f;
                agent.angularSpeed = 300f;
                agent.acceleration = 4f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = 30 + rng.Next(40);
                agent.Warp(hit.position);
                var walker = rig.gameObject.AddComponent<Pedestrian>();
                walker.home = transform.position;
                walker.radius = radius;
                walker.phoneChance = (float)rng.NextDouble() * 0.4f;
                if (spawned % 5 == 0) yield return null;
            }
        }

        bool OnSurface(Vector3 v) =>
            !surface || surface.Raycast(new Ray(v + Vector3.up * 2f, Vector3.down), out _, 4f);
    }
}
