# Match End + UI — Design Spec

Date: 2026-09-23
Branch: master (sync `feat/joystick-input` to HEAD after each commit)
Builds on: NPC AI infrastructure (plans #1–#2, complete). EditMode baseline 77/77.

## 1. Goal

Add the match end-condition subsystem and its UI to the chase game:

- **Chasers win** when all runners are jailed (`ITeamRoster.AllRunnersJailed()`).
- **Runners win** when the countdown expires while ≥1 runner is still free.
- The end screen shows **BẠN THẮNG / BẠN THUA** from the **player's team** perspective.
- In-match HUD: countdown clock + free-runner count.
- End screen: result text, **Chơi lại** (reload scene), **Thoát** (quit).
- On match end: freeze the match; **Chơi lại** restores a clean state.

## 2. Locked decisions (brainstorming)

1. **Countdown = 90s**, exposed as `[SerializeField] matchDurationSeconds` on `MatchController` (Inspector-editable).
2. **Player-runner caught early → match continues (spectator).** Win/lose is decided by the player's team at match end, not by the player's personal capture.
3. **Chơi lại = reload the active scene** (`SceneManager.LoadScene`).
4. **Thoát present** (`Application.Quit`, editor: stop play).

## 3. Architecture — three layers (logic / glue / UI)

### 3.1 `MatchEvaluator` (PURE — no MonoBehaviour, no `UnityEngine.Time`)

The single testable unit. EditMode-tested (RED→GREEN before any glue).

```csharp
namespace ChaseGame.Match
{
    public enum MatchState { Ongoing, ChasersWin, RunnersWin }
    public enum PlayerOutcome { Win, Lose }

    public sealed class MatchEvaluator
    {
        // Priority: capturing every runner wins for chasers even at t == 0.
        public MatchState Evaluate(int freeRunnerCount, float timeRemaining)
        {
            if (freeRunnerCount <= 0) return MatchState.ChasersWin;
            if (timeRemaining <= 0f)  return MatchState.RunnersWin;
            return MatchState.Ongoing;
        }

        public PlayerOutcome ForPlayer(MatchState ended, Team playerTeam)
        {
            // ended is ChasersWin or RunnersWin (never Ongoing).
            bool chasersWon = ended == MatchState.ChasersWin;
            bool playerIsChaser = playerTeam == Team.Chaser;
            return chasersWon == playerIsChaser ? PlayerOutcome.Win : PlayerOutcome.Lose;
        }
    }
}
```

Rule notes:
- `freeRunnerCount <= 0` → `ChasersWin`.
- `timeRemaining <= 0` (with free runners) → `RunnersWin`.
- Else `Ongoing`.
- Capture-all takes priority over time-out at the exact same tick.

### 3.2 `MatchController` (MonoBehaviour in scene; services via `[Inject]`)

Glue only. Owns the countdown and the freeze; contains no win-rule branching beyond delegating to `MatchEvaluator`.

- `[SerializeField] float matchDurationSeconds = 90f;`
- `[SerializeField] MatchHudView hud;`  `[SerializeField] MatchEndView endView;` (drag-drop refs — no extra DI).
- `[Inject] Construct(ITeamRoster roster, MatchEvaluator evaluator)` (VContainer method injection; `RegisterComponentInHierarchy<MatchController>()`).
- State: `float timeRemaining` (init = `matchDurationSeconds`), `MatchState state = Ongoing`.
- `Update()` while `state == Ongoing`:
  1. `timeRemaining -= Time.deltaTime;`
  2. `int free = roster.GetFreeRunners().Count;`
  3. `hud.Render(timeRemaining, free, roster.Runners.Count);`
  4. `var next = evaluator.Evaluate(free, timeRemaining);`
  5. if `next != Ongoing` → `EndMatch(next)`.
- `EndMatch(MatchState result)`: `state = result; Time.timeScale = 0f;` then `endView.Show(evaluator.ForPlayer(result, roster.PlayerCharacter.Team));`
- Spectator (decision #2): the loop never ends on the player's personal capture; only capture-all or time-out ends it. Optional HUD hint when `roster.PlayerCharacter.CaptureState == Jailed`.
- **Replay:** `Time.timeScale = 1f;` **then** `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);`. `timeScale` MUST be reset first — it does not reset across scene loads (known trap; record in ledger).
- **Quit:** `Application.Quit();` wrapped `#if UNITY_EDITOR EditorApplication.isPlaying = false;`.

### 3.3 UI views (MonoBehaviour, on existing `UICanvas`)

- `MatchHudView`: `Render(float timeRemaining, int free, int total)` → clock `mm:ss` + `Tự do: n/total`. Always visible.
- `MatchEndView`: hidden panel. `Show(PlayerOutcome)` → set big text **BẠN THẮNG**/**BẠN THUA** (color by outcome), enable panel. Wires **Chơi lại** → `MatchController.Replay()`, **Thoát** → `MatchController.Quit()` (buttons hold refs, or controller assigns listeners).

## 4. DI wiring (`GameLifetimeScope`)

Add to `Configure`:
```csharp
builder.Register<MatchEvaluator>(Lifetime.Singleton);
builder.RegisterComponentInHierarchy<MatchController>();
```
No change to existing registrations. `MatchController` resolves `ITeamRoster` (already singleton) + `MatchEvaluator`.

## 5. Scene / prefab work (via MCP)

- New GameObject `MatchController` (empty) with `MatchController` component; set `matchDurationSeconds = 90`.
- Under `UICanvas`: `HUD` panel (clock Text + free-count Text) + `EndPanel` (result Text + Chơi lại Button + Thoát Button, initially inactive) with `MatchHudView` / `MatchEndView` components.
- Wire `MatchController.hud`/`.endView` and button targets via `SerializedObject`.

## 6. Test + verification plan

- **EditMode `MatchEvaluatorTests`** (~8 cases): capture-all→ChasersWin; time-out with free→RunnersWin; ongoing; boundary `t==0` with free>0→RunnersWin; capture-all at `t==0`→ChasersWin; `ForPlayer` × 4 (team × winning side). RED→GREEN before glue. Target suite ~85/85.
- After each code change: `refresh_unity(compile=request, mode=force, wait_for_ready=true)` → `read_console(types=["error"])` = 0 errors, then `run_tests` + `get_test_job`.
- **Playtest (real Game scene):** play until one side wins → correct BẠN THẮNG/THUA per player team → click Chơi lại → new match runs clean. Screenshot via `manage_camera`. `Application.runInBackground = true` at runtime if editor loses focus.

## 7. Definition of done

- EditMode green including `MatchEvaluatorTests`, 0 console errors.
- Real playtest: reach a win → correct player-perspective result → Chơi lại → fresh match runs → report result + screenshot.
- Commit per milestone (verify branch first; `git add` whole folders for `.meta`; revert `ProjectSettings/EditorSettings.asset` EnterPlayMode toggle; do NOT commit `Assets/Screenshots`; end message with the Co-Authored-By line; sync `feat/joystick-input` to master).

## 8. Out of scope

- Real jail rescue (capture stays a stub).
- Round/score persistence, main menu, multiple maps.
- Obstacle-carving navmesh, deferred perf items from plan #2.
