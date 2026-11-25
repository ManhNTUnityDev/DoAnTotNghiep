using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChaseGame.Simulation;

namespace ChaseGame.EditorTools
{
    // Launches a benchmark run: writes the plan where SimulationRunner picks it up before the
    // first scene loads, opens the Game scene, and enters play mode. The run exits play mode
    // by itself when the last match is recorded.
    public static class BenchmarkMenu
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("ChaseGame/Benchmark/Run Full (4x30 @3x)", false, 100)]
        public static void RunFull() => Launch(SimulationPlan.Full());

        [MenuItem("ChaseGame/Benchmark/Run Quick (4x3 @3x)", false, 101)]
        public static void RunQuick() => Launch(SimulationPlan.Quick());

        [MenuItem("ChaseGame/Benchmark/Speed Check 1x (bt_bt x10)", false, 120)]
        public static void SpeedCheck1X() => Launch(SimulationPlan.SpeedCheck(1f));

        [MenuItem("ChaseGame/Benchmark/Speed Check 3x (bt_bt x10)", false, 121)]
        public static void SpeedCheck3X() => Launch(SimulationPlan.SpeedCheck(3f));

        public static void Launch(SimulationPlan plan)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Benchmark] already in play mode; stop it first");
                return;
            }

            string root = Directory.GetParent(Application.dataPath).FullName;
            string csvPath = Path.Combine(root, "Benchmark", "results",
                $"{DateTime.Now:yyyyMMdd-HHmm}-{plan.label}.csv");

            string pendingPath = Path.Combine(root, SimulationRunner.PendingPlanFile);
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath));
            File.WriteAllText(pendingPath, JsonUtility.ToJson(
                new SimulationRunner.PendingPlan { plan = plan, csvPath = csvPath }, true));

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                File.Delete(pendingPath);
                return;
            }

            EditorSceneManager.OpenScene(GameScenePath);
            Debug.Log($"[Benchmark] queued {plan.label}: {plan.Expand().Count} matches → {csvPath}");
            EditorApplication.EnterPlaymode();
        }
    }
}
