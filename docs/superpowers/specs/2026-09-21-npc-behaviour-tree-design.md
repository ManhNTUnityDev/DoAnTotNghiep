# NPC System (Behaviour Tree) — Design Spec

- **Date:** 2026-09-21
- **Status:** Design approved, pending spec review → implementation plan
- **Scope:** The end-to-end NPC system for the chase game — hand-rolled Behaviour Tree core, AI perception, hybrid NavMesh movement, Chaser & Runner brains, the ability framework (Gun / SpeedBoost / Decoy), TeamRoster, SpawnManager, and ChaserCoordinator. `JailSystem` (real cages / rescue) is **stubbed** behind an interface this round.
- **Builds on:** [core design spec](2026-09-09-chase-game-core-design.md) (esp. Chapter B2/B4/B5/B6) and the [foundation implementation](../../implementation/2026-09-18-foundation-di-character-body.md) (Body/Brain split, VContainer DI, player movement).

---

## 1. Purpose & success criteria

Turn the currently player-only vertical slice into a full **1 player + 7 NPC** match where the 7 NPCs think and act via Behaviour Trees, using the same pure-Body prefabs as the player. This spec resolves the technical decisions the core spec left open and defines a **phased build** where each phase is independently verifiable.

**Done means:** entering the `Game` scene spawns 8 characters (4 chasers + 4 runners); exactly one is player-controlled; the other 7 run AI brains — chasers hunt free runners and "shoot" them (capture via the stub), runners flee threats, use an escape skill, and attempt rescue; a `ChaserCoordinator` assigns cage guards; all AI decision logic is unit-tested in EditMode, and each phase has a visible PlayMode milestone.

## 2. Locked decisions

| Topic | Decision |
|---|---|
| Scope | Full NPC system end-to-end; real capture/jail/rescue **stubbed** behind `ICaptureService` |
| AI movement | **Hybrid** — `NavMeshAgent` computes the path (`updatePosition`/`updateRotation` off), its `desiredVelocity` feeds the existing `CharacterController` pipeline via `SetMoveDirection`. One motion path for player and AI. |
| Perception | **Distance + line-of-sight** raycast (vision radius from `AIConfig`); FOV cone deferred |
| BT model | **Hand-rolled**, pure C# (no `UnityEngine` in the core), **generic node + delegate** authoring (`ConditionNode(bb => bool)`, `ActionNode(bb => NodeStatus)`) |
| Abilities | Framework + cooldowns from `CharacterStats`; **Gun** (raycast → `ICaptureService.Capture`) and **SpeedBoost** are real; **Decoy** spawns a simple temporary lure the chaser perception treats as a fake target |
| Delivery | 7 phases (0→6), each independently verifiable (EditMode logic + a PlayMode milestone) |

## 3. Assemblies & namespaces

All code lives in the existing `ChaseGame` assembly (`Assets/Scripts/ChaseGame.asmdef`); tests in `ChaseGame.Tests.EditMode`. New namespaces:

| Namespace | Contents | Unity deps |
|---|---|---|
| `ChaseGame.AI.BehaviourTree` | BT core: `NodeStatus`, `Node`, `Selector`, `Sequence`, `ConditionNode`, `ActionNode`, `Blackboard` | **None** (pure C#) — EditMode-testable without a scene |
| `ChaseGame.AI` | `AIBrain` (abstract), `ChaserAIBrain`, `RunnerAIBrain`, `Sensor`, `AIConfig` (SO), `ChaserCoordinator`, tree builders | UnityEngine, AI (NavMesh) |
| `ChaseGame.Abilities` | `CharacterAbilities`, `IAbility`, `Gun`, `SpeedBoost`, `Decoy` | UnityEngine |
| `ChaseGame.Match` | `ITeamRoster`, `TeamRoster`, `CharacterFactory`, `SpawnManager`, `ICaptureService` + `CaptureServiceStub`, `CageStub` | UnityEngine + VContainer |

> `Blackboard` holds Unity references (`Transform`, `Character`) as fields, but the **core node types** (`Node`/`Selector`/`Sequence`/`ConditionNode`/`ActionNode`) only ever see the blackboard as an opaque bag through the delegates, so their logic tests without a scene. Where a delegate needs Unity math (distances), that math is extracted into static pure functions that are tested directly.

## 4. BT core (`ChaseGame.AI.BehaviourTree`)

```csharp
public enum NodeStatus { Success, Failure, Running }

public abstract class Node
{
    public abstract NodeStatus Tick(Blackboard bb);
}

// Priority / fallback: first non-Failure child wins.
public sealed class Selector : Node
{
    public Selector(params Node[] children);
    // Tick: children in order; return first Success/Running; else Failure.
}

public sealed class Sequence : Node
{
    public Sequence(params Node[] children);
    // Tick: children in order; return first Failure/Running; else Success.
}

public sealed class ConditionNode : Node
{
    public ConditionNode(Func<Blackboard, bool> predicate);
    // Tick: predicate(bb) ? Success : Failure.
}

public sealed class ActionNode : Node
{
    public ActionNode(Func<Blackboard, NodeStatus> action);
    // Tick: action(bb).
}
```

**Running semantics (kept deliberately simple for this scope):** the tree is stateless between ticks — each tick re-evaluates from the root. `Running` propagates up so a composite reports it, but there is **no** cached "resume at last running child" bookkeeping. This is adequate because the AI re-ticks at a low rate and re-deciding from the top each tick is the desired reactive behaviour (a closer threat should interrupt). Node-memory decorators are **out of scope**; add them only if a concrete behaviour proves to need them.

**Delegate authoring:** trees are built in `AIBrain.BuildTree()` with lambdas closing over the brain's blackboard/helpers, e.g.

```csharp
new Selector(
    new Sequence(
        new ConditionNode(bb => InShootRange(bb)),
        new ActionNode(bb => AimAndShoot(bb))),
    new Sequence(
        new ConditionNode(bb => bb.CurrentTarget != null),
        new ActionNode(bb => ChaseNearestRunner(bb))),
    new ActionNode(bb => Wander(bb)));
```

The condition/action helpers (`InShootRange`, `ChaseNearestRunner`, …) live on the brain (or shared static helpers) and **only call the Body API** (`Character.Move/MoveTo/Look/UseAbility`) plus read the blackboard — never `NavMeshAgent`/animation directly.

## 5. Movement — hybrid NavMesh (changes to the existing Body)

The seam and Body are extended so the **same prefab** serves both a player brain (direction commands) and an AI brain (destination commands).

**`ICharacterMovement`** gains:
```csharp
void MoveTo(Vector3 destination);   // AI: path to a point
void Stop();                         // zero motion (also used on jail entry)
```

**`CharacterMovement`** (still `[RequireComponent(typeof(CharacterController))]`, now also references a `NavMeshAgent` on the prefab configured `updatePosition = false; updateRotation = false; agent.speed = stats.MoveSpeed`):
- Internal mode flag `{ Direction, Agent }`.
- `SetMoveDirection(dir)` → mode = Direction, stores `moveDirection` (unchanged player path).
- `MoveTo(dest)` → mode = Agent, `agent.SetDestination(dest)`.
- `Stop()` → mode = Direction, `moveDirection = Vector3.zero`, and `agent.ResetPath()` if on a NavMesh.
- `Update()`:
  - In Agent mode, derive `moveDirection = agent.desiredVelocity.sqrMagnitude > eps ? agent.desiredVelocity.normalized : Vector3.zero`.
  - Then the **existing** `ComputeVelocity(moveDirection, speed) + gravity` → `controller.Move(...)` runs for **both** modes. Because `agent.updatePosition = false`, the agent does not move the transform; after `controller.Move`, sync the agent with `agent.nextPosition = transform.position` so path steering stays correct.

**`Character`** gains `MoveTo(Vector3)`, `Look(Vector3)`, `Stop()` — each a no-op when `CaptureState == Jailed`. `Move(dir)` is unchanged.

**Jail immobilization:** when a character enters `Jailed` (set by the capture stub), the Body calls `movement.Stop()`. This closes the [known follow-up](../../implementation/2026-09-18-foundation-di-character-body.md#8-known-follow-ups-deferred-by-design) where a jailed-while-moving character would keep sliding.

**Editor dependency:** the map needs a **baked NavMesh**; the character prefab gains a `NavMeshAgent`.

## 6. Perception — `Sensor` + `Blackboard` + `AIConfig`

**`Blackboard`** (plain class, one per AI):
```
Character Self; ITeamRoster Roster; AIConfig Config;
Transform CurrentTarget;     // chaser: nearest free runner / decoy lure
Transform NearestThreat;     // runner: nearest chaser in threatRadius
CageStub NearestCage;        // runner: nearest cage worth rescuing
Transform GuardAssignment;   // chaser: cage to defend (from coordinator), or null
Vector3? Destination;        // scratch for MoveTo/Wander
```

**`Sensor`** (MonoBehaviour on the AI character; ticked with the brain, not every frame): queries the roster for candidate targets in `visionRadius`, filters by **line-of-sight** (a `Physics.Raycast` from eye to candidate against an obstacle mask), and writes the nearest survivor into the blackboard. The selection is factored into a pure static helper so it is EditMode-tested:
```csharp
static Transform NearestVisible(Vector3 from, IReadOnlyList<Candidate> cands, float radius, Func<Vector3,Vector3,bool> hasLineOfSight);
```
`Candidate` is a tiny struct `(Transform t, Vector3 pos)` so the test injects a fake `hasLineOfSight` and positions without a scene.

**`AIConfig`** (ScriptableObject, shared by many agents): `visionRadius`, `shootRange`, `threatRadius`, `rescueThreatRadius`, `wanderRadius`, `tickRateHz`.

## 7. `AIBrain` host

```csharp
public abstract class AIBrain : CharacterBrain
{
    protected Blackboard Blackboard { get; private set; }
    private Node root;
    private Sensor sensor;
    private float tickInterval, accum;

    public void Initialize(Character c, ITeamRoster roster, AIConfig cfg) {
        Initialize(c);                       // base sets Character
        Blackboard = new Blackboard { Self = c, Roster = roster, Config = cfg };
        sensor = /* get-or-add Sensor */;
        tickInterval = 1f / cfg.TickRateHz;
        root = BuildTree();
    }

    protected abstract Node BuildTree();

    private void Update() {                   // NavMeshAgent runs every frame on its own
        accum += Time.deltaTime;
        if (accum < tickInterval) return;
        accum = 0f;
        sensor.Sense(Blackboard);
        root.Tick(Blackboard);
    }
}
```
`ChaserAIBrain` / `RunnerAIBrain` override only `BuildTree()`. BT ticks at `AIConfig.TickRateHz` (5–10 Hz); the agent/controller motion still runs every frame.

## 8. Behaviour trees

Node shapes exactly as core spec §B2. Authored with the delegate style; helpers call only the Body API.

**ChaserAIBrain:**
```
Selector[
  Sequence[ Condition(target in shootRange && LoS) , Action(AimAndShoot) ],  // shoots rescuers too
  Sequence[ Condition(GuardAssignment != null)      , Action(DefendCage → MoveTo(cage)) ],
  Sequence[ Condition(CurrentTarget != null)        , Action(ChaseNearest → MoveTo(target)) ],
  Action(Wander) ]
```
`AimAndShoot` = `Look(target)` + `Character.UseAbility(Gun)`. Gun does the raycast + capture.

**RunnerAIBrain:**
```
Selector[
  Sequence[ Condition(NearestThreat within threatRadius),
            Selector[ Action(UseEscapeSkill if ready),           // SpeedBoost or Decoy
                      Action(Flee → MoveTo(away-from-threat)) ] ],
  Sequence[ Condition(NearestCage != null && no threat near),
            Action(GoRescue → MoveTo(cage zone)) ],              // rescue itself is contact (stub this round)
  Action(Wander) ]
```

Shared helpers: `MoveTo`, `DetectNearest` (reads blackboard), `IsInRange`, `Wander` (random point within `wanderRadius` sampled onto the NavMesh), `Flee` (pick a point away from the threat), `UseAbility`.

## 9. Abilities + capture stub

**`CharacterAbilities`** (on the prefab): holds an ordered `IAbility[]` per role; `UseAbility(int id)` checks the ability's cooldown (durations from `CharacterStats`) and invokes it. Chaser = `[Gun]`; Runner = `[SpeedBoost, Decoy]`. `Character.UseAbility(id)` forwards here (no-op when Jailed).

```csharp
public interface IAbility {
    float Cooldown { get; }
    bool IsReady { get; }
    void Use(Character self, Vector3 aim);
}
```

- **`Gun`** — raycast forward (from aim/`Look` direction) up to `shootRange`; if it hits a `Character` whose `Team == Runner` and `CaptureState == Free`, call `ICaptureService.Capture(thatCharacter)`. Raycast vs projectile: **raycast** (hitscan) this round.
- **`SpeedBoost`** — multiplies effective `MoveSpeed` for a duration (temporary stat override on `CharacterMovement`/`CharacterStats` read path), then reverts.
- **`Decoy`** — instantiates a short-lived `DecoyLure` (a transform tagged so a chaser's `Sensor` treats it as a `CurrentTarget` candidate) for a few seconds, then destroys it.

**Capture stub:**
```csharp
public interface ICaptureService {
    void Capture(Character runner);   // real cage/jail lands in a later slice
    event Action<Character> OnRunnerCaught;
    IReadOnlyList<CageStub> ActiveCages { get; }
}
```
`CaptureServiceStub`: sets `runner.CaptureState = Jailed` (Body `Stop()`s), spawns a `CageStub` marker at the runner's position, raises `OnRunnerCaught`, and adds it to `ActiveCages`. `CageStub` is a placeholder position/marker so `ChaserCoordinator` and `TeamRoster` have cages to reason about; **no real rescue-zone/progress** yet. When the real `JailSystem` lands it replaces this behind the same interface.

## 10. Spawn / Roster / Coordinator

**`ITeamRoster` / `TeamRoster`** (POCO, DI singleton): `IReadOnlyList<Character> Chasers/Runners`, `Character PlayerCharacter`, and read-only queries — `GetFreeRunners()`, `GetJailedRunners()`, `AllRunnersJailed`, `GetChasers()`. Free/Jailed source of truth stays on `Character.CaptureState`; the roster only reads/filters and raises no events. `ITeamRoster` lets the sensor/coordinator logic be unit-tested with a fake roster.

**`CharacterFactory`**: wraps `IObjectResolver.Instantiate(prefab, pos, rot)` so spawned prefabs get injected.

**`SpawnManager`** — **replaces `PlayerBootstrap`** (which is deleted this round). Injected with prefab refs, spawn-point transforms, configs, roster, coordinator, input, capture service:
```
SpawnMatch(playerTeam):
  spawn 4 ChaserCharacter + 4 RunnerCharacter at their spawn points via factory
  pick playerIdx in playerTeam
  player one → AddComponent<PlayerBrain>().Initialize(char, input)
  others     → AddComponent<Chaser/RunnerAIBrain>().Initialize(char, roster, aiConfig)
               if chaser → coordinator.Register(brain)
  roster.Add(char); roster.PlayerCharacter = chosen
```
Random 50/50 role for the player as per core spec §9. After spawn, `SpawnManager` points the scene's `SimpleFollowCamera` at the chosen player `Character` (the pre-placed scene character is gone, so the camera target must be set at runtime; the camera is found via `RegisterComponentInHierarchy<SimpleFollowCamera>()` or a serialized ref on the installer). Full `MatchManager`/HUD wiring is a separate slice.

**`ChaserCoordinator`** (DI, ticked ~5 Hz): each tick, find cages "threatened" by a free runner within `rescueThreatRadius`; assign the nearest **free** chaser as the single guard per threatened cage (writes `blackboard.GuardAssignment`); unassigned chasers hunt. With the stub producing cages on capture this exercises the real assignment path; when there are no cages every chaser hunts (graceful degrade). Guarding logic is factored into a pure function `AssignGuards(cages, chasers) → map` for EditMode testing.

## 11. DI wiring (`GameLifetimeScope`)

Add registrations (keep the existing input + joystick ones):
```
builder.Register<ITeamRoster, TeamRoster>(Lifetime.Singleton);
builder.Register<ICaptureService, CaptureServiceStub>(Lifetime.Singleton);
builder.Register<CharacterFactory>(Lifetime.Singleton);
builder.Register<ChaserCoordinator>(Lifetime.Singleton);
builder.RegisterEntryPoint<SpawnManager>();   // IStartable — replaces PlayerBootstrap
builder.RegisterComponentInHierarchy<SimpleFollowCamera>();   // SpawnManager retargets it to the player
// installer MonoBehaviour serializes: chaserPrefab, runnerPrefab, decoyPrefab,
//   Transform[] chaserSpawns/runnerSpawns, AIConfig, CharacterStats(x2)
```
Remove `RegisterComponentInHierarchy<Character>()` and `RegisterEntryPoint<PlayerBootstrap>()` — characters now come from the spawner, not the scene. Register `ChaserCoordinator` as `ITickable` so VContainer drives its tick.

## 12. Testing strategy

**EditMode (pure logic, no scene) — the bulk:**
- BT core: `Selector`/`Sequence` ordering & status propagation; `ConditionNode`/`ActionNode` mapping; a small composed tree with fake delegates.
- Movement math: agent-desiredVelocity → direction mapping edge cases (zero/normalize) reuse `ComputeVelocity`.
- `Sensor.NearestVisible`: nearest selection, radius cutoff, LoS filter (injected fake LoS).
- `TeamRoster`: add/query, `GetFreeRunners` reflects `CaptureState`, `AllRunnersJailed`.
- Abilities: cooldown gating (ready → use → not ready → recover); `Gun` target selection (Free runner vs chaser vs jailed) via a fake raycast seam; capture stub flips state + raises event.
- `ChaserCoordinator.AssignGuards`: one guard per threatened cage, nearest free chaser, none when no cages.

**PlayMode milestones (per phase, manual/observed):** see the phase table below.

## 13. Phase decomposition

Each phase is a self-contained deliverable; writing-plans turns each into ordered tasks. Order respects dependencies (movement + roster before brains; brains before coordinator/spawn).

| Phase | Deliverable | Verify |
|---|---|---|
| **0. BT core** | `ChaseGame.AI.BehaviourTree` (POCO) + tests | EditMode: composites, condition/action, sample tree — all green |
| **1. Hybrid movement** | `ICharacterMovement.MoveTo/Stop`, `CharacterMovement` agent mode, `Character.MoveTo/Stop/Look`, NavMeshAgent on prefab, jail-stop | EditMode: mapping math; PlayMode: a character `MoveTo`s a point on the baked NavMesh |
| **2. Perception + roster** | `AIConfig`, `Blackboard`, `Sensor` (+`NearestVisible`), `ITeamRoster`/`TeamRoster` | EditMode: sensor selection/LoS, roster queries |
| **3. AIBrain + ChaserAI** | `AIBrain` host, `ChaserAIBrain` (Chase + Wander; shoot node present but no-op until Phase 4) | **PlayMode: an NPC chaser hunts the player** ← first visible NPC |
| **4. Abilities + capture stub** | `CharacterAbilities`, `IAbility`, `Gun`, `SpeedBoost`, `Decoy`, `ICaptureService`+stub, `CageStub`; wire `AimAndShoot` | EditMode: cooldown/gun/capture; PlayMode: chaser shoots a free runner → it jails (stub) |
| **5. RunnerAI** | `RunnerAIBrain` (threat → escape skill / flee; rescue-to-cage via stub; wander) | PlayMode: runners flee chasers and use SpeedBoost/Decoy |
| **6. Spawn + coordinator** | `CharacterFactory`, `SpawnManager` (replaces `PlayerBootstrap`), `ChaserCoordinator`, DI wiring, installer refs | PlayMode: full 1-player + 7-NPC match; exactly one `PlayerBrain`; chasers guard threatened cages |

## 14. Editor setup dependencies

- **Remove the pre-placed `ChaserCharacter` instance** from the `Game` scene — characters are now spawned by `SpawnManager`. The scene keeps the `Main Camera` (+ `SimpleFollowCamera`, target set at runtime), `GameLifetimeScope`, the installer, and the spawn-point groups.
- **Bake a NavMesh** for the `Game` map.
- Add a `NavMeshAgent` to `ChaserCharacter`/`RunnerCharacter` prefabs (radius/height matching the `CharacterController`).
- Create `RunnerCharacter.prefab` (pure Body: `CharacterController` + `NavMeshAgent` + `CharacterMovement` + `Character` with `Team = Runner` + `CharacterAbilities`), mirroring the existing `ChaserCharacter`.
- Create SO assets: `RunnerStats` (`CharacterStats`), `AIConfig` (one shared, or per role).
- Create `ChaserSpawns` / `RunnerSpawns` parent objects with ≥4 child points each.
- A `DecoyLure` prefab and a `CageStub` marker prefab.
- Ensure an obstacle `LayerMask` exists for the sensor LoS raycast.

## 15. Out of scope (deferred to later slices)

- Real `JailSystem`: cage rescue-zone, rescue hold/progress, `OnRunnerRescued` (only the `ICaptureService` seam + capture-only stub exist now).
- `MatchManager`, `MatchTimer`, `WinConditionChecker`, win/lose flow.
- HUD / `UIManager` / result screens.
- Animation driven by movement speed.
- FOV vision cone; BT node-memory decorators; projectile guns.

## 16. Deferred to implementation

- Exact `tickRateHz` (start 8 Hz), `visionRadius`, ranges, `wanderRadius` — tune in `AIConfig`.
- Whether `AIConfig` is one shared asset or one per role.
- `SpeedBoost` implementation detail (temporary multiplier location on the movement/stats read path).
- Rotation/`Look` approach (rotate transform vs agent) given `updateRotation = false`.
- Obstacle layer setup for LoS.
