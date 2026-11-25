# Post-extension run — statistical reading

120 matches at 3x, 30 per matchup, after each brain gained one behaviour (chaser: search where
the target was lost; runner: hide behind obstacles). Intervals are 95% (1.96 × standard error).

## Do the architectures still play the same? (n = 60 per side)

| side | brain | chaser win % | time to catch all (s) |
| --- | --- | --: | --: |
| chaser | BT | 96.7 ± 4.5 | 21.10 ± 3.82 (sd 15.1) |
| chaser | FSM | 98.3 ± 3.2 | 23.58 ± 4.59 (sd 18.1) |
| runner | BT | 100.0 ± 0.0 | 21.74 ± 4.11 (sd 16.3) |
| runner | FSM | 95.0 ± 5.5 | 22.94 ± 4.35 (sd 17.2) |

Every interval still overlaps its counterpart. The equivalence held through the extension: the
new behaviour was implemented to the same effect in both architectures, which is what makes the
cost figures below a comparison of architectures rather than of two different NPCs.

## Did the new behaviours actually change anything? (whole run, 120 matches)

| metric | baseline | extended |
| --- | --: | --: |
| chaser win % | 92.5 | 97.5 |
| time to catch all (s) | 21.43 ± 2.29 | 22.34 ± 2.98 |
| chaser switches/min | 176.2 | 192.1 |
| runner switches/min | 44.8 | 67.6 |
| chaser mean decide (ms) | 0.0099 | 0.0102 |
| runner mean decide (ms) | 0.0209 | 0.0238 |

Chaser win rate rises 92.5% → 97.5% and runner behaviour switching rises 51%, so both new
behaviours are genuinely running rather than sitting unreachable. Time to catch all is not
clearly different (intervals overlap). Per-tick cost rises 3% for chasers and 14% for runners —
the cover search is the more expensive addition, and both remain far below any budget that
matters.

## Per matchup (n = 30 each)

| matchup | chaser win % | time to catch all (s) |
| --- | --: | --: |
| bt_bt | 100.0 | 21.82 ± 6.31 |
| fsm_fsm | 96.7 | 25.49 ± 7.46 |
| bt_fsm | 93.3 | 20.39 ± 4.42 |
| fsm_bt | 100.0 | 21.66 ± 5.38 |

## Cost of adding one behaviour

Measured from the per-commit diffs in `../extension-diffs.txt`; shared code landed in separate
commits first, so each figure is that brain's decision code alone.

| brain | structure before → after | production lines added |
| --- | --- | --: |
| chaser BT | 8 → 9 nodes (+1) | 76 |
| chaser FSM | 6 → 11 transitions (+5) | 76 |
| runner BT | 5 → 9 nodes (+4) | 39 |
| runner FSM | 2 → 6 transitions (+4) | 68 |

Line counts are comparable. The structural growth is not: one new chaser behaviour cost the tree
a single branch and the FSM five transitions, because every existing state needs a way into the
new one and back out.

## Structure size and selection cost against behaviour count

From `scaling.csv` — a synthetic priority problem with N behaviours, built as a tree, a textbook
FSM and an FSM allowed "from any state" rows. `PriorityModelTests` checks all three pick the same
behaviour in every flag combination from every start state.

| N | BT nodes | FSM transitions (pure) | FSM transitions (any-state) | BT ns/tick | FSM pure ns/tick | FSM any ns/tick |
| --: | --: | --: | --: | --: | --: | --: |
| 2 | 5 | 2 | 2 | 27.0 | 75.1 | 74.7 |
| 3 | 8 | 6 | 3 | 42.2 | 119.5 | 98.6 |
| 4 | 11 | 12 | 4 | 57.2 | 174.1 | 116.5 |
| 5 | 14 | 20 | 5 | 74.6 | 238.0 | 138.8 |
| 6 | 17 | 30 | 6 | 82.6 | 297.2 | 151.5 |
| 7 | 20 | 42 | 7 | 95.8 | 362.2 | 168.3 |
| 8 | 23 | 56 | 8 | 109.7 | 425.0 | 185.0 |
| 9 | 26 | 72 | 9 | 127.6 | 499.8 | 204.8 |
| 10 | 29 | 90 | 10 | 134.4 | 575.9 | 217.7 |

BT nodes follow 3N − 1 (linear). Pure FSM transitions follow N(N − 1) — the quadratic growth the
theory predicts. Two honest qualifications:

- An FSM with "from any state" transitions needs only N rows, which is linear, the same order as
  the tree. The quadratic figure describes the textbook form, not every FSM one could write.
- In absolute terms none of this costs anything. At N = 10 a selection takes 134 ns (BT) or 576 ns
  (pure FSM). With 8 NPCs deciding 8 times a second, even the slowest is about 37 µs per second of
  CPU. The case for the tree is maintainability, not speed.

## Static metrics

| file | team | kind | BT nodes | FSM states | FSM transitions | lines |
| --- | --- | --- | --: | --: | --: | --: |
| ChaserAIBrain | chaser | bt | 9 | — | — | 69 |
| ChaserFsmBrain | chaser | fsm | — | 4 | 11 | 81 |
| RunnerAIBrain | runner | bt | 9 | — | — | 45 |
| RunnerFsmBrain | runner | fsm | — | 3 | 6 | 73 |
| AIActions | shared | — | — | — | — | 89 |
| ChaserActions | shared | — | — | — | — | 93 |
| RunnerActions | shared | — | — | — | — | 166 |
| AIBrain | shared | — | — | — | — | 76 |
| Blackboard | shared | — | — | — | — | 27 |

The shared action code is 451 lines against 69–81 per brain, so most of what an NPC does is the
same code either way. The architecture is the thin layer that chooses between those actions —
worth remembering before reading much into either brain's line count.
