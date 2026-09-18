using UnityEngine;

namespace ChaseGame.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        [SerializeField] private CharacterStats stats;

        private CharacterController controller;
        private Vector3 moveDirection;

        public static Vector3 ComputeVelocity(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            return direction.normalized * speed;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public void SetMoveDirection(Vector3 direction)
        {
            moveDirection = direction;
        }

        private void Update()
        {
            float speed = stats != null ? stats.MoveSpeed : 5f;
            Vector3 velocity = ComputeVelocity(moveDirection, speed);
            // simple gravity so the CharacterController stays grounded
            velocity += Physics.gravity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
