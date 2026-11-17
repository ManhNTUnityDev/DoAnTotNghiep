using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Infrastructure
{
    /// <summary>
    /// First-person view driver, authored on a character prefab and left disabled until
    /// SpawnManager activates it for the one character the human drives.
    ///
    /// It splits the look: <b>yaw goes on the body</b> (so the character faces where the player
    /// looks, which is also what Gun fires along and what the world model faces) and
    /// <b>pitch goes on the camera only</b>. That means CharacterMovement must stop turning the
    /// body toward its movement direction, or the view would spin every time the joystick pushes
    /// sideways - Activate turns that off.
    ///
    /// The camera is <b>positioned, not parented</b>, to the eye anchor each LateUpdate.
    /// Parenting would read better, but Decoy.Use clones the whole character GameObject and a
    /// parented camera would be cloned along with it. Writing the transform directly is just as
    /// tight (same frame phase, no smoothing) and keeps the camera out of the cloned hierarchy.
    /// </summary>
    public sealed class FirstPersonView : MonoBehaviour
    {
        [Tooltip("Where the camera sits. Chicken eyes are around y=0.55; the human runner's are near y=1.7.")]
        [SerializeField] private Transform eyeAnchor;

        [Tooltip("Hide this character's own renderers. Off for the chaser, whose view model is wings " +
                 "and legs meant to be seen from inside; on for the runner, which has only a full body.")]
        [SerializeField] private bool hideOwnVisual;

        [Tooltip("Look angles as Unity euler X: negative looks up, positive looks down.")]
        [SerializeField] private float minPitch = -70f;
        [SerializeField] private float maxPitch = 70f;

        [Tooltip("Near clip for first person. The default 0.3 slices off the character's own wings and feet.")]
        [SerializeField] private float nearClipPlane = 0.05f;

        private Transform cameraTransform;
        private CameraLookTouchPad lookTouchPad;
        private CharacterMovement movement;
        private float yaw;
        private float pitch;
        private bool active;

        public bool IsActive => active;
        public Transform EyeAnchor => eyeAnchor;
        public bool HidesOwnVisual => hideOwnVisual;
        public float Yaw => yaw;
        public float Pitch => pitch;

        // Set by the editor script that builds the prefabs, or by a test.
        public void Configure(Transform anchor, bool hideVisual)
        {
            eyeAnchor = anchor;
            hideOwnVisual = hideVisual;
        }

        /// <summary>Pure: yaw wraps, so it never drifts out of range however long the player plays.</summary>
        public static float AddYaw(float currentYaw, float deltaDegrees)
        {
            return Mathf.Repeat(currentYaw + deltaDegrees, 360f);
        }

        /// <summary>
        /// Pure: pitch clamps instead of wrapping, so the player cannot roll the view over the top.
        /// CameraLookTouchPad already emits a negative y for an upward drag, which is "look up" in
        /// euler X, so the delta is added as-is.
        /// </summary>
        public static float AddPitch(float currentPitch, float deltaDegrees, float min, float max)
        {
            return Mathf.Clamp(currentPitch + deltaDegrees, min, max);
        }

        public void Activate(Transform cameraToDrive, CameraLookTouchPad pad)
        {
            if (cameraToDrive == null)
            {
                Debug.LogWarning("[FirstPersonView] No camera to drive; staying in third person.");
                return;
            }

            cameraTransform = cameraToDrive;
            lookTouchPad = pad;

            // The orbit camera writes the same transform in its own LateUpdate, and the order
            // between two components' LateUpdate is undefined, so it has to be switched off rather
            // than merely out-written.
            foreach (var orbit in cameraToDrive.GetComponents<SimpleFollowCamera>())
            {
                orbit.enabled = false;
            }

            foreach (var cam in cameraToDrive.GetComponents<Camera>())
            {
                cam.nearClipPlane = nearClipPlane;
            }

            movement = GetComponent<CharacterMovement>();
            if (movement != null)
            {
                movement.SetFaceMoveDirection(false);
            }

            yaw = transform.eulerAngles.y;
            pitch = 0f;

            if (hideOwnVisual)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }
            }

            active = true;
            ApplyToTransforms();
        }

        private void LateUpdate()
        {
            // A Decoy clone inherits this component but never gets Activated (cameraTransform is a
            // runtime field), so it stays inert and the clone keeps being driven by its AI brain.
            if (!active || cameraTransform == null)
            {
                return;
            }

            var pad = lookTouchPad != null ? lookTouchPad : CameraLookTouchPad.ActiveInstance;
            if (pad != null)
            {
                Vector2 delta = pad.ConsumeLookDelta();
                if (delta.sqrMagnitude > 0f)
                {
                    yaw = AddYaw(yaw, delta.x);
                    pitch = AddPitch(pitch, delta.y, minPitch, maxPitch);
                }
            }

            ApplyToTransforms();
        }

        private void ApplyToTransforms()
        {
            // Body first: the eye anchor is a child, so its world position follows immediately.
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 eye = eyeAnchor != null
                ? eyeAnchor.position
                : transform.position + Vector3.up * 0.6f;

            cameraTransform.SetPositionAndRotation(eye, Quaternion.Euler(pitch, yaw, 0f));
        }
    }
}
