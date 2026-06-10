# Rujukan Parameter CLI

Dokumen untuk penyelenggara, menerangkan perintah, parameter, hubungan tindihan, dan penggunaan lazim.

Pelaksanaan: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

## Gambaran Keseluruhan Perintah

| Perintah | Tujuan |
|---|---|
| `create <dir>` | Cipta projek tapak baharu (setara `init`) |
| `init <dir>` | Mulakan perancah projek tapak |
| `build` | Jana tapak statik |
| `dev` | Pelayan pembangunan HMR (pemantauan fail + binaan tokokan + muat semula langsung) |
| `preview` | Pratonton setempat direktori output |
| `clean` | Bersihkan output dan cache |
| `config check` | Sahkan konfigurasi tanpa membina |
| `config schema` | Jana site.yaml JSON Schema |
| `doctor` | Diagnostik persekitaran dan konfigurasi |
| `plugin` | Perintah berkaitan plugin |
| `theme` | Perintah berkaitan tema |
| `template` | Perintah berkaitan templat |
| `intent` | Perintah berkaitan AI Intent |
| `deploy` | Bina dan deploy ke GitHub Pages |
| `seo` | Audit SEO dan pengesanan regresi |
| `geo` | Audit GEO (Generative Engine Optimization) |
| `clone` | Ekstrak data dari laman web sasaran untuk menjana tema dan kandungan |
| `import` | Import demo HTML atau fail benih ke dalam draf tema/kandungan Bukit |
| `notion` | Jana pelan tolak benih Notion dan sahkan prasyarat tolak |
| `webhook` | Pencetus Webhook |
| `data` | Pemeriksaan dan eksport modul data |
| `completion` | Jana skrip pelengkapan automatik shell |
| `lint` | Periksa konfigurasi dan kandungan Markdown |
| `visual` | Jana skrip ujian regresi visual Playwright |
| `docs` | Semakan konsistensi dokumentasi |
| `publish` | Audit kebolehbacaan mesin dan kepercayaan |
| `route` | Pemeriksaan resolusi laluan |
| `version` | Maklumat versi |

## Hubungan Tindihan Utama (Tertinggi ke Terendah)
1. Parameter CLI (contoh: `--output/--base-url/--clean/--draft/--site-url`)
2. `site.yaml`
3. Lalai kod

## Parameter Binaan Lazim

| Parameter | Tindihan |
|---|---|
| `--config <path>` | Menjadi rootDir konfigurasi |
| `--site <name>` | `sites/<name>.yaml` |
| `--output <dir>` | `build.output` |
| `--base-url <path>` | `site.baseUrl` |
| `--site-url <url>` | `site.url` |
| `--clean`/`--no-clean` | `build.clean` |
| `--draft` | `build.draft=true` |
| `--ci` | Mod CI |
| `--incremental`/`--no-incremental` | Suis tokokan |
| `--cache-dir <dir>` | Lalai `<rootDir>/.cache` |
| `--jobs <n>` | Konkurens rendering selari |
| `--metrics <path>` | JSON metrik binaan |
| `--log-format <text|json>` | Format output log |

## build
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```
