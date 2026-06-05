# 09 Modul (Data Berstruktur): Memacu Laman Syarikat / Halaman Pendaratan dengan Modul Data

Tujuan Modul adalah: **mengekstrak "blok kandungan berstruktur dalam halaman" dari templat dan mengubahnya menjadi data yang boleh dikonfigurasikan**.

Laman web syarikat tipikal selalunya bukan "banyak halaman bebas" tetapi "satu halaman utama + beberapa halaman seksyen", di mana setiap halaman dipasang dari modul seperti banner, navigasi, features, faq, pricing, footer. Modul direka untuk keperluan ini.

Lihat contoh boleh laku:

- Konfigurasi: `examples/starter/site.modules.yaml`
- Data contoh: `examples/starter/data/*.md`

## Apa yang Anda Akan Dapat

- Cara mengkonfigurasi `mode: data` untuk menyuntik modul ke dalam pembolehubah templat `site.modules`
- Medan modul yang disyorkan dan pendekatan pemodelan (serasi dengan Markdown dan Notion)
- Pengarangan modul pelbagai bahasa (locale)
- 3 contoh modul sedia salin (banner/nav/faq)

## Langkah 1: Dayakan mode=data dalam sources

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: content
      mode: content
      markdown:
        dir: content
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Ini membawa tingkah laku utama:

- Item kandungan dengan `mode: data` **tidak menjana laluan** (tiada `/pages/...`)
- Ia dikumpulkan mengikut `type` dan disuntik ke dalam `site.modules.<type>[]`

Contohnya, modul dengan `type: banner` akan muncul dalam `site.modules.banner`.

## Langkah 2: Tulis Data Modul (Mod Markdown)

Data modul juga adalah fail Markdown, kecuali `type`nya mewakili "jenis modul" dan bukannya "page/post".

### Contoh 1: banner

Fail: `data/banner-1.md`

```markdown
---
type: banner
title: Banner 1
order: 1
locale: zh-CN
image: https://example.com/banner-1.png
link: https://example.com/
---

Banner 1 body
```

Lihat contoh boleh laku: `examples/starter/data/banner-1.md`.

### Contoh 2: Navigasi (nav)

Fail: `data/nav-home.md`

```markdown
---
type: nav
title: Home Nav
order: 10
locale: zh-CN
items:
  - text: Utama
    href: /
  - text: Blog
    href: /blog/
  - text: Perihal
    href: /pages/about/
---
```

### Contoh 3: FAQ

Fail: `data/faq-main.md`

```markdown
---
type: faq
title: FAQ
order: 30
locale: zh-CN
q1: Apakah Bukit?
a1: Enjin tapak statik yang menyokong Markdown/Notion.
q2: Adakah saya perlu menulis kod?
a2: Tidak, tetapi anda boleh menyesuaikan secara mendalam melalui templat tema.
---
```

## Konvensyen "Medan Modul" yang Disyorkan (sangat disyorkan untuk diseragamkan)

Modul tidak mempunyai skema yang dikuatkuasakan (ia ditentukan oleh tema anda), tetapi untuk kebolehselenggaraan, adalah disyorkan bahawa semua modul merangkumi medan lazim berikut:

| Medan | Tujuan | Nota |
|---|---|---|
| `type` | Jenis modul (kunci pengelompokan) | Wajib; menentukan `site.modules.<type>` mana ia disuntikkan |
| `title` | Tajuk modul | Pilihan tetapi disyorkan |
| `order` | Tertib isih | Disyorkan numerik, lebih kecil = lebih awal |
| `locale` | Bahasa (tapak pelbagai bahasa) | cth., `zh-CN`/`en-US` |
| `enabled` | Togol (pilihan) | Digunakan untuk menurunkan blok kandungan dengan pantas |

## Menggunakan Modul dalam Templat (Contoh Scriban)

### 1) Hasilkan senarai banner

```scriban
{{ for b in site.modules.banner }}
  <section class="banner">
    {{ if b.fields.image }}<img src="{{ b.fields.image.value }}" />{{ end }}
    <h2>{{ b.title }}</h2>
    {{ if b.fields.link }}<a href="{{ b.fields.link.value }}">Lihat Sekarang</a>{{ end }}
  </section>
{{ end }}
```

### 2) Tapis mengikut locale

```scriban
{{ for m in site.modules.faq }}
  {{ if m.meta.locale == site.language }}
    ...
  {{ end }}
{{ end }}
```

## Cadangan Pemodelan Modul dalam Mod Notion (Contoh)

Konfigurasi sources yang sepadan:

```yaml
content:
  provider: sources
  sources:
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "db_modules"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

## Soalan Lazim

### 1) Mengapa modul tidak muncul dalam direktori output?

Normal: modul tidak menjana laluan, jadi anda tidak akan melihat `dist/pages/...`. Ia hanya mempengaruhi HTML halaman semasa rendering templat.

### 2) Mengapa `site.modules.banner` kosong dalam templat?

Semak: Sama ada modules dalam sources mempunyai `mode: data`; Sama ada data modul merangkumi `type: banner`; Sama ada tapak pelbagai bahasa ditapis keluar oleh locale.

### 3) Bagaimana pelbagai sumber `mode: data` bergabung?

Anda boleh mengkonfigurasi pelbagai sumber `mode: data`. Enjin akan memuatkan semua item kandungan dari sumber-sumber ini dan menyuntikkannya ke dalam `site.modules` sebagai set bersatu. Dalam mod pelbagai sumber, `id` setiap item kandungan secara automatik mendapat awalan: `<sourceKey>:<sourceId>`.

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: modules_marketing
      mode: data
      markdown: { dir: data/marketing, defaultType: module }
    - type: markdown
      name: modules_product
      mode: data
      markdown: { dir: data/product, defaultType: module }
    - type: notion
      name: modules_ops
      mode: data
      notion:
        databaseId: "db_modules_ops"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

Pembacaan templat kekal tidak berubah:

```scriban
{{ for b in site.modules.banner }}
  <h2>{{ b.title }}</h2>
{{ end }}
```
