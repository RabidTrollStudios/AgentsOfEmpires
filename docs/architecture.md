# Architecture

This document explains how Agents of Empires is put together: the "one engine, two front-ends"
design, the canonical tick sequence that makes it deterministic, and the parity system that
enforces that determinism.

If you're only writing agents, you don't need this — see
[agent-authoring.md](agent-authoring.md). This is for people modifying the platform.

---

## The core idea: one simulation, two runtimes

The game logic exists **once**, in an engine-agnostic library (`AgentSDK`). Two different
programs drive that same logic:

```
                         ┌──────────────────────────---─┐
                         │        AgentSDK              │
                         │  (shared, deterministic)     │
                         │                              │
                         │  • map generation            │
                         │  • pathfinding               │
                         │  • command validation        │
                         │    & processing              │
                         │  • unit task advancement     │
                         │  • TickSequence              │
                         │  • IPlanningAgent /          │
                         │    IGameState / IAgentActions│
                         └─────────────┬─────────────--─┘
                                       │ (both depend on it)
                   ┌───────────────────┴─────────────────-──┐
                   │                                        │
        ┌──────────▼───────────┐               ┌────────────▼───────────┐
        │  AgentTestHarness    │               │   Unity project (RTS)  │
        │      (SimGame)       │               │                        │
        │  headless engine     │               │  visualization + input │
        │  fast, CI-friendly   │               │  consumes AgentSDK.dll │
        └──────────────────────┘               └────────────────────────┘
```

- **`AgentSDK`** owns every rule that must be deterministic. It has **no Unity dependency** and
  compiles as a plain .NET library.
- **`AgentTestHarness` (`SimGame`)** is the headless engine. It holds the authoritative game
  state and drives the shared logic. This is what runs in tests and CI.
- **The Unity project (`RTS`)** is a front-end for visualization and input. It consumes
  `AgentSDK` as a compiled plugin (`RTS/Assets/Plugins/AgentSDK.dll`) and drives the *same*
  shared logic.

Because both runtimes call into the same SDK, a match plays out identically whether you watch it
in Unity or run it headless — provided the shared tick order is identical. That ordering is the
`TickSequence`.

---

## The canonical tick sequence

Determinism hinges on both engines executing exactly the same phases, in exactly the same order,
once per tick. That order is defined in **one place** — `AgentSDK/TickSequence.cs` — and both
engines call it:

```csharp
public static void RunOneTick(ITickParticipant engine, int currentTick)
{
    engine.RecordSnapshot(currentTick);   // Phase 0: pre-processing snapshot
    engine.ProcessQueuedCommands();       // Phase 1
    engine.AdvanceUnits();                // Phase 2
    engine.RunAgentUpdates();             // Phase 3: queue commands for next tick
}
```

Each engine implements the four phases via `ITickParticipant`:

| Phase | Method | What happens |
|-------|--------|--------------|
| 0 | `RecordSnapshot(tick)` | Record/observe the **pre-processing** state for this tick. In Unity, the `ParityExporter` writes a snapshot here; the headless engine has nothing to export. |
| 1 | `ProcessQueuedCommands()` | Apply the commands agents queued during the **previous** tick's `Update`. |
| 2 | `AdvanceUnits()` | Advance all units by one tick — tasks, movement, combat, mana, death — via the shared `TickEngine`. |
| 3 | `RunAgentUpdates()` | Call each agent's `IPlanningAgent.Update`, in agent-number order. Agents observe the post-advance state and **queue** commands for the *next* tick. |

Two consequences of this order matter:

- **Commands are one tick delayed.** An agent issues a command during Phase 3 of tick *N*; it is
  processed in Phase 1 of tick *N+1*. This is true in both engines, so both stay in lockstep.
- **The snapshot is taken before processing.** Tick *N*'s recorded state is the state *before*
  tick *N*'s command processing — the pristine, comparable state. Tick 0 is therefore the
  untouched initial board.

### Why this design exists

Earlier, Unity ran each agent's decision on Unity's **per-frame** `MonoBehaviour.Update` —
decoupled from the fixed-timestep tick where commands were processed. That made the agent's
decision cadence depend on frame rate (a determinism hazard) and put Unity a full agent-cycle out
of phase with the headless engine. Moving the agent decision into an explicit in-tick phase
(`RunAgentUpdates`) — driven by the shared `TickSequence` in both engines — is what removes that
hazard. **The tick, not the frame, is the only clock the simulation obeys.**

---

## The parity system

Parity is the guarantee that Unity and the headless engine produce **byte-for-byte identical
per-tick state**. It is enforced, not assumed.

### How parity is checked

1. **Record.** A match is run and per-tick state snapshots are exported to a
   `ParityState_<timestamp>.csv` file (the `ParityExporter` in Unity, or the `ParityRunner` CLI
   headless). Each row is a tick's full observable state: gold, unit count, and every unit's
   type/owner/position/health/action.
2. **Replay & compare.** `Parity.Tests` reconstructs the same match in the headless `SimGame` —
   same map seed, same agents — runs it forward tick by tick, and compares each tick's state
   against the recorded snapshot.
3. **Report first divergence.** If any field differs, the test reports the **first tick** where
   the two disagree and exactly which field diverged. First-divergence tick is the key debugging
   signal: it tells you *when* determinism broke.

### The parity gate

A `pre-commit` hook (tracked in [`.githooks/`](../.githooks/)) runs the parity suite whenever a
commit touches engine/sim files:

- `UnityRTS/AgentSDK/`
- `UnityRTS/AgentTestHarness/`
- `UnityRTS/RTS/Assets/Scripts/GameManager/`

If parity fails, the commit is blocked. See [../CONTRIBUTING.md](../CONTRIBUTING.md) for setup and
the (rare, intentional) `--no-verify` escape hatch.

### Common causes of parity breaks

- **Duplicated logic.** A rule implemented separately in Unity and the harness instead of shared
  in `AgentSDK`. Fix: move it into the SDK and call it from both.
- **Frame/time coupling.** Any simulation logic that depends on frame rate, `Time.deltaTime`, or
  wall-clock time. The tick is the only permitted clock.
- **Nondeterministic ordering.** Iterating an unordered collection (e.g. a `HashSet` or
  dictionary) where iteration order affects resulting state. Sort before iterating.
- **Floating-point / integer-division drift.** Subtle arithmetic differences that accumulate.
  Keep shared math in the SDK so both engines compute it identically.

---

## Map generation & determinism

Map generation is already fully shared: `AgentSDK/MapGenCore.cs` produces the blocked cells and
starting positions deterministically from a seed. Both engines are thin adapters over it — Unity's
`ProceduralMapGenerator` and the harness's `MapGenerator` both delegate to `MapGenCore`. Given the
same seed and config, both compute identical terrain, spawn positions, and mine positions.

Starting units (pawns, mines) are placed from the shared `MapGenResult`, so both engines begin
from the same board.

---

## Project map

| Project | Role |
|---------|------|
| `AgentSDK` | Shared deterministic core + public agent interfaces. No Unity dependency. |
| `AgentTestHarness` | Headless engine (`SimGame`), builder, state, hashing. |
| `ParityRunner` | CLI that runs scenarios headless and reports divergence. |
| `PlanningAgent` | Reference agent (FSM). |
| `Opponents` | Source for bundled sample agents (one DLL each). |
| `EnemyAgents` | Compiled agent DLLs loaded at runtime. |
| `Gameplay.Tests` / `Opponent.Tests` / `Parity.Tests` | Test suites wired into `UnityRTS.slnx`. |
| `RTS` | Unity visualization front-end. |

---

## See also

- [agent-authoring.md](agent-authoring.md) — writing agents against the SDK
- [building-and-testing.md](building-and-testing.md) — build/test reference
- [../CONTRIBUTING.md](../CONTRIBUTING.md) — workflow and the parity requirement
