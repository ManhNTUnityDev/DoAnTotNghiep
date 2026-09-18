using VContainer;
using VContainer.Unity;
using ChaseGame.Brains;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Infrastructure
{
    // Temporary foundation harness: attaches the player brain to the single
    // Character placed in the scene. SpawnManager will replace this in a later plan.
    public class PlayerBootstrap : IStartable
    {
        private readonly Character character;
        private readonly IInputService input;

        public PlayerBootstrap(Character character, IInputService input)
        {
            this.character = character;
            this.input = input;
        }

        public void Start()
        {
            // Method A: attach the Brain at runtime, prefab stays "pure body".
            var brain = character.gameObject.AddComponent<PlayerBrain>();
            brain.Initialize(character, input);
        }
    }
}
