using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Abilities
{
    // Chaser ability: hitscan shot. A ray from the shooter toward the aim direction
    // that strikes a free runner triggers a capture (via ICaptureService).
    public sealed class Gun : MonoBehaviour, IAbility
    {
        [SerializeField] private float range = 8f;
        [SerializeField] private float cooldown = 1f;
        [SerializeField] private float eyeHeight = 0.6f;
        [SerializeField] private LayerMask hitMask = ~0;

        private ICaptureService capture;
        private CooldownTimer timer;

        private void Awake()
        {
            timer = new CooldownTimer(cooldown);
        }

        // Injected at spawn (or by a test) so the Gun can report captures.
        public void Configure(ICaptureService captureService)
        {
            capture = captureService;
        }

        public bool IsReady => timer == null || timer.IsReady(Time.time);

        public static bool IsCapturable(Character c)
            => c != null && c.Team == Team.Runner && c.CaptureState == CaptureState.Free;

        public void Use(Character self, Vector3 aim)
        {
            if (timer != null && !timer.IsReady(Time.time))
            {
                return;
            }

            timer?.Trigger(Time.time);

            Vector3 dir = aim.sqrMagnitude > 1e-4f ? aim.normalized : self.transform.forward;
            // Start beyond the shooter's own body so the ray does not hit self.
            Vector3 origin = self.transform.position + Vector3.up * eyeHeight + dir * 0.6f;

            if (Physics.Raycast(origin, dir, out var hit, range, hitMask))
            {
                var target = hit.collider.GetComponentInParent<Character>();
                if (IsCapturable(target))
                {
                    capture?.Capture(target);
                }
            }
        }
    }
}
