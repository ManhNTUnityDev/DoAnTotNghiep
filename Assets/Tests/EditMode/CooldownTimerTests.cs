using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Abilities;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class CooldownTimerTests
    {
        [Test]
        public void IsReady_InitiallyTrue()
        {
            var timer = new CooldownTimer(1f);
            Assert.IsTrue(timer.IsReady(0f));
            Assert.AreEqual(0f, timer.Remaining(0f), 1e-4f);
            Assert.AreEqual(0f, timer.NormalizedRemaining(0f), 1e-4f);
        }

        [Test]
        public void AfterTrigger_NotReadyUntilCooldownElapses()
        {
            var timer = new CooldownTimer(1f);
            timer.Trigger(10f);

            Assert.IsFalse(timer.IsReady(10.5f));
            Assert.AreEqual(0.5f, timer.Remaining(10.5f), 1e-4f);
            Assert.AreEqual(0.5f, timer.NormalizedRemaining(10.5f), 1e-4f);
            Assert.IsTrue(timer.IsReady(11f));
            Assert.IsTrue(timer.IsReady(12f));
            Assert.AreEqual(0f, timer.Remaining(12f), 1e-4f);
        }

        [Test]
        public void AbilityButtonView_ShouldBeVisibleFor_FiltersByRoleAndAbilityCount()
        {
            // Chaser only has 1 ability (index 0 = Gun)
            Assert.IsTrue(AbilityButtonView.ShouldBeVisibleFor(
                Team.Chaser, AbilityButtonView.RoleVisibility.ChaserOnly, index: 0, availableAbilityCount: 1));
            Assert.IsFalse(AbilityButtonView.ShouldBeVisibleFor(
                Team.Chaser, AbilityButtonView.RoleVisibility.RunnerOnly, index: 0, availableAbilityCount: 1));
            Assert.IsFalse(AbilityButtonView.ShouldBeVisibleFor(
                Team.Chaser, AbilityButtonView.RoleVisibility.AnyRole, index: 1, availableAbilityCount: 1));

            // Runner has 2 abilities (0 = SpeedBoost, 1 = Decoy)
            Assert.IsTrue(AbilityButtonView.ShouldBeVisibleFor(
                Team.Runner, AbilityButtonView.RoleVisibility.RunnerOnly, index: 0, availableAbilityCount: 2));
            Assert.IsTrue(AbilityButtonView.ShouldBeVisibleFor(
                Team.Runner, AbilityButtonView.RoleVisibility.RunnerOnly, index: 1, availableAbilityCount: 2));
        }

        [Test]
        public void AbilityButtonView_FormatCooldown_FormatsDecimalsUnderTenSeconds()
        {
            Assert.AreEqual(string.Empty, AbilityButtonView.FormatCooldown(0f));
            Assert.AreEqual("4.2", AbilityButtonView.FormatCooldown(4.21f));
            Assert.AreEqual("12", AbilityButtonView.FormatCooldown(11.3f));
        }

        [Test]
        public void AbilityButtonView_ComputeChaserAimDirection_SnapsToRunnerInCone()
        {
            var go = new GameObject("FreeRunner");
            try
            {
                go.transform.position = new Vector3(1.5f, 0f, 5f); // ~16.7 deg off +Z forward
                var runner = go.AddComponent<Character>();
                runner.SetTeamForTests(Team.Runner);

                Vector3 aim = AbilityButtonView.ComputeChaserAimDirection(
                    Vector3.zero,
                    Vector3.forward,
                    new List<Character> { runner },
                    maxRange: 8f,
                    maxAngleDeg: 40f);

                Vector3 expected = new Vector3(1.5f, 0f, 5f).normalized;
                Assert.AreEqual(expected.x, aim.x, 1e-4f);
                Assert.AreEqual(expected.z, aim.z, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
