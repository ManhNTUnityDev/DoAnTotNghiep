using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Abilities
{
    // Runner ability: run faster for a short burst, then revert.
    public sealed class SpeedBoost : MonoBehaviour, IAbility
    {
        [SerializeField] private float multiplier = 1.6f;
        [SerializeField] private float duration = 3f;
        [SerializeField] private float cooldown = 6f;

        private CooldownTimer timer;
        private CharacterMovement movement;
        private float boostEndsAt = -1f;

        private void Awake()
        {
            timer = new CooldownTimer(cooldown);
            movement = GetComponent<CharacterMovement>();
        }

        public bool IsReady => timer == null || timer.IsReady(Time.time);

        public void Use(Character self, Vector3 aim)
        {
            if (timer != null && !timer.IsReady(Time.time))
            {
                return;
            }

            timer?.Trigger(Time.time);
            movement?.SetSpeedMultiplier(multiplier);
            boostEndsAt = Time.time + duration;
        }

        private void Update()
        {
            if (boostEndsAt > 0f && Time.time >= boostEndsAt)
            {
                movement?.SetSpeedMultiplier(1f);
                boostEndsAt = -1f;
            }
        }
    }
}
