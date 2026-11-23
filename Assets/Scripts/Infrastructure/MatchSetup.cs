using ChaseGame.AI;

namespace ChaseGame.Infrastructure
{
    // How the next Game scene sets up its match. Normal play leaves Current null and gets
    // the defaults (one human player, BT brains for both teams); the benchmark simulation
    // sets it to run all-AI matches with a chosen brain kind per team.
    public sealed class MatchSetupOptions
    {
        public bool HasHumanPlayer = true;
        public BrainKind ChaserBrain = BrainKind.BehaviourTree;
        public BrainKind RunnerBrain = BrainKind.BehaviourTree;
    }

    public static class MatchSetup
    {
        public static MatchSetupOptions Current;

        public static MatchSetupOptions Effective => Current ?? new MatchSetupOptions();
    }
}
