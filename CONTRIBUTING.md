# Contributing to Agents of Empires

Thanks for your interest in contributing! This guide covers how to set up, make changes, and
get them merged. Whether you're writing a new agent, fixing a bug, or improving the engine,
please read the section that applies to you — and the [parity requirement](#the-parity-requirement),
which every engine change must satisfy.

By participating, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

---

## Table of contents

- [Ways to contribute](#ways-to-contribute)
- [Development setup](#development-setup)
- [The parity requirement](#the-parity-requirement)
- [Branch & pull-request workflow](#branch--pull-request-workflow)
- [Coding conventions](#coding-conventions)
- [Testing](#testing)
- [Reporting bugs & requesting features](#reporting-bugs--requesting-features)

---

## Ways to contribute

- **Write or improve an agent.** See [`docs/agent-authoring.md`](docs/agent-authoring.md). Agents
  depend only on `AgentSDK` and don't touch the engine — the lowest-friction way to contribute.
- **Fix a bug or add an engine feature.** These touch the shared simulation and **must preserve
  parity** (see below).
- **Improve documentation or tests.** Always welcome.
- **Triage issues.** Reproducing, labeling, and clarifying issues is real help.

Good first issues are labeled [`good first issue`](https://github.com/RabidTrollStudios/AgentsOfEmpires/labels/good%20first%20issue).

---

## Development setup

### Prerequisites

- **.NET SDK 10.0+** — required for everything. The entire engine, SDK, agents, and test suites
  build and run as a normal .NET solution, with **no Unity dependency**.
- **Unity 6000.3.5f2** — only needed if your change affects the visual front-end (`UnityRTS/RTS/`).
- A POSIX shell (Git Bash on Windows) for the helper scripts.

### Clone and build

```bash
git clone https://github.com/RabidTrollStudios/AgentsOfEmpires.git
cd AgentsOfEmpires/UnityRTS

dotnet build UnityRTS.slnx -c Release
dotnet test  UnityRTS.slnx -c Release
```

### Install the parity pre-commit hook (important)

The repository ships a tracked `pre-commit` hook (in [`.githooks/`](.githooks/)) that runs the
parity suite when you touch engine/sim files. Git does not use tracked hooks automatically — run
this **once** after cloning, from the repository root:

```bash
git config core.hooksPath .githooks
```

That's it — the hook is now active for this clone. To confirm, make a trivial change under
`UnityRTS/AgentSDK/` and run `git commit`; you should see
*"Engine/sim files changed — running parity tests…"*. If you don't, re-run the command above.

---

## The parity requirement

**This is the single most important rule for engine changes.**

Agents of Empires runs the same simulation two ways: inside **Unity** (for visualization) and
**headless** (`AgentTestHarness`/`SimGame`, for fast reproducible matches). These two runtimes
must produce **byte-for-byte identical per-tick state**. That determinism is what makes matches
fair, reproducible, and testable.

Any change to the shared simulation can break this. The monitored areas are:

- `UnityRTS/AgentSDK/` — the shared interface + game-logic layer
- `UnityRTS/AgentTestHarness/` — the headless simulation engine
- `UnityRTS/RTS/Assets/Scripts/GameManager/` — the Unity game engine

If your change touches any of these, the **`pre-commit` hook runs the parity tests and blocks
the commit if they fail.** You can bypass it locally with `git commit --no-verify`, but do so
only when you are *intentionally* committing known-incomplete work — and say so in the commit
message. **PRs that break parity will not be merged.**

Rules of thumb for keeping parity:

- Put shared game logic in `AgentSDK` and have *both* engines call it — never reimplement a rule
  in Unity and the harness separately.
- Avoid frame-rate-dependent or wall-clock-dependent logic in the simulation; the tick is the
  only clock.
- Avoid nondeterministic ordering (e.g. iterating a hash set); sort before iterating when order
  affects state.
- When in doubt, run `dotnet test Parity.Tests/Parity.Tests.csproj -c Release` and check the
  first divergence tick.

See [`docs/architecture.md`](docs/architecture.md) for how the tick sequence and parity system
work.

---

## Branch & pull-request workflow

`main` is protected. **All changes land via reviewed pull requests** — direct pushes to `main`
are blocked, and every PR requires **at least one approving review** before it can merge.

1. **Create a branch** off `main`. Use a descriptive, prefixed name:
   - `fix/…` for bug fixes (e.g. `fix/barracks-placement-parity`)
   - `feat/…` for features
   - `docs/…` for documentation
   - `refactor/…`, `test/…`, `chore/…` as appropriate

2. **Make focused commits.** Keep unrelated changes out of the same PR. Write clear commit
   messages (imperative mood: *"Fix stale mine reference"*). If your change rebuilds agent DLLs,
   commit those alongside the source so the binaries stay in sync.

3. **Run the tests** before pushing:
   ```bash
   cd UnityRTS && dotnet test UnityRTS.slnx -c Release
   ```

4. **Open a PR against `main`.** In the description:
   - Summarize *what* changed and *why*.
   - Link the issue it addresses (`Closes #123`).
   - Note any parity impact, and confirm the parity suite passes (or explain why it can't yet).

5. **Address review feedback.** Reviews are dismissed when you push new commits, so re-request
   review after changes. All review conversations must be resolved before merge.

6. **Merge** once approved and green. Prefer a clean history (squash or rebase as the maintainers
   direct).

> **Note:** if you're an outside contributor without write access, fork the repository, push your
> branch to your fork, and open the PR from there.

---

## Coding conventions

The codebase is C# (.NET) with a Unity front-end. There is no enforced formatter config, so the
guiding rule is: **match the surrounding code.**

- **Style:** follow the conventions already in the file you're editing (indentation, brace
  placement, naming). The engine uses PascalCase for public members and `UPPER_SNAKE_CASE` for
  enum values and constants — stay consistent.
- **Comments:** document *why*, not *what*. Public SDK types carry XML doc comments; keep them
  accurate when you change behavior.
- **No engine access from agents.** Agent code depends only on `AgentSDK` interfaces. Never
  reach into engine internals from an agent.
- **Shared logic lives in `AgentSDK`.** If both Unity and the harness need a behavior, implement
  it once in the SDK and call it from both — this is a hard rule for parity.
- **Determinism first.** Prefer deterministic data structures and explicit ordering in any code
  the simulation depends on.

---

## Testing

All tests run headless via the .NET solution:

```bash
cd UnityRTS

dotnet test UnityRTS.slnx -c Release          # everything

# Or a specific suite:
dotnet test Parity.Tests/Parity.Tests.csproj -c Release      # parity gate
dotnet test Gameplay.Tests/Gameplay.Tests.csproj -c Release  # gameplay mechanics
```

The suites:

- **`Gameplay.Tests`** — core game mechanics.
- **`Opponent.Tests`** — sample-agent and balance behavior.
- **`Parity.Tests`** — the determinism gate (Unity vs. headless).

(These three are the projects wired into `UnityRTS.slnx`. Some other `*.Tests/` directories
exist on disk but are not currently part of the solution.)

**Add tests with your change.** Bug fixes should include a regression test; new behavior should
be covered. Engine changes should keep the parity suite green.

---

## Reporting bugs & requesting features

Use the [issue tracker](https://github.com/RabidTrollStudios/AgentsOfEmpires/issues). A good
report includes:

- **What you expected** vs. **what happened.**
- **Repro steps** — ideally the map seed, the agents involved, and the tick where things go wrong
  (parity/determinism bugs are much easier to fix with a first-divergence tick).
- Environment: .NET version, and Unity version if relevant.

Thanks for contributing! 🛡️
