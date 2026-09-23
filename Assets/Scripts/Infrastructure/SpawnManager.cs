using UnityEngine;
using VContainer.Unity;
using ChaseGame.Brains;
using ChaseGame.Characters;
using ChaseGame.Input;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Infrastructure
{
    // Match bootstrap (spec §9): spawns both teams from their spawn points, promotes one
    // character (slot 0 of the randomly chosen player team) to the human PlayerBrain and
    // the rest to AI brains, registers AI chasers with the coordinator, and points the
    // follow camera at the player. Replaces PlayerBootstrap.
    public sealed class SpawnManager : IStartable
    {
        private readonly CharacterFactory factory;
        private readonly ITeamRoster roster;
        private readonly IInputService input;
        private readonly SpawnSettings settings;
        private readonly ChaserCoordinator coordinator;

        public SpawnManager(
            CharacterFactory factory,
            ITeamRoster roster,
            IInputService input,
            SpawnSettings settings,
            ChaserCoordinator coordinator)
        {
            this.factory = factory;
            this.roster = roster;
            this.input = input;
            this.settings = settings;
            this.coordinator = coordinator;
        }

        // Pure: a coin flip decides which team the human plays.
        public static Team PickPlayerTeam(float roll) => roll < 0.5f ? Team.Chaser : Team.Runner;

        public void Start()
        {
            Team playerTeam = PickPlayerTeam(Random.value);

            SpawnTeam(Team.Chaser, settings.chaserPrefab, settings.chaserSpawnPoints, playerTeam);
            SpawnTeam(Team.Runner, settings.runnerPrefab, settings.runnerSpawnPoints, playerTeam);

            if (settings.followCamera != null && roster.PlayerCharacter != null)
            {
                settings.followCamera.SetTarget(roster.PlayerCharacter.transform);
            }
        }

        private void SpawnTeam(Team team, Character prefab, Transform[] points, Team playerTeam)
        {
            if (prefab == null || points == null)
            {
                return;
            }

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point == null)
                {
                    continue;
                }

                var character = factory.Spawn(prefab, point.position, point.rotation);

                bool isPlayer = team == playerTeam && i == 0;
                if (isPlayer)
                {
                    var brain = character.gameObject.AddComponent<PlayerBrain>();
                    brain.Initialize(character, input);
                    roster.PlayerCharacter = character;
                }
                else
                {
                    AttachAIBrain(character, team);
                }
            }
        }

        private void AttachAIBrain(Character character, Team team)
        {
            if (team == Team.Chaser)
            {
                var brain = character.gameObject.AddComponent<ChaserAIBrain>();
                brain.Initialize(character, roster, settings.aiConfig);
                coordinator.Register(brain.Blackboard);
            }
            else
            {
                var brain = character.gameObject.AddComponent<RunnerAIBrain>();
                brain.Initialize(character, roster, settings.aiConfig);
            }
        }
    }
}
