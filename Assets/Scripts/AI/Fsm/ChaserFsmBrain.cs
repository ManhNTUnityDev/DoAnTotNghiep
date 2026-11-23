using System.Collections.Generic;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // FSM chaser, behaviourally equivalent to ChaserAIBrain: Shoot / Chase / Wander with an
    // explicit transition table. Pure FSM — every transition names its source state.
    public sealed class ChaserFsmBrain : FsmBrain
    {
        protected override StateMachine BuildMachine()
        {
            var shoot = new ActionState("Shoot", ChaserActions.AimAndShoot);
            var chase = new ActionState("Chase", ChaserActions.ChaseTarget);
            var wander = new ActionState("Wander", AIActions.Wander);

            var transitions = new List<FsmTransition>
            {
                new FsmTransition(wander, ChaserActions.CanShoot, shoot),
                new FsmTransition(wander, ChaseNotShoot, chase),
                new FsmTransition(chase, ChaserActions.CanShoot, shoot),
                new FsmTransition(chase, NoTarget, wander),
                new FsmTransition(shoot, ChaseNotShoot, chase),
                new FsmTransition(shoot, NoTarget, wander),
            };

            return new StateMachine(wander, transitions);
        }

        private static bool ChaseNotShoot(Blackboard bb) => ChaserActions.HasTarget(bb) && !ChaserActions.CanShoot(bb);
        private static bool NoTarget(Blackboard bb) => !ChaserActions.HasTarget(bb);
    }
}
