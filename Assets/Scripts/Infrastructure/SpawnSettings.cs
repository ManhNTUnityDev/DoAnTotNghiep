using System;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.AI;
using ChaseGame.Match;

namespace ChaseGame.Infrastructure
{
    // Scene-authored references the SpawnManager needs. Serialized on GameLifetimeScope
    // and registered as an instance so the (plain-class) SpawnManager can inject it.
    [Serializable]
    public class SpawnSettings
    {
        public Character chaserPrefab;
        public Character runnerPrefab;
        public Transform[] chaserSpawnPoints;
        public Transform[] runnerSpawnPoints;
        public AIConfig aiConfig;
        public SimpleFollowCamera followCamera;

        // Optional: leave empty and JailService builds a primitive cage at runtime.
        public Cage cagePrefab;
    }
}
