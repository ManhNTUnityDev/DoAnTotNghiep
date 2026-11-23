using UnityEngine;
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
    }
}
