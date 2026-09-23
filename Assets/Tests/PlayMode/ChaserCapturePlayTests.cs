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
    // Play-mode proof of Phase 4: a ChaserAIBrain hunts a free runner, and once in
    // shooting range its Gun captures the runner via the ICaptureService stub (jailed).
    public class ChaserCapturePlayTests
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
            ground.AddComponent<BoxCollider>().size = new Vector3(40f, 1f, 40f);

            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(40f, 0.1f, 40f),
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
                    area = 0,
                },
            };
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(60f, 10f, 60f)), Vector3.zero, Quaternion.identity);
            navMeshInstance = NavMesh.AddNavMeshData(data);

            config = ScriptableObject.CreateInstance<AIConfig>(); // vision 12, shootRange 8
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (navMeshInstance.valid) NavMesh.RemoveNavMeshData(navMeshInstance);
            foreach (var go in spawned) { if (go != null) Object.Destroy(go); }
            spawned.Clear();
            if (config != null) Object.DestroyImmediate(config);
        }

        private static void AddBody(GameObject go)
        {
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.5f, 0f);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1f; agent.stoppingDistance = 0.3f;
            go.AddComponent<CharacterMovement>();
        }

        [UnityTest]
        public IEnumerator ChaserAI_ShootsAndJailsFreeRunner()
        {
            // Runner: a plain free-runner body.
            var runnerGo = new GameObject("Runner");
            spawned.Add(runnerGo);
            runnerGo.transform.position = new Vector3(5f, 0.2f, 0f);
            AddBody(runnerGo);
            var runner = runnerGo.AddComponent<Character>();
            runner.SetTeamForTests(Team.Runner);

            // Chaser: built inactive so all Awakes see the full component set
            // (Character <-> CharacterAbilities <-> Gun resolve each other).
            var chaserGo = new GameObject("Chaser");
            spawned.Add(chaserGo);
            chaserGo.SetActive(false);
            chaserGo.transform.position = new Vector3(-5f, 0.2f, 0f);
            AddBody(chaserGo);
            var gun = chaserGo.AddComponent<Gun>();
            chaserGo.AddComponent<CharacterAbilities>();
            var chaser = chaserGo.AddComponent<Character>();
            chaser.SetTeamForTests(Team.Chaser);
            chaserGo.SetActive(true);

            var capture = new CaptureServiceStub();
            gun.Configure(capture);

            var roster = new TeamRoster();
            roster.Add(runner);
            roster.Add(chaser);

            var brain = chaserGo.AddComponent<ChaserAIBrain>();
            brain.Initialize(chaser, roster, config);

            for (int i = 0; i < 30; i++) yield return null; // settle

            float t = 0f;
            while (t < 6f && runner.CaptureState == CaptureState.Free)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(CaptureState.Jailed, runner.CaptureState, "runner should be jailed by the chaser's shot");
            Assert.AreEqual(1, capture.ActiveCages.Count, "a cage should be recorded for the capture");
        }
    }
}
