using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Brains
{
    public class PlayerBrain : CharacterBrain
    {
        private IInputService input;

        public void Initialize(Character character, IInputService inputService)
        {
            Initialize(character);
            input = inputService;
        }

        public static Vector3 ToWorldMove(Vector2 axis)
        {
            return new Vector3(axis.x, 0f, axis.y);
        }

        public void Tick()
        {
            if (input == null || Character == null)
            {
                return;
            }

            Character.Move(ToWorldMove(input.MoveAxis));
        }

        private void Update()
        {
            Tick();
        }
    }
}
