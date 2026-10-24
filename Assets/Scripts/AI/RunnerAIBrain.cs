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
        private const float FleeExitMultiplier = 1.35f;
        private static readonly float[] FleeCandidateAngles = { 0f, 40f, -40f, 80f, -80f, 120f, -120f };

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

        public static bool ThreatNear(Blackboard bb)
        {
            if (bb.NearestThreat == null)
            {
                bb.IsFleeing = false;
                return false;
            }

            float baseRadius = bb.Config != null ? bb.Config.ThreatRadius : 6f;
            float threshold = bb.IsFleeing ? baseRadius * FleeExitMultiplier : baseRadius;

            Vector3 a = bb.Self.transform.position;
            Vector3 b = bb.NearestThreat.position;
            bool near = PlanarDistance(a, b) <= threshold;
            bb.IsFleeing = near;
            return near;
        }

        private static NodeStatus EscapeAndFlee(Blackboard bb)
        {
            // Clear any pre-flee wander destination so the runner never snaps back
            // toward the chaser once it escapes outside the threat radius.
            bb.Destination = null;

            Vector3 selfPos = bb.Self.transform.position;
            Vector3 threatPos = bb.NearestThreat.position;
            Vector3 away = selfPos - threatPos;
            away.y = 0f;
            Vector3 dir = away.sqrMagnitude > 1e-4f ? away.normalized : bb.Self.transform.forward;

            if (bb.Self.IsAbilityReady(0)) bb.Self.UseAbility(0, dir); // SpeedBoost
            if (bb.Self.IsAbilityReady(1)) bb.Self.UseAbility(1, dir); // Decoy

            float fleeDist = bb.Config != null ? bb.Config.WanderRadius : 6f;
            Vector3 flee = PickBestFleePoint(selfPos, threatPos, dir, fleeDist);

            bb.Self.MoveTo(flee);
            return NodeStatus.Running;
        }

        private static Vector3 PickBestFleePoint(Vector3 selfPos, Vector3 threatPos, Vector3 awayDir, float fleeDist)
        {
            Vector3 bestPoint = selfPos + awayDir * fleeDist;
            float bestScore = -1f;
            bool foundOnNavMesh = false;

            foreach (float angle in FleeCandidateAngles)
            {
                Vector3 candidateDir = Quaternion.Euler(0f, angle, 0f) * awayDir;
                Vector3 candidate = selfPos + candidateDir * fleeDist;

                if (NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    float stepDist = PlanarDistance(selfPos, hit.position);
                    if (stepDist < 1f)
                    {
                        continue; // Avoid picking a point right at our feet when backed against a wall
                    }

                    float distFromThreat = PlanarDistance(hit.position, threatPos);
                    // Slightly prefer straight-ahead away direction unless blocked
                    float anglePenalty = Mathf.Abs(angle) * 0.015f;
                    float score = distFromThreat - anglePenalty;

                    if (!foundOnNavMesh || score > bestScore)
                    {
                        bestScore = score;
                        bestPoint = hit.position;
                        foundOnNavMesh = true;
                    }
                }
            }

            return bestPoint;
        }

        public static bool HasJailedTeammate(Blackboard bb)
            => FindSafeRescueTarget(bb) != null;

        private static NodeStatus GoRescue(Blackboard bb)
        {
            Transform nearest = FindSafeRescueTarget(bb);
            if (nearest == null)
            {
                return NodeStatus.Failure;
            }

            bb.Destination = null;
            bb.Self.MoveTo(nearest.position);
            return NodeStatus.Running;
        }

        private static Transform FindSafeRescueTarget(Blackboard bb)
        {
            if (bb.Roster == null)
            {
                return null;
            }

            var jailed = bb.Roster.GetJailedRunners();
            if (jailed == null || jailed.Count == 0)
            {
                return null;
            }

            float rescueSafeRadius = bb.Config != null ? bb.Config.RescueThreatRadius : 5f;
            Vector3 self = bb.Self != null ? bb.Self.transform.position : Vector3.zero;
            Transform nearest = null;
            float bestSqr = float.MaxValue;

            foreach (var r in jailed)
            {
                if (r == null)
                {
                    continue;
                }

                Vector3 cagePos = r.transform.position;
                if (IsGuardedByThreat(bb, cagePos, rescueSafeRadius))
                {
                    continue;
                }

                float d = (cagePos - self).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    nearest = r.transform;
                }
            }

            return nearest;
        }

        private static bool IsGuardedByThreat(Blackboard bb, Vector3 targetPos, float safeRadius)
        {
            if (safeRadius <= 0f)
            {
                return false;
            }

            if (bb.NearestThreat != null &&
                PlanarDistance(bb.NearestThreat.position, targetPos) <= safeRadius)
            {
                return true;
            }

            if (bb.Roster?.Chasers != null)
            {
                foreach (var chaser in bb.Roster.Chasers)
                {
                    if (chaser != null && PlanarDistance(chaser.transform.position, targetPos) <= safeRadius)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
