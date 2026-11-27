using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChaseGame.AI;
using ChaseGame.Abilities;
using ChaseGame.Characters;
using ChaseGame.Infrastructure;
using ChaseGame.Match;

namespace ChaseGame.Simulation
{
    // Plays a benchmark run: all-AI matches in the Game scene, one CSV row each, reloading
    // the scene between matches. Inert unless Begin is called, so normal play is untouched.
    //
    // An Editor menu item hands the plan over through a JSON file rather than a static field,
    // because entering play mode reloads the domain and clears statics.
    public sealed class SimulationRunner : MonoBehaviour
    {
        public const string PendingPlanFile = "Benchmark/pending-plan.json";

        private static SimulationRunner instance;

        private SimulationPlan plan;
        private string csvPath;
        private SimulationProgress progress;

        private MatchController controller;
        private readonly List<Character> chasers = new List<Character>();
        private readonly List<Character> runners = new List<Character>();
        private readonly HashSet<Character> alreadyJailed = new HashSet<Character>();
        private float? firstCaptureSeconds;
        private float? lastCaptureSeconds;
        private bool recording;

        public static bool IsRunning => instance != null;

        /// <summary>
        /// Whether finishing a run leaves play mode. True for a menu-launched run, which is
        /// meant to end by itself; a caller that is already driving play mode (the smoke test)
        /// sets it false, since exiting would tear down the caller too.
        /// </summary>
        public static bool ExitPlaymodeWhenDone = true;

        public static void Begin(SimulationPlan plan, string outputCsvPath)
        {
            if (instance != null)
            {
                Debug.LogWarning("[Benchmark] a run is already in progress");
                return;
            }

            var matches = plan.Expand();
            if (matches.Count == 0)
            {
                Debug.LogWarning("[Benchmark] plan expands to no matches");
                return;
            }

            var go = new GameObject("SimulationRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SimulationRunner>();
            instance.plan = plan;
            instance.csvPath = outputCsvPath;
            instance.progress = new SimulationProgress(matches);

            // Before the first scene loads, so SpawnManager sees the all-AI setup.
            instance.ApplyCurrentMatchSetup();
            SceneManager.sceneLoaded += instance.OnSceneLoaded;

            Debug.Log($"[Benchmark] start: {matches.Count} matches, {plan.timeScale:0.#}x → {outputCsvPath}");
        }

#if UNITY_EDITOR
        // Picks up the plan an Editor menu item left behind, before any scene loads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResumePendingPlan()
        {
            string path = System.IO.Path.Combine(ProjectRoot, PendingPlanFile);
            if (!System.IO.File.Exists(path))
            {
                // No plan means this is ordinary play, so nothing may be left over from a
                // simulation. With "Reload Domain" turned off, static state survives between Play
                // sessions, and an interrupted run would otherwise leave MatchSetup.Current set —
                // spawning the next ordinary match with no human player at all.
                if (instance == null)
                {
                    MatchSetup.Current = null;
                }

                return;
            }

            string json = System.IO.File.ReadAllText(path);
            System.IO.File.Delete(path); // one run per request, even if it throws below

            var request = JsonUtility.FromJson<PendingPlan>(json);
            if (request == null || request.plan == null || string.IsNullOrEmpty(request.csvPath))
            {
                Debug.LogError($"[Benchmark] unreadable plan file: {json}");
                return;
            }

            Begin(request.plan, request.csvPath);
        }
#endif

        // The folder that holds Assets/ — benchmark output belongs outside the asset database.
        public static string ProjectRoot => System.IO.Directory.GetParent(Application.dataPath).FullName;

        [System.Serializable]
        public sealed class PendingPlan
        {
            public SimulationPlan plan;
            public string csvPath;
        }

        private void ApplyCurrentMatchSetup()
        {
            var match = progress.Current.Value;
            MatchSetup.Current = new MatchSetupOptions
            {
                HasHumanPlayer = false,
                ChaserBrain = match.ChaserBrain,
                RunnerBrain = match.RunnerBrain,
            };
            Random.InitState(match.Seed);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneNames.Game)
            {
                return; // Loading/Menu: the runner only drives the Game scene.
            }

            chasers.Clear();
            runners.Clear();
            alreadyJailed.Clear();
            firstCaptureSeconds = null;
            lastCaptureSeconds = null;

            controller = FindFirstObjectByType<MatchController>();
            if (controller == null)
            {
                Debug.LogError("[Benchmark] no MatchController in the Game scene; stopping");
                Finish();
                return;
            }

            // Runs before MatchController.Start, so the match initializes with no role-reveal card.
            controller.RoleRevealDurationSeconds = 0f;
            controller.MatchEnded += OnMatchEnded;
            Time.timeScale = plan.timeScale;
            recording = true;
        }

        private void Update()
        {
            if (!recording || controller == null)
            {
                return;
            }

            if (runners.Count == 0)
            {
                CollectTeams();
                if (runners.Count == 0) return;
            }

            float elapsed = ElapsedMatchSeconds();
            foreach (var runner in runners)
            {
                if (runner != null && runner.CaptureState == CaptureState.Jailed && alreadyJailed.Add(runner))
                {
                    firstCaptureSeconds ??= elapsed;
                    lastCaptureSeconds = elapsed;
                }
            }
        }

        private float ElapsedMatchSeconds()
            => Mathf.Max(0f, controller.MatchDurationSeconds - controller.TimeRemaining);

        // The 8 spawned characters. Decoy clones carry a DecoyLure and are not roster
        // members, so their brains must not count toward a team's stats.
        private void CollectTeams()
        {
            chasers.Clear();
            runners.Clear();
            foreach (var character in FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (character.GetComponent<DecoyLure>() != null)
                {
                    continue;
                }

                (character.Team == Team.Chaser ? chasers : runners).Add(character);
            }
        }

        private void OnMatchEnded(MatchState result)
        {
            if (!recording)
            {
                return;
            }

            recording = false;
            var match = progress.Current.Value;
            if (!progress.TryRecord(match.Index))
            {
                return; // already written; never a second row for one match
            }

            CollectTeams();
            int caught = 0;
            foreach (var runner in runners)
            {
                if (runner != null && runner.CaptureState == CaptureState.Jailed) caught++;
            }

            var row = new MatchResultRow
            {
                Index = match.Index,
                Matchup = match.Matchup,
                Seed = match.Seed,
                ChaserBrain = match.ChaserBrain,
                RunnerBrain = match.RunnerBrain,
                Winner = result,
                RunnersCaught = caught,
                RunnersTotal = runners.Count,
                FirstCaptureSeconds = firstCaptureSeconds,
                LastCaptureSeconds = lastCaptureSeconds,
                MatchDurationSeconds = ElapsedMatchSeconds(),
                ChaserStats = MergeStats(chasers),
                RunnerStats = MergeStats(runners),
                TimeScale = plan.timeScale,
            };

            MatchCsv.AppendRow(csvPath, row);
            Debug.Log($"[Benchmark] {progress.Completed}/{progress.Total} {match.Matchup} seed={match.Seed} → {result} ({caught}/{runners.Count} caught)");
            StartCoroutine(NextMatch());
        }

        private static DecisionStats MergeStats(List<Character> team)
        {
            var parts = new List<DecisionStats>();
            foreach (var character in team)
            {
                if (character != null && character.TryGetComponent<AIBrain>(out var brain))
                {
                    parts.Add(brain.Stats);
                }
            }

            return DecisionStats.Merge(parts);
        }

        private IEnumerator NextMatch()
        {
            // Real time: the ended match left timeScale at 0.
            yield return new WaitForSecondsRealtime(0.5f);

            controller.MatchEnded -= OnMatchEnded;
            controller.PrepareSceneExit();

            if (progress.Advance())
            {
                ApplyCurrentMatchSetup();
                SceneManager.LoadScene(SceneNames.Game);
            }
            else
            {
                Finish();
            }
        }

        private void Finish()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            MatchSetup.Current = null;
            Time.timeScale = 1f;
            recording = false;
            instance = null;
            Debug.Log($"[Benchmark] done: {progress.Completed} matches → {csvPath}");
            Destroy(gameObject);

#if UNITY_EDITOR
            if (ExitPlaymodeWhenDone)
            {
                UnityEditor.EditorApplication.ExitPlaymode();
            }
#endif
        }
    }
}
