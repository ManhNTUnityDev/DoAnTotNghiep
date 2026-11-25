using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Actions
{
    // Chaser conditions and actions, shared by the BT and FSM chaser brains.
    public static class ChaserActions
    {
        public static bool HasTarget(Blackboard bb) => bb.CurrentTarget != null;

        public static bool CanShoot(Blackboard bb)
        {
            if (bb.CurrentTarget == null)
            {
                return false;
            }

            float range = bb.Config != null ? bb.Config.ShootRange : 8f;
            Vector3 a = bb.Self.transform.position;
            Vector3 b = bb.CurrentTarget.position;
            float planar = AIActions.PlanarDistance(a, b);

            return planar <= range && bb.Self.IsAbilityReady(0);
        }

        public static NodeStatus AimAndShoot(Blackboard bb)
        {
            bb.ActiveBehaviour = BehaviourId.Shoot;

            Vector3 targetPos = bb.CurrentTarget.position;
            Vector3 dir = targetPos - bb.Self.transform.position;
            dir.y = 0f;
            Vector3 aim = dir.sqrMagnitude > 1e-4f ? dir.normalized : bb.Self.transform.forward;

            // Keep pursuing and remember last known position while firing.
            bb.Destination = targetPos;
            bb.Self.MoveTo(targetPos);
            bb.Self.UseAbility(0, aim);
            return NodeStatus.Success;
        }

        public static NodeStatus ChaseTarget(Blackboard bb)
        {
            bb.ActiveBehaviour = BehaviourId.Chase;

            Vector3 targetPos = bb.CurrentTarget.position;
            // Store target position in Destination so if sight is lost next tick,
            // Wander smoothly finishes walking to the runner's last seen position
            // instead of snapping 180° back to a stale pre-chase wander point.
            bb.Destination = targetPos;

            if (bb.Self.IsAbilityReady(1))
            {
                Vector3 dir = targetPos - bb.Self.transform.position;
                dir.y = 0f;
                Vector3 aim = dir.sqrMagnitude > 1e-4f ? dir.normalized : bb.Self.transform.forward;
                bb.Self.UseAbility(1, aim); // SpeedBoost
            }

            bb.Self.MoveTo(targetPos);
            return NodeStatus.Running;
        }

        // Points to sweep around where a target was lost: evenly spaced on a circle, each
        // snapped to the NavMesh. Candidates off the mesh are dropped, so the list can be
        // shorter than count — or empty when nothing around is reachable.
        public static List<Vector3> GenerateSearchPoints(Vector3 center, int count, float radius)
        {
            var points = new List<Vector3>();
            if (count <= 0 || radius <= 0f)
            {
                return points;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = 360f * i / count;
                Vector3 candidate = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                if (NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    points.Add(hit.position);
                }
            }

            return points;
        }

        // Walks the search points in order. Returns Success once the last one is reached,
        // which is how a brain learns the sweep is over; Failure when there is nothing to sweep.
        public static NodeStatus SearchStep(Blackboard bb, IReadOnlyList<Vector3> points, ref int index)
        {
            bb.ActiveBehaviour = BehaviourId.Search;

            if (points == null || points.Count == 0)
            {
                return NodeStatus.Failure;
            }

            if (index >= points.Count)
            {
                return NodeStatus.Success;
            }

            Vector3 target = points[index];
            if (AIActions.PlanarDistance(bb.Self.transform.position, target) <= 1.5f)
            {
                index++;
                if (index >= points.Count)
                {
                    return NodeStatus.Success;
                }

                target = points[index];
            }

            bb.Destination = target;
            bb.Self.MoveTo(target);
            return NodeStatus.Running;
        }
    }
}
