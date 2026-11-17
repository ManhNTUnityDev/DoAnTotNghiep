using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.Abilities;
using ChaseGame.AI;

namespace ChaseGame.Tests.PlayMode
{
    // Play-mode proof of Phase 5: a RunnerAIBrain perceives a nearby chaser as a
    // threat and flees away from it (using its escape skills).
    public class RunnerFleePlayTests
    {
        private NavMeshDataInstance navMeshInstance;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private AIConfig config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider>().size = new Vector3(60f, 1f, 60f);

            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(60f, 0.1f, 60f),
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
                    area = 0,
                },
            };
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(80f, 10f, 80f)), Vector3.zero, Quaternion.identity);
            navMeshInstance = NavMesh.AddNavMeshData(data);

            config = ScriptableObject.CreateInstance<AIConfig>(); // threatRadius 6, vision 12
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (navMeshInstance.valid) NavMesh.RemoveNavMeshData(navMeshInstance);
            for (int i = DecoyLure.Active.Count - 1; i >= 0; i--)
            {
                if (DecoyLure.Active[i] != null) Object.Destroy(DecoyLure.Active[i].gameObject);
            }
            DecoyLure.Active.Clear();
            foreach (var go in spawned) { if (go != null) Object.Destroy(go); }
            spawned.Clear();
            if (config != null) Object.DestroyImmediate(config);
        }

        private void AddBody(GameObject go)
        {
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.5f, 0f);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1f; agent.stoppingDistance = 0.3f;
            go.AddComponent<CharacterMovement>();
        }

        [UnityTest]
        public IEnumerator RunnerAI_FleesFromNearbyChaser()
        {
            // Static chaser threat at the origin.
            var chaserGo = new GameObject("Chaser");
            spawned.Add(chaserGo);
            chaserGo.transform.position = new Vector3(0f, 0.2f, 0f);
            AddBody(chaserGo);
            var chaser = chaserGo.AddComponent<Character>();
            chaser.SetTeamForTests(Team.Chaser);

            // Runner with escape abilities, built inactive so Awake sees the full set.
            var runnerGo = new GameObject("Runner");
            spawned.Add(runnerGo);
            runnerGo.SetActive(false);
            runnerGo.transform.position = new Vector3(3f, 0.2f, 0f); // within threatRadius
            AddBody(runnerGo);
            runnerGo.AddComponent<SpeedBoost>();
            runnerGo.AddComponent<Decoy>();
            runnerGo.AddComponent<CharacterAbilities>();
            var runner = runnerGo.AddComponent<Character>();
            runner.SetTeamForTests(Team.Runner);
            runnerGo.SetActive(true);

            var roster = new TeamRoster();
            roster.Add(chaser);
            roster.Add(runner);

            var brain = runnerGo.AddComponent<RunnerAIBrain>();
            brain.Initialize(runner, roster, config);

            for (int i = 0; i < 30; i++) yield return null; // settle

            float startGap = Vector3.Distance(runner.transform.position, chaser.transform.position);
            float maxGap = startGap;
            float t = 0f;
            while (t < 5f)
            {
                t += Time.deltaTime;
                maxGap = Mathf.Max(maxGap, Vector3.Distance(runner.transform.position, chaser.transform.position));
                yield return null;
            }

            // The runner flees to the edge of its ThreatRadius, then resumes wandering,
            // so the peak distance (not the final one) proves it escaped.
            Assert.Greater(maxGap, startGap + 2.5f, "runner should flee substantially away from the chaser");
        }

        [UnityTest]
        public IEnumerator DecoyClone_MovesIndependently_AndDestroysOnGunShotWithoutCreatingCage()
        {
            var roster = new TeamRoster();
            var capture = new JailService();

            var chaserGo = new GameObject("Chaser");
            spawned.Add(chaserGo);
            chaserGo.SetActive(false);
            chaserGo.transform.position = new Vector3(0f, 0.2f, 0f);
            AddBody(chaserGo);
            var gun = chaserGo.AddComponent<Gun>();
            gun.Configure(capture);
            chaserGo.AddComponent<CharacterAbilities>();
            var chaser = chaserGo.AddComponent<Character>();
            chaser.SetTeamForTests(Team.Chaser);
            chaserGo.SetActive(true);
            roster.Add(chaser);

            var runnerGo = new GameObject("Runner");
            spawned.Add(runnerGo);
            runnerGo.SetActive(false);
            runnerGo.transform.position = new Vector3(4f, 0.2f, 0f);
            AddBody(runnerGo);
            var decoy = runnerGo.AddComponent<Decoy>();
            decoy.SetLifetimeForTests(3f);
            decoy.Configure(roster, config);
            runnerGo.AddComponent<CharacterAbilities>();
            var runner = runnerGo.AddComponent<Character>();
            runner.SetTeamForTests(Team.Runner);
            runnerGo.SetActive(true);
            roster.Add(runner);

            yield return null;

            Vector3 spawnOrigin = runner.transform.position;
            decoy.Use(runner, Vector3.forward);

            Assert.IsTrue(decoy.IsCloneActive, "Clone should be active right after Decoy.Use");
            DecoyLure cloneLure = decoy.ActiveClone;
            Assert.IsNotNull(cloneLure);
            spawned.Add(cloneLure.gameObject);

            // Let the clone move independently for ~0.8 seconds
            float elapsed = 0f;
            while (elapsed < 0.8f && cloneLure != null)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(cloneLure, "Clone should still be alive before 3s lifetime");
            float movedDist = AIBrain.PlanarDistance(spawnOrigin, cloneLure.transform.position);
            Assert.Greater(movedDist, 0.5f, "Clone should move independently across the NavMesh");

            // Now have the Chaser shoot directly at the clone's current position
            Vector3 toClone = cloneLure.transform.position - chaser.transform.position;
            toClone.y = 0f;
            // Place chaser within gun range (3m away) and shoot
            chaser.transform.position = cloneLure.transform.position - toClone.normalized * 3f;
            Physics.SyncTransforms();

            gun.Use(chaser, toClone.normalized);
            Assert.IsNotNull(gun.LastSpawnedProjectile, "Gun should spawn an EggProjectile");

            float flightWait = 0f;
            while (flightWait < 0.5f && cloneLure != null)
            {
                flightWait += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(cloneLure == null, "Clone should be destroyed when hit by the flying EggProjectile");
            Assert.IsFalse(gun.IsReady, "Chaser's Gun should be put on cooldown after shooting the clone");
            Assert.AreEqual(0, capture.ActiveCages.Count, "Shooting a decoy clone must NOT create a cage");
        }
    }
}
