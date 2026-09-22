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
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float eyeHeight = 1f;

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
            Transform best = null;
            float bestSqr = radius * radius;

            foreach (var cand in candidates)
            {
                float sqr = (cand.Position - from).sqrMagnitude;
                if (sqr > bestSqr)
                {
                    continue;
                }

                if (!hasLineOfSight(from, cand.Position))
                {
                    continue;
                }

                bestSqr = sqr;
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

            var candidates = CandidatesFor(bb.Self.Team, bb.Roster);
            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            var target = NearestVisible(eye, candidates, bb.Config.VisionRadius, HasLineOfSight);

            if (bb.Self.Team == Team.Chaser)
            {
                bb.CurrentTarget = target;
            }
            else
            {
                bb.NearestThreat = target;
            }
        }

        private bool HasLineOfSight(Vector3 from, Vector3 targetPos)
        {
            Vector3 to = (targetPos + Vector3.up * eyeHeight) - from;
            // Blocked if an obstacle sits between the eye and the target.
            return !Physics.Raycast(from, to.normalized, to.magnitude, obstacleMask);
        }
    }
}
