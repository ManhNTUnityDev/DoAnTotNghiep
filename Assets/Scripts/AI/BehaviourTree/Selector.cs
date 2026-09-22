namespace ChaseGame.AI.BehaviourTree
{
    public sealed class Selector : Node
    {
        private readonly Node[] children;

        public Selector(params Node[] children)
        {
            this.children = children;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            foreach (var child in children)
            {
                var status = child.Tick(bb);
                if (status != NodeStatus.Failure)
                {
                    return status; // Success or Running short-circuits
                }
            }

            return NodeStatus.Failure;
        }
    }
}
