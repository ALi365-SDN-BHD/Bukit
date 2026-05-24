# 10 Ciri Terbina Dalam & Output: sitemap/rss/search, Teg, Kategori & Halaman Terbitan

Selain menjana HTML halaman, Bukit juga menjana satu set “artifak peringkat tapak” berdasarkan kandungan dan konfigurasi, untuk SEO, langganan, carian, dan agregasi kandungan.

Halaman ini memberi tumpuan kepada “apa yang pengguna boleh kawal, dan fail apa yang akan dijana”; jika anda memerlukan kontrak plugin dan sempadan yang lebih terperinci, lihat dokumentasi pembangun: [guide/dev/built-in-plugins](../dev/built-in-plugins.ms.md).

## Apa yang Anda Akan Dapat

- Fail tambahan apa yang akan dijana, dan di mana lokasinya
- Bagaimana fail-fail ini dioutputkan semasa pelbagai bahasa (split/merged/index)
- Apakah “halaman terbitan” seperti teg/kategori/arkib/penomboran
- Soalan lazim: mengapa pautan dalam sitemap tidak betul, mengapa search.json kosong

## Senarai Artifak Peringkat Tapak (Lazim)

Dalam direktori output binaan (`build.output`, lalai `dist/`), anda biasanya akan melihat:

- `sitemap.xml`
- `rss.xml`
- `search.json` (data carian untuk pelayar)
- `search.index.json` (pilihan: indeks agregat)
- `tags/`, `categories/` (halaman senarai terbitan, bergantung pada tema dan logik terbitan)

Contoh boleh jalan untuk dibandingkan:

- `examples/starter/dist/`
- `examples/starter/.bukit_test/dist/` (output lengkap untuk ujian)

## sitemap.xml: Pintu Masuk Pengindeksan Enjin Carian

### Apa yang Boleh Anda Konfigurasikan

- `site.url`: domain mutlak tapak (asas untuk menjana pautan mutlak)
- `site.baseUrl`: sublaluan (lazim untuk GitHub Pages)
- `site.sitemapMode`: mod output pelbagai bahasa (lihat bahagian seterusnya)
- `site.sitemapDetail.defaultPriority`: nilai `<priority>` lalai (0.0-1.0, v3.0+)
- `site.sitemapDetail.defaultChangefreq`: nilai `<changefreq>` lalai (v3.0+)
- `site.sitemapDetail.imageEnabled`: sama ada untuk mengaktifkan sambungan Sitemap imej (v3.0+)
- `site.sitemapDetail.videoEnabled`: sama ada untuk mengaktifkan sambungan Sitemap video (v3.0+)

### Penggantian Per Halaman (v3.0+)

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
  images:
    - url: "/images/hero.jpg"
      caption: "Imej utama"
---
```

### Perangkap Lazim

- `site.url` tidak ditetapkan: sitemap mungkin menjana pautan relatif atau pautan mutlak yang salah
- baseUrl tersalah konfigurasi: URL dalam sitemap membawa awalan yang salah, menyebabkan enjin carian gagal merangkak

Untuk butiran berkaitan deployment, lihat: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md).

## rss.xml → Feed Berbilang Format (naik taraf v3.0)

Sebelum ini hanya `rss.xml` dijana. Bermula v3.0, RSS 2.0 + Atom 1.0 + JSON Feed 1.1 boleh dijana serentak.

Cara konfigurasi (baharu dalam v3.0):

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]
    limit: 20
    path: feed
```

Fail yang dijana:
- `rss.xml` (RSS 2.0, format sedia ada)
- `feed/atom.xml` (Atom 1.0, baharu)
- `feed/feed.json` (JSON Feed 1.1, baharu)

⚠️ Key suis plugin berubah daripada `rss` kepada `feed`:
```yaml
site:
  plugins:
    feed:
      enabled: false   # Nyahdayakan semua penjanaan feed
```

> Feed bebas per collection: lihat `collection.output.feedPath`.

Sumber langganan biasanya bergantung pada:
- URL tapak (`site.url`)
- Tajuk kandungan/masa terbit/type (terutamanya post)

Jika anda mendapati kandungan feed tidak lengkap, semak dahulu:
- Sama ada kandungan anda mempunyai `publishAt`
- Sama ada ia dikecualikan oleh draf/syarat penapisan (Notion Published, build.draft, dan sebagainya)

## search.json: Data Carian Dalam Tapak

search.json biasanya ialah senarai “tajuk/ringkasan/URL bagi setiap halaman” untuk JS hujung depan melaksanakan carian.

### Berat Carian dan Pengecualian (v3.0+)

Kawal tingkah laku carian dalam front matter:

```yaml
---
searchWeight: 5        # Lebih tinggi berat, lebih awal kedudukan (lalai 1)
searchExclude: true    # Jangan masukkan ke indeks carian
---
```

### UI Carian Terbina Dalam (v3.0+)

```yaml
site:
  search:
    ui: "default"      # Aktifkan UI carian terbina dalam (false untuk tutup)
    uiTheme: "dark"    # light / dark / auto
    placeholderText: "Cari..."
```

Menjana `bukit-search.html`, yang boleh disertakan dalam templat:

```html
{{ include "bukit-search.html" }}
```

UI carian merangkumi kotak input, padanan kata kunci, navigasi papan kekunci, dan hasil yang diserlahkan, tanpa memerlukan pustaka JS tambahan.

Biasanya anda perlu:
- Melaksanakan UI carian dalam tema (membaca search.json dan menapis)
- Atau terus menggunakan `bukit-search.html` terbina dalam

Jika search.json kosong:
- Tapak mungkin tidak mempunyai item kandungan (pembacaan content gagal/ditapis keluar)
- Atau tema/konfigurasi belum mengaktifkan output yang sepadan (bergantung pada versi dan mod)

## Teg dan Kategori (tags / categories)

Apabila kandungan anda mengandungi `tags` atau `categories`:

- Enjin/plugin akan mengagregatkan maklumat ini
- Tema biasanya merender halaman senarai dan halaman butiran untuk tags/categories

Pilihan: aktifkan susunan pin untuk kandungan di bawah kategori/teg tertentu:

- Tandakan `pinned: true` dalam kandungan (pilihan `pinOrder` nombor; semakin kecil nombor, semakin awal kedudukan)
- Item konfigurasi: `taxonomy.pinField` / `taxonomy.pinOrderField` (untuk berbilang sumber data, anda boleh menggunakan `pinFieldBySource` / `pinOrderFieldBySource` untuk pemetaan nama medan)

### Metadata term (v3.0.0+)

Anda boleh menetapkan maklumat tambahan untuk setiap tag/category, dengan dua cara pilihan:

**Cara 1: fail data** (`content/data/tags.yaml`):
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10          # Berat isihan, lebih besar lebih awal
  parent: tech        # Kategori induk (hierarki)
```

**Cara 2: konvensyen direktori** (`content/_taxonomy/tags/ml/_index.md`), gaya Hugo:
```yaml
---
description: Everything about ML and AI
image: /assets/images/ml-cover.png
---
```

### Kategori Berhierarki

Aktifkan melalui `taxonomy.kinds[].hierarchical: true`. Term membina hubungan induk-anak melalui medan `parent`, dan mengira `children` serta `ancestors` secara automatik (navigasi serbuk roti).

### RSS feeds

Setiap term menjana feed RSS 2.0 bebas secara automatik: `/tags/python/feed.xml`, dan boleh dilanggan secara berasingan.

### Alihan Alias

Term boleh dikonfigurasikan dengan alias (medan `aliases`), lalu menjana halaman alihan secara automatik untuk memastikan URL lama tidak menjadi 404.

Contoh Markdown (tags/categories), lihat: [05 Kandungan Markdown](./05-markdown-content.md).

## Halaman Terbitan: Apakah tags/categories/penomboran/arkib

Halaman terbitan (derived pages) bukan halaman yang anda tulis secara langsung dalam sumber kandungan, tetapi halaman yang “diterbitkan” oleh enjin berdasarkan kandungan, contohnya:

- `/tags/<tag>/`: senarai artikel di bawah teg tertentu
- `/categories/<category>/`: senarai artikel di bawah kategori tertentu
- `/blog/page/2/`: halaman senarai selepas penomboran
- `/archive/2026/`: arkib mengikut tahun

Perkara yang perlu diberi perhatian oleh pengguna:

- Sama ada halaman terbitan dirender bergantung pada: sama ada enjin mengaktifkan keupayaan terbitan yang sepadan + sama ada tema menyediakan templat yang sepadan
- Halaman terbitan akan menyertai sitemap/search (oleh itu ketepatan baseUrl dan url menjadi lebih penting)

## pluginFailMode: Sama ada Binaan Patut Dihentikan Apabila Terbitan/Output Gagal

```yaml
site:
  pluginFailMode: strict  # strict (lalai) | warn
```

- `strict`: ralat plugin akan menghentikan binaan (sesuai untuk produksi)
- `warn`: log ralat tetapi teruskan output (sesuai untuk tempoh migrasi/penyahpepijatan)

## Mod Output Pelbagai Bahasa (sitemap/rss/search)

Di bawah tapak pelbagai bahasa, artifak ini mempunyai tiga mod lazim (makna adalah konsisten bagi artifak sejenis):

- `split`: satu salinan untuk setiap bahasa (contohnya `zh-CN/sitemap.xml` dan `en-US/sitemap.xml`)
- `merged`: diagregatkan menjadi satu salinan (biasanya satu salinan dioutputkan di direktori akar)
- `index`: direktori akar mengoutputkan fail indeks yang menunjuk ke fail setiap bahasa

Cara memilih, lihat: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md).

## Pengoptimuman Imej Automatik (WebP / AVIF)

Semasa binaan, imej PNG/JPG dalam direktori `assets/` akan ditukar secara automatik kepada format WebP/AVIF.

**Kebergantungan**: perlu memasang `cwebp` (libwebp) atau `magick` (ImageMagick):

```bash
# macOS
brew install webp imagemagick
# Linux
sudo apt install webp imagemagick
```

**Konfigurasi**:

```yaml
theme:
  images:
    enabled: true
    formats: [webp]          # avif juga disokong
    sizes: [480, 768, 1200]  # saiz responsif untuk srcset
    quality: 85
```

Jika alat penukaran tidak dipasang, proses binaan akan melangkau pengoptimuman imej dan mengoutputkan amaran, tanpa melaporkan ralat.

## Kompilasi SCSS Automatik

Semasa binaan, fail `.scss` dalam direktori `assets/` akan dikompilasi secara automatik menjadi `.css`.

**Kebergantungan**: perlu memasang CLI `sass` atau `dart-sass`:

```bash
npm install -g sass
```

**Konfigurasi**:

```yaml
theme:
  scss:
    enabled: true
```

Selepas kompilasi berjaya, fail `.scss` asal akan dipadam secara automatik. Jika CLI tidak dipasang, kompilasi akan dilangkau dan amaran akan dioutputkan.

## Cadangan Kandungan Berkaitan (v3.0+)

Padankan kandungan berkaitan secara automatik berdasarkan pelbagai dimensi seperti teg/kategori/kata kunci.

```yaml
site:
  related:
    enabled: true
    threshold: 80
    limit: 5
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
```

## Sistem Menu (v3.0+)

Navigasi berbilang menu, menyokong submenu bersarang.

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
```

## Fail Data (v3.0+)

Letakkan fail YAML/JSON/TOML dalam direktori `data/`; ia akan dimuatkan secara automatik ke dalam templat semasa binaan.

```
data/
  authors.yaml
  navigation.json
```

## Alihan Alias URL (v3.0+)

Isytiharkan URL lama dalam front matter untuk menjana halaman alihan HTML secara automatik:

```yaml
---
aliases:
  - /old-url/
  - /previous-permalink/
---
```

## Pemprosesan Imej Berbilang Saiz (v3.0+)

Jana varian berbilang saiz secara automatik untuk imej di bawah `assets/` (bergantung pada ImageMagick).

```yaml
theme:
  images:
    enabled: true
    sizes: [480, 768, 1200]
    quality: 80
```

📖 Untuk penggunaan terperinci dan konfigurasi lengkap, lihat: [19 Ciri Baharu v3.0](./19-new-features-v3.ms.md).
