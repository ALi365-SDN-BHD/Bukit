# Codex High-Speed Workflow Tooling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn Bukit's high-speed agent rules into deterministic tooling for verification evidence, closure selection, delta-only review, single-writer coordination, resource scheduling, and speed metrics.

**Architecture:** Add one standard-library Python CLI with six isolated subcommands and one policy file. A shell self-test exercises observable CLI behavior in temporary repositories and state directories. Repository governance documents define when controllers invoke each subcommand, while the owner-routing contract maps the new verification files to their direct self-test.

**Tech Stack:** Python 3 standard library, Bash self-tests, JSON schema-versioned records, Git, existing Bukit governance scripts.

## Global Constraints

- Keep one repository writer; do not modify unrelated working-tree changes.
- Implement priorities 1 through 6 in order.
- For every priority, observe a behavior-specific RED before implementation and run the complete specialty GREEN after implementation.
- Do not run `post-change-*`, `ci-fast`, full/release gates, whole-solution tests, or historical fixtures.
- Use `/tmp/codex-reports/` for runtime evidence; never store environment-variable values.
- All JSON output is deterministic UTF-8 with sorted keys and a trailing newline.
- Every persisted record declares `schemaVersion: 1`.

---

### Task 1: GREEN evidence cache

**Files:**
- Create: `scripts/checks/codex-workflow.py`
- Create: `scripts/checks/codex-workflow-self-test.sh`

**Interfaces:**
- Produces: `cache record` and `cache check`.
- Record fingerprint inputs: resolved base HEAD, closure file content hashes, exact command, environment state, SDK version.
- Cache hit: exit `0`; cache miss: exit `1`; malformed input: exit `2`.

- [x] Add a self-test that records passing evidence, verifies a hit, then verifies misses after file, command, environment-state, SDK, or failed-result changes.
- [x] Run `bash scripts/checks/codex-workflow-self-test.sh` and observe RED because the CLI does not exist.
- [x] Implement deterministic fingerprinting and atomic record writes.
- [x] Run the self-test and observe GREEN.

### Task 2: Verification closure generator

**Files:**
- Modify: `scripts/checks/codex-workflow.py`
- Create: `scripts/checks/codex-workflow-policy.v1.json`
- Modify: `scripts/checks/codex-workflow-self-test.sh`

**Interfaces:**
- Produces: `closure` JSON containing changed files, direct source consumers, contract consumers, closure files, exact specialty commands, and unmapped files.
- Consumes: ordered path rules from `codex-workflow-policy.v1.json`.

- [x] Add a self-test fixture with a changed public/config type, a direct consumer, a mapped test project, and one unmapped file.
- [x] Run the self-test and observe RED because `closure` is unavailable.
- [x] Implement token-based direct-consumer discovery plus deterministic policy mapping.
- [x] Run the self-test and observe GREEN.

### Task 3: Delta-only final review

**Files:**
- Modify: `scripts/checks/codex-workflow.py`
- Modify: `scripts/checks/codex-workflow-self-test.sh`
- Modify: `AGENTS.md`
- Modify: `guide/dev/agent-task-workflow.md`
- Modify: `guide/dev/testing.md`

**Interfaces:**
- Produces: `review-scope` JSON with reusable evidence, invalidated evidence, cross-task intersections, uncovered changed files, public-contract focus, and open Critical/Important findings.

- [x] Add a self-test with two specialty evidence records, one overlapping file, one uncovered file, and findings at Important and Minor severities.
- [x] Run the self-test and observe RED because `review-scope` is unavailable.
- [x] Implement the scope calculation.
- [x] Document that final review consumes unchanged specialty evidence and reviews only interaction, uncovered, invalidated, contract, and open Critical/Important deltas.
- [x] Run the self-test and observe GREEN.

### Task 4: Single-writer queue

**Files:**
- Modify: `scripts/checks/codex-workflow.py`
- Modify: `scripts/checks/codex-workflow-self-test.sh`
- Modify: `AGENTS.md`
- Modify: `guide/dev/agent-task-workflow.md`

**Interfaces:**
- Produces: `queue init`, `queue acquire`, `queue transition`, and `queue status`.
- States: `writing`, `testing`, `review_wait`, `blocked`, `done`.
- A second writer cannot acquire an active queue.

- [x] Add a self-test proving a second acquire fails, invalid transitions fail, and completion releases the writer slot.
- [x] Run the self-test and observe RED because `queue` is unavailable.
- [x] Implement atomic lock-file coordination and transition validation.
- [x] Run the self-test and observe GREEN.

### Task 5: Test resource classification

**Files:**
- Modify: `scripts/checks/codex-workflow.py`
- Modify: `scripts/checks/codex-workflow-policy.v1.json`
- Modify: `scripts/checks/codex-workflow-self-test.sh`
- Modify: `AGENTS.md`
- Modify: `guide/dev/testing.md`

**Interfaces:**
- Produces: `classify` JSON grouped into `static-parallel`, `dotnet-serial`, and `fixture-exclusive`, with execution batches ordered from safe parallel work to exclusive work.

- [x] Add a self-test covering a Markdown contract, C# source, and Bukit fixture/build-manifest path.
- [x] Run the self-test and observe RED because `classify` is unavailable.
- [x] Implement ordered policy classification and closure command resource labels.
- [x] Run the self-test and observe GREEN.

### Task 6: Speed metrics

**Files:**
- Modify: `scripts/checks/codex-workflow.py`
- Modify: `scripts/checks/codex-workflow-self-test.sh`
- Modify: `AGENTS.md`
- Modify: `guide/dev/agent-task-workflow.md`

**Interfaces:**
- Produces: `metrics add` and `metrics report`.
- Tracks phase durations, cache hits/misses, duplicate command labels, reruns, conflicts, and task totals without storing raw commands or secrets.

- [x] Add a self-test with implementation, test, review, and idle events plus a duplicate command label and cache hit/miss.
- [x] Run the self-test and observe RED because `metrics` is unavailable.
- [x] Implement atomic event persistence and deterministic summaries.
- [x] Run the self-test and observe GREEN.

### Task 7: Direct owner routing and completion

**Files:**
- Modify: `scripts/checks/post-change-focused-owner-checks.sh`
- Modify: `scripts/checks/post-change-focused-owner-checks-self-test.sh`
- Modify: `scripts/checks/agent-governance-contract.sh`

**Interfaces:**
- Maps every Codex workflow tool, policy, governance document, and self-test to `codex-workflow-self-test.sh` or `agent-governance-contract.sh`.

- [x] Add failing owner-routing and governance assertions for the new workflow.
- [x] Run the two direct self-tests and observe RED.
- [x] Implement the owner mappings and new governance contract.
- [x] Run only:
  - `bash scripts/checks/codex-workflow-self-test.sh`
  - `bash scripts/checks/post-change-focused-owner-checks-self-test.sh`
  - `bash scripts/checks/agent-governance-contract.sh`
- [x] Perform one final review limited to this task's changed files and verification evidence.
