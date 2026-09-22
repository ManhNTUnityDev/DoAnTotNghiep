using System;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class BehaviourTreeLeafTests
    {
        [Test]
        public void ConditionNode_TruePredicate_ReturnsSuccess()
        {
            var node = new ConditionNode(_ => true);
            Assert.AreEqual(NodeStatus.Success, node.Tick(new Blackboard()));
        }

        [Test]
        public void ConditionNode_FalsePredicate_ReturnsFailure()
        {
            var node = new ConditionNode(_ => false);
            Assert.AreEqual(NodeStatus.Failure, node.Tick(new Blackboard()));
        }

        [Test]
        public void ActionNode_ReturnsDelegateStatus()
        {
            var node = new ActionNode(_ => NodeStatus.Running);
            Assert.AreEqual(NodeStatus.Running, node.Tick(new Blackboard()));
        }

        [Test]
        public void ActionNode_ReceivesBlackboard()
        {
            Blackboard seen = null;
            var bb = new Blackboard();
            var node = new ActionNode(b => { seen = b; return NodeStatus.Success; });
            node.Tick(bb);
            Assert.AreSame(bb, seen);
        }
    }
}
