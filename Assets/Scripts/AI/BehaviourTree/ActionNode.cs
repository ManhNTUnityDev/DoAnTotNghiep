using System;

namespace ChaseGame.AI.BehaviourTree
{
    public sealed class ActionNode : Node
    {
        private readonly Func<Blackboard, NodeStatus> action;

        public ActionNode(Func<Blackboard, NodeStatus> action)
        {
            this.action = action;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            return action(bb);
        }
    }
}
