using UnityEngine;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Behaviour Tree runner: when threatened, hide behind cover if there is any, otherwise
    // flee in the open; when safe, wander. Rescue was dropped from the design — a jailed
    // runner stays caged for the rest of the match.
    public sealed class RunnerAIBrain : BehaviourTreeBrain, IRunnerCloneBrain
    {
        public bool IsClone => Blackboard != null && Blackboard.IsClone;

        public void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f)
        {
            RunnerActions.ConfigureAsClone(Blackboard, initialMoveDirection, fleeAngleBias);
        }

        protected override Node BuildTree()
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(RunnerActions.ThreatNear),
                    new Selector(
                        new Sequence(
                            new ConditionNode(CanHide),
                            new ActionNode(HideInCover)),
                        new ActionNode(RunnerActions.EscapeAndFlee))),
                new ActionNode(AIActions.Wander));
        }

        public static bool ThreatNear(Blackboard bb) => RunnerActions.ThreatNear(bb);

        // A condition cannot hand a value to the action that follows it, so the hiding place
        // goes on the blackboard for HideInCover to pick up in the same tick.
        private static bool CanHide(Blackboard bb)
        {
            bb.CoverPoint = null;

            // A decoy clone's whole job is to be seen, so it never hides.
            if (bb.IsClone || bb.NearestThreat == null)
            {
                return false;
            }

            float radius = bb.Config != null ? bb.Config.CoverSearchRadius : 8f;
            int samples = bb.Config != null ? bb.Config.CoverSampleCount : 12;

            if (RunnerActions.TryFindCoverPoint(
                bb.Self.transform.position, bb.NearestThreat.position, radius, samples,
                bb.SightMask, bb.EyeHeight, out var point))
            {
                bb.CoverPoint = point;
                return true;
            }

            return false;
        }

        private static NodeStatus HideInCover(Blackboard bb) => RunnerActions.TakeCover(bb, bb.CoverPoint.Value);
    }
}
