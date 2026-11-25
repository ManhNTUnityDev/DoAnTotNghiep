using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChaseGame.Simulation;

namespace ChaseGame.Tests.PlayMode
{
    // Runs a benchmark plan without the Editor UI, for a machine where the Editor is closed
    // (Unity refuses -executeMethod play mode, and a batch-mode PlayMode test is the only
    // reliable way to get a running game loop).
    //
    // Drop a plan at <project>/Benchmark/batch-plan.json, then:
    //   Unity.exe -batchmode -runTests -testPlatform PlayMode -testFilter BenchmarkBatchRun
    // With no plan file the test reports itself ignored, so a normal suite run skips it.
    //
    // Deliberately NOT the production Benchmark/pending-plan.json: that file auto-starts a run
    // on BeforeSceneLoad and would exit play mode under the test runner.
    public class BenchmarkBatchRun
    {
        public const string PlanFile = "Benchmark/batch-plan.json";

        private static string PlanPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, PlanFile);

        // The test framework's default is 180 s per test, which cuts a run off after a few
        // matches. A whole plan is the unit of work here, so allow three hours.
        [UnityTest, Timeout(3 * 60 * 60 * 1000)]
        public IEnumerator RunPlanFromFile()
        {
            if (!File.Exists(PlanPath))
            {
                Assert.Ignore($"no plan at {PlanFile}");
                yield break;
            }

            var request = JsonUtility.FromJson<SimulationRunner.PendingPlan>(File.ReadAllText(PlanPath));
            Assert.IsNotNull(request?.plan, $"unreadable plan at {PlanFile}");
            Assert.IsFalse(string.IsNullOrEmpty(request.csvPath), "plan must name csvPath");

            // The test runner owns play mode here, so the run must not end it.
            SimulationRunner.ExitPlaymodeWhenDone = false;
            try
            {
                SimulationRunner.Begin(request.plan, request.csvPath);
                UnityEngine.SceneManagement.SceneManager.LoadScene("Game");

                int matches = request.plan.Expand().Count;
                // Generous: a 90 s match at 1x, plus scene reloads and a safety factor.
                float budget = matches * 140f / Mathf.Max(1f, request.plan.timeScale) + 120f;
                float waited = 0f;
                while (waited < budget && SimulationRunner.IsRunning)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }

                Assert.IsFalse(SimulationRunner.IsRunning,
                    $"run did not finish within {budget:0} s of real time");
                Assert.IsTrue(File.Exists(request.csvPath), "run wrote no CSV");
                Assert.AreEqual(matches + 1, File.ReadAllLines(request.csvPath).Length,
                    "CSV should hold a header plus one row per match");
            }
            finally
            {
                SimulationRunner.ExitPlaymodeWhenDone = true;
                Time.timeScale = 1f;
            }
        }
    }
}
