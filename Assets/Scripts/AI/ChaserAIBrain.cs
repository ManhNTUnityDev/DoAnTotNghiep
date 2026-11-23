using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Behaviour Tree chaser: shoot when a target is in range and the gun is ready,
    // otherwise chase a visible target, otherwise roam.
    public sealed class ChaserAIBrain : BehaviourTreeBrain
    {
        protected override Node BuildTree()
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(ChaserActions.CanShoot),
                    new ActionNode(ChaserActions.AimAndShoot)),
                new Sequence(
                    new ConditionNode(ChaserActions.HasTarget),
                    new ActionNode(ChaserActions.ChaseTarget)),
                new ActionNode(AIActions.Wander));
        }
    }
}
