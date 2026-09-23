using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Pure match rules — no MonoBehaviour, no UnityEngine.Time. The only unit-tested piece.
    public sealed class MatchEvaluator
    {
        // Priority: capturing every runner wins for chasers even when time is also up.
        public MatchState Evaluate(int freeRunnerCount, float timeRemaining)
        {
            if (freeRunnerCount <= 0) return MatchState.ChasersWin;
            if (timeRemaining <= 0f)  return MatchState.RunnersWin;
            return MatchState.Ongoing;
        }

        // ended is ChasersWin or RunnersWin (never Ongoing).
        public PlayerOutcome ForPlayer(MatchState ended, Team playerTeam)
        {
            bool chasersWon = ended == MatchState.ChasersWin;
            bool playerIsChaser = playerTeam == Team.Chaser;
            return chasersWon == playerIsChaser ? PlayerOutcome.Win : PlayerOutcome.Lose;
        }
    }
}
