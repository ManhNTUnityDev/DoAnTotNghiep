using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChaseGame.AI;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests.PlayMode
{
    // Chaser scenarios C1–C4, run against both decision architectures.
    [TestFixture(BrainKind.BehaviourTree)]
    [TestFixture(BrainKind.Fsm)]
    public class ChaserScenarioTests
    {
        private readonly BrainKind kind;
        private ScenarioArena arena;

        public ChaserScenarioTests(BrainKind kind) { this.kind = kind; }

        [SetUp]
        public void SetUp() { arena = new ScenarioArena(); }

        [TearDown]
        public void TearDown() { arena.Dispose(); }

        [UnityTest]
        public IEnumerator C1_RunnerVisibleOutOfRange_ChaserCloses()
        {
            yield return arena.Bake();
            var runner = arena.MakePuppet("Runner", Team.Runner, new Vector3(5.5f, 0f, 0f));
            var chaser = arena.MakeBody("Chaser", Team.Chaser, new Vector3(-5.5f, 0.2f, 0f));
            var brain = arena.AttachBrain(chaser, kind);

            for (float t = 0f; t < 1.5f; t += Time.deltaTime) yield return null;

            Assert.Less(ScenarioArena.Planar(chaser.transform.position, runner.transform.position), 9f);
            Assert.AreEqual(BehaviourId.Chase, brain.Blackboard.ActiveBehaviour);
        }

        [UnityTest]
        public IEnumerator C2_RunnerInRangeGunReady_IsCaptured()
        {
            yield return arena.Bake();
            var runner = arena.MakeBody("Runner", Team.Runner, new Vector3(3f, 0.2f, 0f));
            var chaser = arena.MakeChaserWithGun(new Vector3(-3f, 0.2f, 0f));
            arena.AttachBrain(chaser, kind);

            for (float t = 0f; t < 3f && runner.CaptureState == CaptureState.Free; t += Time.deltaTime)
                yield return null;

            Assert.AreEqual(CaptureState.Jailed, runner.CaptureState);
        }

        [UnityTest]
        public IEnumerator C3_NoOneVisible_Wanders()
        {
            yield return arena.Bake();
            var chaser = arena.MakeBody("Chaser", Team.Chaser, new Vector3(0f, 0.2f, 0f));
            var brain = arena.AttachBrain(chaser, kind);

            float travelled = 0f;
            Vector3 last = chaser.transform.position;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                yield return null;
                travelled += ScenarioArena.Planar(last, chaser.transform.position);
                last = chaser.transform.position;
            }

            Assert.Greater(travelled, 2f);
            Assert.AreEqual(BehaviourId.Wander, brain.Blackboard.ActiveBehaviour);
        }

        [UnityTest]
        public IEnumerator C4_TargetBehindWall_GoesToLastSeen()
        {
            arena.AddWall(new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3f, 10f));
            yield return arena.Bake();
            var lastSeen = new Vector3(-2f, 0f, 6f);
            var runner = arena.MakePuppet("Runner", Team.Runner, lastSeen);
            var chaser = arena.MakeBody("Chaser", Team.Chaser, new Vector3(-6f, 0.2f, 0f));
            var brain = arena.AttachBrain(chaser, kind);

            for (float t = 0f; t < 1f && brain.Blackboard.ActiveBehaviour != BehaviourId.Chase; t += Time.deltaTime)
                yield return null;
            Assert.AreEqual(BehaviourId.Chase, brain.Blackboard.ActiveBehaviour, "sanity: chaser saw the runner");

            // Runner slips behind the wall, out of sight.
            runner.transform.position = new Vector3(6f, 0f, 6f);

            float best = ScenarioArena.Planar(chaser.transform.position, lastSeen);
            float t2 = 0f;
            while (t2 < 3f && best >= 1.5f)
            {
                yield return null;
                t2 += Time.deltaTime;
                float d = ScenarioArena.Planar(chaser.transform.position, lastSeen);
                Assert.LessOrEqual(d, best + 0.3f, "chaser turned away from the last-seen point");
                best = Mathf.Min(best, d);
            }

            Assert.Less(best, 1.5f, "chaser should reach the runner's last-seen point");
        }
    }
}
