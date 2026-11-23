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
    }
}
