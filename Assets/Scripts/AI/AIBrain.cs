using UnityEngine;
using ChaseGame.Brains;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Host for an AI-driven character, shared by every decision architecture: owns the
    // Blackboard, refreshes perception through a Sensor, and runs a decision at a fixed
    // low rate (AIConfig.TickRateHz) with a random phase so brains don't tick together.
    // The NavMeshAgent/CharacterController still run every frame via CharacterMovement;
    // only the *decisions* are throttled. Subclasses supply Decide (BT or FSM).
    public abstract class AIBrain : CharacterBrain
    {
        public Blackboard Blackboard { get; private set; }

        public abstract BrainKind Kind { get; }

        // Measurements for the BT vs FSM comparison; never null, and TickForTests never records.
        public DecisionStats Stats { get; } = new DecisionStats();

        private readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        private Sensor sensor;
        private float tickInterval;
        private float accum;
        private bool built;

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
            OnBuild();
            built = true;
        }

        // Build the decision structure (tree or state machine) once Blackboard exists.
        protected abstract void OnBuild();

        // One decision over the current blackboard.
        protected abstract void Decide(Blackboard bb);

        private void Update()
        {
            if (!built)
            {
                return;
            }

            if (Character != null && !Character.CanAct)
            {
                return;
            }

            accum += Time.deltaTime;
            if (accum < tickInterval)
            {
                return;
            }

            accum = 0f;

            watch.Restart();
            sensor.Sense(Blackboard);
            double senseMs = watch.Elapsed.TotalMilliseconds;

            var before = Blackboard.ActiveBehaviour;
            watch.Restart();
            Decide(Blackboard);
            double decideMs = watch.Elapsed.TotalMilliseconds;

            Stats.Record(senseMs, decideMs, before, Blackboard.ActiveBehaviour);
        }

        // Test seam: run one decision tick without the frame timer or sensor,
        // so decision wiring can be exercised in EditMode with a hand-set blackboard.
        public void TickForTests()
        {
            if (built)
            {
                Decide(Blackboard);
            }
        }

        public static float PlanarDistance(Vector3 a, Vector3 b) => AIActions.PlanarDistance(a, b);
    }
}
