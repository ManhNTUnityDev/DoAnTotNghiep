using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class SpeedMultiplierTests
    {
        [Test]
        public void SetSpeedMultiplier_StoresValue_AndClampsNegativeToZero()
        {
            var go = new GameObject("mover");
            go.AddComponent<CharacterController>();
            var mv = go.AddComponent<CharacterMovement>();

            Assert.AreEqual(1f, mv.SpeedMultiplier, 1e-4f); // default

            mv.SetSpeedMultiplier(1.6f);
            Assert.AreEqual(1.6f, mv.SpeedMultiplier, 1e-4f);

            mv.SetSpeedMultiplier(-2f);
            Assert.AreEqual(0f, mv.SpeedMultiplier, 1e-4f);

            Object.DestroyImmediate(go);
        }
    }
}
