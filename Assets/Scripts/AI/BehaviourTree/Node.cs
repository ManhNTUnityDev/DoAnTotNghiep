using System.Collections.Generic;

namespace ChaseGame.AI.BehaviourTree
{
    public abstract class Node
    {
        public abstract NodeStatus Tick(Blackboard bb);

        // Composite nodes expose their children so tree size can be measured.
        public virtual IReadOnlyList<Node> Children => System.Array.Empty<Node>();
    }
}
