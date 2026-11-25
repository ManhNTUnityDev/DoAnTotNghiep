#!/usr/bin/env bash
# Mirror the checkout into the scratch verify copy and run Unity tests there in batch mode.
# Usage: run-unity-tests.sh <EditMode|PlayMode> [testFilter]
# Exit: 0 when the results XML reports result="Passed" and failed="0"; non-zero otherwise.
set -uo pipefail
mode=${1:?mode}
filter=${2:-}
SRC_DEFAULT='C:\Users\Admin\Desktop\Manh\UnityProjects\Game'
SRC=${SRC_OVERRIDE:-$SRC_DEFAULT}
DST='C:\Users\Admin\AppData\Local\ChaseGameVerify'
UNITY='/c/Program Files/Unity/Hub/Editor/6000.0.72f1/Editor/Unity.exe'
for d in Assets Packages ProjectSettings; do
  robocopy.exe "$SRC\\$d" "$DST\\$d" //MIR //NFL //NDL //NJH //NJS //NP >/dev/null
  rc=$?; [ $rc -ge 8 ] && { echo "robocopy $d failed ($rc)"; exit 2; }
done
# Batch mode runs frames uncapped; per-frame moves then fall under CharacterController.minMoveDistance
# and characters barely move. Cap the frame rate in the verify copy only.
VD=$(cygpath -u "$DST")
mkdir -p "$VD/Assets/VerifyHarness"
cat > "$VD/Assets/VerifyHarness/FrameCap.cs" <<'CS'
using UnityEngine;
public static class VerifyHarnessFrameCap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Cap() { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120; }
}
CS
if [ ! -d "$VD/Library" ]; then
  robocopy.exe "$SRC\\Library" "$DST\\Library" //E //NFL //NDL //NJH //NJS //NP //XD ScriptAssemblies Bee >/dev/null
fi
out="$DST\\results-$mode.xml"; log="$DST\\unity-$mode.log"
rm -f "$(cygpath -u "$out")"
args=(-batchmode -nographics -projectPath "$DST" -runTests -testPlatform "$mode" -testResults "$out" -logFile "$log")
[ -n "$filter" ] && args+=(-testFilter "$filter")
"$UNITY" "${args[@]}"
code=$?
xml=$(cygpath -u "$out")
if [ ! -f "$xml" ]; then echo "no results (unity exit $code); log tail:"; tail -30 "$(cygpath -u "$log")"; exit 3; fi
head -c 2000 "$xml" | grep -o '<test-run[^>]*>' | head -1
grep -o '<test-case [^>]*result="Failed"[^>]*>' "$xml" | grep -o 'fullname="[^"]*"' | head -20
# Green means nothing failed and nothing was inconclusive. A deliberately ignored test
# (BenchmarkBatchRun with no plan file) makes the run-level result "Skipped:Ignored", which
# is still a pass.
grep -q '<test-run[^>]*failed="0"' "$xml" && grep -q '<test-run[^>]*inconclusive="0"' "$xml"
