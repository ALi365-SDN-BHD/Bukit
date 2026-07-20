# Bukit Core Public API Inventory Audit Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a current, evidence-backed inventory of every exported Bukit Core CLR type, distinguish supported contracts from implementation-public surface, and define non-mutating 1.x/2.0 governance batches.

**Architecture:** Build the authoritative type list from the twelve Release assemblies, map types back to current source declarations where possible, and collect repository-local consumer evidence across Core, Labs, plugins, tests, docs, reflection, and serialization. Publish one Chinese audit report plus one machine-readable JSON snapshot; do not change production code or compatibility surfaces.

**Tech Stack:** .NET 10 reflection, C# source inspection, project-reference analysis, ripgrep, JSON, Markdown, repository documentation gates.

## Global Constraints

- Do not change public/internal access modifiers, namespaces, assemblies, project references, configuration schema, plugin protocol, report schema, persisted formats, or runtime behavior.
- Treat exported CLR visibility, serialized/wire compatibility, CLI behavior, and documented product promises as separate compatibility dimensions.
- Every inventoried exported type must have an owner, classification, compatibility level, and consumer-evidence summary.
- Lack of repository-local consumers is not proof that an API is safe to remove.
- `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, and `scripts-0.2/` are historical evidence only and must not drive current classifications.
- No access-level change may be proposed for 1.x without an explicit external-consumer and reflection/AOT risk review.

---

### Task 1: Establish the compiled and source baselines

**Files:**
- Create: `/tmp/bukit-g01-public-api/` temporary analysis tooling and raw evidence
- Create: `docs/analysis/bukit-core-public-api-inventory-2026-07-20.json`

**Interfaces:**
- Consumes: the twelve projects under `src/Bukit-Core/` and their Release assemblies
- Produces: a deterministic list of exported types, public declared members, source locations, assembly names, and source/compiled reconciliation status

- [x] Enumerate the twelve Core projects and current project-reference edges.
- [x] Build `bukit-core.slnx` in Release without changing repository sources.
- [x] Extract every `Assembly.GetExportedTypes()` result and its declared public/protected member counts.
- [x] Parse current C# declarations to map namespace, type name, kind, source file, and line.
- [x] Reconcile generated-only, source-only, nested, record-generated, and load-failure cases explicitly.
- [x] Record the baseline commit and exact extraction commands in the report.

### Task 2: Collect consumer and contract evidence

**Files:**
- Modify: `docs/analysis/bukit-core-public-api-inventory-2026-07-20.json`

**Interfaces:**
- Consumes: Task 1 type inventory
- Produces: repository-local consumer counts and evidence flags used by classification

- [x] Count lexical identifier references separately in Core, Labs, plugins, tests, active docs, and historical docs; keep their confidence below semantic symbol evidence.
- [x] Record assembly/project-reference consumers independently from lexical symbol evidence.
- [x] Flag serialization, source-generation, reflection, P/Invoke, CLI registration, configuration, report, and plugin-wire evidence.
- [x] Treat ambiguous short-name matches as weak evidence and record them rather than silently upgrading their confidence.
- [x] Inspect externally meaningful assemblies and namespaces manually where automated evidence cannot determine support intent.

### Task 3: Classify every exported type

**Files:**
- Modify: `docs/analysis/bukit-core-public-api-inventory-2026-07-20.json`

**Interfaces:**
- Consumes: compiled/source inventory and consumer evidence
- Produces: owner, classification, compatibility level, evidence confidence, and migration horizon for each exported type

- [x] Assign exactly one primary classification: `supported-sdk`, `plugin-wire-contract`, `serialized-contract`, `documented-cli-contract`, `aot-serialization-surface`, `cross-assembly-implementation`, `implementation-public`, `persisted-internal-format`, or `unresolved-owner-review`.
- [x] Assign compatibility level: `1.x-supported`, `1.x-shape-stable`, `1.x-do-not-narrow`, `1.x-migration-safe`, `2.0-candidate`, or `not-a-clr-contract`.
- [x] Assign an owning module and cite at least one source, project-reference, test, documentation, or explicit absence-of-evidence basis.
- [x] Keep all unresolved cases visible; do not force unsupported certainty.
- [x] Validate that every exported type has all mandatory fields and that aggregate totals reconcile with assembly totals.

### Task 4: Produce the formal G-01 audit report

**Files:**
- Create: `docs/analysis/bukit-core-public-api-inventory-audit-2026-07-20.zh-CN.md`

**Interfaces:**
- Consumes: final JSON inventory
- Produces: management conclusion, architecture findings, compatibility policy, and ordered non-mutating follow-up batches

- [x] Document scope, methodology, baseline commit, evidence limitations, and reconciliation results.
- [x] Provide assembly and classification matrices, supported-contract lists, unresolved cases, and public-surface hotspots.
- [x] Separate CLR API compatibility from YAML/JSON/report/plugin/CLI compatibility.
- [x] Define safe 1.x governance actions, 2.0-only narrowing candidates, migration prerequisites, and explicit no-action areas.
- [x] Decide whether AD-04 remains confirmed, narrows, or closes; do not infer that any type can be removed merely from zero local references.
- [x] Recommend the next single architecture task only after the inventory evidence is complete.

### Task 5: Validate and strictly review the deliverables

**Files:**
- Verify: `docs/analysis/bukit-core-public-api-inventory-2026-07-20.json`
- Verify: `docs/analysis/bukit-core-public-api-inventory-audit-2026-07-20.zh-CN.md`
- Verify: `docs/superpowers/plans/2026-07-20-bukit-core-public-api-inventory.md`

**Interfaces:**
- Consumes: all G-01 deliverables
- Produces: evidence that the inventory is complete, internally consistent, linked, and free of scope drift

- [x] Validate JSON parsing, schema identity, mandatory fields, unique assembly/type keys, and aggregate totals.
- [x] Re-run extraction and compare the regenerated exported-type identity set with the committed snapshot.
- [x] Run active documentation consistency and `git diff --check`.
- [x] Scan for placeholders, absolute local paths, backup-tree authority claims, accidental API-change language, and unsupported removal claims.
- [x] Review every conclusion against the no-code/no-contract-change boundary and record remaining evidence limitations.
- [x] Commit only the plan, JSON inventory, and Chinese audit report on `codex/g01-public-api-inventory`.
