<!--
Thanks for contributing! Please fill out the sections below.
See CONTRIBUTING.md for the full workflow and the parity requirement.
-->

## Summary

What does this PR change, and why?

## Related issue

Closes #<!-- issue number -->

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Agent (new or improved)
- [ ] Documentation
- [ ] Refactor / tech debt
- [ ] Build / tooling

## Parity impact

<!-- Required for any change touching AgentSDK/, AgentTestHarness/, or
     RTS/Assets/Scripts/GameManager/. -->

- [ ] This change does **not** touch engine/simulation code, **or**
- [ ] It does, and the parity suite still passes
      (`dotnet test Parity.Tests/Parity.Tests.csproj -c Release`).

If parity is affected, describe how determinism is preserved:

## Checklist

- [ ] Builds: `dotnet build UnityRTS.slnx -c Release`
- [ ] Tests pass: `dotnet test UnityRTS.slnx -c Release`
- [ ] Added/updated tests for the change (regression test for bug fixes)
- [ ] Rebuilt agent DLLs are committed alongside source, if applicable
- [ ] Docs updated if behavior/API changed
- [ ] Follows the [coding conventions](../CONTRIBUTING.md#coding-conventions)

## Notes for reviewers

Anything reviewers should focus on.
