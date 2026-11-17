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
        private bool hasJailedParameter;

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
            hasJailedParameter = HasBoolParameter(animator, "IsJailed");
        }

        // Guards against controllers that don't declare IsJailed (Unity logs a warning
        // every frame for a missing parameter).
        private static bool HasBoolParameter(Animator target, string parameterName)
        {
            if (target == null || target.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (var parameter in target.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
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

            // 1. Update Speed for Locomotion blend tree.
            // Speed is NORMALIZED to 0..1 (current planar speed / current max speed), not raw m/s:
            // every locomotion blend tree in the project is authored against 0..1 thresholds
            // (NPC.controller uses 0 / 0.5 / 1 with m_MaxThreshold: 1). Feeding raw m/s pinned
            // every character to its top clip and made the walk clips dead code.
            if (iMovement != null && movement != null)
            {
                var cc = character.GetComponent<CharacterController>();

                // If it's a NavMeshAgent based movement (Runner AI often uses MoveTo), 
                // the velocity might be from NavMeshAgent. Let's use the local plane velocity.
                Vector3 horizontalVelocity = cc != null ? new Vector3(cc.velocity.x, 0, cc.velocity.z) : Vector3.zero;

                // MaxSpeed already includes the SpeedBoost multiplier, so a boosted character does
                // not jump straight to 1: the clip keeps matching leg speed relative to the body.
                animator.SetFloat(
                    SpeedHash,
                    NormalizeSpeed(horizontalVelocity.magnitude, movement.MaxSpeed),
                    0.1f,
                    Time.deltaTime);
            }

            // 2. Update Capture State (Jailed) — drives the caged idle pose.
            if (hasJailedParameter)
            {
                animator.SetBool(IsJailedHash, character.CaptureState == CaptureState.Jailed);
            }
        }

        /// <summary>Raised by the footstep AnimationEvent; a footstep sound can hook on here.</summary>
        public event System.Action OnFootstep;

        /// <summary>
        /// Receiver for the "PlayFootstep" AnimationEvent baked into Armature_Chic_FPP_Walk.
        /// Unity delivers animation events only to components sitting on the Animator's own
        /// GameObject, which is exactly where this component lives on both visual prefabs.
        ///
        /// Nothing in the project defined this method, which went unnoticed for as long as the
        /// chaser had no Animator Controller at all - the clip never played, so the event never
        /// fired. Now that it plays, Unity logged "has no receiver!" on every single step.
        /// </summary>
        public void PlayFootstep()
        {
            OnFootstep?.Invoke();
        }

        // Pure helper: current planar speed as a 0..1 fraction of the character's current top speed.
        public static float NormalizeSpeed(float planarSpeed, float maxSpeed)
        {
            if (maxSpeed <= 0.01f)
            {
                return 0f;
            }

            return Mathf.Clamp01(planarSpeed / maxSpeed);
        }

        private void HandleAbilityUsed(int abilityId)
        {
            // ResetTrigger first: abilities with no clip of their own (Chaser AbilityID 1 =
            // SpeedBoost, Runner AbilityID 0 = SpeedBoost) set OnAbility with no transition to
            // consume it, so the trigger stays latched and would fire at the wrong moment on the
            // NEXT ability whose AbilityID does have a transition.
            animator.ResetTrigger(OnAbilityHash);
            animator.SetInteger(AbilityIDHash, abilityId);
            animator.SetTrigger(OnAbilityHash);
        }
    }
}
