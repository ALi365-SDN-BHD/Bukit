# 04 Konfigurasi (`site.yaml`): Penerangan Medan, Tingkah Laku Lalai & Corak Lazim

`site.yaml` ialah “panel kawalan” tapak anda. Anda boleh memahaminya sebagai: **dari mana kandungan datang, ke mana output dihantar, tema apa yang digunakan, dan fail tambahan apa yang dijana**.

Halaman ini ditujukan kepada pengguna umum dan menerangkan medan mengikut “senario paling lazim”; jika anda memerlukan jadual medan berwibawa dan butiran pengesahan, lihat dokumentasi pembangun: [guide/dev/config-site-yaml](../dev/config-site-yaml.ms.md).

## Keutamaan Tindihan (Sangat Penting)

Untuk item konfigurasi yang sama, keutamaan akhir yang berkuat kuasa dari tinggi ke rendah ialah:

1. Parameter CLI (contohnya `--output/--base-url/--site-url/--clean/--draft`)
2. `site.yaml`
3. Nilai lalai enjin

Salah faham lazim: anda telah mengubah `site.yaml`, tetapi CLI masih membawa `--output dist2`, jadi ia “kelihatan seperti tidak berkesan”.

## Konfigurasi Minimum Boleh Guna (Markdown)

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: alt
logging:
  level: info
```

Bandingkan dengan contoh boleh dijalankan: `examples/starter/site.yaml`.

## Blok Peringkat Atas: site / content / build / theme / deploy / logging

### site: Maklumat Peringkat Tapak (SEO, Berbilang Bahasa, Strategi Plugin Semuanya Di Sini)

Medan lazim (yang paling kerap diubah oleh pengguna):

| Medan | Fungsi | Contoh Lazim |
|---|---|---|
| `site.name` | Pengecam dalaman tapak (disyorkan huruf kecil semua dan pendek) | `starter` |
| `site.title` | Tajuk paparan (digunakan untuk templat/SEO) | `Bukit Starter` |
| `site.baseUrl` | Sub-laluan deployment tapak (lazim untuk GitHub Pages) | `/` atau `/my-repo` |
| `site.url` | Domain mutlak tapak (digunakan untuk sitemap/rss) | `https://user.github.io/my-repo` |
| `site.language` | Bahasa lalai | `zh-CN` |
| `site.languages` | Senarai berbilang bahasa (mendayakan i18n) | `[zh-CN, en-US]` |
| `site.defaultLanguage` | Bahasa lalai dalam mod berbilang bahasa | `zh-CN` |
| `site.timezone` | Zon waktu (mempengaruhi paparan tarikh dan beberapa tingkah laku lalai) | `Asia/Shanghai` |
| `site.pluginFailMode` | Strategi kegagalan plugin | `strict` / `warn` |
| `site.plugins` | Suis plugin dan parameter plugin | `sitemap: false` atau `path-report: { enabled: true, options: {...} }` |
| `site.externalPlugins` | Konfigurasi plugin proses luaran | `my-plugin: { runtime: process, entry: ..., hooks: [...] }`. Juga menyokong `maxStdoutBytes`/`maxStderrBytes` (had output), `allowEnvironment` (laluan pemboleh ubah persekitaran), `timeoutMs`, `capabilities` (kotak pasir: `emit-outputs` / `derive-pages`), `options`. |
| `site.autoSummary` | Sama ada mengekstrak ringkasan daripada kandungan badan apabila `summary` tidak disediakan | `true` / `false` |
| `site.autoSummaryMaxLength` | Panjang maksimum ringkasan automatik (bilangan aksara) | `200` |
| `site.outputPathEncoding` | Strategi pengekodan laluan output (mengendalikan aksara Cina/khas) | `none` / `slug` / `urlencode` / `sanitize` |
| `site.permalinks` | Menyesuaikan struktur URL mengikut jenis | `post: "/{year}/{month}/{slug}/"` |
| `site.collections` | Konfigurasi routing dipacu collection (disyorkan) | `post: { permalink, template, listRoute }` |
| `site.seo` | Konfigurasi model SEO peringkat enjin | `enabled/defaultImage/twitterSite/organization` |
| `site.analytics` | Konfigurasi kod analitik (GA4) | `google_analytics_id: G-...` |

Mod yang berkaitan dengan output (sangat penting untuk berbilang bahasa):

| Medan | Fungsi | Nilai Lazim |
|---|---|---|
| `site.sitemapMode` | Mod output sitemap | `merged` / `split` / `index` |
| `site.search.mode` | Mod output search | `merged` / `split` / `index` |

Cara memilih mod ini: [11-Berbilang Bahasa dan SEO](./11-i18n-seo.ms.md).

### site: Konfigurasi Baharu v3.0 (Feed, Sitemap, Carian, Kandungan Berkaitan, Menu, Penomboran Halaman)

| Medan | Fungsi | Nilai Lazim |
|---|---|---|
| `site.feed.formats` | Senarai format Feed | `["rss", "atom", "json"]` |
| `site.feed.limit` | Bilangan item maksimum setiap feed | `20` |
| `site.feed.path` | Awalan laluan output Feed | `feed` |
| `site.sitemapDetail.defaultPriority` | `priority` lalai Sitemap | `0.5` |
| `site.sitemapDetail.defaultChangefreq` | `changefreq` lalai Sitemap | `weekly` |
| `site.sitemapDetail.imageEnabled` | Dayakan Sitemap imej | `true` / `false` |
| `site.sitemapDetail.videoEnabled` | Dayakan Sitemap video | `true` / `false` |
| `site.search.ui` | UI carian terbina dalam | `default` / `false` |
| `site.search.uiTheme` | Tema UI carian | `light` / `dark` / `auto` |
| `site.search.placeholderText` | Teks placeholder kotak carian | `"Cari..."` |
| `site.related.enabled` | Dayakan cadangan kandungan berkaitan | `true` / `false` |
| `site.related.threshold` | Ambang kerelevanan | `80` |
| `site.related.limit` | Bilangan cadangan maksimum setiap halaman | `5` |
| `site.menus` | Takrif pelbagai menu | Lihat [19-Ciri Baharu](./19-new-features-v3.ms.md) |
| `site.pagination.pageSize` | Saiz penomboran global | `10` |

📖 Penggunaan terperinci: [19-Ciri Baharu v3.0](./19-new-features-v3.ms.md).

### site: SEO dan Google Analytics (Pilihan)

Semasa binaan, Bukit akan mengira model `page.seo` yang seragam untuk setiap halaman. Tema boleh merender canonical, description, robots, OG, Twitter, hreflang dan JSON-LD secara langsung.

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    defaultImage: /assets/og-default.png
    twitterSite: "@your_account"
    organization:
      name: Example Inc
      url: https://example.com/about
      logo: https://example.com/logo.png
  analytics:
    google_analytics_id: G-XXXXXXXXXX
```

Penerangan medan:

| Medan | Nilai Lalai | Penerangan |
|---|---:|---|
| `site.seo.enabled` | `true` | Sama ada menjana model `page.seo`; selepas ditetapkan kepada `false`, SEO partial baharu tidak akan mengoutput tag SEO |
| `site.seo.defaultImage` | kosong | Imej perkongsian lalai yang digunakan apabila halaman tiada `og_image/cover/image` |
| `site.seo.twitterSite` | kosong | Mengoutput `twitter:site`, contohnya `@your_account` |
| `site.seo.organization.name/url/logo` | kosong | Untuk Organization JSON-LD |
| `site.analytics.enabled` | `true` | Sama ada membenarkan output kod analitik |
| `site.analytics.google_analytics_id` | kosong | GA4 Measurement ID, contohnya `G-XXXXXXXXXX` |

Analytics hanya menyokong GA4 `gtag`. Selagi `site.analytics.google_analytics_id` telah dikonfigurasi dan `enabled: false` tidak ditetapkan, starter partial versi baharu akan mengoutput kod Google Analytics.

Jika mahu mematikan kod analitik:

```yaml
site:
  analytics:
    enabled: false
    google_analytics_id: G-XXXXXXXXXX
```

Perhatian: enjin hanya bertanggungjawab mengira `page.seo` dan `site.analytics`; ia tidak akan memaksa penulisan semula HTML. Tema perlu memasukkan SEO/Analytics partial secara eksplisit dalam `<head>`. Lihat: [08-Tema dan Templat](./08-themes-templates.ms.md).

### site: Ringkasan Automatik (Pilihan)

Apabila artikel tidak menyediakan `summary`, anda boleh mendayakan “ringkasan automatik” untuk mengekstrak sepotong teks biasa daripada kandungan badan sebagai ringkasan dan menulisnya ke `meta.summary`, supaya taxonomy/RSS/search.json/templat yang membaca `summary` semuanya boleh mendapat nilai.

```yaml
site:
  autoSummary: true
  autoSummaryMaxLength: 200
```

### site: Struktur URL Tersuai (Permalinks)

Disyorkan untuk mengutamakan `site.collections`; `site.permalinks` terutamanya untuk keserasian.

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

Secara lalai, URL artikel jenis `post` ialah `/blog/<slug>/`, manakala jenis `page` ialah `/pages/<slug>/`. Jika anda mahu menyesuaikan struktur URL (contohnya termasuk tarikh), anda boleh menggunakan `site.permalinks`:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
```

Kesan: artikel `my-post` yang diterbitkan pada 2025-03-15 akan mempunyai URL `/2025/03/my-post/`.

Placeholder yang tersedia:

| Placeholder | Penerangan | Nilai Contoh |
|---|---|---|
| `{slug}` | Slug artikel | `my-post` |
| `{year}` | Tahun terbitan (4 digit) | `2025` |
| `{month}` | Bulan terbitan (2 digit) | `03` |
| `{day}` | Tarikh terbitan (2 digit) | `15` |
| `{type}` | Jenis kandungan | `post` |

Anda boleh mengkonfigurasi corak berbeza untuk beberapa jenis pada masa yang sama:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

Perhatian: jika sesebuah artikel menetapkan `route.url` dan `route.template` pilihan, tindihan routing mempunyai keutamaan lebih tinggi daripada permalinks. `outputPath` sentiasa diterbitkan daripada URL akhir; `outputPath` aras atas dan `route.outputPath` ditolak dalam Bukit 1.0.

### content: Sumber Kandungan (Markdown / Notion / Pelbagai Sumber)

Bukit 1.0 menggunakan `content.sources[]` untuk projek satu sumber dan pelbagai sumber.

#### Markdown source

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
```

| Medan | Fungsi | Penerangan |
|---|---|---|
| `content.sources[].markdown.dir` | Direktori akar Markdown | Membaca `*.md` secara rekursif |
| `content.sources[].markdown.defaultType` | Jenis lalai apabila `type` tidak diisytiharkan | Lazimnya `page` |
| `content.sources[].markdown.maxItems` | Bilangan maksimum artikel untuk dibaca | Integer positif; digunakan sebagai had untuk repo besar |
| `content.sources[].markdown.includePaths` | Hanya membaca laluan tertentu | Relatif kepada `content.sources[].markdown.dir`; `.md` boleh ditinggalkan |
| `content.sources[].markdown.includeGlobs` | Hanya membaca glob yang sepadan | Memadankan laluan relatif, pemisah menggunakan `/` |

Cara menulis kandungan Markdown: [05-Kandungan Markdown](./05-markdown-content.md).

#### Notion source

```yaml
content:
  sources:
    - type: notion
      name: pages
      mode: content
      collection: page
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

| Medan | Fungsi | Penerangan |
|---|---|---|
| `content.sources[].notion.maxItems` | Bilangan maksimum item untuk ditarik | Integer positif; digunakan sebagai had untuk pangkalan data besar |
| `content.sources[].notion.includeSlugs` | Hanya menarik slug tertentu | Penapisan query pangkalan data (memudahkan debug satu artikel) |
| `content.sources[].notion.includeSlugProperty` | Medan yang sepadan dengan `includeSlugs` | Lalai `Slug`; disyorkan rich_text |
| `content.sources[].notion.cacheMode` | Mod cache render Notion | `off`/`readwrite`/`readonly` |
| `content.sources[].notion.cacheDir` | Direktori cache | Relatif kepada direktori tempat config berada; jika kosong, lalai ialah `<rootDir>/.cache/notion` |
| `content.sources[].notion.renderConcurrency` | Konkurensi render kandungan badan | Integer positif; lalai setempat 4, CI 2 |
| `content.sources[].notion.maxRps` | Had kadar global permintaan Notion | Integer positif; lalai 3 (termasuk query pangkalan data + blocks children) |
| `content.sources[].notion.maxRetries` | Bilangan maksimum cuba semula untuk 429 | Integer bukan negatif; mematuhi backoff `Retry-After` |

Prasyarat mod Notion:

- Mesti menetapkan pembolehubah persekitaran `NOTION_TOKEN` (dilarang sama sekali menulisnya ke fail repo)

Lihat butiran: [06-Kandungan Notion](./06-notion-content.ms.md).

#### Gabungan pelbagai sumber, menyokong `mode: data`

```yaml
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Perkara penting:

- Sumber dengan `mode: content` akan menjana route dan halaman
- Sumber dengan `mode: data` tidak akan menjana route, sebaliknya disuntik ke `site.modules` (lihat: [09-Modules-Data Berstruktur](./09-modules-data.ms.md))
- Apabila source `mode: data` dikonfigurasi sebagai `name: categories` (atau `name: tags`), ia akan digunakan untuk taxonomy: walaupun kategori/tag tertentu belum dirujuk oleh mana-mana artikel pada masa ini, halaman agregasi kosong yang sepadan tetap akan dijana untuk mengelakkan 404 selepas diklik.

### build: Direktori Output dan Strategi Binaan

| Medan | Fungsi | Contoh Lazim |
|---|---|---|
| `build.output` | Direktori output | `dist` |
| `build.clean` | Sama ada membersihkan direktori output sebelum binaan | `true` (memerlukan fail `.bukit-output-marker`; menolak membersihkan direktori bukan Bukit) |
| `build.draft` | Sama ada merender kandungan draf | `false` (lalai) |
| `build.listPageContentMode` | Strategi pemasangan `pages[*].content` dalam halaman senarai | `auto` |
| `build.schemaFailMode` | Tingkah laku apabila pengesahan Schema gagal | `warn` / `strict` |
| `build.assetHashMode` | Mod perbandingan salinan aset | `"sha256"` (gunakan hash kandungan SHA256) atau lalai (saiz + masa) |

Parameter CLI setara:

- `--output <dir>` menindih `build.output`
- `--clean/--no-clean` menindih `build.clean`
- `--draft` menindih `build.draft`

`build.listPageContentMode` hanya mempengaruhi 3 halaman senarai tetap:

- Laman utama `/`
- Senarai blog `/blog/`
- Senarai halaman `/pages/`

Ia tidak mempengaruhi `page.content` halaman butiran; ia hanya mengawal sama ada `pages[*].content` dalam halaman senarai membawa kandungan badan lebih awal:

- `auto`: hanya membawa kandungan badan apabila tema telah menyatakan secara eksplisit bahawa ia diperlukan; jika belum dinyatakan, gunakan logik keserasian
- `always`: sentiasa membawa kandungan badan
- `never`: tidak membawa kandungan badan; `pages[*].content` ialah rentetan kosong

Corak yang disyorkan:

```yaml
build:
  output: dist
  listPageContentMode: auto
```

### theme: Lokasi Tema dan Parameter

Corak paling disyorkan ialah hanya menentukan `theme.name`, dengan direktori tema diletakkan di `themes/<name>/`:

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

Jika anda tidak menggunakan direktori `themes`, anda juga boleh menentukan setiap direktori secara eksplisit:

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

Medan lengkap yang disokong oleh `theme`:

| Medan | Jenis | Contoh | Penerangan |
|---|---|---|---|
| `name` | rentetan | `alt` | Nama tema (sepadan dengan `themes/<name>/`) |
| `source` | rentetan | `https://github.com/user/theme.git@v1.0.0` | URL Git tema jauh dengan tag versi pilihan. Disimpan dalam cache setempat; binaan seterusnya TIDAK auto-pull (boleh dihasilkan semula). `bukit-theme.lock.json` merekodkan commit yang diselesaikan. |
| `params` | peta | `{brand: my-site}` | Parameter tersuai yang dihantar kepada tema |
| `layouts` | rentetan | `layouts` | Direktori templat layout tersuai |
| `assets` | rentetan | `assets` | Direktori aset tersuai (SCSS/JS/imej) |
| `static` | rentetan | `static` | Direktori fail statik tersuai (disalin apa adanya) |
| `shortcodes` | peta | `shortcode_name: template_string` | Serpihan HTML boleh guna semula (Markdown `{% %}` atau Scriban `{{ shortcode }}`) |
| `components` | peta | `name: {template, props}` | Komponen templat dengan props (Scriban `{{ comp.render }}`) |
| `scss` | objek | `{enabled, entryPoint, outputDir}` | Kompilasi automatik SCSS → CSS (memerlukan `sass` dipasang pada sistem) |
| `images` | objek | `{enabled, formats, sizes, quality}` | Pengoptimuman imej automatik dan penukaran WebP/AVIF (memerlukan `cwebp`/`magick`) |
| `extends` | rentetan | Nama tema induk | Pewarisan tema (tema anak mencantumkan templat, fail statik dan aset tema induk secara berperingkat) |

Pembolehubah tema dan templat: [08-Tema dan Templat](./08-themes-templates.ms.md).

### logging: Tahap Log (biasanya tidak perlu diubah dengan kerap)

```yaml
logging:
  level: info
```

Dalam senario CI, disyorkan untuk digabungkan dengan `--log-format json` supaya pengumpulan dan penyelesaian masalah lebih mudah (lihat: [12-Rujukan Baris Perintah](./12-cli-reference.ms.md)).

### deploy: Konfigurasi Deployment (Pilihan)

Mengawal tingkah laku deployment perintah `bukit deploy`:

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "bukit deploy"
  cname: example.com
```

| Medan | Penerangan | Nilai Lalai |
|------|------|--------|
| `deploy.provider` | Platform sasaran deployment (buat masa ini hanya `github-pages`) | — |
| `deploy.branch` | Cabang Git sasaran | `gh-pages` |
| `deploy.message` | Mesej commit Git | `bukit deploy` |
| `deploy.cname` | Domain tersuai (akan ditulis ke fail CNAME) | — |

Tindihan CLI:
- `--branch <name>` menindih `deploy.branch`
- `--message <text>` menindih `deploy.message`
- `--dry-run` hanya pratonton, tidak benar-benar push
- `--skip-build` melangkau binaan dan terus deploy `dist/` sedia ada

Lihat butiran: [13-Deploy ke GitHub Pages](./13-deploy-github-pages.ms.md) dan [bukit-deploy skill](../../src/skills/bukit-deploy/SKILL.md).

### collections: Pengesahan Medan Front Matter Kandungan (Schema)

Melalui `site.collections`, anda boleh mentakrifkan peraturan pengesahan medan Front Matter untuk setiap jenis kandungan:

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      schema:
        - name: title
          type: string
          label: Tajuk artikel
          required: true
        - name: publishAt
          type: date
          label: Masa terbit
          required: true
        - name: tags
          type: list
          label: Teg
          default: []
        - name: featured
          type: bool
          label: Artikel pilihan
          default: false
        - name: priority
          type: number
          label: Keutamaan
          default: 0
```

Penerangan medan schema:

| Medan | Jenis | Penerangan |
|---|---|---|
| `schema` | tatasusunan | `[{name, type, label, required, default}]` | Pengesahan jenis medan Front Matter kandungan (`string/number/bool/date/list`) |

Apabila pengesahan gagal, tingkah laku dikawal oleh `build.schemaFailMode`:
- `warn`: output amaran tetapi teruskan binaan
- `strict`: hentikan binaan serta-merta apabila pengesahan gagal

## Senario Konfigurasi Lazim (Boleh Disalin Terus)

### 1) Sub-laluan GitHub Pages (baseUrl)

Jika tapak dideploy ke `https://user.github.io/my-repo/`, maka:

- `site.baseUrl` sepatutnya `/my-repo`
- `site.url` sepatutnya `https://user.github.io/my-repo`

Contoh perintah binaan:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### 2) Konfigurasi Minimum Berbilang Bahasa

```yaml
site:
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

Bandingkan dengan contoh: `examples/starter/site.i18n.yaml`.

### 3) Konfigurasi Minimum Modules (data)

```yaml
content:
  sources:
    - name: content
      mode: content
      collection: page
      markdown: { dir: content }
    - name: modules
      mode: data
      markdown: { dir: data, defaultType: module }
```

Bandingkan dengan contoh: `examples/starter/site.modules.yaml` dan `examples/starter/data/*.md`.

## Perangkap Lazim (Semakan Kendiri Pantas)

- `site.url` tidak ditetapkan: pautan sitemap/rss mungkin tidak betul (boleh ditindih dengan `--site-url`)
- `site.baseUrl` salah dikonfigurasi: aset 404 selepas GitHub Pages dibuka (laluan CSS/JS/imej salah)
- Asas laluan relatif salah difahami: `dir: content` bukan relatif kepada direktori kerja baris perintah, tetapi relatif kepada direktori tempat `site.yaml` berada
- Token Notion ditulis ke YAML: tidak dibenarkan dan tidak selamat; mesti menggunakan pembolehubah persekitaran `NOTION_TOKEN`
