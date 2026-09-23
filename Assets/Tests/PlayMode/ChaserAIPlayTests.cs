using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Tests.PlayMode
{
    // Play-mode proof of the first autonomous NPC (spec Phase 3): a ChaserAIBrain
    // perceives a free runner and hunts it across a runtime-baked NavMesh.
    public class ChaserAIPlayTests
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
            var settings = NavMesh.GetSettingsByID(0);
            var bounds = new Bounds(Vector3.zero, new Vector3(60f, 10f, 60f));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            navMeshInstance = NavMesh.AddNavMeshData(data);

            config = ScriptableObject.CreateInstance<AIConfig>(); // defaults: visionRadius 12
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

        private Character MakeBody(string name, Team team, Vector3 pos)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.transform.position = pos;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.5f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1f; agent.stoppingDistance = 0.3f;

            go.AddComponent<CharacterMovement>();
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            return ch;
        }

        [UnityTest]
        public IEnumerator ChaserAI_HuntsFreeRunner()
        {
            // 10m apart, inside the default 12m vision radius.
            var runner = MakeBody("Runner", Team.Runner, new Vector3(5f, 0.2f, 0f));
            var chaser = MakeBody("Chaser", Team.Chaser, new Vector3(-5f, 0.2f, 0f));

            var roster = new TeamRoster();
            roster.Add(runner);
            roster.Add(chaser);

            var brain = chaser.gameObject.AddComponent<ChaserAIBrain>();
            brain.Initialize(chaser, roster, config);

            // settle onto the ground/navmesh
            for (int i = 0; i < 30; i++) yield return null;
            var start = chaser.transform.position;

            float t = 0f;
            while (t < 6f) { t += Time.deltaTime; yield return null; }
            var end = chaser.transform.position;

            float startGap = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(runner.transform.position.x, runner.transform.position.z));
            float endGap = Vector2.Distance(new Vector2(end.x, end.z), new Vector2(runner.transform.position.x, runner.transform.position.z));

            Assert.Greater(startGap, 8f, "sanity: chaser starts far from the runner");
            Assert.Less(endGap, startGap - 5f, "chaser should close a large part of the gap");
            Assert.Less(endGap, 3f, "chaser should end adjacent to the runner");
        }
    }
}
