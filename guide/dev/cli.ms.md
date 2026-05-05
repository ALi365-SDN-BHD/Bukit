# Rujukan Parameter CLI

Dokumen untuk penyelenggara, menerangkan perintah, parameter, hubungan tindihan, dan penggunaan lazim.

Pelaksanaan: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

## Gambaran Keseluruhan Perintah

| Perintah | Tujuan |
|---|---|
| `create <dir>` | Cipta projek tapak baharu (setara `init`) |
| `init <dir>` | Mulakan perancah projek tapak |
| `build` | Jana tapak statik |
| `preview` | Pratonton setempat direktori output |
| `clean` | Bersihkan output dan cache |
| `doctor` | Diagnostik persekitaran dan konfigurasi |
| `plugin` | Perintah berkaitan plugin |
| `theme` | Perintah berkaitan tema |
| `intent` | Perintah berkaitan AI Intent |
| `webhook` | Pencetus Webhook |
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
