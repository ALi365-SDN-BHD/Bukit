# 01 Permulaan Pantas: Dari Sifar ke Pratonton (10 Minit)

Halaman ini membimbing anda melalui saluran lengkap menggunakan pendekatan "salin-tampal": mulakan tapak → tulis kandungan → bina → pratonton setempat → sediakan untuk penerapan.

## Apa yang Anda Akan Dapat

- Sebuah tapak statik yang boleh dipratonton secara setempat (output di `dist/`)
- Satu `site.yaml` minimum yang berfungsi (semua ciri seterusnya dikembangkan daripadanya)
- Satu set arahan CLI yang paling kerap digunakan (build/preview/doctor/clean)

## Prasyarat

- .NET dipasang (projek ini menyasarkan .NET 10; jika anda menjalankan dari sumber repositori, SDK yang sepadan mesti tersedia pada mesin anda)
- Selesa menggunakan baris arahan (PowerShell / bash)
- Pemahaman asas tentang sintaks YAML/Markdown

## Laluan A: Jalankan Contoh Tapak Dalam-Repo Secara Langsung (Disyorkan)

Tapak contoh terletak di: `examples/starter/`, yang dilengkapi dengan kandungan, tema, dan konfigurasi variasi untuk pelbagai bahasa/modul, sesuai untuk mengesahkan persekitaran anda disediakan dengan betul.

### 1) Bina dan Semak Kendiri (doctor)

Jalankan dari akar repositori:

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
```

Jika doctor melaporkan ralat, semak dahulu: [14 Penyelesaian Masalah](./14-troubleshooting.ms.md) (dan versi pembangun panduan doctor: [guide/dev/doctor](../dev/doctor.md)).

### 2) Bina Tapak (build)

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
```

Output binaan dihantar ke `build.output` dalam konfigurasi contoh (lalai: `examples/starter/dist/`).

### 3) Pratonton Setempat (preview)

```bash
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

Konsol akan mencetak URL setempat — buka dalam pelayar anda.

## Laluan B: Cipta Tapak Anda Sendiri (Mod Markdown)

Jika anda memulakan projek laman web sebenar, adalah disyorkan untuk menjalankan `create` dalam direktori baharu, yang akan menjana struktur direktori asas dan konfigurasi lalai.

### 1) Cipta Perancah Tapak

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
```

Anda akan mendapat struktur yang serupa dengan ini (ilustrasi; output sebenar bergantung pada perancah):

```text
my-site/
  site.yaml
  content/
  layouts/    # atau themes/ (bergantung pada perancah dan pilihan tema)
  assets/
  static/
```

### 2) Edit Konfigurasi Minimum (site.yaml)

Satu `site.yaml` minimum yang berfungsi (tapak Markdown) kelihatan seperti ini:

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
  layouts: layouts
  assets: assets
  static: static
  name: alt
logging:
  level: info
```

> **Disyorkan: Gunakan site.collections untuk mentakrifkan penghalaan dan templat** Konfigurasi di atas bergantung pada lapisan keserasian post/page untuk penghalaan (page → `/pages/`, post → `/blog/`). Untuk projek baharu, kami mengesyorkan mengisytiharkan collections secara eksplisit (lihat [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md)). Contoh:
>
> ```yaml
> site:
>   collections:
>     page:
>       permalink: /pages/{slug}/
>       template: pages/page.html
>       listRoute: /pages/
>     post:
>       permalink: /blog/{slug}/
>       template: pages/post.html
>       listRoute: /blog/
> ```

Untuk penjelasan yang lebih lengkap tentang medan dan lalai, lihat: [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md).

### 3) Tulis Kandungan Pertama Anda (content/hello-world.md)

```markdown
---
type: page
title: Hello World
slug: hello-world
tags: [demo, first]
summary: Ini adalah halaman pertama saya
---

# Hello World

Jika anda dapat melihat teks ini, saluran paip binaan dan pemaparan telah berjalan dengan jayanya.
```

### 4) Semak Kendiri (doctor)

Jalankan dari direktori tapak:

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- doctor --config site.yaml
```

### 5) Bina dan Pratonton

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project ../src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Langkah Seterusnya (mengikut Jenis Tapak)

- Menulis kandungan (Markdown): [05 Kandungan Markdown](./05-markdown-content.ms.md)
- Menggunakan Notion: [06 Kandungan Notion](./06-notion-content.ms.md)
- Komposisi pelbagai sumber (pages/posts/modules): [07 Pelbagai Sumber](./07-multi-source.ms.md)
- Modul tapak syarikat (Modules): [09 Modul Data Berstruktur](./09-modules-data.ms.md)
- Pelbagai bahasa & SEO: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md)
- Menerapkan ke GitHub Pages: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md)
