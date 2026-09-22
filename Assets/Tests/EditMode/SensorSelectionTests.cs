using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class SensorSelectionTests
    {
        [Test]
        public void Blackboard_HoldsConfigAndTargets()
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            var bb = new Blackboard { Config = config };

            Assert.AreEqual(8f, bb.Config.TickRateHz, 1e-4f); // default from AIConfig
            Assert.IsNull(bb.CurrentTarget);

            Object.DestroyImmediate(config);
        }
    }
}
