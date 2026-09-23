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
            Assert.AreEqual(MatchState.ChasersWin, eval.Evaluate(0, 42f));
        }

        [Test]
        public void Evaluate_TimeUpWithFreeRunners_RunnersWin()
        {
            Assert.AreEqual(MatchState.RunnersWin, eval.Evaluate(2, 0f));
        }

        [Test]
        public void Evaluate_TimeLeftAndFreeRunners_Ongoing()
        {
            Assert.AreEqual(MatchState.Ongoing, eval.Evaluate(3, 15f));
        }

        [Test]
        public void Evaluate_NegativeTimeWithFreeRunners_RunnersWin()
        {
            Assert.AreEqual(MatchState.RunnersWin, eval.Evaluate(1, -0.5f));
        }

        [Test]
        public void Evaluate_CaptureAllAtTimeZero_ChasersWinTakesPriority()
        {
            Assert.AreEqual(MatchState.ChasersWin, eval.Evaluate(0, 0f));
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
