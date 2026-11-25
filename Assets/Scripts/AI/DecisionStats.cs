using System.Collections.Generic;

namespace ChaseGame.AI
{
    // Per-brain measurements collected over a match, for the BT vs FSM comparison.
    //
    // The decide timing includes the shared action work a tick performs (MoveTo,
    // NavMesh.SamplePosition and so on), because that is what a tick actually costs in a
    // live match. It answers "is this architecture light enough for 8 NPCs?", not "which
    // selection mechanism is cheaper" — that one needs the isolated selection benchmark.
    public sealed class DecisionStats
    {
        public int Ticks { get; private set; }
        public double TotalSenseMs { get; private set; }
        public double TotalDecideMs { get; private set; }
        public double MaxDecideMs { get; private set; }

        // Times the brain switched from one behaviour to a different one. The first
        // behaviour of a match (None → something) is not a switch.
        public int BehaviourChanges { get; private set; }

        public double MeanDecideMs => Ticks > 0 ? TotalDecideMs / Ticks : 0.0;
        public double MeanSenseMs => Ticks > 0 ? TotalSenseMs / Ticks : 0.0;

        public void Record(double senseMs, double decideMs, BehaviourId before, BehaviourId after)
        {
            Ticks++;
            TotalSenseMs += senseMs;
            TotalDecideMs += decideMs;
            if (decideMs > MaxDecideMs)
            {
                MaxDecideMs = decideMs;
            }

            if (before != BehaviourId.None && before != after)
            {
                BehaviourChanges++;
            }
        }

        public static DecisionStats Merge(IEnumerable<DecisionStats> parts)
        {
            var merged = new DecisionStats();
            if (parts == null)
            {
                return merged;
            }

            foreach (var part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                merged.Ticks += part.Ticks;
                merged.TotalSenseMs += part.TotalSenseMs;
                merged.TotalDecideMs += part.TotalDecideMs;
                merged.BehaviourChanges += part.BehaviourChanges;
                if (part.MaxDecideMs > merged.MaxDecideMs)
                {
                    merged.MaxDecideMs = part.MaxDecideMs;
                }
            }

            return merged;
        }
    }
}
