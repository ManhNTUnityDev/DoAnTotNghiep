using UnityEngine;
using UnityEngine.AI;
using ChaseGame.Characters;
using ChaseGame.Brains;
using ChaseGame.Match;
using ChaseGame.AI;

namespace ChaseGame.Abilities
{
    // Runner ability: spawns a temporary visual & AI-driven clone of the runner right at
    // the runner's current position. The clone acts as an independent runner (fleeing from
    // chasers along a diverging path or wandering) for `lifetime` seconds to distract
    // chasers while the real runner escapes.
    public sealed class Decoy : MonoBehaviour, IAbility
    {
        [SerializeField] private float cooldown = 8f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private float divergenceAngle = 55f;

        private CooldownTimer timer;
        private ITeamRoster roster;
        private AIConfig aiConfig;
        private AIConfig fallbackConfig;
        private DecoyLure activeClone;
        private bool flipDivergenceSign;

        private void Awake()
        {
            timer = new CooldownTimer(cooldown);
        }

        private void OnDestroy()
        {
            if (fallbackConfig != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(fallbackConfig);
                }
                else
                {
                    DestroyImmediate(fallbackConfig);
                }
            }
        }

        public void Configure(ITeamRoster teamRoster, AIConfig config)
        {
            if (teamRoster != null)
            {
                roster = teamRoster;
            }

            if (config != null)
            {
                aiConfig = config;
            }
        }

        public bool IsReady => timer == null || timer.IsReady(Time.time);
        public float CooldownRemaining => timer != null ? timer.Remaining(Time.time) : 0f;
        public float CooldownNormalized => timer != null ? timer.NormalizedRemaining(Time.time) : 0f;
        public float Lifetime => lifetime;
        public bool IsCloneActive => activeClone != null;
        public DecoyLure ActiveClone => activeClone;

        public void SetLifetimeForTests(float seconds)
        {
            lifetime = Mathf.Max(0.05f, seconds);
        }

        public void Use(Character self, Vector3 aim)
        {
            if (self == null)
            {
                return;
            }

            if (timer != null && !timer.IsReady(Time.time))
            {
                return;
            }

            timer?.Trigger(Time.time);

            Vector3 spawnPos = self.transform.position;
            if (NavMesh.SamplePosition(spawnPos, out var navHit, 1.5f, NavMesh.AllAreas))
            {
                spawnPos = navHit.position;
            }

            Vector3 baseDir = new Vector3(aim.x, 0f, aim.z);
            if (baseDir.sqrMagnitude < 1e-4f)
            {
                baseDir = new Vector3(self.transform.forward.x, 0f, self.transform.forward.z);
            }
            if (baseDir.sqrMagnitude < 1e-4f)
            {
                baseDir = Vector3.forward;
            }
            baseDir.Normalize();

            flipDivergenceSign = !flipDivergenceSign;
            float signedAngle = flipDivergenceSign ? divergenceAngle : -divergenceAngle;
            Vector3 cloneInitialDir = (Quaternion.Euler(0f, signedAngle, 0f) * baseDir).normalized;
            Quaternion spawnRot = Quaternion.LookRotation(cloneInitialDir, Vector3.up);

            // The clone thinks with the same architecture as its spawner (BT for a human player),
            // so a match measuring one architecture never mixes in the other through decoys.
            BrainKind cloneKind = BrainFactory.KindOf(self.gameObject);

            GameObject cloneGo = Instantiate(self.gameObject, spawnPos, spawnRot);
            cloneGo.name = $"{self.gameObject.name}_DecoyClone";

            SanitizeCloneComponents(self.gameObject, cloneGo, cloneKind, out var cloneCharacter, out var keptBrain);

            var lure = cloneGo.GetComponent<DecoyLure>();
            if (lure == null)
            {
                lure = cloneGo.AddComponent<DecoyLure>();
            }
            lure.SetLifetime(lifetime);
            activeClone = lure;

            ResolveAIServices(self, out var resolvedRoster, out var resolvedConfig);

            AIBrain cloneBrain = keptBrain != null ? keptBrain : BrainFactory.Add(cloneGo, Team.Runner, cloneKind);
            cloneBrain.Initialize(cloneCharacter, resolvedRoster, resolvedConfig);
            ((IRunnerCloneBrain)cloneBrain).ConfigureAsClone(cloneInitialDir, signedAngle);

            Debug.Log($"[Decoy] Spawned runner clone '{cloneGo.name}' at {spawnPos} for {lifetime:0.0}s");
        }

        private static void SanitizeCloneComponents(GameObject sourceGo, GameObject cloneGo, BrainKind kind, out Character cloneCharacter, out AIBrain keptBrain)
        {
            // 1. Remove any human PlayerBrain so joystick/keyboard never drives the clone.
            foreach (var playerBrain in cloneGo.GetComponents<PlayerBrain>())
            {
                playerBrain.enabled = false;
                DestroyComponentSafe(playerBrain);
            }

            // 2. Keep at most one runner brain of the requested kind; remove every other AI brain.
            keptBrain = null;
            foreach (var aiBrain in cloneGo.GetComponents<AIBrain>())
            {
                if (keptBrain == null && aiBrain is IRunnerCloneBrain && aiBrain.Kind == kind)
                {
                    keptBrain = aiBrain;
                    continue;
                }

                aiBrain.enabled = false;
                DestroyComponentSafe(aiBrain);
            }

            // 3. Disable and strip abilities on the clone so it cannot use SpeedBoost or spawn recursive clones.
            cloneCharacter = cloneGo.GetComponent<Character>();
            if (cloneCharacter != null)
            {
                cloneCharacter.SetAbilitiesForTests(null);
                cloneCharacter.CaptureState = CaptureState.Free;
            }

            foreach (var sb in cloneGo.GetComponents<SpeedBoost>())
            {
                sb.enabled = false;
                DestroyComponentSafe(sb);
            }

            foreach (var dec in cloneGo.GetComponents<Decoy>())
            {
                dec.enabled = false;
                DestroyComponentSafe(dec);
            }

            foreach (var ca in cloneGo.GetComponents<CharacterAbilities>())
            {
                ca.enabled = false;
                DestroyComponentSafe(ca);
            }

            // 4. Ensure base movement speed is reset to 1x (in case creator was boosting when cloning).
            var cloneMovement = cloneGo.GetComponent<CharacterMovement>();
            if (cloneMovement != null)
            {
                cloneMovement.SetSpeedMultiplier(1f);

                // A clone of a first-person player inherits faceMoveDirection = false, because
                // FirstPersonView turned it off on the original. The clone has no view driver of
                // its own, so without this it would never turn and would strafe sideways forever.
                cloneMovement.SetFaceMoveDirection(true);
            }

            // 4b. Strip the first-person rig: the clone is AI-driven and must not try to steer a
            //     camera or own its yaw. (It is never Activated, so this is belt and braces - but
            //     it also stops the clone from hiding its own renderers.)
            foreach (var view in cloneGo.GetComponents<Infrastructure.FirstPersonView>())
            {
                view.enabled = false;
                DestroyComponentSafe(view);
            }

            var agent = cloneGo.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(cloneGo.transform.position);
            }

            // 5. Ignore physics collisions between the creator and the clone so spawning at the exact
            //    same position never pushes or blocks the real runner.
            var sourceColliders = sourceGo.GetComponentsInChildren<Collider>();
            var cloneColliders = cloneGo.GetComponentsInChildren<Collider>();
            foreach (var sc in sourceColliders)
            {
                if (sc == null || !sc.enabled) continue;
                foreach (var cc in cloneColliders)
                {
                    if (cc == null || !cc.enabled) continue;
                    Physics.IgnoreCollision(sc, cc, true);
                }
            }
        }

        private void ResolveAIServices(Character self, out ITeamRoster resolvedRoster, out AIConfig resolvedConfig)
        {
            resolvedRoster = roster;
            resolvedConfig = aiConfig;

            if ((resolvedRoster == null || resolvedConfig == null) &&
                self.TryGetComponent<AIBrain>(out var selfAi) &&
                selfAi.Blackboard != null)
            {
                resolvedRoster ??= selfAi.Blackboard.Roster;
                resolvedConfig ??= selfAi.Blackboard.Config;
            }

            if (resolvedRoster == null)
            {
                resolvedRoster = new TeamRoster();
            }

            if (resolvedConfig == null)
            {
                if (fallbackConfig == null)
                {
                    fallbackConfig = ScriptableObject.CreateInstance<AIConfig>();
                }
                resolvedConfig = fallbackConfig;
            }
        }

        private static void DestroyComponentSafe(Object component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }
    }
}
