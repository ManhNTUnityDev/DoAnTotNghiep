using UnityEngine;

namespace ChaseGame.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Chaser;

        private ICharacterMovement movement;
        private ICharacterAbilities abilities;
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
            // Resolve sibling Body components if a test hasn't injected them.
            movement ??= GetComponent<ICharacterMovement>();
            abilities ??= GetComponent<ICharacterAbilities>();
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

        public void UseAbility(int id, Vector3 aim)
        {
            if (captureState == CaptureState.Jailed)
            {
                return;
            }

            abilities?.UseAbility(id, aim);
        }

        public bool IsAbilityReady(int id) => abilities != null && abilities.IsReady(id);

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

        // Test seam: inject an abilities double.
        public void SetAbilitiesForTests(ICharacterAbilities injected)
        {
            abilities = injected;
        }
    }
}
