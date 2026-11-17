using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Runner behaviour (spec §8): flee threats (using escape skills), otherwise wander.
    // Rescue was dropped from the design — a jailed runner stays caged for the rest of
    // the match, so there is no branch that walks toward a jailed teammate.
    public sealed class RunnerAIBrain : AIBrain
    {
        private const float FleeExitMultiplier = 1.35f;
        private static readonly float[] FleeCandidateAngles = { 0f, 40f, -40f, 80f, -80f, 120f, -120f };

        public bool IsClone => Blackboard != null && Blackboard.IsClone;

        public void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f)
        {
            if (Blackboard == null)
            {
                return;
            }

            Blackboard.IsClone = true;
            Blackboard.CloneFleeAngleBias = fleeAngleBias;

            if (Blackboard.Self == null)
            {
                return;
            }

            Vector3 selfPos = Blackboard.Self.transform.position;
            float wanderRadius = Blackboard.Config != null ? Blackboard.Config.WanderRadius : 6f;
            Vector3 flatDir = new Vector3(initialMoveDirection.x, 0f, initialMoveDirection.z);
            if (flatDir.sqrMagnitude < 1e-4f)
            {
                flatDir = Blackboard.Self.transform.forward;
            }
            flatDir.Normalize();

            Vector3 candidate = selfPos + flatDir * wanderRadius;
            if (NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas) &&
                PlanarDistance(selfPos, hit.position) >= 1f)
            {
                Blackboard.Destination = hit.position;
                Blackboard.Self.MoveTo(hit.position);
            }
            else if (TryRandomPoint(selfPos, flatDir, wanderRadius, out var fallbackPoint))
            {
                Blackboard.Destination = fallbackPoint;
                Blackboard.Self.MoveTo(fallbackPoint);
            }
        }

        protected override Node BuildTree()
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(ThreatNear),
                    new ActionNode(EscapeAndFlee)),
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

            if (!bb.IsClone)
            {
                if (bb.Self.IsAbilityReady(0)) bb.Self.UseAbility(0, dir); // SpeedBoost
                if (bb.Self.IsAbilityReady(1)) bb.Self.UseAbility(1, dir); // Decoy
            }
            else if (Mathf.Abs(bb.CloneFleeAngleBias) > 1e-3f)
            {
                // Split off at an angle from the direct away vector so the clone
                // draws the chaser along a separate path from the real runner.
                dir = (Quaternion.Euler(0f, bb.CloneFleeAngleBias, 0f) * dir).normalized;
            }

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

    }
}
