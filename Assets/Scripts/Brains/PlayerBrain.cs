using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Brains
{
    public class PlayerBrain : CharacterBrain
    {
        private IInputService input;
        private Transform cameraTransform;

        public void Initialize(Character character, IInputService inputService, Transform viewCamera = null)
        {
            Initialize(character);
            input = inputService;
            cameraTransform = viewCamera;
        }

        public static Vector3 ToWorldMove(Vector2 axis)
        {
            return new Vector3(axis.x, 0f, axis.y);
        }

        public static Vector3 ToCameraRelativeMove(Vector2 axis, Transform cam)
        {
            if (cam == null)
            {
                return ToWorldMove(axis);
            }

            Vector3 forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            return right * axis.x + forward * axis.y;
        }

        public void Tick()
        {
            if (input == null || Character == null)
            {
                return;
            }

            Character.Move(ToCameraRelativeMove(input.MoveAxis, cameraTransform));
        }

        private void Update()
        {
            Tick();
        }
    }
}
