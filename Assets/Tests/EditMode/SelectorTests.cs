using System.Collections.Generic;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class SelectorTests
    {
        private static ActionNode Record(List<string> log, string name, NodeStatus status)
            => new ActionNode(_ => { log.Add(name); return status; });

        [Test]
        public void Selector_FirstSucceeds_ReturnsSuccess_ShortCircuits()
        {
            var log = new List<string>();
            var sel = new Selector(
                Record(log, "a", NodeStatus.Success),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Success, sel.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log); // b never ran
        }

        [Test]
        public void Selector_FirstFails_TriesNext()
        {
            var log = new List<string>();
            var sel = new Selector(
                Record(log, "a", NodeStatus.Failure),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Success, sel.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Selector_ChildRunning_ReturnsRunning_ShortCircuits()
        {
            var log = new List<string>();
            var sel = new Selector(
                Record(log, "a", NodeStatus.Running),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Running, sel.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log);
        }

        [Test]
        public void Selector_AllFail_ReturnsFailure()
        {
            var sel = new Selector(
                new ActionNode(_ => NodeStatus.Failure),
                new ActionNode(_ => NodeStatus.Failure));

            Assert.AreEqual(NodeStatus.Failure, sel.Tick(new Blackboard()));
        }

        [Test]
        public void Selector_NoChildren_ReturnsFailure()
        {
            var sel = new Selector();
            Assert.AreEqual(NodeStatus.Failure, sel.Tick(new Blackboard()));
        }
    }
}
