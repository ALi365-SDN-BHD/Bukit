# G-04B3 Consumer Window Checkpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a source-backed G-04B3 checkpoint that determines whether Bukit's 2.0 public-surface consumer declaration window is eligible for closure review without changing any governed contract or external state.

**Architecture:** Treat the current governed baseline, candidate manifest, active guide, GitHub Issue #60, GitHub Releases API, and candidate-level authenticated code search as separate evidence sources. Reconcile them in one analysis report. Stop before eligibility, window closure, or G-04C whenever any lifecycle prerequisite or evidence refresh remains incomplete.

**Tech Stack:** Markdown, JSON/jq, Git, GitHub REST API, repository documentation gates.

## Global Constraints

- Do not modify Core, Labs, plugins, tests, public CLR access levels, API signatures, schema, plugin protocol, persisted formats, or project references.
- Do not modify `docs/governance/bukit-core-public-api-baseline.v1.json` or `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` unless the stable-release prerequisite, feedback disposition, authenticated candidate search refresh, and independent evidence review are all complete.
- Do not close, comment on, relabel, or otherwise mutate GitHub Issue #60.
- Do not treat zero comments, zero public search matches, elapsed calendar time, RC tags, or pre-window releases as proof of no consumers.
- Do not authorize G-04C or any access-level change.
- Use only official GitHub API evidence for current Issue and release state; record retrieval time and evidence limitations.
- The final status must reflect the first unmet gate: `waiting-stable-release`, `feedback-disposition-required`, or `post-stable-evidence-refresh-required`. Eligibility may be recorded only when every gate is proven complete.

---

### Task 1: Produce the G-04B3 precondition checkpoint

**Files:**
- Create: `docs/analysis/bukit-core-g04b3-consumer-window-checkpoint-2026-07-22.zh-CN.md`
- Modify: none outside this plan and the new report
- Test: active documentation links, public documentation contracts, absolute-path scan, and post-change focused owner checks

**Interfaces:**
- Consumes: current public API baseline, 136-candidate manifest, G-04B2 declaration documents, GitHub Issue #60, GitHub Releases API, current Git history.
- Produces: a formal checkpoint with lifecycle status, exact release/feedback evidence, candidate reconciliation, blocker, allowed next action, and forbidden actions.

- [ ] **Step 1: Capture current repository evidence**

Record current `main` commit, baseline type count and compatibility counts, candidate count/state, candidate identity reconciliation hash, `windowPolicy`, current drift-gate evidence, and the timestamp of the last authenticated candidate search.

- [ ] **Step 2: Capture current official GitHub evidence**

Read Issue #60 and its comments plus the repository releases endpoint. Record Issue state/comment count and list releases relevant to the window-opening timestamp. Do not send any write request.

- [ ] **Step 3: Write the checkpoint report**

The report must classify the current lifecycle from live evidence. It must distinguish `v1.0.10-rc.1` from a qualifying stable release, classify every Issue comment without treating acknowledgements as consumer evidence, distinguish governance containment from actual surface narrowing, document the 472-to-476 baseline evolution without reopening the candidate set, identify whether authenticated candidate searches are fresh enough for closure, and define the exact resume conditions. If the authenticated search cannot be refreshed, the status is `post-stable-evidence-refresh-required` and governed lifecycle fields remain unchanged.

- [ ] **Step 4: Verify documentation ownership and scope**

Run:

```bash
bash scripts/checks/docs/public-doc-contracts.sh
bash scripts/checks/docs/public-absolute-paths.sh
bash scripts/checks/docs/active-links.sh
bash scripts/checks/post-change-focused.sh -- docs/superpowers/plans/2026-07-22-g04b3-consumer-window-checkpoint.md docs/analysis/bukit-core-g04b3-consumer-window-checkpoint-2026-07-22.zh-CN.md
git diff --check
```

Expected: all commands exit `0`. Do not run full, release, whole-solution, or aggregate targeted gates.

- [ ] **Step 5: Commit for independent review**

Commit only the plan and checkpoint report with message:

```text
docs(governance): checkpoint G-04B3 eligibility
```

Then request an independent read-only review of source accuracy, lifecycle logic, scope, and non-authorization language.
