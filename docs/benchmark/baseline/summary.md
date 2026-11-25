# BT vs FSM benchmark summary

Matches: 120 · time scale: 3x

## Per matchup

| matchup | n | chaser win % | mean caught | median first capture (s) | median last capture (s) | median duration (s) | chaser decide ms (mean/max) | runner decide ms (mean/max) | chaser switches/min | runner switches/min |
| --- | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: |
| bt_bt | 30 | 96.7 | 3.97 | 4.83 | 20.91 | 21.27 | 0.0090 / 1.5380 | 0.0179 / 9.5720 | 162.0 | 42.7 |
| fsm_fsm | 30 | 100.0 | 4.00 | 5.30 | 17.19 | 17.21 | 0.0108 / 0.5890 | 0.0203 / 23.1760 | 190.2 | 48.2 |
| bt_fsm | 30 | 90.0 | 3.90 | 4.65 | 18.12 | 20.18 | 0.0096 / 0.7310 | 0.0232 / 37.7980 | 169.3 | 45.5 |
| fsm_bt | 30 | 83.3 | 3.80 | 5.30 | 21.15 | 21.76 | 0.0101 / 0.7080 | 0.0221 / 32.8930 | 183.2 | 42.9 |

Charts not produced (matplotlib unavailable): win_rate.png, capture_time_boxplot.png, decide_ms.png, behaviour_changes.png
