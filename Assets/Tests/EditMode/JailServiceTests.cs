using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class JailServiceTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private JailService service;

        [SetUp]
        public void SetUp()
        {
            service = new JailService();
        }

        private Character NewChar(Team team, CaptureState state = CaptureState.Free, Vector3 position = default)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            go.transform.position = position;
            var c = go.AddComponent<Character>();
            c.SetTeamForTests(team);
            c.CaptureState = state;
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var cage in service.ActiveCages)
            {
                if (cage != null) Object.DestroyImmediate(cage.gameObject);
            }
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void Capture_FreeRunner_JailsAndAddsCageAndRaisesEvent()
        {
            var runner = NewChar(Team.Runner, CaptureState.Free);
            Character caught = null;
            service.OnRunnerCaught += c => caught = c;

            service.Capture(runner);

            Assert.AreEqual(CaptureState.Jailed, runner.CaptureState);
            Assert.AreEqual(1, service.ActiveCages.Count);
            Assert.AreSame(runner, service.ActiveCages[0].Occupant);
            Assert.AreSame(runner, caught);
        }

        [Test]
        public void Capture_Chaser_IsIgnored()
        {
            var chaser = NewChar(Team.Chaser, CaptureState.Free);

            service.Capture(chaser);

            Assert.AreEqual(CaptureState.Free, chaser.CaptureState);
            Assert.AreEqual(0, service.ActiveCages.Count);
        }

        [Test]
        public void Capture_AlreadyJailed_IsIgnored()
        {
            var runner = NewChar(Team.Runner, CaptureState.Jailed);

            service.Capture(runner);

            Assert.AreEqual(0, service.ActiveCages.Count);
        }

        [Test]
        public void Capture_SameRunnerTwice_DoesNotStackCages()
        {
            var runner = NewChar(Team.Runner, CaptureState.Free);

            service.Capture(runner);
            service.Capture(runner);

            Assert.AreEqual(1, service.ActiveCages.Count, "a runner can only be caged once");
        }

        [Test]
        public void Capture_BuildsCageAtTheRunnerPosition()
        {
            var runner = NewChar(Team.Runner, CaptureState.Free, new Vector3(4f, 0f, -3f));

            service.Capture(runner);

            // No NavMesh in EditMode, so the cage lands exactly where the runner stood.
            Assert.AreEqual(new Vector3(4f, 0f, -3f), service.ActiveCages[0].Position);
        }

        [Test]
        public void Capture_JailedRunner_StopsAcceptingCommands()
        {
            var runner = NewChar(Team.Runner, CaptureState.Free);

            service.Capture(runner);

            Assert.IsFalse(runner.CanAct, "a caged runner must not accept move or ability commands");
        }

        [Test]
        public void PrimitiveCage_HasBlockingColliderAndNavMeshObstacle()
        {
            var runner = NewChar(Team.Runner, CaptureState.Free);

            service.Capture(runner);
            var cage = service.ActiveCages[0];

            Assert.IsNotNull(cage.GetComponent<BoxCollider>(), "other characters must not walk through a cage");
            var obstacle = cage.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            Assert.IsNotNull(obstacle, "NPC agents need the cage as an obstacle");
            Assert.IsFalse(obstacle.carving, "carving would invalidate the baked NavMesh mid-match");
            Assert.Greater(cage.transform.childCount, 0, "the placeholder cage builds its bars as children");
        }
    }
}
