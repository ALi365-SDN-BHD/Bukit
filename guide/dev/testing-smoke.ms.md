# Sistem Pengujian

Strategi pengujian Bukit menggabungkan ujian unit, ujian integrasi, ujian asap, ujian regresi keselamatan, dan ujian penerimaan berasaskan lekapan. Semua titik masuk direka untuk dijalankan dalam CI dan setempat dengan satu perintah.

## Titik Masuk Skrip

| Skrip | Tujuan | Penggunaan |
|--------|---------|-------|
| `scripts/test-all.sh` | Saluran penuh sekali klik: restore → build → ujian unit → quality-gate → smoke → smoke-all → AOT publish | `bash scripts/test-all.sh [Release]` |
| `scripts/quality-gate.sh` | Ambang liputan (65%), had saiz fail, semakan pengekodan, dotnet format | `COVERAGE_THRESHOLD=65 bash scripts/quality-gate.sh [Release]` |
| `scripts/smoke.sh` | Bina dan sahkan tapak contoh permula | `bash scripts/smoke.sh [Release]` |
| `scripts/smoke-all.sh` | Bina semua 7 tapak contoh + 9 tapak lekapan, sahkan output | `bash scripts/smoke-all.sh [Release]` |
| `scripts/security-regression.sh` | Ujian keselamatan terasing merentasi 5 modul (Shared/Config/CLI/Engine/Content) | `bash scripts/security-regression.sh [Release]` |
| `scripts/stress-test.sh` | Ulang suite ujian penuh N kali untuk mengesan kegagalan berselang | `bash scripts/stress-test.sh 20 [Release]` |

## Struktur CI

GitHub Actions (`ci.yml`) menjalankan 5 kerja:

| Kerja | Matriks OS | Pencetus |
|-----|-----------|---------|
| `quality-gate` | ubuntu-latest | push, PR |
| `cross-platform-tests` | ubuntu, windows, macos | push, PR |
| `smoke-examples` | ubuntu-latest | push, PR |
| `native-aot` | ubuntu, windows, macos | push, PR |
| `stress-cli` | ubuntu-latest | `workflow_dispatch` sahaja (manual) |

## Tapak Lekapan

10 tapak lekapan di bawah `tests/fixtures/` menyediakan pengesahan hujung-ke-hujung yang deterministik:

| Lekapan | Mengesahkan |
|---------|-----------|
| `basic-markdown-site` | Tapak markdown minimum, penjanaan index.html |
| `route-security-site` | Konfigurasi keselamatan laluan |
| `safe-url-content-site` | Pembersihan URL dalam output |
| `plugin-policy-site` | Tingkah laku polisi plugin luaran |
| `output-safety-site` | Keselamatan direktori output |
| `incremental-site` | Binaan tambahan (binaan pertama + kedua) |
| `i18n-site` | Binaan berbilang bahasa (en, zh-CN) |
| `taxonomy-site` | Penjanaan halaman senarai/istilah taksonomi |
| `component-validation-site` | Pengesahan komponen/tema |
| `dotfile-leak-site` | Fail sensitif (.env, .key, .pfx, .git) tidak bocor ke dist/ |

Setiap lekapan mengandungi `site.yaml` minimum, `content/index.md`, direktori `layouts/`, dan fail `static/` pilihan.

### Pengesahan Asap

`smoke-all.sh` melakukan semakan berikut pada setiap binaan yang berjaya:
- `index.html` wujud (mengendalikan subdirektori i18n)
- `sitemap.xml` mengandungi entri `<url>`
- `rss.xml` mengandungi entri `<channel>`
- `search.json` adalah JSON yang sah
- Tiada kebocoran dotfile (`.env`, `.npmrc`, `.key`, `.pfx`, `.p12`, `.git/`)
- Tiada URL berbahaya dalam output (`javascript:`, `data:text/html`, `file://`, `vbscript:`, `//evil.com`)

## Ujian Regresi Keselamatan

`security-regression.sh` mengasingkan ujian berkaitan keselamatan:

- **Shared**: Ujian unit `SafeUrl.ForLink/ForMedia/ForEmbed` dan penolakan URL relatif-protokol
- **Config**: Pengesahan `ExternalPluginPolicy`, laluan pengecualian konfigurasi
- **CLI**: Penolakan lintasan laluan, pengendalian pengecualian konfigurasi
- **Engine**: Keselamatan laluan, keselamatan plugin luaran, mod kegagalan plugin
- **Content**: Keselamatan URL pemapar blok (86 ujian merentasi 8 pemapar), pembersihan teks kaya Notion

## Plugin Protokol Ujian

`ProtocolEchoPlugin` (`tests/ProtocolEchoPlugin/Program.cs`) menyediakan mod deterministik untuk pengujian integrasi plugin luaran:

| Mod | Hook | Output |
|------|------|--------|
| `success` (lalai) | mana-mana | ok=true dengan fail output contoh |
| `derive-success` | derive-pages | 1 halaman terbitan di `/derived/derived-1/` |
| `derive-conflict` | derive-pages | 1 halaman di `/blog/post-1/` (bercanggah dengan kandungan ujian) |
| `derive-lastwins` | derive-pages | 1 halaman di `/derived/conflict/` (tidak bercanggah) |
| `derive-plugin-a` | derive-pages | 1 halaman di `/plugin-conflict/page/` (ID: plugin-a) |
| `derive-plugin-b` | derive-pages | 1 halaman di `/plugin-conflict/page/` (ID: plugin-b, bercanggah dengan plugin-a) |
| `env` | after-build | Melaporkan OPENAI_API_KEY, GITHUB_TOKEN, pemboleh ubah BUKIT_* ke fail |
| `env-allowlist` | after-build | Melaporkan PATH, HOME, NOTION_TOKEN, OPENAI_API_KEY, pemboleh ubah BUKIT_* ke env-report.json |
| `error` | after-build | ok=false dengan mesej ralat |
| `empty` | after-build | Tiada output (stdin kosong) |
| `sleep` | after-build | Tidur 1s, keluar 0 |
| `traversal` | after-build | Menghasilkan fail dengan laluan `../escape.json` (sepatutnya ditolak) |
| `handshake-v2` | handshake | Merundingkan versi skema 2 |

## Bila Perlu Menambah Ujian

- **Ujian unit**: Logik baharu dalam `Bukit.Shared`, `Bukit.Config`, `Bukit.Content`, `Bukit.Engine`, `Bukit.Rendering`
- **Tapak lekapan**: Tingkah laku masa binaan baharu, perubahan struktur output, sempadan keselamatan
- **Regresi keselamatan**: Sebarang perubahan pada SafeUrl, protokol plugin luaran, pengesahan laluan/output laluan
- **Asap**: Perubahan yang mempengaruhi binaan tapak contoh atau laluan hujung-ke-hujung teras

## Seni Bina

```
scripts/
  test-all.sh           → saluran penuh sekali klik
  quality-gate.sh        → liputan + format + semakan pengekodan
  smoke.sh               → asap tapak tunggal
  smoke-all.sh           → tapak contoh + tapak lekapan
  security-regression.sh → ujian keselamatan terasing
  stress-test.sh         → ulang N larian

tests/
  fixtures/              → 10 tapak lekapan deterministik
  ProtocolEchoPlugin/    → plugin luaran deterministik untuk ujian integrasi
  Bukit.*.Tests/         → projek ujian unit/integrasi
```
