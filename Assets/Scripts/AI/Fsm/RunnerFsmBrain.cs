using System.Collections.Generic;
using UnityEngine;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // FSM runner, behaviourally equivalent to RunnerAIBrain: Hide / Flee / Wander with an
    // explicit transition table. Pure FSM — every transition names its source state.
    public sealed class RunnerFsmBrain : FsmBrain, IRunnerCloneBrain
    {
        // Moving to a hiding place. The state owns the point it found, so the transition
        // predicate and the tick share it without going through the blackboard.
        private sealed class HideState : IFsmState
        {
            private Vector3 point;
            private bool hasPoint;

            public string Name => "Hide";

            // Doubles as the transition predicate: true when there is cover to take, and the
            // point it finds is what Tick then moves toward.
            public bool HasCover(Blackboard bb)
            {
                hasPoint = false;

                // A decoy clone's whole job is to be seen, so it never hides.
                if (bb.IsClone || bb.NearestThreat == null || !RunnerActions.ThreatNear(bb))
                {
                    return false;
                }

                float radius = bb.Config != null ? bb.Config.CoverSearchRadius : 8f;
                int samples = bb.Config != null ? bb.Config.CoverSampleCount : 12;

                if (RunnerActions.TryFindCoverPoint(
                    bb.Self.transform.position, bb.NearestThreat.position, radius, samples, bb.SightMask, out var found))
                {
                    point = found;
                    hasPoint = true;
                }

                return hasPoint;
            }

            public bool LostCover(Blackboard bb) => RunnerActions.ThreatNear(bb) && !HasCover(bb);

            public void Enter(Blackboard bb)
            {
            }

            public void Tick(Blackboard bb)
            {
                if (hasPoint)
                {
                    RunnerActions.TakeCover(bb, point);
                }
                else
                {
                    RunnerActions.EscapeAndFlee(bb);
                }
            }

            public void Exit(Blackboard bb)
            {
            }
        }

        public bool IsClone => Blackboard != null && Blackboard.IsClone;

        public void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f)
        {
            RunnerActions.ConfigureAsClone(Blackboard, initialMoveDirection, fleeAngleBias);
        }

        protected override StateMachine BuildMachine()
        {
            var hide = new HideState();
            var flee = new ActionState("Flee", RunnerActions.EscapeAndFlee);
            var wander = new ActionState("Wander", AIActions.Wander);

            // Order matters: a threat prefers cover over open flight, matching the tree's
            // nested Selector.
            var transitions = new List<FsmTransition>
            {
                new FsmTransition(wander, hide.HasCover, hide),
                new FsmTransition(wander, RunnerActions.ThreatNear, flee),

                new FsmTransition(flee, hide.HasCover, hide),
                new FsmTransition(flee, NoThreat, wander),

                new FsmTransition(hide, hide.LostCover, flee),
                new FsmTransition(hide, NoThreat, wander),
            };

            return new StateMachine(wander, transitions);
        }

        private static bool NoThreat(Blackboard bb) => !RunnerActions.ThreatNear(bb);
    }
}
