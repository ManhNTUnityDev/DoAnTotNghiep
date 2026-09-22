using System.Collections.Generic;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class SequenceTests
    {
        private static ActionNode Record(List<string> log, string name, NodeStatus status)
            => new ActionNode(_ => { log.Add(name); return status; });

        [Test]
        public void Sequence_AllSucceed_ReturnsSuccess_RunsAll()
        {
            var log = new List<string>();
            var seq = new Sequence(
                Record(log, "a", NodeStatus.Success),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Success, seq.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Sequence_FirstFails_ReturnsFailure_ShortCircuits()
        {
            var log = new List<string>();
            var seq = new Sequence(
                Record(log, "a", NodeStatus.Failure),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Failure, seq.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log); // b never ran
        }

        [Test]
        public void Sequence_ChildRunning_ReturnsRunning_ShortCircuits()
        {
            var log = new List<string>();
            var seq = new Sequence(
                Record(log, "a", NodeStatus.Running),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Running, seq.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log);
        }

        [Test]
        public void Sequence_NoChildren_ReturnsSuccess()
        {
            var seq = new Sequence();
            Assert.AreEqual(NodeStatus.Success, seq.Tick(new Blackboard()));
        }
    }
}
