using UnityEngine;

namespace ChaseGame.Characters
{
    // The Body's ability seam: the Brain asks whether an ability is ready and to
    // use it by index. Concrete abilities live in ChaseGame.Abilities.
    public interface ICharacterAbilities
    {
        bool IsReady(int id);
        void UseAbility(int id, Vector3 aim);
    }
}
