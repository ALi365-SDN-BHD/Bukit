# 12 Rujukan CLI: Perintah & Parameter Paling Lazim (Edisi Pengguna)

Halaman ini menyediakan helaian tipu CLI yang "mencukupi, mudah disalin, perangkap minimum" untuk pengguna biasa. Untuk versi penyelenggara yang lebih lengkap, lihat: [guide/dev/cli](../dev/cli.md).

## Gambaran Keseluruhan Perintah

| Perintah | Bila Anda Menggunakannya |
|---|---|
| `create <dir>` | Cipta projek tapak baharu (perancah); juga gunakan alias `init` |
| `build` | Jana tapak statik (output ke dist/) |
| `preview` | Pratonton setempat direktori output |
| `doctor` | Semakan kendiri persekitaran/konfigurasi (langkah pertama penyelesaian masalah) |
| `clean` | Bersihkan direktori output dan cache |
| `theme` | Senarai/tukar tema |
| `webhook` | Perubahan Notion mencetuskan GitHub Actions (pilihan) |
| `intent` | Berkaitan AI Intent (pilihan) |
| `version` | Output nombor versi |

## Parameter Lazim (dikongsi oleh build/doctor dll.)

| Parameter | Tujuan | Penggunaan Tipikal |
|---|---|---|
| `--config <path>` | Tentukan laluan fail konfigurasi | `--config site.yaml` |
| `--site <name>` | Pelbagai tapak membaca `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | Tindih direktori output | `--output dist` |
| `--base-url <path>` | Tindih baseUrl | `--base-url /my-repo` |
| `--site-url <url>` | Tindih URL mutlak tapak | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | Bersihkan direktori output sebelum bina | `--clean` |
| `--draft` | Hasilkan kandungan draf | `--draft` |
| `--incremental` / `--no-incremental` | Togol binaan tokokan | `--no-incremental` |
| `--cache-dir <dir>` | Direktori cache | `--cache-dir .cache` |
| `--jobs <n>` | Konkurens rendering selari | `--jobs 8` |
| `--metrics <path>` | Output JSON metrik binaan | `--metrics metrics.json` |
| `--log-format <text|json>` | Format log | `--log-format json` |
| `--ci` | Mod CI (tahap log lalai WARN) | `--ci` |

## create / init: Cipta Tapak

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
```

Mod Notion: `dotnet run --project src/Bukit.Cli -c Release -- create my-site --provider notion`

Tentukan templat: `dotnet run --project src/Bukit.Cli -c Release -- create my-site --template minimal`

## build: Bina Tapak

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --site-url https://example.com
```

Sub-laluan GitHub Pages: `dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo`

## preview: Pratonton Setempat

```bash
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## doctor: Semakan Kendiri

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

## clean: Bersihkan

```bash
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
```

## theme: Senarai & Tukar Tema

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

## webhook

```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

Memerlukan: `BUKIT_WEBHOOK_TOKEN`, `BUKIT_GITHUB_TOKEN` (atau `GITHUB_TOKEN`).

## version

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```
