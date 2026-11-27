using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Actions
{
    // Runner conditions and actions, shared by the BT and FSM runner brains.
    public static class RunnerActions
    {
        public const float FleeExitMultiplier = 1.35f;
        private static readonly float[] FleeCandidateAngles = { 0f, 40f, -40f, 80f, -80f, 120f, -120f };

        public static void ConfigureAsClone(Blackboard bb, Vector3 initialMoveDirection, float fleeAngleBias)
        {
            if (bb == null)
            {
                return;
            }

            bb.IsClone = true;
            bb.CloneFleeAngleBias = fleeAngleBias;

            if (bb.Self == null)
            {
                return;
            }

            Vector3 selfPos = bb.Self.transform.position;
            float wanderRadius = bb.Config != null ? bb.Config.WanderRadius : 6f;
            Vector3 flatDir = new Vector3(initialMoveDirection.x, 0f, initialMoveDirection.z);
            if (flatDir.sqrMagnitude < 1e-4f)
            {
                flatDir = bb.Self.transform.forward;
            }
            flatDir.Normalize();

            Vector3 candidate = selfPos + flatDir * wanderRadius;
            if (NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas) &&
                AIActions.PlanarDistance(selfPos, hit.position) >= 1f)
            {
                bb.Destination = hit.position;
                bb.Self.MoveTo(hit.position);
            }
            else if (AIActions.TryRandomPoint(selfPos, flatDir, wanderRadius, out var fallbackPoint))
            {
                bb.Destination = fallbackPoint;
                bb.Self.MoveTo(fallbackPoint);
            }
        }

        // Also maintains bb.IsFleeing, the hysteresis flag that widens the radius once fleeing
        // has begun. Called more than once per tick from different conditions, which is safe only
        // because the answer cannot change inside one tick: nothing moves between those calls, so
        // every call in a tick computes the same threshold and writes the same flag.
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
            bool near = AIActions.PlanarDistance(a, b) <= threshold;
            bb.IsFleeing = near;
            return near;
        }

        public static NodeStatus EscapeAndFlee(Blackboard bb)
        {
            bb.ActiveBehaviour = BehaviourId.Flee;

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

        public static Vector3 PickBestFleePoint(Vector3 selfPos, Vector3 threatPos, Vector3 awayDir, float fleeDist)
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
                    float stepDist = AIActions.PlanarDistance(selfPos, hit.position);
                    if (stepDist < 1f)
                    {
                        continue; // Avoid picking a point right at our feet when backed against a wall
                    }

                    float distFromThreat = AIActions.PlanarDistance(hit.position, threatPos);
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

        // Where to look for cover, relative to the runner: a spiral of distinct directions whose
        // distances sweep from half the radius out to the radius, so nearby cover is reachable
        // and not just a far dash. Pure geometry, so the spread itself is unit-tested.
        public static List<Vector3> CoverCandidateOffsets(int samples, float radius)
        {
            var offsets = new List<Vector3>();
            if (samples <= 0 || radius <= 0f)
            {
                return offsets;
            }

            for (int i = 0; i < samples; i++)
            {
                float angle = 360f * i / samples;
                float t = samples == 1 ? 1f : (float)i / (samples - 1);
                float distance = Mathf.Lerp(radius * 0.5f, radius, t);
                offsets.Add(Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance);
            }

            return offsets;
        }

        // A reachable spot near the runner that the threat cannot see, and that is no closer to
        // the threat than the runner already is. The nearest valid candidate wins, so the runner
        // commits to the cheapest hiding place rather than sprinting across the map.
        public static bool TryFindCoverPoint(
            Vector3 self, Vector3 threat, float radius, int samples, int mask, float eyeHeight,
            out Vector3 point)
        {
            point = self;
            if (samples <= 0 || radius <= 0f)
            {
                return false;
            }

            float eye = eyeHeight > 0f ? eyeHeight : 0.6f;
            Vector3 threatEye = threat + Vector3.up * eye;
            float currentGap = AIActions.PlanarDistance(self, threat);

            bool found = false;
            float bestDistance = float.MaxValue;

            foreach (var offset in CoverCandidateOffsets(samples, radius))
            {
                if (!NavMesh.SamplePosition(self + offset, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                if (AIActions.PlanarDistance(hit.position, threat) < currentGap)
                {
                    continue; // no safer than standing still
                }

                // Look past bodies: the runner's own collider usually sits between the threat
                // and the best-shielded candidate, and treating that as "visible" would throw
                // away exactly the candidate worth taking.
                if (Sensor.IsSightClearPastBodies(threatEye, hit.position + Vector3.up * eye, mask))
                {
                    continue; // the threat can still see it
                }

                float distance = AIActions.PlanarDistance(self, hit.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    point = hit.position;
                    found = true;
                }
            }

            return found;
        }

        // Head for a hiding place, using escape skills on the way exactly as fleeing does.
        // Precondition: bb.NearestThreat is set. Only reachable behind a condition that already
        // checked it, the same contract EscapeAndFlee and AimAndShoot are written to.
        public static NodeStatus TakeCover(Blackboard bb, Vector3 point)
        {
            bb.ActiveBehaviour = BehaviourId.Hide;
            bb.Destination = null;

            if (!bb.IsClone)
            {
                Vector3 away = bb.Self.transform.position - bb.NearestThreat.position;
                away.y = 0f;
                Vector3 dir = away.sqrMagnitude > 1e-4f ? away.normalized : bb.Self.transform.forward;
                if (bb.Self.IsAbilityReady(0)) bb.Self.UseAbility(0, dir); // SpeedBoost
                if (bb.Self.IsAbilityReady(1)) bb.Self.UseAbility(1, dir); // Decoy
            }

            bb.Self.MoveTo(point);
            return NodeStatus.Running;
        }
    }
}
