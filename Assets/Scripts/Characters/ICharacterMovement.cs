using UnityEngine;

namespace ChaseGame.Characters
{
    public interface ICharacterMovement
    {
        // Player / direct control.
        void SetMoveDirection(Vector3 direction);

        // AI / NavMesh control: path toward a world destination.
        void MoveTo(Vector3 destination);

        // Zero all motion (also used when a character is jailed).
        void Stop();
    }
}
