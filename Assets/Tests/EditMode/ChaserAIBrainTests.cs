using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Tests
{
    [TestFixture(BrainKind.BehaviourTree)]
    [TestFixture(BrainKind.Fsm)]
    public class ChaserAIBrainTests
    {
        private readonly BrainKind kind;
        public ChaserAIBrainTests(BrainKind kind) { this.kind = kind; }

        private readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();
        private AIConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<AIConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
            Object.DestroyImmediate(config);
        }

        private (Character, SpyCharacterMovement, AIBrain) MakeChaser()
        {
            var go = new GameObject("Chaser");
            spawned.Add(go);
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(Team.Chaser);
            var spy = new SpyCharacterMovement();
            ch.SetMovementForTests(spy);
            var brain = BrainFactory.Add(go, Team.Chaser, kind);
            brain.Initialize(ch, new TeamRoster(), config);
            return (ch, spy, brain);
        }

        [Test]
        public void WhenTargetPresent_ChasesTargetPosition()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(5f, 0f, 3f);

            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();

            Assert.AreEqual(1, spy.MoveToCallCount);
            Assert.AreEqual(new Vector3(5f, 0f, 3f), spy.LastDestination);
        }

        [Test]
        public void WhenJailed_DoesNotChase()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(5f, 0f, 0f);
            ch.CaptureState = CaptureState.Jailed;

            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();

            Assert.AreEqual(0, spy.MoveToCallCount);
        }

        [Test]
        public void WhenTargetLost_ContinuesToLastKnownTargetPosition_InsteadOfOldWanderPoint()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(6f, 0f, 4f);

            // Pre-existing wander destination in the opposite direction
            brain.Blackboard.Destination = new Vector3(-10f, 0f, -10f);

            // Tick 1: sees target and chases it
            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();
            Assert.AreEqual(new Vector3(6f, 0f, 4f), spy.LastDestination);

            // Tick 2: loses sight of target -> continues to last known position (6, 0, 4), NOT (-10, 0, -10)
            brain.Blackboard.CurrentTarget = null;
            brain.TickForTests();
            Assert.AreEqual(new Vector3(6f, 0f, 4f), spy.LastDestination);
        }

        [Test]
        public void WhenChasingTarget_UsesSpeedBoostSkill()
        {
            var (ch, spy, brain) = MakeChaser();
            var abilities = new FakeCharacterAbilities();
            ch.SetAbilitiesForTests(abilities);

            var target = new GameObject("Runner");
            spawned.Add(target);
            // Distance 10m (> 8m ShootRange so it enters ChaseTarget branch)
            target.transform.position = new Vector3(10f, 0f, 0f);

            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();

            CollectionAssert.Contains(abilities.Used, 1, "Chaser should trigger SpeedBoost (ability 1) while chasing");
        }
        [Test]
        public void Chase_WithBoostOnCooldown_StillMovesToTarget()
        {
            var (ch, spy, brain) = MakeChaser();
            var abilities = new FakeCharacterAbilities();
            abilities.ReadyIds.Clear();
            ch.SetAbilitiesForTests(abilities);
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(10f, 0f, 0f);

            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();

            Assert.AreEqual(1, spy.MoveToCallCount);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), spy.LastDestination);
            Assert.AreEqual(0, abilities.Used.Count);
            Assert.AreEqual(BehaviourId.Chase, brain.Blackboard.ActiveBehaviour);
        }

        [Test]
        public void NoNavMesh_WanderFails_NoException()
        {
            var (ch, spy, brain) = MakeChaser();
            Assert.DoesNotThrow(brain.TickForTests);
            Assert.DoesNotThrow(brain.TickForTests);
            Assert.AreEqual(BehaviourId.Wander, brain.Blackboard.ActiveBehaviour);
            Assert.AreEqual(0, spy.MoveToCallCount);
        }
        // --- extension experiment: search where the target was lost (C5) ---

        [Test]
        public void TargetLost_WalksToLastSeenThenSearches()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(6f, 0f, 0f);

            brain.Blackboard.Now = 100f;
            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();
            Assert.AreEqual(BehaviourId.Chase, brain.Blackboard.ActiveBehaviour);

            // Sight lost: head for the last-seen point and enter Search, not Wander.
            brain.Blackboard.CurrentTarget = null;
            brain.Blackboard.Now = 100.2f;
            brain.TickForTests();

            Assert.AreEqual(BehaviourId.Search, brain.Blackboard.ActiveBehaviour);
            Assert.AreEqual(new Vector3(6f, 0f, 0f), spy.LastDestination);
        }

        [Test]
        public void SearchTimesOut_ReturnsToWander()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(6f, 0f, 0f);

            brain.Blackboard.Now = 100f;
            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();
            brain.Blackboard.CurrentTarget = null;
            brain.Blackboard.Now = 100.2f;
            brain.TickForTests();
            Assert.AreEqual(BehaviourId.Search, brain.Blackboard.ActiveBehaviour);

            // Past searchDurationSeconds (6 s by default) the sweep is abandoned.
            brain.Blackboard.Now = 100f + 6.5f;
            brain.TickForTests();

            Assert.AreEqual(BehaviourId.Wander, brain.Blackboard.ActiveBehaviour);
        }

        [Test]
        public void TargetReappearsDuringSearch_Chases()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(6f, 0f, 0f);

            brain.Blackboard.Now = 100f;
            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();
            brain.Blackboard.CurrentTarget = null;
            brain.Blackboard.Now = 100.2f;
            brain.TickForTests();
            Assert.AreEqual(BehaviourId.Search, brain.Blackboard.ActiveBehaviour);

            target.transform.position = new Vector3(9f, 0f, 1f);
            brain.Blackboard.CurrentTarget = target.transform;
            brain.Blackboard.Now = 100.4f;
            brain.TickForTests();

            Assert.AreEqual(BehaviourId.Chase, brain.Blackboard.ActiveBehaviour);
            Assert.AreEqual(new Vector3(9f, 0f, 1f), spy.LastDestination);
        }
    }
}
