namespace ChaseGame.AI.BehaviourTree
{
    public sealed class Sequence : Node
    {
        private readonly Node[] children;

        public Sequence(params Node[] children)
        {
            this.children = children;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            foreach (var child in children)
            {
                var status = child.Tick(bb);
                if (status != NodeStatus.Success)
                {
                    return status; // Failure or Running short-circuits
                }
            }

            return NodeStatus.Success;
        }
    }
}
