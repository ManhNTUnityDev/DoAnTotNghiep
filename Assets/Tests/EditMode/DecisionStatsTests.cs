using NUnit.Framework;
using ChaseGame.AI;

namespace ChaseGame.Tests
{
    public class DecisionStatsTests
    {
        [Test]
        public void Record_AccumulatesTotalsAndMax()
        {
            var stats = new DecisionStats();
            stats.Record(0.1, 0.5, BehaviourId.None, BehaviourId.Wander);
            stats.Record(0.3, 0.2, BehaviourId.Wander, BehaviourId.Chase);

            Assert.AreEqual(2, stats.Ticks);
            Assert.AreEqual(0.4, stats.TotalSenseMs, 1e-9);
            Assert.AreEqual(0.7, stats.TotalDecideMs, 1e-9);
            Assert.AreEqual(0.5, stats.MaxDecideMs, 1e-9);
            Assert.AreEqual(0.35, stats.MeanDecideMs, 1e-9);
            Assert.AreEqual(0.2, stats.MeanSenseMs, 1e-9);
        }

        [Test]
        public void Record_FirstBehaviourFromNone_IsNotAChange()
        {
            var stats = new DecisionStats();
            stats.Record(0.1, 0.1, BehaviourId.None, BehaviourId.Wander);
            Assert.AreEqual(0, stats.BehaviourChanges);
        }

        [Test]
        public void Record_SameBehaviour_IsNotAChange()
        {
            var stats = new DecisionStats();
            stats.Record(0.1, 0.1, BehaviourId.Chase, BehaviourId.Chase);
            Assert.AreEqual(0, stats.BehaviourChanges);
        }

        [Test]
        public void Record_DifferentBehaviour_IsAChange()
        {
            var stats = new DecisionStats();
            stats.Record(0.1, 0.1, BehaviourId.Wander, BehaviourId.Chase);
            stats.Record(0.1, 0.1, BehaviourId.Chase, BehaviourId.Shoot);
            Assert.AreEqual(2, stats.BehaviourChanges);
        }

        [Test]
        public void Mean_WithNoTicks_IsZero()
        {
            var stats = new DecisionStats();
            Assert.AreEqual(0, stats.Ticks);
            Assert.AreEqual(0.0, stats.MeanDecideMs, 1e-9);
            Assert.AreEqual(0.0, stats.MeanSenseMs, 1e-9);
        }

        [Test]
        public void Merge_SumsTicksAndTakesMaxOfMax()
        {
            var a = new DecisionStats();
            a.Record(0.1, 0.5, BehaviourId.Wander, BehaviourId.Chase);
            var b = new DecisionStats();
            b.Record(0.2, 0.9, BehaviourId.Flee, BehaviourId.Wander);
            b.Record(0.2, 0.1, BehaviourId.Wander, BehaviourId.Wander);

            var merged = DecisionStats.Merge(new[] { a, b });

            Assert.AreEqual(3, merged.Ticks);
            Assert.AreEqual(1.5, merged.TotalDecideMs, 1e-9);
            Assert.AreEqual(0.5, merged.TotalSenseMs, 1e-9);
            Assert.AreEqual(0.9, merged.MaxDecideMs, 1e-9);
            Assert.AreEqual(2, merged.BehaviourChanges);
        }

        [Test]
        public void Merge_EmptyOrNullEntries_IsEmptyStats()
        {
            var merged = DecisionStats.Merge(new DecisionStats[] { null });
            Assert.AreEqual(0, merged.Ticks);
            Assert.AreEqual(0.0, merged.MaxDecideMs, 1e-9);
        }
    }
}
