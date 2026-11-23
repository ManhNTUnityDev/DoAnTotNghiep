using System.Collections.Generic;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;
using ChaseGame.AI.Fsm;

namespace ChaseGame.Tests
{
    public class StateMachineTests
    {
        private sealed class RecordingState : IFsmState
        {
            private readonly List<string> log;
            public string Name { get; }
            public RecordingState(string name, List<string> log) { Name = name; this.log = log; }
            public void Enter(Blackboard bb) => log.Add(Name + ".Enter");
            public void Tick(Blackboard bb) => log.Add(Name + ".Tick");
            public void Exit(Blackboard bb) => log.Add(Name + ".Exit");
        }

        private List<string> log;
        private RecordingState a, b, c;
        private Blackboard bb;

        [SetUp]
        public void SetUp()
        {
            log = new List<string>();
            a = new RecordingState("A", log);
            b = new RecordingState("B", log);
            c = new RecordingState("C", log);
            bb = new Blackboard();
        }

        [Test]
        public void FirstTick_EntersInitialThenTicksIt()
        {
            var fsm = new StateMachine(a, new List<FsmTransition>());
            fsm.Tick(bb);
            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Tick" }, log);
        }

        [Test]
        public void MatchingTransition_ExitsEntersThenTicksNewState()
        {
            bool go = false;
            var fsm = new StateMachine(a, new List<FsmTransition> { new FsmTransition(a, _ => go, b) });
            fsm.Tick(bb);
            go = true;
            fsm.Tick(bb);
            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Tick", "A.Exit", "B.Enter", "B.Tick" }, log);
            Assert.AreSame(b, fsm.Current);
        }

        [Test]
        public void FirstMatchingTransitionInListOrderWins()
        {
            var fsm = new StateMachine(a, new List<FsmTransition>
            {
                new FsmTransition(a, _ => true, c),
                new FsmTransition(a, _ => true, b),
            });
            fsm.Tick(bb);
            Assert.AreSame(c, fsm.Current);
        }

        [Test]
        public void AnyStateTransition_FiresFromEveryState_ButNotIntoItself()
        {
            var fsm = new StateMachine(a, new List<FsmTransition> { new FsmTransition(null, _ => true, b) });
            fsm.Tick(bb);
            Assert.AreSame(b, fsm.Current);
            log.Clear();
            fsm.Tick(bb);
            CollectionAssert.AreEqual(new[] { "B.Tick" }, log);
        }

        [Test]
        public void OnlyOneTransitionPerTick()
        {
            var fsm = new StateMachine(a, new List<FsmTransition>
            {
                new FsmTransition(a, _ => true, b),
                new FsmTransition(b, _ => true, c),
            });
            fsm.Tick(bb);
            Assert.AreSame(b, fsm.Current);
        }

        [Test]
        public void TransitionCount_ReturnsTableSize()
        {
            var fsm = new StateMachine(a, new List<FsmTransition>
            {
                new FsmTransition(a, _ => false, b),
                new FsmTransition(b, _ => false, c),
                new FsmTransition(c, _ => false, a),
            });
            Assert.AreEqual(3, fsm.TransitionCount);
            Assert.AreEqual(3, fsm.States.Count);
        }

        [Test]
        public void ActionState_TicksWrappedAction()
        {
            int ticks = 0;
            var s = new ActionState("Act", _ => { ticks++; return NodeStatus.Running; });
            var fsm = new StateMachine(s, new List<FsmTransition>());
            fsm.Tick(bb);
            fsm.Tick(bb);
            Assert.AreEqual(2, ticks);
            Assert.AreEqual("Act", s.Name);
        }
    }
}
