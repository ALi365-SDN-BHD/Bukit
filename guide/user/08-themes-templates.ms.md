# 08 Tema & Templat: Rupa Tapak Anda, Cara Medan Digunakan dalam Templat

Tema menentukan penampilan visual dan struktur halaman tapak anda. Sebagai pengguna biasa, anda biasanya akan melakukan tiga perkara:

1. Pilih/tukar tema
2. Laraskan parameter tema (cth., nama jenama, togol navigasi, coretan SEO)
3. Buat perubahan templat kecil (cth., susun atur halaman utama, kandungan footer, masukkan kod analitik)

## Direktori Apa yang Membentuk Tema

Tema biasanya mengandungi tiga jenis direktori (relatif kepada direktori yang mengandungi `site.yaml`):

- `layouts`: Templat (sintaks Scriban)
- `assets`: Sumber yang disalin ke direktori output semasa pembinaan (cth., CSS)
- `static`: Fail statik yang disalin sebagaimana adanya ke direktori output (pilihan)

Contoh tema dalam repo:

- `examples/starter/themes/alt/`
- `examples/starter/themes/seo-best-practice/`

## Kaedah A: Tukar Tema Menggunakan themes/&lt;name&gt; (Disyorkan)

### Sintaks Konfigurasi

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

### CLI: Senarai dan Tukar Tema

Senaraikan `themes/<name>` di bawah akar projek:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
```

Tulis balik ke konfigurasi (tetapkan `theme.name`):

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

## Kaedah B: Selenggara Templat Secara Langsung di Akar Tapak (untuk suntingan pantas tapak tunggal)

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

Anda boleh menyunting fail templat secara langsung di bawah `layouts/`.

## Pembolehubah Apa yang Boleh Digunakan dalam Templat (Paling Lazim untuk Pengguna)

Anda tidak perlu memahami model dalaman enjin; hanya ingat empat jenis objek:

- `site`: Maklumat tapak dan data global (`site.title/site.baseUrl/site.modules...`)
- `page`: Maklumat tentang halaman/artikel semasa (`page.title/page.slug/page.contentHtml/page.fields...`)
- `pages`: Koleksi halaman dalam halaman senarai (lazim di halaman utama, senarai blog, senarai halaman)
- `paginator` (jika tema/halaman anda mempunyai penomboran): Maklumat penomboran (lihat: [10 Ciri & Output Terbina Dalam](./10-built-in-features.ms.md))

### 1) Baca Maklumat Tapak

```scriban
<h1>{{ site.title }}</h1>
```

### 2) Baca Medan Tersuai (Markdown/Notion universal)

```scriban
{{ if page.fields.seo_title }}
  <title>{{ page.fields.seo_title.value }}</title>
{{ end }}
```

### 3) Baca Parameter Tema (theme.params)

```scriban
{{ if site.theme.params.showNewsletter }}
  <section class="newsletter">…</section>
{{ end }}
```

### 4) Baca Modul (site.modules)

```scriban
{{ for b in site.modules.banner }}
  <a href="{{ b.fields.link.value }}">
    <img src="{{ b.fields.image.value }}" alt="{{ b.title }}" />
  </a>
{{ end }}
```

Lihat pemodelan data Modul dan contoh: [09 Modul Data Berstruktur](./09-modules-data.ms.md).

### 5) Cari Butiran Halaman mengikut pageId (site.data.pages_by_id)

```scriban
{{ p = site.data.pages_by_id[pid] }}
{{ if p }}
  {{ if p.url }}
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
  {{ else }}
    <a href="{{ p.external_url }}">{{ p.title }}</a>
  {{ end }}
{{ end }}
```

## Senarai Semak Pengubahsuaian Lazim (dengan contoh)

### 1) Sunting Susun Atur Halaman Utama

Fail lazim: `layouts/pages/index.html`

### 2) Sunting Pengepala/Footer (partials)

Fail lazim: `layouts/partials/header.html`, `layouts/partials/footer.html`

### 3) Masukkan Kod Analitik / Tag Meta

Biasanya dilakukan dalam susun atur asas: `layouts/layouts/base.html`

SEO berkaitan: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md)

## Ralat Lazim dan Pembaikan

- Fail templat hilang: binaan melaporkan "tidak dapat mencari templat/susun atur" → semak sama ada `theme.name` wujud dan struktur direktori lengkap
- CSS/sumber 404: sering disebabkan oleh `site.baseUrl` salah konfigurasi atau templat tidak menambah baseUrl (lihat: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md))
- Medan kosong: templat membaca `page.fields.xxx` tetapi kandungan tidak menyediakan medan tersebut → tambah medan dalam kandungan atau tambah pelindung `if`
- `p.content` kosong dalam halaman senarai: tidak semestinya kerana kandungan tidak dimuatkan; mungkin `build.listPageContentMode` adalah `never`, atau tema semasa tidak mengisytiharkan bahawa templat senarai memerlukan kandungan badan
