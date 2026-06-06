# vNext Meta Removal Tasks

## Phase 0: Contract And Inventory

- [x] Add `.trae/specs/vnext-remove-meta/spec.md`.
- [x] Add `.trae/specs/vnext-remove-meta/tasks.md`.
- [x] Add `.trae/specs/vnext-remove-meta/checklist.md`.
- [x] Add a failing inventory test for forbidden runtime `.Meta`, `MetaHelpers`,
      and `page.meta` usages.
- [x] Define the initial allowlist for raw-input and migration-only paths.

## Phase 1: Abstractions Types

- [x] Add failing tests for `RawContentDocument` construction and value shape.
- [x] Add `RawContentDocument`, `RawBody`, `RawContentValue`, and
      `ContentSourceInfo`.
- [x] Add failing tests for `ContentDocument` runtime shape.
- [x] Add `ContentDocument`, `ContentBodyRef`, `ContentRoutePolicy`,
      `ContentPublishPolicy`, and `ContentDiagnostic`.
- [x] Add failing tests for graph-level document and relation ownership.
- [x] Extend `CanonicalContentGraph` for vNext documents without deleting old
      records until downstream modules are converted.

## Phase 2: Normalization

- [x] Add failing tests for raw Markdown-like input normalizing to
      `ContentDocument`.
- [x] Add `IContentNormalizer` and content model schema mapping types.
- [x] Port canonical mapping from `CanonicalContentGraphBuilder.ToRecord`.
- [x] Map route, publish, source, body, and custom fields.
- [x] Map entities, relations, and media.
- [x] Add strict unknown-key diagnostics.

## Phase 3: Providers

- [x] Convert Markdown provider tests to assert raw documents.
- [ ] Add Markdown provider fixture test for normalized canonical output.
- [x] Convert Markdown provider to emit raw documents.
- [x] Convert Notion provider tests to assert raw documents.
- [x] Add Notion raw document adapter.
- [ ] Refactor Notion provider internals to emit raw drafts before legacy
      `ContentItem` adaptation.
- [x] Add Composite raw document adapter.
- [ ] Convert body stores to consume `ContentBodyRef` / `ContentSourceInfo`.

## Phase 4: Engine Runtime

- [x] Add typed `ContentPipelineResult.Documents` outlet.
- [ ] Replace `ContentPipelineResult.Items` with typed documents.
- [x] Build `CanonicalContentGraph` from normalized documents.
- [x] Add typed routing, route source, route inventory, and route pipeline entry
      points for `ContentDocument`.
- [ ] Convert SEO, JSON-LD, sitemap, audit, projections, and
      build reports.
- [x] Add typed search index writer and per-site search index generation for
      `ContentDocument`.
- [x] Add typed feed post projection for `ContentDocument`.
- [ ] Delete runtime `ContentItemExtensions` fallback methods.
- [ ] Delete or quarantine `MetaHelpers`.

## Phase 5: Rendering, Templates, Plugins, CLI

- [ ] Remove `page.meta` from template models.
- [ ] Add `page.content`, `page.route`, `page.publish`, `page.fields`,
      `page.source`, `page.provenance`, `page.trust`, and
      `page.representations`.
- [ ] Update starter theme and fixtures.
- [x] Add plugin protocol v2 DTO tests.
- [x] Add `BuildContext.RoutedDocuments` and after-build protocol v2 routed
      page payload without `meta`.
- [ ] Convert derive-pages protocol host/models to v2 and remove protocol v1.
- [ ] Convert `data`, `route`, `doctor`, and report validation CLI paths.

## Phase 6: Final Deletion And Docs

- [ ] Delete `ContentItem.Meta`.
- [ ] Delete old `ContentItem` if fully replaced by `ContentDocument`.
- [ ] Remove old protocol DTOs with `Meta`.
- [ ] Replace meta-heavy test helpers.
- [ ] Update user guide, developer guide, plugin docs, and migration guide.
- [ ] Run final inventory scan.
- [ ] Run full test suites.
- [ ] Run `bash scripts/quality-gate.sh`.

## Completion Rule

Do not mark this spec complete until the repository has no runtime
`ContentItem.Meta` symbol and no business module depends on raw provider
properties.
