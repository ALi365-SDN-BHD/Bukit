# AD-06 Code Quality Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close AD-06 with a reproducible format gate, analyzer debt ratchet, curated correctness and naming rules, and report-only complexity governance without broad source modernization.

**Architecture:** Keep enforcement in repository-owned wrappers called identically by local verification and `ci-fast`. Separate zero-tolerance formatting and selected correctness rules from report-only debt counts. Store only per-diagnostic counts so source moves do not churn the baseline, and fail only when a diagnostic count increases or a new diagnostic appears.

**Tech Stack:** Bash, Python 3 standard library, .NET SDK analyzers, EditorConfig, existing Bukit focused and targeted gates.

## Global Constraints

- Execute AD-06A through AD-06E in order and verify each phase before continuing.
- Do not bulk-fix the existing style/analyzer inventory.
- Do not change public APIs, schemas, plugin protocols, runtime output, or dependency versions.
- Do not run full, release, smoke-all, test-all, coverage, or whole-solution test gates.
- Run `post-change-focused.sh` after each phase and `post-change-targeted.sh` exactly once for the aggregate diff.

---

### Task 1: AD-06A format contract closure

**Files:**
- Create: `scripts/checks/dotnet-format.sh`
- Create: `scripts/checks/dotnet-format-self-test.sh`
- Modify: `scripts/gates/ci-fast.sh`
- Modify: `scripts/checks/post-change-focused-owner-checks.sh`
- Modify: `.github/PULL_REQUEST_TEMPLATE.md`
- Modify: `src/Bukit-Core/Bukit.Engine/RenderPipeline.cs`
- Modify: `guide/dev/testing.md`

**Interfaces:**
- Produces: `bash scripts/checks/dotnet-format.sh`, a no-argument wrapper for `dotnet format bukit-core.slnx --verify-no-changes --no-restore`.

- [ ] Write a shell self-test that requires the wrapper command, exact arguments, status propagation, argument rejection, and exactly-once `ci-fast` wiring.
- [ ] Run the self-test and verify that it fails because the wrapper and wiring do not exist.
- [ ] Add the wrapper, wire its self-test and real check into `ci-fast`, and register direct owner-check routing.
- [ ] Correct only the four Roslyn-reported indentation lines in `RenderPipeline.cs`.
- [ ] Update the PR checklist and testing guide to call the repository wrapper.
- [ ] Run the self-test, the real wrapper, and focused verification for Task 1 paths.
- [ ] Commit Task 1 independently.

### Task 2: AD-06B analyzer inventory baseline and ratchet

**Files:**
- Create: `scripts/checks/code-analysis-ratchet.py`
- Create: `scripts/checks/code-analysis-ratchet.sh`
- Create: `scripts/checks/code-analysis-ratchet-self-test.sh`
- Create: `scripts/checks/baselines/code-analysis.v1.json`
- Modify: `scripts/gates/ci-fast.sh`
- Modify: `scripts/checks/post-change-focused-owner-checks.sh`
- Modify: `guide/dev/testing.md`

**Interfaces:**
- Produces: `bash scripts/checks/code-analysis-ratchet.sh check` and `snapshot OUTPUT`.
- Consumes: the `format-report.json` files emitted by `dotnet format style` and `dotnet format analyzers` at severity `info`.

- [ ] Write comparator and wrapper self-tests covering unchanged/decreased counts, new IDs, increased counts, malformed input, expected formatter exit 2, unexpected formatter failure, and exactly-once `ci-fast` wiring.
- [ ] Run the self-test and verify the missing implementation failure.
- [ ] Implement strict UTF-8 JSON parsing and per-category/per-ID non-increase comparison.
- [ ] Implement the wrapper with bounded temporary reports and cleanup.
- [ ] Generate the initial baseline from the current branch; do not edit source to reduce it.
- [ ] Wire self-test and real ratchet into `ci-fast`, register owner routing, and document snapshot/check commands.
- [ ] Run self-test, real ratchet, and focused verification for Task 2 paths.
- [ ] Commit Task 2 independently.

### Task 3: AD-06C curated correctness, async, and disposal rules

**Files:**
- Modify: `.editorconfig`
- Modify: `Directory.Build.props`
- Modify only the source/test files identified by CA2016 or CA2250 if the findings are confirmed.
- Modify: `scripts/checks/baselines/code-analysis.v1.json`
- Modify: `guide/dev/code-quality-governance.md`

**Interfaces:**
- Produces: pinned analyzer-wave behavior and explicit rule severities.

- [ ] Add a self-test assertion for the pinned analysis level and exact selected-rule severities; verify it fails first.
- [ ] Inventory CA1001, CA1063, CA1816, CA1849, CA2000, CA2012, CA2016, CA2213, CA2215, CA2216, and CA2250 under the proposed policy.
- [ ] Confirm each current CA2016/CA2250 occurrence with its owning focused tests, then make only the minimal behavior-preserving fix.
- [ ] Promote zero-debt or cleaned correctness rules to warning; retain contract-sensitive/noisy rules at suggestion and ratchet them.
- [ ] Pin `AnalysisLevel` to the currently effective wave rather than `latest`.
- [ ] Refresh the baseline only after reviewing the delta.
- [ ] Run the policy self-test, affected project tests, real format/ratchet checks, and focused verification.
- [ ] Commit Task 3 independently.

### Task 4: AD-06D naming and API-design governance

**Files:**
- Modify: `.editorconfig`
- Modify: `scripts/checks/code-analysis-ratchet-self-test.sh`
- Modify: `scripts/checks/baselines/code-analysis.v1.json`
- Modify: `guide/dev/code-quality-governance.md`
- Modify only a narrow EditorConfig path exception for intentional template/protocol names.

**Interfaces:**
- Produces: explicit PascalCase/interface-prefix naming rules and report-only API-design diagnostics.

- [ ] Extend the policy self-test with exact naming symbols/styles/severities and a narrow intentional-boundary exception; verify failure first.
- [ ] Add low-risk naming rules for types, interfaces, and ordinary members at warning severity.
- [ ] Preserve intentional external names such as Scriban snake_case through the narrowest configuration exception.
- [ ] Add CA1068 and selected API-design rules as suggestion-level ratcheted diagnostics; do not alter stable signatures.
- [ ] Document why Task/ValueTask-aware `Async` suffix enforcement is not approximated with an over-broad EditorConfig suffix rule.
- [ ] Refresh and review the baseline, then run real format/ratchet checks and focused verification.
- [ ] Commit Task 4 independently.

### Task 5: AD-06E complexity report and non-regression governance

**Files:**
- Modify: `.editorconfig`
- Modify: `scripts/checks/code-analysis-ratchet-self-test.sh`
- Modify: `scripts/checks/baselines/code-analysis.v1.json`
- Modify: `guide/dev/code-quality-governance.md`

**Interfaces:**
- Produces: report-only CA1502, CA1505, and CA1506 diagnostics with explicit thresholds, enforced only through baseline non-increase.

- [ ] Extend the policy self-test with exact complexity severities and thresholds; verify failure first.
- [ ] Configure complexity rules as suggestion, never warning/error, and avoid file-length thresholds.
- [ ] Generate and review the complexity baseline delta without splitting or rewriting production methods.
- [ ] Run format, ratchet, architecture tests, and focused verification for Task 5 paths.
- [ ] Commit Task 5 independently.

### Task 6: Aggregate verification and read-only audit

**Files:**
- Review all paths changed since the parent-task base `d8bd883f3d771d3eb71bf876fab39e49d974c7eb`.

- [ ] Run `bash scripts/checks/post-change-targeted.sh --base d8bd883f3d771d3eb71bf876fab39e49d974c7eb -- <all changed paths>` exactly once.
- [ ] Run a fresh Architecture test project and both real governance checks.
- [ ] Audit the aggregate diff for public API/runtime drift, overly broad suppressions, baseline inflation, generated files, path leakage, and CI/local command mismatch.
- [ ] Confirm `git diff --check`, clean tracked status after commits, and record any environment blocker without changing unrelated code.
