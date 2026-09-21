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
            // The on-screen joystick adapter (a MonoBehaviour in the scene). Registered by
            // interface so this scope never names the Assembly-CSharp concrete type.
            builder.RegisterComponentInHierarchy<IJoystickInput>();

            builder.Register<IInputService, InputSystemService>(Lifetime.Singleton);

            // The single Character currently placed in the Game scene.
            builder.RegisterComponentInHierarchy<Character>();

            builder.RegisterEntryPoint<PlayerBootstrap>();
        }
    }
}
