using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // AI brain whose decision is a Behaviour Tree, rebuilt from the root every tick.
    public abstract class BehaviourTreeBrain : AIBrain
    {
        public Node Root { get; private set; }

        public override BrainKind Kind => BrainKind.BehaviourTree;

        protected abstract Node BuildTree();

        protected override void OnBuild()
        {
            Root = BuildTree();
        }

        protected override void Decide(Blackboard bb)
        {
            Root.Tick(bb);
        }
    }
}
