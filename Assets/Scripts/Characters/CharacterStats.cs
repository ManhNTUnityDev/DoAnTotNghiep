using UnityEngine;

namespace ChaseGame.Characters
{
    [CreateAssetMenu(menuName = "ChaseGame/Character Stats", fileName = "CharacterStats")]
    public class CharacterStats : ScriptableObject
    {
        [SerializeField] private float moveSpeed = 5f;

        public float MoveSpeed => moveSpeed;
    }
}
