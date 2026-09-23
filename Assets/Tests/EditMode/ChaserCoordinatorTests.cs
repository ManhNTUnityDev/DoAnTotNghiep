using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class ChaserCoordinatorTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            spawned.Clear();
        }

        private Blackboard ChaserAt(Vector3 pos)
        {
            var go = new GameObject("Chaser");
            spawned.Add(go);
            go.transform.position = pos;
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(Team.Chaser);
            return new Blackboard { Self = ch };
        }

        private static CageStub CageAt(Vector3 pos) => new CageStub { Position = pos };

        [Test]
        public void Assign_NoCages_ClearsAllPosts()
        {
            var a = ChaserAt(Vector3.zero);
            a.GuardPost = new Vector3(9f, 0f, 9f); // stale from a previous tick
            var chasers = new List<Blackboard> { a };

            ChaserCoordinator.Assign(chasers, new List<CageStub>());

            Assert.IsFalse(a.GuardPost.HasValue);
        }

        [Test]
        public void Assign_OneCage_NearestChaserGetsPost()
        {
            var near = ChaserAt(new Vector3(1f, 0f, 0f));
            var far = ChaserAt(new Vector3(20f, 0f, 0f));
            var cage = CageAt(new Vector3(2f, 0f, 0f));

            ChaserCoordinator.Assign(new List<Blackboard> { near, far }, new List<CageStub> { cage });

            Assert.AreEqual(cage.Position, near.GuardPost);
            Assert.IsFalse(far.GuardPost.HasValue, "only the nearest chaser guards");
        }

        [Test]
        public void Assign_TwoCages_TwoDistinctChasersEachGuardNearest()
        {
            var west = ChaserAt(new Vector3(-10f, 0f, 0f));
            var east = ChaserAt(new Vector3(10f, 0f, 0f));
            var cageWest = CageAt(new Vector3(-9f, 0f, 0f));
            var cageEast = CageAt(new Vector3(9f, 0f, 0f));

            ChaserCoordinator.Assign(
                new List<Blackboard> { west, east },
                new List<CageStub> { cageWest, cageEast });

            Assert.AreEqual(cageWest.Position, west.GuardPost);
            Assert.AreEqual(cageEast.Position, east.GuardPost);
        }

        [Test]
        public void Assign_FewerChasersThanCages_DoesNotDoubleAssign()
        {
            var only = ChaserAt(Vector3.zero);
            var cageA = CageAt(new Vector3(1f, 0f, 0f));
            var cageB = CageAt(new Vector3(2f, 0f, 0f));

            ChaserCoordinator.Assign(
                new List<Blackboard> { only },
                new List<CageStub> { cageA, cageB });

            // The single chaser guards its nearest cage; the second cage is unguarded.
            Assert.AreEqual(cageA.Position, only.GuardPost);
        }

        [Test]
        public void Assign_NullInputs_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ChaserCoordinator.Assign(null, null));
            var a = ChaserAt(Vector3.zero);
            Assert.DoesNotThrow(() => ChaserCoordinator.Assign(new List<Blackboard> { a }, null));
        }
    }
}
