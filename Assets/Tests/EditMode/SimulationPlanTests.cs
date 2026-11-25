using System;
using System.Linq;
using NUnit.Framework;
using ChaseGame.AI;
using ChaseGame.Simulation;

namespace ChaseGame.Tests
{
    public class SimulationPlanTests
    {
        [Test]
        public void Expand_FullPlan_Has120MatchesInMatchupOrder()
        {
            var matches = SimulationPlan.Full().Expand();

            Assert.AreEqual(120, matches.Count);
            CollectionAssert.AreEqual(
                new[] { "bt_bt", "fsm_fsm", "bt_fsm", "fsm_bt" },
                matches.Select(m => m.Matchup).Distinct().ToArray());
            // Grouped, not interleaved: the first 30 are all the same matchup.
            Assert.IsTrue(matches.Take(30).All(m => m.Matchup == "bt_bt"));
            CollectionAssert.AreEqual(Enumerable.Range(0, 120).ToArray(), matches.Select(m => m.Index).ToArray());
        }

        [Test]
        public void Expand_SameSeedListForEveryMatchup()
        {
            var matches = SimulationPlan.Full().Expand();
            var byMatchup = matches.GroupBy(m => m.Matchup).Select(g => g.Select(m => m.Seed).ToArray()).ToArray();

            foreach (var seeds in byMatchup)
            {
                CollectionAssert.AreEqual(byMatchup[0], seeds);
            }
            Assert.AreEqual(1000, byMatchup[0][0]);
            Assert.AreEqual(1029, byMatchup[0][29]);
        }

        [Test]
        public void ParseMatchup_FsmBt_GivesFsmChaserBtRunner()
        {
            var match = new SimulationMatch(0, "fsm_bt", 7);
            Assert.AreEqual(BrainKind.Fsm, match.ChaserBrain);
            Assert.AreEqual(BrainKind.BehaviourTree, match.RunnerBrain);

            var other = new SimulationMatch(1, "bt_fsm", 7);
            Assert.AreEqual(BrainKind.BehaviourTree, other.ChaserBrain);
            Assert.AreEqual(BrainKind.Fsm, other.RunnerBrain);
        }

        [Test]
        public void ParseMatchup_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SimulationMatch(0, "bt_goap", 1));
            Assert.Throws<ArgumentException>(() => new SimulationMatch(0, "bt", 1));
            Assert.Throws<ArgumentException>(() => new SimulationMatch(0, null, 1));
        }

        [Test]
        public void QuickPlan_Has12Matches_SpeedCheckIsLabelled()
        {
            Assert.AreEqual(12, SimulationPlan.Quick().Expand().Count);

            var oneX = SimulationPlan.SpeedCheck(1f);
            Assert.AreEqual("speed-1x", oneX.label);
            Assert.AreEqual(10, oneX.Expand().Count);
            Assert.IsTrue(oneX.Expand().All(m => m.Matchup == "bt_bt"));
            Assert.AreEqual("speed-3x", SimulationPlan.SpeedCheck(3f).label);
        }

        [Test]
        public void SimulationProgress_RecordTwiceForSameIndex_KeepsOneRow()
        {
            var progress = new SimulationProgress(SimulationPlan.Quick().Expand());

            Assert.IsTrue(progress.TryRecord(0));
            Assert.IsFalse(progress.TryRecord(0), "the same match must never write a second row");
            Assert.AreEqual(1, progress.Completed);
        }

        [Test]
        public void SimulationProgress_AdvancePastLast_ReturnsFalse()
        {
            var matches = SimulationPlan.SpeedCheck(1f).Expand();
            var progress = new SimulationProgress(matches);

            Assert.IsNotNull(progress.Current);
            Assert.AreEqual(matches[0].Seed, progress.Current.Value.Seed);

            for (int i = 0; i < matches.Count - 1; i++)
            {
                Assert.IsTrue(progress.Advance(), $"advance {i} should succeed");
            }

            Assert.AreEqual(matches[matches.Count - 1].Index, progress.Current.Value.Index);
            Assert.IsFalse(progress.Advance());
            Assert.IsNull(progress.Current);
        }
    }
}
