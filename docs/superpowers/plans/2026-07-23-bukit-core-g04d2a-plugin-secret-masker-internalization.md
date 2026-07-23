# Bukit Core G-04D2A PluginSecretMasker Internalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Narrow only `Bukit.PluginHost.PluginSecretMasker` from public to
internal in Bukit Core 2.0 while preserving every masking behavior, the
external-process plugin contract, and the immutable consumer-evidence cohort.

**Architecture:** Keep the type and its three static methods in the existing
PluginHost assembly and retain all same-assembly calls from
`PluginExecutionReporter`. Prove the breaking CLR-surface decision through an
assembly-level RED test, regenerate the governed public API snapshot, and
synchronize only current-state governance text while retaining all historical
decision counts.

**Tech Stack:** C# 14, .NET 10, xUnit, `System.Reflection`,
`System.Text.Json`, the repository public API drift tool, Markdown governance
documents, Native AOT CLI packaging.

## Global Constraints

- Base is local `2.0@764f0eddd242ed67eb88c1e323910d2cf55ea1c3`.
- The only production-code change is
  `public static class PluginSecretMasker` to
  `internal static class PluginSecretMasker`.
- Do not change the access modifiers or bodies of `MaskValue`,
  `MaskEnvironment`, `MaskText`, or `IsSecretKey`.
- Do not move or rename the type or file.
- Do not change secret-key fragments, replacement ordering, comparison modes,
  report fields, report paths, report JSON, `bukit-plugin-v1`, configuration,
  permissions, process execution, timeouts, output limits, schemas, CLI text,
  or release/CI/gate scripts.
- Do not implement general URL query, userinfo, or fragment secret scrubbing;
  the existing WX-P2-09 behavior gap is outside G-04D2A.
- Do not change `PluginHostErrorCodes` or any of the other 14 PluginHost
  candidates.
- Do not add `InternalsVisibleTo`, a facade, factory, adapter, replacement API,
  package, or assembly refactor.
- The current public API baseline must become exactly 14 assemblies / 508
  exported types / 104 `2.0-candidate` entries and differ only by removal of
  `Bukit.PluginHost.PluginSecretMasker`.
- The closed 136-entry candidate manifest must remain byte-identical with Git
  blob `7b07d6890562387010b52301e9f8716e9bf10ed1`; private-consumer status remains
  `unknown-until-voluntary-declaration`.
- Historical 509/105 and “other 105 candidates” statements describing the
  G-04D1C-M2 state must remain historical evidence. Only current-state text
  advances to 508/104.
- After the code subtask, run only focused affected checks. The parent
  controller runs one aggregate targeted check after all final paths are
  frozen.
- Do not run full/release gates, `test-all`, `smoke-all`, or whole-solution
  tests.

---

### Task 1: Single-Type Internalization, Governance Snapshot, and Behavior Proof

**Files:**

- Modify:
  `src/Bukit-Core/Bukit.PluginHost/PluginSecretMasker.cs`
- Modify:
  `tests/Bukit.PluginHost.Tests/PluginLockAndReportTests.cs`
- Create:
  `tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs`
- Modify:
  `tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs`
- Modify:
  `docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify:
  `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify:
  `guide/dev/public-api-governance.md`
- Create:
  `docs/analysis/bukit-core-g04d2a-plugin-secret-masker-internalization-2026-07-23.zh-CN.md`

**Interfaces:**

- Consumes:
  `PluginExecutionReporter.WriteAsync(PluginExecutionReport, CancellationToken)`
  and its existing same-assembly calls to `PluginSecretMasker`.
- Produces: the same internal CLR type identity and masking behavior, with no
  exported `Bukit.PluginHost.PluginSecretMasker` type.
- Produces: governed current-state counts 14 / 508 / 104.
- Preserves: the 136-entry historical candidate manifest and its exact blob.

- [ ] **Step 1: Add an entry-level masking characterization**

Extend the existing Reporter test data so an environment value such as
`secret-token` is embedded in:

```text
https://example.invalid/file?token=secret-token
```

Keep assertions at the public Reporter/report-JSON boundary:

```csharp
Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
Assert.Contains("token=***", json, StringComparison.Ordinal);
Assert.Contains("\"PUBLIC_VALUE\": \"visible\"", json, StringComparison.Ordinal);
```

Do not directly reference `PluginSecretMasker` from the test assembly. This is
a characterization of existing behavior and must pass before the visibility
change.

Run:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~PluginLockAndReportTests
```

Expected: PASS on the unchanged production source.

- [ ] **Step 2: Add the assembly/public-surface RED tests**

Create
`G04D2APluginSecretMaskerInternalizationTests.cs` with the following exact
target constants and assertion shape:

```csharp
private const string TargetTypeName =
    "Bukit.PluginHost.PluginSecretMasker";
private const string CandidateManifestBlob =
    "7b07d6890562387010b52301e9f8716e9bf10ed1";

[Fact]
public void PluginHostAssembly_KeepsMaskerInternalAndDoesNotExportIt()
{
    var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
    var type = assembly.GetType(TargetTypeName, throwOnError: false, ignoreCase: false);

    Assert.NotNull(type);
    Assert.False(type.IsPublic);
    Assert.DoesNotContain(
        assembly.GetExportedTypes(),
        exported => exported.FullName == TargetTypeName);
}

[Fact]
public void CurrentBaseline_ContainsFourteenAssemblies508TypesAnd104Candidates()
{
    using var document = ReadJson(
        "docs", "governance", "bukit-core-public-api-baseline.v1.json");
    var root = document.RootElement;
    var types = root.GetProperty("types").EnumerateArray().ToArray();

    Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
    Assert.Equal(508, types.Length);
    Assert.Equal(104, types.Count(type =>
        type.GetProperty("compatibility").GetString() == "2.0-candidate"));
    Assert.DoesNotContain(types, type =>
        type.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
        type.GetProperty("name").GetString() == TargetTypeName);
}
```

Add a third fact that computes Git blob SHA-1 over the closed manifest bytes
and asserts:

- schema state is `closed`;
- there are exactly 136 entries;
- the target historical entry remains present;
- declaration status is `consumer-declaration-pending`;
- private status is `unknown-until-voluntary-declaration`;
- public-search status is `no-public-match-found`;
- blob equals `CandidateManifestBlob`.

Add a fourth fact that requires both active governance documents to contain:

```text
G-04D2A single-type internalization decision: only `Bukit.PluginHost.PluginSecretMasker` is narrowed from public to internal in 2.0; the other 104 candidates are not batch-approved.
```

and:

```text
The current public API baseline contains 508 types, including 104 `2.0-candidate` entries.
```

It must also require the historical G-04D1C-M2 “other 105 candidates” decision
to remain and require the new decision ledger to exist.

Run only the two new current-state facts before changing production:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  "FullyQualifiedName~PluginHostAssembly_KeepsMaskerInternalAndDoesNotExportIt|FullyQualifiedName~CurrentBaseline_ContainsFourteenAssemblies508TypesAnd104Candidates"
```

Expected: FAIL because the type is still public and the baseline is still
509/105. Record the exact failing assertions.

- [ ] **Step 3: Apply the minimal production change**

Change only:

```csharp
public static class PluginSecretMasker
```

to:

```csharp
internal static class PluginSecretMasker
```

Do not alter any other source line in that file.

- [ ] **Step 4: Regenerate and inspect the governed baseline**

Generate a new snapshot at a new temporary path:

```bash
bash scripts/checks/public-api-drift.sh snapshot \
  /tmp/bukit-g04d2a-public-api-baseline.json Release
```

Before replacing the governed baseline, verify the snapshot reports 14 / 508 /
104 and that the semantic diff removes only
`Bukit.PluginHost.PluginSecretMasker` and its three members. If any other
type/member or governance metadata changes, stop.

Replace the governed baseline with the reviewed generated snapshot as a
mechanical snapshot update. Do not edit the closed candidate manifest.

- [ ] **Step 5: Synchronize current-state governance and create the ledger**

Append matching G-04D2A sections to:

- `docs/governance/bukit-core-2.0-consumer-declaration.md`;
- `guide/dev/public-api-governance.md`.

State explicitly:

- only `Bukit.PluginHost.PluginSecretMasker` is approved;
- the current baseline is 14 / 508 / 104;
- the other 104 candidates are not batch-approved;
- the closed 136-entry manifest remains immutable;
- private consumers remain unknown until voluntary declaration;
- this change is 2.0-only and source/binary breaking for an undisclosed direct
  CLR consumer;
- no replacement API is needed because the supported external plugin surface
  is the process protocol, not this helper;
- masking behavior and all other PluginHost candidates are excluded.

Create the Chinese G-04D2A ledger. It must record the base, exact one-token
production change, breaking boundary, consumer/AOT evidence, RED/GREEN
commands, baseline delta, immutable manifest blob, exclusions, stop
conditions, and a stable statement that final aggregate/review status comes
from the latest task handoff rather than being pre-claimed in the document.

Do not rewrite historical G-04C, G-04D1A, G-04D1B, or G-04D1C-M2 counts.

- [ ] **Step 6: Update only current-state assertions in earlier fixtures**

Update the four existing Architecture fixtures so their assertions against the
live baseline and active current-state sentence expect 508/104. Preserve:

- every historical decision sentence and historical remainder count;
- the G-04D1C-M2 ledger assertion for historical 14 / 509 / 105;
- all excluded-type and canonical-replacement assertions;
- all closed-manifest assertions.

Do not weaken a test merely to make the new baseline compile.

- [ ] **Step 7: Verify GREEN and affected behavior**

Run:

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
```

Expected: all PluginHost tests pass, with the new Reporter characterization.

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  "FullyQualifiedName~G04D2APluginSecretMaskerInternalizationTests|FullyQualifiedName~G04D1CM2AtomicRemovalTests|FullyQualifiedName~G04D1BBlockRendererFacadeRemovalTests|FullyQualifiedName~G04D1AStaticNotionFacadeRemovalTests|FullyQualifiedName~G04CPublicSurfacePilotTests|FullyQualifiedName~PluginBoundaryTests"
```

Expected: all selected governance and plugin-boundary tests pass.

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
git hash-object \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected:

- self-test and real drift check pass;
- manifest blob is exactly
  `7b07d6890562387010b52301e9f8716e9bf10ed1`.

- [ ] **Step 8: Run the code-subtask focused gate**

Run one focused check for every path changed by Task 1:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2a-plugin-secret-masker-internalization.md \
  src/Bukit-Core/Bukit.PluginHost/PluginSecretMasker.cs \
  tests/Bukit.PluginHost.Tests/PluginLockAndReportTests.cs \
  tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d2a-plugin-secret-masker-internalization-2026-07-23.zh-CN.md
```

Expected: PASS. Do not run the parent aggregate here.

- [ ] **Step 9: Self-review and commit**

Confirm:

- `PluginSecretMasker.cs` has exactly the one-token production diff;
- no other `Bukit.PluginHost` source changed;
- no `InternalsVisibleTo` was added;
- no masking expectation was weakened;
- baseline drift is exactly one type;
- manifest has no diff;
- current governance is synchronized and history remains historical;
- no protected reference, CI, release, schema, protocol, or gate file changed.

Commit the complete atomic task with:

```bash
git add \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d2a-plugin-secret-masker-internalization.md \
  src/Bukit-Core/Bukit.PluginHost/PluginSecretMasker.cs \
  tests/Bukit.PluginHost.Tests/PluginLockAndReportTests.cs \
  tests/Bukit.Architecture.Tests/G04D2APluginSecretMaskerInternalizationTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d2a-plugin-secret-masker-internalization-2026-07-23.zh-CN.md
git commit -m "refactor(pluginhost): internalize secret masker"
```

## Parent Completion Verification

After Task 1 receives clean spec and code-quality review, the parent controller
must:

1. run a real `osx-arm64` Native AOT CLI package build;
2. run release-artifact smoke without modifying release scripts;
3. prove the published CLI can still execute a process-plugin/report path, or
   record an exact blocker rather than substituting a static scan;
4. run exactly one aggregate targeted check from base `764f0edd` across the
   final changed paths;
5. dispatch an independent read-only whole-diff review;
6. stop rather than broaden scope if any AOT, masking, manifest, or public API
   proof fails.
