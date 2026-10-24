using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.AI;
using ChaseGame.Input;

namespace ChaseGame.Infrastructure
{
    public class SimpleFollowCamera : MonoBehaviour
    {
        [Header("Follow Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
        [SerializeField] private Vector3 lookAtOffset = Vector3.zero;
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Orbit Rotation (UI Touch Pad)")]
        [SerializeField] private CameraLookTouchPad lookTouchPad;
        [SerializeField] private float minPitch = 8f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float rotationSmoothTime = 0.04f;

        [Header("Obstacle Collision")]
        [SerializeField] private bool preventWallClipping = true;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float minDistance = 0.8f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private Vector3 pivotVelocity;
        private Vector3 smoothedPivot;
        private bool hasInitializedPivot;

        private float targetYaw;
        private float targetPitch;
        private float currentYaw;
        private float currentPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private float orbitDistance;
        private bool hasInitializedAngles;

        public float Yaw => currentYaw;
        public float Pitch => currentPitch;
        public float OrbitDistance => orbitDistance;

        private void Awake()
        {
            InitializeAnglesFromOffset();
        }

        // Retargeted at runtime by the SpawnManager once the player character exists.
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                InitializeAnglesFromOffset();
                smoothedPivot = target.position + lookAtOffset;
                pivotVelocity = Vector3.zero;
                hasInitializedPivot = true;
            }
        }

        public void SetLookTouchPad(CameraLookTouchPad touchPad)
        {
            lookTouchPad = touchPad;
        }

        /// <summary>
        /// Directly applies a look delta (x = yaw degrees, y = pitch degrees) to the camera orbit.
        /// </summary>
        public void AddLookDelta(Vector2 lookDelta)
        {
            InitializeAnglesFromOffset();
            targetYaw = Mathf.Repeat(targetYaw + lookDelta.x, 360f);
            targetPitch = Mathf.Clamp(targetPitch + lookDelta.y, minPitch, maxPitch);
        }

        /// <summary>
        /// Pure helper: extracts initial (yaw, pitch, distance) from a cartesian camera offset.
        /// </summary>
        public static void ExtractOrbitAngles(
            Vector3 offsetVector,
            float minPitchDeg,
            float maxPitchDeg,
            out float yawDeg,
            out float pitchDeg,
            out float distance)
        {
            distance = offsetVector.magnitude;
            if (distance < 1e-4f)
            {
                distance = 5f;
                yawDeg = 0f;
                pitchDeg = Mathf.Clamp(35f, minPitchDeg, maxPitchDeg);
                return;
            }

            Vector3 planarLook = new Vector3(-offsetVector.x, 0f, -offsetVector.z);
            yawDeg = planarLook.sqrMagnitude > 1e-6f
                ? Mathf.Repeat(Mathf.Atan2(planarLook.x, planarLook.z) * Mathf.Rad2Deg, 360f)
                : 0f;

            float sinPitch = Mathf.Clamp(offsetVector.y / distance, -1f, 1f);
            pitchDeg = Mathf.Clamp(Mathf.Asin(sinPitch) * Mathf.Rad2Deg, minPitchDeg, maxPitchDeg);
        }

        /// <summary>
        /// Pure helper: computes the world-space offset from the pivot given spherical (yaw, pitch, distance).
        /// </summary>
        public static Vector3 ComputeOrbitOffset(float yawDeg, float pitchDeg, float distance)
        {
            Quaternion orbitRotation = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            return orbitRotation * new Vector3(0f, 0f, -distance);
        }

        private void InitializeAnglesFromOffset()
        {
            if (hasInitializedAngles)
            {
                return;
            }

            ExtractOrbitAngles(offset, minPitch, maxPitch, out targetYaw, out targetPitch, out orbitDistance);
            currentYaw = targetYaw;
            currentPitch = targetPitch;
            hasInitializedAngles = true;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            InitializeAnglesFromOffset();

            CameraLookTouchPad activePad = lookTouchPad != null ? lookTouchPad : CameraLookTouchPad.ActiveInstance;
            if (activePad != null)
            {
                Vector2 delta = activePad.ConsumeLookDelta();
                if (delta.sqrMagnitude > 0f)
                {
                    AddLookDelta(delta);
                }
            }

            if (rotationSmoothTime > 1e-4f)
            {
                currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
                currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
            }
            else
            {
                currentYaw = targetYaw;
                currentPitch = targetPitch;
            }

            Vector3 desiredPivot = target.position + lookAtOffset;
            if (!hasInitializedPivot)
            {
                smoothedPivot = desiredPivot;
                hasInitializedPivot = true;
            }
            else
            {
                smoothedPivot = Vector3.SmoothDamp(smoothedPivot, desiredPivot, ref pivotVelocity, smoothTime);
            }

            Vector3 orbitOffset = ComputeOrbitOffset(currentYaw, currentPitch, orbitDistance);
            float finalDistance = ResolveCollisionDistance(smoothedPivot, orbitOffset, orbitDistance);
            Vector3 finalPosition = smoothedPivot + orbitOffset.normalized * finalDistance;

            transform.position = finalPosition;
            transform.LookAt(smoothedPivot);
        }

        private float ResolveCollisionDistance(Vector3 pivot, Vector3 orbitOffset, float desiredDistance)
        {
            if (!preventWallClipping || desiredDistance <= minDistance)
            {
                return desiredDistance;
            }

            Vector3 dir = orbitOffset.normalized;
            int mask = collisionMask.value != 0 ? collisionMask.value : ~0;

            if (Physics.SphereCast(
                pivot,
                collisionRadius,
                dir,
                out var hit,
                desiredDistance,
                mask,
                QueryTriggerInteraction.Ignore))
            {
                // Ignore characters and decoy lures so they don't push the camera in.
                if (hit.collider.GetComponentInParent<Character>() == null &&
                    hit.collider.GetComponentInParent<DecoyLure>() == null)
                {
                    return Mathf.Clamp(hit.distance - collisionRadius * 0.5f, minDistance, desiredDistance);
                }
            }

            return desiredDistance;
        }
    }
}
