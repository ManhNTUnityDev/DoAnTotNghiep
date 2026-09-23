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
    }
}
