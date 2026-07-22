# Bukit Core G-04D1B Block Renderer Facade Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove exactly 23 `Bukit.Content.Notion.BlockRenderers` compatibility facades from the Bukit 2.0 CLR surface while preserving canonical `Bukit.Notion.Rendering.BlockRenderers` behavior and all G-04D1C legacy extension-graph coverage.

**Architecture:** Treat the 23 wrappers as one atomic duplicate-public-surface cluster. Move direct renderer behavior and security tests to the canonical `Bukit.Notion.Tests` owner, retain legacy helper and extension-graph tests in `Bukit.Content.Tests`, preserve the internal helper bridge without changing behavior, then approve an exact 23-type public API baseline delta and record a 2.0-only governance decision.

**Tech Stack:** C# / .NET 10, xUnit, JSON public API baseline, Bash governance gates, Native AOT.

## Global Constraints

- Work only on `codex/g04d1b-block-renderer-facade-removal`, based on `2.0@136b6ba127ee7edb6a136cf3a70449110ff47d87`.
- Remove exactly the 23 public types listed in the approved design; preserve their canonical `Bukit.Notion.Rendering.BlockRenderers` replacements.
- Preserve `Bukit.Content.Notion.INotionBlockRenderer`, `NotionBlockTransformer`, `NotionBlockRendererRegistry`, `NotionRenderContext`, and `NotionBlocksRenderer` for G-04D1C.
- Preserve `NotionClientStats`, clients, providers, parsers, options, `NotionBlockHelpers`, project references, and all non-D1B Content Notion types.
- Keep `Directory.Build.props` at `2.0.0-alpha.1`; do not modify or merge into 1.x `main`.
- Keep `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` byte-identical to base blob `7b07d6890562387010b52301e9f8716e9bf10ed1`.
- Do not modify schema, plugin protocol, report shape, configuration, asset URL, path utilities, HTTP/TLS, rendering semantics, version, CI, release, or verification scripts.
- Do not add `Obsolete`, type forwarding, compatibility shims, packages, or new production `InternalsVisibleTo` entries.
- The compiled-assembly removal guard must first fail because the exact legacy types still resolve, then pass after deletion.
- Before baseline replacement, public API drift must report exactly 23 target-type removals and no non-target diagnostic.
- After baseline replacement, the current surface must be 14 assemblies / 514 types / 110 `2.0-candidate` entries.
- Each implementation task runs its affected focused check. The parent task runs `post-change-targeted.sh` exactly once after all closure commits.
- Do not run full, release, `test-all`, `smoke-all`, or whole-solution tests.

---

### Task 1: Atomic removal, canonical test ownership, baseline approval, and governance convergence

**Files:**
- Create: `tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs`
- Create: `src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/NotionBlockHelpers.cs`
- Delete: `src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/BlockRendererFacades.cs`
- Move: `tests/Bukit.Content.Tests/BlockRendererExtendedTests.cs` → `tests/Bukit.Notion.Tests/BlockRendererExtendedTests.cs`
- Move: `tests/Bukit.Content.Tests/BlockRendererColorEncodingTests.cs` → `tests/Bukit.Notion.Tests/BlockRendererColorEncodingTests.cs`
- Move: `tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs` → `tests/Bukit.Notion.Tests/BlockRendererUrlSafetyTests.cs`
- Move: `tests/Bukit.Content.Tests/NotionBlockRenderersTests.cs` → `tests/Bukit.Notion.Tests/NotionBlockRenderersTests.cs`
- Split: `tests/Bukit.Content.Tests/BlockRendererMediaAndContainerTests.cs`
- Create: `tests/Bukit.Notion.Tests/BlockRendererMediaAndContainerTests.cs`
- Split: `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs`
- Create: `tests/Bukit.Notion.Tests/NotionBlockRendererEdgeCasesTests.cs`
- Create: `tests/Bukit.Notion.Tests/CanonicalBlockRendererTestSupport.cs`
- Modify: `tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs`
- Modify: `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Create: `docs/analysis/bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `guide/dev/public-api-governance.md`

**Interfaces:**
- Consumes: canonical `Bukit.Notion.Rendering.BlockRenderers.*`, canonical internal `NotionRenderContext`, canonical `NotionBlocksRenderer`, and `Bukit.Notion.Transport.NotionClient` already visible to `Bukit.Notion.Tests`.
- Produces: no legacy D1B renderer exports in `Bukit.Content.dll`, unchanged canonical behavior coverage, preserved D1C tests in `Bukit.Content.Tests`, a 514/110 current baseline, historically correct earlier decisions, and a provisional G-04D1B ledger.

- [ ] **Step 1: Write the exact compiled-assembly RED guard**

Create `G04D1BBlockRendererFacadeRemovalTests.cs` with the exact identities below. Keep JSON and documentation assertions out of the initial RED phase so only assembly removal is under test:

```csharp
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D1BBlockRendererFacadeRemovalTests
{
    private static readonly string[] RendererNames =
    [
        "AudioBlockRenderer",
        "BookmarkBlockRenderer",
        "CalloutBlockRenderer",
        "ChildEntityBlockRenderer",
        "CodeBlockRenderer",
        "ColumnBlockRenderer",
        "ColumnListBlockRenderer",
        "DividerBlockRenderer",
        "EmbedBlockRenderer",
        "EquationBlockRenderer",
        "FileBlockRenderer",
        "ImageBlockRenderer",
        "LinkPreviewBlockRenderer",
        "LinkToPageBlockRenderer",
        "NoOpBlockRenderer",
        "PdfBlockRenderer",
        "RichTextContainerRenderer",
        "SyncedBlockRenderer",
        "TableBlockRenderer",
        "TableOfContentsBlockRenderer",
        "ToDoBlockRenderer",
        "ToggleBlockRenderer",
        "VideoBlockRenderer"
    ];

    [Fact]
    public void BukitContent_DoesNotExposeApprovedLegacyBlockRendererFacades()
    {
        var assembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;

        Assert.All(RendererNames, rendererName => Assert.Null(assembly.GetType(
            $"Bukit.Content.Notion.BlockRenderers.{rendererName}",
            throwOnError: false,
            ignoreCase: false)));
    }

    [Fact]
    public void CanonicalNotionRendering_AllBlockRendererReplacementsRemainPublic()
    {
        var assembly = typeof(Bukit.Notion.Rendering.BlockRenderers.AudioBlockRenderer).Assembly;

        Assert.Equal("Bukit.Notion", assembly.GetName().Name);
        Assert.All(RendererNames, rendererName => Assert.NotNull(assembly.GetType(
            $"Bukit.Notion.Rendering.BlockRenderers.{rendererName}",
            throwOnError: false,
            ignoreCase: false)));
    }
}
```

- [ ] **Step 2: Run the exact RED test**

```bash
env -u NOTION_TOKEN dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --no-restore --nologo \
  --filter FullyQualifiedName~G04D1BBlockRendererFacadeRemovalTests.BukitContent_DoesNotExposeApprovedLegacyBlockRendererFacades
```

Expected: one failed assertion because at least the first exact legacy type resolves. A compile error, missing assets, missing assembly, or unrelated failure is not an accepted RED.

- [ ] **Step 3: Move the four pure renderer test files to the canonical owner**

Move all four files without changing any test body or assertion. Apply these exact header changes:

```csharp
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Rendering.BlockRenderers;
using Xunit;

namespace Bukit.Notion.Tests;
```

Use only the imports actually required by each file. Remove `Bukit.Engine.Abstractions.Content`, `Bukit.Content.Notion`, legacy block-renderer imports, and the D1A aliases. `NotionBlockRenderersTests.cs` must bind `NotionColorPalette` and `NotionRichTextRenderer` directly from `Bukit.Notion.Rendering`; `BlockRendererUrlSafetyTests.cs` must do the same for `NotionRichTextRenderer`.

Preserve every `[Fact]`, `[Theory]`, `[InlineData]`, input payload, assertion, comment explaining a security regression, and test method name.

- [ ] **Step 4: Split the media/container mixed test by ownership**

Create the canonical owner file by moving these exact methods, unchanged except for canonical namespaces and context/client construction:

```text
AudioBlockRenderer_ExternalUrl_RendersAudioLinkAndCaption
ImageBlockRenderer_WithCaption_RendersFigure
PdfBlockRenderer_WithoutCaption_UsesPdfLinkText
EmbedBlockRenderer_NonYouTubeUrl_RendersIframeFigureWithCaption
VideoBlockRenderer_YouTubeUrl_RendersEmbedIframe
VideoBlockRenderer_FileUrl_RendersVideoWithCaption
MediaBlockRenderers_WhenPayloadOrUrlMissing_ReturnNull
SyncedBlockRenderer_WithChildren_RendersChildHtml
SyncedBlockRenderer_WithoutChildrenOrId_ReturnsNull
DividerBlockRenderer_RendersHr
RichTextContainerRenderer_ParagraphWithChildren_AppendsRenderedChildren
RichTextContainerRenderer_ToggleableHeading_RendersDetailsWithChildren
TableBlockRenderer_WithColumnAndRowHeaders_RendersThCells
TableBlockRenderer_WhenMissingTableChildrenOrRows_ReturnsNull
ColumnRenderers_WithChildren_RenderWrappedColumns
ColumnRenderers_WhenMissingInputs_ReturnNullOrEmptyColumn
ToDoAndToggleRenderers_RenderColorsCheckedAndChildren
CalloutEquationBookmarkAndChildRenderers_CoverStyledAndMissingPaths
ImageAndSimpleRenderers_WithMissingContainers_ReturnNull
```

The canonical file header is:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Rendering.BlockRenderers;
using Bukit.Notion.Transport;
using static Bukit.Notion.Tests.CanonicalBlockRendererTestSupport;
using Xunit;

namespace Bukit.Notion.Tests;
```

Create one shared test-only support file and move the common client/handler logic there; do not duplicate it across the two canonical test files:

```csharp
using System.Net;
using System.Text;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Tests;

internal static class CanonicalBlockRendererTestSupport
{
    internal static NotionClient CreateClient(HttpMessageHandler handler)
    {
        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        return new NotionClient(
            options,
            new HttpClient(handler),
            (_, _) => Task.CompletedTask,
            () => DateTimeOffset.UtcNow,
            ownsHttpClient: true);
    }

    internal sealed class JsonHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
    }

    internal sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = _index < responses.Length
                ? responses[_index]
                : "{\"has_more\":false,\"results\":[]}";
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
```

Construct context-dependent renderers with the already-friended canonical types:

```csharp
var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);
```

Leave exactly these two methods in the Content test file with their existing bodies and imports:

```text
NotionColorPalette_MapsForegroundBackgroundAndFallbacks
NotionBlockHelpers_CoverTextColorFileAndVideoUrlBranches
```

- [ ] **Step 5: Split the edge-case mixed test without moving D1A/D1C coverage**

Move these exact 33 direct block-renderer methods to the canonical Notion test file:

```text
LinkToPageBlockRenderer_DatabaseId_DoesNotExposeIdentifier
LinkToPageBlockRenderer_UnknownType_ReturnsNull
LinkToPageBlockRenderer_EmptyTargetId_ReturnsNull
EmbedBlockRenderer_YouTubeUrl_RendersVideoEmbed
ImageBlockRenderer_WithoutCaption_ReturnsImgOnly
FileBlockRenderer_FileTypeUrl_RendersLink
PdfBlockRenderer_FileTypeUrl_RendersLink
PdfBlockRenderer_WithCaption_RendersCaptionText
AudioBlockRenderer_WithoutCaption_RendersAudioLinkOnly
AudioBlockRenderer_DangerousUrl_ReturnsNull
AudioBlockRenderer_ExternalUrl_RendersRelNoopener
ColumnListBlockRenderer_EmptyColumn_ReturnsEmptyWrapper
ColumnBlockRenderer_WidthRatioZero_OutputsNoStyle
ColumnBlockRenderer_WidthRatioOne_OutputsFullFlexStyle
VideoBlockRenderer_WithoutCaption_RendersVideoOnly
VideoBlockRenderer_YouTubeShortUrl_RendersEmbed
VideoBlockRenderer_YouTubeEmbedUrl_RendersEmbed
LinkPreviewBlockRenderer_MissingContainer_ReturnsNull
BookmarkBlockRenderer_WithCaption_RendersAnchorWithText
CalloutBlockRenderer_EmojiIcon_RendersEmojiSpan
CalloutBlockRenderer_NoColor_NoColorClass
TableBlockRenderer_MultiplePages_ConcatenatesRows
TableBlockRenderer_SkipsNonTableRowTypes
TableBlockRenderer_SkipsMalformedTableRows
RichTextContainerRenderer_NoRichText_ReturnsNull
RichTextContainerRenderer_EmptyRichText_ReturnsNull
RichTextContainerRenderer_HeadingEmptyRichText_ReturnsNull
RichTextContainerRenderer_BlockquoteWithChildren_RendersNested
RichTextContainerRenderer_RenderChildrenIfAny_NoId_ReturnsEmptyChildren
RichTextContainerRenderer_ToggleableHeading_NoRichText_ReturnsNull
RichTextContainerRenderer_ToggleableHeading_EmptyRichText_ReturnsNull
SyncedBlockRenderer_OriginalSyncedBlock_NoChildren_ReturnsNull
EquationBlockRenderer_EmptyExpression_ReturnsNull
```

Retain exactly these seven methods in `Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs`:

```text
NotionBlocksRenderer_Registry_ReturnsRegistry
NotionRichTextRenderer_MentionWithoutPlainText_Skipped
NotionRichTextRenderer_TextItemWithoutPlainTextKey_Skipped
NotionRichTextRenderer_UnknownColor_ReturnsInherit
NotionRichTextRenderer_NonArrayValueKind_ReturnsEmpty
NotionBlocksRenderer_NullType_BlockSkipped
NotionBlocksRenderer_HasMoreNoCursor_StopsPagination
```

Keep the existing legacy `NotionApiClient`, `NotionProviderOptions`, `NotionBlocksRenderer`, `JsonHandler`, `SequenceHandler`, and `HttpMessageHandlerStub` helpers with those retained methods. Keep the canonical `NotionRichTextRenderer` alias for the four D1A tests.

The new Notion test file uses the canonical header and the same static import from Step 4. Preserve all moved test bodies; only replace legacy client/context types with canonical types. No D1C method may appear in the new canonical file.

- [ ] **Step 6: Preserve the internal helper bridge and delete the atomic facade cluster**

Create `NotionBlockHelpers.cs` with the exact helper implementation currently at the end of the facade file:

```csharp
using System.Text.Json;
using Canonical = Bukit.Notion.Rendering.BlockRenderers;

namespace Bukit.Content.Notion.BlockRenderers;

internal static class NotionBlockHelpers
{
    internal static string? GetString(JsonElement obj, string name)
        => Canonical.NotionBlockHelpers.GetString(obj, name);

    internal static string ExtractPlainText(JsonElement richTextArray)
        => Canonical.NotionBlockHelpers.ExtractPlainText(richTextArray);

    internal static string GetBlockColorClass(JsonElement typeContainer)
        => Canonical.NotionBlockHelpers.GetBlockColorClass(typeContainer);

    internal static string? GetBlockColor(JsonElement typeContainer)
        => Canonical.NotionBlockHelpers.GetBlockColor(typeContainer);

    internal static string? ExtractFileUrl(JsonElement container)
        => Canonical.NotionBlockHelpers.ExtractFileUrl(container);

    internal static string NotionBlockColorToCssBackground(string notionColor)
        => Canonical.NotionBlockHelpers.NotionBlockColorToCssBackground(notionColor);

    internal static bool IsYouTubeUrl(string url, out string embedUrl)
        => Canonical.NotionBlockHelpers.IsYouTubeUrl(url, out embedUrl);

    internal static string? ExtractQueryParam(string url, string paramName)
        => Canonical.NotionBlockHelpers.ExtractQueryParam(url, paramName);
}
```

Delete `BlockRendererFacades.cs` in full. Do not edit canonical production renderer files.

- [ ] **Step 7: Update only the compiled legacy consumers that reference removed facades**

In `LegacyNotionConsumerFixture.cs`, delete only:

```csharp
typeof(Bukit.Content.Notion.BlockRenderers.ImageBlockRenderer),
typeof(Bukit.Content.Notion.BlockRenderers.TableBlockRenderer)
```

In `NotionBoundaryTests.LegacyContentNotionTypes`, delete only the 23
`Bukit.Content.Notion.BlockRenderers.*` strings. Preserve all other Content and Shared Notion identities and the exact export-set assertion.

- [ ] **Step 8: Run GREEN and prove test preservation**

```bash
env -u NOTION_TOKEN dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore --nologo
env -u NOTION_TOKEN dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore --nologo
env -u NOTION_TOKEN dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore --nologo
```

Expected: all three projects pass with zero skips. The combined Content + Notion test total must remain 756, matching the valid pre-change 670 + 86 total; any decrease means a test was lost during migration.

- [ ] **Step 9: Prove pre-baseline drift is exactly 23 removals**

Run the real drift check before changing the baseline. Require exit 1 and exactly 23 lines of this form, one per `RendererNames` entry:

```text
breaking: Bukit.Content::Bukit.Content.Notion.BlockRenderers.<RendererName>: exported type removed
```

Require zero non-target `breaking:` lines and zero lines beginning with `review-required:`, `protected-review:`, `type-shape-review:`, `contract-shape-review:`, `aot-review:`, `unclassified:`, or `gate-error:`.

- [ ] **Step 10: Generate and semantically approve the 514/110 baseline**

```bash
snapshot_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04d1b-snapshot.XXXXXX")"
snapshot="$snapshot_root/bukit-core-public-api-baseline.v1.json"
expected="$snapshot_root/expected.json"
bash scripts/checks/public-api-drift.sh snapshot "$snapshot" Release
jq 'del(.types[] | select(.assembly == "Bukit.Content" and (.name | startswith("Bukit.Content.Notion.BlockRenderers."))))' \
  docs/governance/bukit-core-public-api-baseline.v1.json > "$expected"
jq -S . "$snapshot" > "$snapshot_root/snapshot.sorted.json"
jq -S . "$expected" > "$snapshot_root/expected.sorted.json"
diff -u "$snapshot_root/expected.sorted.json" "$snapshot_root/snapshot.sorted.json"
```

Require no semantic diff, 514 types, 110 `2.0-candidate` entries, 14 assemblies, zero legacy facade identities, and all 23 canonical identities. Replace the governed baseline only with the reviewed generated snapshot.

- [ ] **Step 11: Extend the architecture guard for baseline and immutable history**

Add repository-root/JSON helpers patterned after `G04D1AStaticNotionFacadeRemovalTests`. Add tests that assert:

```text
schema = bukit-core-public-api-baseline-v1
targetFramework = net10.0
sdkPolicy = no-general-clr-sdk
assemblies = 14
types = 514
2.0-candidate = 110
closed manifest candidateCount = 136
closed manifest declarationState = closed
each of the 23 historical candidates remains consumer-declaration-pending
each privateConsumerStatus remains unknown-until-voluntary-declaration
each searchStatus remains no-public-match-found
```

Also assert `BlockRendererFacades.cs` is absent, `NotionBlockHelpers.cs` is present, all five D1C identities still resolve, and the candidate manifest blob remains `7b07d6890562387010b52301e9f8716e9bf10ed1` when compared to base.

- [ ] **Step 12: Make earlier governance tests historical rather than stale**

In `G04CPublicSurfacePilotTests`, keep all G-04C history assertions; update only the current baseline totals to 514/110 and require current wording that the other 110 candidates are not batch-approved. Keep historical 135 and post-D1A 133 statements.

In `G04D1AStaticNotionFacadeRemovalTests`, rename the current-baseline test to express preservation, update current totals to 514/110, keep both D1A removed identities absent, and retain the historical D1A decision sentence that the other 133 candidates were not batch-approved at that point. Add a separate assertion for the current post-D1B 110-candidate state.

- [ ] **Step 13: Add the exact active G-04D1B governance statement**

Append this sentence to both active governance documents and guard it verbatim:

```text
G-04D1B block-renderer-facade decision: only the 23 `Bukit.Content.Notion.BlockRenderers` facade types recorded in the G-04D1B ledger are approved for removal in 2.0; the other 110 candidates are not batch-approved.
```

State that the canonical namespace is `Bukit.Notion.Rendering.BlockRenderers`, the closed 136-entry manifest is historical and immutable, G-04C 135 and G-04D1A 133 are historical snapshots, the current baseline is 514/110, and all 1.x CLR visibility remains unchanged.

- [ ] **Step 14: Create the provisional decision ledger**

Create the ledger with status:

```text
状态：实施记录已建立 / 跨边界验证与独立复审待执行
```

Record the base commit, task branch, exact 23 identities, canonical namespace mapping, internal helper preservation, six-file test migration/split, D1C retained methods, combined 756-test preservation, 514/110 baseline result, 136-entry manifest blob, source/binary migration instruction, private-consumer uncertainty, non-goals, and the remaining cross-boundary/review checklist. Do not claim Core/Labs/plugins/AOT or independent review has passed yet.

- [ ] **Step 15: Guard the provisional governance state**

Extend `G04D1BBlockRendererFacadeRemovalTests` to require the exact decision sentence, ledger status, 23 names, canonical namespace, 514/110 counts, 136-entry history, D1C boundary, and all non-goals. Require the ledger to say that cross-boundary validation and independent review remain pending.

- [ ] **Step 16: Run all owner checks and one Task 1 focused gate, then commit**

Run the complete Architecture, Content, and Notion projects; require Content + Notion total 756. Run `public-api-drift-self-test.sh` and `public-api-drift.sh check Release`. Run one focused gate with every Task 1 changed path, including both old/new test locations, `CanonicalBlockRendererTestSupport.cs`, the deleted facade, the preserved helper, all architecture tests, fixture, baseline, ledger, declaration, and guide. Commit the cohesive all-green task:

```bash
git commit -m "breaking(content): remove legacy Notion block renderer facades"
```

Expected: no schema, protocol, canonical production renderer, project file, version, gate script, or unrelated baseline identity changed.

---

### Task 2: Cross-boundary proof, first independent review, and truthful closure

**Files:**
- Verify only: `bukit-core.slnx`
- Verify only: `bukit-labs.slnx`
- Verify only: `bukit-plugins.slnx`
- Verify only: `scripts/build/native-aot.sh`
- Verify only: `scripts/smoke/release-artifacts.sh`
- Modify: `docs/analysis/bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `guide/dev/public-api-governance.md`
- Modify: `tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs`

**Interfaces:**
- Consumes: the reviewed Task 1 implementation commit.
- Produces: real cross-boundary evidence, an independent implementation verdict, and a final ledger whose completed claims are executable assertions.

- [ ] **Step 1: Re-run all affected test projects**

Run Architecture, Content, and Notion tests in Release with `NOTION_TOKEN` unset and `--no-restore`. Require explicit zero-failure/zero-skip summaries and Content + Notion total 756.

- [ ] **Step 2: Build Core, Labs, and official plugins**

```bash
dotnet build bukit-core.slnx -c Release --no-restore --nologo
dotnet build bukit-labs.slnx -c Release --no-restore --nologo
dotnet build bukit-plugins.slnx -c Release --no-restore --nologo
```

Require all three to exit 0. If an exact project lacks assets, restore only that project/solution and rerun. Do not treat missing assets or environment failure as build success.

- [ ] **Step 3: Produce and smoke an osx-arm64 Native AOT archive**

```bash
aot_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-g04d1b-aot.XXXXXX")"
archive="$(bash scripts/build/native-aot.sh 2.0.0-alpha.1 osx-arm64 "$aot_root" Release)"
test -s "$archive"
bash scripts/smoke/release-artifacts.sh "$archive" osx-arm64
```

Use a non-restricted environment if required. Do not upload, publish, or retain the temporary artifact as a repository file.

- [ ] **Step 4: Reconfirm public API and immutable cohort**

Run the drift self-test and real check. Require 514/110, all canonical replacements, no legacy facades, and candidate manifest blob `7b07d6890562387010b52301e9f8716e9bf10ed1` from both base and HEAD.

- [ ] **Step 5: Obtain the first independent read-only implementation review**

The reviewer must inspect the Task 1 diff and verify: exact 23-type deletion; helper bridge byte-equivalent logic; all canonical production renderers unchanged; six-file test ownership with no lost cases; seven retained D1A/D1C edge tests; all other Content Notion exports unchanged; exact baseline semantic delta; immutable historical manifest; honest provisional evidence; and no schema/protocol/URL/path/HTTP/TLS/version/gate drift.

Resolve every Critical or Important finding, rerun affected checks, and obtain re-review before continuing.

- [ ] **Step 6: Close the ledger and make closure executable**

Only after Steps 1-5 pass, change status to:

```text
状态：已实施并通过跨边界验证与独立只读复审
```

Record actual test totals, three build results, AOT archive/smoke evidence, public API evidence, immutable manifest comparison, focused checks, and the first review verdict. Keep the parent aggregate gate and final aggregate review explicitly pending.

Update the active governance documents and `G04D1BBlockRendererFacadeRemovalTests` from provisional wording to completed cross-boundary evidence. Require the test to reject the provisional status. Run Architecture plus focused checks for these four paths and commit:

```bash
git commit -m "docs(governance): close G-04D1B decision ledger"
```

---

### Task 3: Single aggregate gate and fresh final diff audit

**Files:**
- Review: every changed path from `136b6ba127ee7edb6a136cf3a70449110ff47d87` through HEAD
- Do not modify: source, baseline, manifest, governance, test, schema, protocol, CI, release, or gate files after the aggregate gate begins

**Interfaces:**
- Consumes: closed Tasks 1-2 commits.
- Produces: one aggregate targeted-gate result and one fresh independent merge-readiness verdict.

- [ ] **Step 1: Audit final scope before aggregate execution**

Require a clean worktree, `git diff --check`, expected changed paths only, no diff to the closed candidate manifest, `Directory.Build.props` still `2.0.0-alpha.1`, local `main` unchanged, current baseline 514/110, canonical replacements exported, and D1C identities still exported.

- [ ] **Step 2: Run the parent aggregate gate exactly once**

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 136b6ba127ee7edb6a136cf3a70449110ff47d87 \
  -- \
  docs/analysis/bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/superpowers/plans/2026-07-23-bukit-core-g04d1b-block-renderer-facade-removal.md \
  docs/superpowers/specs/2026-07-23-bukit-core-g04d1b-block-renderer-facade-removal-design.zh-CN.md \
  guide/dev/public-api-governance.md \
  src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/BlockRendererFacades.cs \
  src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/NotionBlockHelpers.cs \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs \
  tests/Bukit.Architecture.Tests/G04D1AStaticNotionFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/G04D1BBlockRendererFacadeRemovalTests.cs \
  tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs \
  tests/Bukit.Content.Tests/BlockRendererColorEncodingTests.cs \
  tests/Bukit.Content.Tests/BlockRendererExtendedTests.cs \
  tests/Bukit.Content.Tests/BlockRendererMediaAndContainerTests.cs \
  tests/Bukit.Content.Tests/BlockRendererUrlSafetyTests.cs \
  tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs \
  tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs \
  tests/Bukit.Content.Tests/NotionBlockRenderersTests.cs \
  tests/Bukit.Notion.Tests/BlockRendererColorEncodingTests.cs \
  tests/Bukit.Notion.Tests/BlockRendererExtendedTests.cs \
  tests/Bukit.Notion.Tests/BlockRendererMediaAndContainerTests.cs \
  tests/Bukit.Notion.Tests/BlockRendererUrlSafetyTests.cs \
  tests/Bukit.Notion.Tests/CanonicalBlockRendererTestSupport.cs \
  tests/Bukit.Notion.Tests/NotionBlockRendererEdgeCasesTests.cs \
  tests/Bukit.Notion.Tests/NotionBlockRenderersTests.cs
```

If the restricted environment causes a process or NuGet-cache failure, preserve that evidence and rerun the identical command once in a non-restricted environment. Do not edit unrelated scripts to bypass the failure.

- [ ] **Step 3: Obtain a fresh final aggregate read-only review**

Use a reviewer different from the Task 2 first reviewer. Review the full base-to-HEAD diff, actual aggregate evidence, commit boundaries, exact deletion set, helper preservation, test-count preservation, baseline/manifest semantics, documentation history/current-state separation, D1C isolation, and absence of scope drift. Every Critical or Important finding must be resolved and re-reviewed before merge consideration.

- [ ] **Step 4: Hand off through finishing-development-branch**

Present the standard integration options with base branch `2.0`. Do not merge, push, clean up, or delete the task branch until the user chooses.
