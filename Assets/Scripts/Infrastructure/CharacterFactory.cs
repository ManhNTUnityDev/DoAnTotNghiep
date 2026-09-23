using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.Abilities;

namespace ChaseGame.Infrastructure
{
    // Instantiates a character prefab, registers it with the roster, and injects the
    // capture service into any Gun it carries. The prefab already holds its Body/ability
    // components, so instantiating active is fine (all Awakes see the full set).
    public sealed class CharacterFactory
    {
        private readonly ITeamRoster roster;
        private readonly ICaptureService capture;

        public CharacterFactory(ITeamRoster roster, ICaptureService capture)
        {
            this.roster = roster;
            this.capture = capture;
        }

        public Character Spawn(Character prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(prefab, position, rotation);
            roster.Add(instance);

            foreach (var gun in instance.GetComponents<Gun>())
            {
                gun.Configure(capture);
            }

            return instance;
        }
    }
}
