using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Abilities;

namespace ChaseGame.Tests
{
    public class GunTargetingTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private Character NewChar(Team team, CaptureState state)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var c = go.AddComponent<Character>();
            c.SetTeamForTests(team);
            c.CaptureState = state;
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void IsCapturable_FreeRunner_True()
            => Assert.IsTrue(Gun.IsCapturable(NewChar(Team.Runner, CaptureState.Free)));

        [Test]
        public void IsCapturable_JailedRunner_False()
            => Assert.IsFalse(Gun.IsCapturable(NewChar(Team.Runner, CaptureState.Jailed)));

        [Test]
        public void IsCapturable_Chaser_False()
            => Assert.IsFalse(Gun.IsCapturable(NewChar(Team.Chaser, CaptureState.Free)));

        [Test]
        public void IsCapturable_Null_False()
            => Assert.IsFalse(Gun.IsCapturable(null));

        [Test]
        public void IsCapturable_RunnerWithDecoyLure_False()
        {
            var clone = NewChar(Team.Runner, CaptureState.Free);
            clone.gameObject.AddComponent<ChaseGame.AI.DecoyLure>();

            Assert.IsFalse(Gun.IsCapturable(clone), "A decoy clone must never be capturable into a cage");
        }

        [Test]
        public void GunUse_SpawnsEggProjectile_WhichFliesForwardAndDestroysAtMaxRange()
        {
            var chaser = NewChar(Team.Chaser, CaptureState.Free);
            var gun = chaser.gameObject.AddComponent<Gun>();
            gun.SetRangeForTests(6f);

            gun.Use(chaser, Vector3.forward);

            var egg = gun.LastSpawnedProjectile;
            Assert.IsNotNull(egg, "Gun.Use should spawn an EggProjectile");
            spawned.Add(egg.gameObject);
            Assert.AreEqual(6f, egg.MaxRange, 1e-4f);

            // Step partially (e.g. ~2.2 meters at 22m/s -> 0.1s)
            egg.Step(0.1f);
            Assert.IsTrue(egg != null, "Egg should still exist before reaching max range");
            Assert.Greater(egg.TraveledDistance, 2f);

            // Step beyond remaining range -> egg should self-destruct
            egg.Step(1f);
            Assert.IsTrue(egg == null, "Egg should destroy itself upon reaching max range");
        }
    }
}
