using UnityEngine;

namespace ChaseGame.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Chaser;

        private ICharacterMovement movement;
        private CaptureState captureState = CaptureState.Free;

        public Team Team => team;

        public CaptureState CaptureState
        {
            get => captureState;
            set
            {
                bool enteringJail = value == CaptureState.Jailed && captureState != CaptureState.Jailed;
                captureState = value;
                if (enteringJail)
                {
                    movement?.Stop(); // don't slide while jailed (spec §5)
                }
            }
        }

        private void Awake()
        {
            // Resolve the sibling movement component if a test hasn't injected one.
            movement ??= GetComponent<ICharacterMovement>();
        }

        public void Move(Vector3 direction)
        {
            if (captureState == CaptureState.Jailed)
            {
                return;
            }

            movement?.SetMoveDirection(direction);
        }

        public void MoveTo(Vector3 destination)
        {
            if (captureState == CaptureState.Jailed)
            {
                return;
            }

            movement?.MoveTo(destination);
        }

        public void Stop()
        {
            movement?.Stop();
        }

        // Test seam: inject a movement double without a CharacterController.
        public void SetMovementForTests(ICharacterMovement injected)
        {
            movement = injected;
        }

        // Test seam: set the team without a prefab/SerializedObject.
        public void SetTeamForTests(Team value)
        {
            team = value;
        }
    }
}
