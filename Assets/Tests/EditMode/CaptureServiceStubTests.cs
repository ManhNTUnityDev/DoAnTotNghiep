using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class CaptureServiceStubTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private Character NewChar(Team team, CaptureState state = CaptureState.Free)
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
        public void Capture_FreeRunner_JailsAndAddsCageAndRaisesEvent()
        {
            var svc = new CaptureServiceStub();
            var runner = NewChar(Team.Runner, CaptureState.Free);
            Character caught = null;
            svc.OnRunnerCaught += c => caught = c;

            svc.Capture(runner);

            Assert.AreEqual(CaptureState.Jailed, runner.CaptureState);
            Assert.AreEqual(1, svc.ActiveCages.Count);
            Assert.AreSame(runner, svc.ActiveCages[0].Occupant);
            Assert.AreSame(runner, caught);
        }

        [Test]
        public void Capture_Chaser_IsIgnored()
        {
            var svc = new CaptureServiceStub();
            var chaser = NewChar(Team.Chaser, CaptureState.Free);

            svc.Capture(chaser);

            Assert.AreEqual(CaptureState.Free, chaser.CaptureState);
            Assert.AreEqual(0, svc.ActiveCages.Count);
        }

        [Test]
        public void Capture_AlreadyJailed_IsIgnored()
        {
            var svc = new CaptureServiceStub();
            var runner = NewChar(Team.Runner, CaptureState.Jailed);

            svc.Capture(runner);

            Assert.AreEqual(0, svc.ActiveCages.Count);
        }
    }
}
