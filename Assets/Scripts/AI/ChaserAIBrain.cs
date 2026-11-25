using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Behaviour Tree chaser: shoot when a target is in range and the gun is ready, otherwise
    // chase a visible target, otherwise sweep where the target was last seen, otherwise roam.
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
                new ActionNode(SearchBranch),
                new ActionNode(AIActions.Wander));
        }

        // A tree keeps nothing between ticks, so the sweep's bookkeeping happens here, after
        // the tree has run: remember whether a target was visible, and drop a stale sweep the
        // moment one is.
        protected override void Decide(Blackboard bb)
        {
            base.Decide(bb);

            bool hasTarget = ChaserActions.HasTarget(bb);
            if (hasTarget)
            {
                bb.SearchStartedAt = 0f;
            }

            bb.ChaserHadTarget = hasTarget;
        }

        // Returns Failure whenever no sweep applies, so the Selector falls through to Wander.
        private static NodeStatus SearchBranch(Blackboard bb)
        {
            if (ChaserActions.HasTarget(bb))
            {
                bb.SearchStartedAt = 0f;
                return NodeStatus.Failure;
            }

            // The tick a target disappears: walk to where it was last seen and plan a sweep.
            if (bb.SearchStartedAt <= 0f)
            {
                if (!bb.ChaserHadTarget || !bb.Destination.HasValue)
                {
                    return NodeStatus.Failure;
                }

                bb.SearchStartedAt = bb.Now;
                bb.SearchPoints = ChaserActions.GenerateSearchPoints(
                    bb.Destination.Value,
                    bb.Config != null ? bb.Config.SearchPointCount : 4,
                    bb.Config != null ? bb.Config.SearchRadius : 4f);
                bb.SearchIndex = 0;

                bb.ActiveBehaviour = BehaviourId.Search;
                bb.Self.MoveTo(bb.Destination.Value);
                return NodeStatus.Running;
            }

            float duration = bb.Config != null ? bb.Config.SearchDurationSeconds : 6f;
            if (bb.Now - bb.SearchStartedAt >= duration)
            {
                bb.SearchStartedAt = 0f;
                return NodeStatus.Failure;
            }

            int index = bb.SearchIndex;
            var status = ChaserActions.SearchStep(bb, bb.SearchPoints, ref index);
            bb.SearchIndex = index;

            if (status != NodeStatus.Running)
            {
                bb.SearchStartedAt = 0f; // swept everything, or nothing reachable to sweep
                return NodeStatus.Failure;
            }

            return NodeStatus.Running;
        }
    }
}
