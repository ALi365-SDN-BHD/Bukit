# P1 Canonical Content Completion Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Finish the remaining P1 canonical-content migration so Bukit's primary content, render, SEO, and machine-readable consumers read a canonical content graph first and use `Meta` only as compatibility fallback.

**Architecture:** Keep `ContentItem` as provider/raw input, but push all downstream consumers through `ContentRecord`-derived access. Close the biggest P1 gaps by making SEO models, JSON-LD, search index, page/list render models, and list route indexing canonical-first, then tighten provider-side summary compatibility so structured fields remain authoritative.

**Tech Stack:** .NET, xUnit, Bukit Engine, Bukit Content providers

---

### Task 1: Lock canonical-first SEO behavior with tests

**Files:**
- Modify: `tests/Bukit.Engine.Tests/SeoModelBuilderTests.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoIndexBuilderTests.cs`

**Step 1: Write the failing tests**

Add tests asserting:
- `SeoModelBuilder.BuildForContent` prefers field/canonical summary, author, tags, language, updated time, and media over legacy `Meta`.
- `SeoIndexBuilder.Build` stores canonical collection/type metadata when only fields carry the semantic value.

**Step 2: Run targeted tests to verify they fail**

Run: `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "FullyQualifiedName~SeoModelBuilderTests|FullyQualifiedName~SeoIndexBuilderTests"`

**Step 3: Write minimal implementation**

Update engine builders to derive SEO facts from canonical-access helpers or `ContentRecord` before falling back to `Meta`.

**Step 4: Re-run targeted tests to verify they pass**

Run the same command and confirm green.

### Task 2: Lock canonical-first page/list/search projection behavior with tests

**Files:**
- Modify: `tests/Bukit.Engine.Tests/SearchIndexBuilderTests.cs`
- Modify: `tests/Bukit.Engine.Tests/PageRenderDispatcherLazyBodyTests.cs`
- Modify: `tests/Bukit.Engine.Tests/SpecialListRendererNestedParallelTests.cs`

**Step 1: Write the failing tests**

Add tests asserting:
- search index emits canonical summary, classification, language, provenance, entities
- page render model exposes canonical summary/trust/provenance even when `Meta` omits them
- list page infos prefer canonical summary for source items

**Step 2: Run targeted tests to verify they fail**

Run: `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "FullyQualifiedName~SearchIndexBuilderTests|FullyQualifiedName~PageRenderDispatcherLazyBodyTests|FullyQualifiedName~SpecialListRendererNestedParallelTests"`

**Step 3: Write minimal implementation**

Update render/search builders to compute one canonical record per item and populate `PageInfo` / search documents from it.

**Step 4: Re-run targeted tests to verify they pass**

Run the same command and confirm green.

### Task 3: Keep Notion compatibility while making structured fields authoritative

**Files:**
- Modify: `tests/Bukit.Content.Tests/NotionContentProviderEndToEndTests.cs`
- Modify: `src/Bukit.Content/Notion/NotionContentProvider.cs`

**Step 1: Write the failing test**

Add a test asserting auto-generated summary updates structured fields as well as compatibility meta so canonical ingestion keeps a single authoritative value.

**Step 2: Run the targeted test to verify it fails**

Run: `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --filter "FullyQualifiedName~NotionContentProviderEndToEndTests"`

**Step 3: Write minimal implementation**

When Notion auto-summary fills a missing summary, update the `summary` field payload in addition to compatibility `Meta`.

**Step 4: Re-run the targeted test to verify it passes**

Run the same command and confirm green.

### Task 4: Verify full P1 slice

**Files:**
- No code changes expected

**Step 1: Run engine/content tests**

Run:
- `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`
- `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj`
- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`

**Step 2: Review remaining direct `Meta` reads**

Run: `rg -n "MetaHelpers.GetString\\(item.Meta|TryGetValue\\(\"summary\"|GetStringList\\(item.Meta" src/Bukit.Engine src/Bukit.Content`

**Step 3: Confirm remaining reads are compatibility-only**

Anything still on critical P1 paths must be eliminated or intentionally documented as compatibility fallback.
