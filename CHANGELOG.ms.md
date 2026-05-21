# Log Perubahan

Semua perubahan penting kepada Bukit akan didokumenkan dalam fail ini.

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

### Ditambah
- Keluaran pertama Bukit, penjana tapak statik Native AOT .NET 10
- Sumber kandungan Markdown dengan sokongan Front Matter
- Sumber kandungan Notion dengan pemetaan pangkalan data dan penormalan medan
- Agregasi kandungan pelbagai sumber (`markdown` + `notion`)
- Enjin templat Scriban dengan binaan vendored serasi AOT
- Sistem penghalaan dipacu Collections dengan lapisan keserasian permalink
- Plugin terbina dalam: sitemap, RSS, search JSON, taxonomy, pagination, archive, pages-index
- Sokongan tapak pelbagai bahasa dengan mod split/merged/index
- Binaan tokokan dengan pengesanan perubahan berasaskan manifes
- Sistem tema dengan perintah `theme list` / `theme use`
- Sumber data Modules (`mode=data`) untuk kandungan berstruktur
- Protokol plugin luaran v1/v2 (runtime process dan WASM)
- Penjana sumber plugin untuk pendaftaran sifar pantulan
- Perintah diagnostik `doctor` untuk semakan persekitaran dan konfigurasi
- Perintah `webhook` untuk penghantaran repositori Notion-ke-GitHub Actions
- Sistem Intent untuk konfigurasi tapak berbantu AI
- Penerbitan AOT dengan output fail tunggal
- Output metrik prestasi (`--metrics`)
- Rendering selari dengan konkurens boleh konfigurasi (`--jobs`)
- Mod CI (`--ci`) dengan pengelogan berstruktur JSON
- Dokumentasi pengguna komprehensif (16 bab, 3 bahasa)
- Dokumentasi pembangun (35+ fail merangkumi seni bina, CLI, plugin, dll.)
- Pek prompt ChatGPT untuk pembinaan tapak secara perbualan
