using UnityEngine;
using UnityEngine.AI;
using ChaseGame.Brains;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Host for an AI-driven character: builds a role-specific behaviour tree over a
    // Blackboard, refreshes perception through a Sensor, and ticks the tree at a fixed
    // low rate (AIConfig.TickRateHz). The NavMeshAgent/CharacterController still run
    // every frame via CharacterMovement; only the *decisions* are throttled.
    public abstract class AIBrain : CharacterBrain
    {
        public Blackboard Blackboard { get; private set; }

        private Node root;
        private Sensor sensor;
        private float tickInterval;
        private float accum;

        public void Initialize(Character character, ITeamRoster roster, AIConfig config)
        {
            Initialize(character);

            Blackboard = new Blackboard
            {
                Self = character,
                Roster = roster,
                Config = config,
            };

            sensor = GetComponent<Sensor>();
            if (sensor == null)
            {
                sensor = gameObject.AddComponent<Sensor>();
            }

            tickInterval = config != null && config.TickRateHz > 0f ? 1f / config.TickRateHz : 0.2f;
            accum = Random.Range(0f, tickInterval);
            root = BuildTree();
        }

        protected abstract Node BuildTree();

        private void Update()
        {
            if (root == null)
            {
                return;
            }

            accum += Time.deltaTime;
            if (accum < tickInterval)
            {
                return;
            }

            accum = 0f;
            sensor.Sense(Blackboard);
            root.Tick(Blackboard);
        }

        // Test seam: run one decision tick without the frame timer or sensor,
        // so tree wiring can be exercised in EditMode with a hand-set blackboard.
        public void TickForTests()
        {
            root?.Tick(Blackboard);
        }

        // Shared roaming behaviour: keep heading to a random reachable point,
        // picking a new one on arrival. Fails when there is no NavMesh nearby.
        protected static NodeStatus Wander(Blackboard bb)
        {
            const float reach = 1.5f;
            Vector3 self = bb.Self.transform.position;

            bool needNew = !bb.Destination.HasValue ||
                           PlanarDistance(self, bb.Destination.Value) <= reach;

            if (needNew)
            {
                float radius = bb.Config != null ? bb.Config.WanderRadius : 6f;
                if (TryRandomPoint(self, bb.Self.transform.forward, radius, out var point))
                {
                    bb.Destination = point;
                }
                else
                {
                    return NodeStatus.Failure;
                }
            }

            bb.Self.MoveTo(bb.Destination.Value);
            return NodeStatus.Running;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        protected static bool TryRandomPoint(Vector3 center, float radius, out Vector3 result)
            => TryRandomPoint(center, Vector3.zero, radius, out result);

        protected static bool TryRandomPoint(Vector3 center, Vector3 forwardHint, float radius, out Vector3 result)
        {
            float minDist = Mathf.Max(2.2f, radius * 0.45f);
            float maxDist = Mathf.Max(minDist + 0.5f, radius);

            Vector3 flatForward = new Vector3(forwardHint.x, 0f, forwardHint.z);
            bool hasForward = flatForward.sqrMagnitude > 1e-4f;
            if (hasForward)
            {
                flatForward.Normalize();
            }

            Vector3 fallbackHit = center;
            bool hasFallback = false;

            // Try forward-biased samples first for natural smooth roaming, then full 360° if near an edge/wall.
            for (int i = 0; i < 8; i++)
            {
                float dist = Random.Range(minDist, maxDist);
                Vector3 dir;
                if (hasForward && i < 5)
                {
                    float angle = Random.Range(-95f, 95f);
                    dir = Quaternion.Euler(0f, angle, 0f) * flatForward;
                }
                else
                {
                    Vector2 unit = Random.insideUnitCircle.normalized;
                    if (unit.sqrMagnitude < 1e-4f) unit = Vector2.up;
                    dir = new Vector3(unit.x, 0f, unit.y);
                }

                Vector3 candidate = center + dir * dist;
                if (NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    if (!hasFallback)
                    {
                        fallbackHit = hit.position;
                        hasFallback = true;
                    }

                    if (PlanarDistance(center, hit.position) >= minDist * 0.8f)
                    {
                        result = hit.position;
                        return true;
                    }
                }
            }

            if (hasFallback)
            {
                result = fallbackHit;
                return true;
            }

            result = center;
            return false;
        }
    }
}
