using System.Collections.Generic;
using NUnit.Framework;
using ChaseGame.AI.Analysis;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    // Pins the synthetic models the scaling series measures. If these drift, the N = 2…10
    // curve in the report is measuring something other than what it claims.
    public class PriorityModelTests
    {
        [Test]
        public void Tree_NodeCount_Is3nMinus1()
        {
            for (int n = 2; n <= 10; n++)
            {
                Assert.AreEqual(3 * n - 1, BehaviourTreeStats.CountNodes(PriorityModel.BuildTree(n)), $"n={n}");
            }
        }

        [Test]
        public void PureFsm_TransitionCount_IsNTimesNMinus1()
        {
            for (int n = 2; n <= 10; n++)
            {
                Assert.AreEqual(n * (n - 1), PriorityModel.BuildPureFsm(n, 0).TransitionCount, $"n={n}");
                Assert.AreEqual(n, PriorityModel.BuildPureFsm(n, 0).States.Count, $"n={n}");
            }
        }

        [Test]
        public void AnyStateFsm_TransitionCount_IsN()
        {
            for (int n = 2; n <= 10; n++)
            {
                Assert.AreEqual(n, PriorityModel.BuildAnyStateFsm(n, 0).TransitionCount, $"n={n}");
            }
        }

        [Test]
        public void ExpectedAction_IsHighestPriorityActiveFlag()
        {
            Assert.AreEqual(0, PriorityModel.ExpectedAction(new[] { true, true }));
            Assert.AreEqual(1, PriorityModel.ExpectedAction(new[] { false, true }));
            Assert.AreEqual(2, PriorityModel.ExpectedAction(new[] { false, false }));
        }

        // The fairness check: a generated FSM must pick the same behaviour as the tree in
        // every situation, from every state it could already be in. Without this, the
        // transition counts could just mean the FSM was generated larger than it needed to be.
        [Test]
        public void AllThreeModels_AgreeOnEveryFlagCombination_FromEveryStartState()
        {
            for (int n = 2; n <= 9; n++)
            {
                int combos = 1 << (n - 1);
                for (int bits = 0; bits < combos; bits++)
                {
                    var flags = new bool[n - 1];
                    for (int i = 0; i < n - 1; i++)
                    {
                        flags[i] = (bits & (1 << i)) != 0;
                    }

                    int expected = PriorityModel.ExpectedAction(flags);

                    var treeBb = NewBlackboard(flags);
                    PriorityModel.BuildTree(n).Tick(treeBb);
                    Assert.AreEqual(expected, treeBb.LastAction, $"tree n={n} bits={bits}");

                    for (int initial = 0; initial < n; initial++)
                    {
                        var pureBb = NewBlackboard(flags);
                        PriorityModel.BuildPureFsm(n, initial).Tick(pureBb);
                        Assert.AreEqual(expected, pureBb.LastAction, $"pure fsm n={n} bits={bits} start={initial}");

                        var anyBb = NewBlackboard(flags);
                        PriorityModel.BuildAnyStateFsm(n, initial).Tick(anyBb);
                        Assert.AreEqual(expected, anyBb.LastAction, $"any-state fsm n={n} bits={bits} start={initial}");
                    }
                }
            }
        }

        private static SyntheticBlackboard NewBlackboard(IReadOnlyList<bool> flags)
        {
            var copy = new bool[flags.Count];
            for (int i = 0; i < flags.Count; i++) copy[i] = flags[i];
            return new SyntheticBlackboard { Flags = copy };
        }
    }
}
