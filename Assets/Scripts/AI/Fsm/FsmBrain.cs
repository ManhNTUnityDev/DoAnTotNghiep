using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // AI brain whose decision is a finite state machine with an explicit transition table.
    public abstract class FsmBrain : AIBrain
    {
        public StateMachine Machine { get; private set; }

        public override BrainKind Kind => BrainKind.Fsm;

        protected abstract StateMachine BuildMachine();

        protected override void OnBuild()
        {
            Machine = BuildMachine();
        }

        protected override void Decide(Blackboard bb)
        {
            Machine.Tick(bb);
        }
    }
}
