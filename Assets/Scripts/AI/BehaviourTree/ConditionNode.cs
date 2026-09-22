using System;

namespace ChaseGame.AI.BehaviourTree
{
    public sealed class ConditionNode : Node
    {
        private readonly Func<Blackboard, bool> predicate;

        public ConditionNode(Func<Blackboard, bool> predicate)
        {
            this.predicate = predicate;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            return predicate(bb) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}
