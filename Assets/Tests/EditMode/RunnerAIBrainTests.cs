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
        public void CloneRunner_WhenThreatNear_FleesWithoutUsingSkillsAndDivergesDirection()
        {
            var (ch, spy, abilities, brain) = MakeRunner();
            brain.ConfigureAsClone(Vector3.forward, fleeAngleBias: 60f);
            int callsBefore = spy.MoveToCallCount;

            var threat = new GameObject("Chaser");
            spawned.Add(threat);
            threat.transform.position = new Vector3(3f, 0f, 0f); // threat to +x, direct away is -x (0, 0)

            brain.Blackboard.NearestThreat = threat.transform;
            brain.TickForTests();

            Assert.AreEqual(0, abilities.Used.Count, "Clone runner must never use SpeedBoost or Decoy");
            Assert.Greater(spy.MoveToCallCount, callsBefore);
            // With +60 deg bias from (-1, 0, 0), the flee target should have a non-zero z component.
            Assert.Greater(Mathf.Abs(spy.LastDestination.z), 0.5f, "Clone should diverge at an angle from the direct away vector");
        }

        [Test]
        public void DecoyUse_SpawnsCloneAtRunnerPosition_StripsSkillsAndRegistersDecoyLure()
        {
            var roster = new TeamRoster();
            var runnerGo = new GameObject("OriginalRunner");
            spawned.Add(runnerGo);
            runnerGo.transform.position = new Vector3(4f, 0f, -2f);

            var runner = runnerGo.AddComponent<Character>();
            runner.SetTeamForTests(Team.Runner);
            roster.Add(runner);

            runnerGo.AddComponent<ChaseGame.Abilities.SpeedBoost>();
            var decoy = runnerGo.AddComponent<ChaseGame.Abilities.Decoy>();
            decoy.Configure(roster, config);

            decoy.Use(runner, Vector3.forward);

            Assert.IsTrue(decoy.IsCloneActive);
            Assert.IsNotNull(decoy.ActiveClone);
            spawned.Add(decoy.ActiveClone.gameObject);

            // 1. Spawned right at the caster's position
            Assert.AreEqual(runnerGo.transform.position, decoy.ActiveClone.transform.position);

            // 2. Registered in DecoyLure.Active, NOT in TeamRoster.Runners
            CollectionAssert.Contains(DecoyLure.Active, decoy.ActiveClone);
            Assert.AreEqual(1, roster.Runners.Count, "Clone must not be added to TeamRoster.Runners");

            // 3. Has RunnerAIBrain in clone mode, and cannot use abilities or be captured
            var cloneBrain = decoy.ActiveClone.GetComponent<RunnerAIBrain>();
            Assert.IsNotNull(cloneBrain);
            Assert.IsTrue(cloneBrain.IsClone);

            var cloneChar = decoy.ActiveClone.GetComponent<Character>();
            Assert.IsNotNull(cloneChar);
            Assert.IsFalse(cloneChar.IsAbilityReady(0));
            Assert.IsFalse(cloneChar.IsAbilityReady(1));
            Assert.IsFalse(ChaseGame.Abilities.Gun.IsCapturable(cloneChar));

            // 4. Dismiss immediately removes and destroys the clone
            decoy.ActiveClone.Dismiss();
            Assert.IsFalse(decoy.IsCloneActive);
        }
    }
}
