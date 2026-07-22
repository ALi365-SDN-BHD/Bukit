# G-04B3 Eligibility And Consumer Window Closure Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Determine whether the Bukit Core 2.0 consumer declaration has enough current evidence to request closure, without changing the governed lifecycle or GitHub state.

**Architecture:** Reconcile the current local governance baseline, the published GitHub default branch, Issue #60 and all comments, stable releases, the post-stable authenticated search refresh, and the three declared process/CLI consumers as independent evidence axes. Separate evidence sufficiency from publication consistency and explicit closure authority.

**Tech Stack:** Markdown, JSON/jq, Git, authenticated GitHub connector, GitHub REST API, repository documentation and public-API gates.

## Global Constraints

- Do not modify Core, Labs, plugins, tests, public CLR access levels, API signatures, schema, plugin protocol, persisted formats, project references, or candidate identities.
- Do not modify the public API baseline, candidate manifest, consumer declaration, Issue #60, releases, labels, comments, or any other external state.
- Do not infer that `no-public-match-found` proves there are no private, unindexed, copied, reflected, serialized, inherited, or Native AOT consumers.
- Do not classify a packaged Bukit executable as a site project's direct CLR reference merely because product implementation symbols remain in the binary.
- Do not set `eligibleAfterRelease`, close the declaration window, or authorize G-04C.
- Treat the live GitHub default branch and local `main` as separate evidence sources whenever their commits differ.
- A closure recommendation requires current release evidence, complete feedback disposition, post-stable authenticated candidate evidence, public governance-state convergence, an independent read-only review, and separate explicit approval.

---

### Task 1: Produce The Independent Closure Audit

**Files:**
- Create: `docs/analysis/bukit-core-g04b3-eligibility-window-closure-audit-2026-07-22.zh-CN.md`
- Modify: none outside this plan and the new report
- Test: documentation owner checks, public API drift, aggregate targeted diff, and independent read-only review

**Interfaces:**
- Consumes: current local baseline and candidate manifest; G-04B2/G-04B3 reports; Issue #60 and all comments; release metadata; authenticated search evidence; the current state of SRBiz-bukit, sitegen, and ALi365WebSiteBuilder.
- Produces: one time-bounded verdict that distinguishes evidence readiness, public publication consistency, closure authorization, and G-04C authorization.

- [ ] **Step 1: Capture the current local governance state**

Record the exact local `main` commit, public API count and compatibility distribution, 136-candidate identity hash, declaration lifecycle, action distribution, authenticated search timestamps, public API drift result, and relevant document hashes.

- [ ] **Step 2: Capture current official GitHub evidence**

Use authenticated read-only GitHub access for Issue #60, all comments, and current default-branch commits. Use the official releases REST endpoint for release metadata. Record retrieval timestamps and do not perform any write action.

- [ ] **Step 3: Reconcile local and published governance state**

Compare local `main` with the live GitHub default branch. Record candidate identity equality separately from differences in baseline count, search evidence, proposed actions, and reports. Treat a stale public declaration target as a publication-consistency blocker, not as candidate evidence.

- [ ] **Step 4: Recheck declared consumers**

Recheck SRBiz-bukit, sitegen, and ALi365WebSiteBuilder for project files, source files, packaged assemblies or executables, exact candidate names, and build commands. Preserve the boundary between product/CLI consumption and direct CLR candidate consumption.

- [ ] **Step 5: Write the bounded verdict**

The report must state whether release, feedback, and authenticated-search evidence gates are satisfied; whether public state is synchronized; whether closure is authorized; the exact next sequence; and why G-04C remains out of scope. It must not mutate any governed field.

- [ ] **Step 6: Verify the report**

Run:

```bash
bash scripts/checks/docs/public-doc-contracts.sh
bash scripts/checks/docs/no-absolute-paths.sh
bash scripts/checks/docs/active-links.sh
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
bash scripts/checks/post-change-focused.sh -- \
  docs/superpowers/plans/2026-07-22-g04b3-eligibility-window-closure-audit.md \
  docs/analysis/bukit-core-g04b3-eligibility-window-closure-audit-2026-07-22.zh-CN.md
git diff --check
```

Expected: all commands exit 0. At parent completion, run `post-change-targeted.sh` exactly once against the parent base and these two paths. Do not run full, release, `test-all`, `smoke-all`, or whole-solution tests.

- [ ] **Step 7: Obtain independent read-only review and commit**

The reviewer must verify source accuracy, lifecycle logic, local/remote divergence, consumer classifications, non-authorization language, and change scope. Resolve all findings before committing only the plan and report with:

```text
docs(governance): audit G-04B3 closure eligibility
```
