# 04 Konfigurasi (site.yaml): Penerangan Medan, Lalai & Corak Lazim

`site.yaml` adalah "panel kawalan" tapak anda. Fikirkannya sebagai mentakrifkan: **dari mana kandungan datang, ke mana ia dioutputkan, tema mana yang digunakan, dan fail tambahan apa yang dijana**.

Halaman ini berorientasikan pengguna biasa, menerangkan medan mengikut "senario paling lazim"; jika anda memerlukan rujukan medan yang berwibawa dan butiran pengesahan, lihat dokumen pembangun: [guide/dev/config-site-yaml](../dev/config-site-yaml.md).

## Keutamaan Tindihan (Sangat Penting)

Untuk item konfigurasi yang sama, keutamaan berkesan akhir dari tertinggi ke terendah adalah:

1. Parameter CLI (contohnya, `--output/--base-url/--site-url/--clean/--draft`)
2. `site.yaml`
3. Lalai enjin

Salah faham lazim: anda menukar `site.yaml`, tetapi CLI masih membawa `--output dist2`, jadi ia "kelihatan seperti tidak berkesan."

## Konfigurasi Minimum Berfungsi (Markdown)

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
build:
  output: dist
  clean: true
theme:
  name: alt
logging:
  level: info
```

Lihat contoh boleh laku: `examples/starter/site.yaml`.

## Blok Peringkat Atas: site / content / build / theme / logging

### site: Maklumat Peringkat Tapak (SEO, Pelbagai Bahasa, Strategi Plugin semuanya di sini)

Medan lazim (paling kerap diedit oleh pengguna):

| Medan | Tujuan | Contoh Lazim |
|---|---|---|
| `site.name` | Pengecam dalaman tapak (disyorkan: huruf kecil, pendek) | `starter` |
| `site.title` | Tajuk paparan (digunakan oleh templat/SEO) | `Bukit Starter` |
| `site.baseUrl` | Sub-laluan penyahgunaan (lazim untuk GitHub Pages) | `/` atau `/my-repo` |
| `site.url` | Domain mutlak tapak (digunakan oleh sitemap/rss) | `https://user.github.io/my-repo` |
| `site.language` | Bahasa lalai | `zh-CN` |
| `site.languages` | Senarai pelbagai bahasa (mendayakan i18n) | `[zh-CN, en-US]` |
| `site.defaultLanguage` | Bahasa lalai di bawah pelbagai bahasa | `zh-CN` |
| `site.timezone` | Zon waktu (mempengaruhi paparan tarikh dan beberapa tingkah laku lalai) | `Asia/Shanghai` |
| `site.pluginFailMode` | Strategi kegagalan plugin | `strict` / `warn` |
| `site.plugins` | Suis plugin dan parameter plugin | `sitemap: false` atau `path-report: { enabled: true, options: {...} }` |
| `site.autoSummary` | Sama ada untuk auto-ekstrak ringkasan dari badan apabila tidak disediakan | `true` / `false` |
| `site.autoSummaryMaxLength` | Panjang maks untuk ringkasan auto (aksara) | `200` |
| `site.outputPathEncoding` | Strategi pengekodan laluan output (mengendalikan aksara Cina/khas) | `none` / `slug` / `urlencode` / `sanitize` |
| `site.permalinks` | Struktur URL tersuai mengikut jenis | `post: "/{year}/{month}/{slug}/"` |
| `site.collections` | Konfigurasi penghalaan dipacu collection (disyorkan) | `post: { permalink, template, listRoute }` |

Mod berkaitan output (kritikal untuk pelbagai bahasa):

| Medan | Tujuan | Nilai Lazim |
|---|---|---|
| `site.sitemapMode` | Mod output sitemap | `merged` / `split` / `index` |
| `site.rssMode` | Mod output RSS | `merged` / `split` |
| `site.searchMode` | Mod output carian | `merged` / `split` / `index` |

Cara memilih mod ini: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md).

### site: Ringkasan Auto (Pilihan)

Apabila artikel tidak menyediakan `summary`, anda boleh mendayakan "ringkasan auto" untuk mengekstrak coretan teks biasa dari kandungan badan dan menulisnya ke `meta.summary`, supaya pembacaan taxonomy/RSS/search.json/templat bagi `summary` semuanya mendapat nilai.

```yaml
site:
  autoSummary: true
  autoSummaryMaxLength: 200
```

### site: Struktur URL Tersuai (Permalinks)

Adalah disyorkan untuk mengutamakan `site.collections`; `site.permalinks` terutamanya untuk keserasian.

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

Secara lalai, artikel jenis `post` mempunyai URL `/blog/<slug>/`, dan jenis `page` mempunyai `/pages/<slug>/`. Jika anda mahu menyesuaikan struktur URL (contohnya, termasuk tarikh), anda boleh menggunakan `site.permalinks`:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
```

Kesan: artikel `my-post` yang diterbitkan pada 2025-03-15 akan mempunyai URL `/2025/03/my-post/`.

Pemegang tempat tersedia:

| Pemegang Tempat | Penerangan | Nilai Contoh |
|---|---|---|
| `{slug}` | Slug artikel | `my-post` |
| `{year}` | Tahun terbit (4 digit) | `2025` |
| `{month}` | Bulan terbit (2 digit) | `03` |
| `{day}` | Hari terbit (2 digit) | `15` |
| `{type}` | Jenis kandungan | `post` |

Anda boleh mengkonfigurasi corak berbeza untuk pelbagai jenis secara serentak:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

Nota: Jika artikel mempunyai tindihan laluan (url/outputPath/template) yang ditetapkan melalui Meta atau medan Notion, tindihan laluan mengambil keutamaan berbanding permalinks.

### content: Sumber Kandungan (Markdown / Notion / Pelbagai Sumber)

Anda hanya boleh memilih satu provider:

- `markdown`: Baca Markdown dari folder setempat
- `notion`: Baca dari pangkalan data Notion
- `sources`: Gabungkan pelbagai sumber (disyorkan untuk pemisahan pages + posts + modules)

#### provider=markdown

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
```

| Medan | Tujuan | Nota |
|---|---|---|
| `content.markdown.dir` | Direktori akar Markdown | Membaca secara rekursif `*.md` |
| `content.markdown.defaultType` | Jenis lalai apabila type tidak diisytiharkan | Lazimnya `page` |
| `content.markdown.maxItems` | Item maksimum untuk dibaca | Integer positif; untuk had repo besar |
| `content.markdown.includePaths` | Hanya baca laluan yang ditentukan | Relatif kepada `content.markdown.dir`; `.md` boleh ditinggalkan |
| `content.markdown.includeGlobs` | Hanya baca glob yang sepadan | Sepadan dengan laluan relatif, pemisah ialah `/` |

Pengarangan kandungan Markdown: [05 Kandungan Markdown](./05-markdown-content.md).

#### provider=notion

```yaml
content:
  provider: notion
  notion:
    databaseId: "xxxx"
    pageSize: 50
    filterProperty: Published
    filterType: checkbox_true
    sortProperty: PublishAt
    sortDirection: descending
    fieldPolicy:
      mode: whitelist
      allowed:
        - seo_title
        - seo_desc
        - cover
```

| Medan | Tujuan | Nota |
|---|---|---|
| `content.notion.maxItems` | Item maksimum untuk diambil | Integer positif; untuk had pangkalan data besar |
| `content.notion.includeSlugs` | Hanya ambil slug yang ditentukan | Penapis query pangkalan data (untuk penyahpepijatan halaman tunggal) |
| `content.notion.includeSlugProperty` | Medan yang digunakan oleh includeSlugs | Lalai `Slug`; disyorkan rich_text |
| `content.notion.cacheMode` | Mod cache render Notion | `off`/`readwrite`/`readonly` |
| `content.notion.cacheDir` | Direktori cache | Relatif kepada dir konfigurasi; lalai kepada `<rootDir>/.cache/notion` |
| `content.notion.renderConcurrency` | Konkurens render badan kandungan | Integer positif; lalai setempat 4, CI 2 |
| `content.notion.maxRps` | Had kadar global permintaan Notion | Integer positif; lalai 3 (termasuk query pangkalan data + blocks children) |
| `content.notion.maxRetries` | Cuba semula maks pada 429 | Integer bukan negatif; menghormati undur `Retry-After` |

Prasyarat untuk mod Notion:

- Pembolehubah persekitaran `NOTION_TOKEN` mesti ditetapkan (dilarang sama sekali menulis ke dalam fail repo)

Lihat butiran: [06 Kandungan Notion](./06-notion-content.ms.md).

#### provider=sources (Komposisi pelbagai sumber, menyokong mode=data)

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      markdown:
        dir: content
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Perkara utama:

- Sumber dengan `mode: content` menjana laluan dan halaman
- Sumber dengan `mode: data` tidak menjana laluan, ia disuntik ke dalam `site.modules` (lihat: [09 Modul Data Berstruktur](./09-modules-data.ms.md))
- Apabila sumber `mode: data` dikonfigurasikan dengan `name: categories` (atau `name: tags`), ia digunakan untuk taxonomy: walaupun kategori/teg tertentu pada masa ini tidak mempunyai artikel yang merujuknya, halaman agregasi kosong yang sepadan dijana untuk mengelakkan 404 pada klik.

### build: Direktori Output & Strategi Binaan

| Medan | Tujuan | Contoh Lazim |
|---|---|---|
| `build.output` | Direktori output | `dist` |
| `build.clean` | Sama ada untuk membersihkan output sebelum bina | `true` |
| `build.draft` | Sama ada untuk menghasilkan kandungan draf | `false` (lalai) |
| `build.listPageContentMode` | Strategi pemasangan untuk `pages[*].content` dalam halaman senarai | `auto` |

Parameter CLI setara:

- `--output <dir>` menindih `build.output`
- `--clean/--no-clean` menindih `build.clean`
- `--draft` menindih `build.draft`

Konfigurasi yang disyorkan:

```yaml
build:
  output: dist
  listPageContentMode: auto
```

### theme: Lokasi Tema & Parameter

Pendekatan yang paling disyorkan adalah hanya menentukan `theme.name`, dengan direktori tema di bawah `themes/<name>/`:

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

Jika anda tidak menggunakan direktori themes, anda juga boleh menentukan setiap direktori secara eksplisit:

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

Pembolehubah tema dan templat: [08 Tema & Templat](./08-themes-templates.ms.md).

### logging: Tahap Log (biasanya tidak perlu diubah dengan kerap)

```yaml
logging:
  level: info
```

Dalam senario CI, disyorkan untuk menggabungkan dengan `--log-format json` untuk pengumpulan dan penyelesaian masalah yang lebih mudah (lihat: [12 Rujukan CLI](./12-cli-reference.ms.md)).

## Senario Konfigurasi Lazim (Sedia Salin)

### 1) Sub-Laluan GitHub Pages (baseUrl)

Jika tapak disebarkan di `https://user.github.io/my-repo/`, maka:

- `site.baseUrl` sepatutnya `/my-repo`
- `site.url` sepatutnya `https://user.github.io/my-repo`

Contoh perintah bina:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### 2) Konfigurasi Pelbagai Bahasa Minimum

```yaml
site:
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

Lihat contoh: `examples/starter/site.i18n.yaml`.

### 3) Konfigurasi Modules (data) Minimum

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: content
      mode: content
      markdown: { dir: content, defaultType: page }
    - type: markdown
      name: modules
      mode: data
      markdown: { dir: data, defaultType: module }
```

Lihat contoh: `examples/starter/site.modules.yaml` dan `examples/starter/data/*.md`.

## Perangkap Lazim (Semakan Kendiri Pantas)

- `site.url` tidak ditetapkan: pautan sitemap/rss mungkin tidak betul (boleh ditindih dengan `--site-url`)
- `site.baseUrl` salah konfigurasi: sumber GitHub Pages 404 selepas dibuka (laluan CSS/JS/imej salah)
- Asas laluan relatif disalahfahami: `dir: content` bukan relatif kepada direktori kerja CLI, tetapi relatif kepada direktori yang mengandungi `site.yaml`
- Token Notion ditulis ke dalam YAML: tidak dibenarkan dan tidak selamat, mesti menggunakan pembolehubah persekitaran `NOTION_TOKEN`

