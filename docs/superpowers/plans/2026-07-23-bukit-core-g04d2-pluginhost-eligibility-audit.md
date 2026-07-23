# Bukit Core G-04D2 PluginHost Eligibility Audit Plan

> Status: read-only architecture audit. This task may add only this plan and the
> final audit report. It must not change runtime code, CLR visibility, the
> public API baseline, the closed consumer manifest, plugin protocol, schemas,
> configuration, CI, release, or gate behavior.

**Goal:** Determine whether the 16 governed `Bukit.PluginHost`
`2.0-candidate` CLR identities are eligible for a controlled 2.0 narrowing
batch, identify every public-signature and product-contract blocker, and
recommend the next single bounded task.

**Base:** `2.0@21072f4f45fdb23c0f3a95f03c837c1dab4665b5`

**Report:**
`docs/analysis/bukit-core-g04d2-pluginhost-process-helper-facade-eligibility-audit-2026-07-23.zh-CN.md`

## Scope

- Inventory all 40 current public `Bukit.PluginHost` types and isolate the 16
  governed candidates.
- Trace the live path from CLI composition through config, validation,
  process execution, protocol calls, error reporting, permission checks, and
  execution-report persistence.
- Check candidate propagation through public constructors, methods,
  interfaces, records, enums, and retained non-candidate types.
- Check Core, Labs, official-plugin, test, active-doc, reflection,
  serialization, Native AOT, and consumer-declaration evidence.
- Classify each candidate as:
  - eligible for an independent 2.0 narrowing task;
  - conditionally eligible after a specific migration contract;
  - blocked by a public-signature graph;
  - blocked by a persisted/report/wire contract;
  - retain/freeze.
- Define atomic clusters, stop conditions, compatibility costs, and the next
  recommended implementation order.

## Non-goals

- No access-level, namespace, type, member, constructor, or assembly change.
- No `InternalsVisibleTo`, facade, factory, adapter, or contracts-assembly
  implementation.
- No plugin JSON field, `bukit-plugin-v1`, error-code value, report shape,
  config default, path rule, timeout, output limit, permission, masking, or
  process behavior change.
- No public API baseline or closed 136-entry manifest update.
- No GitHub issue, release, declaration-channel, or external state mutation.
- No full/release gate, `test-all`, `smoke-all`, whole-solution test, Native
  AOT publish, or release package smoke.

## Evidence checklist

- [x] Independent worktree created from local `2.0`.
- [x] Baseline `Bukit.PluginHost.Tests` passes.
- [x] Baseline `PluginBoundaryTests` passes.
- [x] Current public API baseline and closed candidate manifest counted and
  hashed.
- [x] All 16 candidate identities and their public/protected members captured.
- [x] Same-assembly implementation use and cross-assembly source use traced.
- [x] Candidate propagation through retained public signatures traced.
- [x] CLI default composition and formal external-process boundary traced.
- [x] Report writer, report DTO, masking, and documented output path traced.
- [x] Error-code values compared with active protocol documentation.
- [x] Reflection, source-generated serialization, and Native AOT hooks
  searched.
- [x] Authenticated public-search evidence and private-consumer limitations
  reconciled from the closed manifest.
- [x] Final report written.
- [ ] Docs focused check passed.
- [ ] Parent `post-change-targeted.sh` aggregate passed exactly once for the
  two-document diff.
- [ ] Independent read-only whole-diff review completed.

## Required conclusion shape

The final report must state:

1. whether all 16 candidates can be narrowed together;
2. the exact per-type and per-cluster eligibility;
3. which retained public types or members block candidate narrowing;
4. whether execution-report JSON is an implementation detail or a currently
   documented product artifact;
5. whether any candidate is a safe next single-type task;
6. the required order and stop conditions for later implementation;
7. why this audit is not itself removal/internalization authorization.

## Verification

Baseline:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off

dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~PluginBoundaryTests
```

After the two documents are added:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2-pluginhost-eligibility-audit.md \
  docs/analysis/bukit-core-g04d2-pluginhost-process-helper-facade-eligibility-audit-2026-07-23.zh-CN.md
```

At parent-task completion, run exactly one aggregate:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 21072f4f45fdb23c0f3a95f03c837c1dab4665b5 -- \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2-pluginhost-eligibility-audit.md \
  docs/analysis/bukit-core-g04d2-pluginhost-process-helper-facade-eligibility-audit-2026-07-23.zh-CN.md
```

The independent review must be read-only and must not rerun the aggregate.
