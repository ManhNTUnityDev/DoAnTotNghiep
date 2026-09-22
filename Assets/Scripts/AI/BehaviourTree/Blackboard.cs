using ChaseGame.Characters;

namespace ChaseGame.AI.BehaviourTree
{
    // Shared data bag passed to every node. Grows as later tasks add perception
    // and config fields; the core node types never read these directly — only the
    // delegates authored in a brain's BuildTree() do.
    public class Blackboard
    {
        public Character Self;
    }
}
