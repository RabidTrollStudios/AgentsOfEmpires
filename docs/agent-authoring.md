# Writing an Agent

This is a step-by-step guide to building an AI agent for Agents of Empires: the contract, the
tools you have, how to build and test it, and how to enter it into a match.

You only need the **`AgentSDK`** — agents never touch the engine. If you haven't already, skim
[What this is](../README.md#what-this-is) in the README for the game overview.

---

## Contents

- [The agent contract](#the-agent-contract)
- [Naming & file conventions](#naming--file-conventions)
- [What your agent can see (`IGameState`)](#what-your-agent-can-see-igamestate)
- [What your agent can do (`IAgentActions`)](#what-your-agent-can-do-iagentactions)
- [The tick model](#the-tick-model)
- [A minimal agent](#a-minimal-agent)
- [Building your agent](#building-your-agent)
- [Testing your agent](#testing-your-agent)
- [Tips & pitfalls](#tips--pitfalls)

---

## The agent contract

An agent implements `IPlanningAgent` — four methods called by the engine:

```csharp
public interface IPlanningAgent
{
    void InitializeMatch();                              // once per match (best-of-N rounds)
    void InitializeRound(IGameState state);              // once at the start of each round
    void Update(IGameState state, IAgentActions actions);// once per tick — your decisions
    void Learn(IGameState state);                        // once at the end of each round
}
```

In practice you extend **`PlanningAgentBase`**, which implements the interface, tracks your units
for you, and lets you override only what you need. The reference agent
[`PlanningAgent`](../UnityRTS/PlanningAgent/PlanningAgent.cs) is the best worked example.

---

## Naming & file conventions

This trips people up, so read carefully:

- **Every agent's class is named `PlanningAgent` and lives in the `PlanningAgent` namespace** —
  regardless of what the agent actually is. The identity does **not** come from the class name.
- **The agent's identity comes from its filename.** A file `MyAgent.cs` builds into
  `EnemyAgents/PlanningAgent_MyAgent.dll`, and the game refers to it as `MyAgent`.
- Agent source files live in `UnityRTS/Opponents/` (one `.cs` per agent).

So a new agent `MyAgent` is a file `UnityRTS/Opponents/MyAgent.cs` containing:

```csharp
using AgentSDK;

namespace PlanningAgent
{
    public class PlanningAgent : PlanningAgentBase
    {
        // ...
    }
}
```

---

## What your agent can see (`IGameState`)

`IGameState` is your **read-only** view of the world. You receive it fresh each tick.

| Member | Purpose |
|--------|---------|
| `MyAgentNbr`, `EnemyAgentNbr` | Your and your opponent's player number |
| `MyGold`, `EnemyGold` | Current gold totals |
| `MapSize` | Map dimensions |
| `MyWins` | Rounds you've won this match |
| `GetMyUnits(type)` | Unit numbers you own of a given type |
| `GetEnemyUnits(type)` | Enemy unit numbers of a given type |
| `GetAllUnits(type)` | All units of a type regardless of owner — **use this for mines** (they're neutral) |
| `GetUnit(nbr)` | Full `UnitInfo` (type, owner, position, health, current action…) for a unit |
| `IsPositionBuildable(pos)` | Is a single cell buildable |
| `IsAreaBuildable(type, pos)` / `IsBoundedAreaBuildable(type, pos)` | Is a building's footprint buildable at a position |
| `GetPathBetween(a, b)` / `GetPathBetween(a, b, avoidUnits)` | Pathfinding between positions |
| `GetPathToUnit(start, type, unitPos)` | Path toward a unit |
| `GetBuildablePositionsNearUnit(type, unitPos)` | Buildable spots near a unit |
| `FindProspectiveBuildPositions(type)` | All buildable positions for a building type |
| `GetFailedCommands()` | Commands that failed during the last tick's processing (useful for debugging) |

---

## What your agent can do (`IAgentActions`)

`IAgentActions` is how you act. Each call **queues** a command (see [the tick model](#the-tick-model))
and returns a `CommandResult` indicating whether it was accepted.

| Command | Effect |
|---------|--------|
| `Move(unit, target)` | Move a unit toward a grid position |
| `Build(pawn, target, type)` | Have a pawn construct a building at a position |
| `Train(building, type)` | Train a new unit at a building |
| `Gather(pawn, mine, base)` | Send a pawn to mine gold and deliver it to a base |
| `Attack(unit, target)` | Attack an enemy unit |
| `Repair(pawn, building)` | Repair a friendly building |
| `Heal(monk, target)` | Heal a friendly unit |
| `Log(message)` | Emit a debug message |

You **cannot** reach the engine, your opponent's internals, or any global state. The tables above
are the entire surface — which is exactly what keeps matches fair and reproducible.

---

## The tick model

The game advances in discrete ticks. Each tick, the engine calls your `Update(state, actions)`
exactly once. Two rules matter:

1. **Commands are queued, then applied next tick.** A command you issue during `Update` on tick
   *N* is processed at the start of tick *N+1*. Don't expect the world to change mid-`Update`.
2. **You see post-advance state.** By the time your `Update` runs, units have already moved/acted
   for the current tick. Plan for the *next* tick.

This model is identical in the Unity and headless engines — your agent behaves the same either
way. (See [architecture.md](architecture.md) for why.)

---

## A minimal agent

The simplest legal agent does nothing (`Opponents/Idle.cs`):

```csharp
using AgentSDK;

namespace PlanningAgent
{
    public class PlanningAgent : PlanningAgentBase
    {
        public override void InitializeMatch() { }
        public override void Update(IGameState state, IAgentActions actions) { }
    }
}
```

A slightly-less-trivial agent — send every idle pawn to gather from the nearest mine:

```csharp
using AgentSDK;

namespace PlanningAgent
{
    public class PlanningAgent : PlanningAgentBase
    {
        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            base.Update(state, actions); // PlanningAgentBase refreshes your unit lists

            var mines = state.GetAllUnits(UnitType.MINE);   // mines are neutral
            var bases = state.GetMyUnits(UnitType.BASE);
            if (mines.Count == 0 || bases.Count == 0) return;

            foreach (int pawn in state.GetMyUnits(UnitType.PAWN))
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Gather(pawn, mines[0], bases[0]);
            }
        }
    }
}
```

Study the bundled agents in `UnityRTS/Opponents/` (e.g. `ArcherSwarm.cs`, `Commander.cs`,
`EconBoom.cs`) for full strategies, and `UnityRTS/PlanningAgent/PlanningAgent.cs` for a complete
three-phase FSM.

---

## Building your agent

Put your source in `UnityRTS/Opponents/MyAgent.cs`, then:

```bash
cd UnityRTS
./scripts/build-agent.sh MyAgent
# compiles Opponents/MyAgent.cs -> EnemyAgents/PlanningAgent_MyAgent.dll
```

The DLL is deployed to `EnemyAgents/`, where both the headless engine and Unity load it by name
(`MyAgent`).

To (re)build every bundled agent at once:

```bash
./scripts/build-all-agents.sh
```

---

## Testing your agent

The fastest loop is headless — no Unity required.

- **Run it against another agent** using the headless harness / parity runner:
  ```bash
  cd UnityRTS
  dotnet run --project ParityRunner -- --list          # see available scenarios
  dotnet run --project ParityRunner -- --scenario <name>
  ```
- **Watch it in Unity.** Open the Unity project (`UnityRTS/RTS/`), select your agent and an
  opponent, and run a match to see it play.
- **Use `Log()`** to print decisions, and `GetFailedCommands()` to see which commands the engine
  rejected and why (e.g. `INSUFFICIENT_GOLD`, `INVALID_TARGET`).

---

## Tips & pitfalls

- **Mines are neutral.** Use `GetAllUnits(UnitType.MINE)`, not `GetMyUnits` — you don't own mines.
- **Check `CommandResult`.** A queued command can fail (not enough gold, unbuildable target,
  cooldown). Read the result and/or `GetFailedCommands()`.
- **Buildings have footprints.** Use `IsBoundedAreaBuildable` / `FindProspectiveBuildPositions`
  for multi-cell buildings rather than assuming a single cell is free.
- **Don't assume immediate effects.** Commands land next tick; write your logic to tolerate the
  one-tick delay.
- **Keep it deterministic if you contribute it.** Bundled agents run in the parity suite; avoid
  wall-clock time or unordered iteration that affects behavior.

---

## See also

- [architecture.md](architecture.md) — how the engine and tick model work
- [building-and-testing.md](building-and-testing.md) — full build/test reference
- [`PlanningAgent`](../UnityRTS/PlanningAgent/PlanningAgent.cs) — the reference agent
