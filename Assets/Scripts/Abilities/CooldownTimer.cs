namespace ChaseGame.Abilities
{
    // Pure cooldown gate driven by an external time source (Time.time at runtime),
    // so its logic is unit-testable without the engine clock.
    public sealed class CooldownTimer
    {
        private readonly float cooldown;
        private float readyAt;

        public CooldownTimer(float cooldown)
        {
            this.cooldown = cooldown;
            readyAt = float.NegativeInfinity;
        }

        public bool IsReady(float now) => now >= readyAt;

        public void Trigger(float now) => readyAt = now + cooldown;
    }
}
