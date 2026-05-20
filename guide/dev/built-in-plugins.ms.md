# Plugin Terbina Dalam (BuiltIn) Artifak dan Sempadan

Halaman ini menerangkan "kontrak output" plugin terbina dalam (fail/halaman yang dijana, konfigurasi yang diperlukan, tingkah laku dalam persekitaran pelbagai bahasa).

Direktori pelaksanaan: `src/Bukit.Engine/Plugins/BuiltIn/`

Dokumen berkaitan: [Sistem Plugin](./plugins.ms.md), [I18n & SEO](./i18n-seo.ms.md), [Output Enjin Tetap](./engine-outputs.zh-CN.md), [GEO](./geo.md)

## Gambaran Keseluruhan Plugin (9 plugin)

| Plugin | Cangkuk | Output Utama |
|--------|------|-----------|
| **CollectionRouteIndex** | (indeks dalaman) | Indeks laluan dalam memori dikumpulkan mengikut koleksi |
| **TaxonomyPlugin** | derive-pages + after-build | Halaman indeks/term taksonomi |
| **PaginationPlugin** | derive-pages | Halaman senarai berhalaman |
| **PagesIndexPlugin** | derive-pages | Indeks halaman JSON untuk kegunaan templat |
| **ArchivePlugin** | derive-pages | Halaman arkib tahunan/bulanan |
| **SitemapPlugin** | after-build | `sitemap.xml` |
| **RssPlugin** | after-build | `rss.xml` |
| **SearchIndexPlugin** | after-build | `search.json` / `search.index.json` |
| **LlmsTxtPlugin** | after-build | `llms.txt` / `llms-full.txt` + peraturan perangkak AI |


## collection-route-index (Indeks Dalaman)

Fail: `CollectionRouteIndex.cs`

Ini bukan pelaksanaan cangkuk plugin tetapi indeks dalam memori dalaman yang digunakan oleh pelbagai plugin (Pagination, Archive, LlmsTxt, Taxonomy). Ia mengumpulkan semua item kandungan routed mengikut kunci `collection` dan menyediakan carian terurut:

- `GetByCollection(collectionKey)` — diisih mengikut `PublishAt` menurun
- `GetByRoutePrefix(prefix)` — ditapis mengikut awalan URL
- `GetOrBuild(context)` — bina malas dan cache dalam `context.Data`

Penyelesaian kunci koleksi: kandungan `meta["collection"]` → sandar ke `meta["type"]`.


## taxonomy (IDerivePagesPlugin + IAfterBuildPlugin)

Fail: `TaxonomyPlugin.cs`

Menjana halaman daripada `meta.tags` / `meta.categories`:
- `/tags/` → `tags/index.html`
- `/tags/<slug>/` → `tags/<slug>/index.html`
- `/categories/` → `categories/index.html`
- `/categories/<slug>/` → `categories/<slug>/index.html`

Templat: lalai `pages/page.html`, boleh dikonfigurasi melalui `taxonomy.template`/`taxonomy.indexTemplate`/`taxonomy.termTemplate`

Didorong oleh konfigurasi nod `taxonomy` dalam site.yaml. Jenis taksonomi tersuai (selain tags/categories) disokong melalui `taxonomy.kinds[].key`.


## pagination (IDerivePagesPlugin)

Fail: `PaginationPlugin.cs`

Menjana halaman senarai tambahan apabila koleksi mempunyai lebih item daripada `pageSize`. Memerlukan `site.collections.<key>.pagination.enabled: true`.

- Dicetuskan apabila `posts.Count > pageSize`
- Menjana halaman di `<listRoute>/page/2/`, `<listRoute>/page/3/`, ...
- Menggunakan templat `pages/pagination.html` apabila dikesan melalui `TemplateCapabilitiesResolver.SupportsPagination()`, jika tidak sandar ke `pages/page.html`
- Setiap halaman mendedahkan `fields.pagination` (page/page_size/total_pages) dan `fields.items` (kepingan koleksi)

Contoh konfigurasi:

```yaml
site:
  collections:
    post:
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
```


## pages-index (IDerivePagesPlugin)

Fail: `PagesIndexPlugin.cs`

Menjana indeks halaman JSON yang digunakan oleh templat yang perlu mengulangi semua halaman. Indeks merangkumi `id`, `title`, `url`, `slug`, `type`, dan nilai medan setiap halaman.

- Untuk kandungan sumber Notion, boleh secara pilihan mengambil data halaman tambahan melalui `INotionPageFetcher`
- Dicache dalam `build-manifest` untuk binaan inkremental
- Digunakan oleh templat melalui `site.data.pages_index`


## archive (IDerivePagesPlugin)

Fail: `ArchivePlugin.cs`

Menjana halaman arkib berhierarki daripada koleksi dengan `listRoute`:

- **Indeks arkib**: `<listRoute>/archive/` — menyenaraikan semua tahun
- **Halaman tahun**: `<listRoute>/archive/2026/` — menyenaraikan bulan dalam tahun tersebut
- **Halaman bulan**: `<listRoute>/archive/2026/05/` — menyenaraikan pos dari bulan tersebut

Koleksi diselesaikan melalui padanan `site.collections` (koleksi pertama dengan `listRoute` yang mempunyai kandungan). Setiap halaman arkib mendedahkan `fields.year`, `fields.month`, dan `fields.posts` (senarai maklumat pos).


## sitemap (IAfterBuildPlugin)

Fail: `SitemapPlugin.cs`
- Output: `<outputDir>/sitemap.xml`
- Kebergantungan: `site.url` mesti dikonfigurasi (dilangkau jika tidak)
- Merangkumi: `/`, `/blog/`, `/pages/`, semua halaman kandungan routed, semua laluan derived
- lastmod: halaman routed mengutamakan `fields.update_time`, sandar ke `publishAt`
- Pengecualian: halaman dengan `<meta name="robots" content="noindex|none ...">` dikecualikan
- Pelbagai bahasa: mod `merged` menjana di akar; `split` menjana setiap direktori bahasa


## rss (IAfterBuildPlugin)

Fail: `RssPlugin.cs`
- Output: `<outputDir>/rss.xml`
- Kebergantungan: `site.url` mesti dikonfigurasi
- Input: kandungan routed sahaja (tiada halaman derived)
- Pelbagai bahasa: semantik `merged`/`split` yang sama seperti sitemap


## search-index (IAfterBuildPlugin)

Fail: `SearchIndexPlugin.cs`
- Output: `<outputDir>/search.json`
- Medan: `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
- `site.searchIncludeDerived` mengawal sama ada halaman derived dimasukkan
- Pelbagai bahasa: `search.json` setiap bahasa; mod `index` menjana `search.index.json` akar


## llms-txt (IAfterBuildPlugin)

Fail: `LlmsTxtPlugin.cs`

Menjana artifak laman mesra AI untuk pengoptimuman enjin generatif (GEO):

- **llms.txt**: Fail indeks Markdown mengikut standard [llmstxt.org](https://llmstxt.org) dengan bahagian Documentation, Articles, dan Optional. Dikawal oleh `site.seo.geo.llmsTxt` (lalai: true). Mengehadkan artikel kepada `site.seo.geo.llmsTxtMaxArticles` (lalai: 20).
- **llms-full.txt**: Eksport teks penuh semua halaman boleh indeks (HTML dilucutkan). Dikawal oleh `site.seo.geo.llmsFullTxt` (lalai: false).
- **Peraturan robots.txt perangkak AI**: Menambah arahan `Allow`/`Disallow` untuk user-agent perangkak AI yang diketahui (GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI, PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot). Dikawal oleh `site.seo.geo.aiBotMode` (`allow`/`block`/`selective`).

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

Berkaitan: [seni bina GEO](./geo.md)


## Pengesahan Laluan untuk Halaman Derived

Semua plugin derive-pages (Pagination, Archive, Taxonomy) berkongsi saluran pengesahan laluan yang sama:

1. **Semakan konflik setiap plugin** — `PluginRunner.ApplyDeriveConflictPolicy` menyemak setiap halaman derived terhadap laluan kandungan dan laluan derived yang diterima sebelum ini menggunakan perbandingan URL dan outputPath yang dinormalkan.
2. **Pengesahan inventori akhir** — `RouteInventoryValidator.ValidateFinalRoutes` menyemak set laluan lengkap (kandungan + derived + laluan senarai) sebelum rendering bermula.
3. **Integrasi Doctor** — `bukit doctor` menjalankan pengesahan laluan kandungan melalui `RouteInventoryValidator.BuildContentRoutesAsync` + `ValidateContentRoutes`, mengesan konflik tanpa binaan penuh.

Semua halaman derived menghormati `site.outputPathEncoding` (digunakan melalui `RoutePathBuilder.BuildOutputPathFromUrl`).
