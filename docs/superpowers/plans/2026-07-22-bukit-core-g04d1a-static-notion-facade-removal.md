# Bukit Core G-04D1A Static Notion Facade Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove exactly `Bukit.Content.Notion.NotionColorPalette` and `Bukit.Content.Notion.NotionRichTextRenderer` from the Bukit 2.0 CLR surface while preserving their canonical `Bukit.Notion.Rendering` replacements and all rendering behavior.

**Architecture:** Treat the two `Bukit.Content.dll` types as stateless compatibility facades whose implementation already belongs to `Bukit.Notion.dll`. Prove removal with compiled-assembly architecture tests, migrate affected behavior assertions to the canonical owner, generate and semantically verify the current public API baseline, preserve the closed 136-entry consumer manifest, and record one explicit 2.0-only decision. Keep the remaining 28 Content Notion renderer candidates and every non-renderer compatibility type unchanged.

**Tech Stack:** C# / .NET 10, xUnit, JSON public API baseline, Bash governance gates, Native AOT.

## Global Constraints

- Work only on `codex/g04d1a-static-notion-facade-removal`, based on local `2.0@1d72384b10dc011388db44042c35daccb0c5411f`.
- Remove exactly `Bukit.Content.Notion.NotionColorPalette` and `Bukit.Content.Notion.NotionRichTextRenderer` from `Bukit.Content.dll`.
- Preserve every type and public member in `Bukit.Notion.Rendering`, including both canonical replacements.
- Preserve all other legacy `Bukit.Content.Notion.*` types, including 23 block renderer facades, the five extension-graph types, `NotionClientStats`, clients, providers, parsers, options, and assembly references.
- Keep `Directory.Build.props` at `2.0.0-alpha.1`; do not modify 1.x `main`.
- Do not modify the closed `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`; it remains the immutable 136-entry historical cohort.
- Do not modify schema, plugin protocol, report shape, configuration defaults, asset URL, path utilities, HTTP/TLS, Notion transport behavior, exception behavior, or release workflows.
- Do not add type forwarding, compatibility shims, `Obsolete` attributes, new packages, or a canonical Notion SDK promise.
- Use TDD: the compiled-assembly removal guard must fail because both legacy types still exist before deleting production files.
- Public API drift before baseline generation must contain exactly two `breaking:` diagnostics and no other drift category.
- Each implementation task receives focused checks; the parent task runs one aggregate `post-change-targeted.sh` after the closure commit.
- Core/Labs/plugins compile, affected tests, public API checks, `osx-arm64` Native AOT archive smoke, and independent read-only reviews are required before merge consideration.

---

### Task 1: TDD removal, canonical test migration, and deliberate baseline approval

**Files:**
- Create: `tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/NotionColorPalette.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/NotionRichTextRenderer.cs`
- Move: `tests/Bukit.Content.Tests/NotionColorPaletteTests.cs` → `tests/Bukit.Notion.Tests/NotionColorPaletteTests.cs`
- Move: `tests/Bukit.Content.Tests/NotionRichTextRendererExtendedTests.cs` → `tests/Bukit.Notion.Tests/NotionRichTextRendererExtendedTests.cs`
- Modify: `tests/Bukit.Content.Tests/BlockRendererMediaAndContainerTests.cs`
- Modify: `tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs`
- Modify: `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs`
- Modify: `tests/Bukit.Content.Tests/NotionBlockRenderersTests.cs`
- Modify: `tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs`
- Modify: `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Create: `docs/analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `guide/dev/public-api-governance.md`

**Interfaces:**
- Consumes: canonical `Bukit.Notion.Rendering.NotionColorPalette` and `Bukit.Notion.Rendering.NotionRichTextRenderer` with the member parity proven by G-04D1.
- Produces: a 537-type current baseline with 133 `2.0-candidate` entries, an immutable 136-entry historical manifest, migrated canonical behavior tests, and a provisional G-04D1A decision ledger.

- [ ] **Step 1: Write the failing compiled-assembly guard**

Create `G04D1AStaticNotionFacadeRemovalTests.cs` with these exact removal identities and a repository-root helper patterned after `G04CPublicSurfacePilotTests`:

```csharp
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1AStaticNotionFacadeRemovalTests
{
    private const string LegacyColorPalette = "Bukit.Content.Notion.NotionColorPalette";
    private const string LegacyRichTextRenderer = "Bukit.Content.Notion.NotionRichTextRenderer";
    private static readonly string[] RemovedTypes = [LegacyColorPalette, LegacyRichTextRenderer];
    [Fact]
    public void BukitContent_DoesNotExposeApprovedLegacyStaticNotionFacades()
    {
        var assembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;

        Assert.All(RemovedTypes, typeName =>
            Assert.Null(assembly.GetType(typeName, throwOnError: false, ignoreCase: false)));
    }

    [Fact]
    public void CanonicalNotionRendering_ReplacementsRemainPublic()
    {
        var assembly = typeof(Bukit.Notion.Rendering.NotionColorPalette).Assembly;

        Assert.Equal("Bukit.Notion", assembly.GetName().Name);
        Assert.NotNull(assembly.GetType(
            "Bukit.Notion.Rendering.NotionColorPalette",
            throwOnError: false,
            ignoreCase: false));
        Assert.NotNull(assembly.GetType(
            "Bukit.Notion.Rendering.NotionRichTextRenderer",
            throwOnError: false,
            ignoreCase: false));
    }

}
```

`System.Text.Json`, `RepoRoot`, and JSON helpers are added only in the governance step below; do not add unused imports or fields in the RED commit.

- [ ] **Step 2: Run the exact RED test**

```bash
env -u NOTION_TOKEN dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore \
  --filter FullyQualifiedName~G04D1AStaticNotionFacadeRemovalTests.BukitContent_DoesNotExposeApprovedLegacyStaticNotionFacades
```

Expected: one failed test whose assertion reports a non-null legacy type. A compile error, missing assembly, or unrelated failure is not an accepted RED.

- [ ] **Step 3: Migrate dedicated static behavior tests to the canonical owner**

Move the two dedicated files into `tests/Bukit.Notion.Tests/`. In both files change the namespace to:

```csharp
namespace Bukit.Notion.Tests;
```

Replace `using Bukit.Content.Notion;` with:

```csharp
using Bukit.Notion.Rendering;
```

Remove `using Bukit.Engine.Abstractions.Content;` if it is unused. Preserve every test method and assertion body verbatim.

For the four mixed legacy block-renderer test files, keep all legacy renderer imports and behavior intact, but bind the removed static names explicitly to the canonical owner:

```csharp
using NotionColorPalette = Bukit.Notion.Rendering.NotionColorPalette;
using NotionRichTextRenderer = Bukit.Notion.Rendering.NotionRichTextRenderer;
```

Add only the alias each file actually consumes:

- `BlockRendererMediaAndContainerTests.cs`: `NotionColorPalette`;
- `BlockRendererUrlSafetyTests.cs`: `NotionRichTextRenderer`;
- `NotionBlockRendererEdgeCasesTests.cs`: `NotionRichTextRenderer`;
- `NotionBlockRenderersTests.cs`: both aliases.

- [ ] **Step 4: Delete exactly the two facade source files and update remaining compatibility fixtures**

Delete `NotionColorPalette.cs` and `NotionRichTextRenderer.cs`. Do not edit canonical implementations.

In `LegacyNotionConsumerFixture.cs`, remove only:

```csharp
typeof(Bukit.Content.Notion.NotionRichTextRenderer),
```

and update its comment to say that the fixture proves the remaining legacy namespace surface on the 2.0 line.

In `NotionBoundaryTests.LegacyContentNotionTypes`, remove only:

```csharp
"Bukit.Content.Notion.NotionColorPalette",
"Bukit.Content.Notion.NotionRichTextRenderer"
```

Rename `LegacyNotionFacades_MustRemainFrozenDuringOneX` to
`RemainingLegacyNotionFacades_MustMatchGovernedTwoZeroBaseline`. Keep the exact export-set assertion; extend its governance assertions only after the G-04D1A section exists.

In `G04CPublicSurfacePilotTests`, preserve every G-04C removal and historical-cohort assertion. Rename `CurrentPublicApiBaseline_ContainsOnlyTheApprovedRemoval` to `CurrentPublicApiBaseline_PreservesTheApprovedG04CRemoval`, update only the live current-baseline totals from 539/135 to 537/133, and make the active-governance wording distinguish the historical G-04C state (135 remained immediately after that decision) from the current post-G-04D1A state (133 remain). Do not weaken or delete the G-04C guard.

- [ ] **Step 5: Run GREEN and affected behavior tests before baseline approval**

```bash
env -u NOTION_TOKEN dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore \
  --filter FullyQualifiedName~G04D1AStaticNotionFacadeRemovalTests.BukitContent_DoesNotExposeApprovedLegacyStaticNotionFacades
env -u NOTION_TOKEN dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore
env -u NOTION_TOKEN dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore
```

Expected: the RED test is green; Content and Notion test projects pass with every migrated assertion retained. Record actual totals.

- [ ] **Step 6: Prove the pre-baseline drift is exactly two removals**

Run the real check, capture its exit status, and require exactly these two lines in any order:

```text
breaking: Bukit.Content::Bukit.Content.Notion.NotionColorPalette: exported type removed
breaking: Bukit.Content::Bukit.Content.Notion.NotionRichTextRenderer: exported type removed
```

Require exit 1, exactly two `breaking:` lines, and zero lines beginning with
`review-required:`, `protected-review:`, `type-shape-review:`,
`contract-shape-review:`, `aot-review:`, `unclassified:`, or `gate-error:`.

- [ ] **Step 7: Generate and semantically approve the current baseline**

Generate a snapshot into a new temporary directory:

```bash
snapshot_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04d1a-snapshot.XXXXXX")"
snapshot="$snapshot_root/bukit-core-public-api-baseline.v1.json"
bash scripts/checks/public-api-drift.sh snapshot "$snapshot" Release
```

Require 537 types, 133 `2.0-candidate` entries, and zero occurrences of the two legacy names. Produce `expected.json` by deleting exactly the two named `Bukit.Content` entries from the current baseline with `jq`; normalize both expected and generated files with `jq -S`; require `diff -u` to emit no output. Copy the reviewed generated snapshot over the governed baseline. Do not touch the closed manifest.

- [ ] **Step 8: Add current-baseline, historical-cohort, and governance guards**

Extend `G04D1AStaticNotionFacadeRemovalTests` with JSON helpers and three tests:

1. `CurrentBaseline_ContainsExactlyTheApprovedG04D1ARemovals`: assert schema, target framework, SDK policy, 14 assemblies, 537 types, 133 candidates, and absence of both removed identities.
2. `ClosedManifest_PreservesBothHistoricalCandidates`: assert 136 candidates, `closed`, both identities present, each `consumer-declaration-pending`, `unknown-until-voluntary-declaration`, and `no-public-match-found`.
3. `ActiveGovernance_RecordsTheExactTwoTypeDecision`: assert the declaration, guide, and ledger identify only the two removed types, 133 remaining candidates, immutable 136-entry cohort, canonical replacements, and provisional status.

Also assert the source files no longer exist and the canonical source files do exist.

- [ ] **Step 9: Record the provisional decision and migration boundary**

Create `docs/analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md` with:

- status `实施记录已建立 / 跨边界验证与独立复审待执行`;
- base `2.0@1d72384b10dc011388db44042c35daccb0c5411f`;
- exact two-type removal and 537/133 current baseline counts;
- canonical namespace replacements;
- source/binary breaking-change explanation;
- 1.x `main` unchanged;
- immutable historical manifest and private-consumer uncertainty;
- affected tests and remaining validation checklist;
- explicit statement that the other 28 renderer candidates, `NotionClientStats`, schema, plugin protocol, transport, exceptions, URLs, paths, reports, and version are unchanged.

Append a `G-04D1A Two Static Facades` section to the active declaration and guide. State that this separately approved 2.0 decision removes exactly the two types, leaves 133 current candidates, does not batch-authorize them, and points to the decision ledger and canonical replacements.

Where the active declaration and guide currently use present-tense wording that the G-04C decision leaves 135 candidates, retain that count as explicit historical state and add the current 133-candidate state after G-04D1A. The closed 136-entry cohort and the G-04C single-type decision remain immutable facts.

- [ ] **Step 10: Run focused checks and commit the cohesive implementation**

Run the complete Architecture, Content, and Notion projects; public API self-test and real check; confirm the candidate manifest has no diff from `1d72384b`; then run:

```bash
bash scripts/checks/post-change-focused.sh -- \
  src/Bukit-Core/Bukit.Content/Notion/NotionColorPalette.cs \
  src/Bukit-Core/Bukit.Content/Notion/NotionRichTextRenderer.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs \
  tests/Bukit.Content.Tests/BlockRendererMediaAndContainerTests.cs \
  tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs \
  tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs \
  tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs \
  tests/Bukit.Content.Tests/NotionBlockRenderersTests.cs \
  tests/Bukit.Content.Tests/NotionColorPaletteTests.cs \
  tests/Bukit.Content.Tests/NotionRichTextRendererExtendedTests.cs \
  tests/Bukit.Notion.Tests/NotionColorPaletteTests.cs \
  tests/Bukit.Notion.Tests/NotionRichTextRendererExtendedTests.cs \
  docs/analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  guide/dev/public-api-governance.md
```

Commit all Task 1 paths with:

```bash
git commit -m "breaking(content): remove legacy static Notion facades"
```

Expected: no source, test, baseline, or governance path outside this task is changed.

---

### Task 2: Cross-boundary proof and truthful closure

**Files:**
- Verify only: `bukit-core.slnx`
- Verify only: `bukit-labs.slnx`
- Verify only: `bukit-plugins.slnx`
- Verify only: `scripts/build/native-aot.sh`
- Verify only: `scripts/smoke/release-artifacts.sh`
- Modify: `docs/analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md`
- Modify: `tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs`

**Interfaces:**
- Consumes: the reviewed Task 1 implementation commit.
- Produces: real Core/Labs/plugins/AOT evidence and a final ledger whose claims are guarded by architecture tests.

- [ ] **Step 1: Run complete affected test projects**

Run Architecture, Content, and Notion tests in Release with `NOTION_TOKEN` unset and `--no-restore`. Require explicit zero-failure summaries.

- [ ] **Step 2: Build Core, Labs, and official plugins**

```bash
dotnet build bukit-core.slnx -c Release --no-restore --nologo
dotnet build bukit-labs.slnx -c Release --no-restore --nologo
dotnet build bukit-plugins.slnx -c Release --no-restore --nologo
```

Require all three to exit 0. If assets are missing, restore the exact affected solution/project and rerun; do not classify restore absence as compile success.

- [ ] **Step 3: Produce and smoke a real osx-arm64 Native AOT archive**

```bash
aot_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04d1a-aot.XXXXXX")"
archive="$(bash scripts/build/native-aot.sh 2.0.0-alpha.1 osx-arm64 "$aot_root" Release)"
test -s "$archive"
bash scripts/smoke/release-artifacts.sh "$archive" osx-arm64
```

Run outside the restricted sandbox when required. Do not upload or release the temporary artifact.

- [ ] **Step 4: Reconfirm public API and immutable cohort**

Run the drift self-test and real check. Require 537/133 in the current baseline and byte-identical closed manifest from base.

- [ ] **Step 5: Obtain the first independent read-only review**

The reviewer must verify exact two-type deletion, preserved canonical types/members, faithful test migration, exact semantic baseline delta, immutable candidate manifest, no changes to the other 28 renderer candidates or out-of-scope contracts, and honest verification evidence. Resolve every Critical or Important finding and re-review.

- [ ] **Step 6: Close the ledger and guard the final state**

Only after Steps 1-5 pass, change ledger status to:

```text
状态：已实施并通过跨边界验证与独立只读复审
```

Record actual affected-test totals, successful Core/Labs/plugins builds, real AOT archive smoke, public API checks, and first review verdict. State that the parent aggregate targeted gate and final aggregate diff review occur after this closure commit.

Update `ActiveGovernance_RecordsTheExactTwoTypeDecision` to require the final status/evidence and reject the provisional wording. Run the Architecture project and focused checks for those two paths, then commit:

```bash
git commit -m "docs(governance): close G-04D1A decision ledger"
```

---

### Task 3: Parent aggregate gate and final independent diff audit

**Files:**
- Review: every changed path from `1d72384b10dc011388db44042c35daccb0c5411f` through HEAD
- Do not modify: any source, baseline, manifest, governance, test, schema, protocol, or gate file after aggregate begins

**Interfaces:**
- Consumes: closed Task 1 and Task 2 commits.
- Produces: one aggregate gate result and one broad independent merge-readiness verdict.

- [ ] **Step 1: Audit final path scope**

Require a clean worktree, `git diff --check`, the expected changed-path set, and no diff to the closed candidate manifest. Confirm `Directory.Build.props` remains `2.0.0-alpha.1`, `main` remains unchanged, current baseline is 537/133, and canonical replacements remain exported.

- [ ] **Step 2: Run the parent aggregate targeted gate once**

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 1d72384b10dc011388db44042c35daccb0c5411f \
  -- \
  docs/analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/superpowers/plans/2026-07-22-bukit-core-g04d1a-static-notion-facade-removal.md \
  guide/dev/public-api-governance.md \
  src/Bukit-Core/Bukit.Content/Notion/NotionColorPalette.cs \
  src/Bukit-Core/Bukit.Content/Notion/NotionRichTextRenderer.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs \
  tests/Bukit.Content.Tests/BlockRendererMediaAndContainerTests.cs \
  tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs \
  tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs \
  tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs \
  tests/Bukit.Content.Tests/NotionBlockRenderersTests.cs \
  tests/Bukit.Content.Tests/NotionColorPaletteTests.cs \
  tests/Bukit.Content.Tests/NotionRichTextRendererExtendedTests.cs \
  tests/Bukit.Notion.Tests/NotionColorPaletteTests.cs \
  tests/Bukit.Notion.Tests/NotionRichTextRendererExtendedTests.cs
```

If sandbox process or NuGet-cache restrictions cause an environment failure, preserve the failed evidence and rerun the same command outside the sandbox as the required environment retry; do not modify unrelated scripts.

- [ ] **Step 3: Obtain a fresh final aggregate read-only review**

Use a reviewer different from the Task 2 reviewer. Verify the full base-to-HEAD diff, actual gate evidence, commit separation, exact two-type removal, canonical surface stability, test-coverage preservation, baseline/manifest semantics, documentation consistency, and absence of scope drift. Every Critical or Important finding must be resolved and re-reviewed before merge consideration.

- [ ] **Step 4: Hand off through finishing-development-branch**

Present the standard four integration options with base branch `2.0`. Do not merge, push, clean up, or delete the branch until the user chooses.
