# Benchmark tools

`summarize.py` turns a simulation CSV into `summary.md` and charts.

```powershell
python tools/benchmark/summarize.py Benchmark/results/<run>.csv --out docs/benchmark/<run>
```

Add `--scaling Benchmark/results/scaling.csv` and `--static Benchmark/results/static-metrics.csv`
to include the scaling series and the static metrics table.

Charts need `matplotlib` (`python -m pip install matplotlib`); without it the tables are still
written and the skipped charts are listed. The CSVs come from the Editor menu `ChaseGame/Benchmark`.
