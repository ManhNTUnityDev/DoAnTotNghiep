using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using ChaseGame.AI;
using ChaseGame.Match;
using ChaseGame.Simulation;

namespace ChaseGame.Tests
{
    public class MatchCsvTests
    {
        private string tempPath;

        [TearDown]
        public void TearDown()
        {
            if (tempPath != null && File.Exists(tempPath)) File.Delete(tempPath);
            tempPath = null;
        }

        private static MatchResultRow SampleRow()
        {
            return new MatchResultRow
            {
                Index = 4,
                Matchup = "fsm_bt",
                Seed = 1004,
                ChaserBrain = BrainKind.Fsm,
                RunnerBrain = BrainKind.BehaviourTree,
                Winner = MatchState.ChasersWin,
                RunnersCaught = 3,
                RunnersTotal = 4,
                FirstCaptureSeconds = 6.5f,
                LastCaptureSeconds = 41.25f,
                MatchDurationSeconds = 62.5f,
                ChaserStats = new DecisionStats(),
                RunnerStats = new DecisionStats(),
                TimeScale = 3f,
            };
        }

        [Test]
        public void Format_UsesInvariantCultureAndColumnOrder()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                // vi-VN writes a decimal comma, which would shift every later CSV column.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                var fields = MatchCsv.Format(SampleRow()).Split(',');

                Assert.AreEqual(MatchCsv.Header.Split(',').Length, fields.Length);
                Assert.AreEqual("4", fields[0]);
                Assert.AreEqual("fsm_bt", fields[1]);
                Assert.AreEqual("1004", fields[2]);
                Assert.AreEqual("fsm", fields[3]);
                Assert.AreEqual("bt", fields[4]);
                Assert.AreEqual("ChasersWin", fields[5]);
                Assert.AreEqual("3", fields[6]);
                Assert.AreEqual("4", fields[7]);
                Assert.AreEqual("6.500", fields[8]);
                Assert.AreEqual("41.250", fields[9]);
                Assert.AreEqual("62.500", fields[10]);
                Assert.AreEqual("3.000", fields[fields.Length - 1]);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void Header_NamesEveryColumnInOrder()
        {
            Assert.AreEqual(
                "index,matchup,seed,chaser_brain,runner_brain,winner,runners_caught,runners_total," +
                "first_capture_s,last_capture_s,match_duration_s," +
                "chaser_mean_decide_ms,chaser_max_decide_ms,chaser_mean_sense_ms," +
                "runner_mean_decide_ms,runner_max_decide_ms,runner_mean_sense_ms," +
                "chaser_changes_per_min,runner_changes_per_min,time_scale",
                MatchCsv.Header);
        }

        [Test]
        public void Format_NoCapture_LeavesCaptureColumnsEmpty()
        {
            var row = SampleRow();
            row.RunnersCaught = 0;
            row.FirstCaptureSeconds = null;
            row.LastCaptureSeconds = null;

            var fields = MatchCsv.Format(row).Split(',');

            Assert.AreEqual(string.Empty, fields[8]);
            Assert.AreEqual(string.Empty, fields[9]);
        }

        [Test]
        public void Format_ChangesPerMinute_ScalesByMatchDuration()
        {
            var row = SampleRow();
            row.MatchDurationSeconds = 60f;
            for (int i = 0; i < 5; i++) row.ChaserStats.Record(0, 0, BehaviourId.Wander, BehaviourId.Chase);

            var fields = MatchCsv.Format(row).Split(',');

            Assert.AreEqual("5.000", fields[17], "5 changes over 60 s is 5 per minute");
            Assert.AreEqual("0.000", fields[18]);
        }

        [Test]
        public void AppendRow_WritesHeaderOnlyOnce()
        {
            tempPath = Path.Combine(Path.GetTempPath(), "chasegame-csv-test-" + System.Guid.NewGuid() + ".csv");

            MatchCsv.AppendRow(tempPath, SampleRow());
            MatchCsv.AppendRow(tempPath, SampleRow());

            var lines = File.ReadAllLines(tempPath);
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual(MatchCsv.Header, lines[0]);
            Assert.AreEqual(lines[1], lines[2]);
        }

        [Test]
        public void AppendRow_CreatesMissingDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "chasegame-csv-dir-" + System.Guid.NewGuid());
            tempPath = Path.Combine(dir, "results.csv");

            MatchCsv.AppendRow(tempPath, SampleRow());

            Assert.IsTrue(File.Exists(tempPath));
            File.Delete(tempPath);
            Directory.Delete(dir);
            tempPath = null;
        }
    }
}
