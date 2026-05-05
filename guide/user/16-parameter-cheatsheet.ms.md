# 16 Helaian Tipu Parameter: Semua dalam Satu Halaman (Medan/Maksud/Contoh)

Halaman ini adalah untuk carian pantas. Untuk rujukan medan berwibawa yang lebih lengkap dan butiran pengesahan, lihat: [guide/dev/config-site-yaml](../dev/config-site-yaml.md) dan [guide/dev/cli](../dev/cli.md).

## Parameter CLI Lazim

| Parameter | Maksud | Contoh Lazim |
|---|---|---|
| `--config <path>` | Gunakan fail konfigurasi yang ditentukan (juga menentukan asas laluan relatif) | `--config site.yaml` |
| `--site <name>` | Pelbagai tapak: membaca `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | Tindih direktori output | `--output dist` |
| `--base-url <path>` | Tindih baseUrl (lazim digunakan untuk GitHub Pages) | `--base-url /my-repo` |
| `--site-url <url>` | Tindih URL mutlak tapak (sitemap/rss) | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | Bersihkan direktori output sebelum bina | `--clean` |
| `--draft` | Hasilkan kandungan draf (jika konvensyen tapak menyokongnya) | `--draft` |
| `--no-incremental` | Lumpuhkan binaan tokokan (untuk penyelesaian masalah) | `--no-incremental` |
| `--cache-dir <dir>` | Tentukan direktori cache | `--cache-dir .cache` |
| `--metrics <path>` | Output JSON metrik binaan | `--metrics metrics.json` |
| `--log-format <text|json>` | Format log (CI mengesyorkan json) | `--log-format json` |

## site.* (Peringkat Tapak)

| Medan | Maksud | Contoh |
|---|---|---|
| `site.name` | Pengecam dalaman tapak | `starter` |
| `site.title` | Tajuk paparan tapak | `Bukit Starter` |
| `site.description` | Penerangan tapak (pilihan) | `A site built with Bukit` |
| `site.baseUrl` | Sub-laluan penyahgunaan | `/` atau `/my-repo` |
| `site.url` | URL mutlak tapak (SEO) | `https://user.github.io/my-repo` |
| `site.language` | Bahasa lalai | `zh-CN` |
| `site.languages` | Senarai pelbagai bahasa | `[zh-CN, en-US]` |
| `site.defaultLanguage` | Bahasa lalai pelbagai bahasa | `zh-CN` |
| `site.timezone` | Zon waktu | `Asia/Shanghai` |
| `site.pluginFailMode` | Strategi kegagalan plugin | `strict` / `warn` |
| `site.plugins` | Suis dan parameter plugin | `sitemap: false` / `path-report: { enabled: true, options: {...} }` |
| `site.sitemapMode` | Mod output sitemap | `split` / `merged` / `index` |
| `site.rssMode` | Mod output RSS | `split` / `merged` |
| `site.searchMode` | Mod output carian | `split` / `merged` / `index` |
| `site.autoSummary` | Auto-ekstrak ringkasan dari badan apabila tidak disediakan | `true` / `false` |
| `site.autoSummaryMaxLength` | Panjang maks ringkasan auto (aksara) | `200` |

## content.* (Sistem Kandungan)

### provider=markdown

| Medan | Maksud | Contoh |
|---|---|---|
| `content.provider` | Jenis sumber kandungan | `markdown` |
| `content.markdown.dir` | Direktori akar Markdown | `content` |
| `content.markdown.defaultType` | Jenis lalai | `page` |

### provider=notion

| Medan | Maksud | Contoh |
|---|---|---|
| `content.provider` | Jenis sumber kandungan | `notion` |
| `content.notion.databaseId` | ID Pangkalan Data | `xxxxxxxx-xxxx-...` |
| `content.notion.pageSize` | Saiz halaman (pilihan) | `50` |
| `content.notion.filterProperty` | Nama medan penapis | `Published` |
| `content.notion.filterType` | Jenis penapis | `checkbox_true` |
| `content.notion.sortProperty` | Nama medan isihan | `PublishAt` |
| `content.notion.sortDirection` | Arah isihan | `descending` |
| `content.notion.fieldPolicy.mode` | Dasar medan | `whitelist` / `all` |
| `content.notion.fieldPolicy.allowed` | Medan senarai putih (kunci ternormal) | `[seo_title, seo_desc]` |

### provider=sources (Mod Komposit)

| Medan | Maksud | Contoh |
|---|---|---|
| `content.provider` | Jenis sumber kandungan | `sources` |
| `content.sources[].type` | Jenis sumber | `markdown` / `notion` |
| `content.sources[].name` | Nama sumber | `pages` / `posts` / `modules` |
| `content.sources[].mode` | Mod tingkah laku | `content` / `data` |
| `content.sources[].markdown` | Sub-konfigurasi Markdown | `{ dir: content, defaultType: page }` |
| `content.sources[].notion` | Sub-konfigurasi Notion | `{ databaseId: "...", fieldPolicy: { mode: all } }` |

## build.* (Output Binaan)

| Medan | Maksud | Contoh |
|---|---|---|
| `build.output` | Direktori output | `dist` |
| `build.clean` | Bersihkan sebelum bina | `true` |
| `build.draft` | Hasilkan draf | `false` |

## theme.* (Tema & Templat)

| Medan | Maksud | Contoh |
|---|---|---|
| `theme.name` | Nama tema (themes/&lt;name&gt;) | `alt` |
| `theme.layouts` | Direktori templat (apabila tidak menggunakan theme.name) | `layouts` |
| `theme.assets` | Direktori aset (apabila tidak menggunakan theme.name) | `assets` |
| `theme.static` | Direktori statik (apabila tidak menggunakan theme.name) | `static` |
| `theme.params` | Parameter tema (boleh dibaca oleh templat) | `{ brand: starter }` |

## logging.* (Pengelogan)

| Medan | Maksud | Contoh |
|---|---|---|
| `logging.level` | Tahap log | `info` |
