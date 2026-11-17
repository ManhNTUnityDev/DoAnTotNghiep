using NUnit.Framework;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    // Every locomotion blend tree in the project is authored against 0..1 thresholds
    // (NPC.controller: 0 / 0.5 / 1 with m_MaxThreshold 1; Chic.controller: 0 / 0.625 / 1).
    // Feeding raw m/s pinned every character to its top clip, so Speed must arrive normalized.
    public class CharacterAnimationSpeedTests
    {
        [Test]
        public void StandingStill_IsZero()
        {
            Assert.AreEqual(0f, CharacterAnimationController.NormalizeSpeed(0f, 8f), 1e-4f);
        }

        [Test]
        public void ChaserWalking_LandsOnTheWalkThreshold()
        {
            // base 5 m/s out of a top speed of 8 m/s (SpeedBoost multiplier 1.6)
            Assert.AreEqual(0.625f, CharacterAnimationController.NormalizeSpeed(5f, 8f), 1e-4f);
        }

        [Test]
        public void AtTopSpeed_IsOne()
        {
            Assert.AreEqual(1f, CharacterAnimationController.NormalizeSpeed(8f, 8f), 1e-4f);
        }

        // MaxSpeed already includes the boost multiplier, so a boosted character keeps roughly the
        // same normalized value instead of jumping to 1 - the clip stays matched to leg speed
        // relative to the body.
        [Test]
        public void BoostedCharacter_DoesNotJumpToOne()
        {
            Assert.AreEqual(0.625f, CharacterAnimationController.NormalizeSpeed(8f, 12.8f), 1e-4f);
        }

        [Test]
        public void OvershootIsClamped()
        {
            Assert.AreEqual(1f, CharacterAnimationController.NormalizeSpeed(99f, 8f), 1e-4f);
        }

        [Test]
        public void ZeroMaxSpeed_DoesNotDivideByZero()
        {
            Assert.AreEqual(0f, CharacterAnimationController.NormalizeSpeed(5f, 0f), 1e-4f);
        }
    }
}
