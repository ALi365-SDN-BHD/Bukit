# Bukit Core G-04B3 Closure Convergence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconcile the version-controlled G-04B3 consumer-declaration lifecycle with the already-closed GitHub Issue #60 without authorizing or implementing any G-04C public-surface change.

**Architecture:** Treat the candidate manifest as the machine-readable lifecycle source, the active declaration and maintainer guide as its human-readable projections, and a new convergence report as the immutable execution ledger. Preserve the earlier eligibility audit as a point-in-time record and preserve every candidate-level status because closing the declaration channel does not prove that private consumers do not exist.

**Tech Stack:** JSON, Markdown, `jq`, repository documentation gates, GitHub public REST evidence.

## Global Constraints

- Work only on `codex/g04b3-closure-convergence` in `.worktrees/g04b3-closure-convergence`.
- Do not modify `src/Bukit-Core/`, tests, fixtures, the public API baseline, access levels, schemas, plugin protocols, serialized formats, or project references.
- Do not modify the 136 candidate identities, classifications, compatibility values, migration horizons, proposed actions, external search evidence, `declarationStatus`, or `privateConsumerStatus` values.
- Set only the existing lifecycle fields to `declarationState = closed`, `feedbackChannel.state = closed`, and `windowPolicy.eligibleAfterRelease = v1.0.10`; do not invent a new manifest field or schema version.
- Record Issue #60's observed close event as `2026-07-22T07:08:31Z` by actor `ClrsDream`; do not claim that the Issue closure itself authorizes G-04C.
- Keep all 1.x CLR visibility unchanged and state that G-04C requires a separate, single-type, 2.0-only decision.
- Preserve the historical G-04B3 eligibility audit unchanged; add a new closure-convergence report instead of rewriting its point-in-time findings.
- Do not push, reopen, comment on, label, or otherwise mutate GitHub in this local task.
- After the documentation subtask run `bash scripts/checks/post-change-focused.sh -- <changed paths>` once; at parent-task completion run `bash scripts/checks/post-change-targeted.sh --base 82485e4efef5357c5560733c0dc3e758f0b93eaf -- <all changed paths>` exactly once.

---

### Task 1: Governed Lifecycle And Human-Readable Projection

**Files:**
- Modify: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `guide/dev/public-api-governance.md`
- Create: `docs/analysis/bukit-core-g04b3-closure-convergence-2026-07-22.zh-CN.md`

**Interfaces:**
- Consumes: the existing 136-candidate manifest, the G-04B3 eligibility audit, GitHub Issue #60 close event, and stable release `v1.0.10`.
- Produces: one consistent local lifecycle state and one immutable convergence ledger for later merge/publication verification.

- [ ] **Step 1: Capture pre-change invariants**

Run:

```bash
jq -S '[.candidates[] | {assembly,fullName,classification,compatibility,migrationHorizon,declarationStatus,proposedAction,privateConsumerStatus,externalEvidence}]' \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json | shasum -a 256
```

Record the hash and require the same hash after the lifecycle edit.

- [ ] **Step 2: Change only existing manifest lifecycle fields**

Apply these exact transitions:

```text
declarationState: "open" -> "closed"
feedbackChannel.state: "open" -> "closed"
windowPolicy.eligibleAfterRelease: null -> "v1.0.10"
```

Do not add `closedAtUtc` or any other new JSON field. Do not modify `candidates`.

- [ ] **Step 3: Close the active declaration projection**

Update the active declaration so that it:

```text
Status: `closed`
Issue close event: `2026-07-22T07:08:31Z`
Eligible stable release: `v1.0.10`
Candidate-level state: 136 entries remain consumer-declaration-pending and private-consumer status remains unknown
Authorization boundary: closure permits only G-04C eligibility discussion; G-04C requires separate authorization and remains 2.0-only and single-type
```

Retain the feedback instructions as historical context, but state that new evidence must be handled in a separately opened channel or task.

- [ ] **Step 4: Update the maintainer governance guide**

Replace the open-window projection with the closed lifecycle, exact release and Issue close evidence, and the separate G-04C authorization boundary. Keep the warning that `no-public-match-found` cannot prove absence of private consumers.

- [ ] **Step 5: Add the convergence ledger**

Create the report with:

```text
local base: main@82485e4efef5357c5560733c0dc3e758f0b93eaf
remote main observed: 3ceb096a3ae2cdff145a49798460671261968b04
Issue #60: closed at 2026-07-22T07:08:30Z; close event at 2026-07-22T07:08:31Z by ClrsDream
v1.0.10: draft=false, prerelease=false, published_at=2026-07-22T04:24:34Z
comments: 2, both already classified, no candidate-level CLR reference declared
scope: lifecycle/documentation convergence only
publication state: local convergence is not public convergence until merged and published
G-04C state: not authorized by this task
```

Include pre/post invariant hashes and a file-by-file scope ledger.

- [ ] **Step 6: Verify exact lifecycle and preserved candidates**

Run:

```bash
jq -e '
  .declarationState == "closed" and
  .feedbackChannel.state == "closed" and
  .windowPolicy.eligibleAfterRelease == "v1.0.10" and
  (.candidates | length) == 136 and
  all(.candidates[];
    .declarationStatus == "consumer-declaration-pending" and
    .privateConsumerStatus == "unknown-until-voluntary-declaration")
' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Recompute the Step 1 projection hash and require an exact match.

- [ ] **Step 7: Run the focused affected-path gate**

Run exactly once:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04b3-closure-convergence-2026-07-22.zh-CN.md
```

Expected: exit `0`.

- [ ] **Step 8: Commit the lifecycle convergence**

```bash
git add \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04b3-closure-convergence-2026-07-22.zh-CN.md
git commit -m "docs(governance): converge G-04B3 closure state"
```

---

### Task 2: Aggregate Verification And Independent Read-Only Review

**Files:**
- Verify: `docs/superpowers/plans/2026-07-22-bukit-core-g04b3-closure-convergence.md`
- Verify: all Task 1 files

**Interfaces:**
- Consumes: the complete branch diff from base `82485e4efef5357c5560733c0dc3e758f0b93eaf`.
- Produces: aggregate gate evidence and a read-only scope/correctness verdict suitable for a later merge decision.

- [ ] **Step 1: Run aggregate targeted verification once**

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 82485e4efef5357c5560733c0dc3e758f0b93eaf -- \
  docs/superpowers/plans/2026-07-22-bukit-core-g04b3-closure-convergence.md \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04b3-closure-convergence-2026-07-22.zh-CN.md
```

Expected: exit `0`.

- [ ] **Step 2: Run final hygiene checks**

```bash
git diff --check 82485e4efef5357c5560733c0dc3e758f0b93eaf..HEAD
git status --short
```

Expected: no whitespace errors and no uncommitted tracked changes.

- [ ] **Step 3: Independent read-only review**

The reviewer must verify:

```text
Critical/Important/Minor findings are each counted explicitly.
Only the five planned documentation/governance paths changed.
The historical eligibility audit did not change.
Candidate projection hash is unchanged.
Manifest, declaration, guide, and convergence report agree on the closed lifecycle.
No text claims that private consumers are absent or that G-04C is approved.
No Core, schema, protocol, baseline, access-level, candidate identity, or per-candidate status changed.
Local completion is not represented as public remote convergence.
```

- [ ] **Step 4: Record completion without merging or publishing**

Report the branch name, commit range, verification results, reviewer verdict, and the remaining requirement to merge/publish and then verify remote hashes. Do not merge to `main`, push, or mutate Issue #60 in this task.
