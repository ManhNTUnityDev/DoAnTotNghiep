using System;
using System.Collections.Generic;
using ChaseGame.AI;

namespace ChaseGame.Simulation
{
    // One match of a benchmark run. The matchup code is "<chaser>_<runner>" with each side
    // "bt" or "fsm", so "fsm_bt" means an FSM chaser team against a BT runner team.
    public readonly struct SimulationMatch
    {
        public readonly int Index;
        public readonly string Matchup;
        public readonly int Seed;
        public readonly BrainKind ChaserBrain;
        public readonly BrainKind RunnerBrain;

        public SimulationMatch(int index, string matchup, int seed)
        {
            if (string.IsNullOrEmpty(matchup))
            {
                throw new ArgumentException("matchup must be \"<chaser>_<runner>\"", nameof(matchup));
            }

            var parts = matchup.Split('_');
            if (parts.Length != 2)
            {
                throw new ArgumentException($"matchup \"{matchup}\" must have exactly two parts", nameof(matchup));
            }

            Index = index;
            Matchup = matchup;
            Seed = seed;
            ChaserBrain = ParseBrain(parts[0], matchup);
            RunnerBrain = ParseBrain(parts[1], matchup);
        }

        public static BrainKind ParseBrain(string code, string matchup = null)
        {
            switch (code)
            {
                case "bt": return BrainKind.BehaviourTree;
                case "fsm": return BrainKind.Fsm;
                default:
                    throw new ArgumentException(
                        $"unknown brain code \"{code}\"{(matchup != null ? $" in matchup \"{matchup}\"" : "")}; expected \"bt\" or \"fsm\"");
            }
        }

        public static string BrainCode(BrainKind kind) => kind == BrainKind.Fsm ? "fsm" : "bt";
    }

    // A benchmark run: which matchups to play, how many matches each, and how fast.
    // Serializable so an Editor menu item can hand it to the runner through a JSON file
    // (entering play mode reloads the domain and clears statics).
    [Serializable]
    public sealed class SimulationPlan
    {
        public const string MatchupBtBt = "bt_bt";
        public const string MatchupFsmFsm = "fsm_fsm";
        public const string MatchupBtFsm = "bt_fsm";
        public const string MatchupFsmBt = "fsm_bt";

        public static readonly string[] AllMatchups = { MatchupBtBt, MatchupFsmFsm, MatchupBtFsm, MatchupFsmBt };

        public string label = "full";
        public string[] matchups = (string[])AllMatchups.Clone();
        public int matchesPerMatchup = 30;
        public float timeScale = 3f;
        public int baseSeed = 1000;

        // Matches grouped by matchup, in table order. Every matchup replays the same seed
        // list, so the four matchups meet the same spawn/wander draws overall. A seed does
        // not reproduce one match exactly (tick phase and deltaTime vary) — results are
        // compared as distributions.
        public List<SimulationMatch> Expand()
        {
            var matches = new List<SimulationMatch>();
            foreach (var matchup in matchups)
            {
                for (int i = 0; i < matchesPerMatchup; i++)
                {
                    matches.Add(new SimulationMatch(matches.Count, matchup, baseSeed + i));
                }
            }

            return matches;
        }

        public static SimulationPlan Full() => new SimulationPlan();

        public static SimulationPlan Quick() => new SimulationPlan { label = "quick", matchesPerMatchup = 3 };

        public static SimulationPlan SpeedCheck(float timeScale) => new SimulationPlan
        {
            label = $"speed-{timeScale:0.#}x",
            matchups = new[] { MatchupBtBt },
            matchesPerMatchup = 10,
            timeScale = timeScale,
        };
    }
}
