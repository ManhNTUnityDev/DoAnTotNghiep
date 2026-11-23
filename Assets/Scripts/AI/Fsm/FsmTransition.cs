using System;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // One row of a transition table: from From (null = any state), when Condition holds, go to To.
    public sealed class FsmTransition
    {
        public readonly IFsmState From;
        public readonly Func<Blackboard, bool> Condition;
        public readonly IFsmState To;

        public FsmTransition(IFsmState from, Func<Blackboard, bool> condition, IFsmState to)
        {
            From = from;
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            To = to ?? throw new ArgumentNullException(nameof(to));
        }
    }
}
