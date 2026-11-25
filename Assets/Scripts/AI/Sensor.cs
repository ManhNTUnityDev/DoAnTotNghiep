using System;
using System.Collections.Generic;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Perception: reads the roster, keeps the nearest line-of-sight candidate,
    // and writes it to the blackboard. Ticked by the brain (not every frame).
    public class Sensor : MonoBehaviour
    {
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float eyeHeight = 0.6f;

        private const float StickinessSqrBias = 0.64f;
        private const float RetainRadiusMultiplier = 1.15f;

        public readonly struct Candidate
        {
            public readonly Transform Transform;
            public readonly Vector3 Position;

            public Candidate(Transform transform, Vector3 position)
            {
                Transform = transform;
                Position = position;
            }
        }

        public static IReadOnlyList<Candidate> CandidatesFor(Team selfTeam, ITeamRoster roster)
        {
            var result = new List<Candidate>();
            var source = selfTeam == Team.Chaser ? roster.GetFreeRunners() : roster.Chasers;
            foreach (var c in source)
            {
                result.Add(new Candidate(c.transform, c.transform.position));
            }

            return result;
        }

        public static Transform NearestVisible(
            Vector3 from,
            IReadOnlyList<Candidate> candidates,
            float radius,
            Func<Vector3, Vector3, bool> hasLineOfSight)
        {
            return NearestVisible(from, candidates, radius, hasLineOfSight, currentSelection: null);
        }

        public static Transform NearestVisible(
            Vector3 from,
            IReadOnlyList<Candidate> candidates,
            float radius,
            Func<Vector3, Vector3, bool> hasLineOfSight,
            Transform currentSelection)
        {
            Transform best = null;
            float baseMaxSqr = radius * radius;
            float retainMaxSqr = (radius * RetainRadiusMultiplier) * (radius * RetainRadiusMultiplier);
            float bestScoreSqr = float.MaxValue;

            foreach (var cand in candidates)
            {
                bool isCurrent = currentSelection != null && cand.Transform == currentSelection;
                float allowedMaxSqr = isCurrent ? retainMaxSqr : baseMaxSqr;

                float sqr = (cand.Position - from).sqrMagnitude;
                if (sqr > allowedMaxSqr)
                {
                    continue;
                }

                float scoreSqr = isCurrent ? sqr * StickinessSqrBias : sqr;
                if (scoreSqr >= bestScoreSqr)
                {
                    continue;
                }

                if (!hasLineOfSight(from, cand.Position))
                {
                    continue;
                }

                bestScoreSqr = scoreSqr;
                best = cand.Transform;
            }

            return best;
        }

        public void Sense(Blackboard bb)
        {
            if (bb.Roster == null || bb.Self == null || bb.Config == null)
            {
                return;
            }

            var candidates = new List<Candidate>(CandidatesFor(bb.Self.Team, bb.Roster));

            // Chasers are also fooled by active decoy lures.
            if (bb.Self.Team == Team.Chaser)
            {
                foreach (var lure in DecoyLure.Active)
                {
                    if (lure != null)
                    {
                        candidates.Add(new Candidate(lure.transform, lure.transform.position));
                    }
                }
            }

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Transform previous = bb.Self.Team == Team.Chaser ? bb.CurrentTarget : bb.NearestThreat;
            var target = NearestVisible(eye, candidates, bb.Config.VisionRadius, HasLineOfSight, previous);

            if (bb.Self.Team == Team.Chaser)
            {
                bb.CurrentTarget = target;
            }
            else
            {
                bb.NearestThreat = target;
            }
        }

        public float EyeHeight => eyeHeight;

        public int ObstacleMask => obstacleMask.value != 0 ? obstacleMask.value : ~0;

        private bool HasLineOfSight(Vector3 from, Vector3 targetPos)
            => IsSightClear(from, targetPos + Vector3.up * eyeHeight, ObstacleMask);

        /// <summary>
        /// Whether map geometry leaves the line between two eye-height points clear. Characters
        /// and decoy lures never block sight, so only the map itself can hide anything.
        /// Shared with the runner's cover search, which asks the same question from the
        /// threat's point of view.
        /// </summary>
        /// <summary>
        /// Like <see cref="IsSightClear"/>, but it keeps going past characters and decoy lures
        /// instead of stopping at the first one. Perception can stop early — a body in the way
        /// means the target is roughly in the open either way — but the runner's cover search
        /// asks about a point it is standing next to, so its own collider is nearly always the
        /// first hit, and stopping there would call every shielded candidate visible.
        /// </summary>
        public static bool IsSightClearPastBodies(Vector3 fromEye, Vector3 toEye, int mask)
        {
            Vector3 to = toEye - fromEye;
            float dist = to.magnitude;
            const float bodyClearance = 0.45f;

            if (dist <= bodyClearance * 2f)
            {
                return true;
            }

            Vector3 dir = to / dist;
            if (mask == 0)
            {
                mask = ~0;
            }

            var hits = Physics.RaycastAll(
                fromEye + dir * bodyClearance,
                dir,
                dist - bodyClearance * 2f,
                mask,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Character>() != null ||
                    hit.collider.GetComponentInParent<DecoyLure>() != null)
                {
                    continue; // a body never blocks sight
                }

                return false; // real map geometry
            }

            return true;
        }

        public static bool IsSightClear(Vector3 fromEye, Vector3 toEye, int mask)
        {
            Vector3 to = toEye - fromEye;
            float dist = to.magnitude;
            const float bodyClearance = 0.45f;

            if (dist <= bodyClearance * 2f)
            {
                return true;
            }

            Vector3 dir = to / dist;
            if (mask == 0)
            {
                mask = ~0;
            }

            if (Physics.Raycast(
                fromEye + dir * bodyClearance,
                dir,
                out var hit,
                dist - bodyClearance * 2f,
                mask,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Character>() != null ||
                    hit.collider.GetComponentInParent<DecoyLure>() != null)
                {
                    return true;
                }

                return false;
            }

            return true;
        }
    }
}
