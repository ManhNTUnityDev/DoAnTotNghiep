using NUnit.Framework;
using UnityEngine;
using ChaseGame.Input;

namespace ChaseGame.Tests
{
    public class InputSystemServiceTests
    {
        private FakeJoystickInput joystick;
        private InputSystemService service;

        [SetUp]
        public void SetUp()
        {
            joystick = new FakeJoystickInput();
            service = new InputSystemService(joystick);
        }

        [Test]
        public void MoveAxis_WhenJoystickActive_UsesJoystickAxis()
        {
            joystick.Active = true;
            joystick.Axis = new Vector2(1f, 0f);

            Assert.AreEqual(1f, service.MoveAxis.x, 1e-4f);
            Assert.AreEqual(0f, service.MoveAxis.y, 1e-4f);
        }

        [Test]
        public void MoveAxis_WhenJoystickActive_ClampsMagnitudeToOne()
        {
            joystick.Active = true;
            joystick.Axis = new Vector2(3f, 4f); // magnitude 5

            Assert.AreEqual(1f, service.MoveAxis.magnitude, 1e-4f);
        }

        [Test]
        public void MoveAxis_WhenJoystickInactive_IgnoresJoystickAxis()
        {
            // Joystick reports an axis but is not being touched: must fall through to
            // device input (no keys pressed in EditMode => zero), never the joystick axis.
            joystick.Active = false;
            joystick.Axis = new Vector2(1f, 1f);

            Assert.AreEqual(Vector2.zero, service.MoveAxis);
        }
    }
}
