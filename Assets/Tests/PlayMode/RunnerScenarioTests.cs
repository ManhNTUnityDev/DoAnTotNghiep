using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChaseGame.AI;
using ChaseGame.Characters;

namespace ChaseGame.Tests.PlayMode
{
    // Runner scenarios R1–R4, run against both decision architectures.
    [TestFixture(BrainKind.BehaviourTree)]
    [TestFixture(BrainKind.Fsm)]
    public class RunnerScenarioTests
    {
        private readonly BrainKind kind;
        private ScenarioArena arena;

        public RunnerScenarioTests(BrainKind kind) { this.kind = kind; }

        [SetUp]
        public void SetUp() { arena = new ScenarioArena(); }

        [TearDown]
        public void TearDown() { arena.Dispose(); }

        private static IEnumerator WaitForFirstDecision(AIBrain brain)
        {
            for (float t = 0f; t < 1f && brain.Blackboard.ActiveBehaviour == BehaviourId.None; t += Time.deltaTime)
                yield return null;
        }

        [UnityTest]
        public IEnumerator R1_ChaserEntersThreatRadius_DistanceGrows()
        {
            yield return arena.Bake();
            var chaser = arena.MakePuppet("Chaser", Team.Chaser, new Vector3(-2f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(2f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);

            yield return WaitForFirstDecision(brain);
            Assert.AreEqual(BehaviourId.Flee, brain.Blackboard.ActiveBehaviour, "first decision is to flee");

            for (float t = 0f; t < 2f; t += Time.deltaTime) yield return null;

            Assert.Greater(ScenarioArena.Planar(runner.transform.position, chaser.transform.position), 6f);
        }

        [UnityTest]
        public IEnumerator R2_ThreatenedWithSkillsReady_UsesSkills()
        {
            yield return arena.Bake();
            arena.MakePuppet("Chaser", Team.Chaser, new Vector3(-2f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(2f, 0.2f, 0f));
            var abilities = new OneShotAbilities();
            runner.SetAbilitiesForTests(abilities);
            arena.AttachBrain(runner, kind);

            for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, abilities.Used, "SpeedBoost and Decoy, once each");
        }

        [UnityTest]
        public IEnumerator R3_CorneredAgainstWalls_StillFindsFleePoint()
        {
            arena.AddWall(new Vector3(10f, 1.5f, 5f), new Vector3(0.5f, 3f, 10.5f));
            arena.AddWall(new Vector3(5f, 1.5f, 10f), new Vector3(10.5f, 3f, 0.5f));
            yield return arena.Bake();
            var chaser = arena.MakePuppet("Chaser", Team.Chaser, new Vector3(6f, 0f, 6f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(8.8f, 0.2f, 8.8f));
            arena.AttachBrain(runner, kind);

            Vector3 start = runner.transform.position;
            Vector3 last = start;
            float stillFor = 0f, longestStill = 0f;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                yield return null;
                bool threatened = ScenarioArena.Planar(runner.transform.position, chaser.transform.position) <= 6f;
                float step = ScenarioArena.Planar(last, runner.transform.position);
                last = runner.transform.position;
                stillFor = threatened && step < 0.05f * Time.deltaTime / 0.5f ? stillFor + Time.deltaTime : 0f;
                longestStill = Mathf.Max(longestStill, t > 0.3f ? stillFor : 0f);
            }

            Assert.Greater(ScenarioArena.Planar(start, runner.transform.position), 2f, "runner escaped the corner");
            Assert.Less(longestStill, 0.5f, "runner stood stuck while threatened");
        }

        [UnityTest]
        public IEnumerator R4_AtThreatBoundary_NoFleeWanderFlicker()
        {
            yield return arena.Bake();
            var chaser = arena.MakePuppet("Chaser", Team.Chaser, new Vector3(-2f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(2f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);

            yield return WaitForFirstDecision(brain);
            Assert.AreEqual(BehaviourId.Flee, brain.Blackboard.ActiveBehaviour);

            // Hold the chaser at 1.2 × ThreatRadius (7.2 m): outside the 6 m entry radius,
            // inside the 8.1 m exit radius. The runner must keep fleeing, never flicker to Wander.
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                Vector3 r = runner.transform.position;
                Vector3 away = new Vector3(r.x - chaser.transform.position.x, 0f, r.z - chaser.transform.position.z).normalized;
                chaser.transform.position = new Vector3(r.x, 0f, r.z) - away * 7.2f;
                yield return null;
                Assert.AreEqual(BehaviourId.Flee, brain.Blackboard.ActiveBehaviour, $"flickered at t={t:0.00}");
            }
        }
        // --- extension experiment (R5) ---

        [UnityTest]
        public IEnumerator R5_WallNearby_BreaksChaserSight()
        {
            arena.AddWall(new Vector3(4f, 1.5f, 0f), new Vector3(0.5f, 3f, 8f));
            yield return arena.Bake();
            var chaser = arena.MakePuppet("Chaser", Team.Chaser, new Vector3(0f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(3f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);

            // The scenario's chaser never moves, so once the runner is clear of the threat
            // radius it stops hiding and roams — and may wander back into view. What the
            // behaviour must show is that it took cover and broke line of sight while doing
            // so, not where it happens to stand at one later instant.
            bool hidAtSomePoint = false;
            bool brokeSight = false;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                yield return null;
                if (brain.Blackboard.ActiveBehaviour == BehaviourId.Hide) hidAtSomePoint = true;
                bool clear = Sensor.IsSightClear(
                    chaser.transform.position + Vector3.up * 0.6f,
                    runner.transform.position + Vector3.up * 0.6f, ~0);
                if (!clear) brokeSight = true;
                if (hidAtSomePoint && brokeSight) break;
            }

            Assert.IsTrue(hidAtSomePoint, "runner should take cover while a threat is near");
            Assert.IsTrue(brokeSight, "taking cover should break the chaser's line of sight");
        }

        [UnityTest]
        public IEnumerator ThreatNear_CoverAvailable_Hides()
        {
            arena.AddWall(new Vector3(4f, 1.5f, 0f), new Vector3(0.5f, 3f, 8f));
            yield return arena.Bake();
            arena.MakePuppet("Chaser", Team.Chaser, new Vector3(0f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(3f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);

            for (float t = 0f; t < 2f && brain.Blackboard.ActiveBehaviour != BehaviourId.Hide; t += Time.deltaTime)
                yield return null;

            Assert.AreEqual(BehaviourId.Hide, brain.Blackboard.ActiveBehaviour);
        }

        [UnityTest]
        public IEnumerator ThreatNear_NoCover_Flees()
        {
            yield return arena.Bake(); // open ground, nothing to hide behind
            arena.MakePuppet("Chaser", Team.Chaser, new Vector3(-2f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(2f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);

            yield return WaitForFirstDecision(brain);

            Assert.AreEqual(BehaviourId.Flee, brain.Blackboard.ActiveBehaviour);
        }

        [UnityTest]
        public IEnumerator Clone_NeverHides()
        {
            arena.AddWall(new Vector3(4f, 1.5f, 0f), new Vector3(0.5f, 3f, 8f));
            yield return arena.Bake();
            arena.MakePuppet("Chaser", Team.Chaser, new Vector3(0f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(3f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);
            ((ChaseGame.AI.IRunnerCloneBrain)brain).ConfigureAsClone(Vector3.forward);

            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                yield return null;
                Assert.AreNotEqual(BehaviourId.Hide, brain.Blackboard.ActiveBehaviour,
                    "a decoy clone must keep drawing the chaser, never hide");
            }
        }
    }
}
