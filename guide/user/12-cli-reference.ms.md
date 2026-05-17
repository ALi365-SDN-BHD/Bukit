# 12 Rujukan CLI: Perintah & Parameter Paling Lazim (Edisi Pengguna)

Halaman ini menyediakan helaian tipu CLI yang "mencukupi, mudah disalin, perangkap minimum" untuk pengguna biasa. Untuk versi penyelenggara yang lebih lengkap, lihat: [guide/dev/cli](../dev/cli.md).

Nota:
- Anda boleh menggunakan `bukit build --help`, `bukit preview --help`, `bukit theme --help` untuk melihat parameter khusus perintah
- Nama parameter dan nilai lalai mengikut bantuan terbina dalam CLI

## Gambaran Keseluruhan Perintah (Anda Mungkin Hanya Menggunakan Ini)

| Perintah | Bila Anda Menggunakannya |
|---|---|
| `create <dir>` | Cipta projek tapak baharu (perancah); juga gunakan alias `init` |
| `build` | Jana tapak statik (output ke dist/) |
| `preview` | Pratonton setempat direktori output |
| `doctor` | Semakan kendiri persekitaran/konfigurasi (langkah pertama penyelesaian masalah) |
| `clean` | Bersihkan direktori output dan cache |
| `theme` | Cipta, senarai, tukar, jelajah, kongsi, dan pasang tema |
| `template` | Cipta, senarai, lihat, sahkan, segerak, dan semak imbas fail templat |
| `clone` | Klon reka bentuk visual mana-mana laman web ke dalam tema Bukit |
| `webhook` | Perubahan Notion mencetuskan GitHub Actions (pilihan) |
| `intent` | Berkaitan AI Intent (pilihan) |
| `version` | Output nombor versi |

Nota:
- Apabila melaksanakan kebanyakan perintah, CLI akan terlebih dahulu mengeluarkan baris `bukit <version>` (untuk mengesahkan versi yang sedang berjalan; `help/version` adalah pengecualian)

## Parameter Lazim (dikongsi oleh build/doctor dll.)

| Parameter | Tujuan | Penggunaan Tipikal |
|---|---|---|
| `--config <path>` | Tentukan laluan fail konfigurasi | `--config site.yaml` / `--config examples/starter/site.yaml` |
| `--site <name>` | Pelbagai tapak membaca `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | Tindih direktori output | `--output dist` |
| `--base-url <path>` | Tindih baseUrl | `--base-url /my-repo` |
| `--site-url <url>` | Tindih URL mutlak tapak | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | Bersihkan direktori output sebelum bina | `--clean` |
| `--draft` | Hasilkan kandungan draf | `--draft` |
| `--incremental` / `--no-incremental` | Togol binaan tokokan | `--no-incremental` (untuk penyelesaian masalah) |
| `--cache-dir <dir>` | Direktori cache | `--cache-dir .cache` |
| `--jobs <n>` | Konkurens rendering selari (integer positif; lalai bilangan teras CPU) | `--jobs 8` |
| `--metrics <path>` | Output JSON metrik binaan | `--metrics metrics.json` |
| `--log-format <text|json>` | Format log | `--log-format json` (disyorkan untuk CI) |
| `--ci` | Mod CI (tahap log lalai WARN) | `--ci` (disyorkan untuk GH Actions) |

## create / init: Cipta Tapak

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
```

`init` adalah alias setara untuk `create`:

```bash
dotnet run --project src/Bukit.Cli -c Release -- init my-site
```

Mod Notion (perancah menjana konfigurasi yang sepadan):

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --provider notion
```

Tentukan templat (lalai `minimal`):

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --template minimal
```

Perancah akan menjana `themes/starter/`, tema permulaan untuk tapak kandungan dengan partial boleh guna semula, CSS responsif, dan templat pilihan untuk penomboran/carian/taksonomi.

## build: Bina Tapak (Paling Lazim)

Dalam direktori tapak:

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- build --clean --site-url https://example.com
```

### Contoh Sub-Laluan GitHub Pages (baseUrl)

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### Output metrik & log berstruktur (disyorkan untuk CI)

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --metrics metrics.json --log-format json
```

## preview: Pratonton Setempat Direktori Output

```bash
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

Parameter lazim:

- `--dir <path>`: Direktori pratonton (lalai `dist`)
- `--host <host>`: Lalai `localhost`
- `--port <port|auto>`: `auto` untuk pemilihan port automatik
- `--strict-port`: Mod port ketat (ralat dan bukannya tukar automatik apabila port diduduki)

## doctor: Semakan Kendiri & Penyelesaian Masalah (Langkah Pertama)

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

Jalankan doctor dahulu apabila anda menghadapi isu ini:

- Token Notion hilang
- Laluan tidak wujud (content/theme/output binaan)
- Ralat medan konfigurasi, ketidakpadanan jenis

Senarai semak penyelesaian masalah: [14 Penyelesaian Masalah](./14-troubleshooting.ms.md).

## clean: Bersihkan Output & Cache

```bash
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
```

Disyorkan untuk dijalankan dalam situasi berikut:

- Menukar struktur direktori tema
- Membuat perubahan ketara pada peraturan penghalaan/mod output
- Mengesyaki cache tokokan menyebabkan "nampak seperti tidak dikemas kini"

## theme: Penciptaan, Penemuan, Perkongsian Tema

```bash
# Senaraikan semua tema (menunjukkan versi, penerangan, tag)
bukit theme list --config site.yaml

# Cipta dari starter
bukit theme create custom --config site.yaml --brand "My Site" --primary-color "#0b5fff" --use

# Wizard interaktif (Soal Jawab dengan pemilihan pratetap)
bukit theme wizard my-blog

# Penciptaan pantas dengan pratetap
bukit theme wizard my-blog --preset blog

# Lihat butiran tema
bukit theme info starter --config site.yaml

# Senaraikan parameter tema
bukit theme params --config site.yaml

# Tukar tema aktif
bukit theme use alt --config site.yaml
```

`theme create` mencipta `themes/<name>/` dari starter terbina dalam secara lalai. Gunakan `--from <tema-sedia-ada>` untuk menyalin tema sedia ada, `--force` untuk menimpa, dan `--use` untuk menulis `theme.name` kembali ke konfigurasi yang dipilih.

`theme wizard` menjalankan Soal Jawab interaktif. Gunakan `--preset` (blog/docs/landing/minimal/portfolio) untuk penciptaan pantas berasaskan lalai.

### Pengedaran Tema

```bash
# Pek tema untuk perkongsian
bukit theme pack my-blog          # → my-blog-1.0.0.tar.gz

# Pasang dari fail setempat
bukit theme install ./my-blog-1.0.0.tar.gz

# Pasang dari URL
bukit theme install https://github.com/user/theme/releases/download/v1.0/theme.tar.gz

# Cari pendaftaran tema komuniti
bukit theme search               # senaraikan semua
bukit theme search blog          # tapis mengikut nama/tag

# Pasang dari pendaftaran
bukit theme install --registry blog-clean
```

## template: Pengurusan Peringkat Templat

```bash
# Senaraikan semua templat dalam tema aktif
bukit template list --config site.yaml

# Lihat kandungan templat
bukit template show pages/index.html --config site.yaml

# Sahkan sintaks Scriban semua templat
bukit template validate --config site.yaml

# Penciptaan templat interaktif
bukit template create pages/gallery.html --config site.yaml

# Semak imbas pustaka coretan
bukit template snippets
bukit template snippets post-card

# Tunjukkan semua pembolehubah templat yang tersedia
bukit template hints

# Jana bukit.templates.yaml secara automatik
bukit template sync --config site.yaml
```

Untuk penggunaan tema dan templat yang terperinci, lihat: [08 Tema & Templat](./08-themes-templates.ms.md).

## clone: Klon Reka Bentuk Visual Laman Web ke dalam Tema

```bash
# Klon reka bentuk visual laman web
bukit clone https://example.com --name my-theme

# Tentukan direktori output
bukit clone https://example.com --name my-theme --output ./themes

# Klon halaman tertentu sahaja
bukit clone https://example.com/about --name about-theme --page-only
```

Perintah `clone` menganalisis warna, tipografi, jarak, susun atur, dan elemen visual lain laman web sasaran, lalu menjana fail tema Bukit yang sepadan.

## webhook: Perubahan Notion Mencetuskan GitHub Actions (Pilihan)

```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

Parameter yang tersedia:

- `--host <host>`: Alamat dengar (lalai `localhost`)
- `--port <port>`: Port dengar (lalai `8787`)
- `--path <path>`: Laluan HTTP (lalai `/webhook/notion`)
- `--repo <owner/repo>`: Repositori sasaran
- `--event <type>`: Jenis acara repository_dispatch

Ia memerlukan pembolehubah persekitaran:

- `BUKIT_WEBHOOK_TOKEN` (pengepala permintaan masuk `X-Sitegen-Token`)
- `BUKIT_GITHUB_TOKEN` (atau `GITHUB_TOKEN`)

Butiran keselamatan dan penggunaan: [guide/dev/webhook](../dev/webhook.md).

## version: Semak Versi

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```

Mengeluarkan nombor versi CLI semasa.
