# Plugin Terbina Dalam (BuiltIn): Produk dan Sempadan

Halaman ini menerangkan "kontrak output" plugin terbina dalam (fail/halaman yang akan dijana, konfigurasi yang bergantung padanya, dan tingkah laku dalam pelbagai bahasa). Apabila anda mengubah plugin atau tema yang bergantung padanya, utamakan penyelenggaraan halaman ini untuk mengelakkan hanyutan tingkah laku.

Direktori pelaksanaan plugin terbina dalam: `src/Bukit.Engine/Plugins/BuiltIn/`

Dokumen berkaitan:
- [Sistem plugin](./plugins.ms.md)
- [Pelbagai bahasa dan SEO](./i18n-seo.ms.md)
- [Produk tetap enjin](./engine-outputs.ms.md)

## sitemap (IAfterBuildPlugin)

Fail: `SitemapPlugin.cs`

- Output: `<outputDir>/sitemap.xml`
- Kebergantungan: `site.url` mesti dikonfigurasi (jika tidak, terus dilangkau dan tidak dijana)
- Laluan yang disertakan:
  - Halaman tetap enjin: `/`, `/blog/`, `/pages/`
  - Semua halaman kandungan routed
  - Semua laluan derived (daripada plugin derive-pages, seperti taxonomy/pagination/archive)
- Medan dipertingkat (v3.0+):
  - `<priority>`: lalai `site.sitemapDetail.defaultPriority` (0.5), boleh ditindih per halaman melalui front matter `sitemap.priority`
  - `<changefreq>`: lalai `site.sitemapDetail.defaultChangefreq` (weekly), boleh ditindih per halaman melalui front matter `sitemap.changefreq`
  - `<image:image>`: apabila `site.sitemapDetail.imageEnabled: true`, maklumat imej diekstrak daripada front matter `sitemap.images`
  - `<video:video>`: apabila `site.sitemapDetail.videoEnabled: true`, maklumat video diekstrak daripada front matter `sitemap.videos`
- Peraturan lastmod:
  - Halaman kandungan routed: utamakan `fields.update_time` (tarikh yang boleh dihuraikan), jika tiada sandar ke `publishAt`
  - Laluan derived: gunakan `LastModified` yang dipulangkan oleh setiap plugin derive-pages
- Peraturan penyekatan (berdasarkan meta HTML akhir):
  - Jika HTML halaman mengandungi `<meta name="robots" content="noindex|none ...">`, halaman tersebut akan dikeluarkan daripada sitemap
  - Keserasian: `<meta name="sitemap" content="exclude|noindex|false|0">`

Tingkah laku pelbagai bahasa:
- Apabila `site.languages` tidak kosong dan `site.sitemapMode == merged`: plugin ini melangkau penjanaan dalam subdirektori bahasa (enjin menjana merged sitemap di direktori akar)
- Mod lain: setiap direktori output bahasa menjana `sitemap.xml` masing-masing

## feed (IAfterBuildPlugin, v3.0 menggantikan plugin rss asal)

Fail: `FeedPlugin.cs` (`RssPlugin.cs` asal telah dinyahgunakan)

- Output: jana berbilang format mengikut `site.feed.formats`:
  - `rss` → `<outputDir>/rss.xml` (RSS 2.0)
  - `atom` → `<outputDir>/feed/atom.xml` (Atom 1.0)
  - `json` → `<outputDir>/feed/feed.json` (JSON Feed 1.1)
- Kebergantungan: `site.url` mesti dikonfigurasi (jika tidak, terus dilangkau dan tidak dijana)
- Input: hanya menggunakan kandungan routed (tidak termasuk derived)
- Opsyen konfigurasi:
  - `site.feed.formats`: senarai format yang hendak dijana, lalai `["rss"]`
  - `site.feed.limit`: bilangan entri maksimum bagi setiap feed, lalai 20
  - `site.feed.path`: laluan asas fail feed, lalai `feed`
- Feed berasingan per collection:
  - `collection.output.feedPath`: laluan feed tersuai (seperti `blog-feed`)
  - `collection.output.feedTitle`: tajuk feed tersuai
  - `collection.output.feedDescription`: perihalan feed tersuai
- Front matter: `feed.exclude: true` mengecualikan halaman tertentu; `feed.enclosure` menyokong lampiran podcast
- Key suis plugin: `site.plugins.feed` (tidak lagi menggunakan `rss`)

Tingkah laku pelbagai bahasa:
- Apabila `site.languages` tidak kosong dan `site.rssMode == merged`: plugin ini melangkau penjanaan dalam subdirektori bahasa (enjin menjana merged feed di direktori akar)
- Mod lain: setiap direktori output bahasa menjana fail feed masing-masing

## search-index (IAfterBuildPlugin)

Fail: `SearchIndexPlugin.cs`

- Output: `<outputDir>/search.json` + pilihan `bukit-search.html`
- Kebergantungan: tidak bergantung pada `site.url` (boleh digunakan pada laman yang hanya menggunakan pautan relatif)
- Medan kandungan:
  - `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
  - `weight` baharu: ditulis apabila front matter menetapkan `searchWeight`, untuk pengisihan berwajaran di bahagian frontend
- Peningkatan front matter (v3.0+):
  - `searchWeight`: berat carian (lalai 1; nilai lebih tinggi lebih awal dalam pengisihan)
  - `searchExclude: true`: mengecualikan halaman tersebut daripada indeks carian
- Peraturan penjanaan `url`: gabungkan `site.baseUrl` dengan `route.url` halaman (hasilnya ialah laluan dalam laman)
- UI carian terbina dalam (v3.0+):
  - Dayakan dengan konfigurasi `site.search.ui: "default"`
  - Menyokong `site.search.uiTheme` (light/dark/auto)
  - Menyokong `site.search.placeholderText` untuk menyesuaikan teks placeholder
  - Output `bukit-search.html` (JS tanpa kebergantungan ~5KB), boleh dirujuk oleh templat melalui `{{ include }}`

Sama ada memasukkan halaman derived:
- Dikawal oleh `site.searchIncludeDerived`:
  - false: hanya mengindeks routed
  - true: mengindeks routed + derived

Tingkah laku pelbagai bahasa:
- Setiap direktori varian bahasa menjana `search.json` masing-masing
- Jika `site.searchMode == index`, enjin turut menjana `search.index.json` di direktori akar (agregat yang menunjuk kepada indeks setiap bahasa)

## taxonomy (IDerivePagesPlugin + IAfterBuildPlugin)

Fail: `TaxonomyPlugin.cs`

Menjana halaman derived berdasarkan `meta.tags` / `meta.categories` kandungan:

- `/tags/` → `tags/index.html`
- `/tags/<slug>/` → `tags/<slug>/index.html`
- `/categories/` → `categories/index.html`
- `/categories/<slug>/` → `categories/<slug>/index.html`

Nota:
- Halaman derived menggunakan templat: lalai `pages/page.html`
- Boleh dikonfigurasi: `taxonomy.template` / `taxonomy.indexTemplate` / `taxonomy.termTemplate`
- Menyokong penindihan mengikut kind: `taxonomy.templates.tags.*` / `taxonomy.templates.categories.*`
- Keutamaan: index/term peringkat kind > index/term global > template peringkat kind > template global > lalai `pages/page.html`
- Kandungan halaman ialah HTML ringkas yang dijana plugin (senarai ul/li), dan masih ditulis ke `page.content` (serasi dengan tema lama)
- Pada masa yang sama menyuntik medan berstruktur (memudahkan tema merender senarai secara langsung tanpa menghuraikan HTML):
  - Halaman index (`/tags/`, `/categories/`): `page.fields.terms.type == "list"`, `page.fields.terms.value[]` ialah `{ title, slug, url, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
- Halaman term (`/tags/<slug>/`, `/categories/<slug>/`):
  - `page.fields.items.type == "list"`, `page.fields.items.value[]` ialah `{ title, url, publish_date, summary? }`
  - `page.fields.taxonomy.value` ialah `{ kind, term, slug, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
  - `page.fields.pagination.value` ialah `{ page, page_size, total, total_pages, has_prev, has_next }`
- Pengisihan items halaman term:
  - Lalai mengikut `publishAt` menurun
  - Menyokong pin: entri dengan `pinned=true` diletakkan paling hadapan, kemudian mengikut `publishAt` menurun
  - Tertib pin pilihan: apabila `pinOrderField` dikonfigurasi (atau `pinOrderFieldBySource` peringkat source), entri yang dipin akan diisih dahulu mengikut `pinOrder` menaik, kemudian mengikut `publishAt` menurun
  - Kewujudan `pinOrder` dianggap sebagai pin (walaupun tiada `pinned=true` secara eksplisit)
- Peraturan slug: huruf dan angka dikekalkan, selebihnya dimampatkan kepada `-` (huruf kecil); menyokong transliterasi aksara Latin Unicode (`é`→e, `ß`→ss, `æ`→ae, dll.)
- Halaman term menyokong laluan pagination: `/<kind>/<slug>/page/<n>/` (`pageSize` dikawal oleh `taxonomy.pageSize`)
- Pada fasa AfterBuild, output `taxonomy.json` (schema v2), mengandungi data berstruktur semua dimensi taksonomi dan senarai termnya
- Halaman index taxonomy boleh dinyahdayakan: `taxonomy.indexEnabled=false` (atau `taxonomy.kinds[].indexEnabled=false`)
- Konfigurasi medan pin taxonomy:
  - Medan global: `taxonomy.pinField` (lalai `pinned`), `taxonomy.pinOrderField` (pilihan)
  - Pemetaan medan berbilang sumber data: `taxonomy.pinFieldBySource[sourceKey]`, `taxonomy.pinOrderFieldBySource[sourceKey]`
  - Jika bySource tidak dikonfigurasi, semua sumber data menggunakan nama medan global yang sama

### Metadata term (v3.0.0+)

Setiap taxonomy term boleh membawa metadata tambahan, menyokong dua sumber:

1. **Sumber data mod data** (`content/data/tags.yaml` dll.):
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10          # Berat pengisihan; lebih besar lebih awal (lalai 0)
  parent: tech        # Slug term induk (taksonomi berhierarki)
```

2. **Konvensyen _index.md** (meniru Hugo): `content/_taxonomy/<kind>/<slug>/_index.md`

```yaml
---
description: Everything about ML and AI
image: /assets/images/ml-cover.png
weight: 10
parent: tech
---
```

### Taksonomi berhierarki (v3.0.0+)

Dayakan melalui `taxonomy.kinds[].hierarchical: true`, lalu hubungan ibu bapa-anak dikira secara automatik:

```yaml
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true
```

Selepas didayakan:
- Setiap term mengira `children` (anak langsung) dan `ancestors` (rantaian leluhur, dari akar ke semasa) secara automatik
- Boleh digunakan dalam templat untuk navigasi breadcrumb: `page.fields.taxonomy.value.children` / `ancestors`
- Output JSON `taxonomy.json` juga mengandungi tatasusunan `children` dan `ancestors`

### Kawalan keterlihatan term

Tetapkan `IsVisible: false` untuk menyembunyikan term kegunaan dalaman (tidak muncul dalam `terms.value[]` halaman index, tetapi halaman butiran masih boleh diakses).

### RSS feeds for taxonomy terms (v3.0.0+)

Setiap term menjana feed RSS 2.0 bebas secara automatik: `<output>/<kind>/<slug>/feed.xml`

### Redirect alias (v3.0.0+)

Alias yang dikonfigurasi pada medan `Aliases` term akan menjana halaman HTML redirect secara automatik:
`<output>/<kind>/<alias_slug>/index.html` → redirect to `/<kind>/<slug>/`

### Peraturan pengisihan term

- Dalam halaman index dan output JSON, term diisih mengikut `Weight` menurun (berat lebih besar lebih awal), dan jika berat sama mengikut DisplayName menaik
- Term tidak kelihatan (`IsVisible=false`) tidak muncul dalam halaman index

Tambahan Notion:
- taxonomy hanya melihat meta, bukan `page.fields.*`; oleh itu `tags/categories` Notion disarankan mengutamakan `multi_select`
- Jika `tags/categories` Notion anda menggunakan `relation`, Notion provider akan mempromosikan `title` halaman sasaran relation (sandar ke `slug`, kemudian sandar ke `id`) kepada senarai term `meta.tags/meta.categories`, memastikan taxonomy menjana kategori/tag yang boleh dibaca
- Apabila halaman sasaran relation tiada dalam hasil database query semasa, permintaan tambahan Notion `/v1/pages/{id}` akan dibuat untuk melengkapkan title/slug halaman sasaran (maksimum 200, bagi mengelakkan ledakan permintaan)
- Halaman kategori kosong/tag kosong dijana secara automatik (mengelakkan 404 selepas diklik):
  - Jika wujud sumber kandungan `mode: data` dan `name: categories` (atau `name: tags`), enjin menggunakan entri sumber data itu sebagai senarai taxonomy term; walaupun term tersebut belum dirujuk oleh mana-mana artikel, halaman term yang sepadan tetap dijana (slug diutamakan daripada slug entri).
  - Jika menggunakan sumber kandungan Notion, enjin mengekstrak `options[].name` bagi `select/multi_select/status` daripada schema database Notion, lalu memastikan halaman term untuk `tags/categories` (serta medan yang sepadan dengan `taxonomy.kinds[].key`) wujud secara automatik.

Contoh templat (pagination halaman taxonomy term):
```scriban
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <ul>
  {{ for item in page.fields.items.value }}
    <li>
      <a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a>
      {{ if item.publish_date }}
        <small>{{ item.publish_date | date.to_string "%Y-%m-%d" }}</small>
      {{ end }}
    </li>
  {{ end }}
  </ul>

  <nav class="pagination">
    {{ if page.fields.pagination.value.has_prev }}
      <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page - 1 }}/">Prev</a>
    {{ end }}
    <span>Page {{ page.fields.pagination.value.page }} / {{ page.fields.pagination.value.total_pages }}</span>
    {{ if page.fields.pagination.value.has_next }}
      <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page + 1 }}/">Next</a>
    {{ end }}
  </nav>
</article>
```

## pages-index (IDerivePagesPlugin)

Fail: `PagesIndexPlugin.cs`

Menjana data berstruktur "index seluruh laman mengikut id" dan menyuntikkannya ke pemboleh ubah templat:

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`

Kegunaan:
- Apabila templat hanya mempunyai satu pageId (contohnya senarai id yang dipulangkan oleh Notion relation), ia boleh digunakan untuk mendapatkan URL/tajuk dan maklumat lain halaman tersebut

Nota:
- pages-index tidak bergantung pada sumber kandungan: selagi binaan boleh menghasilkan halaman kandungan routed (posts/pages dll.), halaman itu akan masuk ke index
- Index ini hanya meliputi halaman kandungan routed, tidak termasuk laluan derived (taxonomy/pagination/archive dll.)
- Item kandungan `mode: data` tidak menjana halaman routed, maka tidak akan masuk ke `pages_by_id` (melainkan ditulis masuk melalui pelengkapan Notion)
- Pilihan: lakukan "pelengkapan pukal" pageId untuk Notion relation, supaya halaman yang bukan sebahagian laman ini juga ditambahkan ke index (memerlukan `NOTION_TOKEN`):
- Pelengkapan hanya berlaku pada fasa binaan (derive-pages); bacaan `site.data.pages_by_id[...]` dalam templat tidak mencetuskan permintaan API
- Halaman yang dilengkapkan akan menghuraikan Notion properties secara automatik ke dalam `fields` (tidak perlu menentukan nama medan tambahan)
- Pelengkapan hanya didayakan apabila laman menggunakan sumber kandungan Notion; konfigurasi ini diabaikan untuk sumber kandungan lain
- `field_keys`: menentukan medan mana yang hendak diimbas untuk mengumpul relation pageId (nilai medan sepatutnya senarai id pada `page.fields.<key>.value[]`). Jika tidak ditentukan, tiada pelengkapan dilakukan; hanya index halaman routed laman ini dijana.
- Halaman yang dilengkapkan akan mengekstrak medan aras atas Notion `cover` dan `icon` secara automatik lalu menyuntikkannya ke `fields` (selaras dengan tingkah laku `InjectPageCoverAndIcon` dalam saluran kandungan utama)
- URL imej dalam medan halaman yang dilengkapkan (cover, icon, dan medan lain yang ditentukan oleh `content.media.fieldKeys`) akan dimuat turun ke setempat secara automatik melalui `ImageAssetLocalizer` dan ditulis semula sebagai laluan setempat, supaya halaman yang dihasilkan tidak terus merujuk URL sementara Notion S3
- Padanan relation ID menyokong format key dengan awalan sumber (seperti `posts_content:pageId`): jika pageId tertentu sudah wujud dalam index sebagai `sourceKey:pageId`, permintaan Notion API tidak akan dibuat semula

```yaml
theme:
  params:
    pages_index:
      resolve_notion:
        enabled: true
        field_keys: ["related_posts", "payments", "categories"]
        max_items: 200
        concurrency: 4
        max_rps: 3
        max_retries: 5
        request_delay_ms: 0
        cache_mode: readwrite   # off | readwrite | readonly
        cache_path: .cache/notion/pages-index.json
```

## pagination (IDerivePagesPlugin)

Fail: `PaginationPlugin.cs`

Apabila bilangan artikel blog melebihi pageSize, plugin ini menjana halaman pagination derived bagi setiap collection yang mendayakan pagination:

- `/blog/page/2/` → `blog/page/2/index.html`
- … hingga halaman terakhir

Nota:
- Halaman derived menggunakan templat: utamakan `pages/pagination.html`, sandar ke `pages/page.html`
- Kandungan halaman dijana oleh plugin (mengandungi pautan Prev/Next)
- Menyokong pagination berasingan untuk berbilang collection (v3.0+):
  - Setiap collection dengan `pagination.enabled: true` menjana halaman pagination secara berasingan
  - `pagination.pageSize`: bilangan entri per halaman, lalai 10
  - `pagination.urlPattern`: corak URL, placeholder `:num` (lalai `page/:num/`, boleh ditetapkan kepada `p/:num/`)
  - `pagination.firstPageUsesListRoute`: sama ada halaman pertama menggunakan listRoute (lalai true)
- Medan yang disuntik:
  - `page.fields.items.value[]`: senarai artikel halaman semasa (`{title, url, publish_date, summary?}`)
  - `page.fields.pagination.value`: `{page, page_size, total_pages, has_prev, has_next}`

## archive (IDerivePagesPlugin)

Fail: `ArchivePlugin.cs`

Menjana halaman arkib derived mengikut masa penerbitan kandungan:

- `/blog/archive/` → halaman index arkib keseluruhan
- `/blog/archive/<year>/` → halaman tahun
- `/blog/archive/<year>/<month>/` → halaman bulan
- `/blog/archive/<year>/<month>/<day>/` → halaman hari (v3.0+, `depth: daily`)

Nota:
- Halaman derived menggunakan templat: lalai `pages/page.html` (v3.0+ boleh disesuaikan melalui `collection.output.archiveDetail.template`)
- Kandungan halaman dijana oleh plugin (senarai pautan ul/li)
- Konfigurasi dipertingkat (v3.0+):
  - `collection.output.archiveDetail.depth`: `yearly` / `monthly` (lalai) / `daily`
  - `collection.output.archiveDetail.template`: laluan templat tersuai
  - `collection.output.archiveDetail.routePrefix`: awalan URL tersuai (lalai `archive`)

## path-report (IAfterBuildPlugin, plugin luaran)

Fail: `src/plugins/PathReportPlugin/PathReportPlugin.cs`

Plugin untuk nyahpepijat yang menjana laporan audit laluan selepas binaan.

- Output: `<outputDir>/_debug/paths-report.json`
- Order: `int.MaxValue` (dijalankan terakhir)
- Kandungan laporan: rootDir, cacheDir, distDir, themeRoot, layoutsDir, assetsDir, serta senarai fail di bawah setiap direktori

### Konfigurasi

```yaml
site:
  plugins:
    path-report:
      enabled: true
      options:
        wechatMaterialUpload:
          enabled: false
          file: assets/imgs/default.png
          type: image
          wechat:
            appIdEnv: WECHAT_APP_ID
            appSecretEnv: WECHAT_APP_SECRET
```

| Opsyen | Jenis | Lalai | Penerangan |
|---|---:|---|---|
| `wechatMaterialUpload.enabled` | bool | `false` | Sama ada memuat naik bahan ke akaun rasmi WeChat selepas binaan |
| `wechatMaterialUpload.file` | string | `assets/imgs/default.png` | Fail yang hendak dimuat naik (relatif kepada direktori output) |
| `wechatMaterialUpload.type` | string | `image` | Jenis bahan |
| `wechatMaterialUpload.wechat.appIdEnv` | string | - | Nama pemboleh ubah persekitaran yang menyimpan AppID |
| `wechatMaterialUpload.wechat.appSecretEnv` | string | - | Nama pemboleh ubah persekitaran yang menyimpan AppSecret |

Perhatian: laluan fail yang dimuat naik tertakluk kepada kekangan keselamatan dan tidak boleh keluar daripada direktori output.

## llms-txt (IAfterBuildPlugin)

Fail: `LlmsTxtPlugin.cs`

Menjana produk laman mesra AI untuk Generative Engine Optimization (GEO):

- **llms.txt**: fail index Markdown yang mengikut standard [llmstxt.org](https://llmstxt.org), mengandungi seksyen Documentation, Articles dan Optional. Dikawal oleh `site.seo.geo.llmsTxt` (lalai: true). Had bilangan artikel ialah `site.seo.geo.llmsTxtMaxArticles` (lalai: 20).
- **llms-full.txt**: eksport teks penuh semua halaman yang boleh diindeks (HTML dibuang). Dikawal oleh `site.seo.geo.llmsFullTxt` (lalai: false).
- **Peraturan robots.txt untuk perangkak AI**: menambah arahan `Allow`/`Disallow` untuk user-agent perangkak AI yang diketahui (GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI, PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot). Dikawal oleh `site.seo.geo.aiBotMode` (`allow`/`block`/`selective`).

Contoh konfigurasi:

```yaml
site:
  seo:
    geo:
      enabled: true
      llmsTxt: true
      llmsFullTxt: false
      llmsTxtMaxArticles: 20
      aiBotMode: allow
      aiBotAllowList: [GPTBot, PerplexityBot]
      aiBotBlockList: [CCBot]
```

Dokumen berkaitan: [Seni bina GEO](./geo.ms.md)

## related-content (IDerivePagesPlugin, v3.0+)

Fail: `RelatedContentPlugin.cs`

Mengira kandungan berkaitan untuk setiap artikel berdasarkan padanan berwajaran berbilang dimensi seperti tags/categories/keywords/collection/date:

- Konfigurasi: dayakan dengan `site.related.enabled: true`
- `site.related.threshold`: ambang skor minimum (lalai 80)
- `site.related.limit`: bilangan cadangan maksimum per halaman (lalai 5)
- `site.related.indices`: dimensi padanan dan berat, lalai tags(80) + categories(60)
- Dimensi yang disokong: `tags`, `categories`, `keywords`, `collection`/`type`, `date`
- Suntikan data: `context.Data["__related_pages"]`, kamus yang diindeks mengikut content item ID
- Peraturan pengecualian: melangkau halaman derived archive dan pagination secara automatik

## alias (IDerivePagesPlugin, v3.0+)

Fail: `AliasPlugin.cs`

Menjana halaman HTML redirect berdasarkan medan front matter `aliases`:

- Setiap alias menjana satu fail HTML, mengandungi `<meta http-equiv="refresh">` dan `<link rel="canonical">`
- Menyokong rentetan tunggal atau senarai: `aliases: /old-url/` atau `aliases: [/old1/, /old2/]`
- URL dinormalisasi secara automatik (melengkapkan `/` di awal dan akhir)
- Halaman yang dijana ditandakan sebagai `type: redirect`, dan dikecualikan daripada sitemap secara automatik

## data-files (IDerivePagesPlugin, v3.0+)

Fail: `DataFilesPlugin.cs`

Memuatkan fail data YAML/JSON/TOML di bawah direktori `data/`:

- Suntikan data: `context.Data["__data_files"]`
- Menyokong subdirektori bersarang (muat secara rekursif)
- Sokongan pelbagai bahasa: subdirektori `data/{lang}/` dimuat mengikut bahasa
- Dalam pelbagai bahasa: fail akar dikongsi + penindihan khusus bahasa

## menu (IAfterBuildPlugin, v3.0+)

Fail: `MenuPlugin.cs`

Mengeluarkan `menus.json` dan menyuntik `context.Data["menus"]`:

- Konfigurasi: berbilang menu seperti `site.menus.main` / `site.menus.footer`
- Menyokong sarang aras tanpa had (medan `children`)
- Diisih mengikut `weight` (berat lebih kecil lebih awal)
- Diakses dalam templat melalui `site.menus.main` / `site.menus.footer`

## image-processing (IAfterBuildPlugin, v3.0+)

Fail: `ImageProcessingPlugin.cs`

Penjanaan varian imej berbilang saiz berdasarkan alat CLI (ImageMagick):

- Konfigurasi: dayakan dengan `theme.images.enabled: true`
- Menjana varian berbilang saiz untuk imej JPG/PNG di bawah `assets/` (seperti `-480w`, `-768w`, `-1200w`)
- `theme.images.sizes`: senarai saiz, lalai `[480, 768, 1200]`
- `theme.images.quality`: kualiti imej, lalai 80
- Suntikan data: `context.Data["__image_srcsets"]` (data atribut srcset)
- Kebergantungan: ImageMagick perlu dipasang (perintah `magick` atau `convert`); jika tidak dipasang, plugin dilangkau dan amaran dikeluarkan

## Pengesahan laluan halaman derived

Semua plugin derive-pages (Pagination, Archive, Taxonomy) berkongsi saluran pengesahan laluan yang sama:

1. **Pemeriksaan konflik per plugin** — `PluginRunner.ApplyDeriveConflictPolicy` menormalisasi URL dan membandingkan outputPath bagi setiap halaman derived, untuk memeriksa sama ada ia bercanggah dengan laluan kandungan dan laluan derived yang telah diterima.
2. **Pengesahan senarai akhir** — `RouteInventoryValidator.ValidateFinalRoutes` memeriksa set laluan lengkap (kandungan + derived + laluan senarai) sebelum rendering bermula.
3. **Integrasi Doctor** — `bukit doctor` menjalankan pengesahan laluan kandungan melalui `RouteInventoryValidator.BuildContentRoutesAsync` + `ValidateContentRoutes`, membolehkan konflik dikesan tanpa binaan penuh.

Semua halaman derived mematuhi `site.outputPathEncoding` (digunakan melalui `RoutePathBuilder.BuildOutputPathFromUrl`).
