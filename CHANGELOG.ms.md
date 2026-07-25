# Log Perubahan

Semua perubahan penting kepada Bukit akan didokumenkan dalam fail ini.

## [Belum Dikeluarkan]

### Integrasi Dalaman SEO/GEO

- Menambah kontrak publisher organisasi yang disahkan (`type`, output URL
  HTTP(S) mutlak, dan `sameAs`), unjuran relation silang sumber, author berasaskan
  relation, indexability senarai kosong, entiti syarikat, serta schema snapshot
  dan change-set URL penerbitan yang deterministik.
- Menambah process plugin IndexNow yang diasingkan, contoh projek minimum, dan
  manifest pemasangan dalaman Core/plugin `osx-arm64` dengan SHA-256 sebenar.
  Artifak ini untuk kegunaan dalaman, bukan keluaran awam atau keputusan gate
  release.

### Keselamatan Dan Kebolehpercayaan

- **Pembersihan output selamat (F-01)**: Clean eksplisit/konfigurasi, pembersihan build, dan recovery berkongsi cleaner terkawal. Root berbahaya, descendant `.git`, sasaran di luar projek atau symlink/reparse, dan direktori bukan kosong tanpa marker ditolak.
- **Keselamatan DOM UI carian lalai (F-02)**: Title/snippet kandungan dirender dengan text node dan elemen `<mark>`, bukan sink yang mentafsir HTML; placeholder dikodkan.
- **Pemilikan output deterministik (F-03)**: Destinasi static, assets, media, generated tokens, dan render dipra-semak sebelum penulisan penerbitan. Konflik silang kategori, struktur, dan variasi huruf pada volume case-insensitive gagal dengan `BuildAssetOutputCollision`.
- **Penemuan rekursif selamat (F-04)**: Laluan lalai content, static, media, hash, dan inventori report melangkau symlink direktori/reparse point. Follow eksplisit kekal terhad kepada copy path yang disokong.
- **Keputusan templat semasa (F-05)**: Manifest capability menggunakan fingerprint kandungan, analisis dependensi diasingkan per panggilan, dan rebuild proses sama melihat perubahan manifest/root/include/layout tanpa kebocoran hasil cache boleh ubah.
- **Penguatkuasaan had content carian (F-06)**: `site.search.maxContentLength` sedia ada kini digunakan pada `content` carian document, list, plugin, publish projection, dan berbilang bahasa; default dan bentuk schema tidak berubah.
- **Konkurens media pada tahap muat turun (F-07)**: `content.media.maxConcurrency` sedia ada kini mengehadkan muat turun localization aktif dalam skop rewrite operation dan localized body store.
- **Build health tepat (F-08)**: Medan `build-report.v1` sedia ada kini mengandungi kiraan diagnostik build semasa dan inventori public `generatedFiles` yang stabil; bentuk schema beku tidak berubah.

### Changed
- **SiteEngine direfaktor**: 856 → 592 baris orkestrator dengan 8 kelas pipeline bebas (`BuildPipeline`, `ContentPipeline`, `RoutePipeline`, `RenderPipeline`, `AssetPipeline`, `SeoPipeline`, `PluginPipeline`, `BuildReportPipeline`), serta `ThemeBootstrapper`, `BuildOptionsMapper`, `FixedContentProviderFactory`. Dua laluan `BuildAsync` disatukan menjadi rantaian pipeline tunggal. Semua helper ujian refleksi dihapuskan (sifar `BindingFlags`). Ujian regresi prestasi ditambah.

### Added
- **Taxonomy v3.0.0**: Pembinaan semula utama sistem taxonomy dengan 7 ciri baharu
  - Taxonomy hierarki: `taxonomy.kinds[].hierarchical: true` mendayakan hubungan induk-anak term melalui `ParentSlug`, dengan pengiraan automatik `children` dan `ancestors`
  - Metadata term: Konvensyen `_index.md` (gaya Hugo) di `content/_taxonomy/<kind>/<slug>/_index.md` untuk description, image, weight, parent setiap term
  - Suapan RSS 2.0: Setiap term menjana `<output>/<kind>/<slug>/feed.xml` secara automatik
  - Transliterasi slug: Penguraian Unicode NFD (`é→e`, `ß→ss`, `æ→ae`, `œ→oe`, `ø→o`), aksara CJK dikekalkan
  - Alias redirect: Medan `Aliases` menjana halaman redirect HTML `<meta http-equiv="refresh">`
  - Kawalan keterlihatan term: Medan `IsVisible` dan `Weight` untuk penapisan dan pengisihan
  - Skema `taxonomy.json` dinaik taraf ke v2 (termasuk tatasusunan `children` dan `ancestors`)
- **SlugHelper**: Utiliti penjanaan slug berkongsi dalam `Bukit.Shared`, menggabungkan 3 pelaksanaan pendua dengan sokongan transliterasi Latin

### Changed
- **TaxonomyPlugin** dibina semula dari 1194 baris ke 245 baris — 7 pembantu dalaman diekstrak: `TaxonomyIndexBuilder`, `TaxonomyPageCreator`, `TaxonomyDataWriter`, `TaxonomyTemplateResolver`, `TaxonomySortHelper`, `TaxonomyHierarchyBuilder`, `TaxonomyMetadataLoader`
- **Model TaxonomyTerm** diperkaya dengan medan `Description`, `Image`, `Weight`, `IsVisible`, `ParentSlug`, `Aliases`, `Pages`
- `TaxonomyKindConfig` mendapat medan boolean `Hierarchical` baharu (lalai `false`)

### Ujian
- 5 fail ujian baharu (38 kes ujian): `SlugHelperTests` (22), `TaxonomyHierarchyBuilderTests` (3), `TaxonomyMetadataLoaderTests` (6), `TaxonomyFeedWriterTests` (3), `TaxonomyRedirectWriterTests` (4)
- Semua 1311 ujian lulus (Shared 67, Engine 793, Content 451)

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

### Preview

Perintah import HTML Demo (`bukit import html-demo`), import seed (`bukit import seed`), dan migrasi Notion (`bukit notion push`) adalah ciri Preview dan bukan sebahagian daripada kontrak stabil teras Bukit 1.0. Perintah ini mungkin berubah tanpa notis pada keluaran akan datang.

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
