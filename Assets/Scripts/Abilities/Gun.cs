using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Abilities
{
    // Chaser ability: shoots an egg projectile forward along the aim direction up to
    // `range` meters (configurable in Inspector). Striking a free runner triggers a
    // capture (via ICaptureService); striking a Runner's DecoyLure clone destroys the clone.
    public sealed class Gun : MonoBehaviour, IAbility
    {
        [Header("Range & Cooldown")]
        [Tooltip("Maximum distance (meters) the egg projectile travels before disappearing.")]
        [SerializeField] private float range = 8f;
        [SerializeField] private float cooldown = 1f;

        [Header("Egg Projectile")]
        [Tooltip("Prefab for the egg projectile (e.g. a Sphere prefab). Falls back to a runtime Sphere if unassigned.")]
        [SerializeField] private GameObject eggProjectilePrefab;
        [Tooltip("Flight speed (meters/second) of the fired egg projectile.")]
        [SerializeField] private float projectileSpeed = 22f;
        [Tooltip("Collision sweep radius of the egg projectile.")]
        [SerializeField] private float projectileRadius = 0.18f;
        [SerializeField] private float eyeHeight = 0.6f;
        [SerializeField] private float muzzleForwardOffset = 0.6f;
        [SerializeField] private LayerMask hitMask = ~0;

        private ICaptureService capture;
        private CooldownTimer timer;

        public EggProjectile LastSpawnedProjectile { get; private set; }

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
        public float CooldownRemaining => timer != null ? timer.Remaining(Time.time) : 0f;
        public float CooldownNormalized => timer != null ? timer.NormalizedRemaining(Time.time) : 0f;
        public float Range => range;
        public float ProjectileSpeed => projectileSpeed;

        public void SetRangeForTests(float newRange)
        {
            range = Mathf.Max(0.5f, newRange);
        }

        public static bool IsCapturable(Character c)
            => c != null
               && c.Team == Team.Runner
               && c.CaptureState == CaptureState.Free
               && c.GetComponent<DecoyLure>() == null;

        public void Use(Character self, Vector3 aim)
        {
            if (self == null)
            {
                return;
            }

            if (timer != null && !timer.IsReady(Time.time))
            {
                return;
            }

            timer?.Trigger(Time.time);

            Vector3 dir = aim.sqrMagnitude > 1e-4f ? aim.normalized : self.transform.forward;
            if (dir.sqrMagnitude < 1e-4f)
            {
                dir = Vector3.forward;
            }

            // Start beyond the shooter's own body so the projectile spawns clearly in front of the Chaser.
            Vector3 origin = self.transform.position + Vector3.up * eyeHeight + dir * muzzleForwardOffset;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            GameObject eggGo;
            if (eggProjectilePrefab != null)
            {
                eggGo = Instantiate(eggProjectilePrefab, origin, rot);
                eggGo.name = "EggProjectile";
            }
            else
            {
                eggGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eggGo.name = "EggProjectile";
                eggGo.transform.SetPositionAndRotation(origin, rot);
                eggGo.transform.localScale = new Vector3(0.28f, 0.36f, 0.28f);
            }

            var projectile = eggGo.GetComponent<EggProjectile>();
            if (projectile == null)
            {
                projectile = eggGo.AddComponent<EggProjectile>();
            }

            projectile.Initialize(
                self,
                dir,
                range,
                projectileSpeed,
                projectileRadius,
                hitMask,
                capture);

            LastSpawnedProjectile = projectile;
        }
    }
}
