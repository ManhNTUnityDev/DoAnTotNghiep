using NUnit.Framework;
using ChaseGame.Characters;
using ChaseGame.Infrastructure;
using ChaseGame.AI;

namespace ChaseGame.Tests
{
    public class SpawnManagerTests
    {
        [Test]
        public void PickPlayerTeam_LowRoll_IsChaser()
        {
            Assert.AreEqual(Team.Chaser, SpawnManager.PickPlayerTeam(0f));
            Assert.AreEqual(Team.Chaser, SpawnManager.PickPlayerTeam(0.49f));
        }

        [Test]
        public void PickPlayerTeam_HighRoll_IsRunner()
        {
            Assert.AreEqual(Team.Runner, SpawnManager.PickPlayerTeam(0.5f));
            Assert.AreEqual(Team.Runner, SpawnManager.PickPlayerTeam(0.99f));
        }

        [TearDown]
        public void TearDown() { MatchSetup.Current = null; }

        [Test]
        public void ResolveBrainKind_DefaultOptions_IsBehaviourTreeForBoth()
        {
            var options = MatchSetup.Effective;
            Assert.AreEqual(BrainKind.BehaviourTree, SpawnManager.ResolveBrainKind(Team.Chaser, options));
            Assert.AreEqual(BrainKind.BehaviourTree, SpawnManager.ResolveBrainKind(Team.Runner, options));
            Assert.IsTrue(options.HasHumanPlayer);
        }

        [Test]
        public void ResolveBrainKind_UsesPerTeamOption()
        {
            var options = new MatchSetupOptions { ChaserBrain = BrainKind.Fsm, RunnerBrain = BrainKind.BehaviourTree };
            Assert.AreEqual(BrainKind.Fsm, SpawnManager.ResolveBrainKind(Team.Chaser, options));
            Assert.AreEqual(BrainKind.BehaviourTree, SpawnManager.ResolveBrainKind(Team.Runner, options));
        }

        [Test]
        public void Effective_ReturnsCurrentWhenSet()
        {
            var options = new MatchSetupOptions { HasHumanPlayer = false };
            MatchSetup.Current = options;
            Assert.AreSame(options, MatchSetup.Effective);
        }

        [Test]
        public void IsPlayerSlot_NoHumanPlayer_AlwaysFalse()
        {
            var options = new MatchSetupOptions { HasHumanPlayer = false };
            for (int i = 0; i < 4; i++)
            {
                Assert.IsFalse(SpawnManager.IsPlayerSlot(Team.Chaser, i, Team.Chaser, options));
                Assert.IsFalse(SpawnManager.IsPlayerSlot(Team.Runner, i, Team.Runner, options));
            }
        }

        [Test]
        public void IsPlayerSlot_Default_OnlySlot0OfPlayerTeam()
        {
            var options = new MatchSetupOptions();
            Assert.IsTrue(SpawnManager.IsPlayerSlot(Team.Runner, 0, Team.Runner, options));
            Assert.IsFalse(SpawnManager.IsPlayerSlot(Team.Runner, 1, Team.Runner, options));
            Assert.IsFalse(SpawnManager.IsPlayerSlot(Team.Chaser, 0, Team.Runner, options));
        }
    }
}
