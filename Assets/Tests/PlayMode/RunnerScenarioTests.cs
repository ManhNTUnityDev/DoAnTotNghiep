using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
            //
            // The two conditions must hold in the SAME frame. Checked independently, "sight was
            // broken at some point" is satisfied by plain fleeing too: the runner runs away from
            // the chaser at x=0 and ends up past the wall at x=4 either way, so the test would
            // pass without cover ever being the reason.
            bool hidAtSomePoint = false;
            bool hidAndBrokeSight = false;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                yield return null;
                bool hiding = brain.Blackboard.ActiveBehaviour == BehaviourId.Hide;
                if (hiding) hidAtSomePoint = true;

                bool clear = Sensor.IsSightClear(
                    chaser.transform.position + Vector3.up * 0.6f,
                    runner.transform.position + Vector3.up * 0.6f, ~0);

                if (hiding && !clear)
                {
                    hidAndBrokeSight = true;
                    break;
                }
            }

            Assert.IsTrue(hidAtSomePoint, "runner should take cover while a threat is near");
            Assert.IsTrue(hidAndBrokeSight,
                "the runner should be out of the chaser's sight while it is hiding, not merely at "
                + "some later moment when it has run away");
        }

        // The hiding place is found by a condition and used by the action that follows it, and
        // the two are only linked by running in the same tick — in the tree through
        // bb.CoverPoint, in the table through a field on HideState that doubles as the transition
        // predicate. Reordering either would break that link silently, so this pins the property
        // both share: while hiding, the runner is heading somewhere the threat cannot see.
        [UnityTest]
        public IEnumerator Hiding_HeadsForAPointTheThreatCannotSee()
        {
            arena.AddWall(new Vector3(4f, 1.5f, 0f), new Vector3(0.5f, 3f, 8f));
            yield return arena.Bake();
            var chaser = arena.MakePuppet("Chaser", Team.Chaser, new Vector3(0f, 0f, 0f));
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(3f, 0.2f, 0f));
            var brain = arena.AttachBrain(runner, kind);
            var agent = runner.GetComponent<NavMeshAgent>();

            bool sawHide = false;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                yield return null;
                if (brain.Blackboard.ActiveBehaviour != BehaviourId.Hide || !agent.hasPath)
                {
                    continue;
                }

                sawHide = true;
                Vector3 chaserEye = chaser.transform.position + Vector3.up * 0.6f;
                Assert.IsFalse(
                    Sensor.IsSightClearPastBodies(chaserEye, agent.destination + Vector3.up * 0.6f, ~0),
                    "while hiding, the destination should be the sheltered point the condition found, "
                    + "not a point in the open: " + agent.destination);
                break;
            }

            Assert.IsTrue(sawHide, "runner never took cover, so the coupling was never exercised");
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
