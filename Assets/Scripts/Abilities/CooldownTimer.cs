using UnityEngine;

namespace ChaseGame.Abilities
{
    // Pure cooldown gate driven by an external time source (Time.time at runtime),
    // so its logic is unit-testable without the engine clock.
    public sealed class CooldownTimer
    {
        private readonly float cooldown;
        private float readyAt;

        public float Duration => cooldown;

        public CooldownTimer(float cooldown)
        {
            this.cooldown = Mathf.Max(0f, cooldown);
            readyAt = float.NegativeInfinity;
        }

        public bool IsReady(float now) => now >= readyAt;

        public void Trigger(float now) => readyAt = now + cooldown;

        public float Remaining(float now) => Mathf.Max(0f, readyAt - now);

        public float NormalizedRemaining(float now)
            => cooldown > 1e-4f ? Mathf.Clamp01(Remaining(now) / cooldown) : 0f;
    }
}
