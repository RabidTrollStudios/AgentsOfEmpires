# Building & Testing

A reference for contributors working on the platform: how to build the engine, run the test
suites, build agents, and use the headless tooling. The whole engine builds and tests as a normal
.NET solution — **Unity is only needed for the visual front-end.**

For the workflow (branches, PRs, parity requirement), see [../CONTRIBUTING.md](../CONTRIBUTING.md).

---

## Prerequisites

- **.NET SDK 10.0+** — required for everything below.
- **Unity 6000.3.5f2** — only for the Unity project (`UnityRTS/RTS/`).
- A POSIX shell (Git Bash on Windows) for the helper scripts in `UnityRTS/scripts/`.

All commands below are run from the `UnityRTS/` directory unless noted.

---

## Building

### The whole solution

```bash
cd UnityRTS
dotnet build UnityRTS.slnx -c Release
```

This builds every project wired into the solution: `AgentSDK`, `AgentTestHarness`, `ParityRunner`,
`PlanningAgent`, and the test suites. MSBuild resolves dependency order — if `AgentSDK` changes,
its dependents rebuild automatically.

### Build everything and deploy to Unity

```bash
./build-all.sh                # solution + all agents, deploy DLLs to Unity, run smoke tests
./build-all.sh --skip-tests   # skip the smoke/parity tests
```

This also builds every agent in `Opponents/` into `EnemyAgents/` and copies the shared DLLs
(`AgentSDK`, `AgentTestHarness`) into `UnityRTS/RTS/Assets/Plugins/` so Unity picks up your
changes.

### Helper build scripts

| Script | Purpose |
|--------|---------|
| `./scripts/build-sdk.sh` | Build just the `AgentSDK`. |
| `./scripts/build-agent.sh <Name> [File]` | Build one agent (`Opponents/<Name>.cs` → `EnemyAgents/PlanningAgent_<Name>.dll`). |
| `./scripts/build-all-agents.sh` | Build every bundled agent. |
| `./scripts/rebuild-enemy-agents.sh` | Full rebuild: SDK + all agents → `EnemyAgents/`. |
| `./scripts/clean.sh` | Remove build artifacts. |

---

## Testing

The test suites wired into the solution are **`Gameplay.Tests`**, **`Opponent.Tests`**, and
**`Parity.Tests`**.

### Run everything

```bash
dotnet test UnityRTS.slnx -c Release
```

### Run a specific suite

```bash
dotnet test Parity.Tests/Parity.Tests.csproj     -c Release   # the determinism gate
dotnet test Gameplay.Tests/Gameplay.Tests.csproj -c Release   # core mechanics
dotnet test Opponent.Tests/Opponent.Tests.csproj -c Release   # sample-agent / balance behavior
```

### Filter to specific tests

```bash
dotnet test Parity.Tests/Parity.Tests.csproj -c Release \
  --filter "FullyQualifiedName~SameAgents_ProduceIdenticalState"
```

---

## Parity checking

Parity — Unity and the headless engine producing byte-for-byte identical per-tick state — is the
platform's core invariant. See [architecture.md](architecture.md) for how it works.

### Run the parity suite

```bash
dotnet test Parity.Tests/Parity.Tests.csproj -c Release
```

When a parity test fails, it reports the **first divergence tick** and the exact field that
differs. That tick is your primary debugging lead: it tells you *when* determinism broke, which
usually points straight at the offending logic.

### Headless scenario runner

`ParityRunner` runs scenarios headless and reports divergence:

```bash
dotnet run --project ParityRunner -- --list                 # list scenarios
dotnet run --project ParityRunner -- --scenario <name>      # run one
dotnet run --project ParityRunner -- --json results.json    # machine-readable output
```

### The parity pre-commit gate

A tracked `pre-commit` hook ([`.githooks/`](../.githooks/)) runs the parity suite whenever a
commit touches engine/sim files (`AgentSDK/`, `AgentTestHarness/`,
`RTS/Assets/Scripts/GameManager/`). Enable it once per clone:

```bash
git config core.hooksPath .githooks
```

If a change is intentionally mid-flight, you can bypass the gate with `git commit --no-verify` —
but never merge a parity-breaking change. See [../CONTRIBUTING.md](../CONTRIBUTING.md).

---

## Working with the Unity project

Unity is only needed to see matches visually or to change the front-end.

1. Ensure the shared DLLs are current in `RTS/Assets/Plugins/` — run `./build-all.sh` (or the
   relevant build script) after changing `AgentSDK`/`AgentTestHarness`, so Unity compiles against
   your latest engine.
2. Open `UnityRTS/RTS/` in Unity 6000.3.5f2.
3. Select the two agents and run a match.

> **Note:** Unity consumes `AgentSDK` as a *compiled plugin*, not source. If you change SDK code
> and don't rebuild/redeploy the DLL, Unity will still be running the old engine — a common source
> of confusion. Rebuild the DLLs, then let Unity reimport.

---

## Troubleshooting

- **"Unity behaves differently from headless."** You likely changed `AgentSDK` without
  redeploying the DLL to `RTS/Assets/Plugins/`. Rebuild and reimport.
- **A parity test fails after an engine change.** Read the first-divergence tick and field. Check
  for duplicated logic (implement once in `AgentSDK`), frame/time coupling, or nondeterministic
  iteration order. See [architecture.md](architecture.md#common-causes-of-parity-breaks).
- **The pre-commit hook doesn't run.** Run `git config core.hooksPath .githooks`.

---

## See also

- [architecture.md](architecture.md) — engine design, tick sequence, parity system
- [agent-authoring.md](agent-authoring.md) — writing agents
- [../CONTRIBUTING.md](../CONTRIBUTING.md) — contribution workflow
