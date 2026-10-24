using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Tests
{
    public class ChaserAIBrainTests
    {
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

        private (Character, SpyCharacterMovement, ChaserAIBrain) MakeChaser()
        {
            var go = new GameObject("Chaser");
            spawned.Add(go);
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(Team.Chaser);
            var spy = new SpyCharacterMovement();
            ch.SetMovementForTests(spy);
            var brain = go.AddComponent<ChaserAIBrain>();
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
        public void WhenGuardPostSet_AndNoTarget_MovesToGuardPost()
        {
            var (ch, spy, brain) = MakeChaser();
            var post = new Vector3(7f, 0f, -2f);

            brain.Blackboard.CurrentTarget = null;
            brain.Blackboard.GuardPost = post;
            brain.TickForTests();

            Assert.AreEqual(1, spy.MoveToCallCount);
            Assert.AreEqual(post, spy.LastDestination);
        }

        [Test]
        public void WhenTargetPresent_ChasesTarget_EvenIfGuarding()
        {
            var (ch, spy, brain) = MakeChaser();
            var target = new GameObject("Runner");
            spawned.Add(target);
            target.transform.position = new Vector3(4f, 0f, 0f);

            brain.Blackboard.GuardPost = new Vector3(7f, 0f, -2f);
            brain.Blackboard.CurrentTarget = target.transform;
            brain.TickForTests();

            // Active hunting outranks holding the cage post.
            Assert.AreEqual(new Vector3(4f, 0f, 0f), spy.LastDestination);
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
    }
}
