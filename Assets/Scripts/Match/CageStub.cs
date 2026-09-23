using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Placeholder for a jailed runner's cage until the real JailSystem lands.
    // Holds who is jailed and where, so the coordinator/roster can reason about cages.
    public sealed class CageStub
    {
        public Character Occupant;
        public Vector3 Position;
    }
}
