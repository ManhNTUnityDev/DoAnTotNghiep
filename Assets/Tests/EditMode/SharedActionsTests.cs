using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class SharedActionsTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private Blackboard MakeBlackboard(Team team, out SpyCharacterMovement spy)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            spy = new SpyCharacterMovement();
            ch.SetMovementForTests(spy);
            ch.SetAbilitiesForTests(new FakeCharacterAbilities());
            return new Blackboard { Self = ch, Roster = new TeamRoster(), Config = null };
        }

        private Transform MakeTarget(Vector3 position)
        {
            var go = new GameObject("Target");
            spawned.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        [Test]
        public void ChaserActions_CanShoot_NullConfig_UsesDefaultRange8()
        {
            var bb = MakeBlackboard(Team.Chaser, out _);

            bb.CurrentTarget = MakeTarget(new Vector3(7.9f, 0f, 0f));
            Assert.IsTrue(ChaserActions.CanShoot(bb));

            bb.CurrentTarget = MakeTarget(new Vector3(8.1f, 0f, 0f));
            Assert.IsFalse(ChaserActions.CanShoot(bb));
        }

        [Test]
        public void ChaserActions_ChaseTarget_SetsActiveBehaviourChase()
        {
            var bb = MakeBlackboard(Team.Chaser, out var spy);
            bb.CurrentTarget = MakeTarget(new Vector3(10f, 0f, 0f));

            var status = ChaserActions.ChaseTarget(bb);

            Assert.AreEqual(NodeStatus.Running, status);
            Assert.AreEqual(BehaviourId.Chase, bb.ActiveBehaviour);
            Assert.AreEqual(1, spy.MoveToCallCount);
        }

        [Test]
        public void RunnerActions_EscapeAndFlee_SetsActiveBehaviourFlee()
        {
            var bb = MakeBlackboard(Team.Runner, out var spy);
            bb.NearestThreat = MakeTarget(new Vector3(3f, 0f, 0f));

            var status = RunnerActions.EscapeAndFlee(bb);

            Assert.AreEqual(NodeStatus.Running, status);
            Assert.AreEqual(BehaviourId.Flee, bb.ActiveBehaviour);
            Assert.Less(spy.LastDestination.x, 0f);
        }
    }
}
