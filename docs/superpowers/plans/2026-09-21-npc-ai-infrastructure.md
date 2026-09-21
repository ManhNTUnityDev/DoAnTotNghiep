# NPC AI Infrastructure (Phases 0–2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the deterministic AI infrastructure the NPC brains will stand on — a hand-rolled Behaviour Tree core, hybrid NavMesh movement on the shared Body, and perception (Sensor) + `TeamRoster` — all unit-tested, with a PlayMode check that a character path-follows on the NavMesh. No autonomous NPC yet (that is plan #2, Phase 3+).

**Architecture:** A pure-C# Behaviour Tree core (`Node`/`Selector`/`Sequence`/`ConditionNode`/`ActionNode`) that only sees a `Blackboard` through delegates, so its logic tests without a scene. The existing Body gains a **hybrid** movement mode: a `NavMeshAgent` (with `updatePosition`/`updateRotation` off) computes the path, and its `desiredVelocity` feeds the *same* `CharacterController` pipeline the player already uses via `SetMoveDirection`. A passive `TeamRoster` reads `Character.CaptureState`; a `Sensor` fills the blackboard with the nearest **line-of-sight** target within a radius.

**Tech Stack:** Unity 6 (URP), C#, VContainer (DI), Unity AI Navigation (`NavMeshAgent`, `UnityEngine.AI`), Unity Test Framework (EditMode/NUnit).

**Spec:** `docs/superpowers/specs/2026-09-21-npc-behaviour-tree-design.md` (this plan implements Phases 0–2 of the phase table in §13; §4–§6 for the component detail).

## Global Constraints

- **Assembly:** all code lives in the existing `ChaseGame` assembly (`Assets/Scripts/ChaseGame.asmdef`). Do **not** add asmdef references — `NavMeshAgent`/`NavMesh` come from `com.unity.modules.ai` (already in `Packages/manifest.json`) and are engine-auto-referenced.
- **Namespaces:** BT core + `Blackboard` → `ChaseGame.AI.BehaviourTree`; `AIConfig`, `Sensor` → `ChaseGame.AI`; `ITeamRoster`, `TeamRoster` → `ChaseGame.Match`. Existing `ChaseGame.Characters` / `ChaseGame.Brains` / `ChaseGame.Input` / `ChaseGame.Infrastructure` unchanged in identity.
- **BT core purity:** `NodeStatus`, `Node`, `Selector`, `Sequence`, `ConditionNode`, `ActionNode` must **not** reference `UnityEngine`. `Blackboard` may (it holds `Character`/`Transform`).
- **Body/Brain boundary (spec core §2):** movement changes stay in the Body; nothing here reads input or knows player-vs-AI.
- **Movement decision (spec §5):** **hybrid** — agent computes path, `CharacterController` moves. One motion pipeline for player and AI. Do not replace `CharacterController`.
- **Do not break the existing player:** every existing EditMode test in `Assets/Tests/EditMode/` must stay green, and the scene player must still move with WASD/joystick after the movement change. `SpyCharacterMovement` must be updated in lock-step when `ICharacterMovement` grows, or nothing compiles.
- **Editor/PlayMode steps are HUMAN** (creating/baking NavMesh, adding the `NavMeshAgent` to the prefab, pressing Play) **unless the Unity MCP bridge is online**, in which case `mcp__UnityMCP__*` tools may perform them. Test runs use Test Runner ▸ EditMode ▸ Run All, or `mcp__UnityMCP__run_tests` (EditMode) if the bridge is online.
- **Commit after every task** (frequent commits). Include `.meta` files Unity creates for new folders/files/assets — run `git status` and stage what the Editor actually produced.

## Review Focus

- **Empty composites:** a `Selector` with no children must return `Failure`; a `Sequence` with no children must return `Success`. (Pinned in Tasks 3 and 2.)
- **Commands while Jailed:** `Character.MoveTo` must no-op when `CaptureState == Jailed`, exactly like `Move`. (Pinned in Task 6.)
- **Arrival jitter:** when the agent's `desiredVelocity` is ~zero (at/near the destination), the derived move direction must be exactly zero, not a normalized denormal. (Pinned in Task 5.)
- **No target found:** `Sensor.NearestVisible` with an empty list, all candidates outside the radius, or all blocked by line-of-sight must return `null` (consumers read a null target). (Pinned in Task 10.)
- **All-runners-jailed on zero runners:** `TeamRoster.AllRunnersJailed()` must return `false` when there are no runners at all, not vacuously `true`. (Pinned in Task 8.)

---

## File Structure

```
Assets/Scripts/
  AI/
    BehaviourTree/
      NodeStatus.cs        (NEW — enum Success/Failure/Running)
      Node.cs              (NEW — abstract base: Tick(Blackboard))
      ConditionNode.cs     (NEW — Func<Blackboard,bool> leaf)
      ActionNode.cs        (NEW — Func<Blackboard,NodeStatus> leaf)
      Sequence.cs          (NEW — AND composite)
      Selector.cs          (NEW — priority/fallback composite)
      Blackboard.cs        (NEW — shared data bag; grows across tasks)
    AIConfig.cs            (NEW — ScriptableObject tunables)
    Sensor.cs              (NEW — perception: CandidatesFor + NearestVisible + Sense)
  Characters/
    ICharacterMovement.cs  (MODIFY — add MoveTo, Stop)
    CharacterMovement.cs   (MODIFY — agent mode + DesiredToDirection)
    Character.cs           (MODIFY — MoveTo/Stop + jail-stop on CaptureState)
  Match/
    ITeamRoster.cs         (NEW — roster query seam)
    TeamRoster.cs          (NEW — passive roster over Character.CaptureState)
  AI/
    NavMeshMoveProbe.cs    (NEW — tiny dev aid to verify pathing in PlayMode)

Assets/Tests/EditMode/
  BehaviourTreeLeafTests.cs      (NEW)
  SequenceTests.cs               (NEW)
  SelectorTests.cs               (NEW)
  BehaviourTreeIntegrationTests.cs (NEW)
  CharacterMovementMathTests.cs  (MODIFY — add DesiredToDirection cases)
  SpyCharacterMovement.cs        (MODIFY — implement MoveTo/Stop, record them)
  CharacterCommandGatingTests.cs (MODIFY — add MoveTo gating + jail-stop cases)
  TeamRosterTests.cs             (NEW)
  SensorSelectionTests.cs        (NEW)
```

No changes to `GameLifetimeScope` / `PlayerBootstrap` in this plan — DI wiring for spawn/roster/coordinator lands in plan #2 (Phase 6). The existing `PlayerBootstrap` keeps driving the scene player.

---

## PHASE 0 — Behaviour Tree core

### Task 1: BT leaves — NodeStatus, Node, Blackboard, ConditionNode, ActionNode (TDD)

The two leaf node types plus the abstract base and a minimal `Blackboard` (just `Self` for now; it grows in Task 9). The leaves are the smallest testable BT unit.

**Files:**
- Create: `Assets/Scripts/AI/BehaviourTree/NodeStatus.cs`
- Create: `Assets/Scripts/AI/BehaviourTree/Node.cs`
- Create: `Assets/Scripts/AI/BehaviourTree/Blackboard.cs`
- Create: `Assets/Scripts/AI/BehaviourTree/ConditionNode.cs`
- Create: `Assets/Scripts/AI/BehaviourTree/ActionNode.cs`
- Test: `Assets/Tests/EditMode/BehaviourTreeLeafTests.cs`

**Interfaces:**
- Consumes: `Character` (existing, `ChaseGame.Characters`).
- Produces:
  - `enum NodeStatus { Success, Failure, Running }`
  - `abstract class Node { public abstract NodeStatus Tick(Blackboard bb); }`
  - `class Blackboard { public Character Self; }` (grown in Task 9)
  - `sealed class ConditionNode : Node` — ctor `ConditionNode(Func<Blackboard,bool> predicate)`; `Tick` → `predicate(bb) ? Success : Failure`
  - `sealed class ActionNode : Node` — ctor `ActionNode(Func<Blackboard,NodeStatus> action)`; `Tick` → `action(bb)`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/BehaviourTreeLeafTests.cs`:

```csharp
using System;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class BehaviourTreeLeafTests
    {
        [Test]
        public void ConditionNode_TruePredicate_ReturnsSuccess()
        {
            var node = new ConditionNode(_ => true);
            Assert.AreEqual(NodeStatus.Success, node.Tick(new Blackboard()));
        }

        [Test]
        public void ConditionNode_FalsePredicate_ReturnsFailure()
        {
            var node = new ConditionNode(_ => false);
            Assert.AreEqual(NodeStatus.Failure, node.Tick(new Blackboard()));
        }

        [Test]
        public void ActionNode_ReturnsDelegateStatus()
        {
            var node = new ActionNode(_ => NodeStatus.Running);
            Assert.AreEqual(NodeStatus.Running, node.Tick(new Blackboard()));
        }

        [Test]
        public void ActionNode_ReceivesBlackboard()
        {
            Blackboard seen = null;
            var bb = new Blackboard();
            var node = new ActionNode(b => { seen = b; return NodeStatus.Success; });
            node.Tick(bb);
            Assert.AreSame(bb, seen);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: Test Runner ▸ EditMode ▸ `BehaviourTreeLeafTests` (or `mcp__UnityMCP__run_tests` mode=EditMode).
Expected: FAILS to compile — `NodeStatus`, `Blackboard`, `ConditionNode`, `ActionNode` do not exist.

- [ ] **Step 3: Write the implementations**

`Assets/Scripts/AI/BehaviourTree/NodeStatus.cs`:

```csharp
namespace ChaseGame.AI.BehaviourTree
{
    public enum NodeStatus
    {
        Success,
        Failure,
        Running
    }
}
```

`Assets/Scripts/AI/BehaviourTree/Node.cs`:

```csharp
namespace ChaseGame.AI.BehaviourTree
{
    public abstract class Node
    {
        public abstract NodeStatus Tick(Blackboard bb);
    }
}
```

`Assets/Scripts/AI/BehaviourTree/Blackboard.cs`:

```csharp
using ChaseGame.Characters;

namespace ChaseGame.AI.BehaviourTree
{
    // Shared data bag passed to every node. Grows as later tasks add perception
    // and config fields; the core node types never read these directly — only the
    // delegates authored in a brain's BuildTree() do.
    public class Blackboard
    {
        public Character Self;
    }
}
```

`Assets/Scripts/AI/BehaviourTree/ConditionNode.cs`:

```csharp
using System;

namespace ChaseGame.AI.BehaviourTree
{
    public sealed class ConditionNode : Node
    {
        private readonly Func<Blackboard, bool> predicate;

        public ConditionNode(Func<Blackboard, bool> predicate)
        {
            this.predicate = predicate;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            return predicate(bb) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}
```

`Assets/Scripts/AI/BehaviourTree/ActionNode.cs`:

```csharp
using System;

namespace ChaseGame.AI.BehaviourTree
{
    public sealed class ActionNode : Node
    {
        private readonly Func<Blackboard, NodeStatus> action;

        public ActionNode(Func<Blackboard, NodeStatus> action)
        {
            this.action = action;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            return action(bb);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Expected: all four PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AI/BehaviourTree Assets/Tests/EditMode/BehaviourTreeLeafTests.cs
git commit -m "feat(ai): add behaviour-tree leaf nodes (NodeStatus, Node, Condition, Action) + Blackboard"
```

---

### Task 2: Sequence composite (TDD)

`Sequence` = AND with short-circuit: run children in order; the first non-`Success` result is returned (stopping the walk); if all children succeed, return `Success`. An empty sequence returns `Success`.

**Files:**
- Create: `Assets/Scripts/AI/BehaviourTree/Sequence.cs`
- Test: `Assets/Tests/EditMode/SequenceTests.cs`

**Interfaces:**
- Consumes: `Node`, `NodeStatus`, `Blackboard` (Task 1).
- Produces: `sealed class Sequence : Node` — ctor `Sequence(params Node[] children)`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/SequenceTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class SequenceTests
    {
        private static ActionNode Record(List<string> log, string name, NodeStatus status)
            => new ActionNode(_ => { log.Add(name); return status; });

        [Test]
        public void Sequence_AllSucceed_ReturnsSuccess_RunsAll()
        {
            var log = new List<string>();
            var seq = new Sequence(
                Record(log, "a", NodeStatus.Success),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Success, seq.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Sequence_FirstFails_ReturnsFailure_ShortCircuits()
        {
            var log = new List<string>();
            var seq = new Sequence(
                Record(log, "a", NodeStatus.Failure),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Failure, seq.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log); // b never ran
        }

        [Test]
        public void Sequence_ChildRunning_ReturnsRunning_ShortCircuits()
        {
            var log = new List<string>();
            var seq = new Sequence(
                Record(log, "a", NodeStatus.Running),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Running, seq.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log);
        }

        [Test]
        public void Sequence_NoChildren_ReturnsSuccess()
        {
            var seq = new Sequence();
            Assert.AreEqual(NodeStatus.Success, seq.Tick(new Blackboard()));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Expected: FAILS to compile — `Sequence` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/AI/BehaviourTree/Sequence.cs`:

```csharp
namespace ChaseGame.AI.BehaviourTree
{
    public sealed class Sequence : Node
    {
        private readonly Node[] children;

        public Sequence(params Node[] children)
        {
            this.children = children;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            foreach (var child in children)
            {
                var status = child.Tick(bb);
                if (status != NodeStatus.Success)
                {
                    return status; // Failure or Running short-circuits
                }
            }

            return NodeStatus.Success;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Expected: all four PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AI/BehaviourTree/Sequence.cs Assets/Tests/EditMode/SequenceTests.cs
git commit -m "feat(ai): add Sequence composite with short-circuit semantics"
```

---

### Task 3: Selector composite (TDD)

`Selector` = priority/fallback with short-circuit: run children in order; the first non-`Failure` result is returned (stopping the walk); if all children fail, return `Failure`. An empty selector returns `Failure`.

**Files:**
- Create: `Assets/Scripts/AI/BehaviourTree/Selector.cs`
- Test: `Assets/Tests/EditMode/SelectorTests.cs`

**Interfaces:**
- Consumes: `Node`, `NodeStatus`, `Blackboard` (Task 1).
- Produces: `sealed class Selector : Node` — ctor `Selector(params Node[] children)`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/SelectorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class SelectorTests
    {
        private static ActionNode Record(List<string> log, string name, NodeStatus status)
            => new ActionNode(_ => { log.Add(name); return status; });

        [Test]
        public void Selector_FirstSucceeds_ReturnsSuccess_ShortCircuits()
        {
            var log = new List<string>();
            var sel = new Selector(
                Record(log, "a", NodeStatus.Success),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Success, sel.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log); // b never ran
        }

        [Test]
        public void Selector_FirstFails_TriesNext()
        {
            var log = new List<string>();
            var sel = new Selector(
                Record(log, "a", NodeStatus.Failure),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Success, sel.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Selector_ChildRunning_ReturnsRunning_ShortCircuits()
        {
            var log = new List<string>();
            var sel = new Selector(
                Record(log, "a", NodeStatus.Running),
                Record(log, "b", NodeStatus.Success));

            Assert.AreEqual(NodeStatus.Running, sel.Tick(new Blackboard()));
            CollectionAssert.AreEqual(new[] { "a" }, log);
        }

        [Test]
        public void Selector_AllFail_ReturnsFailure()
        {
            var sel = new Selector(
                new ActionNode(_ => NodeStatus.Failure),
                new ActionNode(_ => NodeStatus.Failure));

            Assert.AreEqual(NodeStatus.Failure, sel.Tick(new Blackboard()));
        }

        [Test]
        public void Selector_NoChildren_ReturnsFailure()
        {
            var sel = new Selector();
            Assert.AreEqual(NodeStatus.Failure, sel.Tick(new Blackboard()));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Expected: FAILS to compile — `Selector` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/AI/BehaviourTree/Selector.cs`:

```csharp
namespace ChaseGame.AI.BehaviourTree
{
    public sealed class Selector : Node
    {
        private readonly Node[] children;

        public Selector(params Node[] children)
        {
            this.children = children;
        }

        public override NodeStatus Tick(Blackboard bb)
        {
            foreach (var child in children)
            {
                var status = child.Tick(bb);
                if (status != NodeStatus.Failure)
                {
                    return status; // Success or Running short-circuits
                }
            }

            return NodeStatus.Failure;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Expected: all five PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AI/BehaviourTree/Selector.cs Assets/Tests/EditMode/SelectorTests.cs
git commit -m "feat(ai): add Selector (priority/fallback) composite"
```

---

### Task 4: Composed-tree integration test (TDD, no new production code)

Proves the delegate-authoring style works end to end: a priority tree of `Selector`/`Sequence`/`Condition`/`Action` picks the right branch based on blackboard-derived state, exactly as the brains will author trees in plan #2. This is a test-only task pinning the composition contract.

**Files:**
- Test: `Assets/Tests/EditMode/BehaviourTreeIntegrationTests.cs`

**Interfaces:**
- Consumes: all Task 1–3 types.
- Produces: nothing (test only).

- [ ] **Step 1: Write the test**

`Assets/Tests/EditMode/BehaviourTreeIntegrationTests.cs`:

```csharp
using NUnit.Framework;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class BehaviourTreeIntegrationTests
    {
        // A tiny scratch bag so the test does not depend on Blackboard's real fields.
        private class Flags
        {
            public bool HighPriorityReady;
            public string LastAction;
        }

        private static Node BuildTree(Flags flags)
        {
            return new Selector(
                new Sequence(
                    new ConditionNode(_ => flags.HighPriorityReady),
                    new ActionNode(_ => { flags.LastAction = "high"; return NodeStatus.Success; })),
                new ActionNode(_ => { flags.LastAction = "fallback"; return NodeStatus.Running; }));
        }

        [Test]
        public void Tree_TakesHighPriorityBranch_WhenConditionTrue()
        {
            var flags = new Flags { HighPriorityReady = true };
            var tree = BuildTree(flags);

            Assert.AreEqual(NodeStatus.Success, tree.Tick(new Blackboard()));
            Assert.AreEqual("high", flags.LastAction);
        }

        [Test]
        public void Tree_FallsThroughToWander_WhenConditionFalse()
        {
            var flags = new Flags { HighPriorityReady = false };
            var tree = BuildTree(flags);

            Assert.AreEqual(NodeStatus.Running, tree.Tick(new Blackboard()));
            Assert.AreEqual("fallback", flags.LastAction);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it passes**

Expected: both PASS immediately (all types already exist). This is a characterization test — no red step needed.

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/EditMode/BehaviourTreeIntegrationTests.cs
git commit -m "test(ai): pin behaviour-tree delegate-authoring composition"
```

---

## PHASE 1 — Hybrid NavMesh movement

### Task 5: Grow the movement seam — MoveTo/Stop + agent mode (TDD)

Add `MoveTo`/`Stop` to `ICharacterMovement`, a pure `DesiredToDirection` helper (TDD), the agent-mode implementation in `CharacterMovement`, and update the `SpyCharacterMovement` double so everything compiles.

**Files:**
- Modify: `Assets/Scripts/Characters/ICharacterMovement.cs`
- Modify: `Assets/Scripts/Characters/CharacterMovement.cs`
- Modify: `Assets/Tests/EditMode/SpyCharacterMovement.cs`
- Modify: `Assets/Tests/EditMode/CharacterMovementMathTests.cs`

**Interfaces:**
- Consumes: `CharacterStats` (existing).
- Produces:
  - `interface ICharacterMovement { void SetMoveDirection(Vector3 direction); void MoveTo(Vector3 destination); void Stop(); }`
  - `CharacterMovement.DesiredToDirection(Vector3 desiredVelocity) : Vector3` (static; zero when `sqrMagnitude < 1e-6f`, else `normalized`)
  - `CharacterMovement` agent mode: `MoveTo` → `agent.SetDestination`; `Stop` → zero direction + `agent.ResetPath` (on NavMesh); `Update` derives direction from `agent.desiredVelocity` in Agent mode and syncs `agent.nextPosition`.
  - `SpyCharacterMovement` records `MoveToCallCount`, `LastDestination`, `StopCallCount` in addition to the existing `CallCount`/`LastDirection`.

- [ ] **Step 1: Update the test double first (so the suite compiles)**

Replace `Assets/Tests/EditMode/SpyCharacterMovement.cs`:

```csharp
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class SpyCharacterMovement : ICharacterMovement
    {
        public int CallCount { get; private set; }
        public Vector3 LastDirection { get; private set; }

        public int MoveToCallCount { get; private set; }
        public Vector3 LastDestination { get; private set; }

        public int StopCallCount { get; private set; }

        public void SetMoveDirection(Vector3 direction)
        {
            CallCount++;
            LastDirection = direction;
        }

        public void MoveTo(Vector3 destination)
        {
            MoveToCallCount++;
            LastDestination = destination;
        }

        public void Stop()
        {
            StopCallCount++;
        }
    }
}
```

- [ ] **Step 2: Write the failing test for `DesiredToDirection`**

Append to `Assets/Tests/EditMode/CharacterMovementMathTests.cs` (inside the existing `CharacterMovementMathTests` class):

```csharp
        [Test]
        public void DesiredToDirection_NearZeroVelocity_IsZero()
        {
            var dir = CharacterMovement.DesiredToDirection(new Vector3(1e-4f, 0f, 0f));
            Assert.AreEqual(Vector3.zero, dir);
        }

        [Test]
        public void DesiredToDirection_NonZero_IsUnitLength()
        {
            var dir = CharacterMovement.DesiredToDirection(new Vector3(0f, 0f, 4f));
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);
            Assert.AreEqual(1f, dir.z, 1e-4f);
        }
```

- [ ] **Step 3: Run the test to verify it fails**

Expected: FAILS to compile — `DesiredToDirection` and the new interface members don't exist yet.

- [ ] **Step 4: Grow the interface**

Replace `Assets/Scripts/Characters/ICharacterMovement.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Characters
{
    public interface ICharacterMovement
    {
        // Player / direct control.
        void SetMoveDirection(Vector3 direction);

        // AI / NavMesh control: path toward a world destination.
        void MoveTo(Vector3 destination);

        // Zero all motion (also used when a character is jailed).
        void Stop();
    }
}
```

- [ ] **Step 5: Implement agent mode in `CharacterMovement`**

Replace `Assets/Scripts/Characters/CharacterMovement.cs`:

```csharp
using UnityEngine;
using UnityEngine.AI;

namespace ChaseGame.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, ICharacterMovement
    {
        private enum Mode { Direction, Agent }

        [SerializeField] private CharacterStats stats;

        private CharacterController controller;
        private NavMeshAgent agent;      // optional: present on AI-capable prefabs
        private Vector3 moveDirection;
        private Mode mode = Mode.Direction;

        public static Vector3 ComputeVelocity(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            return direction.normalized * speed;
        }

        // Turn a NavMeshAgent desiredVelocity into a unit move direction (or zero
        // when the agent has essentially arrived, so the character does not jitter).
        public static Vector3 DesiredToDirection(Vector3 desiredVelocity)
        {
            if (desiredVelocity.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            return desiredVelocity.normalized;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                // The agent only computes the path; the CharacterController does the moving.
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.speed = stats != null ? stats.MoveSpeed : 5f;
            }
        }

        public void SetMoveDirection(Vector3 direction)
        {
            mode = Mode.Direction;
            moveDirection = direction;
        }

        public void MoveTo(Vector3 destination)
        {
            if (agent == null)
            {
                return; // no NavMeshAgent on this prefab; MoveTo is a no-op
            }

            mode = Mode.Agent;
            agent.SetDestination(destination);
        }

        public void Stop()
        {
            mode = Mode.Direction;
            moveDirection = Vector3.zero;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private void Update()
        {
            if (mode == Mode.Agent && agent != null)
            {
                moveDirection = DesiredToDirection(agent.desiredVelocity);
            }

            float speed = stats != null ? stats.MoveSpeed : 5f;
            Vector3 velocity = ComputeVelocity(moveDirection, speed);
            velocity += Physics.gravity; // simple gravity keeps the controller grounded
            controller.Move(velocity * Time.deltaTime);

            // Keep the (non-moving) agent in step with where the controller actually is,
            // so path steering stays correct.
            if (agent != null && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run all EditMode tests. Expected: the two new `DesiredToDirection` cases PASS, and **all pre-existing tests stay green** (the spy now implements the wider interface; `ComputeVelocity` behaviour is unchanged).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Characters/ICharacterMovement.cs Assets/Scripts/Characters/CharacterMovement.cs Assets/Tests/EditMode/SpyCharacterMovement.cs Assets/Tests/EditMode/CharacterMovementMathTests.cs
git commit -m "feat(movement): hybrid NavMesh agent mode + MoveTo/Stop on the movement seam"
```

---

### Task 6: Character command API — MoveTo/Stop + jail-stop (TDD)

`Character` gains `MoveTo`/`Stop` (no-op when Jailed, like `Move`), and entering `Jailed` now calls `movement.Stop()` so a character jailed mid-move doesn't slide (closes the spec §5 / foundation §8 follow-up).

**Files:**
- Modify: `Assets/Scripts/Characters/Character.cs`
- Modify: `Assets/Tests/EditMode/CharacterCommandGatingTests.cs`

**Interfaces:**
- Consumes: `ICharacterMovement` (Task 5), `SpyCharacterMovement` (Task 5).
- Produces: on `Character`:
  - `void MoveTo(Vector3 destination)` — forwards to `movement.MoveTo` when `Free`, no-op when `Jailed`
  - `void Stop()` — forwards to `movement.Stop()`
  - `CaptureState` setter calls `movement.Stop()` exactly once on the `Free`→`Jailed` transition

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/CharacterCommandGatingTests.cs` (inside the class):

```csharp
        [Test]
        public void MoveTo_WhenFree_ForwardsToMovement()
        {
            character.CaptureState = CaptureState.Free;
            character.MoveTo(new Vector3(3f, 0f, 4f));

            Assert.AreEqual(1, spy.MoveToCallCount);
            Assert.AreEqual(new Vector3(3f, 0f, 4f), spy.LastDestination);
        }

        [Test]
        public void MoveTo_WhenJailed_IsNoOp()
        {
            character.CaptureState = CaptureState.Jailed;
            character.MoveTo(new Vector3(3f, 0f, 4f));

            Assert.AreEqual(0, spy.MoveToCallCount);
        }

        [Test]
        public void EnteringJail_StopsMovementOnce()
        {
            character.CaptureState = CaptureState.Free;
            character.CaptureState = CaptureState.Jailed;
            Assert.AreEqual(1, spy.StopCallCount);

            // Re-asserting Jailed does not Stop again.
            character.CaptureState = CaptureState.Jailed;
            Assert.AreEqual(1, spy.StopCallCount);
        }

        [Test]
        public void ReleasingAndReJailing_StopsAgain()
        {
            character.CaptureState = CaptureState.Jailed;
            character.CaptureState = CaptureState.Free;
            character.CaptureState = CaptureState.Jailed;
            Assert.AreEqual(2, spy.StopCallCount);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: FAILS to compile (`MoveTo` missing) / assertion failures (`StopCallCount`).

- [ ] **Step 3: Update `Character`**

Replace `Assets/Scripts/Characters/Character.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.Characters
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Chaser;

        private ICharacterMovement movement;
        private CaptureState captureState = CaptureState.Free;

        public Team Team => team;

        public CaptureState CaptureState
        {
            get => captureState;
            set
            {
                bool enteringJail = value == CaptureState.Jailed && captureState != CaptureState.Jailed;
                captureState = value;
                if (enteringJail)
                {
                    movement?.Stop(); // don't slide while jailed (spec §5)
                }
            }
        }

        private void Awake()
        {
            // Resolve the sibling movement component if a test hasn't injected one.
            movement ??= GetComponent<ICharacterMovement>();
        }

        public void Move(Vector3 direction)
        {
            if (captureState == CaptureState.Jailed)
            {
                return;
            }

            movement?.SetMoveDirection(direction);
        }

        public void MoveTo(Vector3 destination)
        {
            if (captureState == CaptureState.Jailed)
            {
                return;
            }

            movement?.MoveTo(destination);
        }

        public void Stop()
        {
            movement?.Stop();
        }

        // Test seam: inject a movement double without a CharacterController.
        public void SetMovementForTests(ICharacterMovement injected)
        {
            movement = injected;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Expected: the four new cases PASS and the three existing gating cases stay green (`Move_WhenJailed_IsNoOp` still sees `spy.CallCount == 0` — `Stop` increments `StopCallCount`, not `CallCount`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Characters/Character.cs Assets/Tests/EditMode/CharacterCommandGatingTests.cs
git commit -m "feat(character): MoveTo/Stop command API + stop-on-jail"
```

---

### Task 7 (HUMAN, Editor + dev probe): NavMesh bake + agent on prefab + PlayMode path-follow check

Wires the hybrid movement into the scene: bake a NavMesh, add a `NavMeshAgent` to the character, and verify with a tiny probe that `Character.MoveTo` walks the character along the NavMesh — and that the player's WASD movement still works (regression).

**Files:**
- Create: `Assets/Scripts/AI/NavMeshMoveProbe.cs`

**Interfaces:**
- Consumes: `Character.MoveTo` (Task 6).
- Produces: `NavMeshMoveProbe` (a dev aid; not consumed by later tasks).

- [ ] **Step 1: Write the dev probe**

`Assets/Scripts/AI/NavMeshMoveProbe.cs`:

```csharp
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.AI
{
    // Dev aid: on Play, tells the sibling Character to path to a target transform,
    // so the hybrid NavMesh movement can be verified before any AI brain exists.
    // Remove or disable once brains drive MoveTo (plan #2).
    [RequireComponent(typeof(Character))]
    public class NavMeshMoveProbe : MonoBehaviour
    {
        [SerializeField] private Transform destination;

        private Character character;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        private void Start()
        {
            if (destination != null)
            {
                character.MoveTo(destination.position);
            }
        }
    }
}
```

- [ ] **Step 2 (HUMAN, Editor): Add the AI Navigation baking + a NavMeshAgent**

1. Ensure `com.unity.ai.navigation` is installed (it is, per `Packages/manifest.json`). Open Window ▸ AI ▸ Navigation (or add a `NavMeshSurface` to the ground object).
2. In `Assets/Scenes/Game.unity`, make sure there is a walkable ground object (the foundation slice's Plane). Bake a NavMesh over it (Navigation window ▸ Bake, or `NavMeshSurface.BuildNavMesh`). Confirm a blue NavMesh overlay covers the ground.
3. Select the `ChaserCharacter` prefab (`Assets/Prefabs/ChaserCharacter.prefab`), Add Component ▸ `Nav Mesh Agent`. Set its Radius/Height to roughly match the `CharacterController` (radius ≈ 0.35, height ≈ 1). Leave speed as-is (the code overrides it from `CharacterStats` in `Awake`).

- [ ] **Step 3 (HUMAN, Editor): Verify path-follow with the probe**

1. In the Game scene, temporarily add an empty GameObject `MoveTarget` somewhere reachable on the plane.
2. On the scene's character, temporarily **disable** `PlayerBootstrap` wiring is not needed — instead just add the `NavMeshMoveProbe` component to the character and drag `MoveTarget` into its Destination field. (Leave the `GameLifetimeScope`/`PlayerBootstrap` in place; the probe's `Start` fires alongside.)
3. Press Play. Expected: the character walks along the NavMesh to `MoveTarget` and stops there. No console errors.

- [ ] **Step 4 (HUMAN, Editor): Regression — player still moves**

1. Remove/disable the `NavMeshMoveProbe` (and `MoveTarget`).
2. Press Play. Expected: WASD / arrows / on-screen joystick still move the character exactly as before (Direction mode is the default; agent mode only engages on `MoveTo`).

If either check fails, debug before committing (superpowers:systematic-debugging).

- [ ] **Step 5: Commit**

```bash
git status   # see what the Editor changed: NavMesh asset, prefab, scene, .meta files
git add Assets/Scripts/AI/NavMeshMoveProbe.cs
git add Assets/Prefabs Assets/Scenes Assets/Settings   # stage the real NavMesh/prefab/scene changes shown by git status
git commit -m "feat(movement): bake NavMesh + agent on prefab; verify hybrid path-follow"
```

---

## PHASE 2 — Perception + roster

### Task 8: TeamRoster (TDD)

A passive roster whose source of truth for Free/Jailed is `Character.CaptureState`. Chasers/Runners are split on `Team`. Queries filter; the roster raises no events.

**Files:**
- Create: `Assets/Scripts/Match/ITeamRoster.cs`
- Create: `Assets/Scripts/Match/TeamRoster.cs`
- Test: `Assets/Tests/EditMode/TeamRosterTests.cs`

**Interfaces:**
- Consumes: `Character`, `Team`, `CaptureState` (existing).
- Produces:
  - `interface ITeamRoster` with `IReadOnlyList<Character> Chasers { get; }`, `IReadOnlyList<Character> Runners { get; }`, `Character PlayerCharacter { get; set; }`, `void Add(Character c)`, `IReadOnlyList<Character> GetFreeRunners()`, `IReadOnlyList<Character> GetJailedRunners()`, `bool AllRunnersJailed()`
  - `class TeamRoster : ITeamRoster`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/TeamRosterTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class TeamRosterTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private Character NewCharacter(Team team, CaptureState state = CaptureState.Free)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var c = go.AddComponent<Character>();
            // Team is a serialized private field; set it via the SerializedObject-free
            // reflection helper used only in tests would be overkill — instead the
            // prefab sets Team. For the roster we route by the public Team getter, so
            // create the component and rely on its serialized default, overriding via
            // a dedicated test hook.
            c.SetTeamForTests(team);
            c.CaptureState = state;
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void Add_RoutesByTeam()
        {
            var roster = new TeamRoster();
            var chaser = NewCharacter(Team.Chaser);
            var runner = NewCharacter(Team.Runner);

            roster.Add(chaser);
            roster.Add(runner);

            CollectionAssert.Contains(roster.Chasers, chaser);
            CollectionAssert.Contains(roster.Runners, runner);
            Assert.AreEqual(1, roster.Chasers.Count);
            Assert.AreEqual(1, roster.Runners.Count);
        }

        [Test]
        public void GetFreeRunners_ExcludesJailed()
        {
            var roster = new TeamRoster();
            var free = NewCharacter(Team.Runner, CaptureState.Free);
            var jailed = NewCharacter(Team.Runner, CaptureState.Jailed);
            roster.Add(free);
            roster.Add(jailed);

            CollectionAssert.AreEquivalent(new[] { free }, roster.GetFreeRunners());
            CollectionAssert.AreEquivalent(new[] { jailed }, roster.GetJailedRunners());
        }

        [Test]
        public void AllRunnersJailed_TrueOnlyWhenEveryRunnerJailed()
        {
            var roster = new TeamRoster();
            var r1 = NewCharacter(Team.Runner, CaptureState.Jailed);
            var r2 = NewCharacter(Team.Runner, CaptureState.Jailed);
            roster.Add(r1);
            roster.Add(r2);
            Assert.IsTrue(roster.AllRunnersJailed());

            r2.CaptureState = CaptureState.Free;
            Assert.IsFalse(roster.AllRunnersJailed());
        }

        [Test]
        public void AllRunnersJailed_FalseWhenNoRunners()
        {
            var roster = new TeamRoster();
            roster.Add(NewCharacter(Team.Chaser));
            Assert.IsFalse(roster.AllRunnersJailed());
        }
    }
}
```

> This test uses `Character.SetTeamForTests(Team)` — a test-only seam added in Step 3 below, mirroring the existing `SetMovementForTests` pattern (the real `Team` is set on the prefab in the Editor).

- [ ] **Step 2: Run the test to verify it fails**

Expected: FAILS to compile — `ChaseGame.Match`, `ITeamRoster`, `TeamRoster`, and `Character.SetTeamForTests` don't exist.

- [ ] **Step 3: Add the `SetTeamForTests` seam on `Character`**

Add this method to `Assets/Scripts/Characters/Character.cs` (next to `SetMovementForTests`):

```csharp
        // Test seam: set the team without a prefab/SerializedObject.
        public void SetTeamForTests(Team value)
        {
            team = value;
        }
```

- [ ] **Step 4: Write the roster interface**

`Assets/Scripts/Match/ITeamRoster.cs`:

```csharp
using System.Collections.Generic;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    public interface ITeamRoster
    {
        IReadOnlyList<Character> Chasers { get; }
        IReadOnlyList<Character> Runners { get; }
        Character PlayerCharacter { get; set; }

        void Add(Character character);

        IReadOnlyList<Character> GetFreeRunners();
        IReadOnlyList<Character> GetJailedRunners();
        bool AllRunnersJailed();
    }
}
```

- [ ] **Step 5: Write the roster implementation**

`Assets/Scripts/Match/TeamRoster.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Passive: the source of truth for Free/Jailed is Character.CaptureState.
    // The roster only stores membership and filters on demand; it raises no events.
    public class TeamRoster : ITeamRoster
    {
        private readonly List<Character> chasers = new List<Character>();
        private readonly List<Character> runners = new List<Character>();

        public IReadOnlyList<Character> Chasers => chasers;
        public IReadOnlyList<Character> Runners => runners;
        public Character PlayerCharacter { get; set; }

        public void Add(Character character)
        {
            if (character.Team == Team.Chaser)
            {
                chasers.Add(character);
            }
            else
            {
                runners.Add(character);
            }
        }

        public IReadOnlyList<Character> GetFreeRunners()
            => runners.Where(r => r.CaptureState == CaptureState.Free).ToList();

        public IReadOnlyList<Character> GetJailedRunners()
            => runners.Where(r => r.CaptureState == CaptureState.Jailed).ToList();

        public bool AllRunnersJailed()
            => runners.Count > 0 && runners.All(r => r.CaptureState == CaptureState.Jailed);
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Expected: all four PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Match Assets/Scripts/Characters/Character.cs Assets/Tests/EditMode/TeamRosterTests.cs
git commit -m "feat(match): passive TeamRoster over Character.CaptureState"
```

---

### Task 9: AIConfig SO + grow the Blackboard

Adds the AI tunables ScriptableObject and grows the `Blackboard` with the perception/config fields the `Sensor` and (plan #2) brains read. Small; the data types are exercised by Task 10's tests, so this task ends on a compile + one round-trip test.

**Files:**
- Create: `Assets/Scripts/AI/AIConfig.cs`
- Modify: `Assets/Scripts/AI/BehaviourTree/Blackboard.cs`
- Test: `Assets/Tests/EditMode/SensorSelectionTests.cs` (created here with one Blackboard round-trip test; grown in Task 10)

**Interfaces:**
- Consumes: `ITeamRoster` (Task 8), `Character`/`Transform`.
- Produces:
  - `class AIConfig : ScriptableObject` with getters `VisionRadius`, `ShootRange`, `ThreatRadius`, `RescueThreatRadius`, `WanderRadius`, `TickRateHz`
  - grown `Blackboard`: `public Character Self; public ITeamRoster Roster; public AIConfig Config; public Transform CurrentTarget; public Transform NearestThreat;`

- [ ] **Step 1: Write the AIConfig ScriptableObject**

`Assets/Scripts/AI/AIConfig.cs`:

```csharp
using UnityEngine;

namespace ChaseGame.AI
{
    [CreateAssetMenu(menuName = "ChaseGame/AI Config", fileName = "AIConfig")]
    public class AIConfig : ScriptableObject
    {
        [SerializeField] private float visionRadius = 12f;
        [SerializeField] private float shootRange = 8f;
        [SerializeField] private float threatRadius = 6f;
        [SerializeField] private float rescueThreatRadius = 5f;
        [SerializeField] private float wanderRadius = 6f;
        [SerializeField] private float tickRateHz = 8f;

        public float VisionRadius => visionRadius;
        public float ShootRange => shootRange;
        public float ThreatRadius => threatRadius;
        public float RescueThreatRadius => rescueThreatRadius;
        public float WanderRadius => wanderRadius;
        public float TickRateHz => tickRateHz;
    }
}
```

- [ ] **Step 2: Grow the Blackboard**

Replace `Assets/Scripts/AI/BehaviourTree/Blackboard.cs`:

```csharp
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.AI.BehaviourTree
{
    // Shared data bag passed to every node. The core node types never read these
    // directly — only the delegates authored in a brain's BuildTree() do.
    public class Blackboard
    {
        public Character Self;
        public ITeamRoster Roster;
        public AIConfig Config;

        public Transform CurrentTarget;  // chaser: nearest visible free runner
        public Transform NearestThreat;  // runner: nearest visible chaser
    }
}
```

> `Blackboard` now lives in `ChaseGame.AI.BehaviourTree` but references `ChaseGame.AI.AIConfig` and `ChaseGame.Match.ITeamRoster`. These are the same assembly (`ChaseGame`), so the `using` directives resolve without an asmdef change. The BT **node** files still don't reference any of this.

- [ ] **Step 3: Write a Blackboard round-trip test**

`Assets/Tests/EditMode/SensorSelectionTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.Tests
{
    public class SensorSelectionTests
    {
        [Test]
        public void Blackboard_HoldsConfigAndTargets()
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            var bb = new Blackboard { Config = config };

            Assert.AreEqual(8f, bb.Config.TickRateHz, 1e-4f); // default from AIConfig
            Assert.IsNull(bb.CurrentTarget);

            Object.DestroyImmediate(config);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Expected: PASS. (No red step: this is a compile-and-round-trip check for the new data types; the meaningful red/green cycles are in Task 10.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AI/AIConfig.cs Assets/Scripts/AI/BehaviourTree/Blackboard.cs Assets/Tests/EditMode/SensorSelectionTests.cs
git commit -m "feat(ai): add AIConfig tunables + grow Blackboard with perception fields"
```

---

### Task 10: Sensor — CandidatesFor + NearestVisible + Sense (TDD)

The perception logic. Two pure static helpers carry all the testable behaviour: `CandidatesFor` (who this agent may target) and `NearestVisible` (closest in radius passing line-of-sight). The `Sense` MonoBehaviour method wires them to the real physics raycast and writes the blackboard; it is exercised in PlayMode, not unit-tested.

**Files:**
- Create: `Assets/Scripts/AI/Sensor.cs`
- Modify: `Assets/Tests/EditMode/SensorSelectionTests.cs`

**Interfaces:**
- Consumes: `Blackboard`, `AIConfig` (Task 9), `ITeamRoster` (Task 8), `Character`/`Team`/`CaptureState`.
- Produces:
  - `readonly struct Candidate { public readonly Transform Transform; public readonly Vector3 Position; public Candidate(Transform, Vector3); }`
  - `static IReadOnlyList<Candidate> Sensor.CandidatesFor(Team selfTeam, ITeamRoster roster)` — chaser → free runners; runner → chasers
  - `static Transform Sensor.NearestVisible(Vector3 from, IReadOnlyList<Candidate> candidates, float radius, Func<Vector3,Vector3,bool> hasLineOfSight)` — nearest candidate within `radius` that passes `hasLineOfSight`, else `null`
  - `void Sensor.Sense(Blackboard bb)` (MonoBehaviour) — builds candidates from `bb.Roster`/`bb.Self.Team`, runs `NearestVisible` with a physics raycast, writes `bb.CurrentTarget` (chaser) or `bb.NearestThreat` (runner)

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/SensorSelectionTests.cs` (add the needed `using`s at the top: `using System.Collections.Generic; using ChaseGame.Characters; using ChaseGame.Match;`):

```csharp
        private readonly List<GameObject> spawned = new List<GameObject>();

        private Transform NewAt(Vector3 pos)
        {
            var go = new GameObject("t");
            go.transform.position = pos;
            spawned.Add(go);
            return go.transform;
        }

        [TearDown]
        public void TearDownObjects()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void NearestVisible_PicksClosestWithinRadius()
        {
            var from = Vector3.zero;
            var near = NewAt(new Vector3(2f, 0f, 0f));
            var far = NewAt(new Vector3(5f, 0f, 0f));
            var cands = new List<Sensor.Candidate>
            {
                new Sensor.Candidate(far, far.position),
                new Sensor.Candidate(near, near.position),
            };

            var picked = Sensor.NearestVisible(from, cands, radius: 10f, hasLineOfSight: (_, __) => true);
            Assert.AreSame(near, picked);
        }

        [Test]
        public void NearestVisible_IgnoresBeyondRadius()
        {
            var from = Vector3.zero;
            var far = NewAt(new Vector3(20f, 0f, 0f));
            var cands = new List<Sensor.Candidate> { new Sensor.Candidate(far, far.position) };

            var picked = Sensor.NearestVisible(from, cands, radius: 10f, hasLineOfSight: (_, __) => true);
            Assert.IsNull(picked);
        }

        [Test]
        public void NearestVisible_SkipsBlockedAndTakesNextVisible()
        {
            var from = Vector3.zero;
            var blockedNear = NewAt(new Vector3(2f, 0f, 0f));
            var visibleFar = NewAt(new Vector3(4f, 0f, 0f));
            var cands = new List<Sensor.Candidate>
            {
                new Sensor.Candidate(blockedNear, blockedNear.position),
                new Sensor.Candidate(visibleFar, visibleFar.position),
            };

            // LoS blocked only for the near one.
            bool Los(Vector3 a, Vector3 target) => target != blockedNear.position;

            var picked = Sensor.NearestVisible(from, cands, radius: 10f, hasLineOfSight: Los);
            Assert.AreSame(visibleFar, picked);
        }

        [Test]
        public void NearestVisible_EmptyList_ReturnsNull()
        {
            var picked = Sensor.NearestVisible(Vector3.zero, new List<Sensor.Candidate>(), 10f, (_, __) => true);
            Assert.IsNull(picked);
        }

        [Test]
        public void CandidatesFor_Chaser_ReturnsOnlyFreeRunners()
        {
            var roster = new TeamRoster();
            var chaser = MakeCharacter(Team.Chaser, CaptureState.Free);
            var freeRunner = MakeCharacter(Team.Runner, CaptureState.Free);
            var jailedRunner = MakeCharacter(Team.Runner, CaptureState.Jailed);
            roster.Add(chaser); roster.Add(freeRunner); roster.Add(jailedRunner);

            var cands = Sensor.CandidatesFor(Team.Chaser, roster);
            Assert.AreEqual(1, cands.Count);
            Assert.AreSame(freeRunner.transform, cands[0].Transform);
        }

        [Test]
        public void CandidatesFor_Runner_ReturnsChasers()
        {
            var roster = new TeamRoster();
            var chaser = MakeCharacter(Team.Chaser, CaptureState.Free);
            var runner = MakeCharacter(Team.Runner, CaptureState.Free);
            roster.Add(chaser); roster.Add(runner);

            var cands = Sensor.CandidatesFor(Team.Runner, roster);
            Assert.AreEqual(1, cands.Count);
            Assert.AreSame(chaser.transform, cands[0].Transform);
        }

        private Character MakeCharacter(Team team, CaptureState state)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var c = go.AddComponent<Character>();
            c.SetTeamForTests(team);
            c.CaptureState = state;
            return c;
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: FAILS to compile — `Sensor`, `Sensor.Candidate`, `Sensor.CandidatesFor`, `Sensor.NearestVisible` don't exist.

- [ ] **Step 3: Write the Sensor**

`Assets/Scripts/AI/Sensor.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI
{
    // Perception: reads the roster, keeps the nearest line-of-sight candidate,
    // and writes it to the blackboard. Ticked by the brain (not every frame).
    public class Sensor : MonoBehaviour
    {
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float eyeHeight = 1f;

        public readonly struct Candidate
        {
            public readonly Transform Transform;
            public readonly Vector3 Position;

            public Candidate(Transform transform, Vector3 position)
            {
                Transform = transform;
                Position = position;
            }
        }

        public static IReadOnlyList<Candidate> CandidatesFor(Team selfTeam, ITeamRoster roster)
        {
            var result = new List<Candidate>();
            var source = selfTeam == Team.Chaser ? roster.GetFreeRunners() : roster.Chasers;
            foreach (var c in source)
            {
                result.Add(new Candidate(c.transform, c.transform.position));
            }

            return result;
        }

        public static Transform NearestVisible(
            Vector3 from,
            IReadOnlyList<Candidate> candidates,
            float radius,
            Func<Vector3, Vector3, bool> hasLineOfSight)
        {
            Transform best = null;
            float bestSqr = radius * radius;

            foreach (var cand in candidates)
            {
                float sqr = (cand.Position - from).sqrMagnitude;
                if (sqr > bestSqr)
                {
                    continue;
                }

                if (!hasLineOfSight(from, cand.Position))
                {
                    continue;
                }

                bestSqr = sqr;
                best = cand.Transform;
            }

            return best;
        }

        public void Sense(Blackboard bb)
        {
            if (bb.Roster == null || bb.Self == null || bb.Config == null)
            {
                return;
            }

            var candidates = CandidatesFor(bb.Self.Team, bb.Roster);
            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            var target = NearestVisible(eye, candidates, bb.Config.VisionRadius, HasLineOfSight);

            if (bb.Self.Team == Team.Chaser)
            {
                bb.CurrentTarget = target;
            }
            else
            {
                bb.NearestThreat = target;
            }
        }

        private bool HasLineOfSight(Vector3 from, Vector3 targetPos)
        {
            Vector3 to = (targetPos + Vector3.up * eyeHeight) - from;
            // Blocked if an obstacle sits between the eye and the target.
            return !Physics.Raycast(from, to.normalized, to.magnitude, obstacleMask);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Expected: all six new cases PASS (plus the Task 9 round-trip). `Sense`/`HasLineOfSight` are not unit-tested (they need scene physics) — they are covered when a brain drives perception in PlayMode in plan #2.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AI/Sensor.cs Assets/Tests/EditMode/SensorSelectionTests.cs
git commit -m "feat(ai): Sensor perception — CandidatesFor + NearestVisible (distance + LoS)"
```

---

## Self-Review

**Spec coverage (Phases 0–2 of §13):**
- **Phase 0 — BT core** (§4): `NodeStatus`, `Node`, `Selector`, `Sequence`, `ConditionNode`, `ActionNode`, `Blackboard`, generic-node+delegate authoring → Tasks 1–4. ✔ (Node-memory decorators intentionally omitted per spec §4.)
- **Phase 1 — hybrid movement** (§5): `ICharacterMovement.MoveTo/Stop`, `CharacterMovement` agent mode (`updatePosition/Rotation` off, `desiredVelocity`→`SetMoveDirection`, `nextPosition` sync), `Character.MoveTo/Stop`, jail-stop, NavMeshAgent on prefab, NavMesh bake → Tasks 5–7. ✔ (`Character.Look` deferred to plan #2 — no consumer until `AimAndShoot`; YAGNI.)
- **Phase 2 — perception + roster** (§6): `AIConfig`, `Blackboard` perception fields, `Sensor` (distance + LoS), `ITeamRoster`/`TeamRoster` → Tasks 8–10. ✔ (`GuardAssignment`/`NearestCage`/`Destination` blackboard fields deferred to plan #2 where the coordinator/cages/wander first read them; YAGNI.)
- **Explicitly out of this plan (→ plan #2, Phases 3–6):** `AIBrain` host, `ChaserAIBrain`/`RunnerAIBrain` + trees, abilities (`Gun`/`SpeedBoost`/`Decoy`), `ICaptureService` stub + `CageStub`, `CharacterFactory`/`SpawnManager` (replacing `PlayerBootstrap`), `ChaserCoordinator`, and the `GameLifetimeScope` re-wiring + camera retarget. No autonomous NPC is produced by this plan by design.

**Placeholder scan:** No TBD/TODO; every code and test step has full source. ✔

**Type consistency:** `Node.Tick(Blackboard)`, `NodeStatus{Success,Failure,Running}`, `ConditionNode(Func<Blackboard,bool>)`, `ActionNode(Func<Blackboard,NodeStatus>)`, `Selector(params Node[])`, `Sequence(params Node[])` are used identically in Tasks 1–4. `ICharacterMovement{SetMoveDirection,MoveTo,Stop}` matches across `CharacterMovement`, `SpyCharacterMovement`, and `Character` (Tasks 5–6). `Character.SetTeamForTests(Team)` is introduced in Task 8 and reused in Task 10. `ITeamRoster` members (`Chasers`, `Runners`, `GetFreeRunners`, `AllRunnersJailed`, `Add`) used in Tasks 8 and 10 match. `Sensor.Candidate(Transform,Vector3)`, `Sensor.CandidatesFor(Team,ITeamRoster)`, `Sensor.NearestVisible(Vector3,IReadOnlyList<Candidate>,float,Func<Vector3,Vector3,bool>)` match between Task 10's tests and implementation. `Blackboard` fields (`Self`,`Roster`,`Config`,`CurrentTarget`,`NearestThreat`) are consistent Task 9 → Task 10. ✔

**Review Focus coverage:**
- Empty composites → `SequenceTests.Sequence_NoChildren_ReturnsSuccess` (Task 2) + `SelectorTests.Selector_NoChildren_ReturnsFailure` (Task 3). ✔
- Commands while Jailed → `CharacterCommandGatingTests.MoveTo_WhenJailed_IsNoOp` (Task 6). ✔
- Arrival jitter → `CharacterMovementMathTests.DesiredToDirection_NearZeroVelocity_IsZero` (Task 5). ✔
- No target found → `SensorSelectionTests.NearestVisible_EmptyList_ReturnsNull` + `NearestVisible_IgnoresBeyondRadius` + `NearestVisible_SkipsBlockedAndTakesNextVisible` (Task 10). ✔
- All-runners-jailed on zero runners → `TeamRosterTests.AllRunnersJailed_FalseWhenNoRunners` (Task 8). ✔

**Note on TDD in Unity:** the pure logic (BT core, movement math, roster queries, perception selection) is unit-tested red/green. MonoBehaviour/physics/NavMesh behaviour (`CharacterMovement.Update` agent stepping, `Sensor.Sense` raycasts) can't be meaningfully unit-tested here and is validated by the Task 7 PlayMode checklist now and by plan #2's brain PlayMode milestones later — a deliberate scope choice.
