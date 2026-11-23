using System;
using System.Collections.Generic;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // Table-driven finite state machine. Each tick takes at most one transition: the first
    // row, in table order, that leaves the current state (or any state) for a different
    // state and whose condition holds. Then the current state ticks.
    public sealed class StateMachine
    {
        private readonly List<FsmTransition> transitions;
        private readonly HashSet<IFsmState> states = new HashSet<IFsmState>();
        private bool entered;

        public IFsmState Current { get; private set; }
        public IReadOnlyList<FsmTransition> Transitions => transitions;
        public IReadOnlyCollection<IFsmState> States => states;
        public int TransitionCount => transitions.Count;

        public StateMachine(IFsmState initial, IReadOnlyList<FsmTransition> transitions)
        {
            Current = initial ?? throw new ArgumentNullException(nameof(initial));
            this.transitions = new List<FsmTransition>(transitions ?? Array.Empty<FsmTransition>());
            states.Add(initial);
            foreach (var t in this.transitions)
            {
                if (t.From != null) states.Add(t.From);
                states.Add(t.To);
            }
        }

        public void Tick(Blackboard bb)
        {
            if (!entered)
            {
                Current.Enter(bb);
                entered = true;
            }

            foreach (var t in transitions)
            {
                if ((t.From == Current || t.From == null) && t.To != Current && t.Condition(bb))
                {
                    Current.Exit(bb);
                    Current = t.To;
                    Current.Enter(bb);
                    break;
                }
            }

            Current.Tick(bb);
        }
    }
}
