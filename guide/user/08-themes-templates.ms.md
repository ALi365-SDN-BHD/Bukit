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

## Pratonton Tema: Memeriksa Struktur Tema

Gunakan `bukit theme preview` untuk mendapatkan gambaran terperinci struktur mana-mana tema:

```bash
bukit theme preview my-blog
```

Ini menunjukkan:
- **Metadata**: nama, versi, penerangan, laman utama, lakaran kenit, tag (dari `theme.yaml`)
- **Sections**: section halaman berdaftar dengan penerangan dan kaitan plugin
- **Components**: komponen templat boleh guna semula dengan props yang diisytiharkan
- **Token reka bentuk**: kiraan kumpulan (colors, fonts, radius, spacing, layout) dengan sampel warna
- **Templat susun atur**: semua fail `.scriban`/`.html`/`.sbn` di bawah direktori `layouts/`
- **Statistik fail**: bilangan fail assets dan static

Ini berguna untuk memahami keupayaan tema sebelum memasang atau menyesuaikannya.

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

## Penciptaan Tema: Wizard (Tanya Jawab Interaktif)

Jika anda tidak mahu menulis perintah secara manual, gunakan wizard interaktif untuk mencipta tema dengan cepat:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme wizard my-blog --config site.yaml
```

Wizard akan bertanya secara berurutan:

- Nama tema
- Jenis/preset tema
- Nama jenama
- Warna utama dan warna aksen
- Sama ada mahu terus bertukar ke tema tersebut

Tekan Enter selepas setiap jawapan untuk ke soalan seterusnya, atau Ctrl+C untuk keluar. Selepas semua soalan dijawab, Bukit akan menjana direktori tema dan menulis konfigurasi secara automatik.

Terdapat 5 preset yang boleh dipilih:

| Preset | Senario Penggunaan |
|--------|-------------------|
| `blog` | Blog peribadi/teknikal, dengan senarai, artikel, tag, arkib |
| `docs` | Tapak dokumentasi, dengan navigasi sidebar dan carian |
| `landing` | Halaman tunggal, dengan Hero, ciri, CTA |
| `minimal` | Templat minimum, hanya susun atur asas dan templat halaman |
| `portfolio` | Portfolio, dengan kad projek dan penapisan kategori |

Jika mahu melangkau tanya jawab interaktif, gunakan `--preset`:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme wizard my-blog --preset blog --config site.yaml
```

Anda juga boleh menggabungkan `--brand`, `--primary-color`, `--accent-color`, `--use` dan parameter lain untuk penciptaan satu langkah.

## Penemuan Tema: info/params

Apabila anda ingin mengetahui maklumat terperinci sesuatu tema, gunakan `theme info`:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme info alt --config site.yaml
```

Output termasuk:

- Nama tema, nombor versi
- Penerangan
- Pengisytiharan keupayaan templat (sama ada menyokong penomboran, carian, taksonomi, dll.)
- Senarai fail templat yang tersedia

Lihat semua parameter boleh konfigurasi untuk tema semasa:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme params --config site.yaml
```

Ini akan menyenaraikan semua nama kunci yang tersedia dalam `theme.params` bersama nilai lalai dan penerangan, memudahkan anda untuk mengubah suai dalam `site.yaml`.

## Pengedaran & Perkongsian Tema

Bukit menyokong pembungkusan, perkongsian, dan pemasangan tema dari registry.

Bungkus tema ke dalam fail `.tar.gz` yang boleh diedarkan:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme pack my-theme
```

Pemasangan tema menyokong tiga sumber:

Pasang dari direktori tempatan:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme install /path/to/theme.tar.gz
```

Pasang dari URL:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme install https://example.com/themes/my-theme.tar.gz
```

Cari dan pasang dari registry:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme search blog
dotnet run --project src/Bukit.Cli -c Release -- theme install my-theme --config site.yaml
```

## Perintah Peringkat Templat

Selain perintah peringkat tema, Bukit juga menyediakan operasi templat yang lebih terperinci, dengan 7 subperintah:

| Perintah | Fungsi |
|----------|--------|
| `template create` | Cipta fail templat baharu dalam `layouts/` tema |
| `template list` | Senaraikan semua fail templat tema semasa |
| `template show` | Paparkan kandungan fail templat tertentu |
| `template validate` | Sahkan sintaks Scriban semua templat |
| `template snippets` | Senaraikan atau sisipkan pustaka coretan kod terbina dalam |
| `template hints` | Paparkan pembolehubah dan objek yang tersedia dalam templat |
| `template sync` | Segerakkan templat tema ke direktori akar tapak |

Contoh penggunaan:

```bash
dotnet run --project src/Bukit.Cli -c Release -- template create about --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template show pages/index.html --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template validate --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template hints --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template sync --config site.yaml
```

### Pustaka Coretan Kod Terbina Dalam (Snippets)

`template snippets` menyediakan pustaka coretan terbina dalam yang mengandungi **8 coretan Scriban** dan **9 coretan CSS**, meliputi keperluan templat dan gaya yang lazim.

Lihat senarai coretan yang tersedia:

```bash
dotnet run --project src/Bukit.Cli -c Release -- template snippets --config site.yaml
```

Coretan Scriban merangkumi: gelung halaman, navigasi penomboran, tag meta SEO, kod analitik, penukar bahasa, navigasi breadcrumb, artikel berkaitan, kotak carian.

Coretan CSS merangkumi: reset asas, tipografi artikel, grid responsif, komponen kad, bar navigasi, footer, butang, mod gelap, gaya cetakan.

Sisipkan coretan tertentu ke dalam fail templat:

```bash
dotnet run --project src/Bukit.Cli -c Release -- template snippets pagination --config site.yaml
```

Coretan akan ditulis ke kedudukan yang sesuai dalam templat semasa. Jika templat sudah mengandungi kod yang serupa, perintah akan memberi amaran konflik dan meminta pengesahan untuk menimpa.

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
{{ if site.params.showNewsletter }}
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
