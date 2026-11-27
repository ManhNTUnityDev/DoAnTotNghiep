# BT vs FSM benchmark summary

Matches: 120 · time scale: 3x

## Per matchup

| matchup | n | chaser win % | mean caught | median first capture (s) | median last capture (s) | median duration (s) | chaser decide ms (mean/max) | runner decide ms (mean/max) | chaser switches/min | runner switches/min |
| --- | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: |
| bt_bt | 30 | 96.7 | 3.97 | 4.83 | 20.91 | 21.27 | 0.0090 / 1.5380 | 0.0179 / 9.5720 | 162.0 | 42.7 |
| fsm_fsm | 30 | 100.0 | 4.00 | 5.30 | 17.19 | 17.21 | 0.0108 / 0.5890 | 0.0203 / 23.1760 | 190.2 | 48.2 |
| bt_fsm | 30 | 90.0 | 3.90 | 4.65 | 18.12 | 20.18 | 0.0096 / 0.7310 | 0.0232 / 37.7980 | 169.3 | 45.5 |
| fsm_bt | 30 | 83.3 | 3.80 | 5.30 | 21.15 | 21.76 | 0.0101 / 0.7080 | 0.0221 / 32.8930 | 183.2 | 42.9 |

## By architecture, per side

Win % carries a 95% Wilson interval (Wald gives a zero-width interval at 100%). Two
outcome columns: *last capture* averages every match that had a capture, *caught all*
only the matches that ended with every runner caught — the second is what "time to
catch them all" means.

| side | brain | n | chaser win % (95% CI) | last capture (s) | caught all: n | caught all (s) |
| --- | --- | --: | --: | --: | --: | --: |
| chaser | BT | 60 | 93.3 (84.1–97.4) | 21.91 ± 3.09 | 56 | 22.61 ± 3.22 |
| chaser | FSM | 60 | 91.7 (81.9–96.4) | 20.94 ± 3.42 | 55 | 20.44 ± 3.22 |
| runner | BT | 60 | 90.0 (79.9–95.3) | 22.66 ± 3.63 | 54 | 22.57 ± 3.53 |
| runner | FSM | 60 | 95.0 (86.3–98.3) | 20.20 ± 2.80 | 57 | 20.55 ± 2.91 |

Win-count difference, Fisher's exact test (two-sided):

- chaser: BT 56/60 vs FSM 55/60 — p = 1.000 (not significant at 0.05)
- runner: BT 54/60 vs FSM 57/60 — p = 0.491 (not significant at 0.05)

A win rate this close to the ceiling leaves the design almost no power to detect a
difference, so a non-significant result here is weak evidence either way. The
catch-all time is the column with real spread, and therefore the one to read.

## Charts

- `win_rate.png`
- `capture_time_boxplot.png`
- `decide_ms.png`
- `behaviour_changes.png`
