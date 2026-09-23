using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Abilities
{
    // Body component that owns the role's ordered ability set (the IAbility
    // MonoBehaviours on this GameObject). The Brain drives it by index.
    public sealed class CharacterAbilities : MonoBehaviour, ICharacterAbilities
    {
        private IAbility[] abilities;
        private Character character;

        private void Awake()
        {
            character = GetComponent<Character>();
            abilities = GetComponents<IAbility>();
        }

        public bool IsReady(int id)
            => abilities != null && id >= 0 && id < abilities.Length && abilities[id].IsReady;

        public void UseAbility(int id, Vector3 aim)
        {
            if (abilities == null || id < 0 || id >= abilities.Length)
            {
                return;
            }

            var ability = abilities[id];
            if (ability.IsReady)
            {
                ability.Use(character, aim);
            }
        }
    }
}
