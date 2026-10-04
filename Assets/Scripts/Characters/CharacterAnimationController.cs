using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Abilities;

namespace ChaseGame.Characters
{
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimationController : MonoBehaviour
    {
        [SerializeField] private Character character;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterAbilities abilities;

        private Animator animator;
        private ICharacterAbilities iAbilities;
        private ICharacterMovement iMovement;

        // Animator Hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsJailedHash = Animator.StringToHash("IsJailed");
        private static readonly int OnAbilityHash = Animator.StringToHash("OnAbility");
        private static readonly int AbilityIDHash = Animator.StringToHash("AbilityID");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            
            // If references are not assigned in Inspector, try to find them on parent/self
            if (character == null) character = GetComponentInParent<Character>();
            if (movement == null) movement = GetComponentInParent<CharacterMovement>();
            if (abilities == null) abilities = GetComponentInParent<CharacterAbilities>();

            iMovement = movement;
            iAbilities = abilities;
        }

        private void OnEnable()
        {
            if (iAbilities != null)
            {
                iAbilities.OnAbilityUsed += HandleAbilityUsed;
            }
        }

        private void OnDisable()
        {
            if (iAbilities != null)
            {
                iAbilities.OnAbilityUsed -= HandleAbilityUsed;
            }
        }

        private void Update()
        {
            if (character == null) return;

            // 1. Update Speed for Locomotion blend tree
            if (iMovement != null && movement != null)
            {
                // Assuming max speed is roughly BaseSpeed * multiplier
                // We can just use the controller's actual velocity magnitude or the movement's desired speed
                // To keep it simple and independent of physics update rate, we use SpeedMultiplier * BaseSpeed approx
                // However, the best way to get velocity is from CharacterController if attached.
                var cc = character.GetComponent<CharacterController>();
                float currentSpeed = cc != null ? cc.velocity.magnitude : 0f;
                
                // If it's a NavMeshAgent based movement (Runner AI often uses MoveTo), 
                // the velocity might be from NavMeshAgent. Let's use the local plane velocity.
                Vector3 horizontalVelocity = cc != null ? new Vector3(cc.velocity.x, 0, cc.velocity.z) : Vector3.zero;
                animator.SetFloat(SpeedHash, horizontalVelocity.magnitude, 0.1f, Time.deltaTime);
            }

            // 2. Update Capture State (Jailed)
            // animator.SetBool(IsJailedHash, character.CaptureState == CaptureState.Jailed);
        }

        private void HandleAbilityUsed(int abilityId)
        {
            // animator.SetInteger(AbilityIDHash, abilityId);
            // animator.SetTrigger(OnAbilityHash);

            Debug.Log("[Character Animator] Handle Ability Used");
        }
    }
}
