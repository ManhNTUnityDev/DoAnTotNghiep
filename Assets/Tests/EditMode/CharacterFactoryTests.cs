using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.Abilities;
using ChaseGame.Infrastructure;

namespace ChaseGame.Tests
{
    public class CharacterFactoryTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private TeamRoster roster;
        private CaptureServiceStub capture;
        private CharacterFactory factory;

        [SetUp]
        public void SetUp()
        {
            roster = new TeamRoster();
            capture = new CaptureServiceStub();
            factory = new CharacterFactory(roster, capture);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            spawned.Clear();
        }

        private Character MakePrefab(Team team, bool withGun)
        {
            var go = new GameObject(team + "Prefab");
            spawned.Add(go);
            if (withGun)
            {
                go.AddComponent<Gun>();
                go.AddComponent<CharacterAbilities>();
            }

            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            return ch;
        }

        [Test]
        public void Spawn_ReturnsNewInstance_NotThePrefab()
        {
            var prefab = MakePrefab(Team.Runner, withGun: false);

            var instance = factory.Spawn(prefab, Vector3.zero, Quaternion.identity);
            spawned.Add(instance.gameObject);

            Assert.AreNotSame(prefab, instance);
        }

        [Test]
        public void Spawn_RegistersChaserWithRoster()
        {
            var prefab = MakePrefab(Team.Chaser, withGun: true);

            var instance = factory.Spawn(prefab, Vector3.zero, Quaternion.identity);
            spawned.Add(instance.gameObject);

            Assert.AreEqual(1, roster.Chasers.Count);
            Assert.AreSame(instance, roster.Chasers[0]);
            Assert.AreEqual(0, roster.Runners.Count);
        }

        [Test]
        public void Spawn_RegistersRunnerWithRoster()
        {
            var prefab = MakePrefab(Team.Runner, withGun: false);

            var instance = factory.Spawn(prefab, Vector3.zero, Quaternion.identity);
            spawned.Add(instance.gameObject);

            Assert.AreEqual(1, roster.Runners.Count);
            Assert.AreSame(instance, roster.Runners[0]);
            Assert.AreEqual(0, roster.Chasers.Count);
        }

        [Test]
        public void Spawn_PlacesInstanceAtRequestedPose()
        {
            var prefab = MakePrefab(Team.Runner, withGun: false);
            var pos = new Vector3(3f, 0f, -4f);

            var instance = factory.Spawn(prefab, pos, Quaternion.identity);
            spawned.Add(instance.gameObject);

            Assert.AreEqual(pos, instance.transform.position);
        }

        [Test]
        public void Spawn_RunnerWithoutGun_DoesNotThrow()
        {
            var prefab = MakePrefab(Team.Runner, withGun: false);

            Assert.DoesNotThrow(() =>
            {
                var instance = factory.Spawn(prefab, Vector3.zero, Quaternion.identity);
                spawned.Add(instance.gameObject);
            });
        }
    }
}
