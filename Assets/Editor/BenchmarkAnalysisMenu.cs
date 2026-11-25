using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.Analysis;
using ChaseGame.AI.BehaviourTree;
using ChaseGame.AI.Fsm;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.EditorTools
{
    // Static analysis for the BT vs FSM comparison: the N = 2…10 scaling series and the
    // structure/size table for the game's own brains. Pure Editor work — no play mode — so
    // both entry points also run under -batchmode -executeMethod.
    public static class BenchmarkAnalysisMenu
    {
        private const int MinN = 2;
        private const int MaxN = 10;
        private const int WarmupTicks = 10_000;
        private const int MeasuredTicks = 200_000;

        private static string ResultsDir =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Benchmark", "results");

        [MenuItem("ChaseGame/Benchmark/Scaling Series", false, 140)]
        public static void WriteScalingSeries()
        {
            var sb = new StringBuilder();
            sb.AppendLine("n,bt_nodes,fsm_pure_transitions,fsm_anystate_transitions," +
                          "bt_ns_per_tick,fsm_pure_ns_per_tick,fsm_any_ns_per_tick");

            for (int n = MinN; n <= MaxN; n++)
            {
                var tree = PriorityModel.BuildTree(n);
                var pure = PriorityModel.BuildPureFsm(n, 0);
                var any = PriorityModel.BuildAnyStateFsm(n, 0);

                double treeNs = MeasureNsPerTick(n, bb => tree.Tick(bb));
                double pureNs = MeasureNsPerTick(n, bb => pure.Tick(bb));
                double anyNs = MeasureNsPerTick(n, bb => any.Tick(bb));

                sb.AppendLine(string.Join(",",
                    n.ToString(CultureInfo.InvariantCulture),
                    BehaviourTreeStats.CountNodes(tree).ToString(CultureInfo.InvariantCulture),
                    pure.TransitionCount.ToString(CultureInfo.InvariantCulture),
                    any.TransitionCount.ToString(CultureInfo.InvariantCulture),
                    treeNs.ToString("0.0", CultureInfo.InvariantCulture),
                    pureNs.ToString("0.0", CultureInfo.InvariantCulture),
                    anyNs.ToString("0.0", CultureInfo.InvariantCulture)));
            }

            Write("scaling.csv", sb.ToString());
        }

        // Every model sees the same pseudo-random flag sequence, so the only difference
        // measured is how each one decides.
        private static double MeasureNsPerTick(int n, Action<SyntheticBlackboard> tick)
        {
            var bb = new SyntheticBlackboard { Flags = new bool[n - 1] };
            var random = new System.Random(42);

            for (int i = 0; i < WarmupTicks; i++)
            {
                Randomize(bb.Flags, random);
                tick(bb);
            }

            random = new System.Random(42);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < MeasuredTicks; i++)
            {
                Randomize(bb.Flags, random);
                tick(bb);
            }

            watch.Stop();
            return watch.Elapsed.TotalMilliseconds * 1_000_000.0 / MeasuredTicks;
        }

        private static void Randomize(bool[] flags, System.Random random)
        {
            for (int i = 0; i < flags.Length; i++)
            {
                flags[i] = random.Next(4) == 0; // sparse, so the default behaviour is common
            }
        }

        [MenuItem("ChaseGame/Benchmark/Static Metrics", false, 141)]
        public static void WriteStaticMetrics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("brain,team,kind,bt_nodes,fsm_states,fsm_transitions,loc");

            foreach (var (type, team) in new[]
            {
                (typeof(ChaserAIBrain), Team.Chaser),
                (typeof(ChaserFsmBrain), Team.Chaser),
                (typeof(RunnerAIBrain), Team.Runner),
                (typeof(RunnerFsmBrain), Team.Runner),
            })
            {
                var brain = Instantiate(type, team, out var host);
                try
                {
                    int nodes = brain is BehaviourTreeBrain bt ? BehaviourTreeStats.CountNodes(bt.Root) : 0;
                    int states = brain is FsmBrain fsm ? fsm.Machine.States.Count : 0;
                    int rows = brain is FsmBrain fsm2 ? fsm2.Machine.TransitionCount : 0;

                    sb.AppendLine(string.Join(",",
                        type.Name,
                        team.ToString().ToLowerInvariant(),
                        brain.Kind == BrainKind.Fsm ? "fsm" : "bt",
                        nodes.ToString(CultureInfo.InvariantCulture),
                        states.ToString(CultureInfo.InvariantCulture),
                        rows.ToString(CultureInfo.InvariantCulture),
                        CountLines(type).ToString(CultureInfo.InvariantCulture)));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }

            // The action code both architectures call. Listed separately so a reader can see
            // how much of each brain's behaviour is shared rather than architectural.
            foreach (var shared in new[]
            {
                "Assets/Scripts/AI/Actions/AIActions.cs",
                "Assets/Scripts/AI/Actions/ChaserActions.cs",
                "Assets/Scripts/AI/Actions/RunnerActions.cs",
                "Assets/Scripts/AI/AIBrain.cs",
                "Assets/Scripts/AI/BehaviourTree/Blackboard.cs",
            })
            {
                sb.AppendLine(string.Join(",",
                    Path.GetFileNameWithoutExtension(shared), "shared", "-", "0", "0", "0",
                    CountLines(shared).ToString(CultureInfo.InvariantCulture)));
            }

            Write("static-metrics.csv", sb.ToString());
        }

        private static AIBrain Instantiate(Type type, Team team, out GameObject host)
        {
            host = new GameObject("MetricsProbe");
            host.hideFlags = HideFlags.HideAndDontSave;
            var character = host.AddComponent<Character>();
            character.SetTeamForTests(team);
            var brain = (AIBrain)host.AddComponent(type);
            var config = ScriptableObject.CreateInstance<AIConfig>();
            brain.Initialize(character, new TeamRoster(), config);
            UnityEngine.Object.DestroyImmediate(config);
            return brain;
        }

        private static int CountLines(Type type)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{type.Name} t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return CountLines(path);
                }
            }

            Debug.LogWarning($"[Benchmark] no script asset found for {type.Name}; loc reported as 0");
            return 0;
        }

        // Non-blank lines that are not line comments — a plain size measure, no parsing.
        private static int CountLines(string assetPath)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            if (!File.Exists(full))
            {
                Debug.LogWarning($"[Benchmark] missing {assetPath}; loc reported as 0");
                return 0;
            }

            int count = 0;
            foreach (var line in File.ReadAllLines(full))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void Write(string name, string contents)
        {
            Directory.CreateDirectory(ResultsDir);
            string path = Path.Combine(ResultsDir, name);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            Debug.Log($"[Benchmark] wrote {path}");
        }
    }
}
