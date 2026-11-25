using System.Globalization;
using System.IO;
using System.Text;
using ChaseGame.AI;
using ChaseGame.Match;

namespace ChaseGame.Simulation
{
    // One finished simulated match, as the CSV records it.
    public sealed class MatchResultRow
    {
        public int Index;
        public string Matchup;
        public int Seed;
        public BrainKind ChaserBrain;
        public BrainKind RunnerBrain;
        public MatchState Winner;
        public int RunnersCaught;
        public int RunnersTotal;

        // Match-clock seconds from the start of Playing; null when nobody was caught.
        public float? FirstCaptureSeconds;
        public float? LastCaptureSeconds;

        public float MatchDurationSeconds;
        public DecisionStats ChaserStats = new DecisionStats();
        public DecisionStats RunnerStats = new DecisionStats();
        public float TimeScale;
    }

    public static class MatchCsv
    {
        public const string Header =
            "index,matchup,seed,chaser_brain,runner_brain,winner,runners_caught,runners_total," +
            "first_capture_s,last_capture_s,match_duration_s," +
            "chaser_mean_decide_ms,chaser_max_decide_ms,chaser_mean_sense_ms," +
            "runner_mean_decide_ms,runner_max_decide_ms,runner_mean_sense_ms," +
            "chaser_changes_per_min,runner_changes_per_min,time_scale";

        public static string Format(MatchResultRow row)
        {
            var sb = new StringBuilder();
            sb.Append(row.Index.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.Matchup).Append(',');
            sb.Append(row.Seed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(SimulationMatch.BrainCode(row.ChaserBrain)).Append(',');
            sb.Append(SimulationMatch.BrainCode(row.RunnerBrain)).Append(',');
            sb.Append(row.Winner).Append(',');
            sb.Append(row.RunnersCaught.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.RunnersTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(NumOrEmpty(row.FirstCaptureSeconds)).Append(',');
            sb.Append(NumOrEmpty(row.LastCaptureSeconds)).Append(',');
            sb.Append(Num((double)row.MatchDurationSeconds)).Append(',');
            sb.Append(Num(row.ChaserStats.MeanDecideMs)).Append(',');
            sb.Append(Num(row.ChaserStats.MaxDecideMs)).Append(',');
            sb.Append(Num(row.ChaserStats.MeanSenseMs)).Append(',');
            sb.Append(Num(row.RunnerStats.MeanDecideMs)).Append(',');
            sb.Append(Num(row.RunnerStats.MaxDecideMs)).Append(',');
            sb.Append(Num(row.RunnerStats.MeanSenseMs)).Append(',');
            sb.Append(Num(PerMinute(row.ChaserStats.BehaviourChanges, row.MatchDurationSeconds))).Append(',');
            sb.Append(Num(PerMinute(row.RunnerStats.BehaviourChanges, row.MatchDurationSeconds))).Append(',');
            sb.Append(Num((double)row.TimeScale));
            return sb.ToString();
        }

        // Appends one row, writing the header first when the file is new or empty, and
        // flushes, so a run stopped halfway still leaves a readable CSV of whole rows.
        public static void AppendRow(string path, MatchResultRow row)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool needHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            using (var writer = new StreamWriter(path, append: true, encoding: new UTF8Encoding(false)))
            {
                if (needHeader)
                {
                    writer.WriteLine(Header);
                }

                writer.WriteLine(Format(row));
                writer.Flush();
            }
        }

        private static double PerMinute(int count, float seconds) => seconds > 0f ? count * 60.0 / seconds : 0.0;

        private static string Num(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        private static string NumOrEmpty(float? value) => value.HasValue ? Num((double)value.Value) : string.Empty;
    }
}
