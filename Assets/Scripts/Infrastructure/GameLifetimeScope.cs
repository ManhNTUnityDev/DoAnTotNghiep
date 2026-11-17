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
            // Jail system: the cage prefab is scene-authored, so build the service by hand.
            builder.Register<ICaptureService>(_ => new JailService(spawnSettings.cagePrefab), Lifetime.Singleton);
            builder.Register<CharacterFactory>(Lifetime.Singleton);

            // Match end-condition subsystem.
            builder.Register<MatchEvaluator>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<MatchController>();

            builder.RegisterEntryPoint<SpawnManager>();
        }
    }
}
