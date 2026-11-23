using UnityEngine;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Behaviour Tree runner: flee threats (using escape skills), otherwise wander.
    // Rescue was dropped from the design — a jailed runner stays caged for the rest of
    // the match, so there is no branch that walks toward a jailed teammate.
    public sealed class RunnerAIBrain : BehaviourTreeBrain
    {
        public bool IsClone => Blackboard != null && Blackboard.IsClone;

        public void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f)
        {
            RunnerActions.ConfigureAsClone(Blackboard, initialMoveDirection, fleeAngleBias);
        }

        protected override Node BuildTree()
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(RunnerActions.ThreatNear),
                    new ActionNode(RunnerActions.EscapeAndFlee)),
                new ActionNode(AIActions.Wander));
        }

        public static bool ThreatNear(Blackboard bb) => RunnerActions.ThreatNear(bb);
    }
}
