using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class SpyCharacterMovement : ICharacterMovement
    {
        public int CallCount { get; private set; }
        public Vector3 LastDirection { get; private set; }

        public int MoveToCallCount { get; private set; }
        public Vector3 LastDestination { get; private set; }

        public int StopCallCount { get; private set; }

        public void SetMoveDirection(Vector3 direction)
        {
            CallCount++;
            LastDirection = direction;
        }

        public void MoveTo(Vector3 destination)
        {
            MoveToCallCount++;
            LastDestination = destination;
        }

        public void Stop()
        {
            StopCallCount++;
        }
    }
}
