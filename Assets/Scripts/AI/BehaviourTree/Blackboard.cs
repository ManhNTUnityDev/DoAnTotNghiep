using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.AI.BehaviourTree
{
    // Shared data bag passed to every node. The core node types never read these
    // directly — only the delegates authored in a brain's BuildTree() do.
    public class Blackboard
    {
        public Character Self;
        public ITeamRoster Roster;
        public AIConfig Config;

        public Transform CurrentTarget;  // chaser: nearest visible free runner
        public Transform NearestThreat;  // runner: nearest visible chaser

        public Vector3? Destination;     // scratch for wander / flee targets
        public Vector3? GuardPost;       // chaser: cage post assigned by ChaserCoordinator
    }
}
