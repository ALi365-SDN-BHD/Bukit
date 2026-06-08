# 02 Konsep Teras: Apa yang Anda Konfigurasikan, Apa yang Enjin Lakukan

Halaman ini menerangkan objek teras Bukit dari "perspektif pengguna": fail apa yang anda tulis, halaman web apa yang dihasilkan oleh fail tersebut, dan medan apa yang boleh anda gunakan untuk mengawal output.

## Memahami Saluran Binaan dalam Satu Rajah

```text
site.yaml
  │
  ├─ content.sources[] (Markdown / Notion / data sources)
  │     └─ membaca kandungan → menormalkan ke dalam ContentDocument
  │
  ├─ routing (ditentukan oleh route/template eksplisit, site.collections, dan theme templates.accepts)
  │
  ├─ rendering (menghasilkan kandungan ke dalam HTML menggunakan templat)
  │
  └─ plugins (pilihan: menjana sitemap/rss/search, halaman terbitan, dsb.)
        ↓
      dist/ (direktori output fail statik)
```

Hanya tiga perkara yang perlu diingat:

1. **Dari mana kandungan datang** (`content.sources[]`)
2. **Ke mana setiap kandungan dioutputkan** (melalui route/front matter atau site.collections secara eksplisit)
3. **Templat apa yang digunakan untuk rendering** (template eksplisit, collection template/listTemplate, atau theme templates.accepts)

## Konfigurasi Tapak (site.yaml)

- `site.*`: Maklumat peringkat tapak (nama tapak, tajuk, URL, baseUrl, pelbagai bahasa, mod output SEO, dsb.)
- `content.*`: Sumber kandungan (Markdown / Notion / pelbagai sumber)
- `build.*`: Direktori output, sama ada perlu dibersihkan, sama ada perlu menghasilkan draf
- `theme.*`: Direktori tema dan parameter (templat/aset/fail statik)
- `logging.*`: Tahap log

Untuk medan terperinci, lihat: [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md).

## Kandungan (ContentDocument) = Satu keping data yang "akan dihasilkan / disuntik ke dalam templat"

Tidak kira sama ada kandungan anda berasal dari Markdown atau Notion, enjin menormalkan semuanya ke dalam "item kandungan." Yang paling penting bagi anda ialah: **medan mana yang mempengaruhi tingkah laku tapak**.

### 1) Record: Metadata yang mempengaruhi keputusan enjin (kekalkan sedikit, kekalkan stabil)

Kunci Record lazim (anda menyediakannya dalam Markdown Front Matter atau medan Notion):

- `collection`: Koleksi kandungan itu dimiliki (disyorkan), sepadan dengan kunci dalam site.collections, menentukan penghalaan dan templat
- `type`: jenis kandungan pilihan atau kunci padanan templat tema; tidak mencipta laluan terbina dalam
- `slug`: Komponen teras URL (secara amnya disyorkan untuk kekal stabil)
- `language`: Gabungan bahasa kandungan (digunakan untuk penapisan dan pautan dalam persediaan pelbagai bahasa)
- `tags` / `categories`: Teg/kategori (digunakan untuk menghasilkan halaman senarai)
- `route` / `url` / `template`: Penggunaan lanjutan untuk menentukan URL/templat secara eksplisit. `outputPath` diterbitkan daripada URL akhir dan tidak boleh dikonfigurasikan.

### 2) Fields: Medan tersuai yang dimaksudkan untuk penggunaan templat (tambah apa sahaja yang anda mahu)

Titik masuk bersatu untuk membaca medan dalam templat ialah:

```scriban
<title>
  {{ if page.fields.seo_title }}
    {{ page.fields.seo_title.value }}
  {{ else }}
    {{ page.title }}
  {{ end }}
  - {{ site.title }}
</title>
```

Dalam mod Notion, sama ada sesuatu medan memasuki `page.fields` dikawal oleh `fieldPolicy` (lihat: [06 Kandungan Notion](./06-notion-content.ms.md)).

## Penghalaan: URL Apa yang Akan Dihasilkan oleh Sesuatu Kandungan?

Pendekatan yang disyorkan: Takrifkan permalink, templat, dan listRoute untuk setiap collection melalui `site.collections` (lihat: [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md)). Enjin teras tidak menjana laluan terbina dalam hanya kerana `type: page` atau `type: post`.

Anda boleh mengawal hasil melalui kaedah berikut:

- Isytiharkan peraturan collection dalam site.collections (disyorkan)
- Tentukan `collection` dalam meta kandungan yang sepadan dengan kunci collection (disyorkan)
- Ubah `slug`: mengubah satu segmen laluan
- Ubah `type`: metadata pilihan atau kunci padanan tema; jangan gunakannya untuk penghalaan
- Gunakan penggantian `route.url` / `route.template`: lebih kuat, tetapi lebih mudah tersalah konfigurasi (lihat: [03 Struktur Projek](./03-project-structure.ms.md) dan [14 Penyelesaian Masalah](./14-troubleshooting.ms.md))

## Tema & Templat: Bagaimana Rupa Halaman

Tema pada dasarnya terdiri daripada tiga jenis benda:

- layouts: Templat (Scriban)
- assets: Sumber yang disalin ke direktori output semasa pembinaan (cth., CSS)
- static: Fail statik yang disalin seperti sedia ada (cth., robots.txt, imej)

Anda boleh menukar tema, menggantikan parameter, dan membaca `site.* / page.* / site.modules.*` dalam templat (lihat: [08 Tema & Templat](./08-themes-templates.ms.md)).

## Plugin: Menjana Fail Tambahan Selepas Pembinaan (sitemap/rss/search, dsb.)

Selepas pembinaan selesai, enjin menjana artifak tambahan berdasarkan konfigurasi dan plugin terbina dalam, seperti:

- `sitemap.xml`
- `rss.xml`
- `search.json` / `search.index.json`
- Halaman senarai teg/kategori (dan halaman terbitan untuk teg/kategori)

Dari perspektif pengguna, anda hanya perlu tahu:

- Anda boleh menggunakan `site.sitemapMode` dan `site.searchMode` untuk mengawal mod output pelbagai bahasa; `site.rssMode` ialah medan legasi (1.0).
- Anda boleh menggunakan `site.pluginFailMode` untuk memutuskan sama ada kegagalan plugin mengganggu pembinaan

Lihat: [10 Ciri & Output Terbina Dalam](./10-built-in-features.ms.md) dan [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md).

## Modules: Tiada Laluan Dijana, Hanya "Menyediakan Data" kepada Templat

Modules digunakan untuk "blok kandungan berstruktur" yang sangat lazim pada laman web syarikat dan halaman pendaratan:

- banner, navigation, features, faq, pricing, footer...

Ia berasal dari `content.sources[].mode: data`, tidak menjadi halaman bebas, tetapi dikumpulkan dan disuntik ke dalam `site.modules.<type>[]` untuk dihasilkan oleh templat halaman utama/halaman seksyen.

Lihat: [09 Modul Data Berstruktur](./09-modules-data.ms.md).
