using UnityEngine;
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
                // Guard: hold the cage post the coordinator assigned (keeps rescuers away).
                new Sequence(
                    new ConditionNode(bb => bb.GuardPost.HasValue),
                    new ActionNode(GuardCage)),
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
            float planar = PlanarDistance(a, b);

            return planar <= range && bb.Self.IsAbilityReady(0);
        }

        private static NodeStatus AimAndShoot(Blackboard bb)
        {
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

        private static NodeStatus ChaseTarget(Blackboard bb)
        {
            Vector3 targetPos = bb.CurrentTarget.position;
            // Store target position in Destination so if sight is lost next tick,
            // Wander smoothly finishes walking to the runner's last seen position
            // instead of snapping 180° back to a stale pre-chase wander point.
            bb.Destination = targetPos;
            bb.Self.MoveTo(targetPos);
            return NodeStatus.Running;
        }

        private static NodeStatus GuardCage(Blackboard bb)
        {
            bb.Destination = null;
            bb.Self.MoveTo(bb.GuardPost.Value);
            return NodeStatus.Running;
        }
    }
}
