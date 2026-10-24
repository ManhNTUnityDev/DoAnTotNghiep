using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Abilities
{
    public interface IAbility
    {
        bool IsReady { get; }
        float CooldownRemaining { get; }
        float CooldownNormalized { get; }
        void Use(Character self, Vector3 aim);
    }
}
