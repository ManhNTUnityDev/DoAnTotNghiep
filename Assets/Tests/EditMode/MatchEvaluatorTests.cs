using NUnit.Framework;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class MatchEvaluatorTests
    {
        private readonly MatchEvaluator eval = new MatchEvaluator();

        [Test]
        public void Evaluate_AllRunnersJailed_ChasersWin()
        {
            Assert.AreEqual(MatchState.ChasersWin, eval.Evaluate(0, 4, 42f));
        }

        [Test]
        public void Evaluate_TimeUpWithFreeRunners_RunnersWin()
        {
            Assert.AreEqual(MatchState.RunnersWin, eval.Evaluate(2, 4, 0f));
        }

        [Test]
        public void Evaluate_TimeLeftAndFreeRunners_Ongoing()
        {
            Assert.AreEqual(MatchState.Ongoing, eval.Evaluate(3, 4, 15f));
        }

        [Test]
        public void Evaluate_NegativeTimeWithFreeRunners_RunnersWin()
        {
            Assert.AreEqual(MatchState.RunnersWin, eval.Evaluate(1, 4, -0.5f));
        }

        [Test]
        public void Evaluate_CaptureAllAtTimeZero_ChasersWinTakesPriority()
        {
            Assert.AreEqual(MatchState.ChasersWin, eval.Evaluate(0, 4, 0f));
        }

        [Test]
        public void Evaluate_NoRunnersSpawnedYet_Ongoing_NotChasersWin()
        {
            // Guard against the pre-spawn race: an empty roster (0 total) must NOT
            // count as "all runners jailed". Mirrors TeamRoster.AllRunnersJailed()'s
            // runners.Count > 0 guard. Even with time up, 0 total stays Ongoing.
            Assert.AreEqual(MatchState.Ongoing, eval.Evaluate(0, 0, 42f));
            Assert.AreEqual(MatchState.Ongoing, eval.Evaluate(0, 0, 0f));
        }

        [Test]
        public void ForPlayer_ChasersWin_ChaserPlayerWins()
        {
            Assert.AreEqual(PlayerOutcome.Win, eval.ForPlayer(MatchState.ChasersWin, Team.Chaser));
        }

        [Test]
        public void ForPlayer_ChasersWin_RunnerPlayerLoses()
        {
            Assert.AreEqual(PlayerOutcome.Lose, eval.ForPlayer(MatchState.ChasersWin, Team.Runner));
        }

        [Test]
        public void ForPlayer_RunnersWin_RunnerPlayerWins()
        {
            Assert.AreEqual(PlayerOutcome.Win, eval.ForPlayer(MatchState.RunnersWin, Team.Runner));
        }

        [Test]
        public void ForPlayer_RunnersWin_ChaserPlayerLoses()
        {
            Assert.AreEqual(PlayerOutcome.Lose, eval.ForPlayer(MatchState.RunnersWin, Team.Chaser));
        }
    }
}
