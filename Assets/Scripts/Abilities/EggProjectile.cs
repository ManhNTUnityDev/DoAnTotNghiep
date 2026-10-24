using System;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Abilities
{
    /// <summary>
    /// Projectile fired by the Chaser's Gun ability ("quả trứng").
    /// Flies forward along its initial direction up to <see cref="MaxRange"/> and
    /// destroys itself upon hitting a Runner (capturing them), a Decoy clone
    /// (dismissing it), an obstacle, or reaching its maximum range.
    /// </summary>
    public sealed class EggProjectile : MonoBehaviour
    {
        private Character shooter;
        private Vector3 direction = Vector3.forward;
        private float maxRange = 8f;
        private float speed = 22f;
        private float hitRadius = 0.18f;
        private LayerMask hitMask = ~0;
        private ICaptureService capture;
        private float traveledDistance;
        private bool initialized;

        public float MaxRange => maxRange;
        public float Speed => speed;
        public float TraveledDistance => traveledDistance;

        public void Initialize(
            Character shooterCharacter,
            Vector3 moveDirection,
            float range,
            float projectileSpeed,
            float radius,
            LayerMask mask,
            ICaptureService captureService)
        {
            shooter = shooterCharacter;
            direction = moveDirection.sqrMagnitude > 1e-4f ? moveDirection.normalized : Vector3.forward;
            maxRange = Mathf.Max(0.1f, range);
            speed = Mathf.Max(0.5f, projectileSpeed);
            hitRadius = Mathf.Max(0f, radius);
            hitMask = mask;
            capture = captureService;
            traveledDistance = 0f;
            initialized = true;

            // Disable any colliders on the visual egg object itself so it never pushes
            // characters or interferes with CharacterController movement.
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            Step(Time.deltaTime);
        }

        public void Step(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f)
            {
                return;
            }

            float remaining = maxRange - traveledDistance;
            if (remaining <= 0f)
            {
                DestroySelf();
                return;
            }

            float stepDistance = Mathf.Min(speed * deltaTime, remaining);
            if (TrySweepHit(transform.position, direction, stepDistance))
            {
                return;
            }

            transform.position += direction * stepDistance;
            traveledDistance += stepDistance;

            if (traveledDistance >= maxRange - 1e-4f)
            {
                DestroySelf();
            }
        }

        private bool TrySweepHit(Vector3 origin, Vector3 dir, float distance)
        {
            if (distance <= 0f)
            {
                return false;
            }

            RaycastHit[] hits = hitRadius > 1e-3f
                ? Physics.SphereCastAll(origin, hitRadius, dir, distance, hitMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(origin, dir, distance, hitMask, QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                var hitCharacter = hit.collider.GetComponentInParent<Character>();
                if (shooter != null && hitCharacter == shooter)
                {
                    continue;
                }

                // Ignore friendly chasers so teammates do not block each other's egg shots.
                if (hitCharacter != null && hitCharacter.Team == Team.Chaser)
                {
                    continue;
                }

                transform.position = hit.point.sqrMagnitude > 1e-6f ? hit.point : origin + dir * hit.distance;

                var decoy = hit.collider.GetComponentInParent<DecoyLure>();
                if (decoy != null)
                {
                    Debug.Log($"[EggProjectile] Hit decoy clone {decoy.gameObject.name}");
                    decoy.Dismiss();
                    DestroySelf();
                    return true;
                }

                if (Gun.IsCapturable(hitCharacter))
                {
                    capture?.Capture(hitCharacter);
                    Debug.Log($"[EggProjectile] Captured {hitCharacter.gameObject.name}");
                    DestroySelf();
                    return true;
                }

                // Hit a wall/environment obstacle or an already-jailed runner -> egg breaks.
                DestroySelf();
                return true;
            }

            return false;
        }

        private void DestroySelf()
        {
            initialized = false;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
