using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ChaseGame.AI;
using ChaseGame.Abilities;
using ChaseGame.Characters;
using ChaseGame.Match;
using Object = UnityEngine.Object;

namespace ChaseGame.Tests.PlayMode
{
    // A small runtime-baked arena for AI scenario tests: flat 40×40 ground, optional walls
    // (physical colliders that block sight + non-walkable NavMesh volumes), bodies, and
    // "puppets" — Characters with no body that the test moves by hand.
    public sealed class ScenarioArena : IDisposable
    {
        private const float GroundSize = 40f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        private NavMeshDataInstance navMeshInstance;

        public TeamRoster Roster { get; } = new TeamRoster();
        public AIConfig Config { get; }

        // Owned so Dispose can destroy the cages a capture spawns. A cage carries a
        // BoxCollider and a NavMeshObstacle, so one left behind blocks sight and pathing
        // for every later test in the run.
        public JailService Capture { get; } = new JailService();

        public ScenarioArena()
        {
            // Earlier fixtures in the same run leave cages and in-flight eggs behind: neither is
            // owned by the fixture that spawned it, a cage blocks sight and pathing, and an egg
            // can jail a runner mid-scenario. Clear both so every scenario starts from a bare arena.
            SweepStrays();

            Config = ScriptableObject.CreateInstance<AIConfig>(); // vision 12, shoot 8, threat 6, wander 6, 8 Hz

            var ground = Track(new GameObject("Ground"));
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider>().size = new Vector3(GroundSize, 1f, GroundSize);

            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(GroundSize, 0.1f, GroundSize),
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
                area = 0,
            });
        }

        // Add before Bake(): a solid collider plus a non-walkable NavMesh volume.
        public GameObject AddWall(Vector3 center, Vector3 size)
        {
            var wall = Track(new GameObject("Wall"));
            wall.transform.position = center;
            wall.AddComponent<BoxCollider>().size = size;

            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = size,
                transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                area = 1, // Not Walkable
            });
            return wall;
        }

        public IEnumerator Bake()
        {
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(GroundSize + 20f, 10f, GroundSize + 20f)),
                Vector3.zero, Quaternion.identity);
            navMeshInstance = NavMesh.AddNavMeshData(data);
            yield return null;
        }

        // A full body (CharacterController + NavMeshAgent + CharacterMovement), registered in the roster.
        public Character MakeBody(string name, Team team, Vector3 pos, Action<GameObject> addComponents = null)
        {
            var go = Track(new GameObject(name));
            go.SetActive(false); // so Awake sees the full component set
            go.transform.position = pos;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.5f, 0f);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1f; agent.stoppingDistance = 0.3f;
            go.AddComponent<CharacterMovement>();
            addComponents?.Invoke(go);

            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            go.SetActive(true);
            Roster.Add(ch);
            return ch;
        }

        // A chaser body with a real Gun wired to this arena's capture service.
        public Character MakeChaserWithGun(Vector3 pos)
        {
            Gun gun = null;
            var ch = MakeBody("Chaser", Team.Chaser, pos, go =>
            {
                gun = go.AddComponent<Gun>();
                go.AddComponent<CharacterAbilities>();
            });
            gun.Configure(Capture);
            return ch;
        }

        // A Character with no body or collider: never moves on its own, the test places it.
        public Character MakePuppet(string name, Team team, Vector3 pos)
        {
            var go = Track(new GameObject(name));
            go.transform.position = pos;
            var ch = go.AddComponent<Character>();
            ch.SetTeamForTests(team);
            Roster.Add(ch);
            return ch;
        }

        public AIBrain AttachBrain(Character c, BrainKind kind)
        {
            var brain = BrainFactory.Add(c.gameObject, c.Team, kind);
            brain.Initialize(c, Roster, Config);
            return brain;
        }

        public void Dispose()
        {
            if (navMeshInstance.valid) NavMesh.RemoveNavMeshData(navMeshInstance);
            foreach (var go in spawned) { if (go != null) Object.Destroy(go); }
            spawned.Clear();
            foreach (var lure in DecoyLure.Active.ToArray()) { if (lure != null) Object.Destroy(lure.gameObject); }
            SweepStrays();
            if (Config != null) Object.DestroyImmediate(Config);
        }

        public static float Planar(Vector3 a, Vector3 b) => AIBrain.PlanarDistance(a, b);

        public static void SweepStrays()
        {
            foreach (var cage in Object.FindObjectsByType<Cage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cage != null) Object.DestroyImmediate(cage.gameObject);
            }

            foreach (var egg in Object.FindObjectsByType<EggProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (egg != null) Object.DestroyImmediate(egg.gameObject);
            }
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }
    }

    // Ability double: both abilities ready until used once (a cooldown that never ends).
    public sealed class OneShotAbilities : ICharacterAbilities
    {
        public readonly List<int> Used = new List<int>();
        private readonly HashSet<int> ready = new HashSet<int> { 0, 1 };

        public event Action<int> OnAbilityUsed;

        public bool IsReady(int id) => ready.Contains(id);

        public void UseAbility(int id, Vector3 aim)
        {
            if (!ready.Remove(id)) return;
            Used.Add(id);
            OnAbilityUsed?.Invoke(id);
        }
    }
}
