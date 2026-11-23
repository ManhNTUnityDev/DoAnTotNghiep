using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.Fsm;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class FsmBrainTableTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private AIConfig config;

        [SetUp]
        public void SetUp() { config = ScriptableObject.CreateInstance<AIConfig>(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
            Object.DestroyImmediate(config);
        }

        private AIBrain Make(Team team, BrainKind kind)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            var brain = BrainFactory.Add(go, team, kind);
            brain.Initialize(ch, new TeamRoster(), config);
            return brain;
        }

        [Test]
        public void ChaserFsm_Has3StatesAnd6Transitions()
        {
            var fsm = ((FsmBrain)Make(Team.Chaser, BrainKind.Fsm)).Machine;
            Assert.AreEqual(3, fsm.States.Count);
            Assert.AreEqual(6, fsm.TransitionCount);
        }

        [Test]
        public void RunnerFsm_Has2StatesAnd2Transitions()
        {
            var fsm = ((FsmBrain)Make(Team.Runner, BrainKind.Fsm)).Machine;
            Assert.AreEqual(2, fsm.States.Count);
            Assert.AreEqual(2, fsm.TransitionCount);
        }

        [Test]
        public void GameFsms_UseNoAnyStateTransitions()
        {
            foreach (var team in new[] { Team.Chaser, Team.Runner })
            {
                foreach (var t in ((FsmBrain)Make(team, BrainKind.Fsm)).Machine.Transitions)
                {
                    Assert.IsNotNull(t.From, team + " FSM must be a pure FSM");
                }
            }
        }

        [Test]
        public void Factory_MapsTeamAndKindToBrainType()
        {
            Assert.IsInstanceOf<ChaserAIBrain>(Make(Team.Chaser, BrainKind.BehaviourTree));
            Assert.IsInstanceOf<ChaserFsmBrain>(Make(Team.Chaser, BrainKind.Fsm));
            Assert.IsInstanceOf<RunnerAIBrain>(Make(Team.Runner, BrainKind.BehaviourTree));
            Assert.IsInstanceOf<RunnerFsmBrain>(Make(Team.Runner, BrainKind.Fsm));
        }

        [Test]
        public void KindOf_NoAIBrain_IsBehaviourTree()
        {
            var go = new GameObject("Plain");
            spawned.Add(go);
            Assert.AreEqual(BrainKind.BehaviourTree, BrainFactory.KindOf(go));
            Assert.AreEqual(BrainKind.Fsm, BrainFactory.KindOf(Make(Team.Runner, BrainKind.Fsm).gameObject));
        }
    }
}
