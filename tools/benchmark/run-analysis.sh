#!/usr/bin/env bash
# Run the static analysis Editor entry points in the verify copy via -executeMethod
# (pure Editor work, so no play mode is needed), then copy the CSVs back to the project.
set -uo pipefail
PROJ='C:\Users\Admin\Desktop\Manh\UnityProjects\Game'
DST='C:\Users\Admin\AppData\Local\ChaseGameVerify'
VD=$(cygpath -u "$DST")
PD=$(cygpath -u "$PROJ")
UNITY='/c/Program Files/Unity/Hub/Editor/6000.0.72f1/Editor/Unity.exe'

for d in Assets Packages ProjectSettings; do
  robocopy.exe "$PROJ\\$d" "$DST\\$d" //MIR //NFL //NDL //NJH //NJS //NP >/dev/null
  rc=$?; [ $rc -ge 8 ] && { echo "robocopy $d failed ($rc)"; exit 2; }
done

for m in WriteScalingSeries WriteStaticMetrics; do
  log="$DST\\unity-analysis-$m.log"
  echo "running $m"
  "$UNITY" -batchmode -nographics -quit -projectPath "$DST" \
    -executeMethod ChaseGame.EditorTools.BenchmarkAnalysisMenu.$m -logFile "$log"
  code=$?
  grep -o "\[Benchmark\] wrote.*\|error CS[0-9]*.*" "$(cygpath -u "$log")" | head -3
  [ $code -ne 0 ] && { echo "$m failed ($code); log tail:"; tail -20 "$(cygpath -u "$log")"; exit 3; }
done

mkdir -p "$PD/Benchmark/results"
for f in scaling.csv static-metrics.csv; do
  if [ -f "$VD/Benchmark/results/$f" ]; then
    cp "$VD/Benchmark/results/$f" "$PD/Benchmark/results/"
    echo "copied $f"
  else
    echo "MISSING $f"; exit 4
  fi
done
