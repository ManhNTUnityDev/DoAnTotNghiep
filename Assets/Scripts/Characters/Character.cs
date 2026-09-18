using UnityEngine;

namespace ChaseGame.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Chaser;

        private ICharacterMovement movement;

        public Team Team => team;
        public CaptureState CaptureState { get; set; } = CaptureState.Free;

        private void Awake()
        {
            // Resolve the sibling movement component if a test hasn't injected one.
            movement ??= GetComponent<ICharacterMovement>();
        }

        public void Move(Vector3 direction)
        {
            if (CaptureState == CaptureState.Jailed)
            {
                return;
            }

            movement?.SetMoveDirection(direction);
        }

        // Test seam: inject a movement double without a CharacterController.
        public void SetMovementForTests(ICharacterMovement injected)
        {
            movement = injected;
        }
    }
}
