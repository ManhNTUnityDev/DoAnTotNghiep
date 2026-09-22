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

        [Test]
        public void DesiredToDirection_NearZeroVelocity_IsZero()
        {
            var dir = CharacterMovement.DesiredToDirection(new Vector3(1e-4f, 0f, 0f));
            Assert.AreEqual(Vector3.zero, dir);
        }

        [Test]
        public void DesiredToDirection_NonZero_IsUnitLength()
        {
            var dir = CharacterMovement.DesiredToDirection(new Vector3(0f, 0f, 4f));
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);
            Assert.AreEqual(1f, dir.z, 1e-4f);
        }

        [Test]
        public void ShouldStopAtDestination_WithinStoppingDistance_ReturnsTrue()
        {
            // Arrived: not still computing a path, and inside the stopping radius.
            Assert.IsTrue(CharacterMovement.ShouldStopAtDestination(
                pathPending: false, remainingDistance: 0.1f, stoppingDistance: 0.25f));
        }

        [Test]
        public void ShouldStopAtDestination_BeyondStoppingDistance_ReturnsFalse()
        {
            Assert.IsFalse(CharacterMovement.ShouldStopAtDestination(
                pathPending: false, remainingDistance: 5f, stoppingDistance: 0.25f));
        }

        [Test]
        public void ShouldStopAtDestination_PathStillPending_ReturnsFalse()
        {
            // While the path is being computed remainingDistance reads 0; must not stop early.
            Assert.IsFalse(CharacterMovement.ShouldStopAtDestination(
                pathPending: true, remainingDistance: 0f, stoppingDistance: 0.25f));
        }
    }
}
