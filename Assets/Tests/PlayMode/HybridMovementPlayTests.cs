using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using ChaseGame.Characters;

namespace ChaseGame.Tests.PlayMode
{
    // Play-mode proof of the hybrid NavMesh movement (spec §5 / plan Task 7):
    // a NavMeshAgent computes the path, the CharacterController does the moving,
    // and the character settles at the destination without oscillating.
    // The NavMesh is built at runtime so the test needs no baked scene.
    public class HybridMovementPlayTests
    {
        private NavMeshDataInstance navMeshInstance;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Real ground collider so the CharacterController has something to stand on.
            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(40f, 1f, 40f);

            // Build a NavMesh over the ground surface (top at y = 0) at runtime.
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
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (navMeshInstance.valid)
            {
                NavMesh.RemoveNavMeshData(navMeshInstance);
            }
            foreach (var go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();
        }

        private Character MakeCharacter(Vector3 pos)
        {
            var go = new GameObject("TestChaser");
            spawned.Add(go);
            go.transform.position = pos;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.5f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1f;
            agent.stoppingDistance = 0.3f;

            go.AddComponent<CharacterMovement>(); // stats null → default speed 5
            return go.AddComponent<Character>();
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
            => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [UnityTest]
        public IEnumerator MoveTo_WalksTowardDestination_AndSettles()
        {
            var ch = MakeCharacter(new Vector3(-8f, 0.2f, 0f));

            // Let Awake wire the agent and let the controller settle onto the ground.
            for (int i = 0; i < 30; i++) yield return null;

            var start = ch.transform.position;
            var dest = new Vector3(8f, 0f, 0f);
            ch.MoveTo(dest);

            float t = 0f;
            while (t < 6f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            var end = ch.transform.position;
            float startDist = PlanarDistance(start, dest);
            float endDist = PlanarDistance(end, dest);

            Assert.Greater(startDist, 10f, "sanity: starts far from the destination");
            Assert.Less(endDist, startDist - 8f, "should travel substantially toward the destination");
            Assert.Less(endDist, 1f, "should arrive at (and settle near) the destination");
        }

        [UnityTest]
        public IEnumerator Stop_HaltsAgentMovement()
        {
            var ch = MakeCharacter(new Vector3(-8f, 0.2f, 0f));
            for (int i = 0; i < 30; i++) yield return null;

            ch.MoveTo(new Vector3(8f, 0f, 0f));
            for (int i = 0; i < 30; i++) yield return null; // move for ~0.5s

            ch.Stop();
            var afterStop = ch.transform.position;
            for (int i = 0; i < 30; i++) yield return null; // wait another ~0.5s

            float drift = PlanarDistance(afterStop, ch.transform.position);
            Assert.Less(drift, 0.5f, "Stop() should halt agent-driven movement");
        }
    }
}
