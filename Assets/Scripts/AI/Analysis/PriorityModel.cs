using System.Collections.Generic;
using ChaseGame.AI.BehaviourTree;
using ChaseGame.AI.Fsm;

namespace ChaseGame.AI.Analysis
{
    // Blackboard for the synthetic models: N-1 trigger flags and a record of which behaviour
    // the model picked. No Character, no Sensor, no NavMesh — only the selection is measured.
    public sealed class SyntheticBlackboard : Blackboard
    {
        public bool[] Flags;
        public int LastAction = -1;
    }

    /// <summary>
    /// Behaviour Tree and FSM built from the same abstract problem, for N = 2…10 behaviours,
    /// so the report can plot structure size and selection cost against behaviour count
    /// instead of relying on the single data point the game's own brains provide.
    ///
    /// The problem: N behaviours in priority order. Behaviour i (i &lt; N-1) should run when its
    /// trigger is active and no higher-priority trigger is; behaviour N-1 is the default.
    ///
    /// Fairness: each model is written the way that architecture is actually written.
    /// - The tree's branch condition is one flag test, because a Selector's ordering already
    ///   guarantees no higher-priority branch succeeded.
    /// - An FSM transition has no such ordering to lean on, so its condition states the whole
    ///   rule: "my trigger is active and no higher-priority trigger is". That is what a person
    ///   hand-writing the transition would have to write.
    /// Neither side is handicapped; the asymmetry is the architectures', not the harness's.
    /// `PriorityModelTests` checks all three models pick the same behaviour in every situation.
    /// </summary>
    public static class PriorityModel
    {
        // Which behaviour should run: the highest-priority active trigger, else the default.
        public static int ExpectedAction(bool[] flags)
        {
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i])
                {
                    return i;
                }
            }

            return flags.Length; // the default behaviour, index N-1
        }

        // Selector(Sequence(flag 0, act 0), …, Sequence(flag N-2, act N-2), act N-1).
        // Node count 3N - 1.
        public static Node BuildTree(int n)
        {
            var children = new List<Node>();
            for (int i = 0; i < n - 1; i++)
            {
                int index = i;
                children.Add(new Sequence(
                    new ConditionNode(bb => Flags(bb)[index]),
                    new ActionNode(bb => Act(bb, index))));
            }

            children.Add(new ActionNode(bb => Act(bb, n - 1)));
            return new Selector(children.ToArray());
        }

        // Textbook FSM: from each state, one transition to each other state. N(N-1) rows.
        public static StateMachine BuildPureFsm(int n, int initial)
        {
            var states = BuildStates(n);
            var transitions = new List<FsmTransition>();
            for (int from = 0; from < n; from++)
            {
                for (int to = 0; to < n; to++)
                {
                    if (from == to)
                    {
                        continue;
                    }

                    int target = to;
                    transitions.Add(new FsmTransition(states[from], bb => TriggerWins(Flags(bb), target, n), states[to]));
                }
            }

            return new StateMachine(states[initial], transitions);
        }

        // The same FSM allowed "from any state" rows: N transitions instead of N(N-1).
        public static StateMachine BuildAnyStateFsm(int n, int initial)
        {
            var states = BuildStates(n);
            var transitions = new List<FsmTransition>();
            for (int to = 0; to < n; to++)
            {
                int target = to;
                transitions.Add(new FsmTransition(null, bb => TriggerWins(Flags(bb), target, n), states[to]));
            }

            return new StateMachine(states[initial], transitions);
        }

        private static IFsmState[] BuildStates(int n)
        {
            var states = new IFsmState[n];
            for (int i = 0; i < n; i++)
            {
                int index = i;
                states[i] = new ActionState($"S{i}", bb => Act(bb, index));
            }

            return states;
        }

        // What an FSM transition condition has to spell out: this trigger active, and no
        // higher-priority trigger active.
        private static bool TriggerWins(bool[] flags, int target, int n)
        {
            if (target == n - 1)
            {
                for (int j = 0; j < flags.Length; j++)
                {
                    if (flags[j])
                    {
                        return false;
                    }
                }

                return true;
            }

            if (!flags[target])
            {
                return false;
            }

            for (int j = 0; j < target; j++)
            {
                if (flags[j])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool[] Flags(Blackboard bb) => ((SyntheticBlackboard)bb).Flags;

        private static NodeStatus Act(Blackboard bb, int index)
        {
            ((SyntheticBlackboard)bb).LastAction = index;
            return NodeStatus.Running;
        }
    }
}
