using UnityEngine;
using ChaseGame.Input;

namespace ChaseGame.Tests
{
    public class FakeJoystickInput : IJoystickInput
    {
        public bool Active { get; set; }
        public Vector2 Axis { get; set; }
    }
}
