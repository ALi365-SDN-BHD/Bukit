# Changelog

All notable changes to Bukit will be documented in this file.

## [Unreleased]

### Changed
- **SiteEngine refactored**: 856 → 592 line orchestrator with 8 independent pipeline classes (`BuildPipeline`, `ContentPipeline`, `RoutePipeline`, `RenderPipeline`, `AssetPipeline`, `SeoPipeline`, `PluginPipeline`, `BuildReportPipeline`), plus `ThemeBootstrapper`, `BuildOptionsMapper`, `FixedContentProviderFactory`. Dual `BuildAsync` paths unified into single pipeline chain. All reflection-based test helpers eliminated (zero `BindingFlags` remaining). Added performance regression tests.

### Added
- **Taxonomy v3.0.0**: Major overhaul of the taxonomy system with 7 new features
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

## [1.0.0] - 2026-05-05

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
