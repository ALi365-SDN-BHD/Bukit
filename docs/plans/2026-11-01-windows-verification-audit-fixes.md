# Windows Verification Audit Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Resolve W-01 (Windows-only skips breaking Linux coverage) and W-02 (the Windows Git shim hiding effective transport targets).

**Architecture:** Keep the existing per-project coverage filter and strict TRX acceptance. Resolve transport URLs through real Git before mapping the test remote to local A/B repositories; reject unsupported multi-target transport. Reuse the current real-Git fixture and run the same embedded shim assembly on each platform.

**Tech Stack:** Bash, Python 3, .NET 10, xUnit, Git, GitHub Actions.

## Approved scope

The user approved complete remediation of the two findings against `fc59b114`.
This is verification maintenance under the existing deployment identity spec;
the production deployment contract and public API do not change.

## One implementation task: both connected verification fixes

1. Extend `scripts/checks/coverage/project-list-self-test.sh` to discover the
   actual Windows-only methods and verify the emitted filter on Linux, macOS,
   and Windows shell identities. Run it before changing the project list and
   retain the expected failure.
2. Add real-Git shim calibration tests in
   `tests/Bukit.Cli.Tests/DeployIdentityTransportTests.cs`: normal push,
   `pushurl`, `insteadOf`, `pushInsteadOf`, fetch/query selection, unknown
   effective target, and multiple push targets. Expose the existing compiled
   assembly path in `WindowsGitShim.cs` for these tests. Run the complete CLI
   specialty and retain failures showing the old shim targets B incorrectly.
3. In `scripts/checks/coverage/list-core-projects.sh`, emit exact method-name
   exclusions for the five Windows-only methods on non-Windows hosts. Keep
   Windows selections and the other projects unchanged.
4. In `WindowsGitShim.cs`, use real `git remote get-url --all` and add `--push`
   for push operations. Require exactly one returned URL before allowlisted
   mapping, logging, or transport.
5. Update `.github/workflows/ci.yaml` and
   `tests/Bukit.Architecture.Tests/WindowsGitTransportWorkflowTests.cs` to require
   all 61 Windows cases, including the ten new shim calibration cases and the
   deployment metadata symlink regression.
6. Register the coverage selection owner and this plan in
   `scripts/checks/codex-workflow-policy.v1.json`, with exact mapping assertions
   in `scripts/checks/codex-workflow-self-test.d/closure-mappings.sh`.
7. Run the required specialties below serially for .NET/fixture work. Record
   GREEN evidence with the affected closure, exact command, toolchain and
   environment state; reuse it during review.
8. Perform one specialty review, address Critical/Important findings only,
   then perform one generated delta-only final review.

## Required verification

- `bash scripts/checks/coverage/project-list-self-test.sh`
- `bash scripts/checks/coverage/matrix-self-test.sh`
- `bash scripts/checks/coverage-run-one-self-test.sh`
- `bash scripts/checks/codex-workflow-self-test.sh`
- `bash scripts/checks/active-workflow-boundary-self-test.sh`
- `bash scripts/checks/active-workflow-boundary.sh`
- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`
- `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj`
- Run `scripts/checks/coverage/run-one.sh` for the CLI project only, using the
  exact filter emitted by `list-core-projects.sh` and an external evidence
  directory. Require zero skipped results and retain the actual TRX.

The execution record holds exact expanded commands and results. Full/release
gates, remote publication and unrelated Config coverage repair are outside this
task. Fresh native Windows evidence must be distinguished from cross-platform
execution of the unchanged shim entry point; earlier CI evidence is not reused
as proof for the new diff.
