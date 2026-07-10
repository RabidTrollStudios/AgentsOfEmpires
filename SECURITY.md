# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in Agents of Empires, please report it
**privately** — do not open a public issue for security problems.

Email **dana@rabidtrollstudios.com** with:

- A description of the vulnerability and its potential impact.
- Steps to reproduce it (proof-of-concept, affected files, or a minimal example).
- Any suggested remediation, if you have one.

You can expect an acknowledgement within a reasonable time frame. We ask that you
give us a chance to investigate and address the issue before any public
disclosure.

## Scope

This project is a research/competition platform. The most relevant security
considerations are:

- **Untrusted agent DLLs.** Agents are compiled to DLLs and loaded at runtime. A
  malicious or buggy agent runs as normal .NET code with the privileges of the
  host process. **Only load agent DLLs you trust.** Do not run untrusted agents
  from unknown sources on a machine where that matters.
- **The engine and SDK.** Bugs in `AgentSDK`, the harness, or the Unity front-end
  that could be exploited beyond a single match are in scope.

Reports about a *poorly-playing* agent, game-balance issues, or ordinary bugs are
not security issues — please use the normal
[issue tracker](https://github.com/RabidTrollStudios/AgentsOfEmpires/issues) for
those.

## Supported versions

This project is in active development; security fixes are applied to the `main`
branch. There is no long-term-support release line at this time.
