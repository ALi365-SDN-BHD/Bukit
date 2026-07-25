# Changelog

All notable changes to Bukit will be documented in this file.

## [Unreleased]

### SEO/GEO Internal Integration

- Added the verified organization publisher contract (`type`, absolute HTTP(S)
  URL output, and `sameAs`), cross-source relation projection, relation-backed
  authors, empty-list indexability, company entities, and deterministic publish
  URL snapshot/change-set schemas.
- Added the isolated IndexNow process plugin, its minimal project example, and
  an `osx-arm64` internal Core/plugin install manifest with real SHA-256 values.
  This is an internal artifact, not a public release or release-gate result.

### Product Positioning

- **Internal-first operating mode**: Bukit Core adopts Route 2, a deterministic
  trusted-content publishing compiler, as its product direction and Route 3,
  an internal stable engine, as its current operating mode. The public
  repository and license remain available, but regular public binary releases,
  public support, compatibility guarantees, SLA commitments, and a fixed
  release cadence are paused.

### Security And Reliability

- **Safe output cleanup (F-01)**: Explicit/configured clean, build cleanup, and recovery now share one guarded cleaner. Dangerous roots, `.git` descendants, outside-project and symlink/reparse targets, and non-empty unmarked directories are refused.
- **Default search UI DOM safety (F-02)**: Content title/snippet values are rendered with text nodes and explicit `<mark>` elements instead of an HTML interpretation sink; placeholder text is encoded.
- **Deterministic output ownership (F-03)**: Static, assets, media, generated tokens, and render destinations are preflighted before publication writes. Cross-category, structural, and case-variant conflicts on case-insensitive volumes fail with `BuildAssetOutputCollision`.
- **Safe recursive discovery (F-04)**: Default content, static, media, hash, and report inventory paths skip directory symlinks and reparse points. Explicit following remains limited to supported copy paths.
- **Fresh template decisions (F-05)**: Capability manifests use content fingerprints, static dependency analysis is call-scoped, and same-process rebuilds observe manifest/root/include/layout changes without mutable cached results leaking to callers.
- **Search content cap enforcement (F-06)**: Existing `site.search.maxContentLength` now applies to document, list, plugin, publish-projection, and multilingual search `content`; default/schema shape are unchanged.
- **Download-level media concurrency (F-07)**: Existing `content.media.maxConcurrency` now limits active localization downloads within rewrite-operation and localized-body-store scopes.
- **Truthful build health (F-08)**: Existing `build-report.v1` fields now contain current build diagnostic counts and a stable public `generatedFiles` inventory; the frozen schema shape is unchanged.

## [1.0.0] - 2026-06-09
### 1.0 Trust Hardening
- **Config strict mode**: `ConfigRemovedFieldScanner` rejects all pre-1.0 config fields (`content.markdown`, `content.provider`, `site.rssMode`, etc.) with migration hints.
- **Publish audit**: `MachineReadabilityTrustAuditBuilder` generates comprehensive SEO/GEO trust reports with multi-format coverage validation.
- **Import safety**: `ImportSafetyScanner` and `HtmlDemoImporter` share a unified `ImportSafetyPatterns` module for consistent sensitive-file detection.
- **Test migration**: All test fixtures migrated from `content.markdown` to `content.sources[].markdown` 1.0 schema.
- **Audit rule fix**: `PublishDocumentAuditScope.IsContentBacked` correctly excludes generated list routes from content-quality audit checks.
- **DevFileWatcher**: Tests now run in serial collection to avoid parallel-execution timing issues.
- **Schema alignment**: Root `site.yaml` updated to 1.0 schema (`content.sources[]` instead of `content.provider`).

### Preview

HTML Demo import (`bukit import html-demo`), import seed (`bukit import seed`), and Notion migration (`bukit notion push`) are Preview features and are **not** part of the Bukit 1.0 stable core contract. These commands may change without notice in future releases.

### Breaking
- **Content meta ABI removed**: `ContentItem.Meta` is no longer part of the runtime content ABI. Providers normalize front matter / Notion properties into typed `ContentField` values and canonical content records.
- **Plugin protocol routed pages now expose canonical content**: protocol routed page payloads include `fields` plus a typed `content` object with identity, lifecycle, provenance, trust, entities, relations, and media.

### Added
- **Document-first publishing foundation**: Added `RawContentDocument`, `ContentDocument`, and `RoutedContentDocument` as the vNext content pipeline types. Route, render, build context, publish projection, and report stages now carry document-first views alongside legacy item tuples during migration.
- **Raw provider ingestion contract**: Added `IRawContentProvider` and connected Markdown, Notion, and composite providers through raw document loading before normalization.
- **Content model schema validation**: Added `ContentModelSchema` and canonical validation rules for status, review/sync status, provenance, ownership, relation targets, media metadata, and entity IDs.
- **Machine-readable publish projections**: per-document JSON/Markdown representations and `agent-manifest.json` are generated from canonical content records.
- **Publish audit command path**: `bukit publish audit` and `bukit publish diff` are the primary audit commands; `seo audit` now prefers `.bukit/publish-audit-report.json` when present.

### Changed
- **Canonical graph enriched**: entities now support URL and `sameAs`; relations include target type/id; media extraction keeps alt/caption/description/license when provided.
- **Template context enriched**: page templates can read `page.content_model`, `page.content_record`, `page.entities`, `page.provenance`, `page.trust`, and `page.representations`.
- **SiteEngine refactored**: 856 → 592 line orchestrator with 8 independent pipeline classes (`BuildPipeline`, `ContentPipeline`, `RoutePipeline`, `RenderPipeline`, `AssetPipeline`, `SeoPipeline`, `PluginPipeline`, `BuildReportPipeline`), plus `ThemeBootstrapper`, `BuildOptionsMapper`, `FixedContentProviderFactory`. Dual `BuildAsync` paths unified into single pipeline chain. All reflection-based test helpers eliminated (zero `BindingFlags` remaining). Added performance regression tests.

### Added (Build Hardening)
- **Build core hardening**: 15-task TDD repair covering static HTML routing, theme inheritance fix, incremental stale cleanup, route/output path security, plugin hardening, safe output filesystem, remote theme reproducibility, and composite template fingerprinting
  - **Plugin environment isolation**: Process plugins now run in a clean environment with only `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, and `BUKIT_OUTPUT_DIR` exposed by default; `allowEnvironment` config allows explicit host variable passthrough
  - **Plugin output limits**: `maxStdoutBytes` / `maxStderrBytes` fields in `externalPlugins` config cap plugin stdout/stderr; exceeding the limit kills the process
  - **Plugin output manifest tracking**: `build-manifest.json` now records structured `pluginOutputs` (plugin/hook/path/hash); stale plugin outputs from previous builds are automatically cleaned during incremental builds
  - **Build asset hash mode**: `build.assetHashMode: "sha256"` enables SHA256 content-based asset copy detection (recommended for CI and network filesystems)
  - **Route security validation**: `RouteSecurityValidator` rejects path traversal (`../`), absolute paths, cross-drive paths, and Windows reserved names (`CON`, `PRN`, etc.) in all generated routes and output paths
  - **Static HTML route inventory**: Static `.html` files are now included in route conflict detection alongside content pages and derived pages
  - **Safe output filesystem**: `IOutputFileSystem` / `SafeOutputFileSystem` API ensures all output write/delete operations stay within the build output root; stale file cleanup uses this guard
  - **Output clean marker**: `build.clean` now requires a `.bukit-output-marker` file (written on every successful build) before deleting the output directory; refuses to clean project root, home directory, filesystem root, or `.git` directories
  - **Remote theme reproducibility**: Cached remote themes no longer auto-`git pull` during build; `@ref` checkouts record the resolved commit in `bukit-theme.lock.json`; mismatched lock commits cause build failure
  - **Composite template fingerprint**: Incremental template hash now combines child/parent/user layouts, `theme.yaml`, and a renderer version marker — parent theme or user layout changes correctly trigger re-rendering
  - **Multilingual concurrency budget**: Multi-language builds now respect a global concurrency budget to prevent resource exhaustion
  - **Theme inheritance asset/static order**: Parent theme `assets/` and `static/` are now copied before child/project directories, ensuring correct override order
  - **Remote theme checkout hardening**: Missing version tags cause immediate build failure instead of silent fallback; Git operations use process tree kill on timeout
  - **Partial route override**: `route.outputPath`-only overrides now correctly derive the corresponding URL
  - Hierarchical taxonomy: `taxonomy.kinds[].hierarchical: true` enables parent-child term relationships via `ParentSlug`, with automatic `children` and `ancestors` computation
  - Term metadata: `_index.md` convention (Hugo-style) at `content/_taxonomy/<kind>/<slug>/_index.md` for per-term description, image, weight, parent
  - RSS 2.0 feeds: each term automatically generates `<output>/<kind>/<slug>/feed.xml`
  - Slug transliteration: Unicode NFD decomposition (`é→e`, `ß→ss`, `æ→ae`, `œ→oe`, `ø→o`), CJK characters preserved
  - Alias redirects: `Aliases` field generates HTML `<meta http-equiv="refresh">` redirect pages
  - Term visibility control: `IsVisible` and `Weight` fields for term ordering and filtering
  - `taxonomy.json` schema upgraded to v2 (includes `children` and `ancestors` arrays)
- **SlugHelper**: Shared slug generation utility in `Bukit.Shared`, consolidating 3 duplicate implementations with Latin transliteration support

### Changed
- **TaxonomyPlugin** refactored from 1194 lines to 245 lines — extracted 7 internal helpers: `TaxonomyIndexBuilder`, `TaxonomyPageCreator`, `TaxonomyDataWriter`, `TaxonomyTemplateResolver`, `TaxonomySortHelper`, `TaxonomyHierarchyBuilder`, `TaxonomyMetadataLoader`
- **TaxonomyTerm model** enriched with `Description`, `Image`, `Weight`, `IsVisible`, `ParentSlug`, `Aliases`, `Pages` fields
- `TaxonomyKindConfig` gains new `Hierarchical` boolean field (default `false`)

### Tests
- 5 new test files (38 test cases): `SlugHelperTests` (22), `TaxonomyHierarchyBuilderTests` (3), `TaxonomyMetadataLoaderTests` (6), `TaxonomyFeedWriterTests` (3), `TaxonomyRedirectWriterTests` (4)
- All 1311 tests passing (Shared 67, Engine 793, Content 451)

## [1.0.6] - 2026-05-21

### Added
- **Shortcodes system**: `theme.shortcodes` in site.yaml — define reusable snippets (`youtube`, `callout`, etc.) that work in both Markdown (`{% name args %}`) and Scriban templates (`{{ shortcode }}`)
- **Content schema validation**: `schema` in collection config — validate Front Matter fields by type (string/number/bool/date/list) with warn/strict failure modes
- **SCSS compilation pipeline**: `theme.scss` — automatic `.scss` → `.css` compilation during build using system `sass`/`dart-sass` CLI
- **Theme inheritance**: `theme.extends` in site.yaml — child themes cascade from parent; template lookup (child-first, parent-fallback), static/assets merging
- **Component-based templates**: `theme.components` — declare reusable components with props in site.yaml, use `{{ comp.render "Name" args }}` in Scriban templates
- **Image optimization pipeline**: `theme.images` — automatic WebP/AVIF conversion using system `cwebp`/`magick` CLI, with `ImageOptimizer.BuildSrcset()` for responsive images
- **HMR development server**: `bukit dev` command — file watching, incremental rebuild on change, WebSocket live reload to all connected browsers, debounced 300ms
- **Shared layout directive parser**: `ScribanLayoutDirectiveParser` extracted to `Bukit.Shared`, eliminating DRY violations between renderer and static analyzer
- **Async I/O in core pipeline**: `TemplateCapabilitiesResolver` now uses `File.ReadAllTextAsync` with `Task<T>` caching for non-blocking YAML manifest loading

### Changed
- **Refactored God Classes**: `SiteEngine` reduced from 1122 to 558 lines (extracted `SeoAlternatesService`, `RobotsTxtWriter`, `StaticFileService`); `PageRenderDispatcher` reduced from 581 to 491 lines (extracted `SpecialListRouteBuilder`)
- **ScribanTemplateRenderer** now accepts `shortcodes` and `components` dictionaries for runtime function injection
- **FileTemplateLoader** supports cascading lookup: primary directory first, optional fallback directory for theme inheritance
- **BuildVariantContext** extended with `ParentLayoutsDir`, `ParentAssetsDir`, `ParentStaticDir` for inherited themes
- **ConfigLoader** extended with new YAML deserialization helpers: `ReadComponents`, `ReadImageOptimizationConfig`, `ReadScssConfig`, `ReadSchema`

## [0.10.0] - 2026-05-05

### Added
- Initial release of Bukit, a .NET 10 Native AOT static site generator
- Markdown content source with Front Matter support
- Notion content source with database mapping and field normalization
- Multi-source content aggregation (`markdown` + `notion`)
- Scriban template engine with AOT-compatible vendored build
- Collections-driven routing system with permalink compatibility layer
- Built-in plugins: sitemap, RSS, search JSON, taxonomy, pagination, archive, pages-index
- Multi-language site support with split/merged/index modes
- Incremental build with manifest-based change detection
- Theme system with `theme list` / `theme use` commands
- Modules data source (`mode=data`) for structured content
- External plugin protocol v1/v2 (process and WASM runtimes)
- Plugin source generator for zero-reflection registration
- `doctor` diagnostic command for environment and configuration checks
- `webhook` command for Notion-to-GitHub Actions repository dispatch
- Intent system for AI-assisted site configuration
- AOT publishing with single-file output
- Performance metrics output (`--metrics`)
- Parallel rendering with configurable concurrency (`--jobs`)
- CI mode (`--ci`) with JSON structured logging
- Comprehensive user documentation (16 chapters, 3 languages)
- Developer documentation (35+ files covering architecture, CLI, plugins, etc.)
- ChatGPT prompt pack for conversational site building
- 322 unit tests across CLI, Content, Engine, and Rendering layers
- Smoke test scripts for Windows and Linux/macOS
- Documentation asset consistency check scripts
