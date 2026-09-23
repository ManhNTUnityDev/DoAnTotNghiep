using UnityEngine;
using VContainer;
using VContainer.Unity;
using ChaseGame.AI;
using ChaseGame.Input;
using ChaseGame.Match;

namespace ChaseGame.Infrastructure
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private SpawnSettings spawnSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            // The on-screen joystick adapter (a MonoBehaviour in the scene). Registered by
            // interface so this scope never names the Assembly-CSharp concrete type.
            builder.RegisterComponentInHierarchy<IJoystickInput>();

            builder.Register<IInputService, InputSystemService>(Lifetime.Singleton);

            // Match services shared across every spawned character.
            builder.RegisterInstance(spawnSettings);
            builder.Register<ITeamRoster, TeamRoster>(Lifetime.Singleton);
            builder.Register<ICaptureService, CaptureServiceStub>(Lifetime.Singleton);
            builder.Register<CharacterFactory>(Lifetime.Singleton);

            // The coordinator ticks (ITickable) and is injected into SpawnManager (AsSelf).
            builder.RegisterEntryPoint<ChaserCoordinator>().AsSelf();

            builder.RegisterEntryPoint<SpawnManager>();
        }
    }
}
