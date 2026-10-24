using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Brains;
using ChaseGame.Input;
using ChaseGame.Infrastructure;

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

        [Test]
        public void ToCameraRelativeMove_RotatesInputByCameraHorizontalYaw()
        {
            var camGo = new GameObject("Cam");
            try
            {
                // Camera rotated 90 degrees right (looking along +X, pitched down 45 degrees)
                camGo.transform.rotation = Quaternion.Euler(45f, 90f, 0f);

                // Pushing joystick UP (0, 1) should move along camera's planar forward (+X)
                Vector3 moveForward = PlayerBrain.ToCameraRelativeMove(new Vector2(0f, 1f), camGo.transform);
                Assert.AreEqual(1f, moveForward.x, 1e-4f);
                Assert.AreEqual(0f, moveForward.y, 1e-4f);
                Assert.AreEqual(0f, moveForward.z, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void CameraLookTouchPad_LocksToFirstPointer_AndResetsOnConsume()
        {
            var uiGo = new GameObject("TouchPad", typeof(RectTransform));
            try
            {
                var pad = uiGo.AddComponent<CameraLookTouchPad>();

                pad.BeginTouch(pointerId: 1);
                Assert.IsTrue(pad.IsDragging);

                // Second finger (pointerId: 2) tries to drag simultaneously -> ignored
                pad.DragTouch(pointerId: 2, new Vector2(100f, 100f), 1080f);
                Assert.AreEqual(Vector2.zero, pad.PendingLookDelta);

                // First finger (pointerId: 1) drags -> accumulated
                pad.DragTouch(pointerId: 1, new Vector2(50f, -20f), 1080f);
                Vector2 consumed = pad.ConsumeLookDelta();
                Assert.Greater(consumed.x, 0f);
                Assert.Greater(consumed.y, 0f);

                // Consuming resets pending delta so holding a stationary finger stops rotating
                Assert.AreEqual(Vector2.zero, pad.ConsumeLookDelta());

                pad.EndTouch(pointerId: 1);
                Assert.IsFalse(pad.IsDragging);
            }
            finally
            {
                Object.DestroyImmediate(uiGo);
            }
        }

        [Test]
        public void SimpleFollowCamera_ExtractAndComputeOrbitOffset_PreservesInitialOffsetAndClampsPitch()
        {
            Vector3 initialOffset = new Vector3(0f, 3f, -3f);
            SimpleFollowCamera.ExtractOrbitAngles(
                initialOffset,
                minPitchDeg: 8f,
                maxPitchDeg: 75f,
                out float yaw,
                out float pitch,
                out float distance);

            Assert.AreEqual(0f, yaw, 1e-3f);
            Assert.AreEqual(45f, pitch, 1e-3f);

            Vector3 reconstructed = SimpleFollowCamera.ComputeOrbitOffset(yaw, pitch, distance);
            Assert.AreEqual(initialOffset.x, reconstructed.x, 1e-3f);
            Assert.AreEqual(initialOffset.y, reconstructed.y, 1e-3f);
            Assert.AreEqual(initialOffset.z, reconstructed.z, 1e-3f);
        }
    }
}
