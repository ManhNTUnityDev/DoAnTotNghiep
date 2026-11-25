# BT vs FSM benchmark summary

Matches: 120 · time scale: 3x

## Per matchup

| matchup | n | chaser win % | mean caught | median first capture (s) | median last capture (s) | median duration (s) | chaser decide ms (mean/max) | runner decide ms (mean/max) | chaser switches/min | runner switches/min |
| --- | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: |
| bt_bt | 30 | 100.0 | 4.00 | 3.40 | 15.36 | 15.38 | 0.0112 / 1.4120 | 0.0232 / 15.6640 | 222.4 | 65.3 |
| fsm_fsm | 30 | 96.7 | 3.97 | 2.62 | 18.73 | 18.75 | 0.0101 / 0.4370 | 0.0221 / 4.3790 | 186.3 | 68.5 |
| bt_fsm | 30 | 93.3 | 3.93 | 3.12 | 17.34 | 19.95 | 0.0094 / 0.2650 | 0.0242 / 33.6690 | 183.2 | 69.9 |
| fsm_bt | 30 | 100.0 | 4.00 | 3.30 | 18.43 | 18.45 | 0.0100 / 0.2230 | 0.0256 / 34.5280 | 176.7 | 66.6 |

## Static metrics

| brain | team | kind | bt_nodes | fsm_states | fsm_transitions | loc |
| --- | --- | --- | --- | --- | --- | --- |
| ChaserAIBrain | chaser | bt | 9 | 0 | 0 | 69 |
| ChaserFsmBrain | chaser | fsm | 0 | 4 | 11 | 81 |
| RunnerAIBrain | runner | bt | 9 | 0 | 0 | 45 |
| RunnerFsmBrain | runner | fsm | 0 | 3 | 6 | 73 |
| AIActions | shared | - | 0 | 0 | 0 | 89 |
| ChaserActions | shared | - | 0 | 0 | 0 | 93 |
| RunnerActions | shared | - | 0 | 0 | 0 | 166 |
| AIBrain | shared | - | 0 | 0 | 0 | 76 |
| Blackboard | shared | - | 0 | 0 | 0 | 27 |

Charts not produced (matplotlib unavailable): win_rate.png, capture_time_boxplot.png, decide_ms.png, behaviour_changes.png, scaling.png, selection_cost.png
