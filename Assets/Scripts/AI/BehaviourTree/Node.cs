namespace ChaseGame.AI.BehaviourTree
{
    public abstract class Node
    {
        public abstract NodeStatus Tick(Blackboard bb);
    }
}
