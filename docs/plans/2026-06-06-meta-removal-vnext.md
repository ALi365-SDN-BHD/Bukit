# Bukit vNext Meta Complete Removal Design

## Goal

Bukit vNext removes `ContentItem.Meta` as an internal business data surface.

The intent is not to stop users from writing Markdown front matter or Notion properties. Those remain valid input formats. The change is that raw provider metadata stops flowing through the engine as the primary truth source. Providers emit raw input documents; the pipeline normalizes them once into a canonical content document; all routing, rendering, SEO, audit, feeds, search, projections, CLI inspection, and plugins consume the canonical model.

This is a breaking redesign. No long-lived compatibility fallback is included in vNext.

## Design Principles

- `Meta` is removed from runtime domain objects, not merely deprecated.
- Raw provider properties are allowed only before normalization.
- Canonical fields are first-class typed properties.
- Custom user fields remain supported through structured `ContentField` / `CustomFields`, not `Meta`.
- Source and sync details are modeled explicitly as provenance/source records.
- Route overrides, SEO controls, publish state, and plugin hints are explicit typed models.
- Unknown raw keys fail during normalization unless mapped to canonical fields or declared as custom fields.
- Plugins move to protocol v2 and cannot mutate page `Meta`.

## New Type Design

### Provider Input Layer

Providers should no longer return `ContentItem` directly. They return raw documents whose only job is to represent external input faithfully.

```csharp
public sealed record RawContentDocument(
    string SourceId,
    string SourceKind,
    string Title,
    string? Slug,
    DateTimeOffset? PublishedAt,
    RawBody Body,
    IReadOnlyDictionary<string, RawContentValue> Properties,
    ContentSourceInfo Source,
    IReadOnlyDictionary<string, ContentField> CustomFields);

public sealed record RawBody(
    string? InlineHtml,
    string? BodyKey,
    string? Markdown,
    string? PlainText);

public sealed record RawContentValue(
    string Kind,
    object? Value);

public sealed record ContentSourceInfo(
    string Provider,
    string? SourceKey,
    string? SourcePath,
    string? ExternalId,
    Uri? ExternalUrl,
    DateTimeOffset? SyncedAt,
    string? SyncStatus);
```

Rules:

- Markdown front matter maps to `RawContentDocument.Properties`.
- Notion properties map to `RawContentDocument.Properties` with original Notion property types preserved in `RawContentValue.Kind`.
- Provider bookkeeping such as `sourceMode`, `sourceKey`, `sourcePath`, `notionPageId`, `notionDatabaseId`, and sync timestamps maps to `ContentSourceInfo`.
- User-defined schema fields map to `CustomFields`.
- Providers do not decide SEO, route, trust, or projection semantics directly.

### Canonical Runtime Layer

`ContentItem` should be replaced by `ContentDocument` as the runtime object used after normalization.

```csharp
public sealed record ContentDocument(
    ContentRecord Record,
    ContentBodyRef Body,
    ContentRoutePolicy Route,
    ContentPublishPolicy Publish,
    IReadOnlyDictionary<string, ContentField> CustomFields,
    IReadOnlyList<ContentDiagnostic> Diagnostics);

public sealed record ContentBodyRef(
    string? Html,
    string? BodyKey,
    string? Markdown,
    string? PlainText);

public sealed record ContentRoutePolicy(
    string? Url,
    string? OutputPath,
    string? Template,
    string? PermalinkPattern,
    string? ListGroup);

public sealed record ContentPublishPolicy(
    bool Draft,
    bool NoIndex,
    bool NoFollow,
    bool ExcludeFromFeed,
    bool ExcludeFromSearch,
    bool ExcludeFromSitemap,
    bool IsDataModule);

public sealed record ContentDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Field,
    string? SourceId);
```

Rules:

- `ContentDocument.Record` is the semantic truth.
- `ContentDocument.CustomFields` is the only dynamic user data surface.
- `ContentRoutePolicy` replaces route-related `Meta` keys such as `url`, `outputPath`, `template`, and nested `route`.
- `ContentPublishPolicy` replaces control keys such as `draft`, `noindex`, data-module flags, and output exclusions.
- `ContentSourceInfo` should be embedded inside or referenced by `ContentRecord.Provenance`.

### Canonical Content Graph

The current `CanonicalContentGraph` should remain the main graph shape, but vNext should tighten ownership:

```csharp
public sealed record CanonicalContentGraph(
    IReadOnlyList<ContentDocument> Documents,
    IReadOnlyList<EntityRecord> Entities,
    IReadOnlyList<ContentRelation> Relations);
```

`ContentRecord` can keep the existing P1 domains:

- `Identity`
- `Presentation`
- `Classification`
- `Ownership`
- `Lifecycle`
- `Provenance`
- `Trust`
- `Entities`
- `Relations`
- `Media`

Changes:

- Move relations to graph-level as well as document-level references.
- Add stable `ContentDocument` wrapper so routing, body, publish controls, custom fields, and diagnostics do not leak into `ContentRecord`.
- Remove canonical fallback reads from `ContentItem.Meta`; the normalizer is the only place allowed to inspect raw properties.

### Normalization Layer

Add a dedicated mapper contract:

```csharp
public interface IContentNormalizer
{
    ContentDocument Normalize(RawContentDocument raw, ContentModelSchema schema);
}

public sealed record ContentModelSchema(
    IReadOnlyDictionary<string, CanonicalFieldMapping> CanonicalMappings,
    IReadOnlyDictionary<string, CustomFieldDefinition> CustomFields,
    IReadOnlyDictionary<string, EntityMapping> EntityMappings,
    IReadOnlyDictionary<string, RelationMapping> RelationMappings);
```

Responsibilities:

- Map raw `type`, `collection`, `language`, `summary`, `author`, dates, tags, media, source, trust, and route keys to typed canonical fields.
- Convert provider-specific keys into source/provenance fields.
- Reject unknown keys unless schema declares them as `CustomFields`.
- Produce diagnostics for missing required canonical fields.
- Build `ContentRoutePolicy` and `ContentPublishPolicy`.

### Plugin Protocol v2

Current plugin protocol models expose `Meta`. vNext should replace that with typed page/document payloads.

```csharp
public sealed record PluginContentDocumentDto(
    ContentRecordDto Content,
    ContentRoutePolicyDto Route,
    ContentPublishPolicyDto Publish,
    IReadOnlyDictionary<string, ContentFieldDto> Fields,
    ContentSourceInfoDto Source);
```

Plugin changes:

- `DerivePagesProtocolModels.Page.Meta` removed.
- `AfterBuildProtocolModels.Page.Meta` removed.
- Plugins that need custom data write `Fields`.
- Plugins that need route changes write `Route`.
- Plugins that need publish controls write `Publish`.
- Plugin protocol version bumps to `2`.

## Affected Modules

### `Bukit.Engine.Abstractions`

Affected:

- `ContentItem`
- `ContentItemExtensions`
- `CanonicalContent`
- plugin protocol DTOs
- `BuildContext`

Required changes:

- Remove `ContentItem.Meta`.
- Introduce `RawContentDocument`, `ContentDocument`, `ContentRoutePolicy`, `ContentPublishPolicy`, `ContentSourceInfo`.
- Remove `ContentItemExtensions` methods that fall back to `Meta`.
- Update plugin protocol models to v2.

### `Bukit.Content`

Affected:

- Markdown provider
- Notion provider
- Composite provider
- body stores
- media/image rewrite pipeline

Required changes:

- Providers emit `RawContentDocument`.
- Markdown front matter and Notion properties no longer become runtime `Meta`.
- Composite provider merges source information and raw/custom fields, not `Meta`.
- Body stores key off `ContentSourceInfo` and `ContentBodyRef`, not `Meta.sourceId`.
- Notion relation/rollup/people/file mapping becomes explicit normalizer input.

### `Bukit.Engine`

Affected:

- `ContentPipeline`
- `CanonicalContentGraphBuilder`
- `CanonicalContentValidator`
- `MetaHelpers`
- `ContentSchemaValidator`
- `RouteInventoryValidator`
- `RoutePipeline`
- `ThemeTemplateResolver`
- `SeoModelBuilder`
- `SeoJsonLdBuilder`
- `SeoIndexBuilder`
- `SearchIndexBuilder`
- `RssGenerator`, `AtomFeedGenerator`, `JsonFeedGenerator`
- `SitemapGenerator` callers
- `PublishDocument`, `PublishAuditBuilder`, audit rules
- `ContentProjectionWriter`
- built-in plugins and output projection consumers

Required changes:

- Replace `ContentPipelineResult.Items` with `Documents`.
- Replace `CanonicalContentGraphBuilder.Build(ContentItem[])` with graph construction from normalized `ContentDocument[]`.
- Delete `MetaHelpers` or shrink it to test-only raw parsing helpers outside runtime engine.
- Update all SEO/feed/search/projection code to use `ContentDocument.Record`, `Route`, `Publish`, and `CustomFields`.
- Remove all `item.Meta` route, SEO, data, source, language, and publish checks.

### `Bukit.Routing`

Affected:

- `RouteGenerator`
- route security validation callers

Required changes:

- Accept `ContentDocument` or a smaller `RoutableContent` interface.
- Read collection/type from `ContentRecord.Classification`.
- Read explicit overrides from `ContentRoutePolicy`.
- Stop reading top-level `url`, `outputPath`, `template`, `route`, or `outputPath` deprecation keys from `Meta`.

### `Bukit.Rendering` and `Bukit.Theme`

Affected:

- page render dispatcher
- Scriban model construction
- `SectionDataResolver`
- list renderers
- known template fields

Required changes:

- Expose `page.content`, `page.route`, `page.publish`, `page.fields`, `page.source`, `page.provenance`, `page.trust`, `page.representations`.
- Remove `page.meta`.
- Section sources read `ContentRecord.Classification.Type`, `Collection`, and `Sections`.
- Filters read `CustomFields`.

### `Bukit.Cli`

Affected:

- `data`
- `route`
- `doctor`
- schema validation commands
- report validators
- import/notion helper output if they assume `Meta`

Required changes:

- Inspect `ContentDocument` instead of `ContentItem`.
- `doctor schema` validates raw input through `ContentModelSchema` and canonical output through `CanonicalContentValidator`.
- CLI JSON outputs rename `meta` to `fields`, `content`, `route`, `publish`, and `source`.

### Tests and Fixtures

Affected:

- Most engine/content tests that construct `ContentItem`.
- Provider tests that assert `item.Meta[...]`.
- Plugin protocol tests.
- Template tests referencing `page.meta`.

Required changes:

- Add test builders for `RawContentDocument` and `ContentDocument`.
- Replace direct `Meta` assertions with canonical field assertions.
- Add explicit breaking tests that compile fails or runtime rejects legacy `page.meta` expectations.

## Breaking Changes

### Public API Breaks

- `ContentItem.Meta` removed.
- `ContentPipelineResult.Items` replaced by `ContentPipelineResult.Documents`.
- `IContentProvider` return type changes from `ContentItem`-based results to `RawContentDocument`-based results.
- `RouteGenerator.Generate(ContentItem, ...)` replaced by `RouteGenerator.Generate(ContentDocument, ...)`.
- `BuildVariantResult.Routed` changes from `(ContentItem, RouteInfo)` to `(ContentDocument, RouteInfo)`.
- Plugin protocol `Page.Meta` removed.
- Plugin protocol version bumped to v2.

### Template Breaks

- `page.meta` removed.
- Legacy template access such as `page.meta.summary`, `page.meta.tags`, `page.meta.author`, `page.meta.source_url`, `page.meta.noindex` no longer works.
- Replacements:
  - `page.content.summary`
  - `page.content.classification.tags`
  - `page.content.ownership.author`
  - `page.content.provenance.original_source`
  - `page.publish.no_index`
  - `page.fields.<customField>`

### Config and Content Breaks

- Unknown front matter keys are rejected unless mapped or declared as custom fields.
- Route overrides must map to `route.url`, `route.outputPath`, `route.template` during normalization.
- Data modules must map to `Publish.IsDataModule` and typed module fields.
- SEO and publish controls must map to typed policy fields.

### Output Breaks

- CLI inspect JSON changes shape.
- Build reports reference canonical content IDs and typed route/source/publish sections.
- Search/feed/audit outputs may change where fields are sourced from, even when values remain the same.

## Migration Order

### Phase 0: Freeze the vNext Contract

Tasks:

1. Add this design as the vNext contract.
2. Add a compile-time inventory test that fails on `.Meta` usages outside approved raw-input namespaces.
3. Define allowed raw-input namespaces:
   - `Bukit.Content`
   - `Bukit.Engine.Normalization`
   - test fixtures for raw provider parsing only
4. Mark all other `.Meta` reads as blockers.

Exit criteria:

- There is a failing inventory test showing all remaining runtime `Meta` dependencies.
- The vNext type names and payload boundaries are fixed.

### Phase 1: Add vNext Types

Tasks:

1. Add `RawContentDocument`, `RawBody`, `RawContentValue`, `ContentSourceInfo`.
2. Add `ContentDocument`, `ContentBodyRef`, `ContentRoutePolicy`, `ContentPublishPolicy`, `ContentDiagnostic`.
3. Add `ContentModelSchema` and normalization mapping types.
4. Add `PluginContentDocumentDto` protocol v2 DTOs.

Exit criteria:

- Types compile.
- Existing behavior unchanged behind old pipeline.
- New type unit tests cover construction and serialization shapes.

### Phase 2: Build the Normalizer

Tasks:

1. Implement `IContentNormalizer`.
2. Port existing canonical mapping from `CanonicalContentGraphBuilder.ToRecord`.
3. Map route, publish, source, body, custom fields.
4. Convert `ContentSchemaValidator` from `Meta` validation to raw-to-canonical validation.
5. Add strict unknown-key diagnostics.

Exit criteria:

- Markdown and Notion raw fixtures normalize to identical `ContentDocument` semantics.
- No engine consumer needs to inspect raw provider properties.

### Phase 3: Convert Providers

Tasks:

1. Convert Markdown provider to emit `RawContentDocument`.
2. Convert Notion provider to emit `RawContentDocument`.
3. Convert Composite provider to merge raw docs and source info.
4. Convert body stores to use `ContentBodyRef` and `ContentSourceInfo`.

Exit criteria:

- Provider tests assert raw properties and normalized canonical fields separately.
- No provider test asserts `ContentItem.Meta`.

### Phase 4: Convert Engine Runtime

Tasks:

1. Change `ContentPipelineResult.Items` to `Documents`.
2. Build `CanonicalContentGraph` from `ContentDocument`.
3. Update routing, render, SEO, search, feed, sitemap, projections, audit, reports.
4. Delete runtime `ContentItemExtensions` fallback methods.
5. Delete or quarantine `MetaHelpers`.

Exit criteria:

- `rg "\\.Meta|MetaHelpers" src/Bukit.Engine src/Bukit.Routing src/Bukit.Rendering src/Bukit.Theme src/Bukit.Cli` has no runtime business hits.
- Full engine test suite passes.

### Phase 5: Convert Templates and Plugin Protocol

Tasks:

1. Remove `page.meta` from template model.
2. Add canonical template model fields.
3. Update starter theme and fixtures.
4. Add plugin protocol v2.
5. Remove protocol v1 support for vNext.

Exit criteria:

- Template fixtures use `page.content`, `page.fields`, `page.route`, `page.publish`.
- Plugin tests use protocol v2 documents.

### Phase 6: Delete Old Surface

Tasks:

1. Delete `ContentItem.Meta`.
2. Delete old `ContentItem` entirely if `ContentDocument` fully replaces it.
3. Delete old protocol DTOs with `Meta`.
4. Delete old report/test helpers constructing meta-heavy items.
5. Update docs and migration guide.

Exit criteria:

- Repository has no `ContentItem.Meta` symbol.
- `rg "\\bMeta\\b|\\.Meta|MetaHelpers" src tests` only finds unrelated words such as HTML meta tags, metadata assemblies, or docs explaining the removal.

## One-Time Switch Plan

This is the recommended vNext path if we intentionally do not carry compatibility fallback.

### Branch Strategy

- Create a dedicated branch: `codex/vnext-remove-meta`.
- Do not merge partial runtime states into main.
- Keep all breaking changes in one major-version branch until the final test suite passes.

### Execution Shape

1. Add vNext types and normalizer behind new names.
2. Convert providers to raw documents.
3. Convert pipeline to normalize immediately after load.
4. Convert all consumers to `ContentDocument`.
5. Convert templates and plugin protocol.
6. Remove `ContentItem.Meta`.
7. Run full test suite and inventory checks.
8. Update docs and migration notes.

### No-Fallback Rules

- Do not keep `GetTextValue()` fallback to `Meta`.
- Do not keep `page.meta`.
- Do not keep plugin protocol v1 in vNext.
- Do not silently accept unknown front matter keys.
- Do not allow SEO/feed/search/audit to read raw provider properties.

### Temporary Compile Bridges

Allowed only inside the branch:

- `ContentDocumentFactory.FromLegacyItem(...)` may exist in tests while converting suites, but must be deleted before merge.
- `RawContentDocumentFactory.FromLegacyItem(...)` may exist for provider migration tests, but must be deleted before merge.
- Compile bridges must live under `tests` or an explicitly named `MigrationOnly` namespace.

### Final Removal Gate

Before merging vNext:

```bash
rg -n "\\.Meta|MetaHelpers|page\\.meta|\\\"meta\\\"" src tests guide docs
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

Every `rg` hit must be one of:

- HTML `<meta>` support
- .NET metadata assembly/cache references
- migration documentation
- tests explicitly asserting legacy access is gone

## Executable vNext Task Breakdown

### Task A: Contract and Inventory

Files:

- Add: `tests/Bukit.Engine.Tests/MetaRemovalInventoryTests.cs`
- Modify: `docs/plans/2026-06-06-meta-removal-vnext.md`

Checks:

- Inventory test fails on `.Meta` outside allowlisted raw-input paths.
- Inventory test fails on `page.meta`.
- Inventory test fails on `MetaHelpers` outside removed/quarantined paths.

### Task B: Abstractions Types

Files:

- Add: `src/Bukit.Engine.Abstractions/RawContent.cs`
- Add: `src/Bukit.Engine.Abstractions/ContentDocument.cs`
- Modify: `src/Bukit.Engine.Abstractions/CanonicalContent.cs`
- Modify: plugin protocol DTO files

Tests:

- `RawContentDocumentTests`
- `ContentDocumentTests`
- plugin DTO serialization tests

### Task C: Normalization

Files:

- Add: `src/Bukit.Engine/Normalization/ContentNormalizer.cs`
- Add: `src/Bukit.Engine/Normalization/ContentModelSchema.cs`
- Modify: `CanonicalContentGraphBuilder`
- Modify: `CanonicalContentValidator`
- Modify: `ContentSchemaValidator`

Tests:

- Markdown raw input normalizes to canonical document.
- Notion raw input normalizes to the same canonical fields.
- Unknown raw key fails unless declared as custom field.
- Route and publish policies normalize from raw input.

### Task D: Providers

Files:

- Modify: `MarkdownFolderProvider`
- Modify: `NotionContentProvider`
- Modify: `CompositeContentProvider`
- Modify: body stores

Tests:

- Provider tests assert `RawContentDocument.Properties`.
- Normalizer tests assert canonical output.
- No provider test asserts `item.Meta`.

### Task E: Engine Consumers

Files:

- Modify: `ContentPipeline`
- Modify: `RouteInventoryValidator`, `RoutePipeline`, `RouteGenerator`
- Modify: `SeoModelBuilder`, `SeoJsonLdBuilder`, `SeoIndexBuilder`
- Modify: `SearchIndexBuilder`
- Modify: feed/sitemap generators and callers
- Modify: publish audit and projection writers
- Modify: build reports

Tests:

- Route generation from `ContentDocument.Route` and `Record.Classification`.
- SEO/search/feed/audit output from `ContentRecord`.
- No expired/draft/noindex data leaks into outputs.

### Task F: Rendering, Themes, Plugins, CLI

Files:

- Modify: render model builders
- Modify: `SectionDataResolver`
- Modify: starter theme templates
- Modify: plugin protocol host/models
- Modify: `DataCommand`, `RouteCommand`, `DoctorSchemaChecker`

Tests:

- `page.content` and `page.fields` render correctly.
- `page.meta` is unavailable.
- plugin protocol v2 derive pages works.
- CLI inspect output uses typed sections.

### Task G: Deletion and Docs

Files:

- Delete or replace: `ContentItem.Meta`
- Delete or quarantine: `MetaHelpers`
- Update: user guide, dev guide, plugin docs, migration guide

Tests:

- Full test suite.
- Inventory test.
- Sample site build.

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Large compile break after `ContentItem.Meta` deletion | High | Add vNext types first, then convert modules in dependency order |
| Plugin ecosystem break | High | Explicit protocol v2 and migration guide |
| Templates using `page.meta` fail | High | Provide mechanical mapping table and update starter theme first |
| Unknown front matter rejection surprises users | Medium | Add clear diagnostics with mapping suggestions |
| Provider-specific data gets lost | Medium | Preserve raw provider details in `ContentSourceInfo` and declared `CustomFields` |
| Tests become noisy during conversion | Medium | Add builders for `RawContentDocument` and `ContentDocument` early |

## Migration Mapping Cheat Sheet

| Legacy `Meta` key | vNext destination |
|---|---|
| `type` | `ContentRecord.Classification.Type` |
| `collection` | `ContentRecord.Classification.Collection` |
| `collections` | `ContentRecord.Classification.Sections` or schema-defined grouping |
| `tags` | `ContentRecord.Classification.Tags` |
| `summary`, `description`, `excerpt` | `ContentRecord.Presentation.Summary` |
| `language` | `ContentRecord.Presentation.Language` |
| `author` | `ContentRecord.Ownership.Author` |
| `organization` | `ContentRecord.Ownership.Organization` |
| `owner`, `reviewer` | `ContentRecord.Ownership.Owner`, `Reviewer` |
| `updatedAt`, `lastModified` | `ContentRecord.Lifecycle.UpdatedAt` |
| `expiresAt` | `ContentRecord.Lifecycle.ExpiresAt` |
| `source`, `source_url`, `original_url` | `ContentRecord.Provenance` / `ContentSourceInfo` |
| `citations`, `references` | `ContentRecord.Provenance.Citations`, `References` |
| `reviewStatus`, `credibilityScore` | `ContentRecord.Trust` |
| `image`, `cover`, `video`, `attachment` | `ContentRecord.Media` |
| `url`, `outputPath`, `template`, `route` | `ContentRoutePolicy` |
| `draft`, `noindex`, feed/search/sitemap excludes | `ContentPublishPolicy` |
| `sourceMode`, `sourceKey`, `sourcePath`, `sourceId` | `ContentSourceInfo` |
| arbitrary custom keys | `ContentDocument.CustomFields` if declared by schema |

## Definition of Done

vNext Meta removal is complete when:

- No runtime domain type exposes `Meta`.
- No engine, routing, rendering, theme, CLI, feed, search, SEO, audit, projection, or plugin business logic reads raw provider properties.
- Providers emit raw documents only.
- Normalizer is the single place where raw properties become canonical fields.
- Templates use `page.content`, `page.fields`, `page.route`, `page.publish`, `page.source`, `page.provenance`, `page.trust`.
- Plugin protocol v2 has no `Meta`.
- Unknown raw keys produce deterministic diagnostics.
- Full test suite and inventory checks pass.
