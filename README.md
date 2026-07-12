# Agents of Empires

A deterministic real-time-strategy (RTS) platform where two AI agents compete head-to-head
on a tile-based map. Agents are written in C#, compiled to DLLs, and loaded at runtime — they
observe game state and issue commands through a stable SDK, with **no access to the engine
internals**. The same simulation runs both inside Unity (for visualization) and headless (for
fast, reproducible matches and testing), and the two are held to **byte-for-byte per-tick
parity**.

> **Status:** active development. The core engine, SDK, headless harness, and Unity front-end
> are functional. See [Roadmap & known issues](#roadmap--known-issues).

---

## Table of contents

- [What this is](#what-this-is)
- [Repository layout](#repository-layout)
- [Quick start](#quick-start)
- [Writing an agent](#writing-an-agent)
- [The Agent SDK](#the-agent-sdk)
- [Building & testing](#building--testing)
- [Architecture](#architecture)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License](#license)

---

## What this is

Agents of Empires is a competition and research platform, not a hand-played game. Two
`IPlanningAgent` implementations face off; each controls one side of an RTS match and wins by
out-producing and out-fighting the opponent.

- **Units:** `PAWN`, `WARRIOR`, `ARCHER`, `LANCER`, `MONK` (mobile) and `BASE`, `BARRACKS`,
  `ARCHERY`, `TOWER`, `MONASTERY`, plus neutral `MINE`s.
- **Economy:** pawns gather gold from mines and deliver it to a base; gold trains units and
  constructs buildings.
- **Determinism:** the simulation is fully deterministic and tick-based. Given the same map
  seed and the same agents, every run produces identical state — a hard requirement enforced
  by an automated parity suite.
- **Two runtimes, one engine:** gameplay logic lives in a shared, engine-agnostic core
  (`AgentSDK`). Both the Unity project and the headless harness drive that same core through an
  identical [tick sequence](#architecture), so a match looks the same whether you watch it or
  run it in CI.

---

## Repository layout

Everything of substance lives under `UnityRTS/`.

| Path | What it is |
|------|-----------|
| `UnityRTS/AgentSDK/` | The public contract. Interfaces (`IPlanningAgent`, `IGameState`, `IAgentActions`), shared game-logic (map generation, pathfinding, command processing, tick engine), and constants. **Agents depend only on this.** |
| `UnityRTS/PlanningAgent/` | A full reference agent (`PlanningAgent`) implementing a three-phase FSM. Start here to learn the SDK. |
| `UnityRTS/Opponents/` | Source for the bundled sample agents; each `.cs` builds to its own DLL. |
| `UnityRTS/EnemyAgents/` | Compiled agent DLLs, loaded at runtime by both engines. |
| `UnityRTS/AgentTestHarness/` | The headless simulation engine (`SimGame`) — runs matches with no Unity dependency. |
| `UnityRTS/ParityRunner/` | Runs a match in the headless engine and exports per-tick state for parity checking. |
| `UnityRTS/*.Tests/` | Test suites in the solution: `Gameplay.Tests`, `Opponent.Tests`, `Parity.Tests`. |
| `UnityRTS/RTS/` | The Unity project (visualization front-end). Consumes `AgentSDK` as a compiled plugin. |
| `UnityRTS/scripts/` | Build/test helper scripts. |
| `UnityRTS/UnityRTS.slnx` | The .NET solution — builds and tests the entire engine **without Unity**. |
| `docs/` | Extended documentation (architecture, agent-authoring guide, contributing). |

---

## Quick start

### Prerequisites

- **.NET SDK 10.0+** (the engine, SDK, agents, and tests are all standard .NET — no Unity
  required to build or run headless matches).
- **Unity 6000.3.5f2** — only if you want the visual front-end.
- A POSIX shell (Git Bash on Windows) to run the helper scripts, or run the `dotnet` commands
  directly.

### Build and test the engine (no Unity)

```bash
cd UnityRTS
dotnet build UnityRTS.slnx -c Release
dotnet test  UnityRTS.slnx -c Release
```

### Build everything and deploy to Unity

```bash
cd UnityRTS
./build-all.sh            # builds solution + all agents, deploys DLLs to Unity, runs smoke tests
./build-all.sh --skip-tests
```

---

## Writing an agent

An agent is a C# class that implements `IPlanningAgent` (or extends `PlanningAgentBase` for
convenience). It is compiled to a DLL and dropped into `EnemyAgents/`, where both engines load
it by name at runtime.

The contract is four methods:

```csharp
using AgentSDK;

public class MyAgent : IPlanningAgent
{
    // Called once at the start of a match (best-of-N rounds).
    public void InitializeMatch() { }

    // Called once at the start of each round, before any ticks.
    public void InitializeRound(IGameState state) { }

    // Called once per tick. Observe `state`, issue commands via `actions`.
    public void Update(IGameState state, IAgentActions actions) { }

    // Called once at the end of each round (for adaptive agents).
    public void Learn(IGameState state) { }
}
```

Each tick, your `Update` reads the world through `IGameState` and issues commands through
`IAgentActions`. Commands are **queued** during `Update` and applied at the start of the next
tick — identically in both engines.

The fastest way to learn the API is to read [`PlanningAgent`](UnityRTS/PlanningAgent/PlanningAgent.cs),
the reference agent. A step-by-step tutorial lives in
[`docs/agent-authoring.md`](docs/agent-authoring.md).

### Build your agent

```bash
cd UnityRTS
./scripts/build-agent.sh MyAgent      # compiles and deploys MyAgent.dll to EnemyAgents/
```

---

## The Agent SDK

Agents interact with the world through two interfaces only.

**`IGameState` — what you can observe** (read-only):

| Member | Purpose |
|--------|---------|
| `MyAgentNbr`, `EnemyAgentNbr` | Player identity |
| `MyGold`, `EnemyGold` | Gold totals |
| `MapSize`, `MyWins` | Map dimensions, rounds won |
| `GetMyUnits(type)` / `GetEnemyUnits(type)` / `GetAllUnits(type)` | Unit lookup by owner/type (mines are neutral — use `GetAllUnits`) |
| `GetUnit(nbr)` | Full `UnitInfo` for a unit |
| `IsPositionBuildable` / `IsAreaBuildable` / `IsBoundedAreaBuildable` | Buildability checks |
| `GetPathBetween` / `GetPathToUnit` | Pathfinding |
| `GetBuildablePositionsNearUnit` / `FindProspectiveBuildPositions` | Placement candidates |
| `GetFailedCommands()` | Commands that failed during the last tick's processing |

**`IAgentActions` — what you can do** (returns a `CommandResult`):

| Command | Effect |
|---------|--------|
| `Move(unit, target)` | Move a unit toward a grid position |
| `Build(pawn, target, type)` | Have a pawn construct a building |
| `Train(building, type)` | Train a unit at a building |
| `Gather(pawn, mine, base)` | Send a pawn to mine gold and deliver it |
| `Attack(unit, target)` | Attack an enemy unit |
| `Repair(pawn, building)` | Repair a friendly building |
| `Heal(monk, target)` | Heal a friendly unit |
| `Log(message)` | Emit a debug message |

Agents **cannot** reach into the engine, other agents, or global state — the SDK surface above
is the entire contract. This is what makes matches fair and reproducible.

---

## Building & testing

The whole engine builds and tests as a normal .NET solution — Unity is only needed for
visualization.

```bash
cd UnityRTS

# Build
dotnet build UnityRTS.slnx -c Release

# Run all tests
dotnet test UnityRTS.slnx -c Release

# Helper scripts (in UnityRTS/scripts/)
./scripts/build-sdk.sh             # build just the SDK
./scripts/build-all-agents.sh      # rebuild every bundled agent

# Run the parity suite directly:
dotnet test Parity.Tests/Parity.Tests.csproj -c Release
```

**Parity is enforced.** A `pre-commit` hook runs the parity suite whenever engine or
simulation files change, so a change that breaks Unity/headless determinism can't land
silently. See [`docs/architecture.md`](docs/architecture.md) for how parity works and
[`CONTRIBUTING.md`](CONTRIBUTING.md) for the workflow.

---

## Architecture

The design goal is **one simulation, two front-ends, bit-identical results.**

- **`AgentSDK`** holds all game logic that must be deterministic: map generation, pathfinding,
  command validation/processing, unit task advancement, and the canonical tick sequence. It has
  no Unity dependency.
- **`AgentTestHarness` (`SimGame`)** is the headless engine. It owns the game state and drives
  the shared logic.
- **The Unity project (`RTS`)** is a visualization + input front-end. It consumes `AgentSDK` as
  a compiled plugin and drives the *same* shared logic.
- **`TickSequence`** defines the one canonical per-tick phase order —
  `RecordSnapshot → ProcessQueuedCommands → AdvanceUnits → RunAgentUpdates` — that **both**
  engines execute identically. This is the backbone of determinism: the agent decision runs as
  an explicit in-tick phase in both runtimes, never on a frame clock.

A full write-up (data flow, determinism guarantees, the parity system) is in
[`docs/architecture.md`](docs/architecture.md).

---

## Documentation

| Document | For |
|----------|-----|
| [`docs/architecture.md`](docs/architecture.md) | How the engine, SDK, and parity system fit together |
| [`docs/agent-authoring.md`](docs/agent-authoring.md) | Step-by-step guide to writing, building, and testing an agent |
| [`docs/building-and-testing.md`](docs/building-and-testing.md) | Full build/test reference for contributors |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | How to contribute to the platform |
| [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) | Community expectations |

---

## Roadmap & known issues

- Unity/headless parity is aligned through the opening game; a known building-placement
  determinism difference is under active investigation (tracked internally).
- See the issue tracker for the current backlog.

---

## Contributing

Contributions are welcome — see [`CONTRIBUTING.md`](CONTRIBUTING.md) for the development
workflow, coding conventions, and the parity requirements every engine change must satisfy.
By participating you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).

---

## License

© 2026 Rabid Troll Studios LLC. This repository is **dual-licensed** — see the
[LICENSE](LICENSE) map for the authoritative details and [NOTICE.md](NOTICE.md) for a
plain-English summary:

| Part | License |
|------|---------|
| **Agent SDK** (`UnityRTS/AgentSDK/`) — the contract agents build against | [MIT](UnityRTS/AgentSDK/LICENSE) — write & keep your agent private or commercial |
| **Engine & everything else** — simulation, harness, runners, Unity front-end, tests | [AGPL-3.0-or-later](LICENSES/AGPL-3.0.txt) + [AI-training exception](LICENSES/EXCEPTION-AI-TRAINING.txt) |
| **Art** (`art/`) | [MIT](art/LICENSE) — maximally open |

**Why:** the SDK is permissive so anyone can build (even commercial) agents against it; the
engine is copyleft so it can't be taken closed-source; the art is fully open. **AI/ML training
on the whole repo — code and art — is expressly permitted for everyone, commercially, with no
attribution required** (the AGPL part is freed for training use by the exception above).

This repository also bundles third-party assets and packages (e.g. Unity packages and fonts)
that are covered by their own licenses; those licenses remain in effect for the respective
files. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the inventory.

Security issues? See our [Security Policy](SECURITY.md).
