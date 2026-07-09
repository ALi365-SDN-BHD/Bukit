# Coverage Gate Residual Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close Coverage Gate findings #1, #5, and #10 without changing Core runtime behavior or unrelated local work.

**Architecture:** Keep cleanup validation in the existing small Python helper, use YamlDotNet for semantic workflow assertions, and use explicit Git path allowlists plus a backup branch/stash for history reconstruction. Each code change is test-first and independently verified before Git packaging.

**Tech Stack:** Bash, Python 3 standard library, .NET 10, xUnit, YamlDotNet, Git.

## Global Constraints

- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, `scripts-0.2/`, or `.github/workflows-0.1/`.
- Do not change coverage thresholds, Core project membership, or Core runtime code.
- Preserve every pre-existing staged, unstaged, and untracked unrelated change.
- Keep all active scripts below the repository 200-line shell-script limit.
- Execute tasks serially and stop on a failed test or scope audit.

---

### Task 1: Restrict temporary coverage cleanup

**Files:**
- Modify: `scripts/checks/coverage/output-path-self-test.sh`
- Modify: `scripts/checks/coverage/validate-output-root.py`

**Interfaces:**
- Consumes: `validate-output-root.py <output-root> <repo-root>`.
- Produces: the canonical accepted output path on stdout or exit code 1.

- [ ] **Step 1: Add failing path cases**

Add an accepted `bukit-coverage-*` temporary directory and a rejected unrelated
temporary directory to `output-path-self-test.sh`.

- [ ] **Step 2: Verify RED**

Run: `bash scripts/checks/coverage/output-path-self-test.sh`

Expected: FAIL because the unrelated temporary directory is currently accepted.

- [ ] **Step 3: Implement the minimum namespace restriction**

Accept a temporary path only when the first component below a recognized temp
root starts with `bukit-coverage-`; retain repository coverage behavior and
symlink resolution checks.

- [ ] **Step 4: Verify GREEN**

Run: `bash scripts/checks/coverage/output-path-self-test.sh`

Expected: PASS with the unrelated path rejected.

- [ ] **Step 5: Run Task 1 targeted verification**

Run shell/Python syntax checks and the path validator's direct accepted/rejected
commands. Review only the two-file diff before continuing.

### Task 2: Make workflow Architecture contracts structural

**Files:**
- Modify: `tests/Bukit.Architecture.Tests/CoverageGateTests.cs`

**Interfaces:**
- Consumes: `.github/workflows/ci.yaml` and `.github/workflows/release.yaml`.
- Produces: xUnit failures tied to concrete job, dependency, step, and artifact nodes.

- [ ] **Step 1: Replace workflow substring assertions with YAML navigation tests**

Use `YamlStream`, `YamlMappingNode`, and `YamlSequenceNode` helpers to read jobs,
needs, steps, run commands, artifact names, and artifact paths.

- [ ] **Step 2: Verify RED against mutated fixtures**

Temporarily copy each workflow to a test string, mutate one required dependency
or artifact path, and assert the structural helper rejects it. Run the focused
test and confirm it fails before completing the validator implementation.

- [ ] **Step 3: Implement the minimum structural validator**

Validate CI and release coverage job graphs, package dependencies, project
collection commands, summary commands, and artifact paths. Do not validate
unrelated workflow behavior.

- [ ] **Step 4: Verify GREEN**

Run: `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --filter FullyQualifiedName~CoverageGateTests`

Expected: all CoverageGateTests pass.

- [ ] **Step 5: Run all Architecture tests**

Run: `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release`

Expected: 55 or more tests, zero failures.

### Task 3: Reconstruct and package clean Git history

**Files:**
- Track: `scripts/checks/coverage/matrix.py`
- Track: `scripts/checks/coverage/validate-output-root.py`
- Track: `scripts/checks/coverage/matrix-self-test.sh`
- Track: `scripts/checks/coverage/output-path-self-test.sh`
- Track: `scripts/checks/coverage/project-list-self-test.sh`
- Track: `scripts/checks/coverage/summarize-self-test.py`
- Repackage only the existing Coverage-related tracked files shown by the final diff audit.

**Interfaces:**
- Consumes: authorized local-only commits based on `origin/1.0.8`.
- Produces: separate Coverage and post-change commits plus preserved unrelated working state.

- [ ] **Step 1: Establish recovery points**

Create a `codex/coverage-rewrite-backup-*` branch at the original HEAD, record
status and hashes under `/private/tmp`, then stash all local changes with
`--include-untracked`.

- [ ] **Step 2: Split the contaminated commit**

Rewrite the local sequence from `origin/1.0.8`, separating the Coverage path
allowlist from `AGENTS.md`, `guide/dev/agent-task-workflow.md`, and
`scripts/checks/post-change-targeted.sh`. Replay the existing P1/P2 design commits.

- [ ] **Step 3: Restore the working state**

Apply the saved stash with its index. Resolve only evidence-backed conflicts,
retain the stash until verification passes, and compare status with the saved
pre-rewrite status.

- [ ] **Step 4: Commit the Coverage closure by explicit path allowlist**

Stage and commit only Coverage workflows, policy/docs, coverage scripts, the
artifact allowlist, and `CoverageGateTests.cs`. Leave all post-change files in
their original staged/untracked state.

- [ ] **Step 5: Run final verification**

Run all Coverage self-tests, full Coverage, all Architecture tests, `ci-fast`,
security regression, the targeted post-change gate for Coverage paths, syntax
checks, `git diff --check`, and the backup/reference path audit.

- [ ] **Step 6: Perform independent read-only review**

Request a bounded reviewer to inspect only the rewritten commit sequence and
Coverage closure diff for incomplete tracking, destructive path gaps, weak YAML
contracts, over-limit scripts, or unrelated drift.

