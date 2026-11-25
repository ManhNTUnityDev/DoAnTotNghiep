using System.Collections.Generic;

namespace ChaseGame.Simulation
{
    // Where a benchmark run has got to. Survives scene loads (the runner is persistent) and
    // refuses to record the same match twice, so a match that ends and reloads in the same
    // frame still writes exactly one CSV row.
    public sealed class SimulationProgress
    {
        private readonly List<SimulationMatch> matches;
        private readonly HashSet<int> recorded = new HashSet<int>();
        private int cursor;

        public SimulationProgress(IReadOnlyList<SimulationMatch> matches)
        {
            this.matches = new List<SimulationMatch>(matches ?? new List<SimulationMatch>());
        }

        public int Total => matches.Count;
        public int Completed => recorded.Count;

        public SimulationMatch? Current => cursor >= 0 && cursor < matches.Count ? matches[cursor] : (SimulationMatch?)null;

        // True the first time this match index is recorded; false afterwards.
        public bool TryRecord(int index) => recorded.Add(index);

        // Moves to the next match; false when the run is finished.
        public bool Advance()
        {
            if (cursor >= matches.Count)
            {
                return false;
            }

            cursor++;
            return cursor < matches.Count;
        }
    }
}
