using VContainer;
using VContainer.Unity;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Infrastructure
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IInputService, InputSystemService>(Lifetime.Singleton);

            // The single Character currently placed in the Game scene.
            builder.RegisterComponentInHierarchy<Character>();

            builder.RegisterEntryPoint<PlayerBootstrap>();
        }
    }
}
