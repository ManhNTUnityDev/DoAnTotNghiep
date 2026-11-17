using UnityEngine;
using UnityEngine.AI;

namespace ChaseGame.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        private enum Mode { Direction, Agent }

        [SerializeField] private CharacterStats stats;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float steeringSmoothRate = 16f;

        [Tooltip("Turn the body toward its movement direction. AI needs this; a first-person " +
                 "character does not, because FirstPersonView owns its yaw - leaving both on makes " +
                 "the view spin every time the joystick pushes sideways.")]
        [SerializeField] private bool faceMoveDirection = true;

        private CharacterController controller;
        private NavMeshAgent agent;      // optional: present on AI-capable prefabs
        private Vector3 moveDirection;
        private Vector3 lastDestination;
        private bool hasDestination;
        private Mode mode = Mode.Direction;
        private float speedMultiplier = 1f;

        public float SpeedMultiplier => speedMultiplier;

        private float BaseSpeed => stats != null ? stats.MoveSpeed : 5f;

        // Current top speed, boost included. Read by CharacterAnimationController to normalize
        // the Animator's Speed parameter to 0..1.
        public float MaxSpeed => BaseSpeed * speedMultiplier;

        public bool FaceMoveDirection => faceMoveDirection;

        // FirstPersonView turns this off for the character it drives; Decoy turns it back on for a
        // clone copied from a first-person player, which has no FirstPersonView of its own.
        public void SetFaceMoveDirection(bool value)
        {
            faceMoveDirection = value;
        }

        // Temporary speed scaling (e.g. SpeedBoost). Keeps agent.speed in sync so the
        // path-steering desiredVelocity matches the body's actual speed.
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
            if (agent != null)
            {
                agent.speed = BaseSpeed * speedMultiplier;
            }
        }

        public static Vector3 ComputeVelocity(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            return direction.normalized * speed;
        }

        // Turn a NavMeshAgent desiredVelocity into a unit move direction (or zero
        // when the agent has essentially arrived, so the character does not jitter).
        public static Vector3 DesiredToDirection(Vector3 desiredVelocity)
        {
            if (desiredVelocity.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            return desiredVelocity.normalized;
        }

        // Smooths high-frequency RVO avoidance direction changes while stopping
        // immediately when targetDirection is zero.
        public static Vector3 SmoothDirection(Vector3 currentDirection, Vector3 targetDirection, float smoothRate, float deltaTime)
        {
            if (targetDirection.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            if (currentDirection.sqrMagnitude < 1e-6f || deltaTime <= 0f)
            {
                return targetDirection.normalized;
            }

            float t = Mathf.Clamp01(smoothRate * deltaTime);
            return Vector3.Slerp(currentDirection.normalized, targetDirection.normalized, t).normalized;
        }

        // Smoothly turns the character on the horizontal plane toward its movement direction.
        public static Quaternion ComputeNextRotation(
            Quaternion currentRotation,
            Vector3 direction,
            float turnSpeedDegPerSec,
            float deltaTime)
        {
            Vector3 planar = new Vector3(direction.x, 0f, direction.z);
            if (planar.sqrMagnitude < 1e-4f || deltaTime <= 0f)
            {
                return currentRotation;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planar.normalized, Vector3.up);
            return Quaternion.RotateTowards(currentRotation, targetRotation, turnSpeedDegPerSec * deltaTime);
        }

        // Because the body is driven at full speed (magnitude is discarded), the
        // near-zero desiredVelocity clamp alone can let the controller overshoot the
        // destination and oscillate. Once the path is resolved and we are within the
        // agent's stopping distance, stop cleanly instead of steering by desiredVelocity.
        public static bool ShouldStopAtDestination(bool pathPending, float remainingDistance, float stoppingDistance)
        {
            return !pathPending && remainingDistance <= stoppingDistance;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                // The agent only computes the path; the CharacterController does the moving.
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.speed = BaseSpeed * speedMultiplier;
            }
        }

        public void SetMoveDirection(Vector3 direction)
        {
            mode = Mode.Direction;
            hasDestination = false;
            moveDirection = direction;
        }

        public void MoveTo(Vector3 destination)
        {
            if (agent == null)
            {
                return; // no NavMeshAgent on this prefab; MoveTo is a no-op
            }

            mode = Mode.Agent;

            // Avoid resetting the NavMesh path 8 times/sec when destination hasn't meaningfully changed.
            if (hasDestination &&
                (agent.hasPath || agent.pathPending) &&
                (destination - lastDestination).sqrMagnitude < 0.0225f)
            {
                return;
            }

            lastDestination = destination;
            hasDestination = true;
            agent.SetDestination(destination);
        }

        public void Stop()
        {
            mode = Mode.Direction;
            hasDestination = false;
            moveDirection = Vector3.zero;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private void Update()
        {
            float speed = BaseSpeed * speedMultiplier;

            if (mode == Mode.Agent && agent != null)
            {
                agent.speed = speed;
                Vector3 rawDirection = ShouldStopAtDestination(agent.pathPending, agent.remainingDistance, agent.stoppingDistance)
                    ? Vector3.zero
                    : DesiredToDirection(agent.desiredVelocity);

                moveDirection = SmoothDirection(moveDirection, rawDirection, steeringSmoothRate, Time.deltaTime);
            }

            if (faceMoveDirection && moveDirection.sqrMagnitude > 1e-4f)
            {
                transform.rotation = ComputeNextRotation(transform.rotation, moveDirection, turnSpeed, Time.deltaTime);
            }

            Vector3 velocity = ComputeVelocity(moveDirection, speed);
            velocity += Physics.gravity; // simple gravity keeps the controller grounded
            controller.Move(velocity * Time.deltaTime);

            // Keep the (non-moving) agent in step with where the controller actually is,
            // so path steering stays correct.
            if (agent != null && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }
    }
}
