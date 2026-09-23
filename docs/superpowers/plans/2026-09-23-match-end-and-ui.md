# Match End + UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add match end-conditions (chasers win on capture-all, runners win on time-out) with an in-match HUD and an end screen (BẠN THẮNG/THUA + Chơi lại + Thoát), from the player's team perspective.

**Architecture:** A pure `MatchEvaluator` (no MonoBehaviour, no `Time`) decides `Ongoing/ChasersWin/RunnersWin` and maps a result to the player's `Win/Lose`; it is the only unit-tested piece. A scene `MatchController` MonoBehaviour is glue: it owns the `[SerializeField]` countdown, ticks it with `Time.deltaTime`, reads `ITeamRoster.GetFreeRunners().Count`, delegates the verdict to the evaluator, freezes with `Time.timeScale = 0`, and drives two UI views (`MatchHudView`, `MatchEndView`). A pure `MatchClock.Format` renders `mm:ss`.

**Tech Stack:** Unity 6 (6000.0.72f1), URP, C#, VContainer DI, NUnit EditMode tests run via UnityMCP (`run_tests` + `get_test_job`).

**Spec:** `docs/superpowers/specs/2026-09-23-match-end-and-ui-design.md`

## Global Constraints

- Assembly for runtime code: `ChaseGame` (`Assets/Scripts`); tests: `ChaseGame.Tests.EditMode` (`Assets/Tests/EditMode`), namespace `ChaseGame.Tests`.
- Runtime namespaces: match logic in `ChaseGame.Match`; scene glue/UI in `ChaseGame.Match` (views are MonoBehaviours). Follow existing file layout under `Assets/Scripts/Match`.
- Do NOT rewrite existing NPC/AI/BehaviourTree code. Build only on `ITeamRoster`, `ICaptureService`, `MatchEvaluator`, `Character`.
- Countdown default = `90f`, exposed as `[SerializeField] float matchDurationSeconds` on `MatchController`.
- Chơi lại = reset `Time.timeScale = 1f` **then** `SceneManager.LoadScene(active)`. Thoát = `Application.Quit()` (`#if UNITY_EDITOR` → `EditorApplication.isPlaying = false`).
- Player-runner caught early does NOT end the match (spectator); the verdict is decided by team at capture-all or time-out.
- After every code change: `refresh_unity(compile=request, mode=force, wait_for_ready=true)` → `read_console(types=["error"])` must be 0 errors BEFORE trusting `run_tests` (a compile failure makes `run_tests` silently re-run the last good assembly). Then `run_tests`(EditMode, assembly `ChaseGame.Tests.EditMode`) + `get_test_job`. Baseline EditMode = 77/77.
- Commit workflow: verify branch first (`git rev-parse --abbrev-ref HEAD` — checkout can silently jump to master); `git add` whole folders so `.meta` rides along; revert `ProjectSettings/EditorSettings.asset` EnterPlayMode toggle; NEVER commit `Assets/Screenshots`; end message with `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`; after commit `git branch -f feat/joystick-input master`.

## Review Focus

- **Capture-all at the exact same tick time expires** → must be `ChasersWin`, not `RunnersWin` (capture-all wins ties). Pinned in Task 1 tests.
- **Negative / zero remaining time in the clock** → HUD must show `0:00`, never `-0:01` or `0:-3`. Pinned in Task 2 tests.
- **`ForPlayer` for every team × winning-side combination** → a chaser player on `RunnersWin` is `Lose`; a runner player on `RunnersWin` is `Win`. Pinned in Task 1 tests.
- **Match end fires twice / countdown keeps running after end** → `MatchController.Update` must gate on `state == Ongoing` so `EndMatch` runs once and `Time.timeScale` is set once. Pinned as a code guard in Task 4 + playtest assertion in Task 5 (end screen appears once).
- **`Time.timeScale` still 0 after Chơi lại** → next match must run (not frozen). `timeScale` does not reset across scene loads; Replay resets it first. Pinned as playtest assertion in Task 5 (second match advances).

---

### Task 1: MatchEvaluator (pure logic + enums)

**Files:**
- Create: `Assets/Scripts/Match/MatchState.cs`
- Create: `Assets/Scripts/Match/MatchEvaluator.cs`
- Test: `Assets/Tests/EditMode/MatchEvaluatorTests.cs`

**Interfaces:**
- Consumes: `ChaseGame.Characters.Team` (existing enum: `Chaser`, `Runner`).
- Produces:
  - `enum ChaseGame.Match.MatchState { Ongoing, ChasersWin, RunnersWin }`
  - `enum ChaseGame.Match.PlayerOutcome { Win, Lose }`
  - `MatchState MatchEvaluator.Evaluate(int freeRunnerCount, float timeRemaining)`
  - `PlayerOutcome MatchEvaluator.ForPlayer(MatchState ended, Team playerTeam)`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/MatchEvaluatorTests.cs
using NUnit.Framework;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class MatchEvaluatorTests
    {
        private readonly MatchEvaluator eval = new MatchEvaluator();

        [Test]
        public void Evaluate_AllRunnersJailed_ChasersWin()
        {
            Assert.AreEqual(MatchState.ChasersWin, eval.Evaluate(0, 42f));
        }

        [Test]
        public void Evaluate_TimeUpWithFreeRunners_RunnersWin()
        {
            Assert.AreEqual(MatchState.RunnersWin, eval.Evaluate(2, 0f));
        }

        [Test]
        public void Evaluate_TimeLeftAndFreeRunners_Ongoing()
        {
            Assert.AreEqual(MatchState.Ongoing, eval.Evaluate(3, 15f));
        }

        [Test]
        public void Evaluate_NegativeTimeWithFreeRunners_RunnersWin()
        {
            Assert.AreEqual(MatchState.RunnersWin, eval.Evaluate(1, -0.5f));
        }

        [Test]
        public void Evaluate_CaptureAllAtTimeZero_ChasersWinTakesPriority()
        {
            Assert.AreEqual(MatchState.ChasersWin, eval.Evaluate(0, 0f));
        }

        [Test]
        public void ForPlayer_ChasersWin_ChaserPlayerWins()
        {
            Assert.AreEqual(PlayerOutcome.Win, eval.ForPlayer(MatchState.ChasersWin, Team.Chaser));
        }

        [Test]
        public void ForPlayer_ChasersWin_RunnerPlayerLoses()
        {
            Assert.AreEqual(PlayerOutcome.Lose, eval.ForPlayer(MatchState.ChasersWin, Team.Runner));
        }

        [Test]
        public void ForPlayer_RunnersWin_RunnerPlayerWins()
        {
            Assert.AreEqual(PlayerOutcome.Win, eval.ForPlayer(MatchState.RunnersWin, Team.Runner));
        }

        [Test]
        public void ForPlayer_RunnersWin_ChaserPlayerLoses()
        {
            Assert.AreEqual(PlayerOutcome.Lose, eval.ForPlayer(MatchState.RunnersWin, Team.Chaser));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (RED)**

`refresh_unity(compile=request, mode=force, wait_for_ready=true)` → `read_console(types=["error"])`.
Expected: compile error CS0246/CS0234 — `MatchState`/`MatchEvaluator` do not exist yet. (This is the true RED; do not trust a green `run_tests` here — it would be a stale-assembly fallback.)

- [ ] **Step 3: Write the enums**

```csharp
// Assets/Scripts/Match/MatchState.cs
namespace ChaseGame.Match
{
    public enum MatchState { Ongoing, ChasersWin, RunnersWin }
    public enum PlayerOutcome { Win, Lose }
}
```

- [ ] **Step 4: Write the evaluator**

```csharp
// Assets/Scripts/Match/MatchEvaluator.cs
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Pure match rules — no MonoBehaviour, no UnityEngine.Time. The only unit-tested piece.
    public sealed class MatchEvaluator
    {
        // Priority: capturing every runner wins for chasers even when time is also up.
        public MatchState Evaluate(int freeRunnerCount, float timeRemaining)
        {
            if (freeRunnerCount <= 0) return MatchState.ChasersWin;
            if (timeRemaining <= 0f)  return MatchState.RunnersWin;
            return MatchState.Ongoing;
        }

        // ended is ChasersWin or RunnersWin (never Ongoing).
        public PlayerOutcome ForPlayer(MatchState ended, Team playerTeam)
        {
            bool chasersWon = ended == MatchState.ChasersWin;
            bool playerIsChaser = playerTeam == Team.Chaser;
            return chasersWon == playerIsChaser ? PlayerOutcome.Win : PlayerOutcome.Lose;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass (GREEN)**

`refresh_unity(...)` → `read_console(types=["error"])` = 0 errors → `run_tests`(EditMode, `ChaseGame.Tests.EditMode`) + `get_test_job(wait_timeout)`.
Expected: MatchEvaluatorTests 9/9 pass; full suite 86/86 (77 baseline + 9).

- [ ] **Step 6: Commit**

```bash
git rev-parse --abbrev-ref HEAD   # confirm master
git add Assets/Scripts/Match Assets/Tests/EditMode
git commit -m "feat(match): pure MatchEvaluator + MatchState/PlayerOutcome (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git branch -f feat/joystick-input master
```

---

### Task 2: MatchClock (pure mm:ss formatter)

**Files:**
- Create: `Assets/Scripts/Match/MatchClock.cs`
- Test: `Assets/Tests/EditMode/MatchClockTests.cs`

**Interfaces:**
- Produces: `static string ChaseGame.Match.MatchClock.Format(float seconds)` → `"m:ss"`, clamps negatives to `"0:00"`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/MatchClockTests.cs
using NUnit.Framework;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class MatchClockTests
    {
        [Test] public void Format_NinetySeconds_OneThirty() => Assert.AreEqual("1:30", MatchClock.Format(90f));
        [Test] public void Format_FiveSeconds_PadsSeconds()  => Assert.AreEqual("0:05", MatchClock.Format(5f));
        [Test] public void Format_TwoMinutesFive()           => Assert.AreEqual("2:05", MatchClock.Format(125f));
        [Test] public void Format_Zero_IsZeroZeroZero()      => Assert.AreEqual("0:00", MatchClock.Format(0f));
        [Test] public void Format_Negative_ClampsToZero()    => Assert.AreEqual("0:00", MatchClock.Format(-3.2f));
        [Test] public void Format_Fractional_FloorsSeconds() => Assert.AreEqual("0:05", MatchClock.Format(5.9f));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (RED)**

`refresh_unity(...)` → `read_console(types=["error"])`.
Expected: CS0117/CS0246 — `MatchClock.Format` missing.

- [ ] **Step 3: Write the formatter**

```csharp
// Assets/Scripts/Match/MatchClock.cs
namespace ChaseGame.Match
{
    // Pure HUD clock rendering. Clamps negative time to 0 so the display never shows -0:01.
    public static class MatchClock
    {
        public static string Format(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;         // floor toward zero for non-negative input
            int minutes = total / 60;
            int secs = total % 60;
            return minutes + ":" + secs.ToString("00");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass (GREEN)**

`refresh_unity(...)` → `read_console` = 0 errors → `run_tests` + `get_test_job`.
Expected: MatchClockTests 6/6; full suite 92/92.

- [ ] **Step 5: Commit**

```bash
git rev-parse --abbrev-ref HEAD
git add Assets/Scripts/Match Assets/Tests/EditMode
git commit -m "feat(match): pure MatchClock mm:ss formatter (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git branch -f feat/joystick-input master
```

---

### Task 3: UI views (MatchHudView + MatchEndView)

**Files:**
- Create: `Assets/Scripts/Match/MatchHudView.cs`
- Create: `Assets/Scripts/Match/MatchEndView.cs`

**Interfaces:**
- Consumes: `MatchClock.Format`, `PlayerOutcome`.
- Produces:
  - `MatchHudView.Render(float timeRemaining, int freeRunners, int totalRunners)`; `MatchHudView.SetSpectating(bool)`.
  - `MatchEndView.Show(PlayerOutcome outcome)`; `MatchEndView.Hide()`; `event Action ReplayClicked`; `event Action QuitClicked`.

These are thin MonoBehaviours (no unit tests — pure UnityEngine UI). Verified by compile (Step 3) and by the Task 5 playtest.

- [ ] **Step 1: Write MatchHudView**

```csharp
// Assets/Scripts/Match/MatchHudView.cs
using UnityEngine;
using UnityEngine.UI;

namespace ChaseGame.Match
{
    // Always-visible HUD: countdown + free-runner tally. Driven each frame by MatchController.
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] private Text clockText;
        [SerializeField] private Text runnersText;
        [SerializeField] private GameObject spectatingBanner; // "Bạn đã bị bắt — đang xem đồng đội…"

        public void Render(float timeRemaining, int freeRunners, int totalRunners)
        {
            if (clockText != null) clockText.text = MatchClock.Format(timeRemaining);
            if (runnersText != null) runnersText.text = "Tự do: " + freeRunners + "/" + totalRunners;
        }

        public void SetSpectating(bool spectating)
        {
            if (spectatingBanner != null && spectatingBanner.activeSelf != spectating)
                spectatingBanner.SetActive(spectating);
        }
    }
}
```

- [ ] **Step 2: Write MatchEndView**

```csharp
// Assets/Scripts/Match/MatchEndView.cs
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChaseGame.Match
{
    // End screen: result text + Chơi lại / Thoát buttons. Hidden until the match ends.
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;      // the end overlay, inactive at start
        [SerializeField] private Text resultText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Color winColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color loseColor = new Color(0.85f, 0.25f, 0.25f);

        public event Action ReplayClicked;
        public event Action QuitClicked;

        private void Awake()
        {
            if (replayButton != null) replayButton.onClick.AddListener(() => ReplayClicked?.Invoke());
            if (quitButton != null) quitButton.onClick.AddListener(() => QuitClicked?.Invoke());
            Hide();
        }

        public void Show(PlayerOutcome outcome)
        {
            if (resultText != null)
            {
                resultText.text = outcome == PlayerOutcome.Win ? "BẠN THẮNG" : "BẠN THUA";
                resultText.color = outcome == PlayerOutcome.Win ? winColor : loseColor;
            }
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
```

- [ ] **Step 3: Compile-verify**

`refresh_unity(compile=request, mode=force, wait_for_ready=true)` → `read_console(types=["error"])`.
Expected: 0 errors. Then `run_tests` + `get_test_job` → full suite still 92/92 (no new tests, no regressions).

- [ ] **Step 4: Commit**

```bash
git rev-parse --abbrev-ref HEAD
git add Assets/Scripts/Match
git commit -m "feat(match): HUD + end-screen views (clock, tally, result, buttons)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git branch -f feat/joystick-input master
```

---

### Task 4: MatchController + DI wiring

**Files:**
- Create: `Assets/Scripts/Match/MatchController.cs`
- Modify: `Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `ITeamRoster` (existing singleton), `MatchEvaluator`, `MatchHudView`, `MatchEndView`, `Character.Team`, `Character.CaptureState`.
- Produces: `MatchController` scene component with `[SerializeField] float matchDurationSeconds`; public `Replay()` / `Quit()`.

`MatchController` is glue (MonoBehaviour + `Time` + `SceneManager`) → no EditMode unit test; verified by compile + the Task 5 playtest. All win-rule logic is delegated to the already-tested `MatchEvaluator`.

- [ ] **Step 1: Write MatchController**

```csharp
// Assets/Scripts/Match/MatchController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using ChaseGame.Characters;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChaseGame.Match
{
    // Match lifecycle glue: owns the countdown, asks MatchEvaluator for the verdict every
    // frame, freezes on end, and drives the HUD + end views. No win-rule branching here.
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private float matchDurationSeconds = 90f;
        [SerializeField] private MatchHudView hud;
        [SerializeField] private MatchEndView endView;

        private ITeamRoster roster;
        private MatchEvaluator evaluator;

        private float timeRemaining;
        private MatchState state = MatchState.Ongoing;

        [Inject]
        public void Construct(ITeamRoster roster, MatchEvaluator evaluator)
        {
            this.roster = roster;
            this.evaluator = evaluator;
        }

        private void Start()
        {
            timeRemaining = matchDurationSeconds;
            if (endView != null)
            {
                endView.Hide();
                endView.ReplayClicked += Replay;
                endView.QuitClicked += Quit;
            }
        }

        private void Update()
        {
            if (state != MatchState.Ongoing) return; // gate: end runs once, no double-freeze
            if (roster == null || evaluator == null) return;

            timeRemaining -= Time.deltaTime;

            var free = roster.GetFreeRunners();
            int freeCount = free.Count;
            int total = roster.Runners.Count;

            if (hud != null)
            {
                hud.Render(timeRemaining, freeCount, total);
                var player = roster.PlayerCharacter;
                hud.SetSpectating(player != null && player.CaptureState == CaptureState.Jailed);
            }

            var next = evaluator.Evaluate(freeCount, timeRemaining);
            if (next != MatchState.Ongoing) EndMatch(next);
        }

        private void EndMatch(MatchState result)
        {
            state = result;
            Time.timeScale = 0f;

            if (endView != null)
            {
                var playerTeam = roster.PlayerCharacter != null ? roster.PlayerCharacter.Team : Team.Runner;
                endView.Show(evaluator.ForPlayer(result, playerTeam));
            }
        }

        public void Replay()
        {
            Time.timeScale = 1f; // MUST reset before load — timeScale survives scene loads
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
```

- [ ] **Step 2: Wire DI in GameLifetimeScope**

Modify `Assets/Scripts/Infrastructure/GameLifetimeScope.cs` — add the `ChaseGame.Match` using if absent (already present) and register inside `Configure`, after the existing match-service registrations (`CharacterFactory` line):

```csharp
            builder.Register<CharacterFactory>(Lifetime.Singleton);

            // Match end-condition subsystem.
            builder.Register<MatchEvaluator>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<MatchController>();
```

- [ ] **Step 3: Compile-verify**

`refresh_unity(compile=request, mode=force, wait_for_ready=true)` → `read_console(types=["error"])`.
Expected: 0 errors. `run_tests` + `get_test_job` → full suite 92/92 (no regressions).

- [ ] **Step 4: Commit**

```bash
git rev-parse --abbrev-ref HEAD
git add Assets/Scripts/Match Assets/Scripts/Infrastructure
git commit -m "feat(match): MatchController lifecycle + freeze/replay/quit + DI wiring

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git branch -f feat/joystick-input master
```

---

### Task 5: Scene authoring (MCP) + full playtest

**Files:**
- Modify (via MCP, not text): `Assets/Scenes/Game.unity` (add MatchController GameObject + HUD/EndPanel under `UICanvas`, wire refs).

**Interfaces:**
- Consumes everything above. No new code — this task builds scene objects and proves the whole feature.

This task has no unit test; its deliverable is a green playtest with a screenshot. Set the active UnityMCP instance first (`set_active_instance` "Game@…") — required at the start of every turn.

- [ ] **Step 1: Build the MatchController GameObject**

Via `manage_gameobject`: create empty GO `MatchController` in `Game.unity`, add component `MatchController` (namespace `ChaseGame.Match`). Set `matchDurationSeconds = 90`.

- [ ] **Step 2: Build the HUD under UICanvas**

Via `manage_gameobject`/`manage_ui`: under `UICanvas`, create `HUD` panel containing:
- `ClockText` (UI `Text`, top-center, large).
- `RunnersText` (UI `Text`, top-left, "Tự do: 4/4").
- `SpectatingBanner` (GameObject with a `Text`, inactive at start).
Add `MatchHudView` component to `HUD`; assign `clockText`, `runnersText`, `spectatingBanner` via `SerializedObject`.

- [ ] **Step 3: Build the EndPanel under UICanvas**

Under `UICanvas`, create `EndPanel` (full-screen dimmed `Image`, **inactive at start**) containing:
- `ResultText` (UI `Text`, center, very large — "BẠN THẮNG").
- `ReplayButton` (UI `Button` + child `Text` "Chơi lại").
- `QuitButton` (UI `Button` + child `Text` "Thoát").
Add `MatchEndView` component to `EndPanel` (or a parent that stays active — put `MatchEndView` on an ALWAYS-ACTIVE object and point its `panel` field at the inactive `EndPanel`, so `Awake` runs). Assign `panel`, `resultText`, `replayButton`, `quitButton` via `SerializedObject`.

> Gotcha: a MonoBehaviour on an inactive GameObject never runs `Awake`, so button listeners wouldn't wire. Put `MatchEndView` on an active object and have its `panel` reference the hideable overlay.

- [ ] **Step 4: Wire MatchController refs**

Via `SerializedObject` on the `MatchController` component: assign `hud` → the `MatchHudView`, `endView` → the `MatchEndView`. Save the scene (`manage_scene` save).

- [ ] **Step 5: Compile + console check**

`refresh_unity(...)` → `read_console(types=["error"])` = 0 errors.

- [ ] **Step 6: Playtest — chasers win path (or whichever fires)**

`manage_editor(play)`. If the editor is unfocused, set `Application.runInBackground = true` at runtime via `execute_code`. Observe: HUD clock counts down from 1:30, "Tự do: n/4" decrements as runners are jailed. Let it run until capture-all OR time-out. Confirm:
- End screen shows once (Review Focus: single end), correct **BẠN THẮNG/THUA** for the player's team (check `roster.PlayerCharacter.Team` via `execute_code` and verify the text matches `ForPlayer`).
- Screenshot via `manage_camera`.

- [ ] **Step 7: Playtest — Chơi lại restores a clean match**

With the end screen up (`Time.timeScale == 0`), invoke `MatchController.Replay()` (click the button via UI, or call through `execute_code`). Confirm (Review Focus: timeScale reset): the scene reloads, a NEW match runs — clock counts down again, characters move (not frozen), `Time.timeScale == 1`. Screenshot.

- [ ] **Step 8: Stop + report + commit scene**

`manage_editor(stop)`. Revert any `ProjectSettings/EditorSettings.asset` EnterPlayMode toggle. Then:

```bash
git rev-parse --abbrev-ref HEAD
git checkout -- ProjectSettings/EditorSettings.asset   # if toggled by PlayMode
git add Assets/Scenes
git commit -m "feat(match): scene HUD + end panel wired to MatchController

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git branch -f feat/joystick-input master
```

Do NOT `git add Assets/Screenshots`. Report the two screenshots + the observed win/lose correctness + the clean-replay result. Update the ledger `.superpowers/sdd/2026-09-21-npc-ai-infrastructure/progress.md` with a "Match End + UI" section (tasks, test counts, playtest evidence).
