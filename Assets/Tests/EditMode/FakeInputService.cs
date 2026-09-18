using UnityEngine;
using ChaseGame.Input;

namespace ChaseGame.Tests
{
    public class FakeInputService : IInputService
    {
        public Vector2 MoveAxis { get; set; }
    }
}
