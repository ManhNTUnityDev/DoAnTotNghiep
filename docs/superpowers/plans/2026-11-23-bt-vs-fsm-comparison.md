# BT vs FSM Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give both teams a hand-written FSM brain that is behaviourally equivalent to the existing Behaviour Tree brain. Prove the equivalence with shared scenario tests. Measure both brains in automated all-AI match simulations, in a one-behaviour extension experiment, and in a synthetic N = 2…10 scaling series, so Chapter 4 of the thesis has real numbers.

**Architecture:**
- **Shared host.** `AIBrain` keeps the parts both brains share: Blackboard, Sensor, the phase-shifted tick and per-tick stats. It exposes one abstract `Decide(Blackboard)`. `BehaviourTreeBrain` and `FsmBrain` are its only two subclasses.
- **Shared action code.** All action code lives in static `ChaserActions` / `RunnerActions`, so the two brains differ only in how they choose an action.
- **Simulation runner.** A runtime `SimulationRunner` reloads the `Game` scene once per match and writes one CSV row per match. It is started from an Editor menu, which hands the plan over through a JSON file (domain reload clears statics on entering Play).

**Tech Stack:** Unity 6000.0.72f1, C#, VContainer, NavMesh (`UnityEngine.AI`), NUnit EditMode + PlayMode tests, Python 3 + matplotlib for charts.

**Spec:** `docs/superpowers/specs/2026-11-23-bt-vs-fsm-comparison-design.md`

## Global Constraints

- **Default play is unchanged.** With no simulation active, both teams use BT, there is one human player, and role reveal is 2.5s. A human playing Menu → Game sees no difference.
- **Equivalence rule.** Both brains of a team call the *same* shared action functions. Only the code that picks an action may differ.
- **FSM style.** Every state is an `IFsmState` class with `Enter/Tick/Exit`. Each FSM brain has exactly one transition table (`IReadOnlyList<FsmTransition>`). Game brains use **no any-state transitions**: this is the textbook pure FSM. Any-state transitions appear only as the second variant in the scaling series.
- **Decoy clones** use the same `BrainKind` as the character that spawned them. If the spawner has no `AIBrain` (the human player), the clone uses `BrainKind.BehaviourTree`.
- **Simulation match.**
  - 8 AI, no human player.
  - Role reveal 0s, head-start 10s, match 90s.
  - Default `Time.timeScale = 3`.
  - `Random.InitState(seed)` is called and the seed is logged for lookup only. Results are compared as distributions, never match by match.
- **Matchups.** Four of them, named `chaser_runner` with brain codes `bt` / `fsm`: `bt_bt`, `fsm_fsm`, `bt_fsm`, `fsm_bt`. Default 30 matches each.
- **Output location.** All benchmark output goes to `<project root>/Benchmark/results/` (outside `Assets/`).
  - `Benchmark/pending-plan.json` is gitignored.
  - Final result CSVs and charts are copied to `docs/benchmark/` and committed.
- **Extension commit order (spec §Mục tiêu 2).**
  1. One commit with the shared helpers, the `AIConfig` fields and the C5/R5 tests.
  2. Then one commit per brain, each containing only that brain's decision code plus any state storage that brain alone needs.
  - Order: BT Chaser, FSM Chaser, BT Runner, FSM Runner.
  - The new tests are red for the brains not yet extended. That is expected between these commits.
- **Verification.**
  - Compile C# with `dotnet build ChaseGame.csproj`, `dotnet build ChaseGame.Tests.EditMode.csproj`, `dotnet build ChaseGame.Tests.PlayMode.csproj` and `dotnet build Assembly-CSharp-Editor.csproj` from the project root. The Editor stays open and batch mode is blocked.
  - If a generated csproj misses a new file, temporarily add `<Compile Include=...>`, build, then restore the csproj.
  - Run tests through the open Editor (MCP `run_tests`, or ask the user to use the Test Runner).
- **Non-goals.** No third architecture, no in-game BT/FSM picker, no patrol zones, no items/audio/settings, and no writing of Chapter 4.

## Review Focus

1. **Skills on cooldown.** A brain whose abilities are on cooldown during flee/chase must still move. In the FSM, a state must never "stick" because an ability call failed. Test in Task 3: `Flee_WithSkillsOnCooldown_StillMovesAway`, run for both kinds.
2. **Character spawned with a `null` AIConfig.** Shared actions fall back to the current defaults: ShootRange 8, ThreatRadius 6, WanderRadius 6. Test in Task 1: `ChaserActions_CanShoot_NullConfig_UsesDefaultRange8`.
3. **Match ends while the runner is between scene loads.** Exactly one CSV row is written per match. No duplicate row if `MatchEnded` fires and the scene reloads in the same frame. Test in Task 7: `SimulationProgress_RecordTwiceForSameIndex_KeepsOneRow`.
4. **Simulation interrupted.** If Play is stopped mid-run, the CSV written so far stays valid: a header, then complete rows only. The runner appends and flushes each row. Test in Task 7: `MatchCsv_AppendRow_WritesHeaderOnlyOnce`.
5. **FSM entered with no target and no NavMesh.** `Wander` returns Failure and the FSM stays in Wander without throwing, matching the BT where the root Selector returns Failure. Test in Task 3: `NoNavMesh_WanderFails_NoException`, run for both kinds.

---

### Task 1: Shared AI host, shared actions, BT node introspection

A pure refactor: behaviour stays identical, and every existing test stays green unchanged.

**Files:**
- Modify: `Assets/Scripts/AI/AIBrain.cs`
  - Keep: Blackboard, Sensor, tick, `TickForTests`.
  - Replace `BuildTree`/`root` with `Decide`.
  - Move `Wander`, `TryRandomPoint` and `PlanarDistance` to `AIActions`.
- Create: `Assets/Scripts/AI/BehaviourTreeBrain.cs`
- Create: `Assets/Scripts/AI/Actions/AIActions.cs` (Wander, TryRandomPoint, PlanarDistance)
- Create: `Assets/Scripts/AI/Actions/ChaserActions.cs` (CanShoot, AimAndShoot, ChaseTarget, HasTarget)
- Create: `Assets/Scripts/AI/Actions/RunnerActions.cs` (ThreatNear, EscapeAndFlee, PickBestFleePoint, ConfigureAsClone, constants `FleeExitMultiplier = 1.35f`, `FleeCandidateAngles`)
- Create: `Assets/Scripts/AI/BehaviourId.cs`, `Assets/Scripts/AI/BrainKind.cs`
- Modify: `Assets/Scripts/AI/BehaviourTree/Blackboard.cs` (add `ActiveBehaviour`)
- Modify: `Assets/Scripts/AI/BehaviourTree/Node.cs`, `Selector.cs`, `Sequence.cs` (children introspection)
- Modify: `Assets/Scripts/AI/ChaserAIBrain.cs`, `Assets/Scripts/AI/RunnerAIBrain.cs` (derive from `BehaviourTreeBrain`, call shared actions)
- Test: `Assets/Tests/EditMode/SharedActionsTests.cs`, `Assets/Tests/EditMode/BehaviourTreeIntrospectionTests.cs`

**Interfaces:**
- Produces: `public enum BehaviourId { None, Wander, Chase, Shoot, Flee, Search, Hide }` in `ChaseGame.AI`.
- Produces: `Blackboard.ActiveBehaviour` (`BehaviourId`, default `None`). Each shared action sets it on entry:
  - `Wander` → `Wander`
  - `ChaseTarget` → `Chase`
  - `AimAndShoot` → `Shoot`
  - `EscapeAndFlee` → `Flee`
- Produces: `public abstract class AIBrain : CharacterBrain` with:
  - `public Blackboard Blackboard { get; }`
  - `public void Initialize(Character, ITeamRoster, AIConfig)`, which calls `protected abstract void OnBuild()` at the end
  - `protected abstract void Decide(Blackboard bb)`
  - `public abstract BrainKind Kind { get; }`
  - `public void TickForTests()`, which calls `Decide(Blackboard)`
  - `Update` keeps today's gating: skip when `!CanAct`, accumulate by `TickRateHz`, then `sensor.Sense` then `Decide`.
- Produces: `public enum BrainKind { BehaviourTree, Fsm }` (in `ChaseGame.AI`, file `BrainKind.cs`).
- Produces: `public abstract class BehaviourTreeBrain : AIBrain` with:
  - `protected abstract Node BuildTree()`
  - `public Node Root { get; }`
  - `OnBuild` sets `Root = BuildTree()`
  - `Decide` = `Root.Tick(bb)`
  - `Kind => BehaviourTree`
- Produces, static in `ChaseGame.AI.Actions`:
  - `AIActions.Wander(Blackboard) -> NodeStatus`
  - `AIActions.TryRandomPoint(Vector3 center, Vector3 forwardHint, float radius, out Vector3) -> bool`
  - `AIActions.PlanarDistance(Vector3, Vector3) -> float`
  - `ChaserActions.HasTarget(Blackboard) -> bool`, `CanShoot(Blackboard) -> bool`, `AimAndShoot(Blackboard) -> NodeStatus`, `ChaseTarget(Blackboard) -> NodeStatus`
  - `RunnerActions.ThreatNear(Blackboard) -> bool`, `EscapeAndFlee(Blackboard) -> NodeStatus`, `ConfigureAsClone(Blackboard, Vector3 initialMoveDirection, float fleeAngleBias)`
  - Bodies move verbatim from today's brains.
- Keep `AIBrain.PlanarDistance` as a `public static` forwarder to `AIActions.PlanarDistance`, because tests and `Decoy` reference it.
- Keep `RunnerAIBrain.ThreatNear` as a public static forwarder and `RunnerAIBrain.ConfigureAsClone(Vector3, float = 50f)` as an instance method.
- Produces: `public virtual IReadOnlyList<Node> Children => System.Array.Empty<Node>();` on `Node`, overridden by `Selector`/`Sequence`. Also `public static int BehaviourTreeStats.CountNodes(Node root)`, a recursive count that includes the root (file `Assets/Scripts/AI/BehaviourTree/BehaviourTreeStats.cs`).

- [ ] **Step 1: Write failing tests**

```csharp
// SharedActionsTests
[Test] public void ChaserActions_CanShoot_NullConfig_UsesDefaultRange8()
// Chaser at origin, abilities ready, bb.Config = null: target at (7.9,0,0) → true; target at (8.1,0,0) → false.
[Test] public void ChaserActions_ChaseTarget_SetsActiveBehaviourChase()
// → bb.ActiveBehaviour == BehaviourId.Chase, returns NodeStatus.Running
[Test] public void RunnerActions_EscapeAndFlee_SetsActiveBehaviourFlee()

// BehaviourTreeIntrospectionTests — brains created with AddComponent + Initialize, as in ChaserAIBrainTests
[Test] public void CountNodes_ChaserTree_Is8()  // Selector + 2 Sequences + 4 leaves + Wander
[Test] public void CountNodes_RunnerTree_Is5()  // Selector + Sequence + Cond + Act + Wander
```

- [ ] **Step 2: Build the test project and confirm it fails to compile** (`dotnet build ChaseGame.Tests.EditMode.csproj`): `ChaserActions`, `BehaviourId` and `Root` are not defined.
- [ ] **Step 3: Implement the refactor per Interfaces.** Existing `ChaserAIBrainTests` and `RunnerAIBrainTests` must not be edited.
- [ ] **Step 4: Build all four csproj; run the EditMode + PlayMode suites in the Editor.** Expected: all previous tests and the new ones pass.
- [ ] **Step 5: Commit** `refactor(ai): shared AI host and action library ahead of FSM brains`

---

### Task 2: FSM core

**Files:**
- Create: `Assets/Scripts/AI/Fsm/IFsmState.cs`, `FsmTransition.cs`, `StateMachine.cs`, `ActionState.cs`, `FsmBrain.cs`
- Test: `Assets/Tests/EditMode/StateMachineTests.cs`

**Interfaces:**
- Produces (namespace `ChaseGame.AI.Fsm`):
  - `public interface IFsmState { string Name { get; } void Enter(Blackboard bb); void Tick(Blackboard bb); void Exit(Blackboard bb); }`
  - `public sealed class FsmTransition { public IFsmState From; /* null = any state */ public Func<Blackboard,bool> Condition; public IFsmState To; public FsmTransition(IFsmState from, Func<Blackboard,bool> condition, IFsmState to) }`
  - `public sealed class ActionState : IFsmState`, built with `ActionState(string name, Func<Blackboard, NodeStatus> tick, Action<Blackboard> enter = null, Action<Blackboard> exit = null)`. It wraps a shared action, and `Tick` ignores the returned status.
  - `public sealed class StateMachine`:
    - Constructor `StateMachine(IFsmState initial, IReadOnlyList<FsmTransition> transitions)`.
    - Members `IFsmState Current`, `IReadOnlyList<FsmTransition> Transitions`, `IReadOnlyCollection<IFsmState> States` (distinct From/To plus initial), and `int TransitionCount => Transitions.Count`.
    - `void Tick(Blackboard bb)`:
      1. On the first tick, call `Current.Enter`.
      2. Scan `Transitions` in list order and take the **first** one where `(From == Current || From == null) && To != Current && Condition(bb)`.
      3. If one is found: `Current.Exit` → `Current = To` → `To.Enter`.
      4. Then always `Current.Tick(bb)`.
    - At most one transition per tick.
  - `public abstract class FsmBrain : AIBrain` with `protected abstract StateMachine BuildMachine()`, `public StateMachine Machine { get; }`. `OnBuild` sets `Machine`, `Decide` = `Machine.Tick(bb)`, `Kind => Fsm`.

- [ ] **Step 1: Write failing tests** (use recording fake states):
  - `FirstTick_EntersInitialThenTicksIt`: the log equals `["A.Enter","A.Tick"]`.
  - `MatchingTransition_ExitsEntersThenTicksNewState`: the log gains `["A.Exit","B.Enter","B.Tick"]`.
  - `FirstMatchingTransitionInListOrderWins`: two matching transitions from A → the one listed first is taken.
  - `AnyStateTransition_FiresFromEveryState_ButNotIntoItself`: with `From == null` and `To == B`, the machine is already in B → no Exit/Enter.
  - `OnlyOneTransitionPerTick`: A→B and B→C are both true → after one tick `Current == B`.
  - `TransitionCount_ReturnsTableSize`.
- [ ] **Step 2: Build; expect compile failure** (types missing).
- [ ] **Step 3: Implement per Interfaces.**
- [ ] **Step 4: Run `StateMachineTests`.** Expected: 6 pass.
- [ ] **Step 5: Commit** `feat(ai): finite state machine core`

---

### Task 3: FSM brains, brain factory, Decoy, both-kind fixtures

**Files:**
- Create: `Assets/Scripts/AI/Fsm/ChaserFsmBrain.cs`, `Assets/Scripts/AI/Fsm/RunnerFsmBrain.cs`, `Assets/Scripts/AI/BrainFactory.cs`, `Assets/Scripts/AI/IRunnerCloneBrain.cs`
- Modify: `Assets/Scripts/AI/RunnerAIBrain.cs` (implement `IRunnerCloneBrain`), `Assets/Scripts/Abilities/Decoy.cs:120-150` (brain of same kind; sanitize keeps any `IRunnerCloneBrain`)
- Modify: `Assets/Tests/EditMode/ChaserAIBrainTests.cs`, `Assets/Tests/EditMode/RunnerAIBrainTests.cs` (parameterize the fixture over `BrainKind`)

**Interfaces:**
- Consumes: Task 1 actions, Task 2 FSM types.
- Produces: `public interface IRunnerCloneBrain { bool IsClone { get; } void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f); }`.
- Produces: `public static class BrainFactory`:
  - `public static AIBrain Add(GameObject go, Team team, BrainKind kind)` adds the component and does **not** initialize it. The mapping:
    - Chaser + BT → `ChaserAIBrain`
    - Chaser + FSM → `ChaserFsmBrain`
    - Runner + BT → `RunnerAIBrain`
    - Runner + FSM → `RunnerFsmBrain`
  - `public static BrainKind KindOf(GameObject go)` returns the `AIBrain.Kind` if one is present, else `BehaviourTree`.
- `ChaserFsmBrain`:
  - States `Shoot` (`ChaserActions.AimAndShoot`), `Chase` (`ChaserActions.ChaseTarget`), `Wander` (`AIActions.Wander`); initial state `Wander`.
  - Transition table (exactly 6, in this order):
    1. Wander→Shoot `CanShoot`
    2. Wander→Chase `HasTarget && !CanShoot`
    3. Chase→Shoot `CanShoot`
    4. Chase→Wander `!HasTarget`
    5. Shoot→Chase `HasTarget && !CanShoot`
    6. Shoot→Wander `!HasTarget`
- `RunnerFsmBrain : FsmBrain, IRunnerCloneBrain`:
  - States `Flee` (`RunnerActions.EscapeAndFlee`), `Wander`; initial state `Wander`.
  - Transitions (exactly 2): Wander→Flee `ThreatNear`, Flee→Wander `!ThreatNear`.
  - `ConfigureAsClone` forwards to `RunnerActions.ConfigureAsClone`.
- Equivalence note: BT re-picks from the root every tick, and both tables above reproduce that priority order from every state. The parameterized tests are the proof.

- [ ] **Step 1: Parameterize the existing fixtures.**
  - Add `[TestFixture(BrainKind.BehaviourTree)] [TestFixture(BrainKind.Fsm)]` and a constructor `(BrainKind kind)`.
  - Replace `go.AddComponent<ChaserAIBrain>()` with `BrainFactory.Add(go, Team.Chaser, kind)`, then `Initialize`. The local type becomes `AIBrain`.
  - Runner clone tests cast to `IRunnerCloneBrain`.
  - Add to both fixtures:
    - `Flee_WithSkillsOnCooldown_StillMovesAway` (runner; abilities not ready → `MoveTo` called and destination farther from the threat)
    - `Chase_WithBoostOnCooldown_StillMovesToTarget` (chaser)
    - `NoNavMesh_WanderFails_NoException` (no target and no NavMesh in EditMode → `Assert.DoesNotThrow(brain.TickForTests)` and `Blackboard.ActiveBehaviour == Wander`)
  - In `DecoyUse_SpawnsClone…` add an assertion: the clone's `BrainFactory.KindOf(clone)` equals the spawner's kind.
- [ ] **Step 2: Build; expect compile failure** (`BrainFactory` missing).
- [ ] **Step 3: Implement the brains, factory and `Decoy` changes.**
  - `Decoy` gets `kind = BrainFactory.KindOf(self.gameObject)`.
  - It adds the clone brain via `BrainFactory.Add(cloneGo, Team.Runner, kind)` unless the clone already has an `IRunnerCloneBrain` of that kind.
  - `SanitizeCloneComponents` destroys every `AIBrain` that is not an `IRunnerCloneBrain`.
- [ ] **Step 4: Run the EditMode suite.** Expected: every Chaser/Runner brain test passes twice (BT and FSM).
- [ ] **Step 5: Commit** `feat(ai): FSM brains for both teams with shared fixtures`

---

### Task 4: Brain selection at spawn and the no-player mode

**Files:**
- Create: `Assets/Scripts/Infrastructure/MatchSetup.cs`
- Modify: `Assets/Scripts/Infrastructure/SpawnManager.cs` (`Start`, `SpawnTeam`, `AttachAIBrain`)
- Test: `Assets/Tests/EditMode/SpawnManagerTests.cs`

**Interfaces:**
- Produces: `public sealed class MatchSetupOptions { public bool HasHumanPlayer = true; public BrainKind ChaserBrain = BrainKind.BehaviourTree; public BrainKind RunnerBrain = BrainKind.BehaviourTree; }`
- Produces: `public static class MatchSetup { public static MatchSetupOptions Current; /* null = normal play */ public static MatchSetupOptions Effective => Current ?? new MatchSetupOptions(); }`
- Produces: `public static BrainKind SpawnManager.ResolveBrainKind(Team team, MatchSetupOptions options)` and `public static bool SpawnManager.IsPlayerSlot(Team team, int index, Team playerTeam, MatchSetupOptions options)`. The second returns false whenever `!options.HasHumanPlayer`.
- `AttachAIBrain` uses `BrainFactory.Add(..., ResolveBrainKind(team, MatchSetup.Effective))`, then `Initialize`.
- With no human, the camera calls are skipped (`roster.PlayerCharacter` stays null) and the camera keeps its scene pose.

- [ ] **Step 1: Write failing tests:**
  - `ResolveBrainKind_DefaultOptions_IsBehaviourTreeForBoth`
  - `ResolveBrainKind_UsesPerTeamOption` (Chaser FSM / Runner BT)
  - `IsPlayerSlot_NoHumanPlayer_AlwaysFalse`
  - `IsPlayerSlot_Default_OnlySlot0OfPlayerTeam`
  - Add `[TearDown] MatchSetup.Current = null;`.
- [ ] **Step 2: Build; expect compile failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `SpawnManagerTests` + the full EditMode suite.** Expected: pass.
- [ ] **Step 5: Manual check:** play Menu → Game once in the Editor. One player, BT NPCs, role reveal shown.
- [ ] **Step 6: Commit** `feat(match): choose AI brain kind per team at spawn`

---

### Task 5: Scenario tests C1–C4, R1–R4 and the baseline tag

**Files:**
- Create: `Assets/Tests/PlayMode/ScenarioArena.cs` (helper), `Assets/Tests/PlayMode/ChaserScenarioTests.cs`, `Assets/Tests/PlayMode/RunnerScenarioTests.cs`

**Interfaces:**
- Consumes: `BrainFactory.Add`, `AIBrain.Blackboard.ActiveBehaviour`.
- Produces: `ScenarioArena`, built the same way as `ChaserAIPlayTests.SetUp`:
  - `static IEnumerator Build(ScenarioArena arena, float size = 40f)` bakes a runtime NavMesh box.
  - `GameObject AddWall(Vector3 center, Vector3 size)` adds a BoxCollider on the default layer and is included in the NavMesh build sources (carved as non-walkable). Walls must be added **before** `Bake()`.
  - `IEnumerator Bake()`
  - `Character MakeBody(string name, Team team, Vector3 pos)` creates a CharacterController + NavMeshAgent + CharacterMovement + Character.
  - `AIBrain AttachBrain(Character c, Team team, BrainKind kind, ITeamRoster roster)`
  - `void Dispose()`
- Both fixtures carry `[TestFixture(BrainKind.BehaviourTree)] [TestFixture(BrainKind.Fsm)]`. The opponent is a body with no brain, moved by the test, except in C2 where the runner stands still.
- Use the default `AIConfig` (vision 12, shoot 8, threat 6, wander 6, tick 8 Hz).

Scenario assertions (each is one `[UnityTest]`):
- `C1_RunnerVisibleOutOfRange_ChaserCloses`: runner at 11 m. After 1.5s the planar distance is < 9 m and `ActiveBehaviour == Chase`.
- `C2_RunnerInRangeGunReady_IsCaptured`: runner at 6 m, chaser with the real `Gun` from the ChaserCharacter abilities setup used in `ChaserCapturePlayTests`. Within 3s the runner's `CaptureState == Jailed`.
- `C3_NoOneVisible_Wanders`: no runner. Over 3s the chaser moves > 2 m total and `ActiveBehaviour == Wander`.
- `C4_TargetBehindWall_GoesToLastSeen`:
  - Wall 0.5×3×10 at x = 0. The chaser starts at (−6, 0, 0) with the runner visible at (−2, 0, 6).
  - After one tick, teleport the runner behind the wall to (6, 0, 6).
  - Within 3s the chaser comes within 1.5 m of (−2, 0, 6) and never moves farther than 1 m from it toward its pre-chase wander point. Simplest form of the check: record the planar distance to (−2, 0, 6) each frame and assert it is non-increasing until < 1.5 m, within a 0.3 m tolerance.
- `R1_ChaserEntersThreatRadius_DistanceGrows`: chaser placed at 4 m. After 2s the distance is > 6 m and `ActiveBehaviour` was `Flee` on the first decision tick.
- `R2_ThreatenedWithSkillsReady_UsesSkills`: a spy `ICharacterAbilities` with both abilities ready, as in `RunnerAIBrainTests`. Within 0.5s, `UseAbility(0)` and `UseAbility(1)` have each been called once.
- `R3_CorneredAgainstWalls_StillFindsFleePoint`:
  - Walls along x = 10 and z = 10 form a corner. The runner is at (9, 0, 9) and the chaser at (6, 0, 6).
  - Within 2s the runner moves > 2 m and is never stationary (< 0.05 m moved) for 0.5s straight while threatened.
- `R4_AtThreatBoundary_NoFleeWanderFlicker`:
  - The chaser starts at 4 m. After the first flee tick, every frame for 2s, re-place the chaser at exactly 1.2 × 6 = 7.2 m from the runner along the chaser→runner line.
  - `ActiveBehaviour` stays `Flee` throughout (exit threshold 1.35 × 6 = 8.1 m).

- [ ] **Step 1: Write `ScenarioArena` and the eight scenarios.**
- [ ] **Step 2: Build `ChaseGame.Tests.PlayMode.csproj`.** Expected: 0 errors.
- [ ] **Step 3: Run the PlayMode suite in the Editor.** Expected: 16 new tests pass (8 × 2 kinds) and the old PlayMode tests still pass.
  - If a scenario fails for exactly one kind, fix the FSM brain. That is a real equivalence bug.
  - If it fails for both, fix the scenario setup.
- [ ] **Step 4: Commit and tag.**

```bash
git commit -m "test(ai): C1-C4 / R1-R4 scenarios run against BT and FSM brains"
git tag bt-fsm-baseline
```

---

### Task 6: Per-brain decision stats

**Files:**
- Create: `Assets/Scripts/AI/DecisionStats.cs`
- Modify: `Assets/Scripts/AI/AIBrain.cs` (`Update` times Sense and Decide with `System.Diagnostics.Stopwatch` and records them)
- Test: `Assets/Tests/EditMode/DecisionStatsTests.cs`

**Interfaces:**
- Produces: `public sealed class DecisionStats`:
  - Fields: `int Ticks`, `double TotalSenseMs`, `double TotalDecideMs`, `double MaxDecideMs`, `int BehaviourChanges`.
  - `void Record(double senseMs, double decideMs, BehaviourId before, BehaviourId after)` increments `BehaviourChanges` only when `before != None && before != after`.
  - `double MeanDecideMs` returns 0 when `Ticks == 0`; `MeanSenseMs` likewise.
  - `static DecisionStats Merge(IEnumerable<DecisionStats>)`.
- Produces: `AIBrain.Stats` (`DecisionStats`, never null). `TickForTests` does **not** record.

- [ ] **Step 1: Write failing tests:**
  - `Record_AccumulatesTotalsAndMax`
  - `Record_FirstBehaviourFromNone_IsNotAChange`
  - `Record_SameBehaviour_IsNotAChange`
  - `Mean_WithNoTicks_IsZero`
  - `Merge_SumsTicksAndTakesMaxOfMax`
- [ ] **Step 2: Build; expect compile failure.**
- [ ] **Step 3: Implement**, including the Stopwatch timing in `AIBrain.Update`.
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit** `feat(ai): per-brain decision timing and behaviour-change stats`

---

### Task 7: Match simulation runner and Editor menu

**Files:**
- Create: `Assets/Scripts/Simulation/SimulationPlan.cs`, `SimulationProgress.cs`, `MatchCsv.cs`, `SimulationRunner.cs`
- Create: `Assets/Editor/BenchmarkMenu.cs`
- Modify: `Assets/Scripts/Match/MatchController.cs` (add `public event Action<MatchState> MatchEnded;`, raised at the end of `EndMatch`)
- Modify: `.gitignore` (add `Benchmark/pending-plan.json`)
- Test: `Assets/Tests/EditMode/SimulationPlanTests.cs`, `Assets/Tests/EditMode/MatchCsvTests.cs`

**Interfaces:**
- Produces: `[Serializable] public sealed class SimulationPlan` (namespace `ChaseGame.Simulation`):
  - Fields: `string label`, `string[] matchups`, `int matchesPerMatchup = 30`, `float timeScale = 3f`, `int baseSeed = 1000`.
  - `List<SimulationMatch> Expand()` returns, for each matchup in order, `matchesPerMatchup` entries with `seed = baseSeed + i`. The **same seed list is reused for every matchup**.
  - `static SimulationPlan Full()`: label `full`, all 4 matchups, 30 matches, 3×.
  - `static SimulationPlan Quick()`: label `quick`, 4 matchups, 3 matches, 3×.
  - `static SimulationPlan SpeedCheck(float timeScale)`: label `speed-1x` or `speed-3x`, `["bt_bt"]`, 10 matches.
- Produces: `public readonly struct SimulationMatch { public readonly int Index; public readonly string Matchup; public readonly int Seed; public BrainKind ChaserBrain; public BrainKind RunnerBrain; }`. The brains are parsed from `chaser_runner`; an unknown code throws `ArgumentException`.
- Produces: `public sealed class MatchResultRow`, holding the CSV columns in this order:
  - Columns: `index, matchup, seed, chaser_brain, runner_brain, winner, runners_caught, runners_total, first_capture_s, last_capture_s, match_duration_s, chaser_mean_decide_ms, chaser_max_decide_ms, chaser_mean_sense_ms, runner_mean_decide_ms, runner_max_decide_ms, runner_mean_sense_ms, chaser_changes_per_min, runner_changes_per_min, time_scale`.
  - Capture times are match-clock seconds measured from the start of `Playing`; they are empty when there was no capture.
  - Floats use `CultureInfo.InvariantCulture` with 3 decimals.
- Produces: `public static class MatchCsv`:
  - `static string Header`
  - `static string Format(MatchResultRow row)`
  - `static void AppendRow(string path, MatchResultRow row)` writes the header if the file is missing or empty, appends one line and flushes.
- Produces: `public sealed class SimulationProgress`:
  - Constructor `SimulationProgress(IReadOnlyList<SimulationMatch> matches)`.
  - Members: `SimulationMatch? Current`, `bool TryRecord(int index)` (returns false for an index already recorded), `bool Advance()` (moves to the next match and returns false when done), `int Completed`.
- `SimulationRunner : MonoBehaviour` (`DontDestroyOnLoad`):
  - `static void Begin(SimulationPlan plan, string outputCsvPath)`.
  - On `sceneLoaded` of `Game`:
    1. Set `MatchSetup.Current = { HasHumanPlayer = false, ChaserBrain, RunnerBrain }`, call `Random.InitState(seed)`, set `Time.timeScale = plan.timeScale`, and set the `MatchController.RoleRevealDurationSeconds = 0`. `sceneLoaded` runs after `Awake` and before `Start`, and `SpawnManager.Start` runs after scope build.
    2. Subscribe to `MatchEnded`.
    3. Resolve `ICaptureService` from `LifetimeScope.Find<GameLifetimeScope>().Container` for capture times, and `ITeamRoster` for brain stats.
  - On `MatchEnded`:
    1. If `TryRecord`, build the row (stats merged per team from `roster.Chasers` / `Runners` `AIBrain.Stats`) and append it.
    2. Wait 0.5s of real time (`WaitForSecondsRealtime`), then call `MatchController.PrepareSceneExit()`.
    3. If `Advance()`, load `Game`. Otherwise set `MatchSetup.Current = null` and `Time.timeScale = 1`, log `"[Benchmark] done: N matches → path"`, and under `#if UNITY_EDITOR` call `UnityEditor.EditorApplication.ExitPlaymode()`.
  - The runner is inert unless `Begin` is called.
  - `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` under `#if UNITY_EDITOR` reads `<project>/Benchmark/pending-plan.json`. If it exists, the method deletes it and calls `Begin`.
- `BenchmarkMenu` (`Assets/Editor`). Each item writes `pending-plan.json` (output `Benchmark/results/<yyyyMMdd-HHmm>-<label>.csv`), opens `Assets/Scenes/Game.unity`, and enters Play. Menu items:
  - `ChaseGame/Benchmark/Run Full (4×30 @3x)`
  - `ChaseGame/Benchmark/Run Quick (4×3 @3x)`
  - `ChaseGame/Benchmark/Speed Check 1x (bt_bt ×10)`
  - `ChaseGame/Benchmark/Speed Check 3x (bt_bt ×10)`

- [ ] **Step 1: Write failing tests:**
  - `Expand_FullPlan_Has120MatchesInMatchupOrder`
  - `Expand_SameSeedListForEveryMatchup`
  - `ParseMatchup_FsmBt_GivesFsmChaserBtRunner`
  - `ParseMatchup_Unknown_Throws`
  - `MatchCsv_Format_UsesInvariantCultureAndColumnOrder`: run it under the `vi-VN` culture and expect `"6.500"`, not `"6,500"`.
  - `MatchCsv_Format_NoCapture_LeavesCaptureColumnsEmpty`
  - `MatchCsv_AppendRow_WritesHeaderOnlyOnce`: write into a temp file twice → 3 lines.
  - `SimulationProgress_RecordTwiceForSameIndex_KeepsOneRow`
  - `SimulationProgress_AdvancePastLast_ReturnsFalse`
- [ ] **Step 2: Build; expect compile failure.**
- [ ] **Step 3: Implement the pure classes; run the tests; expect pass.**
- [ ] **Step 4: Implement `SimulationRunner`, `MatchController.MatchEnded`, and `BenchmarkMenu`. Build all four csproj.**
- [ ] **Step 5: Smoke run:** use the menu `Run Quick`, or `execute_menu_item` via MCP. Expected:
  - Play mode exits on its own after 12 matches.
  - The CSV has 13 lines, and every `matchup` value appears 3 times.
  - `Editor.log` has the `[Benchmark] done` line and no exceptions.
  - Then play Menu → Game manually once: normal play, with a human.
- [ ] **Step 6: Commit** `feat(sim): automated all-AI match simulation with CSV output`

---

### Task 8: Summary and chart script

**Files:**
- Create: `tools/benchmark/summarize.py`, `tools/benchmark/README.md` (how to run, 5 lines)

**Interfaces:**
- CLI: `python tools/benchmark/summarize.py <results.csv> [--scaling <scaling.csv>] [--static <static-metrics.csv>] --out <dir>`
- Writes to `<dir>`:
  - `summary.md`: a table per matchup with n, chaser win %, mean runners caught, median first/last capture, mean/max decide ms per team, and changes/min per team.
  - `win_rate.png`
  - `capture_time_boxplot.png`
  - `decide_ms.png`
  - `behaviour_changes.png`
  - With `--scaling`: `scaling.png` (lines: BT nodes, FSM pure transitions, FSM any-state transitions vs N) and `selection_cost.png`.
  - With `--static`, the table goes into `summary.md`.
- Standard library `csv` + `statistics` + `matplotlib` only.
- Before the first run, check `python -c "import matplotlib"`. matplotlib is not installed today, so **ask the user** before running `pip install matplotlib`.

- [ ] **Step 1: Write the script.**
- [ ] **Step 2: Run it on the Task 7 quick CSV.** Expected: `summary.md` shows 4 matchup rows with n = 3 each, and 4 PNGs open without error.
- [ ] **Step 3: Commit** `feat(sim): benchmark summary and chart script`

---

### Task 9: Baseline simulation run

- [ ] **Step 1: Run `Speed Check 1x` and `Speed Check 3x`.** Compare the chaser win % and median last-capture time with `summarize.py`.
  - The distributions are acceptable if chaser win % differs by ≤ 20 points and the median capture time by ≤ 15%.
  - Otherwise, drop `SimulationPlan.Full().timeScale` to 2 and repeat at 2×. Record the decision in `docs/benchmark/README.md`.
- [ ] **Step 2: Run `Run Full`** (about 1 hour at 3×; the user may need to leave the Editor focused).
- [ ] **Step 3: Copy the CSVs to `docs/benchmark/baseline/` and run `summarize.py` with `--out docs/benchmark/baseline/`.**
- [ ] **Step 4: Commit** `docs(benchmark): baseline BT vs FSM simulation results`

---

### Task 10: Extension — shared helpers, config and tests (commit 1 of 5)

**Files:**
- Modify: `Assets/Scripts/AI/AIConfig.cs`. New fields:
  - `searchDurationSeconds = 6f`
  - `searchPointCount = 4`
  - `searchRadius = 4f`
  - `coverSearchRadius = 8f`
  - `coverSampleCount = 12`
  - Each has a matching read-only property.
- Modify: `Assets/Scripts/AI/Sensor.cs`. Extract the body of `HasLineOfSight` into `public static bool IsSightClear(Vector3 fromEye, Vector3 toEye, int mask)`. The instance method calls it, so behaviour is unchanged.
- Modify: `Assets/Scripts/AI/BehaviourTree/Blackboard.cs` (add `public float Now;`) and `Assets/Scripts/AI/AIBrain.cs` (set `bb.Now = Time.time` before `Decide`; `TickForTests` leaves `Now` as the test set it). Both brains need the clock, so it counts as shared.
- Modify: `Assets/Scripts/AI/Actions/ChaserActions.cs`
- Modify: `Assets/Scripts/AI/Actions/RunnerActions.cs`
- Test: add the C5 tests to `ChaserAIBrainTests`, the R5 tests to `RunnerAIBrainTests`, and `C5_…`/`R5_…` scenarios to the Task 5 PlayMode fixtures.

**Interfaces:**
- Produces, all stateless:
  - `ChaserActions.GenerateSearchPoints(Vector3 center, int count, float radius) -> List<Vector3>`: evenly spaced angles, each sampled to NavMesh (`SamplePosition`, 2.5 m). Points that fail sampling are skipped.
  - `ChaserActions.SearchStep(Blackboard bb, IReadOnlyList<Vector3> points, ref int index) -> NodeStatus`:
    - Sets `ActiveBehaviour = Search` and moves to `points[index]`.
    - Advances `index` when within 1.5 m.
    - Returns `Running`, or `Success` once all points are visited.
  - `RunnerActions.TryFindCoverPoint(Vector3 self, Vector3 threat, float radius, int samples, int mask, out Vector3 point) -> bool`:
    - Candidates are spread on a ring at 0.5 × radius..radius around `self`, NavMesh-sampled.
    - A candidate is valid if `!Sensor.IsSightClear(threat + up×eye, point + up×eye, mask)` and its planar distance from the threat is ≥ the runner's current distance.
    - Pick the valid candidate nearest to `self`.
  - `RunnerActions.TakeCover(Blackboard bb, Vector3 point) -> NodeStatus`: sets `ActiveBehaviour = Hide`, uses skills exactly like `EscapeAndFlee`, calls `MoveTo(point)`, returns `Running`.
- Behaviour each brain must implement in Tasks 11–12:
  - **Search (Chaser):** when the chaser had a target last decision tick and has none now, search starts.
    1. Walk to the last-seen point (already `bb.Destination`).
    2. Then run `SearchStep` over `GenerateSearchPoints(lastSeen, searchPointCount, searchRadius)`.
    3. Stop when all points are visited or `Now ≥ searchStart + searchDurationSeconds` → Wander.
    - A visible target at any time → Shoot/Chase as usual.
  - **Hide (Runner):** when `ThreatNear` and `TryFindCoverPoint` succeeds, `TakeCover`; otherwise `EscapeAndFlee`. Clones never hide. Re-evaluate the cover point each decision tick.
- New tests, both kinds:
  - `TargetLost_WalksToLastSeenThenSearchPoints`: spy movement. Tick with target at (6,0,0), then null. `LastDestination == (6,0,0)` and `ActiveBehaviour == Search`.
  - `SearchTimesOut_ReturnsToWander`: set `Now` past 6s → `ActiveBehaviour == Wander`.
  - `TargetReappearsDuringSearch_Chases`
  - These Runner tests go in the Task 5 PlayMode `RunnerScenarioTests`, because cover needs NavMesh and colliders:
    - `ThreatNear_CoverAvailable_Hides`: a wall between the threat and a candidate.
    - `ThreatNear_NoCover_Flees`
    - `Clone_NeverHides`
  - PlayMode scenarios:
    - `C5_LostBehindWall_SearchesThenWanders`: `ActiveBehaviour` sequence contains `Search` before `Wander`, and the chaser comes within 2 m of a search point.
    - `R5_WallNearby_EndsOutOfChaserSight`: after 3s, `Sensor.IsSightClear(chaserEye, runnerEye)` is false.
  - EditMode note: `GenerateSearchPoints` has no NavMesh in EditMode. Test the BT/FSM decision via a Blackboard-injected point list where needed, or keep the search assertions to the PlayMode scenarios.

- [ ] **Step 1: Add the config fields, `Sensor.IsSightClear`, `Blackboard.Now`, the shared helpers and all new tests.**
- [ ] **Step 2: Build all csproj; run the old tests.** Expected: every old test passes. New tests fail for all 4 brains (behaviour not wired yet).
- [ ] **Step 3: Commit** `feat(ai): shared search and cover helpers for the extension experiment`

### Task 11: Extension — Chaser search (commits 2 and 3 of 5)

**Files:** `Assets/Scripts/AI/ChaserAIBrain.cs` (+ `Blackboard.cs` only if the BT needs fields), then `Assets/Scripts/AI/Fsm/ChaserFsmBrain.cs` (+ a new state class file if used)

- [ ] **Step 1: BT.** Add a Search branch between Chase and Wander. Any search memory the BT needs (search start time, points, index, had-target flag) goes on `Blackboard` in this commit and is counted as BT cost. Run the Chaser tests. Expected: BT C5 tests pass, FSM C5 tests still fail.
- [ ] **Step 2: Commit** `feat(ai): BT chaser searches where it lost the target`
- [ ] **Step 3: FSM.** Add a `Search` state class holding its own memory, plus its transitions:
  - Chase→Search
  - Shoot→Search
  - Search→Shoot
  - Search→Chase
  - Search→Wander
  - Chase→Wander must now fire only when no search applies.
  - Run the Chaser tests. Expected: all pass for both kinds.
- [ ] **Step 4: Commit** `feat(ai): FSM chaser searches where it lost the target`

### Task 12: Extension — Runner hide (commits 4 and 5 of 5)

**Files:** `Assets/Scripts/AI/RunnerAIBrain.cs`, then `Assets/Scripts/AI/Fsm/RunnerFsmBrain.cs`

- [ ] **Step 1: BT.** Change the flee branch to `Sequence(ThreatNear, Selector(Sequence(CanHide, TakeCover), EscapeAndFlee))`. Run the Runner tests. Expected: BT R5 tests pass.
- [ ] **Step 2: Commit** `feat(ai): BT runner hides behind obstacles`
- [ ] **Step 3: FSM.** Add a `Hide` state and its transitions: Wander→Hide, Flee→Hide, Hide→Flee (cover lost), Hide→Wander. Wander→Flee becomes "threat and no cover". Run the full EditMode + PlayMode suites. Expected: all pass.
- [ ] **Step 4: Commit** `feat(ai): FSM runner hides behind obstacles`

---

### Task 13: Scaling series, selection-cost benchmark, static metrics

**Files:**
- Create: `Assets/Scripts/AI/Analysis/PriorityModel.cs` (runtime assembly, so EditMode tests can reach it)
- Create: `Assets/Editor/BenchmarkAnalysisMenu.cs`
- Test: `Assets/Tests/EditMode/PriorityModelTests.cs`

**Interfaces:**
- Produces: `public sealed class SyntheticBlackboard : Blackboard { public bool[] Flags; public int LastAction = -1; }`
- Produces: `public static class PriorityModel` (namespace `ChaseGame.AI.Analysis`). Behaviour `i` (0-based, i < N−1) fires when `Flags[i]` is true and every `Flags[j<i]` is false. Behaviour N−1 is the default. Actions set `LastAction = i` and return `Running`.
  - `static Node BuildTree(int n)`: `Selector(Sequence(Cond i, Act i)… , Act n−1)`. `CountNodes == 3n − 1`.
  - `static StateMachine BuildPureFsm(int n, int initial)`: one `ActionState` per behaviour. From every state s, a transition to every t ≠ s with the condition "t is the highest-priority active behaviour", giving `n(n−1)` transitions.
  - `static StateMachine BuildAnyStateFsm(int n, int initial)`: `n` transitions with `From == null`, one per target with the same predicate.
  - `static int ExpectedAction(bool[] flags)`
- Equivalence check: for every n, every one of the `2^(n−1)` flag combinations and every `initial` in 0…n−1:
  1. Build fresh machines with that initial state.
  2. Tick once.
  3. Assert `LastAction == ExpectedAction(flags)` for the BT, the pure FSM and the any-state FSM.
- `BenchmarkAnalysisMenu`:
  - `ChaseGame/Benchmark/Scaling Series` writes `Benchmark/results/scaling.csv` with columns:
    - `n, bt_nodes, fsm_pure_transitions, fsm_anystate_transitions, bt_ns_per_tick, fsm_pure_ns_per_tick, fsm_any_ns_per_tick`
    - n = 2…10.
    - ns/tick comes from 200 000 ticks over a fixed pseudo-random flag sequence (`System.Random(42)`), after 10 000 warm-up ticks.
  - `ChaseGame/Benchmark/Static Metrics` writes `Benchmark/results/static-metrics.csv` with columns:
    - `brain, team, kind, bt_nodes, fsm_states, fsm_transitions, loc`
    - One row per game brain class. It instantiates each brain on a temp GameObject with a default `AIConfig` and reads `Root` / `Machine`.
    - `loc` = non-blank lines that are not `//` comment lines in the brain's own `.cs` file plus any state-class files it owns, found via `MonoScript.FromMonoBehaviour` and the asset path. The shared action files are listed in their own rows with team `shared`.
    - Counts are whatever the code has. Before the extension, the expected values are Chaser FSM 3 states / 6 transitions and Runner FSM 2 / 2.

- [ ] **Step 1: Write failing tests:**
  - `Tree_NodeCount_Is3nMinus1` (n = 2…10)
  - `PureFsm_TransitionCount_IsNTimesNMinus1`
  - `AnyStateFsm_TransitionCount_IsN`
  - `AllThreeModels_AgreeOnEveryFlagCombination_FromEveryStartState` (n = 2…8; n = 9–10 too if it takes under 5s)
- [ ] **Step 2: Build; expect compile failure.**
- [ ] **Step 3: Implement `PriorityModel`; run the tests; expect pass.**
- [ ] **Step 4: Implement `BenchmarkAnalysisMenu`; run both menu items.** Expected:
  - `scaling.csv` has 9 data rows, with `fsm_pure_transitions` = 2, 6, 12, …, 90.
  - `static-metrics.csv` lists 4 brains plus shared files, and the FSM state counts are 4 (Chaser) and 3 (Runner).
- [ ] **Step 5: Commit** `feat(analysis): BT vs FSM scaling series, selection cost and static metrics`

---

### Task 14: Post-extension run and final results

- [ ] **Step 1: Run `Run Full` again** (now with Search/Hide).
- [ ] **Step 2: Record the extension cost.** Run `git diff --stat` for each of the five extension commits (Tasks 10–12) and save the output to `docs/benchmark/extension-diffs.txt`.
- [ ] **Step 3: Copy the results.** Put the CSVs, `scaling.csv` and `static-metrics.csv` in `docs/benchmark/extended/`, then run `summarize.py --scaling … --static … --out docs/benchmark/extended/`.
- [ ] **Step 4: Write `docs/benchmark/README.md`.** List each folder's contents, the time scale actually used (with the Task 9 speed-check outcome), the matchups, the seeds, and how to reproduce each file via the menu items. It must not draw conclusions; that belongs to the report.
- [ ] **Step 5: Commit** `docs(benchmark): post-extension simulation, scaling series and metrics`
