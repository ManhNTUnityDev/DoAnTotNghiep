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

        // Read twice per spawned character, so the no-simulation case shares one instance
        // rather than allocating a fresh default each time. Callers only read it.
        private static readonly MatchSetupOptions Defaults = new MatchSetupOptions();

        public static MatchSetupOptions Effective => Current ?? Defaults;
    }
}
