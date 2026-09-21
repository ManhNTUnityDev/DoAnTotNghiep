using UnityEngine;
using UnityEngine.InputSystem;

namespace ChaseGame.Input
{
    public class InputSystemService : IInputService
    {
        private readonly IJoystickInput joystick;

        public InputSystemService(IJoystickInput joystick)
        {
            this.joystick = joystick;
        }

        public Vector2 MoveAxis
        {
            get
            {
                // On-screen joystick wins while it is being touched (mobile).
                if (joystick != null && joystick.Active)
                {
                    return Vector2.ClampMagnitude(joystick.Axis, 1f);
                }

                Vector2 axis = Vector2.zero;

                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) axis.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) axis.y -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis.x += 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis.x -= 1f;
                }

                var gamepad = Gamepad.current;
                if (gamepad != null && axis == Vector2.zero)
                {
                    axis = gamepad.leftStick.ReadValue();
                }

                return Vector2.ClampMagnitude(axis, 1f);
            }
        }
    }
}
