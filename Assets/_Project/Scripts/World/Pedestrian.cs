using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SeoulFirstWeek.World
{
    public class Pedestrian : MonoBehaviour
    {
        public Vector3 home;
        public float radius = 100f;
        public float phoneChance = 0.2f;
        public static List<Vector3> Candidates;

        NavMeshAgent agent;
        HumanRig rig;
        float waitUntil;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rig = GetComponent<HumanRig>();
        }

        void Update()
        {
            if (!agent.isOnNavMesh) return;
            rig.walkSpeed = agent.velocity.magnitude;
            rig.phone = rig.walkSpeed < 0.2f && Time.time < waitUntil && phoneChance > 0.3f;
            if (agent.pathPending || agent.remainingDistance > 0.6f) return;
            if (waitUntil < 0)
            {
                waitUntil = Time.time + Random.Range(1f, 6f);
                return;
            }
            if (Time.time < waitUntil) return;
            waitUntil = -1;
            if (Candidates == null || Candidates.Count == 0) return;
            for (int i = 0; i < 6; i++)
            {
                var p = Candidates[Random.Range(0, Candidates.Count)];
                if ((p - transform.position).sqrMagnitude > 60f * 60f) continue;
                if (NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    break;
                }
            }
        }
    }
}
