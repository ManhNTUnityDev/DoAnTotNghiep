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
    // the rest to AI brains, and points the follow camera at the player.
    // Replaces PlayerBootstrap.
    public sealed class SpawnManager : IStartable
    {
        private readonly CharacterFactory factory;
        private readonly ITeamRoster roster;
        private readonly IInputService input;
        private readonly SpawnSettings settings;

        public SpawnManager(
            CharacterFactory factory,
            ITeamRoster roster,
            IInputService input,
            SpawnSettings settings)
        {
            this.factory = factory;
            this.roster = roster;
            this.input = input;
            this.settings = settings;
        }

        // Pure: a coin flip decides which team the human plays.
        public static Team PickPlayerTeam(float roll) => roll < 0.5f ? Team.Chaser : Team.Runner;

        public void Start()
        {
            Team playerTeam = PickPlayerTeam(Random.value);

            SpawnTeam(Team.Chaser, settings.chaserPrefab, settings.chaserSpawnPoints, playerTeam);
            SpawnTeam(Team.Runner, settings.runnerPrefab, settings.runnerSpawnPoints, playerTeam);

            if (roster.PlayerCharacter != null)
            {
                ActivatePlayerCamera(roster.PlayerCharacter);
            }
        }

        // First person whenever the player's prefab carries a FirstPersonView rig; otherwise fall
        // back to the orbit camera, so a prefab without the rig still plays.
        private void ActivatePlayerCamera(Character player)
        {
            Transform camTransform = settings.followCamera != null ? settings.followCamera.transform : null;
            var view = player.GetComponent<FirstPersonView>();

            if (view != null && camTransform != null)
            {
                view.enabled = true;
                view.Activate(camTransform, CameraLookTouchPad.ActiveInstance);
                return;
            }

            if (settings.followCamera != null)
            {
                settings.followCamera.SetTarget(player.transform);
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

                foreach (var decoy in character.GetComponents<Abilities.Decoy>())
                {
                    decoy.Configure(roster, settings.aiConfig);
                }

                bool isPlayer = team == playerTeam && i == 0;

                // Keep the first-person view model only on the character the human drives;
                // everyone else gets the full-body world model. Harmless on prefabs that have no
                // selector (the Runner today), and the selector itself falls back to the world
                // model if this call never happens.
                foreach (var selector in character.GetComponents<CharacterVisualSelector>())
                {
                    selector.Apply(isPlayer);
                }

                if (isPlayer)
                {
                    var brain = character.gameObject.AddComponent<PlayerBrain>();
                    Transform camTransform = settings.followCamera != null ? settings.followCamera.transform : null;
                    brain.Initialize(character, input, camTransform);
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
            }
            else
            {
                var brain = character.gameObject.AddComponent<RunnerAIBrain>();
                brain.Initialize(character, roster, settings.aiConfig);
            }
        }
    }
}
