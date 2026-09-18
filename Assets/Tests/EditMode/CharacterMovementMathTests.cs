using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class CharacterMovementMathTests
    {
        [Test]
        public void ComputeVelocity_ZeroDirection_IsZero()
        {
            var v = CharacterMovement.ComputeVelocity(Vector3.zero, 5f);
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void ComputeVelocity_NormalizesDirectionAndScalesBySpeed()
        {
            var v = CharacterMovement.ComputeVelocity(new Vector3(3f, 0f, 0f), 5f);
            Assert.AreEqual(5f, v.x, 1e-4f);
            Assert.AreEqual(0f, v.z, 1e-4f);
        }

        [Test]
        public void ComputeVelocity_DiagonalHasSpeedMagnitude()
        {
            var v = CharacterMovement.ComputeVelocity(new Vector3(1f, 0f, 1f), 5f);
            Assert.AreEqual(5f, v.magnitude, 1e-4f);
        }
    }
}
