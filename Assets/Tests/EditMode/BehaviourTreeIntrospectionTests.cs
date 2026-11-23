using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.BehaviourTree;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class BehaviourTreeIntrospectionTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private AIConfig config;

        [SetUp]
        public void SetUp() { config = ScriptableObject.CreateInstance<AIConfig>(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
            Object.DestroyImmediate(config);
        }

        private T Make<T>(Team team) where T : BehaviourTreeBrain
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            var brain = go.AddComponent<T>();
            brain.Initialize(ch, new TeamRoster(), config);
            return brain;
        }

        [Test]
        public void CountNodes_ChaserTree_Is8()
        {
            Assert.AreEqual(8, BehaviourTreeStats.CountNodes(Make<ChaserAIBrain>(Team.Chaser).Root));
        }

        [Test]
        public void CountNodes_RunnerTree_Is5()
        {
            Assert.AreEqual(5, BehaviourTreeStats.CountNodes(Make<RunnerAIBrain>(Team.Runner).Root));
        }

        [Test]
        public void CountNodes_SingleLeaf_Is1()
        {
            Assert.AreEqual(1, BehaviourTreeStats.CountNodes(new ActionNode(_ => NodeStatus.Success)));
        }
    }
}
