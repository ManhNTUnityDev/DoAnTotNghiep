using System.Collections.Generic;
using UnityEngine;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // FSM runner, behaviourally equivalent to RunnerAIBrain: Flee / Wander with an explicit
    // transition table. Pure FSM — every transition names its source state.
    public sealed class RunnerFsmBrain : FsmBrain, IRunnerCloneBrain
    {
        public bool IsClone => Blackboard != null && Blackboard.IsClone;

        public void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f)
        {
            RunnerActions.ConfigureAsClone(Blackboard, initialMoveDirection, fleeAngleBias);
        }

        protected override StateMachine BuildMachine()
        {
            var flee = new ActionState("Flee", RunnerActions.EscapeAndFlee);
            var wander = new ActionState("Wander", AIActions.Wander);

            var transitions = new List<FsmTransition>
            {
                new FsmTransition(wander, RunnerActions.ThreatNear, flee),
                new FsmTransition(flee, NoThreat, wander),
            };

            return new StateMachine(wander, transitions);
        }

        private static bool NoThreat(Blackboard bb) => !RunnerActions.ThreatNear(bb);
    }
}
