using System;
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
    /// Fairness: two FSM authoring styles are measured, so that the selection-cost figures cannot
    /// be dismissed as an artefact of how the conditions happen to be written.
    /// - The tree's branch condition is one flag test: a Selector's ordering already guarantees
    ///   no higher-priority branch succeeded.
    /// - <see cref="BuildPureFsm"/> writes self-contained conditions - "my trigger is active and
    ///   no higher-priority trigger is" - the form a person gets when each row is meant to be
    ///   readable on its own. Re-scanning the higher-priority flags costs O(target) per row.
    /// - <see cref="BuildOrderedFsm"/> has the same N(N-1) rows but leans on table order, exactly
    ///   as the tree leans on Selector order, so every condition is a flag test or two. That is
    ///   not hypothetical: ChaserFsmBrain already relies on the same guarantee, listing
    ///   Chase -> Search above Chase -> Wander.
    ///
    /// What the measurement shows is that the two FSM styles cost the same within noise, so the
    /// condition form is not where the cost is. <see cref="StateMachine.Tick"/> walks the whole
    /// flat transition list every tick, testing From before it ever evaluates a condition, so the
    /// work per tick follows the number of rows - N(N-1) of them - whatever each row says. The
    /// any-state table, with N rows, is cheap for the same reason. Cost tracks table size, and
    /// table size is the architectural result the counts already report.
    /// `PriorityModelTests` checks all four models pick the same behaviour in every situation.
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

        /// <summary>
        /// The same N(N-1) rows as <see cref="BuildPureFsm"/>, but written to lean on table
        /// order the way a Selector's branches do. Rows leave state s in ascending target
        /// order, so each condition only has to rule out what the rows above it left open:
        /// a lower-priority target is only reachable once the current state's own trigger has
        /// dropped, and the default row is reached only after every higher-priority row failed.
        ///
        /// The naive "just test flags[to]" form is wrong, because <see cref="StateMachine.Tick"/>
        /// skips rows whose To is already Current: with n = 3, Current = S0 and both flags set it
        /// would wrongly leave S0 for S1. PriorityModelTests covers every start state and flag
        /// combination for exactly that reason.
        /// </summary>
        public static StateMachine BuildOrderedFsm(int n, int initial)
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

                    int self = from;
                    int target = to;
                    Func<Blackboard, bool> condition;

                    if (target == n - 1)
                    {
                        // The default is the one row that still states its whole rule. Leaning on
                        // order here would allow the cheaper `!Flags(bb)[self]`, which is correct
                        // by the same argument, but keeping the explicit form makes this row read
                        // the same as the pure table's and costs one scan per tick, not per row.
                        condition = bb => NoTriggerSet(Flags(bb));
                    }
                    else if (target < self)
                    {
                        condition = bb => Flags(bb)[target];
                    }
                    else
                    {
                        condition = bb => Flags(bb)[target] && !Flags(bb)[self];
                    }

                    transitions.Add(new FsmTransition(states[from], condition, states[to]));
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

        private static bool NoTriggerSet(bool[] flags)
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

        private static bool[] Flags(Blackboard bb) => ((SyntheticBlackboard)bb).Flags;

        private static NodeStatus Act(Blackboard bb, int index)
        {
            ((SyntheticBlackboard)bb).LastAction = index;
            return NodeStatus.Running;
        }
    }
}
