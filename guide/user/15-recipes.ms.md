# 15 Resipi: Panduan Langkah Demi Langkah Mengikut "Apa yang Saya Ingin Capai"

Halaman ini menyusun keperluan lazim sebagai "Matlamat → Konfigurasi → Data → Perintah," sesuai untuk diikuti secara terus.

Anggap ia sebagai "buku resipi": mula-mula salin sepenuhnya untuk menjalankannya, kemudian ubah suai mengikut keperluan anda.

## Resipi 1: Blog Minimum (Markdown)

### Matlamat

- Blog menggunakan Markdown setempat sahaja
- Jana senarai blog dan halaman artikel (bergantung pada tema)

### Konfigurasi (site.yaml)

```yaml
site:
  name: my-blog
  title: My Blog
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
content:
  sources:
    - name: pages
      mode: content
      collection: post
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

### Data Contoh (content/)

`content/2026-01-first.md`

```markdown
---
collection: post
title: Artikel Pertama
slug: first
publishAt: 2026-01-01T10:00:00+08:00
tags: [demo]
categories: updates
summary: Ini adalah artikel pertama
---

# Artikel Pertama

Hello Bukit.
```

### Bina & Pratonton

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Resipi 2: Laman Pelbagai Bahasa (Markdown Dwibahasa)

### Matlamat

- Output dwibahasa zh-CN + en-US
- Setiap kandungan ditandakan dengan language

### Konfigurasi

Rujuk terus contoh yang boleh dijalankan: `examples/starter/site.i18n.yaml`.

Versi minimum:

```yaml
site:
  name: my-i18n
  title: My i18n Site
  baseUrl: /
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
  timezone: Asia/Shanghai
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
content:
  sources:
    - name: pages
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: alt
```

### Data Contoh

`content/greeting-zh.md`

```markdown
---
collection: page
title: 你好
slug: greeting
language: zh-CN
---

# 你好
```

`content/greeting-en.md`

```markdown
---
collection: page
title: Hello
slug: greeting
language: en-US
---

# Hello
```

## Resipi 3: Halaman Utama Korporat (Modules Data + Tema)

### Matlamat

- Halaman utama dipasang dari modul banner/features/faq/pricing/footer
- Kandungan modul diuruskan dari `data/`, templat membaca `site.modules.*`

### Konfigurasi

Rujuk terus contoh yang boleh dijalankan: `examples/starter/site.modules.yaml`.

### Data Contoh (Meniru Tiga Blok)

`data/banner-1.md`

```markdown
---
type: banner
title: Banner 1
order: 1
locale: zh-CN
image: https://example.com/banner.png
link: https://example.com/
---
```

`data/features-main.md`

```markdown
---
type: features
title: Keupayaan Teras
order: 10
locale: zh-CN
f1_title: Pantas
f1_desc: Bermula dalam 10 minit
f2_title: Terkawal
f2_desc: Dipacu konfigurasi, templat boleh dikembangkan
---
```

`data/footer-main.md`

```markdown
---
type: footer
title: Footer
order: 100
locale: zh-CN
copyright: "© 2026 My Site"
---
```

### Apa yang Templat Perlu Lakukan

Dalam templat tema, baca:

- `site.modules.banner`
- `site.modules.features`
- `site.modules.footer`

Contoh lihat: [09 Modul Data Berstruktur](./09-modules-data.ms.md).

## Resipi 4: Notion sebagai CMS (Hanya Papar Published)

### Matlamat

- Kandungan diselenggara oleh operasi dalam pangkalan data Notion
- Hanya papar kandungan di mana Published=✅

### Konfigurasi (site.yaml)

```yaml
site:
  name: notion-site
  title: Notion Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  sources:
    - name: notion
      mode: content
      notion:
        databaseId: "id-pangkalan-data-anda"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        fieldPolicy:
          mode: whitelist
          allowed: [seo_title, seo_desc, cover, reading_time]
build:
  output: dist
  clean: true
theme:
  name: alt
```

> **Disyorkan: isytihar site.collections dan selaraskan dengan medan Notion Collection.** Disyorkan untuk menambah nod `site.collections` dalam site.yaml dan mencipta medan `Collection` (jenis select) dalam pangkalan data Notion, supaya enjin mengutamakan penghalaan dipacu collection.

### Perkara Utama Semasa Menjalankan

- Tetapkan pembolehubah persekitaran `NOTION_TOKEN` secara setempat dahulu
- Kemudian jalankan `doctor` dan `build`

Butiran lihat: [06 Kandungan Notion](./06-notion-content.ms.md).

## Resipi 5: Pelbagai Laman (Urus main + blog Dalam Repo yang Sama)

### Matlamat

- Akar repo adalah laman utama
- `sites/blog.yaml` adalah laman blog

### Langkah

1. Tulis konfigurasi blog dalam `sites/blog.yaml` (boleh rujuk `examples/starter/sites/blog.yaml`)
2. Bina blog:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean --site-url https://example.com
```

## Resipi 6: Penyahgunaan Repositori Projek GitHub Pages (Membaiki Sumber 404)

### Matlamat

- Menyahguna ke `https://<owner>.github.io/<repo>/`
- Halaman dan sumber semuanya dimuatkan dengan betul

### Perkara Utama

Mesti dihantar semasa bina:

```bash
--base-url /<repo> --site-url https://<owner>.github.io/<repo>
```

Arahan penuh lihat: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md).
