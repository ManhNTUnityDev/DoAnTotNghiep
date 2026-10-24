using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Tests
{
    public class RunnerAIBrainTests
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

        private (Character, SpyCharacterMovement, FakeCharacterAbilities, RunnerAIBrain) MakeRunner()
        {
            var go = new GameObject("Runner");
            spawned.Add(go);
            go.transform.position = Vector3.zero;
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(Team.Runner);
            var spy = new SpyCharacterMovement();
            ch.SetMovementForTests(spy);
            var abilities = new FakeCharacterAbilities();
            ch.SetAbilitiesForTests(abilities);
            var brain = go.AddComponent<RunnerAIBrain>();
            brain.Initialize(ch, new TeamRoster(), config);
            return (ch, spy, abilities, brain);
        }

        [Test]
        public void WhenThreatNear_FleesAwayFromThreat()
        {
            var (ch, spy, abilities, brain) = MakeRunner();
            var threat = new GameObject("Chaser");
            spawned.Add(threat);
            threat.transform.position = new Vector3(3f, 0f, 0f); // threat to +x, within ThreatRadius

            brain.Blackboard.NearestThreat = threat.transform;
            brain.TickForTests();

            Assert.GreaterOrEqual(spy.MoveToCallCount, 1);
            Assert.Less(spy.LastDestination.x, 0f, "should flee to -x, away from the +x threat");
        }

        [Test]
        public void WhenThreatNear_UsesEscapeSkills()
        {
            var (ch, spy, abilities, brain) = MakeRunner();
            var threat = new GameObject("Chaser");
            spawned.Add(threat);
            threat.transform.position = new Vector3(3f, 0f, 0f);

            brain.Blackboard.NearestThreat = threat.transform;
            brain.TickForTests();

            CollectionAssert.Contains(abilities.Used, 0); // SpeedBoost
            CollectionAssert.Contains(abilities.Used, 1); // Decoy
        }

        [Test]
        public void WhenNoThreatButJailedTeammate_MovesToRescue()
        {
            var (ch, spy, abilities, brain) = MakeRunner();
            var roster = (TeamRoster)brain.Blackboard.Roster;
            var jailed = new GameObject("Jailed");
            spawned.Add(jailed);
            jailed.transform.position = new Vector3(7f, 0f, 2f);
            var jailedCh = jailed.AddComponent<Character>();
            jailedCh.SetTeamForTests(Team.Runner);
            jailedCh.CaptureState = CaptureState.Jailed;
            roster.Add(jailedCh);

            brain.Blackboard.NearestThreat = null;
            brain.TickForTests();

            Assert.GreaterOrEqual(spy.MoveToCallCount, 1);
            Assert.AreEqual(new Vector3(7f, 0f, 2f), spy.LastDestination);
        }

        [Test]
        public void ThreatNear_AppliesHysteresis_DoesNotDropFleeJustOutsideBaseRadius()
        {
            var (ch, spy, abilities, brain) = MakeRunner();
            var threat = new GameObject("Chaser");
            spawned.Add(threat);

            // Step 1: Threat inside 6m triggers fleeing and clears stale wander destination.
            brain.Blackboard.Destination = new Vector3(10f, 0f, 0f);
            threat.transform.position = new Vector3(5.8f, 0f, 0f);
            brain.Blackboard.NearestThreat = threat.transform;
            brain.TickForTests();

            Assert.IsTrue(brain.Blackboard.IsFleeing);
            Assert.IsNull(brain.Blackboard.Destination, "fleeing must clear stale wander destination");

            // Step 2: Threat at 6.5m (just outside 6m base radius, inside 8.1m exit radius) keeps fleeing.
            threat.transform.position = new Vector3(6.5f, 0f, 0f);
            Assert.IsTrue(RunnerAIBrain.ThreatNear(brain.Blackboard));

            // Step 3: Threat at 8.5m (beyond hysteresis exit radius) stops fleeing.
            threat.transform.position = new Vector3(8.5f, 0f, 0f);
            Assert.IsFalse(RunnerAIBrain.ThreatNear(brain.Blackboard));
        }

        [Test]
        public void WhenJailedTeammateIsGuardedByChaser_DoesNotSuicidallyRushCage()
        {
            var (ch, spy, abilities, brain) = MakeRunner();
            var roster = (TeamRoster)brain.Blackboard.Roster;

            var jailed = new GameObject("Jailed");
            spawned.Add(jailed);
            jailed.transform.position = new Vector3(10f, 0f, 0f);
            var jailedCh = jailed.AddComponent<Character>();
            jailedCh.SetTeamForTests(Team.Runner);
            jailedCh.CaptureState = CaptureState.Jailed;
            roster.Add(jailedCh);

            var guard = new GameObject("GuardChaser");
            spawned.Add(guard);
            guard.transform.position = new Vector3(9f, 0f, 0f); // 1m from cage (within 5m RescueThreatRadius)
            var guardCh = guard.AddComponent<Character>();
            guardCh.SetTeamForTests(Team.Chaser);
            roster.Add(guardCh);

            Assert.IsFalse(RunnerAIBrain.HasJailedTeammate(brain.Blackboard));
        }
    }
}
