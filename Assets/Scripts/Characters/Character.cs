using UnityEngine;

namespace ChaseGame.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Chaser;

        private ICharacterMovement movement;
        private ICharacterAbilities abilities;
        private CaptureState captureState = CaptureState.Free;
        private bool isActionLocked;

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

        /// <summary>
        /// Locks movement and ability usage (e.g. Chasers waiting during the pre-match head-start)
        /// without marking the character as Jailed.
        /// </summary>
        public bool IsActionLocked
        {
            get => isActionLocked;
            set
            {
                bool enteringLock = value && !isActionLocked;
                isActionLocked = value;
                if (enteringLock)
                {
                    movement?.Stop();
                }
            }
        }

        public bool CanAct => captureState != CaptureState.Jailed && !isActionLocked;

        private void Awake()
        {
            // Resolve sibling Body components if a test hasn't injected them.
            movement ??= GetComponent<ICharacterMovement>();
            abilities ??= GetComponent<ICharacterAbilities>();
        }

        public void Move(Vector3 direction)
        {
            if (!CanAct)
            {
                return;
            }

            movement?.SetMoveDirection(direction);
        }

        public void MoveTo(Vector3 destination)
        {
            if (!CanAct)
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
            if (!CanAct)
            {
                return;
            }

            abilities?.UseAbility(id, aim);
        }

        public bool IsAbilityReady(int id) => CanAct && abilities != null && abilities.IsReady(id);

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
