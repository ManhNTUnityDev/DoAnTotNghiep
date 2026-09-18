# Foundation: DI + Character Body + Player Movement — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the DI foundation and the Character Body so a single character in the Game scene can be driven by keyboard input (the first runnable vertical slice of the chase game).

**Architecture:** Body/Brain separation from the spec, realized with VContainer DI. The Character (Body) exposes a command API and delegates real movement to a `CharacterMovement` component behind an `ICharacterMovement` interface. A `PlayerBrain` (attached at runtime — "Method A") reads an injected `IInputService` and calls the Body API. A single `GameLifetimeScope` per scene wires input service + a bootstrap that attaches the player brain to the scene's character.

**Tech Stack:** Unity 6 (URP), C#, VContainer (DI), Unity Input System (new), CharacterController for movement, Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-09-09-chase-game-core-design.md` (Chapter A §2–6, Chapter B B0/B1/B6/B8)

## Global Constraints

- **DI container:** VContainer. One `LifetimeScope` per scene (spec B0). No message bus — direct DI + C# events (spec B).
- **Body/Brain boundary (spec §2, §26):** the Brain issues commands; the Body executes. Body code (`Character`, `CharacterMovement`) must never read input or know whether a player or AI drives it. Brains only call the Body's public command API — never touch `CharacterController`/animation directly.
- **Namespace:** all new code lives under the `ChaseGame` root namespace (sub-namespaces per folder). Do not modify the existing `LoadingBar.cs` (no namespace) — leave it as is.
- **Test seam:** pure logic and command-gating are covered by EditMode tests. MonoBehaviour wiring that needs a running scene is validated by a manual PlayMode checklist (Task 7), not automated, for this slice.
- **Movement tech decision (resolves spec §12 "Rigidbody vs CharacterController"):** use `CharacterController` for direct player movement in this slice.
- **Editor-side steps** (creating prefabs, placing objects in a scene, adding the LifetimeScope component, pressing Play) are performed by the human in the Unity Editor — the Unity MCP bridge is currently offline, so an agent cannot do them. These steps are called out explicitly in Task 1 and Task 7 and must be checked off by the human.

---

## File Structure

```
Assets/Scripts/
  ChaseGame.asmdef                       (NEW — game assembly; refs VContainer + Input System)
  Characters/
    CaptureState.cs                      (enum: Free, Jailed)
    ICharacterMovement.cs                (movement command interface)
    CharacterMovement.cs                 (CharacterController impl of ICharacterMovement)
    Character.cs                          (Body root: Team, CaptureState, Move())
    Team.cs                              (enum: Chaser, Runner)
    CharacterStats.cs                    (ScriptableObject: moveSpeed)
  Input/
    IInputService.cs                     (MoveAxis, etc.)
    InputSystemService.cs                (reads Keyboard/Gamepad via new Input System)
  Brains/
    CharacterBrain.cs                    (base MonoBehaviour holding Character ref)
    PlayerBrain.cs                       (reads IInputService -> Character.Move)
  Infrastructure/
    GameLifetimeScope.cs                 (VContainer scope for the Game scene)
    PlayerBootstrap.cs                   (IStartable: attach PlayerBrain to scene Character)
    SimpleFollowCamera.cs                (small camera follow so movement is visible)

Assets/Tests/EditMode/
  ChaseGame.Tests.EditMode.asmdef        (NEW — test assembly; refs ChaseGame)
  CharacterMovementMathTests.cs
  CharacterCommandGatingTests.cs
  PlayerBrainTests.cs
  FakeInputService.cs                    (test double for IInputService)
  SpyCharacterMovement.cs                (test double for ICharacterMovement)
```

Packages/manifest.json — MODIFIED to add VContainer.

---

### Task 1: Project scaffolding — VContainer + assembly definitions + green test harness

Sets up dependencies and the two assemblies everything else compiles into, and proves the test runner works. This whole task is scaffolding for later tasks, so it is one task ending in a runnable (trivially green) EditMode test.

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/Scripts/ChaseGame.asmdef`
- Create: `Assets/Tests/EditMode/ChaseGame.Tests.EditMode.asmdef`
- Create: `Assets/Tests/EditMode/HarnessSmokeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: assembly `ChaseGame` (root namespace `ChaseGame`) and test assembly `ChaseGame.Tests.EditMode`. Later tasks add files under `Assets/Scripts/**` (auto-included in `ChaseGame`) and tests under `Assets/Tests/EditMode/**`.

- [ ] **Step 1 (HUMAN, Editor): Install VContainer via manifest**

Add VContainer through the OpenUPM scoped registry. Edit `Packages/manifest.json`: add a `scopedRegistries` block and the dependency line.

```json
{
  "dependencies": {
    "jp.hadashikick.vcontainer": "1.16.8",
    "com.unity.ai.navigation": "2.0.12",
    ... (leave all existing dependencies unchanged) ...
  },
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "jp.hadashikick.vcontainer"
      ]
    }
  ]
}
```

Then return to the Unity Editor and let it resolve packages (Window ▸ Package Manager should show VContainer). Confirm no console errors.

- [ ] **Step 2: Create the game assembly definition**

Create `Assets/Scripts/ChaseGame.asmdef`:

```json
{
    "name": "ChaseGame",
    "rootNamespace": "ChaseGame",
    "references": [
        "VContainer",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Create the EditMode test assembly definition**

Create `Assets/Tests/EditMode/ChaseGame.Tests.EditMode.asmdef`:

```json
{
    "name": "ChaseGame.Tests.EditMode",
    "rootNamespace": "ChaseGame.Tests",
    "references": [
        "ChaseGame",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "VContainer",
        "Unity.InputSystem"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Write a smoke test**

Create `Assets/Tests/EditMode/HarnessSmokeTests.cs`:

```csharp
using NUnit.Framework;

namespace ChaseGame.Tests
{
    public class HarnessSmokeTests
    {
        [Test]
        public void TestHarness_Runs()
        {
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 5 (HUMAN, Editor): Run EditMode tests**

Open Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
Expected: `HarnessSmokeTests.TestHarness_Runs` PASSES. This confirms VContainer resolved, both asmdefs compile, and the runner sees the test assembly.

- [ ] **Step 6: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json Assets/Scripts/ChaseGame.asmdef Assets/Scripts/ChaseGame.asmdef.meta Assets/Tests
git commit -m "chore: add VContainer + assembly definitions + EditMode test harness"
```

---

### Task 2: Character enums + CharacterStats ScriptableObject

Small pure data types every later task references. No behaviour, so no test cycle of its own beyond compiling; it is folded here rather than spread across tasks.

**Files:**
- Create: `Assets/Scripts/Characters/Team.cs`
- Create: `Assets/Scripts/Characters/CaptureState.cs`
- Create: `Assets/Scripts/Characters/CharacterStats.cs`

**Interfaces:**
- Produces: `enum Team { Chaser, Runner }`, `enum CaptureState { Free, Jailed }`, `class CharacterStats : ScriptableObject` with `float MoveSpeed { get; }` (backed by serialized field `moveSpeed`, default 5).

- [ ] **Step 1: Create the enums**

`Assets/Scripts/Characters/Team.cs`:

```csharp
namespace ChaseGame.Characters
{
    public enum Team
    {
        Chaser,
        Runner
    }
}
```

`Assets/Scripts/Characters/CaptureState.cs`:

```csharp
namespace ChaseGame.Characters
{
    public enum CaptureState
    {
        Free,
        Jailed
    }
}
```

- [ ] **Step 2: Create the CharacterStats ScriptableObject**

`Assets/Scripts/Characters/CharacterStats.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Characters
{
    [CreateAssetMenu(menuName = "ChaseGame/Character Stats", fileName = "CharacterStats")]
    public class CharacterStats : ScriptableObject
    {
        [SerializeField] private float moveSpeed = 5f;

        public float MoveSpeed => moveSpeed;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Characters
git commit -m "feat: add Team/CaptureState enums and CharacterStats SO"
```

---

### Task 3: CharacterMovement — pure velocity math (TDD)

The one piece of movement that is pure and unit-testable: turning a move direction + speed into a world velocity. Kept separate so the math has its own red/green cycle before it is embedded in a MonoBehaviour.

**Files:**
- Create: `Assets/Scripts/Characters/ICharacterMovement.cs`
- Create: `Assets/Scripts/Characters/CharacterMovement.cs`
- Test: `Assets/Tests/EditMode/CharacterMovementMathTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `interface ICharacterMovement { void SetMoveDirection(Vector3 direction); }`
  - `class CharacterMovement : MonoBehaviour, ICharacterMovement` with a `public static Vector3 ComputeVelocity(Vector3 direction, float speed)` helper (normalizes non-zero direction, zero stays zero) used by later movement code.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/CharacterMovementMathTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class CharacterMovementMathTests
    {
        [Test]
        public void ComputeVelocity_ZeroDirection_IsZero()
        {
            var v = CharacterMovement.ComputeVelocity(Vector3.zero, 5f);
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void ComputeVelocity_NormalizesDirectionAndScalesBySpeed()
        {
            var v = CharacterMovement.ComputeVelocity(new Vector3(3f, 0f, 0f), 5f);
            Assert.AreEqual(5f, v.x, 1e-4f);
            Assert.AreEqual(0f, v.z, 1e-4f);
        }

        [Test]
        public void ComputeVelocity_DiagonalHasSpeedMagnitude()
        {
            var v = CharacterMovement.ComputeVelocity(new Vector3(1f, 0f, 1f), 5f);
            Assert.AreEqual(5f, v.magnitude, 1e-4f);
        }
    }
}
```

- [ ] **Step 2 (HUMAN, Editor): Run test to verify it fails**

Test Runner ▸ EditMode ▸ run `CharacterMovementMathTests`.
Expected: FAILS to compile / not found (`CharacterMovement` does not exist yet).

- [ ] **Step 3: Write the interface and minimal implementation**

`Assets/Scripts/Characters/ICharacterMovement.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Characters
{
    public interface ICharacterMovement
    {
        void SetMoveDirection(Vector3 direction);
    }
}
```

`Assets/Scripts/Characters/CharacterMovement.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        [SerializeField] private CharacterStats stats;

        private CharacterController controller;
        private Vector3 moveDirection;

        public static Vector3 ComputeVelocity(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            return direction.normalized * speed;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public void SetMoveDirection(Vector3 direction)
        {
            moveDirection = direction;
        }

        private void Update()
        {
            float speed = stats != null ? stats.MoveSpeed : 5f;
            Vector3 velocity = ComputeVelocity(moveDirection, speed);
            // simple gravity so the CharacterController stays grounded
            velocity += Physics.gravity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
```

- [ ] **Step 4 (HUMAN, Editor): Run test to verify it passes**

Test Runner ▸ EditMode ▸ run `CharacterMovementMathTests`.
Expected: all three PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Characters/ICharacterMovement.cs Assets/Scripts/Characters/CharacterMovement.cs Assets/Tests/EditMode/CharacterMovementMathTests.cs
git commit -m "feat: add CharacterMovement with tested velocity math"
```

---

### Task 4: Character (Body root) with capture-state gating (TDD)

The Body's command API. `Move` forwards to the movement component when Free and no-ops when Jailed (spec §4, B4, B6 "Move() no-ops when Jailed"). Tested with a spy movement so no CharacterController is needed.

**Files:**
- Create: `Assets/Scripts/Characters/Character.cs`
- Test: `Assets/Tests/EditMode/SpyCharacterMovement.cs`
- Test: `Assets/Tests/EditMode/CharacterCommandGatingTests.cs`

**Interfaces:**
- Consumes: `ICharacterMovement` (Task 3), `Team`/`CaptureState` (Task 2).
- Produces: `class Character : MonoBehaviour` with:
  - `Team Team { get; }`
  - `CaptureState CaptureState { get; set; }` (default `Free`)
  - `void Move(Vector3 direction)` — forwards to movement when `Free`, no-op when `Jailed`
  - `void SetMovementForTests(ICharacterMovement movement)` (internal seam; also resolved from `GetComponent` in `Awake`)

- [ ] **Step 1: Write the test double**

`Assets/Tests/EditMode/SpyCharacterMovement.cs`:

```csharp
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class SpyCharacterMovement : ICharacterMovement
    {
        public int CallCount { get; private set; }
        public Vector3 LastDirection { get; private set; }

        public void SetMoveDirection(Vector3 direction)
        {
            CallCount++;
            LastDirection = direction;
        }
    }
}
```

- [ ] **Step 2: Write the failing test**

`Assets/Tests/EditMode/CharacterCommandGatingTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class CharacterCommandGatingTests
    {
        private Character character;
        private SpyCharacterMovement spy;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Character");
            character = go.AddComponent<Character>();
            spy = new SpyCharacterMovement();
            character.SetMovementForTests(spy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character.gameObject);
        }

        [Test]
        public void Move_WhenFree_ForwardsToMovement()
        {
            character.CaptureState = CaptureState.Free;
            character.Move(Vector3.forward);

            Assert.AreEqual(1, spy.CallCount);
            Assert.AreEqual(Vector3.forward, spy.LastDirection);
        }

        [Test]
        public void Move_WhenJailed_IsNoOp()
        {
            character.CaptureState = CaptureState.Jailed;
            character.Move(Vector3.forward);

            Assert.AreEqual(0, spy.CallCount);
        }

        [Test]
        public void CaptureState_DefaultsToFree()
        {
            var go = new GameObject("Fresh");
            var fresh = go.AddComponent<Character>();
            Assert.AreEqual(CaptureState.Free, fresh.CaptureState);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 3 (HUMAN, Editor): Run test to verify it fails**

Expected: FAILS to compile (`Character` does not exist).

- [ ] **Step 4: Write minimal implementation**

`Assets/Scripts/Characters/Character.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Chaser;

        private ICharacterMovement movement;

        public Team Team => team;
        public CaptureState CaptureState { get; set; } = CaptureState.Free;

        private void Awake()
        {
            // Resolve the sibling movement component if a test hasn't injected one.
            movement ??= GetComponent<ICharacterMovement>();
        }

        public void Move(Vector3 direction)
        {
            if (CaptureState == CaptureState.Jailed)
            {
                return;
            }

            movement?.SetMoveDirection(direction);
        }

        // Test seam: inject a movement double without a CharacterController.
        public void SetMovementForTests(ICharacterMovement injected)
        {
            movement = injected;
        }
    }
}
```

- [ ] **Step 5 (HUMAN, Editor): Run test to verify it passes**

Expected: all three PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Characters/Character.cs Assets/Tests/EditMode/SpyCharacterMovement.cs Assets/Tests/EditMode/CharacterCommandGatingTests.cs
git commit -m "feat: add Character body with capture-state move gating"
```

---

### Task 5: Input service — IInputService + InputSystemService

The input seam (spec B8). `IInputService` exposes a normalized move axis; `InputSystemService` reads the new Input System directly (WASD/arrows + left stick) so no `.inputactions` asset needs authoring for this slice. A fake in the test assembly proves the seam.

**Files:**
- Create: `Assets/Scripts/Input/IInputService.cs`
- Create: `Assets/Scripts/Input/InputSystemService.cs`
- Test: `Assets/Tests/EditMode/FakeInputService.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `interface IInputService { Vector2 MoveAxis { get; } }` (only `MoveAxis` for this slice; aim/skills added in later plans)
  - `class InputSystemService : IInputService` reading `Keyboard.current` / `Gamepad.current`
  - test-assembly `class FakeInputService : IInputService` with a settable `MoveAxis`.

- [ ] **Step 1: Create the interface**

`Assets/Scripts/Input/IInputService.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Input
{
    public interface IInputService
    {
        Vector2 MoveAxis { get; }
    }
}
```

- [ ] **Step 2: Create the Input System implementation**

`Assets/Scripts/Input/InputSystemService.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChaseGame.Input
{
    public class InputSystemService : IInputService
    {
        public Vector2 MoveAxis
        {
            get
            {
                Vector2 axis = Vector2.zero;

                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) axis.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) axis.y -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis.x += 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis.x -= 1f;
                }

                var gamepad = Gamepad.current;
                if (gamepad != null && axis == Vector2.zero)
                {
                    axis = gamepad.leftStick.ReadValue();
                }

                return Vector2.ClampMagnitude(axis, 1f);
            }
        }
    }
}
```

- [ ] **Step 3: Create the fake for tests**

`Assets/Tests/EditMode/FakeInputService.cs`:

```csharp
using UnityEngine;
using ChaseGame.Input;

namespace ChaseGame.Tests
{
    public class FakeInputService : IInputService
    {
        public Vector2 MoveAxis { get; set; }
    }
}
```

- [ ] **Step 4 (HUMAN, Editor): Verify compile**

Return to the Editor; confirm the console shows no compile errors (there is no behaviour to test yet — `InputSystemService` reads live devices, exercised in Task 7).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Input Assets/Tests/EditMode/FakeInputService.cs
git commit -m "feat: add IInputService + Input System implementation"
```

---

### Task 6: Brains — CharacterBrain base + PlayerBrain (TDD)

`PlayerBrain` maps input to Body commands (spec §5, B2 "PlayerBrain single class"). It converts the 2D `MoveAxis` into a world-space XZ direction and calls `Character.Move`. Tested by driving a real `Character` (with a spy movement) through the brain's per-frame tick.

**Files:**
- Create: `Assets/Scripts/Brains/CharacterBrain.cs`
- Create: `Assets/Scripts/Brains/PlayerBrain.cs`
- Test: `Assets/Tests/EditMode/PlayerBrainTests.cs`

**Interfaces:**
- Consumes: `Character` (Task 4), `IInputService` (Task 5), `SpyCharacterMovement`/`FakeInputService` (Tasks 4/5).
- Produces:
  - `abstract class CharacterBrain : MonoBehaviour` with `protected Character Character { get; }` and `void Initialize(Character character)`.
  - `class PlayerBrain : CharacterBrain` with `void Initialize(Character character, IInputService input)` and a `public void Tick()` that reads input and calls `Character.Move`. `Update()` calls `Tick()`.
  - `public static Vector3 ToWorldMove(Vector2 axis)` on `PlayerBrain` mapping `(x,y)` → `(x,0,y)`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/PlayerBrainTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Brains;

namespace ChaseGame.Tests
{
    public class PlayerBrainTests
    {
        private Character character;
        private SpyCharacterMovement spy;
        private PlayerBrain brain;
        private FakeInputService input;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Character");
            character = go.AddComponent<Character>();
            spy = new SpyCharacterMovement();
            character.SetMovementForTests(spy);

            input = new FakeInputService();
            brain = go.AddComponent<PlayerBrain>();
            brain.Initialize(character, input);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character.gameObject);
        }

        [Test]
        public void ToWorldMove_MapsAxisXToWorldXAndAxisYToWorldZ()
        {
            var world = PlayerBrain.ToWorldMove(new Vector2(1f, 0.5f));
            Assert.AreEqual(1f, world.x, 1e-4f);
            Assert.AreEqual(0f, world.y, 1e-4f);
            Assert.AreEqual(0.5f, world.z, 1e-4f);
        }

        [Test]
        public void Tick_ForwardsInputAsWorldMove()
        {
            input.MoveAxis = new Vector2(0f, 1f);
            brain.Tick();

            Assert.AreEqual(1, spy.CallCount);
            Assert.AreEqual(Vector3.forward, spy.LastDirection);
        }

        [Test]
        public void Tick_WhenJailed_DoesNotMove()
        {
            character.CaptureState = CaptureState.Jailed;
            input.MoveAxis = new Vector2(0f, 1f);
            brain.Tick();

            Assert.AreEqual(0, spy.CallCount);
        }
    }
}
```

- [ ] **Step 2 (HUMAN, Editor): Run test to verify it fails**

Expected: FAILS to compile (`CharacterBrain` / `PlayerBrain` do not exist).

- [ ] **Step 3: Write CharacterBrain base**

`Assets/Scripts/Brains/CharacterBrain.cs`:

```csharp
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Brains
{
    public abstract class CharacterBrain : MonoBehaviour
    {
        protected Character Character { get; private set; }

        public void Initialize(Character character)
        {
            Character = character;
        }
    }
}
```

- [ ] **Step 4: Write PlayerBrain**

`Assets/Scripts/Brains/PlayerBrain.cs`:

```csharp
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Brains
{
    public class PlayerBrain : CharacterBrain
    {
        private IInputService input;

        public void Initialize(Character character, IInputService inputService)
        {
            Initialize(character);
            input = inputService;
        }

        public static Vector3 ToWorldMove(Vector2 axis)
        {
            return new Vector3(axis.x, 0f, axis.y);
        }

        public void Tick()
        {
            if (input == null || Character == null)
            {
                return;
            }

            Character.Move(ToWorldMove(input.MoveAxis));
        }

        private void Update()
        {
            Tick();
        }
    }
}
```

- [ ] **Step 5 (HUMAN, Editor): Run test to verify it passes**

Expected: all three PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Brains Assets/Tests/EditMode/PlayerBrainTests.cs
git commit -m "feat: add CharacterBrain base and PlayerBrain input mapping"
```

---

### Task 7: DI wiring + scene integration — walk one character

Assembles the slice: a `GameLifetimeScope` registers `IInputService` and a `PlayerBootstrap` entry point that attaches `PlayerBrain` to the scene's `Character` (spec "Method A" attachment, B5). Includes the human Editor steps to build the prefab/scene and the manual PlayMode acceptance check. This is the payoff task — its deliverable is a running character, verified by the checklist.

**Files:**
- Create: `Assets/Scripts/Infrastructure/GameLifetimeScope.cs`
- Create: `Assets/Scripts/Infrastructure/PlayerBootstrap.cs`
- Create: `Assets/Scripts/Infrastructure/SimpleFollowCamera.cs`

**Interfaces:**
- Consumes: `IInputService`/`InputSystemService` (Task 5), `Character` (Task 4), `PlayerBrain` (Task 6).
- Produces: `GameLifetimeScope : LifetimeScope` (VContainer) and `PlayerBootstrap : IStartable`. No later task in this plan consumes these (end of slice).

- [ ] **Step 1: Write PlayerBootstrap (VContainer IStartable)**

`Assets/Scripts/Infrastructure/PlayerBootstrap.cs`:

```csharp
using VContainer;
using VContainer.Unity;
using ChaseGame.Brains;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Infrastructure
{
    // Temporary foundation harness: attaches the player brain to the single
    // Character placed in the scene. SpawnManager will replace this in a later plan.
    public class PlayerBootstrap : IStartable
    {
        private readonly Character character;
        private readonly IInputService input;

        public PlayerBootstrap(Character character, IInputService input)
        {
            this.character = character;
            this.input = input;
        }

        public void Start()
        {
            // Method A: attach the Brain at runtime, prefab stays "pure body".
            var brain = character.gameObject.AddComponent<PlayerBrain>();
            brain.Initialize(character, input);
        }
    }
}
```

- [ ] **Step 2: Write GameLifetimeScope**

`Assets/Scripts/Infrastructure/GameLifetimeScope.cs`:

```csharp
using VContainer;
using VContainer.Unity;
using ChaseGame.Characters;
using ChaseGame.Input;

namespace ChaseGame.Infrastructure
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IInputService, InputSystemService>(Lifetime.Singleton);

            // The single Character currently placed in the Game scene.
            builder.RegisterComponentInHierarchy<Character>();

            builder.RegisterEntryPoint<PlayerBootstrap>();
        }
    }
}
```

- [ ] **Step 3: Write SimpleFollowCamera**

`Assets/Scripts/Infrastructure/SimpleFollowCamera.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Infrastructure
{
    public class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.LookAt(target);
        }
    }
}
```

- [ ] **Step 4 (HUMAN, Editor): Create the CharacterStats asset**

In Project window: right-click ▸ Create ▸ ChaseGame ▸ Character Stats. Name it `ChaserStats`. Set Move Speed = 5.

- [ ] **Step 5 (HUMAN, Editor): Build the character in the Game scene**

Open `Assets/Scenes/Game.unity`. Create the test character:
1. Create an empty GameObject named `Chaser`. Add components: `Character Controller`, `CharacterMovement`, `Character`.
2. On `CharacterMovement`, assign the `ChaserStats` asset to the Stats field.
3. On `Character`, set Team = Chaser.
4. Give it a visible child (e.g. a Capsule 3D object) so you can see it move; position the CharacterController to fit.
5. Add a ground plane (3D Object ▸ Plane) under it if the scene has none.

- [ ] **Step 6 (HUMAN, Editor): Add the LifetimeScope and camera**

1. Create an empty GameObject named `GameLifetimeScope`; add the `GameLifetimeScope` component.
2. Select `Main Camera`; add `SimpleFollowCamera`; drag the `Chaser` object into its Target field.
3. Ensure the scene has an `EventSystem`/input is set to the new Input System (Project Settings ▸ Player ▸ Active Input Handling = "Input System Package" or "Both"). If it shows "Input Manager (Old)", switch it and let the Editor restart.

- [ ] **Step 7 (HUMAN, Editor): PlayMode acceptance check**

Press Play in the Game scene. Verify:
- No console errors on entering Play.
- Exactly one `PlayerBrain` component appears on the `Chaser` at runtime (added by `PlayerBootstrap`).
- WASD / arrow keys move the character around the plane; releasing keys stops it.
- The camera follows the character.

If any check fails, this is a wiring bug — debug before committing (use superpowers:systematic-debugging).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Infrastructure Assets/Scenes/Game.unity Assets/Settings ProjectSettings
git commit -m "feat: wire GameLifetimeScope + player bootstrap; drive one character with input"
```

(Adjust the `git add` paths to whatever the Editor actually created/modified — e.g. the new `.asset`, prefab, and scene `.meta` files. Run `git status` first and stage the real changes.)

---

## Self-Review

**Spec coverage (foundation slice only):**
- Body/Brain separation (§2, §5) → Tasks 4 (Body), 6 (Brain), 7 (Method-A attach). ✔
- Prefabs "pure body", Brain attached at spawn (§3, §5, B5) → `PlayerBootstrap` adds `PlayerBrain` at runtime. ✔ (A true prefab asset is optional for this slice — a scene object suffices; full prefab/spawn is the next plan.)
- `Character` API `Move()` no-op when Jailed (§4, B4, B6) → Task 4. ✔
- `CharacterMovement` (§4, B6) with CharacterController decision (§12) → Task 3 + constraint. ✔
- `CharacterStats` SO (§4) → Task 2. ✔
- `IInputService`, only `PlayerBrain` uses it (B8) → Task 5/6. ✔
- One `LifetimeScope` per scene (B0) → Task 7. ✔
- Out of scope by design (later plans): abilities/Gun, AI/BT, spawn manager/roster, capture/jail/rescue, match/win-lose, HUD/UIManager, camera controller polish, NavMesh. These are intentionally excluded from this slice and noted in the plan goal.

**Placeholder scan:** No TBD/TODO left; every code step has full source. ✔

**Type consistency:** `ICharacterMovement.SetMoveDirection(Vector3)`, `Character.Move(Vector3)`, `Character.SetMovementForTests(ICharacterMovement)`, `IInputService.MoveAxis` (Vector2), `PlayerBrain.Initialize(Character, IInputService)` / `PlayerBrain.Tick()` / `PlayerBrain.ToWorldMove(Vector2)` are used identically across tasks and tests. ✔

**Note on TDD in Unity:** MonoBehaviour wiring and live-device input can't be meaningfully unit-tested here, so the slice's end-to-end behaviour is validated by the Task 7 manual PlayMode checklist rather than an automated PlayMode test — a deliberate scope choice for the foundation.
