# 03 Struktur Projek & Konvensyen: Tempat Meletakkan Fail, Cara Laluan Relatif Berfungsi

Halaman ini menangani dua soalan berfrekuensi tinggi:

1. "Di mana saya harus meletakkan kandungan, tema, dan aset?"
2. "Apakah `dir: content` dalam konfigurasi relatif kepada?"

## Struktur Direktori Minimum yang Disyorkan

Ambil tapak Markdown sebagai contoh:

```text
my-site/
  site.yaml
  content/            # Kandungan Markdown
    about.md
    hello-world.md
  assets/             # Aset (contohnya, CSS)
    style.css
  static/             # Fail statik disalin sebagaimana adanya (pilihan)
    robots.txt
  layouts/            # Templat tema (atau gunakan themes/<name>)
    layouts/
      base.html
    pages/
      index.html
      page.html
      post.html
      list.html
    partials/
      header.html
      footer.html
  dist/               # Output binaan (build.output)
```

Contoh yang boleh dijalankan wujud dalam repositori: `examples/starter/`, dengan struktur yang lebih lengkap untuk rujukan terus.

## "Asas Laluan Relatif" (Sangat Penting)

Dalam Bukit, sebahagian besar laluan relatif diselesaikan relatif kepada **direktori yang mengandungi fail konfigurasi** (direktori `site.yaml`).

Contohnya, jika anda menulis:

```yaml
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
theme:
  layouts: layouts
  assets: assets
```

Ini bermaksud:

- Direktori kandungan adalah `<direktori site.yaml>/content`
- Direktori output adalah `<direktori site.yaml>/dist`
- Direktori templat adalah `<direktori site.yaml>/layouts`

Inilah sebabnya `--config <path>` adalah kritikal: ia bukan sahaja menentukan fail konfigurasi, tetapi juga menetapkan asas laluan.

## Pelbagai Tapak: Cara sites/<name>.yaml Berfungsi

Apabila anda menyelenggara beberapa tapak dalam repositori yang sama (contohnya, `main` dan `blog`), anda boleh menggunakan:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

Ia membaca `sites/blog.yaml` sebagai konfigurasi, tetapi **rootDir masih direktori semasa** (bukan direktori `sites/`).

Rujuk contoh:

- `examples/starter/sites/blog.yaml`

Konvensyen yang disyorkan:

```text
repo/
  site.yaml           # Konfigurasi tapak utama (lalai)
  sites/
    blog.yaml         # Konfigurasi tapak blog
  content/            # Kandungan boleh guna semula
  themes/             # Koleksi tema
```

## Konvensyen Direktori Tema: layouts/assets/static

Anda boleh meletakkan `layouts/assets/static` terus dalam akar tapak, atau anda boleh mengumpulkan tema di bawah `themes/<name>/` dan menukar menggunakan `theme.name`.

### Kaedah A: Selenggara Templat Terus dalam Tapak

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

### Kaedah B: Tukar Tema Menggunakan themes/<name> (Lebih Disyorkan)

```yaml
theme:
  name: alt
```

Dan letakkan direktori tema di bawah:

```text
themes/
  alt/
    layouts/
    assets/
    static/
```

Contoh yang boleh dijalankan:

- `examples/starter/themes/alt/`
- `examples/starter/site.theme.yaml`

## Penamaan Fail Kandungan & Konvensyen Medan (Cadangan)

### slug (Sangat Disyorkan untuk Kekal Stabil)

- slug adalah fragmen teras URL; menukarnya selalunya bermaksud URL berubah
- Cadangan: kekalkan slug selaras dengan nama fail (contohnya, `hello-world.md` → slug `hello-world`)
- Jika anda memerlukan penghubungan pelbagai bahasa dan i18n, adalah juga disyorkan untuk menyelenggara `i18n_key` yang stabil (terutamanya biasa dalam Notion)

### collection / type (Medan Padanan Routing)

> Adalah disyorkan untuk mengutamakan penggunaan `site.collections` untuk mentakrifkan koleksi kandungan dan peraturan penghalaan (lihat [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md)).

Kandungan yang perlu dijana sebagai halaman harus mengisytiharkan `collection` yang sepadan dengan `site.collections`. Medan `type` boleh kekal sebagai metadata kandungan atau padanan tema, tetapi ia bukan medan routing starter 1.0.

Tema biasanya membezakan templat dan halaman senarai mengikut type atau collection; tidak disyorkan untuk menambah terlalu banyak jenis tersuai secara sambil lewa melainkan tema anda sudah menyokong templat yang sepadan.

### language (Pelbagai Bahasa)

Untuk tapak pelbagai bahasa, setiap kandungan harus secara eksplisit tergolong dalam satu bahasa:

- Markdown: Tulis `language: zh-CN` / `language: en-US` dalam Front Matter
- Notion: Tambah medan `language` (ia akan dinaikkan kepada meta)

Untuk output pelbagai bahasa dan SEO, lihat: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md).

## Lanjutan: Medan Penggantian Laluan (Gunakan dengan Berhati-hati)

Jika anda benar-benar perlu URL awam tersuai, gunakan medan tindihan laluan berikut:

- `route.url` atau `url` aras atas: Menentukan URL awam
- `route.template` atau `template` aras atas: Menentukan templat
- `outputPath`: Dibuang dalam Bukit 1.0; laluan output diterbitkan daripada URL akhir dan nilai manual ditolak

Akibat biasa daripada salah konfigurasi medan ini:

- Halaman "hilang" (dioutputkan ke laluan yang tidak dijangka)
- Pautan tidak betul dalam sitemap/rss/search
- Ralat 404 GitHub Pages (ketidakpadanan baseUrl/laluan)

Adalah disyorkan untuk menyelesaikan keperluan penghalaan melalui `collection` dan `slug` dahulu; rujuk [14 Penyelesaian Masalah](./14-troubleshooting.ms.md) apabila anda benar-benar memerlukan penggantian.
