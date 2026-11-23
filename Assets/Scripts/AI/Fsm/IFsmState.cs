using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // One state of a finite state machine.
    public interface IFsmState
    {
        string Name { get; }
        void Enter(Blackboard bb);
        void Tick(Blackboard bb);
        void Exit(Blackboard bb);
    }
}
