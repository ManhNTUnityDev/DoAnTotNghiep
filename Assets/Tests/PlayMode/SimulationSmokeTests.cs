using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChaseGame.Simulation;

namespace ChaseGame.Tests.PlayMode
{
    // End-to-end proof of the benchmark pipeline: two all-AI matches in the real Game scene,
    // reloaded between them, each writing exactly one CSV row with usable numbers.
    // Runs at a high time scale, because it checks the plumbing, not the balance.
    public class SimulationSmokeTests
    {
        private string csvPath;

        [SetUp]
        public void SetUp()
        {
            SimulationRunner.ExitPlaymodeWhenDone = false; // exiting would tear down the test runner
            csvPath = Path.Combine(Path.GetTempPath(), "chasegame-smoke-" + System.Guid.NewGuid() + ".csv");
        }

        // Leaves a bare scene behind: this test loads the real Game scene, and its 8
        // characters would otherwise be left for whatever fixture runs next.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SimulationRunner.ExitPlaymodeWhenDone = true;
            Time.timeScale = 1f;
            if (File.Exists(csvPath)) File.Delete(csvPath);

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.name == "Game")
            {
                var blank = UnityEngine.SceneManagement.SceneManager.CreateScene(
                    "SmokeCleanup-" + System.Guid.NewGuid());
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(blank);
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(active);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoMatchPlan_WritesOneRowPerMatch()
        {
            var plan = new SimulationPlan
            {
                label = "smoke",
                matchups = new[] { SimulationPlan.MatchupBtBt, SimulationPlan.MatchupFsmFsm },
                matchesPerMatchup = 1,
                timeScale = 20f,
            };

            SimulationRunner.Begin(plan, csvPath);

            // Begin only prepares the setup; the Game scene has to arrive for a match to run.
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");

            float waited = 0f;
            while (waited < 120f && SimulationRunner.IsRunning)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Assert.IsFalse(SimulationRunner.IsRunning, "the run should have finished within 120 s of real time");
            Assert.IsTrue(File.Exists(csvPath), "the run should have written a CSV");

            var lines = File.ReadAllLines(csvPath);
            Assert.AreEqual(MatchCsv.Header, lines[0]);
            Assert.AreEqual(3, lines.Length, "header plus one row per match");

            var first = lines[1].Split(',');
            var second = lines[2].Split(',');
            Assert.AreEqual("bt_bt", first[1]);
            Assert.AreEqual("fsm_fsm", second[1]);

            foreach (var row in new[] { first, second })
            {
                Assert.AreNotEqual("Ongoing", row[5], "a recorded match must have a winner");
                Assert.AreEqual("4", row[7], "all four runners should have spawned");
                Assert.Greater(float.Parse(row[10], System.Globalization.CultureInfo.InvariantCulture), 1f,
                    "match duration should be real seconds");
                Assert.Greater(float.Parse(row[11], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    "chaser decide time should be measured");
                Assert.Greater(float.Parse(row[14], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    "runner decide time should be measured");
            }
        }
    }
}
