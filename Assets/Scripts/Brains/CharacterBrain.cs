using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Brains
{
    public abstract class CharacterBrain : MonoBehaviour
    {
        protected Character Character { get; private set; }

        public void Initialize(Character character)
        {
            Character = character;
        }
    }
}
