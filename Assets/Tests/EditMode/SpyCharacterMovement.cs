using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class SpyCharacterMovement : ICharacterMovement
    {
        public int CallCount { get; private set; }
        public Vector3 LastDirection { get; private set; }

        public void SetMoveDirection(Vector3 direction)
        {
            CallCount++;
            LastDirection = direction;
        }
    }
}
