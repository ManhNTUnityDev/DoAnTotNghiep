using System.Collections.Generic;
using VContainer.Unity;
using ChaseGame.Match;
using ChaseGame.AI.BehaviourTree;

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

        // Pure: one guard per cage, the nearest not-yet-claimed chaser. Everyone else is
        // cleared (free to hunt/wander). Safe with null/empty inputs.
        public static void Assign(IReadOnlyList<Blackboard> chasers, IReadOnlyList<CageStub> cages)
        {
            if (chasers == null)
            {
                return;
            }

            foreach (var c in chasers)
            {
                c.GuardPost = null;
            }

            if (cages == null || cages.Count == 0)
            {
                return;
            }

            var claimed = new HashSet<Blackboard>();
            foreach (var cage in cages)
            {
                Blackboard best = null;
                float bestSqr = float.MaxValue;
                foreach (var c in chasers)
                {
                    if (c.Self == null || claimed.Contains(c))
                    {
                        continue;
                    }

                    float sqr = (c.Self.transform.position - cage.Position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = c;
                    }
                }

                if (best != null)
                {
                    best.GuardPost = cage.Position;
                    claimed.Add(best);
                }
            }
        }
    }
}
