using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.AI.Fsm;

namespace ChaseGame.AI
{
    // Picks the brain component for a team and decision architecture. Adds it; the caller
    // still calls Initialize.
    public static class BrainFactory
    {
        public static AIBrain Add(GameObject go, Team team, BrainKind kind)
        {
            if (team == Team.Chaser)
            {
                return kind == BrainKind.Fsm
                    ? (AIBrain)go.AddComponent<ChaserFsmBrain>()
                    : go.AddComponent<ChaserAIBrain>();
            }

            return kind == BrainKind.Fsm
                ? (AIBrain)go.AddComponent<RunnerFsmBrain>()
                : go.AddComponent<RunnerAIBrain>();
        }

        // The architecture of the AI brain on go; BehaviourTree when it has none (a human player).
        public static BrainKind KindOf(GameObject go)
        {
            return go != null && go.TryGetComponent<AIBrain>(out var brain) ? brain.Kind : BrainKind.BehaviourTree;
        }
    }
}
