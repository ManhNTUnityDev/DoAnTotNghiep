using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Brains;

namespace ChaseGame.Tests
{
    public class PlayerBrainTests
    {
        private Character character;
        private SpyCharacterMovement spy;
        private PlayerBrain brain;
        private FakeInputService input;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Character");
            character = go.AddComponent<Character>();
            spy = new SpyCharacterMovement();
            character.SetMovementForTests(spy);

            input = new FakeInputService();
            brain = go.AddComponent<PlayerBrain>();
            brain.Initialize(character, input);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character.gameObject);
        }

        [Test]
        public void ToWorldMove_MapsAxisXToWorldXAndAxisYToWorldZ()
        {
            var world = PlayerBrain.ToWorldMove(new Vector2(1f, 0.5f));
            Assert.AreEqual(1f, world.x, 1e-4f);
            Assert.AreEqual(0f, world.y, 1e-4f);
            Assert.AreEqual(0.5f, world.z, 1e-4f);
        }

        [Test]
        public void Tick_ForwardsInputAsWorldMove()
        {
            input.MoveAxis = new Vector2(0f, 1f);
            brain.Tick();

            Assert.AreEqual(1, spy.CallCount);
            Assert.AreEqual(Vector3.forward, spy.LastDirection);
        }

        [Test]
        public void Tick_WhenJailed_DoesNotMove()
        {
            character.CaptureState = CaptureState.Jailed;
            input.MoveAxis = new Vector2(0f, 1f);
            brain.Tick();

            Assert.AreEqual(0, spy.CallCount);
        }
    }
}
