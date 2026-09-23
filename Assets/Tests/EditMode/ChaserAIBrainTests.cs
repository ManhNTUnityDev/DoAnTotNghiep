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
    }
}
