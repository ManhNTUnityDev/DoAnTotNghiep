using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Infrastructure;

namespace ChaseGame.Tests
{
    public class FirstPersonViewTests
    {
        private const float MinPitch = -70f;
        private const float MaxPitch = 70f;

        // --- yaw wraps -------------------------------------------------------------------

        [Test]
        public void Yaw_AccumulatesWithinTurn()
        {
            Assert.AreEqual(35f, FirstPersonView.AddYaw(20f, 15f), 1e-3f);
        }

        [Test]
        public void Yaw_WrapsPastFullTurn()
        {
            Assert.AreEqual(10f, FirstPersonView.AddYaw(350f, 20f), 1e-3f);
        }

        [Test]
        public void Yaw_WrapsBelowZero()
        {
            Assert.AreEqual(350f, FirstPersonView.AddYaw(10f, -20f), 1e-3f);
        }

        // --- pitch clamps ----------------------------------------------------------------

        // CameraLookTouchPad emits a NEGATIVE y for an upward drag, and negative euler X looks up,
        // so dragging up must decrease pitch.
        [Test]
        public void Pitch_DragUpLooksUp()
        {
            Assert.AreEqual(-10f, FirstPersonView.AddPitch(0f, -10f, MinPitch, MaxPitch), 1e-3f);
        }

        [Test]
        public void Pitch_DragDownLooksDown()
        {
            Assert.AreEqual(10f, FirstPersonView.AddPitch(0f, 10f, MinPitch, MaxPitch), 1e-3f);
        }

        // Clamping rather than wrapping is the point: the player must never roll the view over.
        [Test]
        public void Pitch_ClampsLookingUp()
        {
            Assert.AreEqual(MinPitch, FirstPersonView.AddPitch(-60f, -45f, MinPitch, MaxPitch), 1e-3f);
        }

        [Test]
        public void Pitch_ClampsLookingDown()
        {
            Assert.AreEqual(MaxPitch, FirstPersonView.AddPitch(60f, 45f, MinPitch, MaxPitch), 1e-3f);
        }

        [Test]
        public void Pitch_NeverWrapsAround()
        {
            float pitch = 0f;
            for (int i = 0; i < 100; i++)
            {
                pitch = FirstPersonView.AddPitch(pitch, 10f, MinPitch, MaxPitch);
            }

            Assert.AreEqual(MaxPitch, pitch, 1e-3f);
        }

        // --- rig wiring ------------------------------------------------------------------

        [Test]
        public void Configure_StoresAnchorAndVisualFlag()
        {
            var root = new GameObject("Character");
            try
            {
                var anchor = new GameObject("EyeAnchor").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(0f, 0.55f, 0f);

                var view = root.AddComponent<FirstPersonView>();
                view.Configure(anchor, true);

                Assert.AreSame(anchor, view.EyeAnchor);
                Assert.IsTrue(view.HidesOwnVisual);
                Assert.IsFalse(view.IsActive, "a configured rig is still inert until Activate");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A first-person character must not also steer itself toward its movement direction, or the
        // view spins whenever the joystick pushes sideways.
        [Test]
        public void Movement_FaceMoveDirectionCanBeTurnedOffAndBackOn()
        {
            var go = new GameObject("Character");
            try
            {
                go.AddComponent<CharacterController>();
                var movement = go.AddComponent<CharacterMovement>();

                Assert.IsTrue(movement.FaceMoveDirection, "AI default: body follows movement");

                movement.SetFaceMoveDirection(false);
                Assert.IsFalse(movement.FaceMoveDirection);

                // Decoy restores it on a clone copied from a first-person player.
                movement.SetFaceMoveDirection(true);
                Assert.IsTrue(movement.FaceMoveDirection);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
