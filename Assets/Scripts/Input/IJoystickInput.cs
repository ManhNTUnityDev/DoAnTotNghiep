using UnityEngine;

namespace ChaseGame.Input
{
    // On-screen joystick source. Implemented by an Assembly-CSharp adapter that
    // wraps the third-party UltimateJoystick, so ChaseGame stays free of that dependency.
    public interface IJoystickInput
    {
        // True while the player is actively touching/dragging the joystick.
        bool Active { get; }

        // Raw joystick axis, each component in [-1, 1].
        Vector2 Axis { get; }
    }
}
