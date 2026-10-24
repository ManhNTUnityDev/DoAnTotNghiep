using System.Collections.Generic;
using VContainer.Unity;
using ChaseGame.Match;
using ChaseGame.AI.BehaviourTree;
using UnityEngine;

namespace ChaseGame.AI
{
    // Team-level chaser coordination (spec §9): while runners sit in cages, pull the
    // nearest free chaser onto each cage as a guard so runners can't rescue them. The
    // assignment is written into each registered chaser's Blackboard.GuardPost; the
    // ChaserAIBrain reads it (below active hunting, above wandering). Recomputed each
    // tick, so a guard that leaves to chase a runner is replaced next tick.
    public sealed class ChaserCoordinator : ITickable
    {
        private readonly ICaptureService capture;
        private readonly List<Blackboard> chasers = new List<Blackboard>();

        public ChaserCoordinator(ICaptureService capture)
        {
            this.capture = capture;
        }

        // AI chaser brains register their blackboard at spawn. The player chaser has no
        // AIBrain/Blackboard, so it is naturally excluded from guard duty.
        public void Register(Blackboard chaser)
        {
            if (chaser != null && !chasers.Contains(chaser))
            {
                chasers.Add(chaser);
            }
        }

        public void Tick()
        {
            Assign(chasers, capture?.ActiveCages);
        }

        // Hysteresis factor (0.8^2 = 0.64) so an already-assigned guard doesn't swap
        // roles every frame with another chaser at nearly the same distance.
        private const float ExistingGuardSqrBias = 0.64f;

        // Pure: one guard per cage, preferring the nearest not-yet-claimed chaser that
        // is not actively chasing a runner (falling back to any unclaimed chaser if all
        // are busy). Everyone else is cleared (free to hunt/wander).
        public static void Assign(IReadOnlyList<Blackboard> chasers, IReadOnlyList<CageStub> cages)
        {
            if (chasers == null)
            {
                return;
            }

            var previousPosts = new Dictionary<Blackboard, Vector3?>(chasers.Count);
            foreach (var c in chasers)
            {
                if (c != null)
                {
                    previousPosts[c] = c.GuardPost;
                    c.GuardPost = null;
                }
            }

            if (cages == null || cages.Count == 0)
            {
                return;
            }

            var claimed = new HashSet<Blackboard>();
            foreach (var cage in cages)
            {
                Blackboard best = FindBestCandidate(chasers, cage.Position, claimed, previousPosts, onlyIdle: true)
                               ?? FindBestCandidate(chasers, cage.Position, claimed, previousPosts, onlyIdle: false);

                if (best != null)
                {
                    best.GuardPost = cage.Position;
                    claimed.Add(best);
                }
            }
        }

        private static Blackboard FindBestCandidate(
            IReadOnlyList<Blackboard> chasers,
            Vector3 cagePos,
            HashSet<Blackboard> claimed,
            Dictionary<Blackboard, Vector3?> previousPosts,
            bool onlyIdle)
        {
            Blackboard best = null;
            float bestScore = float.MaxValue;

            foreach (var c in chasers)
            {
                if (c?.Self == null || claimed.Contains(c))
                {
                    continue;
                }

                if (onlyIdle && c.CurrentTarget != null)
                {
                    continue;
                }

                float sqr = (c.Self.transform.position - cagePos).sqrMagnitude;
                if (previousPosts.TryGetValue(c, out var prev) &&
                    prev.HasValue &&
                    (prev.Value - cagePos).sqrMagnitude < 0.04f)
                {
                    sqr *= ExistingGuardSqrBias;
                }

                if (sqr < bestScore)
                {
                    bestScore = sqr;
                    best = c;
                }
            }

            return best;
        }
    }
}
