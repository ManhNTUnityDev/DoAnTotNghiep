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
        private AIBrain searchBrain;

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
            // Pinned, not inherited: the sweep that follows heads for a ring of this radius
            // around the last-seen point, so the closest approach below depends on it. Left at
            // the asset default, raising searchRadius would turn this test red for a reason that
            // has nothing to do with what it checks.
            arena.TuneSearch(durationSeconds: 6f, pointCount: 4, radius: 4f);
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

            // Closest approach, not a frame-by-frame march. The chaser sweeps a ring around the
            // last-seen point, so its path legitimately curves and at times moves away again;
            // requiring every frame to close the gap was asserting a straight line, which the
            // behaviour never promised.
            float start = ScenarioArena.Planar(chaser.transform.position, lastSeen);
            float best = start;
            for (float t2 = 0f; t2 < 4f && best >= 1.5f; t2 += Time.deltaTime)
            {
                yield return null;
                best = Mathf.Min(best, ScenarioArena.Planar(chaser.transform.position, lastSeen));
            }

            Assert.Less(best, 1.5f,
                $"chaser should reach the runner's last-seen point; got no closer than {best:0.00} m "
                + $"from {start:0.00} m away");
        }
        // --- extension experiment (C5) ---

        [UnityTest]
        public IEnumerator C5_LostBehindWall_SearchesThenWanders()
        {
            arena.AddWall(new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3f, 10f));
            yield return arena.Bake();
            var runner = arena.MakePuppet("Runner", Team.Runner, new Vector3(-2f, 0f, 6f));
            var chaser = arena.MakeBody("Chaser", Team.Chaser, new Vector3(-6f, 0.2f, 0f));
            var brain = arena.AttachBrain(chaser, kind);

            for (float t = 0f; t < 1f && brain.Blackboard.ActiveBehaviour != BehaviourId.Chase; t += Time.deltaTime)
                yield return null;
            Assert.AreEqual(BehaviourId.Chase, brain.Blackboard.ActiveBehaviour, "sanity: chaser saw the runner");

            runner.transform.position = new Vector3(14f, 0f, 14f); // far away and out of sight

            var seen = new System.Collections.Generic.List<BehaviourId>();
            Vector3? sweepStart = null;
            float sweptDistance = 0f;
            for (float t = 0f; t < 10f; t += Time.deltaTime)
            {
                yield return null;
                var b = brain.Blackboard.ActiveBehaviour;
                if (seen.Count == 0 || seen[seen.Count - 1] != b) seen.Add(b);

                // How far the chaser got from where the sweep began, so "searched the area" is
                // distinguished from "set the Search flag and stood still".
                if (b == BehaviourId.Search)
                {
                    if (sweepStart == null) sweepStart = chaser.transform.position;
                    sweptDistance = Mathf.Max(
                        sweptDistance, ScenarioArena.Planar(chaser.transform.position, sweepStart.Value));
                }

                if (seen.Contains(BehaviourId.Search) && b == BehaviourId.Wander) break;
            }

            int search = seen.IndexOf(BehaviourId.Search);
            int wander = seen.LastIndexOf(BehaviourId.Wander);
            Assert.Greater(search, -1, "chaser should search after losing the target; saw " + string.Join(",", seen));
            Assert.Greater(wander, search, "search should give way to wander; saw " + string.Join(",", seen));
            Assert.Greater(sweptDistance, 2f,
                $"the sweep should move the chaser through the area, not just set a flag; it covered "
                + $"{sweptDistance:0.00} m");
        }

        // --- the search time limit ---
        //
        // These two cover the branch that abandons a sweep because the clock ran out. They are a
        // pair on purpose: the EditMode test of the same idea cannot fail for that reason, because
        // EditMode has no NavMesh, so the point list comes back empty and the sweep ends by
        // exhaustion no matter what the clock says. Here the points are real and far apart —
        // 4 points at radius 12, so a full sweep is roughly 60 m of travel at 5 m/s — which means
        // nothing can be exhausted inside the 3 s window. The first test therefore fails if the
        // time limit is never enforced, and the second fails if the sweep ends for some other
        // reason.
        private const float SweepWindow = 3f;

        [UnityTest]
        public IEnumerator SearchTimeLimitReached_ReturnsToWander()
        {
            arena.TuneSearch(durationSeconds: 1f, pointCount: 4, radius: 12f);
            yield return LoseTheTarget();

            BehaviourId last = BehaviourId.None;
            for (float t = 0f; t < SweepWindow; t += Time.deltaTime)
            {
                yield return null;
                last = searchBrain.Blackboard.ActiveBehaviour;
                if (last == BehaviourId.Wander) break;
            }

            Assert.AreEqual(BehaviourId.Wander, last,
                "a 1 s search limit should end the sweep well inside " + SweepWindow + " s");
        }

        [UnityTest]
        public IEnumerator SearchTimeLimitNotReached_KeepsSearching()
        {
            arena.TuneSearch(durationSeconds: 30f, pointCount: 4, radius: 12f);
            yield return LoseTheTarget();

            for (float t = 0f; t < SweepWindow; t += Time.deltaTime)
            {
                yield return null;
                Assert.AreNotEqual(BehaviourId.Wander, searchBrain.Blackboard.ActiveBehaviour,
                    "with a 30 s limit the chaser should still be sweeping after " + t + " s");
            }

            Assert.AreEqual(BehaviourId.Search, searchBrain.Blackboard.ActiveBehaviour);
        }

        // Chaser sees the runner, then the runner vanishes: the setup both search tests need.
        private IEnumerator LoseTheTarget()
        {
            arena.AddWall(new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3f, 10f));
            yield return arena.Bake();
            var runner = arena.MakePuppet("Runner", Team.Runner, new Vector3(-2f, 0f, 6f));
            var chaser = arena.MakeBody("Chaser", Team.Chaser, new Vector3(-6f, 0.2f, 0f));
            searchBrain = arena.AttachBrain(chaser, kind);

            for (float t = 0f; t < 1f && searchBrain.Blackboard.ActiveBehaviour != BehaviourId.Chase; t += Time.deltaTime)
                yield return null;
            Assert.AreEqual(BehaviourId.Chase, searchBrain.Blackboard.ActiveBehaviour, "sanity: chaser saw the runner");

            runner.transform.position = new Vector3(14f, 0f, 14f); // far away and out of sight

            for (float t = 0f; t < 2f && searchBrain.Blackboard.ActiveBehaviour != BehaviourId.Search; t += Time.deltaTime)
                yield return null;
            Assert.AreEqual(BehaviourId.Search, searchBrain.Blackboard.ActiveBehaviour, "sanity: chaser started a sweep");
        }
    }
}
