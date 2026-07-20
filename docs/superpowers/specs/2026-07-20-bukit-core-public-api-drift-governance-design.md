# Bukit Core Public API Drift Governance Design

Date: 2026-07-20

Status: Approved design for G-02 implementation

## 1. Purpose

G-01 established the current Bukit Core exported CLR surface: twelve Core
assemblies, 472 exported types, and distinct compatibility responsibilities for
wire, serialized, AOT, cross-assembly, implementation-public, and internal
persisted-format types.

G-02 turns that inventory into an enforceable drift-governance mechanism. It
must detect changes to the compiled Release public surface, explain the review
class that applies, and prevent an unreviewed baseline change from silently
passing CI.

G-02 is governance infrastructure. It does not reduce the current public
surface and does not create a supported CLR SDK.

## 2. Scope

G-02 will:

1. Promote the rule `CLR public visibility != supported Bukit SDK` into active
   English and Chinese compatibility governance.
2. Replace the stale `Source-generated plugin SDK` contract-matrix row with the
   implemented process-protocol DTO and static JSON serialization boundary.
3. Create a checked-in, deterministic baseline of the twelve Core Release
   assemblies.
4. Detect type-shape, public-member, protected-member, classification, and
   compatibility drift.
5. Give additive, removal, protected, wire/serialized, AOT, and unclassified
   changes distinct diagnostics.
6. Wire both mutation self-tests and the real assembly check into `ci-fast`.
7. Provide a safe snapshot command that can only write an explicit candidate
   file and cannot overwrite the governed baseline.

G-02 will not:

- change any Core access modifier, namespace, type, member, or assembly name;
- change any Core project reference;
- change `site.yaml`, `theme.yaml`, report, plugin, or persistence schemas;
- change `bukit-plugin-v1` fields or behavior;
- publish a NuGet package or declare a CLR SDK;
- automatically accept or rewrite a baseline after drift;
- infer that an implementation-public type is safe to remove;
- perform the later 2.0 API-narrowing work.

The new baseline JSON schema is a governance-artifact schema, not a Bukit user
configuration, report, plugin, or persistence contract.

## 3. Considered Approaches

### 3.1 Selected: deterministic reflection snapshot with classification-aware comparison

A repository-owned .NET console tool captures the compiled Release surface and
compares it with a checked-in JSON baseline. The baseline carries the G-01
classification and compatibility level for every exported type. The comparer
uses those values to emit review-specific diagnostics.

Advantages:

- uses compiled assembly truth rather than source-text approximation;
- has no external package dependency;
- can distinguish Bukit-specific contract families;
- can self-test comparison and failure semantics with small fixtures;
- does not inject analyzers into all twelve production projects;
- keeps acceptance as an explicit, reviewable baseline diff.

Trade-offs:

- Bukit owns the canonical signature formatter;
- the initial implementation is a governance drift detector, not a complete
  replacement for .NET binary compatibility tools;
- reflection metadata must be kept deterministic across supported CI hosts.

### 3.2 Rejected: Microsoft.CodeAnalysis.PublicApiAnalyzers shipped files

This would provide mature compiler diagnostics but would add an analyzer
dependency to all Core projects, produce large per-project text surfaces, and
would not natively distinguish Bukit's wire, serialized, AOT, and
implementation-public review policies.

### 3.3 Rejected: type-only hash derived from the dated G-01 inventory

This would be small and easy to implement but would miss public/protected member
drift and would promote a dated analysis artifact into an active gate input.

## 4. Repository Structure

The implementation will use these focused units:

```text
tools/Bukit.PublicApiDrift/
  Bukit.PublicApiDrift.csproj
  Program.cs
  ApiSurfaceModels.cs
  ApiSurfaceCapture.cs
  ApiSignatureFormatter.cs
  ApiSurfaceComparer.cs
  BaselineFile.cs

scripts/checks/
  public-api-drift.sh
  public-api-drift-self-test.sh

tests/fixtures/public-api-drift/
  baseline.json
  unchanged.json
  additive.json
  removal.json
  protected-change.json
  stable-contract-change.json
  malformed.json

docs/governance/
  bukit-core-public-api-baseline.v1.json

docs/schemas/
  bukit-core-public-api-baseline.v1.schema.json

guide/dev/
  public-api-governance.md
```

Existing files updated by G-02:

- `bukit-core.slnx` includes the standalone tool under a tools folder so the
  existing Core restore/build path prepares it without introducing a Core
  project reference.
- `scripts/gates/ci-fast.sh` runs the mutation self-test and real drift check.
- `scripts/checks/docs/public-doc-contracts.sh` requires the active governance
  page, baseline, and governance schema.
- `docs/compatibility-governance.md` and
  `docs/compatibility-governance.zh-CN.md` receive matching policy entries.
- `docs/bukit-1.0-contract-matrix.zh-CN.md` removes the nonexistent plugin SDK
  claim.
- `guide/dev/documentation-governance.md` links the new CLR governance surface.

No file in a backup/reference directory is read as gate authority or modified.

## 5. Baseline Model

The baseline root contains:

- a fixed schema identifier and schema version;
- the target framework (`net10.0`);
- the exact expected assembly/project mapping for all twelve Core projects;
- policy metadata stating that no general CLR SDK is currently declared;
- one sorted entry for every exported type.

Each type entry contains:

- assembly name and full CLR type name;
- G-01 owner, classification, compatibility, and migration horizon;
- a canonical type signature;
- sorted declared public-member signatures;
- sorted declared protected/protected-internal member signatures.

The committed baseline must reject:

- missing or extra assembly mappings;
- duplicate assemblies, types, or member signatures;
- unknown classification or compatibility values;
- `unresolved-owner-review` or `review-required` entries;
- unsorted arrays or non-canonical JSON formatting;
- missing source project paths;
- a baseline whose schema identifier does not match v1.

The initial baseline classifications come from the reviewed G-01 inventory, but
the active gate does not load the dated G-01 report at runtime.

## 6. Canonical Compiled Surface

The capture tool loads the exact Release DLL for every baseline project. It
fails closed when an assembly is absent, duplicated, unloadable, or has an
unexpected assembly name.

The type signature records:

- top-level or nested visibility;
- class, struct, interface, enum, or delegate kind;
- static, abstract, sealed, and generic arity state;
- base type and implemented interfaces;
- generic parameter variance and constraints;
- enum underlying type.

Declared member signatures cover:

- constructors;
- ordinary methods and operators, excluding property/event accessors as
  duplicate representations;
- properties with getter/setter accessibility;
- fields, including enum values and public constants;
- events with add/remove accessibility.

Signatures include canonical parameter and return types, nullable annotations
reported by `NullabilityInfoContext`, ref/in/out state, generic arity and
constraints, optional/default values using invariant formatting, and
static/abstract/virtual/final state where applicable. Unknown nullability is
encoded explicitly rather than guessed. Type names use assembly-independent CLR
names so output paths and build roots cannot affect the baseline.

Protected coverage includes `protected` and `protected internal`; it excludes
`private protected`, which is not visible to a derived type in another
assembly. Nested exported types are represented as their own type entries.

The first version intentionally does not treat arbitrary custom-attribute
changes as CLR drift. Wire names, YAML keys, and product schema behavior remain
governed by their existing schema/contract tests. The formatter may include a
small explicit attribute allowlist later only through a separate reviewed
baseline version.

## 7. Drift Policy

Every detected drift returns a non-zero process status until the baseline is
explicitly reviewed and updated. Diagnostics distinguish policy meaning rather
than describing every change as breaking.

| Change | Diagnostic | Gate result |
|---|---|---|
| Removed exported type | `breaking` | fail |
| Removed public member | `breaking` | fail |
| Public member signature replacement | `breaking` plus `review-required` for the replacement | fail |
| Added public type/member | `review-required` | fail |
| Added/removed/changed protected member | `protected-review` | fail |
| Type shape changed | `type-shape-review` | fail |
| Any drift in `plugin-wire-contract` or `serialized-contract` | add `contract-shape-review` | fail |
| Any drift in `aot-serialization-surface` | add `aot-review` | fail |
| New type without an approved classification | `unclassified` | fail |
| Missing/unloadable assembly or invalid baseline | `gate-error` | fail closed |
| Exact match | no diagnostic | pass |

`implementation-public` additions are not called breaking. They still fail
until reviewed because G-02 exists to prevent silent public-surface growth.

The gate does not decide whether a wire or YAML change is protocol-compatible.
It points maintainers to the corresponding protocol/schema tests and requires
those product-contract checks to remain authoritative.

## 8. Commands and Exit Semantics

The shell entry point is:

```text
bash scripts/checks/public-api-drift.sh check [Configuration]
bash scripts/checks/public-api-drift.sh snapshot OUTPUT [Configuration]
```

`check` performs these operations:

1. validate the baseline file and governance schema identity;
2. build `bukit-core.slnx` using the requested configuration and
   `--no-restore`;
3. capture the twelve compiled assemblies;
4. compare the captured surface with the baseline;
5. print deterministic, sorted diagnostics.

`snapshot` performs the same build and capture, preserves classifications for
known types, marks new types as unresolved in the candidate, and writes only
to the explicit `OUTPUT` path. It refuses:

- an output path equal to the governed baseline;
- an existing output path;
- an output path outside the repository or system temporary directory;
- a missing or empty output argument.

The snapshot command never stages, commits, or copies a candidate into the
governed baseline.

The .NET tool also exposes a fixture-only `compare BASELINE CURRENT` command so
the self-test can exercise policy branches without rebuilding Core repeatedly.

Exit codes:

- `0`: exact match;
- `1`: valid inputs with one or more review-required drift diagnostics;
- `2`: invalid arguments, malformed/non-canonical baseline, unsafe snapshot
  output, missing build output, or assembly-load failure.

No command emits a stack trace by default. Gate errors include the failed
assembly/path and a bounded innermost exception type/message without leaking
environment variables or file contents.

## 9. CI Integration

`ci-fast` receives two ordered steps:

1. `public API drift self-test` runs fixture comparisons and asserts expected
   exit codes/diagnostic classes, plus the exact `ci-fast` wiring line.
2. `public API drift` executes the real Release/selected-configuration check.

The GitHub CI and release jobs already restore `bukit-core.slnx` before running
`ci-fast`; adding the tool to that solution keeps `--no-restore` viable. Local
callers without restored assets receive a clear gate error and must restore the
same solution rather than letting the checker perform hidden network access.

Because `release.sh` calls `ci-fast`, the same drift check applies to release
qualification without adding a second implementation path.

The actual check must be wired into `ci-fast`; a standalone green self-test is
not sufficient closure evidence.

## 10. Documentation Governance

The new active guide states:

- C# `public` is CLR visibility, not an automatic support promise;
- currently supported external surfaces are CLI behavior, configuration/theme
  shapes, template objects, report schemas, and process-plugin contracts;
- no general-purpose Bukit Core CLR SDK is currently distributed;
- any baseline update requires an owner, classification, compatibility level,
  reason, and the relevant schema/protocol/AOT evidence;
- zero repository-local consumers never proves removal safety;
- actual narrowing is a separate 2.0 task.

The Chinese 1.0 contract matrix replaces `Source-generated plugin SDK` with an
implemented capability description:

```text
Process protocol DTO and static JSON serialization support
```

It explicitly says third-party process plugins implement JSON and do not need
to reference a Bukit CLR assembly. This documentation correction does not
change `bukit-plugin-v1`.

## 11. Test Strategy

Implementation follows red-green-refactor sequencing.

### 11.1 Comparator fixture tests

The self-test proves:

1. identical baseline/current fixtures pass;
2. implementation-public addition fails with `review-required`, not
   `breaking`;
3. a removed type/member fails with `breaking`;
4. protected addition and removal fail with `protected-review`;
5. wire/serialized drift includes `contract-shape-review`;
6. AOT drift includes `aot-review`;
7. a new unclassified type includes `unclassified`;
8. malformed, duplicate, unsorted, or unresolved baselines fail with exit 2;
9. diagnostics are sorted and deterministic;
10. `ci-fast` contains both the self-test and real check exactly once.

### 11.2 Capture tests

The real baseline check proves:

- all twelve expected assemblies load;
- the current output contains 472 unique exported types;
- the current baseline matches without drift;
- a second capture produces byte-identical canonical JSON;
- paths, build roots, timestamps, MVIDs, and local machine values do not enter
  the baseline.

### 11.3 Repository gates

After each code subtask, run:

```text
bash scripts/checks/post-change-targeted.sh -- <subtask paths>
```

Because G-02 changes CI/gate-owned behavior, the completed parent task also
requires:

- direct `public-api-drift-self-test.sh`;
- direct real `public-api-drift.sh check Release`;
- `bash scripts/gates/ci-fast.sh Release`;
- `Bukit.Architecture.Tests` Release;
- `git diff --check`;
- an independent read-only review of every subtask and the aggregate diff.

Full, release, `test-all`, `smoke-all`, and whole-solution test gates remain out
of scope unless separately requested.

## 12. Failure and Recovery Behavior

- Missing restore/build output is an environment/gate setup failure, not API
  drift.
- Assembly load errors fail closed and identify the assembly.
- A malformed baseline cannot be replaced automatically.
- A failing mutation fixture stops before the real check so comparator policy
  regressions are not hidden by a matching current baseline.
- A matching real baseline cannot override a failed self-test.
- If a legitimate change is approved, maintainers create a candidate snapshot,
  review the baseline diff, assign every new type, update relevant docs/tests,
  and rerun the same gate.
- The baseline is never regenerated as an incidental side effect of build,
  test, CI, or release execution.

## 13. Acceptance Criteria

G-02 is complete only when:

1. the active English and Chinese governance text distinguishes CLR visibility
   from SDK support;
2. the stale source-generated plugin SDK claim is removed;
3. the checked baseline contains the exact current twelve-assembly surface;
4. all committed entries have resolved classifications and compatibility;
5. comparator mutations produce the specified diagnostics and exit codes;
6. two real captures are deterministic;
7. `ci-fast` runs both self-test and real check;
8. targeted verification and Architecture tests pass;
9. the aggregate diff contains no Core API, schema, protocol, persistence, or
   runtime behavior change;
10. independent read-only review reports no unresolved important issue.
