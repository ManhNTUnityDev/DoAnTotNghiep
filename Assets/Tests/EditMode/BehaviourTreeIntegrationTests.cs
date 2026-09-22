using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class BehaviourTreeIntegrationTests
    {
        // A tiny scratch bag so the test does not depend on Blackboard's real fields.
        private class Flags
        {
            public bool HighPriorityReady;
            public string LastAction;
        }

        private static Node BuildTree(Flags flags)
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(_ => flags.HighPriorityReady),
                    new ActionNode(_ => { flags.LastAction = "high"; return NodeStatus.Success; })),
                new ActionNode(_ => { flags.LastAction = "fallback"; return NodeStatus.Running; }));
        }

        [Test]
        public void Tree_TakesHighPriorityBranch_WhenConditionTrue()
        {
            var flags = new Flags { HighPriorityReady = true };
            var tree = BuildTree(flags);

            Assert.AreEqual(NodeStatus.Success, tree.Tick(new Blackboard()));
            Assert.AreEqual("high", flags.LastAction);
        }

        [Test]
        public void Tree_FallsThroughToWander_WhenConditionFalse()
        {
            var flags = new Flags { HighPriorityReady = false };
            var tree = BuildTree(flags);

            Assert.AreEqual(NodeStatus.Running, tree.Tick(new Blackboard()));
            Assert.AreEqual("fallback", flags.LastAction);
        }
    }
}
