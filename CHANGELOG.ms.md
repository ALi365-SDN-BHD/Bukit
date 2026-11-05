# Log Perubahan

Semua perubahan penting kepada Bukit akan didokumenkan dalam fail ini.

## [Belum Dikeluarkan]

- **Pembaikan audit Analytics**: serpihan permulaan head mengikut pengisytiharan pengekodan halaman, laporan keperluan CSP menambah `connectSrc` dan `imgSrc`, dan iframe `<noscript>` GTM tidak dijana apabila mana-mana nilai lalai persetujuan Google ialah `denied`.
- **Pembaikan audit SEO**: canonical kandungan relatif-akar diselesaikan terhadap `site.url` + `baseUrl` tanpa mengulang laluan base yang sedia ada, bentuk relatif lain dilaporkan sebagai `seo.canonical_relative`, dan `og:locale` menggunakan pemisah garis bawah seperti `zh_CN` dengan diagnostik `seo.og_locale_region_missing` untuk bahasa tanpa wilayah.
- **Polisi perangkak AI**: `robots.txt` mengumpulkan perangkak AI mengikut tujuan (latihan, carian AI, ambilan pengguna) dan menambah `aiBotMode: block-training` (ia juga menyekat `Google-Extended`, yang mengawal latihan dan grounding Gemini; binaan merekodkan `event=robots.ai_grounding_blocked`). Perubahan tingkah laku: mod `selective` dengan `aiBotAllowList` kini menyekat semua perangkak lain yang dikenali, termasuk perangkak carian AI; binaan memberi amaran `event=robots.ai_search_blocked`. Binaan juga memberi amaran `event=robots.ai_policy_inactive` apabila polisi AI dikonfigurasi tetapi tidak dapat ditulis.
- **Skop sitemap**: sitemap hanya menyenaraikan canonical di bawah `site.url` + `baseUrl` (port lalai dan huruf hos dinormalkan), sekali setiap satu; audit penerbitan tidak lagi menjangka halaman yang canonical-nya menunjuk ke tempat lain.
- **Penemuan ejen AI**: apabila GEO didayakan dalam mod `inject`, halaman kandungan yang boleh diindeks memautkan versi Markdown dan JSON-nya dengan `<link rel="alternate">`, dan `llms.txt` memautkan `agent-manifest.json` dalam bahagian `## Optional`. Menukar `site.seo.geo.enabled` kini memaparkan semula halaman dalam binaan tokokan. Dalam mod `theme` dan `off`, susun atur boleh membaca URL yang sama daripada `page.representation_urls`, yang hanya ditetapkan untuk halaman diterbitkan tanpa `noindex`. `agent-manifest.json` kini ditulis tanpa BOM UTF-8.

## [2.0.7] - 2026-11-04

- **Versi**: versi produk dalam `Directory.Build.props` ditetapkan kepada 2.0.7 supaya sepadan dengan barisan keluaran (v2.0.6 dikeluarkan ketika fail masih 2.0.4; binaan keluaran menimpanya dengan `-p:VersionPrefix`).
- **Ujian kendiri regresi keselamatan**: setiap pemilih dalam `scripts/security/security-regression.sh` kini mesti sepadan dengan pengisytiharan ujian sebenar dalam fail yang mempunyai `[Fact]`/`[Theory]`; entri yang tidak dapat dihuraikan gagal dan bukan dilangkau; ujian kendiri dijalankan dalam pintu gerbang CI fast dan menyokong laluan checkout dengan ruang.
- **Dokumen keluaran**: menambah seksyen log perubahan 2.0.0 hingga 2.0.6 dan senarai semak keluaran dalaman 2.0.

## [2.0.6] - 2026-11-03

- Mengintegrasikan peringkat Core 6-10 (penutupan output enjin dan audit, ujian hujung-ke-hujung saluran, liputan CLI dan pemalam, pintu gerbang seni bina/liputan/tiga platform), kontrak ralat pemalam dan penyamaran rahsia, serta pembetulan audit pintu gerbang ujian (PR #66-#86).

### Integrasi Dalaman SEO/GEO

- Menambah kontrak publisher organisasi yang disahkan (`type`, output URL
  HTTP(S) mutlak, dan `sameAs`), unjuran relation silang sumber, author berasaskan
  relation, indexability senarai kosong, entiti syarikat, serta schema snapshot
  dan change-set URL penerbitan yang deterministik.
- Menambah process plugin IndexNow yang diasingkan, contoh projek minimum, dan
  manifest pemasangan dalaman Core/plugin `osx-arm64` dengan SHA-256 sebenar.
  Artifak ini untuk kegunaan dalaman, bukan keluaran awam atau keputusan gate
  release.

### Pembaikan Audit

- Menutup empat belas penemuan audit: front matter Markdown tidak sah kini gagal tertutup dan tidak menggugurkan metadata draf; deployment menolak penulisan melalui symlink `CNAME`/`.nojekyll`; Notion upsert membaca nilai medan unik sebenar dan menolak nilai hilang, tidak disokong, atau padanan berbilang halaman; WeChat berhenti mencuba semula penghantaran draf yang tidak pasti dan mengekalkan keadaan pemulihan.
- Penyetempatan media hanya menulis semula rujukan imej; glob rekursif `**/` merangkumi fail pada akar kandungan; tatasusunan Seed ditulis sebagai jujukan YAML; pemantau Dev merangkumi direktori Markdown tersuai dan input tema warisan; cache Notion menyegarkan pautan bertandatangan yang tamat tempoh dan sasaran relation dalam talian; penyediaan data berbilang bahasa mematuhi pembatalan; Markdown memajukan tetapan auto-summary.
- Menggunakan semula penyelesai laluan canonical bersama dan membuang logik laluan serta pembaca medan yang bertindih/tidak digunakan. Menambah liputan regresi dan panduan pemulihan `DraftSubmitting`.

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

## [2.0.5]

- Versi dilangkau: tiada tag `v2.0.5` atau GitHub Release.

## [2.0.4] - 2026-09-13

- **Transaksi binaan**: mendokumenkan transaksi binaan dan melindungi laluan output berperingkat; membetulkan regresi analisis transaksi binaan.
- **Audit penerbitan**: audit unjuran kini menjejak URL yang diterbitkan.
- **Pembungkusan natif**: pakej natif dikecilkan dan artifak tidak lengkap ditolak.

## [2.0.3] - 2026-09-12

- **Imej responsif**: mengasingkan penemuan sumber imej responsif, membaiki kitar hayat imej responsif, mengekalkan senarai format imej kosong, dan menyegarkan output media tokokan apabila format imej berubah.
- **Laluan dalaman**: laluan dalaman dilindungi daripada output awam.
- **CI**: memperkukuh pintu gerbang CI dan pengesahan bukti keluaran; menjelaskan pengesahan SHA tag keluaran.

## [2.0.2] - 2026-09-12

- **Imej**: menambah pemprosesan imej responsif dengan sandaran ImageSharp; mengoptimumkan regex pemprosesan imej dan normalisasi saiz.

## [2.0.1] - 2026-09-12

- **Keselamatan output**: kitar hayat output awam kini gagal-tutup; laluan output statik `404.html` dikekalkan; laluan relatif pemalam dikuatkuasakan.
- **Preview**: Preview menutup penyajian fail dalaman dan pendedahan LAN; dasar laluan dalaman mengklasifikasi mengikut sasaran fizikal yang diselesaikan, menutup pintasan alias symlink dalam root.
- **Pelaksanaan proses**: alat luaran di macOS dijalankan melalui pembungkus kumpulan proses yang menghapuskan interpolasi shell.
- **Kandungan dan SEO**: unjuran badan Markdown mengekalkan pautan; pembetulan cache Notion dan penghuraian teks; peraturan audit robots.txt dipusatkan; penghalaan HTML dan penemuan media dibetulkan; tajuk halaman disertakan dalam konfigurasi Google Analytics.
- **CI**: keputusan liputan unik dikekalkan dan fixture platform dihadkan; `bukit-test.slnx` kini termasuk `Bukit.Content.Notion.Tests`.

## [2.0.0] - 2026-08-07

- **API awam**: permukaan API awam 2.0 yang disemak diterima dan dasar kestabilan 2.x ditakrifkan; kontrak binari rekod proses dan manifest pemalam dikekalkan.
- **SSRF dan laluan**: pengawal SSRF kongsi gagal-tutup apabila resolusi hos tidak selamat dan mencuba semula alamat sambungan awam; dasar laluan lalai diterbitkan secara atom; laluan mutlak berakar dinormalkan.
- **Kumpulan proses**: Linux menggunakan `setsid` untuk pengurusan pokok proses.
- **Enjin**: atribut URL halaman ringkas dilepaskan; laluan salinan output yang diselesaikan dihormati.

## [2.0.0-RC.1] - 2026-07-24

- Calon keluaran 2.0 pertama. Core diposisikan semula sebagai pengkompil penerbitan deterministik mengutamakan dalaman (lihat Kedudukan Produk di bawah Belum Dikeluarkan).

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
