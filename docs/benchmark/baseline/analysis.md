# Baseline run — statistical reading

120 matches at 3x, 30 per matchup, before the extension experiment (chaser 3 states, runner 2).
Source: `Benchmark/results/20261125-1110-full.csv`. Intervals are 95% (1.96 × standard error).

## Does either side's architecture change how the game plays? (n = 60 per side)

| side | brain | chaser win % | mean runners caught | time to catch all (s) |
| --- | --- | --: | --: | --: |
| chaser | BT | 93.3 ± 6.3 | 3.93 | 21.91 ± 3.09 (sd 12.2) |
| chaser | FSM | 91.7 ± 7.0 | 3.90 | 20.94 ± 3.42 (sd 13.5) |
| runner | BT | 90.0 ± 7.6 | 3.88 | 22.66 ± 3.63 (sd 14.3) |
| runner | FSM | 95.0 ± 5.5 | 3.95 | 20.20 ± 2.80 (sd 11.1) |

Every interval overlaps its counterpart, on win rate and on time to catch all. The data does not
separate the two architectures on play outcome.

## Per matchup (n = 30 each)

| matchup | chaser win % | time to catch all (s) |
| --- | --: | --: |
| bt_bt | 96.7 ± 6.4 | 23.54 ± 4.58 (sd 12.79) |
| fsm_fsm | 100.0 ± 0.0 | 20.11 ± 3.85 (sd 10.76) |
| bt_fsm | 90.0 ± 10.7 | 20.29 ± 4.14 (sd 11.57) |
| fsm_bt | 83.3 ± 13.3 | 21.78 ± 5.70 (sd 15.93) |

`fsm_bt` (83.3%) against `fsm_fsm` (100%) is the widest apparent gap, but the intervals are ±13.3
and ±0.0 across four comparisons, and the gap disappears in the 2 × 2 grouping above. Treat the
per-matchup spread as noise.

## Decision cost per tick (ms)

| side | brain | mean | median of per-match max | worst single tick |
| --- | --- | --: | --: | --: |
| chaser | BT | 0.0093 | 0.135 | 1.5 |
| chaser | FSM | 0.0105 | 0.150 | 0.7 |
| runner | BT | 0.0200 | 1.376 | 32.9 |
| runner | FSM | 0.0217 | 1.356 | 37.8 |

These include the shared action work a tick performs (`MoveTo`, `NavMesh.SamplePosition`), which
dominates: both architectures call identical action code. Runner ticks cost about twice a chaser's
because of Decoy clone spawning, and the worst single ticks (30 ms+) are one-off spikes of that
kind, not a property of either architecture. Isolating selection cost needs the separate EditMode
benchmark.

## Behaviour switches per minute

| side | BT | FSM |
| --- | --: | --: |
| chaser | 165.7 ± 21.5 | 186.7 ± 24.7 |
| runner | 42.8 ± 6.9 | 46.8 ± 6.6 |

Overlapping. Comparable within this run only — see the time-scale caveat in `../README.md`.

## Ceiling effect to note

Chasers win 90–100% in every matchup: 4 chasers against 4 runners with no rescue mechanic, so the
count of free runners only falls. Win rate therefore has little room to discriminate anything.
Time to catch all varies properly (sd ≈ 12 s on a ≈ 21 s mean) and is the usable outcome metric.
