# Agents of Empires — instructions for AI coding agents

Project guidance for any AI agent (Claude Code, etc.) working in this repository.

> **Naming:** This project is **"Agents of Empires."** Use that name in all prose,
> docs, commits, and PRs. "Warcrap" is only the local folder name (`C:\Git\Warcrap`,
> not yet renamed) and is being phased out.

## Working state: create a worktree before writing

**Before beginning any task that will MODIFY files or commit, create a git worktree
and do the work there.** Read-only tasks (explaining code, searching, answering a
question) do NOT need a worktree — work in place.

**Why:** this repo is often driven by multiple agent sessions at once, like parallel
devs sharing one machine (e.g. one session on art, one on engine code). The hazard
is not merge conflicts — it's **build and working-tree collision**:

- Building `AgentSDK` triggers a cascade that **rebuilds every agent DLL** (they must
  stay in sync), dirtying `UnityRTS/EnemyAgents/*.dll` and `UnityRTS/RTS/Assets/Plugins/*`.
- If two sessions share one checkout, that rebuild noise — or a branch switch — stomps
  the other session's state.

A per-session worktree gives each "dev" its own build sandbox, so the DLL cascade in
one session is invisible to the other.

**Rules:**
- Task writes files or commits → create a worktree first; commit and open a PR from it.
- Task is read-only → no worktree.
- **Never** commit on `main`, and **never** switch the shared checkout's branch when
  uncommitted changes exist — they may belong to another session.
- A fresh worktree branches from `origin/main` (clean), so it will **not** see
  uncommitted changes sitting in another checkout. Start work from a committed base.

**Human discipline (agents: remind, don't enforce):** keep only **one Unity editor
session open at a time** across all sessions. Worktrees isolate files and builds, but
not Unity's `Library/` cache or the editor lock; two editors on the project invites
corruption, and opening a fresh worktree in Unity may trigger a full reimport.

## Build & parity essentials

- The engine, SDK, agents, and tests build and test as a normal .NET solution
  (`UnityRTS/UnityRTS.slnx`) — **no Unity required** for headless work.
- **Parity is enforced by a pre-commit hook**: Unity and the headless `SimGame` must
  produce byte-identical per-tick state. Any engine/sim change runs the parity suite
  on commit; a change that breaks determinism can't land silently. Don't bypass the
  hook.
- See `docs/architecture.md` for the tick sequence and parity system, and
  `docs/agent-authoring.md` for writing agents.

## Licensing (be aware when adding files)

This repo is dual-licensed (see `LICENSE` for the map, `NOTICE.md` for plain English):

- `UnityRTS/AgentSDK/` → **MIT** (agents build against it freely).
- Engine and everything else → **AGPL-3.0-or-later** + an AI-training exception.
- `art/` → **MIT**.

When adding a new file, keep it on the correct side of that boundary, and add
third-party components to `THIRD-PARTY-NOTICES.md`.
