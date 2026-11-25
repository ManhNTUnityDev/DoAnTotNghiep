#!/usr/bin/env bash
# Run a benchmark plan in the verify copy via batch mode, and copy the CSV back to the project.
# Usage: run-benchmark.sh <label> <matchups-csv> <matchesPerMatchup> <timeScale> [baseSeed]
#   e.g. run-benchmark.sh speed-1x bt_bt 10 1
#        run-benchmark.sh full bt_bt,fsm_fsm,bt_fsm,fsm_bt 30 3
set -uo pipefail
label=${1:?label}
matchups=${2:?matchups}
per=${3:?matchesPerMatchup}
scale=${4:?timeScale}
seed=${5:-1000}

PROJ='C:\Users\Admin\Desktop\Manh\UnityProjects\Game'
DST='C:\Users\Admin\AppData\Local\ChaseGameVerify'
VD=$(cygpath -u "$DST")
PD=$(cygpath -u "$PROJ")
UNITY='/c/Program Files/Unity/Hub/Editor/6000.0.72f1/Editor/Unity.exe'

for d in Assets Packages ProjectSettings; do
  robocopy.exe "$PROJ\\$d" "$DST\\$d" //MIR //NFL //NDL //NJH //NJS //NP >/dev/null
  rc=$?; [ $rc -ge 8 ] && { echo "robocopy $d failed ($rc)"; exit 2; }
done

# Frame cap: uncapped batch frames make per-frame moves fall under CharacterController.minMoveDistance.
mkdir -p "$VD/Assets/VerifyHarness"
cat > "$VD/Assets/VerifyHarness/FrameCap.cs" <<'CS'
using UnityEngine;
public static class VerifyHarnessFrameCap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Cap() { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120; }
}
CS

stamp=$(date +%Y%m%d-%H%M)
csv_win="$DST\\Benchmark\\results\\$stamp-$label.csv"
mkdir -p "$VD/Benchmark/results"
# Written with bash, not python: this sandbox hides %LOCALAPPDATA%\ChaseGameVerify from
# python (os.listdir cannot see it) while bash and Unity reach it normally.
quoted_matchups=$(printf '"%s",' ${matchups//,/ }); quoted_matchups=${quoted_matchups%,}
csv_json=${csv_win//\\/\\\\}
cat > "$VD/Benchmark/batch-plan.json" <<JSON
{
  "plan": {
    "label": "$label",
    "matchups": [$quoted_matchups],
    "matchesPerMatchup": $per,
    "timeScale": $scale,
    "baseSeed": $seed
  },
  "csvPath": "$csv_json"
}
JSON
[ -s "$VD/Benchmark/batch-plan.json" ] || { echo "failed to write plan"; exit 2; }

out="$DST\\results-benchmark.xml"; log="$DST\\unity-benchmark.log"
rm -f "$(cygpath -u "$out")"
echo "running $label: $matchups x$per @${scale}x"
"$UNITY" -batchmode -nographics -projectPath "$DST" -runTests -testPlatform PlayMode \
  -testFilter "ChaseGame.Tests.PlayMode.BenchmarkBatchRun" -testResults "$out" -logFile "$log"
code=$?
rm -f "$VD/Benchmark/batch-plan.json"

grep -o "\[Benchmark\] done.*\|\[Benchmark\] start.*" "$(cygpath -u "$log")" | tail -2
xml=$(cygpath -u "$out")
[ -f "$xml" ] && grep -o '<test-run[^>]*result="[^"]*"' "$xml" | head -1

src=$(cygpath -u "$csv_win")
if [ -f "$src" ]; then
  mkdir -p "$PD/Benchmark/results"
  cp "$src" "$PD/Benchmark/results/"
  echo "csv: Benchmark/results/$(basename "$src") ($(($(wc -l < "$src") - 1)) rows)"
else
  echo "NO CSV (unity exit $code); log tail:"; tail -20 "$(cygpath -u "$log")"
  exit 3
fi
