using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Actions
{
    // Actions and geometry helpers shared by every AI brain, whatever its decision
    // architecture (Behaviour Tree or FSM). Brains choose *which* action runs; the
    // actions themselves live here so both architectures execute identical code.
    public static class AIActions
    {
        // Shared roaming behaviour: keep heading to a random reachable point,
        // picking a new one on arrival. Fails when there is no NavMesh nearby.
        public static NodeStatus Wander(Blackboard bb)
        {
            bb.ActiveBehaviour = BehaviourId.Wander;

            const float reach = 1.5f;
            Vector3 self = bb.Self.transform.position;

            bool needNew = !bb.Destination.HasValue ||
                           PlanarDistance(self, bb.Destination.Value) <= reach;

            if (needNew)
            {
                float radius = bb.Config != null ? bb.Config.WanderRadius : 6f;
                if (TryRandomPoint(self, bb.Self.transform.forward, radius, out var point))
                {
                    bb.Destination = point;
                }
                else
                {
                    return NodeStatus.Failure;
                }
            }

            bb.Self.MoveTo(bb.Destination.Value);
            return NodeStatus.Running;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static bool TryRandomPoint(Vector3 center, Vector3 forwardHint, float radius, out Vector3 result)
        {
            float minDist = Mathf.Max(2.2f, radius * 0.45f);
            float maxDist = Mathf.Max(minDist + 0.5f, radius);

            Vector3 flatForward = new Vector3(forwardHint.x, 0f, forwardHint.z);
            bool hasForward = flatForward.sqrMagnitude > 1e-4f;
            if (hasForward)
            {
                flatForward.Normalize();
            }

            Vector3 fallbackHit = center;
            bool hasFallback = false;

            // Try forward-biased samples first for natural smooth roaming, then full 360° if near an edge/wall.
            for (int i = 0; i < 8; i++)
            {
                float dist = Random.Range(minDist, maxDist);
                Vector3 dir;
                if (hasForward && i < 5)
                {
                    float angle = Random.Range(-95f, 95f);
                    dir = Quaternion.Euler(0f, angle, 0f) * flatForward;
                }
                else
                {
                    Vector2 unit = Random.insideUnitCircle.normalized;
                    if (unit.sqrMagnitude < 1e-4f) unit = Vector2.up;
                    dir = new Vector3(unit.x, 0f, unit.y);
                }

                Vector3 candidate = center + dir * dist;
                if (NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    if (!hasFallback)
                    {
                        fallbackHit = hit.position;
                        hasFallback = true;
                    }

                    if (PlanarDistance(center, hit.position) >= minDist * 0.8f)
                    {
                        result = hit.position;
                        return true;
                    }
                }
            }

            if (hasFallback)
            {
                result = fallbackHit;
                return true;
            }

            result = center;
            return false;
        }
    }
}
