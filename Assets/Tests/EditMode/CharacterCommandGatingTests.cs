using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class CharacterCommandGatingTests
    {
        private Character character;
        private SpyCharacterMovement spy;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Character");
            character = go.AddComponent<Character>();
            spy = new SpyCharacterMovement();
            character.SetMovementForTests(spy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character.gameObject);
        }

        [Test]
        public void Move_WhenFree_ForwardsToMovement()
        {
            character.CaptureState = CaptureState.Free;
            character.Move(Vector3.forward);

            Assert.AreEqual(1, spy.CallCount);
            Assert.AreEqual(Vector3.forward, spy.LastDirection);
        }

        [Test]
        public void Move_WhenJailed_IsNoOp()
        {
            character.CaptureState = CaptureState.Jailed;
            character.Move(Vector3.forward);

            Assert.AreEqual(0, spy.CallCount);
        }

        [Test]
        public void CaptureState_DefaultsToFree()
        {
            var go = new GameObject("Fresh");
            var fresh = go.AddComponent<Character>();
            Assert.AreEqual(CaptureState.Free, fresh.CaptureState);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MoveTo_WhenFree_ForwardsToMovement()
        {
            character.CaptureState = CaptureState.Free;
            character.MoveTo(new Vector3(3f, 0f, 4f));

            Assert.AreEqual(1, spy.MoveToCallCount);
            Assert.AreEqual(new Vector3(3f, 0f, 4f), spy.LastDestination);
        }

        [Test]
        public void MoveTo_WhenJailed_IsNoOp()
        {
            character.CaptureState = CaptureState.Jailed;
            character.MoveTo(new Vector3(3f, 0f, 4f));

            Assert.AreEqual(0, spy.MoveToCallCount);
        }

        [Test]
        public void EnteringJail_StopsMovementOnce()
        {
            character.CaptureState = CaptureState.Free;
            character.CaptureState = CaptureState.Jailed;
            Assert.AreEqual(1, spy.StopCallCount);

            // Re-asserting Jailed does not Stop again.
            character.CaptureState = CaptureState.Jailed;
            Assert.AreEqual(1, spy.StopCallCount);
        }

        [Test]
        public void ReleasingAndReJailing_StopsAgain()
        {
            character.CaptureState = CaptureState.Jailed;
            character.CaptureState = CaptureState.Free;
            character.CaptureState = CaptureState.Jailed;
            Assert.AreEqual(2, spy.StopCallCount);
        }
    }
}
