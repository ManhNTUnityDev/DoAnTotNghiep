using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class MatchControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f; // never leak a frozen clock to other tests
        }

        [Test]
        public void PrepareSceneExit_ResetsTimeScaleToOne()
        {
            // A match ends (or pauses) with Time.timeScale = 0. Leaving the scene must restore it,
            // else the Menu / next match inherits timeScale 0 and stays frozen.
            // (SceneManager.LoadScene throws in EditMode, so the load itself is not unit-tested.)
            var go = new GameObject("MatchControllerUnderTest");
            try
            {
                var mc = go.AddComponent<MatchController>();
                Time.timeScale = 0f;
                mc.PrepareSceneExit();
                Assert.AreEqual(1f, Time.timeScale);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputeCenterCountdownLabel_Shows321ReadyOnlyInFinalSeconds()
        {
            // Above 3s threshold: center countdown is hidden.
            Assert.AreEqual(string.Empty, MatchHudView.ComputeCenterCountdownLabel(10f, 0f, 3f));
            Assert.AreEqual(string.Empty, MatchHudView.ComputeCenterCountdownLabel(3.1f, 0f, 3f));

            // Last 3 seconds: shows "3", "2", "1".
            Assert.AreEqual("3", MatchHudView.ComputeCenterCountdownLabel(3.0f, 0f, 3f));
            Assert.AreEqual("3", MatchHudView.ComputeCenterCountdownLabel(2.1f, 0f, 3f));
            Assert.AreEqual("2", MatchHudView.ComputeCenterCountdownLabel(2.0f, 0f, 3f));
            Assert.AreEqual("2", MatchHudView.ComputeCenterCountdownLabel(1.1f, 0f, 3f));
            Assert.AreEqual("1", MatchHudView.ComputeCenterCountdownLabel(1.0f, 0f, 3f));
            Assert.AreEqual("1", MatchHudView.ComputeCenterCountdownLabel(0.05f, 0f, 3f));

            // Head-start finished (0s), while readyHoldRemaining > 0: shows "READY!".
            Assert.AreEqual(MatchHudView.ReadyLabel, MatchHudView.ComputeCenterCountdownLabel(0f, 0.8f, 3f));

            // After readyHoldRemaining expires: hidden again.
            Assert.AreEqual(string.Empty, MatchHudView.ComputeCenterCountdownLabel(0f, 0f, 3f));
        }

        [Test]
        public void HeadStartPhase_LocksChasers_LeavesRunnersFree_AndReleasesChasersOnReady()
        {
            var mcGo = new GameObject("MatchController");
            var chaserGo = new GameObject("Chaser");
            var runnerGo = new GameObject("Runner");

            try
            {
                var chaser = chaserGo.AddComponent<Character>();
                chaser.SetTeamForTests(Team.Chaser);

                var runner = runnerGo.AddComponent<Character>();
                runner.SetTeamForTests(Team.Runner);

                var roster = new TeamRoster();
                roster.Add(chaser);
                roster.Add(runner);
                roster.PlayerCharacter = runner;

                var mc = mcGo.AddComponent<MatchController>();
                mc.RoleRevealDurationSeconds = 0f; // this test covers head-start only
                mc.HeadStartDurationSeconds = 10f;
                mc.MatchDurationSeconds = 90f;
                mc.Construct(roster, new MatchEvaluator());
                mc.InitializeMatch();

                // Step 4 seconds into the 10s head-start:
                mc.Step(4f);
                Assert.AreEqual(MatchPhase.HeadStart, mc.Phase);
                Assert.AreEqual(6f, mc.HeadStartRemaining, 1e-3f);
                Assert.AreEqual(90f, mc.TimeRemaining, 1e-3f, "Main match clock must not tick down during head-start.");
                Assert.IsTrue(chaser.IsActionLocked, "Chaser must be locked during head-start.");
                Assert.IsFalse(runner.IsActionLocked, "Runner must be free to run during head-start.");

                // Step remaining 6 seconds to finish the 10s head-start:
                mc.Step(6f);
                Assert.AreEqual(MatchPhase.Playing, mc.Phase);
                Assert.AreEqual(0f, mc.HeadStartRemaining, 1e-3f);
                Assert.Greater(mc.ReadyDisplayRemaining, 0f);
                Assert.IsFalse(chaser.IsActionLocked, "Chaser must be unlocked once head-start finishes (READY!).");
                Assert.AreEqual(90f, mc.TimeRemaining, 1e-3f);

                // Subsequent step in Playing phase ticks down the main match clock:
                mc.Step(2f);
                Assert.AreEqual(88f, mc.TimeRemaining, 1e-3f);
                Assert.AreEqual(0f, mc.ReadyDisplayRemaining, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(mcGo);
                Object.DestroyImmediate(chaserGo);
                Object.DestroyImmediate(runnerGo);
            }
        }

        [Test]
        public void FormatHeadStartLabels_RespectBothRoles()
        {
            StringAssert.Contains("RUNNER", MatchHudView.FormatHeadStartTitle(Team.Runner));
            StringAssert.Contains("CHASER", MatchHudView.FormatHeadStartTitle(Team.Chaser));
            StringAssert.Contains("10s", MatchHudView.FormatHeadStartSubtitle(Team.Runner, 9.2f));
            StringAssert.Contains("10s", MatchHudView.FormatHeadStartSubtitle(Team.Chaser, 9.2f));
        }

        [Test]
        public void FormatRoleRevealLabels_MatchSpecCopy()
        {
            Assert.AreEqual("BẠN LÀ GÀ", MatchHudView.FormatRoleRevealTitle(Team.Chaser));
            Assert.AreEqual("Bắt hết phe Người trước khi hết giờ!", MatchHudView.FormatRoleRevealSubtitle(Team.Chaser));
            Assert.AreEqual("BẠN LÀ NGƯỜI", MatchHudView.FormatRoleRevealTitle(Team.Runner));
            Assert.AreEqual("Trốn đến hết giờ!", MatchHudView.FormatRoleRevealSubtitle(Team.Runner));
        }

        private sealed class MatchRig : System.IDisposable
        {
            public readonly GameObject McGo = new GameObject("MatchController");
            public readonly GameObject ChaserGo = new GameObject("Chaser");
            public readonly GameObject RunnerGo = new GameObject("Runner");
            public readonly Character Chaser;
            public readonly Character Runner;
            public readonly TeamRoster Roster = new TeamRoster();
            public readonly MatchController Mc;

            public MatchRig(float roleReveal, float headStart, bool populateRoster = true)
            {
                Chaser = ChaserGo.AddComponent<Character>();
                Chaser.SetTeamForTests(Team.Chaser);
                Runner = RunnerGo.AddComponent<Character>();
                Runner.SetTeamForTests(Team.Runner);
                if (populateRoster) Populate();

                Mc = McGo.AddComponent<MatchController>();
                Mc.RoleRevealDurationSeconds = roleReveal;
                Mc.HeadStartDurationSeconds = headStart;
                Mc.MatchDurationSeconds = 90f;
                Mc.Construct(Roster, new MatchEvaluator());
                Mc.InitializeMatch();
            }

            public void Populate()
            {
                Roster.Add(Chaser);
                Roster.Add(Runner);
                Roster.PlayerCharacter = Runner;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(McGo);
                Object.DestroyImmediate(ChaserGo);
                Object.DestroyImmediate(RunnerGo);
            }
        }

        [Test]
        public void RoleReveal_LocksBothTeams_AndFreezesHeadStartAndClock()
        {
            using (var rig = new MatchRig(2.5f, 10f))
            {
                rig.Mc.Step(1f);
                Assert.AreEqual(MatchPhase.RoleReveal, rig.Mc.Phase);
                Assert.AreEqual(1.5f, rig.Mc.RoleRevealRemaining, 1e-3f);
                Assert.AreEqual(10f, rig.Mc.HeadStartRemaining, 1e-3f);
                Assert.AreEqual(90f, rig.Mc.TimeRemaining, 1e-3f);
                Assert.IsTrue(rig.Chaser.IsActionLocked, "Chaser must be locked during role reveal.");
                Assert.IsTrue(rig.Runner.IsActionLocked, "Runner must be locked during role reveal.");
            }
        }

        [Test]
        public void RoleReveal_Ends_ThenHeadStartLocksOnlyChasers()
        {
            using (var rig = new MatchRig(2.5f, 10f))
            {
                rig.Mc.Step(2.5f);
                Assert.AreEqual(MatchPhase.HeadStart, rig.Mc.Phase);
                Assert.AreEqual(10f, rig.Mc.HeadStartRemaining, 1e-3f);
                Assert.IsTrue(rig.Chaser.IsActionLocked, "Chaser stays locked into the head-start.");
                Assert.IsFalse(rig.Runner.IsActionLocked, "Runner is released when the head-start begins.");
            }
        }

        [Test]
        public void RoleRevealZero_StartsDirectlyInHeadStart()
        {
            using (var rig = new MatchRig(0f, 10f))
            {
                Assert.AreEqual(MatchPhase.HeadStart, rig.Mc.Phase);
            }
        }

        [Test]
        public void RoleReveal_PausedDoesNotAdvance()
        {
            using (var rig = new MatchRig(2.5f, 10f))
            {
                rig.Mc.Step(0f);
                rig.Mc.Pause();
                rig.Mc.Step(5f);
                Assert.AreEqual(MatchPhase.RoleReveal, rig.Mc.Phase);
                Assert.AreEqual(2.5f, rig.Mc.RoleRevealRemaining, 1e-3f);

                rig.Mc.Resume();
                rig.Mc.Step(2.5f);
                Assert.AreEqual(MatchPhase.HeadStart, rig.Mc.Phase);
            }
        }

        [Test]
        public void RoleReveal_LocksCharactersThatSpawnAfterFirstStep()
        {
            using (var rig = new MatchRig(2.5f, 10f, populateRoster: false))
            {
                rig.Mc.Step(0.1f);
                rig.Populate();
                rig.Mc.Step(0.1f);
                Assert.IsTrue(rig.Chaser.IsActionLocked);
                Assert.IsTrue(rig.Runner.IsActionLocked);
            }
        }
    }
}
