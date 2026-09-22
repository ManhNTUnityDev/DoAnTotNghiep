using UnityEngine;
using UnityEngine.AI;

namespace ChaseGame.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        private enum Mode { Direction, Agent }

        [SerializeField] private CharacterStats stats;

        private CharacterController controller;
        private NavMeshAgent agent;      // optional: present on AI-capable prefabs
        private Vector3 moveDirection;
        private Mode mode = Mode.Direction;

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
                agent.speed = stats != null ? stats.MoveSpeed : 5f;
            }
        }

        public void SetMoveDirection(Vector3 direction)
        {
            mode = Mode.Direction;
            moveDirection = direction;
        }

        public void MoveTo(Vector3 destination)
        {
            if (agent == null)
            {
                return; // no NavMeshAgent on this prefab; MoveTo is a no-op
            }

            mode = Mode.Agent;
            agent.SetDestination(destination);
        }

        public void Stop()
        {
            mode = Mode.Direction;
            moveDirection = Vector3.zero;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private void Update()
        {
            if (mode == Mode.Agent && agent != null)
            {
                moveDirection = ShouldStopAtDestination(agent.pathPending, agent.remainingDistance, agent.stoppingDistance)
                    ? Vector3.zero
                    : DesiredToDirection(agent.desiredVelocity);
            }

            float speed = stats != null ? stats.MoveSpeed : 5f;
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
