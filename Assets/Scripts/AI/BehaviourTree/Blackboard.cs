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

        public Vector3? Destination;     // scratch for wander / last-known targets
        public bool IsFleeing;           // runner: hysteresis flag to prevent ping-pong at threat boundary
        public bool IsClone;             // runner decoy clone: disables skills and diverges flee path
        public float CloneFleeAngleBias; // horizontal angle offset (degrees) so clone splits from real runner

        public float Now;                // seconds, set by the host before each decision
        public BehaviourId ActiveBehaviour; // what the NPC is doing now; set by the shared actions
    }
}
