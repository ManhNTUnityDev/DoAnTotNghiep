using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Runner behaviour (spec §8): flee threats (using escape skills), otherwise
    // approach a jailed teammate to rescue (contact-rescue lands with the real
    // JailSystem), otherwise wander.
    public sealed class RunnerAIBrain : AIBrain
    {
        protected override Node BuildTree()
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(ThreatNear),
                    new ActionNode(EscapeAndFlee)),
                new Sequence(
                    new ConditionNode(HasJailedTeammate),
                    new ActionNode(GoRescue)),
                new ActionNode(Wander));
        }

        private static bool ThreatNear(Blackboard bb)
        {
            if (bb.NearestThreat == null)
            {
                return false;
            }

            float r = bb.Config != null ? bb.Config.ThreatRadius : 6f;
            Vector3 a = bb.Self.transform.position;
            Vector3 b = bb.NearestThreat.position;
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) <= r;
        }

        private static NodeStatus EscapeAndFlee(Blackboard bb)
        {
            Vector3 away = bb.Self.transform.position - bb.NearestThreat.position;
            away.y = 0f;
            Vector3 dir = away.sqrMagnitude > 1e-4f ? away.normalized : bb.Self.transform.forward;

            if (bb.Self.IsAbilityReady(0)) bb.Self.UseAbility(0, dir); // SpeedBoost
            if (bb.Self.IsAbilityReady(1)) bb.Self.UseAbility(1, dir); // Decoy

            float fleeDist = bb.Config != null ? bb.Config.WanderRadius : 6f;
            Vector3 flee = bb.Self.transform.position + dir * fleeDist;
            if (NavMesh.SamplePosition(flee, out var hit, fleeDist, NavMesh.AllAreas))
            {
                flee = hit.position;
            }

            bb.Self.MoveTo(flee);
            return NodeStatus.Running;
        }

        private static bool HasJailedTeammate(Blackboard bb)
            => bb.Roster != null && bb.Roster.GetJailedRunners().Count > 0;

        private static NodeStatus GoRescue(Blackboard bb)
        {
            var jailed = bb.Roster.GetJailedRunners();
            Transform nearest = null;
            float best = float.MaxValue;
            Vector3 self = bb.Self.transform.position;
            foreach (var r in jailed)
            {
                float d = (r.transform.position - self).sqrMagnitude;
                if (d < best) { best = d; nearest = r.transform; }
            }

            if (nearest == null)
            {
                return NodeStatus.Failure;
            }

            bb.Self.MoveTo(nearest.position);
            return NodeStatus.Running;
        }
    }
}
