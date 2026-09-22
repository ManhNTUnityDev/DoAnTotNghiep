using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.AI
{
    // Dev aid: on Play, tells the sibling Character to path to a target transform,
    // so the hybrid NavMesh movement can be verified before any AI brain exists.
    // Remove or disable once brains drive MoveTo (plan #2).
    [RequireComponent(typeof(Character))]
    public class NavMeshMoveProbe : MonoBehaviour
    {
        [SerializeField] private Transform destination;

        private Character character;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        private void Start()
        {
            if (destination != null)
            {
                character.MoveTo(destination.position);
            }
        }
    }
}
