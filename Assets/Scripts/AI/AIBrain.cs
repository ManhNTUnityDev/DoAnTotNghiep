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

            bool needNew = bb.Destination == null ||
                           Vector3.Distance(self, bb.Destination.Value) <= reach;

            if (needNew)
            {
                float radius = bb.Config != null ? bb.Config.WanderRadius : 6f;
                if (TryRandomPoint(self, radius, out var point))
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

        protected static bool TryRandomPoint(Vector3 center, float radius, out Vector3 result)
        {
            Vector3 candidate = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(candidate, out var hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = center;
            return false;
        }
    }
}
