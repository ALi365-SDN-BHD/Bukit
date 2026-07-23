# Bukit Core G-04D1C-M2 Five-Type Atomic Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the five approved `Bukit.Content.Notion` legacy renderer-extension CLR identities as one 2.0-only atomic batch while preserving canonical behavior coverage, historical consumer uncertainty, and all unrelated public contracts.

**Architecture:** The canonical owner remains `Bukit.Notion.Rendering`; the four legacy compatibility source files in `Bukit.Content` are deleted together. Behavior tests that still exercise canonical rendering through the legacy wrapper move to `Bukit.Notion.Tests`, while architecture and governance fixtures are rewritten from “M1 retention” to “M2 removal” invariants. The current public API baseline changes from 514/110 to 509/105, but the closed 136-entry candidate manifest remains byte-identical.

**Tech Stack:** C# 14, .NET 10, xUnit, System.Text.Json, repository public-API drift tooling, Bash verification gates.

## Global Constraints

- The deliberate public API approval applies only to these five identities:
  - `Bukit.Content.Notion.INotionBlockRenderer`
  - `Bukit.Content.Notion.NotionBlockTransformer`
  - `Bukit.Content.Notion.NotionBlockRendererRegistry`
  - `Bukit.Content.Notion.NotionRenderContext`
  - `Bukit.Content.Notion.NotionBlocksRenderer`
- Delete all five identities atomically; no intermediate commit may remove only part of the graph.
- Do not delete, internalize, rename, obsolete, or change the signatures of `NotionApiClient`, `NotionProviderOptions`, or `NotionClientStats`.
- Do not change canonical transport/retry behavior, exception semantics, schema, plugin protocol, CLI, config, asset URLs, path tools, report contracts, CI, release, or verification policy.
- Keep `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` byte-identical at Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`.
- Preserve the historical `consumer-declaration-pending`, `unknown-until-voluntary-declaration`, and `no-public-match-found` records; do not rewrite them as proof of no consumers.
- The current governed baseline after the approved removal must contain 14 assemblies, 509 types, and 105 `2.0-candidate` entries.
- After each code subtask, run only `bash scripts/checks/post-change-focused.sh -- <changed paths>`.
- Run `bash scripts/checks/post-change-targeted.sh --base f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c -- <all changed paths>` exactly once at parent completion.
- Do not run full/release gates, `scripts/test-all.sh`, `scripts/smoke-all.sh`, or whole-solution tests.
- Native AOT and release-artifact smoke are not authorized by this plan.

---

### Task 1: Record the deliberate approval and pre-delete evidence boundary

**Files:**
- Create: `docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md`

**Interfaces:**
- Consumes: the five exact legacy CLR names, the merged M1 result, the 14/514/110 pre-removal baseline, and manifest blob `7b07d6890562387010b52301e9f8716e9bf10ed1`.
- Produces: a stable M2 ledger path and explicit scope boundary used by the atomic-removal fixture.

- [ ] **Step 1: Add the provisional M2 ledger**

Create the ledger with:

```markdown
# Bukit Core G-04D1C-M2：Content Notion five-type atomic 2.0 removal

日期：2026-07-23
基线：`2.0@f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c`
状态：实施中；最终状态以最新 handoff/controller 为准

## 明确批准与原子范围

用户在 M1 合并并验证后明确批准进入 G-04D1C-M2。批准只覆盖五个
`Bukit.Content.Notion` legacy renderer-extension CLR identity 的 2.0 原子删除。
M2 不授权删除 `NotionApiClient`、`NotionProviderOptions` 或
`NotionClientStats`，也不授权修改 transport、retry、exception、schema、plugin
protocol、CLI、config、asset URL、path 或 report contract。
```

Record the pre-delete check as current repository evidence, not as a claim about private consumers:

```markdown
- 仓内 Core 主链、Labs 和官方插件未发现五个完整 legacy CLR identity 的新消费；
- M1 合并点以后生产目录无差异；
- 私有、未索引、反射、序列化、AOT 或 binary plugin consumer 仍未知；
- 闭合 manifest 的历史不确定性保持不变。
```

- [ ] **Step 2: Verify the pre-delete evidence and immutable manifest**

```bash
rg -n -g '*.cs' \
  'Bukit\.Content\.Notion\.(INotionBlockRenderer|NotionBlockTransformer|NotionBlockRendererRegistry|NotionRenderContext|NotionBlocksRenderer)' \
  src/Bukit-Core src/Bukit-Labs src/Bukit-Plugins
git hash-object docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: no fully qualified production consumer outside the compatibility graph; manifest hash
`7b07d6890562387010b52301e9f8716e9bf10ed1`. If a real consumer appears, stop M2
instead of modifying it.

- [ ] **Step 3: Run the focused documentation check**

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md
```

Expected: PASS.

- [ ] **Step 4: Commit the explicit approval record**

```bash
git add docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md
git commit -m "docs(notion): authorize G-04D1C-M2 atomic scope"
```
---

### Task 2: Move valuable rendering behavior coverage to the canonical owner

**Files:**
- Move: `tests/Bukit.Content.Tests/NotionBlockRendererRegistryTests.cs` → `tests/Bukit.Notion.Tests/NotionBlockRendererRegistryTests.cs`
- Move: `tests/Bukit.Content.Tests/NotionBlocksRendererPaginationTests.cs` → `tests/Bukit.Notion.Tests/NotionBlocksRendererPaginationTests.cs`
- Move: `tests/Bukit.Content.Tests/NotionRenderContextTests.cs` → `tests/Bukit.Notion.Tests/NotionRenderContextTests.cs`
- Move: `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs` → `tests/Bukit.Notion.Tests/NotionBlockRendererCompatibilityEdgeCasesTests.cs`
- Delete: `tests/Bukit.Content.Tests/LegacyNotionExtensionMigrationContractTests.cs`
- Modify: `tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs`

**Interfaces:**
- Consumes: canonical `Bukit.Notion.Rendering` and `Bukit.Notion.Transport` public types plus `Bukit.Notion.Tests` internals visibility.
- Produces: canonical coverage for pagination, list rendering, registry replacement/removal, nested rendering, context client ownership, and renderer edge cases.

- [ ] **Step 1: Move the four behavior suites and switch their owner**

For every moved file:

```csharp
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Tests;
```

Remove `Bukit.Engine.Abstractions.Content`, `Bukit.Content.Notion`, and
`Bukit.Shared` imports. Replace each legacy client setup:

```csharp
var options = new NotionProviderOptions
{
    DatabaseId = "db",
    Token = "token",
    RequestDelayMs = 0
};
using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
```

with:

```csharp
var options = new NotionClientOptions
{
    Token = "token",
    RequestDelayMs = 0,
    MaxRetries = 0
};
using var client = new NotionClient(options, http);
```

Rename the moved edge-case class to avoid colliding with the existing canonical suite:

```csharp
public sealed class NotionBlockRendererCompatibilityEdgeCasesTests
```

Keep the assertions and JSON fixtures byte-for-byte unless the canonical exception type is an explicit part of the assertion.

- [ ] **Step 2: Remove legacy-only translation tests**

Delete `LegacyNotionExtensionMigrationContractTests.cs`. Its legacy
`ContentException` translation and old client-ownership behavior disappear with the approved
facade. Do not reproduce those semantics in `Bukit.Notion`.

- [ ] **Step 3: Narrow the remaining legacy consumer fixture**

Set `LegacyNotionConsumerFixture.PublicTypes` to:

```csharp
internal static readonly Type[] PublicTypes =
[
    typeof(Bukit.Content.Notion.NotionApiClient),
    typeof(Bukit.Content.Notion.NotionClientStats),
    typeof(Bukit.Content.Notion.NotionContentProvider),
    typeof(Bukit.Content.Notion.NotionPropertyParser),
    typeof(Bukit.Content.Notion.NotionProviderOptions)
];
```

- [ ] **Step 4: Run the canonical and remaining Content tests**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter "FullyQualifiedName~NotionBlockRendererRegistryTests|FullyQualifiedName~NotionBlocksRendererPaginationTests|FullyQualifiedName~NotionRenderContextTests|FullyQualifiedName~NotionBlockRendererCompatibilityEdgeCasesTests|FullyQualifiedName~CanonicalExtensionGraphMigrationContractTests|FullyQualifiedName~CanonicalClientMigrationContractTests"

dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
```

Expected: PASS. The legacy source still exists at this step, but no retained behavior suite depends on it.

- [ ] **Step 5: Run the focused check**

Run:

```bash
bash scripts/checks/post-change-focused.sh -- \
  tests/Bukit.Content.Tests/NotionBlockRendererRegistryTests.cs \
  tests/Bukit.Content.Tests/NotionBlocksRendererPaginationTests.cs \
  tests/Bukit.Content.Tests/NotionRenderContextTests.cs \
  tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs \
  tests/Bukit.Content.Tests/LegacyNotionExtensionMigrationContractTests.cs \
  tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs \
  tests/Bukit.Notion.Tests/NotionBlockRendererRegistryTests.cs \
  tests/Bukit.Notion.Tests/NotionBlocksRendererPaginationTests.cs \
  tests/Bukit.Notion.Tests/NotionRenderContextTests.cs \
  tests/Bukit.Notion.Tests/NotionBlockRendererCompatibilityEdgeCasesTests.cs
```

Expected: PASS for all selected owners.

- [ ] **Step 6: Commit the canonical test ownership migration**

```bash
git add tests/Bukit.Content.Tests tests/Bukit.Notion.Tests
git commit -m "test(notion): move renderer coverage to canonical owner"
```

---

### Task 3: Remove the five legacy CLR identities atomically

**Files:**
- Create: `tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/INotionBlockRenderer.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/NotionBlockRendererRegistry.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/NotionRenderContext.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/NotionBlocksRenderer.cs`
- Modify: `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs`
- Delete: `tests/Bukit.Architecture.Tests/G04D1CM1MigrationContractTests.cs`

**Interfaces:**
- Consumes: Task 2 canonical behavior coverage.
- Produces: a `Bukit.Content` assembly with none of the five approved legacy identities, no compatibility adapter implementation, and a passing source/type removal guard.

- [ ] **Step 1: Add and run the failing source/type removal fixture**

Create `G04D1CM2AtomicRemovalTests` with:

```csharp
private static readonly string[] RemovedLegacyTypes =
[
    "Bukit.Content.Notion.INotionBlockRenderer",
    "Bukit.Content.Notion.NotionBlockTransformer",
    "Bukit.Content.Notion.NotionBlockRendererRegistry",
    "Bukit.Content.Notion.NotionRenderContext",
    "Bukit.Content.Notion.NotionBlocksRenderer"
];

[Fact]
public void BukitContent_DoesNotExposeApprovedLegacyExtensionGraph()
{
    var contentAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;
    Assert.All(RemovedLegacyTypes, name =>
        Assert.Null(contentAssembly.GetType(name, throwOnError: false, ignoreCase: false)));
}

[Fact]
public void LegacyCompatibilitySourceFiles_AreRemovedAsOneBatch()
{
    string[] files =
    [
        "INotionBlockRenderer.cs",
        "NotionBlockRendererRegistry.cs",
        "NotionRenderContext.cs",
        "NotionBlocksRenderer.cs"
    ];
    var directory = Path.Combine(
        RepoRoot, "src", "Bukit-Core", "Bukit.Content", "Notion");
    Assert.All(files, file => Assert.False(File.Exists(Path.Combine(directory, file))));
}
```

Add passing companion assertions that the five canonical replacements are public and
`NotionApiClient`, `NotionProviderOptions`, and `NotionClientStats` still resolve.

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~G04D1CM2AtomicRemovalTests
```

Expected: FAIL only because the five legacy identities and four source files still exist.

- [ ] **Step 2: Delete all four compatibility source files in one patch**

The four files contain exactly the five approved CLR identities. Delete them together. Do not touch:

```text
src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs
src/Bukit-Core/Bukit.Content/Notion/NotionProviderOptions.cs
src/Bukit-Core/Bukit.Content/Notion/NotionContentProvider.cs
```

- [ ] **Step 3: Replace retention guards with removal guards**

In `NotionBoundaryTests`:

- remove `LegacyRendererRegistry_MustDelegateDefaultOwnershipToCanonicalRegistry`;
- remove the five deleted names from `LegacyContentNotionTypes`;
- preserve the exact remaining legacy namespace export list:

```csharp
private static readonly string[] LegacyContentNotionTypes =
[
    "Bukit.Content.Notion.NotionApiClient",
    "Bukit.Content.Notion.NotionClientStats",
    "Bukit.Content.Notion.NotionContentProvider",
    "Bukit.Content.Notion.NotionPropertyParser",
    "Bukit.Content.Notion.NotionProviderOptions"
];
```

In `G04D1BBlockRendererFacadeRemovalTests`, keep D1B’s historical decision strings
but change the live D1C source/type assertion to verify the five names are now absent under
the separately approved M2 decision.

Delete `G04D1CM1MigrationContractTests.cs`; Task 1’s M2 fixture replaces its live retention
assertions. Preserve the M1 guide as historical migration documentation.

- [ ] **Step 4: Build the directly affected projects**

Run:

```bash
dotnet build src/Bukit-Core/Bukit.Content/Bukit.Content.csproj \
  -c Release --nologo
dotnet build tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo
```

Expected: PASS with no references to any removed legacy CLR type.

- [ ] **Step 5: Run the removal fixture and focused check**

Run the M2 fixture again:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~G04D1CM2AtomicRemovalTests
```

Expected: PASS.

Run:

```bash
bash scripts/checks/post-change-focused.sh -- \
  src/Bukit-Core/Bukit.Content/Notion/INotionBlockRenderer.cs \
  src/Bukit-Core/Bukit.Content/Notion/NotionBlockRendererRegistry.cs \
  src/Bukit-Core/Bukit.Content/Notion/NotionRenderContext.cs \
  src/Bukit-Core/Bukit.Content/Notion/NotionBlocksRenderer.cs \
  tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM1MigrationContractTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs
```

Expected: PASS.

- [ ] **Step 6: Commit the atomic removal**

```bash
git add src/Bukit-Core/Bukit.Content/Notion tests/Bukit.Architecture.Tests
git commit -m "breaking(content): remove legacy Notion extension graph"
```

---

### Task 4: Update the governed baseline and active M2 decision record

**Files:**
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `guide/dev/public-api-governance.md`
- Modify: `docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md`
- Modify: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs`

**Interfaces:**
- Consumes: the compiled post-removal public surface from Task 3.
- Produces: the 14/509/105 current baseline and a source-faithful active decision record without changing the historical manifest.

- [ ] **Step 1: Extend the M2 fixture and prove governance is still stale**

Add these durable assertions to `G04D1CM2AtomicRemovalTests`:

```csharp
[Fact]
public void CurrentBaseline_ContainsFourteenAssembliesFiveHundredNineTypesAndOneHundredFiveCandidates()
{
    using var document = ReadJson(
        "docs", "governance", "bukit-core-public-api-baseline.v1.json");
    var root = document.RootElement;
    var types = root.GetProperty("types").EnumerateArray().ToArray();

    Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
    Assert.Equal(509, types.Length);
    Assert.Equal(105, types.Count(type =>
        type.GetProperty("compatibility").GetString() == "2.0-candidate"));
    Assert.All(RemovedLegacyTypes, removed => Assert.DoesNotContain(types, type =>
        type.GetProperty("name").GetString() == removed));
}
```

Add companion assertions that:

- the closed manifest retains all five historical entries and exact blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1`;
- both active governance documents and the M2 ledger contain the exact five-type approval,
  509/105 state, private-consumer uncertainty, and explicit exclusions.

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~G04D1CM2AtomicRemovalTests
```

Expected: FAIL because the committed baseline and active governance still describe 514/110.

- [ ] **Step 2: Generate the post-removal baseline**

Run:

```bash
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04d1c-m2-baseline.XXXXXX")"
snapshot="$scratch/baseline.json"
bash scripts/checks/public-api-drift.sh snapshot "$snapshot" Release
mv "$snapshot" docs/governance/bukit-core-public-api-baseline.v1.json
rmdir "$scratch"
```

Verify:

```bash
jq '[.types[]] | length' docs/governance/bukit-core-public-api-baseline.v1.json
jq '[.types[] | select(.compatibility == "2.0-candidate")] | length' \
  docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: `509` and `105`.

- [ ] **Step 3: Add the M2 active-governance section**

Append the same decision block to the consumer declaration and governance guide:

```markdown
## G-04D1C-M2 Notion Extension Graph

G-04D1C-M2 five-type atomic decision: only
`Bukit.Content.Notion.INotionBlockRenderer`,
`Bukit.Content.Notion.NotionBlockTransformer`,
`Bukit.Content.Notion.NotionBlockRendererRegistry`,
`Bukit.Content.Notion.NotionRenderContext`, and
`Bukit.Content.Notion.NotionBlocksRenderer` are approved for removal in 2.0;
the other 105 candidates are not batch-approved.

Their canonical replacements are in `Bukit.Notion.Rendering`. The current
public API baseline contains 509 types, including 105 `2.0-candidate` entries.
The closed 136-entry candidate manifest remains the immutable historical
cohort and continues to record unknown private-consumer status. This decision
does not modify any 1.x CLR visibility.
```

Keep the G-04C, G-04D1A, and G-04D1B remainder counts as explicitly historical
snapshots. Replace only sentences that claim 514/110 is the current baseline.

- [ ] **Step 4: Update prior architecture fixtures for the new current state**

Keep historical decision assertions, but set current-state constants and baseline assertions to:

```csharp
const string currentRemainder = "the other 105 candidates are not batch-approved.";
const string currentBaseline =
    "The current public API baseline contains 509 types, including 105 `2.0-candidate` entries.";

Assert.Equal(509, types.Length);
Assert.Equal(105, types.Count(type =>
    type.GetProperty("compatibility").GetString() == "2.0-candidate"));
```

The new M2 fixture must assert all five exact names appear in both active governance
documents and in the M2 ledger, and must continue to assert manifest blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

- [ ] **Step 5: Run architecture and public API owner checks**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter "FullyQualifiedName~G04D1CM2AtomicRemovalTests|FullyQualifiedName~G04D1BBlockRendererFacadeRemovalTests|FullyQualifiedName~G04D1AStaticNotionFacadeRemovalTests|FullyQualifiedName~G04CPublicSurfacePilotTests|FullyQualifiedName~NotionBoundaryTests"

bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
```

Expected: all selected architecture tests PASS; drift self-test prints
`public API drift self-test OK`; real drift check exits 0.

- [ ] **Step 6: Run the focused check**

Run:

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1CM2AtomicRemovalTests.cs
```

Expected: PASS.

- [ ] **Step 7: Commit governance convergence**

```bash
git add docs/governance guide/dev/public-api-governance.md \
  docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md \
  tests/Bukit.Architecture.Tests
git commit -m "docs(governance): record G-04D1C-M2 atomic removal"
```

---

### Task 5: Complete cross-boundary verification, aggregate gate, and review

**Files:**
- Modify: `docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md`

**Interfaces:**
- Consumes: Tasks 1–4 complete diff.
- Produces: final execution evidence and merge eligibility; no new runtime behavior.

- [ ] **Step 1: Run required owner test projects**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off
```

Expected: all four projects PASS.

- [ ] **Step 2: Run cross-boundary builds**

Run:

```bash
dotnet build bukit-core.slnx -c Release --no-restore --nologo
dotnet build bukit-labs.slnx -c Release --no-restore --nologo
dotnet build bukit-plugins.slnx -c Release --no-restore --nologo
```

Expected: all three builds PASS. Do not substitute a whole-repository test run.

- [ ] **Step 3: Run public API and immutable-manifest checks**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
git hash-object docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: self-test and real check PASS; hash is
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

- [ ] **Step 4: Run the one aggregate gate**

Collect the exact tracked path list:

```bash
bash -lc '
  mapfile -t changed_paths < <(git diff --name-only \
    f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c..HEAD)
  bash scripts/checks/post-change-targeted.sh \
    --base f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c \
    -- "${changed_paths[@]}"
'
```

Expected: PASS. This is the only parent aggregate execution authorized by the plan.

- [ ] **Step 5: Record evidence without freezing transient review state**

Update the ledger with exact test counts, build outcomes, drift result, manifest hash,
and aggregate result. Keep:

```markdown
状态：verification ledger complete；正式关闭状态以最新 handoff/controller 为准
```

Do not encode a specific commit or pending/completed review state as an architecture-test invariant.

- [ ] **Step 6: Commit the evidence ledger**

```bash
git add docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md
git commit -m "docs(notion): record G-04D1C-M2 verification"
```

- [ ] **Step 7: Perform independent whole-diff read-only review**

Review the fixed diff from
`f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c` to final `HEAD`. Require:

- 0 Critical / 0 Important / 0 Minor;
- exactly five approved legacy identities removed;
- canonical behavior tests retained;
- 14/509/105 baseline;
- unchanged 136-entry manifest blob;
- no transport, exception, schema, plugin, CI, release, or unrelated public API drift;
- M2 ledger accurately separates source/binary break, known evidence, and unknown private consumers.

- [ ] **Step 8: Final controller audit**

Run:

```bash
git status --short --branch
git diff --check f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c..HEAD
git diff --name-status f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c..HEAD
```

Expected: clean worktree, no whitespace errors, and no paths outside the approved plan.
