# 19 Ciri Baharu v3.0: Feed Pelbagai Format, Sitemap Dipertingkat, UI Carian, Refaktor Taxonomy, Kandungan Berkaitan, Menu, Fail Data, Alias, Pemprosesan Imej

Bukit v3.0 menambah 5 plugin baharu di atas 9 plugin terbina dalam sedia ada, dan menaik taraf 6 plugin sedia ada secara besar-besaran. Halaman ini merumuskan semua perubahan.

## Sekilas Pandang

| Ciri | Status | Lokasi Konfigurasi | Output |
|------|------|---------|------|
| Feed pelbagai format (RSS + Atom + JSON) | 🆕 Naik taraf | `site.feed` | `rss.xml` / `feed/atom.xml` / `feed/feed.json` |
| Sitemap priority / changefreq | 🆕 Naik taraf | `site.sitemapDetail` + front matter | `sitemap.xml` (mengandungi `<priority>` `<changefreq>`) |
| Sambungan imej/video Sitemap | 🆕 Naik taraf | `site.sitemapDetail` + front matter | `sitemap.xml` (mengandungi `<image:image>` `<video:video>`) |
| UI carian | 🆕 Naik taraf | `site.search` | `search.json` + `bukit-search.html` |
| searchWeight / searchExclude | 🆕 Naik taraf | front matter | `search.json` (mengandungi medan `weight`) |
| Penomboran pelbagai collection + urlPattern | 🆕 Naik taraf | `collection.pagination` | Halaman bernombor |
| Arkib kedalaman daily + templat tersuai | 🆕 Naik taraf | `collection.output.archiveDetail` | Halaman arkib |
| **Taxonomy v3.0.0 dinaik taraf sepenuhnya** | 🆕 Refaktor | `taxonomy.kinds` + `_index.md` | Kategori hierarki / RSS term / redirect |
| Cadangan kandungan berkaitan | 🆕 Baharu | `site.related` | Suntikan data `__related_pages` |
| Sistem menu | 🆕 Baharu | `site.menus` | `menus.json` + suntikan data |
| Fail data | 🆕 Baharu | Direktori `data/` | Suntikan data `__data_files` |
| Alias/alihan URL | 🆕 Baharu | front matter `aliases` | Halaman HTML redirect |
| Pemprosesan imej pelbagai saiz | 🆕 Baharu | `theme.images` | Varian pelbagai saiz + srcset |

---

## Satu, Feed Pelbagai Format (RSS + Atom + JSON Feed)

Sebelum ini hanya RSS 2.0 disokong. Kini tiga format boleh dijana serentak.

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]   # Lalai hanya rss
    limit: 20                           # Bilangan item maksimum setiap feed
    path: feed                          # Awalan laluan output
```

**Feed bebas untuk setiap collection:**

```yaml
collections:
  post:
    output:
      rss: true
      feedPath: blog-feed          # Direktori bebas, seperti /blog-feed/atom.xml
      feedTitle: "Artikel Blog Saya"
      feedDescription: "Kemas kini blog terkini"
```

**Kecualikan halaman tertentu / lampiran podcast:**

```yaml
---
feed:
  exclude: true                    # Tidak dimasukkan ke dalam feed
  enclosure:                       # Lampiran podcast
    url: "https://example.com/ep1.mp3"
    length: 12345678
    type: "audio/mpeg"
---
```

> ⚠️ Key suis plugin berubah daripada `rss` kepada `feed`: `site.plugins.feed.enabled: false`

---

## Dua, Sitemap Dipertingkat

### priority / changefreq

```yaml
site:
  sitemapDetail:
    defaultPriority: 0.5
    defaultChangefreq: "weekly"
```

**Timpa mengikut halaman:**

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
---
```

### Sambungan Imej Sitemap

```yaml
site:
  sitemapDetail:
    imageEnabled: true
```

Isytiharkan dalam front matter:
```yaml
---
sitemap:
  images:
    - url: "/images/hero.jpg"
      caption: "Imej utama"
      title: "Hero"
---
```

### Sambungan Video Sitemap

```yaml
site:
  sitemapDetail:
    videoEnabled: true
```

```yaml
---
sitemap:
  videos:
    - url: "https://youtube.com/watch?v=xxx"
      title: "Video tutorial"
      thumbnail: "/images/thumb.jpg"
---
```

---

## Tiga, Penambahbaikan Carian

### Pemberat Carian dan Pengecualian

```yaml
---
searchWeight: 5        # Semakin tinggi pemberat, semakin ke hadapan isihan (lalai 1)
searchExclude: true    # Tidak dimasukkan ke dalam indeks carian
---
```

### UI Carian Terbina Dalam

```yaml
site:
  search:
    ui: "default"      # Dayakan UI carian terbina dalam (false untuk tutup)
    uiTheme: "dark"    # light / dark / auto
    placeholderText: "Cari artikel..."
```

`bukit-search.html` yang dijana boleh disertakan melalui templat:

```html
{{ include "bukit-search.html" }}
```

Ciri UI carian:
- JS tulen ~5KB, sifar kebergantungan
- Cari sambil menaip, padanan berwajaran tajuk + kandungan
- Menyokong pemberat `searchWeight`
- Navigasi papan kekunci (↑ ↓ Enter Escape)
- Sorotan hasil carian
- Pertukaran tema terang/gelap

---

## Empat, Cadangan Kandungan Berkaitan

Memadankan kandungan berkaitan secara automatik berdasarkan pelbagai dimensi seperti tag/kategori/kata kunci.

```yaml
site:
  related:
    enabled: true
    threshold: 80      # Skor minimum
    limit: 5           # Maksimum 5 item setiap halaman
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
      - name: keywords
        weight: 40
```

Dimensi padanan yang disokong: `tags`, `categories`, `keywords`, `collection` (bonus untuk jenis yang sama), `date` (bonus dalam 90 hari).

**Penggunaan dalam templat:**

Data boleh diakses melalui `context.Data["__related_pages"]`, diindeks mengikut ID kandungan; setiap item mengandungi `{title, url, score}`.

---

## Lima, Sistem Menu

```yaml
site:
  menus:
    main:
      - identifier: home
        name: Laman Utama
        url: /
        weight: 1
      - identifier: blog
        name: Blog
        url: /blog/
        weight: 2
        children:
          - identifier: tech
            name: Teknologi
            url: /blog/tags/tech/
            weight: 1
    footer:
      - identifier: about
        name: Tentang
        url: /about/
        weight: 1
```

**Render dalam templat:**

```html
<nav>
  <ul>
    {{ for item in site.menus.main }}
      <li>
        <a href="{{ item.url }}">{{ item.name }}</a>
        {{ if item.children }}
          <ul>
            {{ for child in item.children }}
              <li><a href="{{ child.url }}">{{ child.name }}</a></li>
            {{ end }}
          </ul>
        {{ end }}
      </li>
    {{ end }}
  </ul>
</nav>
```

Pada masa yang sama, fail `menus.json` akan dioutputkan.

---

## Enam, Fail Data (Direktori data/)

Cipta folder `data/` dalam direktori akar projek dan letakkan fail YAML/JSON/TOML:

```
data/
  authors.yaml
  navigation.json
  zh-CN/
    strings.yaml
  en/
    strings.yaml
```

Data dimuatkan secara automatik ke `context.Data["__data_files"]`.

**Sokongan pelbagai bahasa**: data dalam subdirektori `data/{lang}/` dimuatkan mengikut bahasa; fail peringkat akar yang dikongsi tersedia untuk semua bahasa.

---

## Tujuh, Alias URL (Alihan)

Isytiharkan alias dalam front matter untuk menjana halaman alihan HTML secara automatik:

```yaml
---
title: "Artikel Baharu"
aliases:
  - /old-permalink/
  - /another-old-url/
---
```

HTML yang dijana mengandungi:

```html
<meta http-equiv="refresh" content="0; url=/new-url/">
<link rel="canonical" href="/new-url/">
```

Halaman alias ditandakan sebagai `type: redirect` dan dikecualikan secara automatik daripada sitemap.

---

## Lapan, Pemprosesan Imej Pelbagai Saiz

Menjana varian pelbagai saiz secara automatik untuk imej JPG/PNG di bawah `assets/`:

```yaml
theme:
  images:
    enabled: true
    formats: ["webp", "avif"]
    sizes: [480, 768, 1200]
    quality: 80
```

Fail varian yang dijana (seperti `hero-480w.jpg`, `hero-768w.jpg`) dan data srcset disuntik ke dalam `__image_srcsets`.

**Kebergantungan**: perlu memasang ImageMagick (arahan `magick` atau `convert`). Jika tidak dipasang, ia akan dilangkau dan amaran akan dioutputkan.

---

## Sembilan, Penambahbaikan Penomboran

### Penomboran Bebas untuk Pelbagai Collection

```yaml
collections:
  post:
    pagination:
      enabled: true
      pageSize: 10
      urlPattern: "p/:num/"           # Pilihan: corak URL tersuai
      firstPageUsesListRoute: true    # Halaman pertama menggunakan listRoute
  docs:
    pagination:
      enabled: true
      pageSize: 20
```

### Nilai Lalai Penomboran Global

```yaml
site:
  pagination:
    pageSize: 10
```

---

## Sepuluh, Penambahbaikan Arkib

```yaml
collections:
  post:
    output:
      archive:
        enabled: true
        depth: "daily"              # yearly | monthly | daily
        template: "pages/archive.html"
        routePrefix: "archives"     # Awalan URL tersuai
```

---

## Sebelas, Taxonomy v3.0.0 Dinaik Taraf Sepenuhnya

Sistem Taxonomy telah direfaktor sepenuhnya dari segi seni bina hingga fungsi; TaxonomyPlugin dipecahkan daripada 1194 baris kepada 7 modul tanggungjawab, serta menambah 7 ciri baharu.

### Kategori Hierarki

Dayakan melalui `taxonomy.kinds[].hierarchical: true`. Term membina hubungan induk-anak melalui medan `parent`, dan mengira `children` serta `ancestors` (navigasi serbuk roti) secara automatik.

```yaml
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true
```

**Akses dalam templat:**

```html
{{ if taxonomy.ancestors }}
  <nav class="breadcrumb">
  {{ for ancestor in taxonomy.ancestors }}
    <a href="{{ site.base_url }}/{{ taxonomy.kind }}/{{ ancestor }}/">{{ ancestor }}</a>
  {{ end }}
  </nav>
{{ end }}
```

### Metadata Term (Konvensyen `_index.md`)

Mengikut gaya Hugo, metadata term ditakrifkan melalui YAML front matter dalam `content/_taxonomy/<kind>/<slug>/_index.md`:

```yaml
---
title: "Pembelajaran Mesin"
description: "Artikel tentang algoritma, rangka kerja, dan amalan pembelajaran mesin"
image: "/images/ml-cover.jpg"
weight: 10
parent: "ai"
aliases:
  - machine-learning
  - ml
---
```

Medan yang disokong: `title`, `description`, `image`, `weight`, `parent`, `aliases`.

### Feed RSS Term

Setiap term yang mempunyai artikel menjana feed RSS 2.0 secara automatik:

| Produk | Laluan | Penerangan |
|------|------|------|
| RSS feed | `<output>/<kind>/<slug>/feed.xml` | 20 artikel terkini, dengan penemuan automatik `<atom:link>` |

### Transliterasi Slug (Transliteration)

`SlugHelper` menyokong penguraian Unicode NFD, menukar aksara Latin bertanda diakritik kepada ASCII secara automatik:

| Input | Output | Penerangan |
|------|------|------|
| `café` | `cafe` | Tanda aksen dibuang |
| `naïve` | `naive` | Tanda diaeresis dibuang |
| `über` | `uber` | Umlaut vokal dibuang |
| `Straße` | `strasse` | Ligatur `ß` → `ss` |
| `Æsop` | `aesop` | Ligatur `Æ` → `ae` |
| `kanji` | `kanji` | Aksara bukan Latin yang tidak memerlukan transliterasi boleh dikekalkan |

### Alihan Alias

Medan `Aliases` bagi term menjana halaman HTML redirect secara automatik:

```
content/_taxonomy/tags/dl/_index.md:
  aliases: [deep-learning, deep_learning]

→ jana:
  /tags/deep-learning/index.html  → redirect to /tags/dl/
  /tags/deep_learning/index.html  → redirect to /tags/dl/
```

### Isihan dan Keterlihatan Term

- `weight`: semakin besar nombor, semakin ke hadapan isihan (dalam halaman indeks)
- `isVisible: false`: term tidak menjana halaman (tetapi dikekalkan dalam data JSON)

### taxonomy.json Schema v2

Menambah medan array `children` dan `ancestors`:

```json
{
  "tags": {
    "ml": {
      "title": "Pembelajaran Mesin",
      "slug": "ml",
      "count": 15,
      "description": "...",
      "children": ["deep-learning", "nlp"],
      "ancestors": ["ai"]
    }
  }
}
```

---

## Panduan Migrasi

| Konfigurasi Lama | Konfigurasi Baharu |
|--------|--------|
| `site.plugins.rss.enabled: false` | `site.plugins.feed.enabled: false` |
| `RssPlugin` (nama kelas kod sumber) | `FeedPlugin` (nama kelas kod sumber) |
| Hanya menjana `rss.xml` | Boleh menjana RSS + Atom + JSON Feed serentak |
| Carian hanya `search.json` | + `searchWeight` / `searchExclude` + UI terbina dalam |
| `taxonomy.json` schema v1 | schema v2 (menambah array `children` / `ancestors`) |
| Term hanya mempunyai `title` + `slug` | Menambah `description`, `image`, `weight`, `parent`, `children`, `ancestors`, `aliases` |
| Tiada kategori hierarki | Dayakan dengan `taxonomy.kinds[].hierarchical: true` |
| Tiada metadata term | `content/_taxonomy/<kind>/<slug>/_index.md` (gaya Hugo) |
| Tiada RSS term | Setiap term menjana `<kind>/<slug>/feed.xml` secara automatik |

---

## Pengukuhan Teras Binaan (v3.x)

Keluaran ini juga merangkumi pelbagai penambahbaikan kebolehpercayaan dan keselamatan enjin binaan:

| Ciri | Penerangan | Impak |
|---|---|---|
| **Pengasingan persekitaran plugin** | Plugin luaran berjalan dalam persekitaran bersih dengan hanya `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, `BUKIT_OUTPUT_DIR` didedahkan. Gunakan `allowEnvironment` untuk laluan hos eksplisit. | Pembangun plugin mesti membaca pemboleh ubah ini dan bukannya bergantung pada persekitaran hos |
| **Had output plugin** | `externalPlugins.<name>.maxStdoutBytes` / `maxStderrBytes` mengehadkan output plugin. Melebihi had membunuh proses. | Mencegah plugin liar daripada menggunakan sumber |
| **Manifes output plugin + pembersihan lapuk** | Semua output plugin dikesan dengan plugin/hook/path/hash dalam `build-manifest.json`. Output lama dari binaan sebelumnya dipadam secara automatik semasa binaan tambahan. | Direktori output lebih bersih merentas binaan |
| **Mod hash aset** | `build.assetHashMode: "sha256"` membolehkan pengesanan salinan aset berasaskan kandungan SHA256 (disyorkan untuk CI dan sistem fail rangkaian). | Mencegah penyalinan semula aset yang tidak berubah |
| **Pengesahan keselamatan laluan** | Semua laluan yang dijana disahkan terhadap pencerobohan laluan (`../`), laluan mutlak, laluan merentas pemacu, dan nama terpelihara Windows. | Mencegah pelarian fail output |
| **Pengesanan konflik laluan HTML statik** | Fail `.html` dalam direktori `static/` kini termasuk dalam pengesanan konflik laluan bersama halaman kandungan dan halaman terbitan. | Mencegah konflik laluan senyap |
| **Perlindungan penanda clean** | `build.clean` kini memerlukan fail `.bukit-output-marker` sebelum membersihkan direktori output. Menolak membersihkan direktori bukan Bukit. | Mencegah pemadaman tidak sengaja |
| **Kebolehhasilan semula tema jauh** | Tema jauh yang dicache tidak lagi auto-`git pull`. Checkout `@ref` dikunci melalui `bukit-theme.lock.json`. Commit tidak sepadan menyebabkan kegagalan binaan. | Binaan konsisten merentas persekitaran |
| **Cap jari templat komposit** | Hash templat tambahan kini menggabungkan child/parent/user layouts, `theme.yaml`, dan penanda versi perender. Perubahan tema induk atau susun atur pengguna mencetuskan perenderan semula. | Kurang kejutan "templat tidak dikemas kini" |
| **Belanjawan serentak pelbagai bahasa** | Binaan pelbagai bahasa mematuhi belanjawan serentak global untuk mencegah kehabisan sumber. | Penggunaan sumber lebih boleh diramal |
