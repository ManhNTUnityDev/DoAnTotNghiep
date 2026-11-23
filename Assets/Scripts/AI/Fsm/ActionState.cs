using System;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // A state whose Tick runs one shared action; the returned status is ignored, because
    // leaving the state is decided by the transition table, not by the action.
    public sealed class ActionState : IFsmState
    {
        private readonly Func<Blackboard, NodeStatus> tick;
        private readonly Action<Blackboard> enter;
        private readonly Action<Blackboard> exit;

        public string Name { get; }

        public ActionState(string name, Func<Blackboard, NodeStatus> tick,
            Action<Blackboard> enter = null, Action<Blackboard> exit = null)
        {
            Name = name;
            this.tick = tick ?? throw new ArgumentNullException(nameof(tick));
            this.enter = enter;
            this.exit = exit;
        }

        public void Enter(Blackboard bb) => enter?.Invoke(bb);
        public void Tick(Blackboard bb) => tick(bb);
        public void Exit(Blackboard bb) => exit?.Invoke(bb);
    }
}
