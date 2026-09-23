using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Chaser behaviour (spec §8, chase + wander slice). Shooting/guarding branches
    // are added in later phases (abilities, coordinator).
    public sealed class ChaserAIBrain : AIBrain
    {
        protected override Node BuildTree()
        {
            return new Selector(
                // Shoot: target in range and the gun is ready → fire (captures free runners).
                new Sequence(
                    new ConditionNode(CanShoot),
                    new ActionNode(AimAndShoot)),
                // Hunt: if perception found a free runner, move to it.
                new Sequence(
                    new ConditionNode(bb => bb.CurrentTarget != null),
                    new ActionNode(ChaseTarget)),
                // Otherwise roam.
                new ActionNode(Wander));
        }

        private static bool CanShoot(Blackboard bb)
        {
            if (bb.CurrentTarget == null)
            {
                return false;
            }

            float range = bb.Config != null ? bb.Config.ShootRange : 8f;
            Vector3 a = bb.Self.transform.position;
            Vector3 b = bb.CurrentTarget.position;
            float planar = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            return planar <= range && bb.Self.IsAbilityReady(0);
        }

        private static NodeStatus AimAndShoot(Blackboard bb)
        {
            Vector3 dir = bb.CurrentTarget.position - bb.Self.transform.position;
            dir.y = 0f;
            Vector3 aim = dir.sqrMagnitude > 1e-4f ? dir.normalized : bb.Self.transform.forward;
            bb.Self.UseAbility(0, aim);
            return NodeStatus.Success;
        }

        private static NodeStatus ChaseTarget(Blackboard bb)
        {
            bb.Self.MoveTo(bb.CurrentTarget.position);
            return NodeStatus.Running;
        }

        private static NodeStatus Wander(Blackboard bb)
        {
            float reach = 1.5f;
            Vector3 self = bb.Self.transform.position;

            bool needNew = bb.Destination == null ||
                           Vector3.Distance(self, bb.Destination.Value) <= reach;

            if (needNew)
            {
                if (TryRandomPoint(self, bb.Config != null ? bb.Config.WanderRadius : 6f, out var point))
                {
                    bb.Destination = point;
                }
                else
                {
                    return NodeStatus.Failure; // no NavMesh nearby; nothing to do
                }
            }

            bb.Self.MoveTo(bb.Destination.Value);
            return NodeStatus.Running;
        }

        private static bool TryRandomPoint(Vector3 center, float radius, out Vector3 result)
        {
            Vector3 candidate = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(candidate, out var hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = center;
            return false;
        }
    }
}
