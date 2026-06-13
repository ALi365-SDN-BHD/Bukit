# Rujukan Parameter CLI (Penyelenggara)

Dokumen ini adalah rujukan pelaksanaan untuk penyelenggara; ia mesti seiring dengan `src/Bukit.Cli/Cli/BukitCliSpecs.cs` dan `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`.

## Perintah Tahap Atas yang Disokong

| Perintah | Tujuan | Fail Pelaksanaan |
|---|---|---|
| `build` | Jana output tapak statik | `src/Bukit.Cli/Commands/BuildCommand.cs` |
| `config` | Semakan/penjanaan schema | `src/Bukit.Cli/Commands/ConfigCommand.cs` |
| `clean` | Padam output bina dan `.cache/.bukit` | `src/Bukit.Cli/Commands/CleanCommand.cs` |
| `completion` | Jana skrip pelengkapan shell | `src/Bukit.Cli/Commands/CompletionCommand.cs` |
| `deploy` | Jana dan deploy ke GitHub Pages | `src/Bukit.Cli/Commands/DeployCommand.cs` |
| `doctor` | Pemeriksaan diagnostik konfigurasi & tema | `src/Bukit.Cli/Commands/DoctorCommand.cs` |
| `geo` | Audit kualiti GEO (`.bukit/geo-report.json`) | `src/Bukit.Cli/Commands/GeoCommand.cs` |
| `preview` | Server pratonton output bina | `src/Bukit.Cli/Commands/PreviewCommand.cs` |
| `publish` | Audit kualiti publish (`.bukit/publish-audit-report.json`) | `src/Bukit.Cli/Commands/PublishCommand.cs` |
| `seo` | Audit kualiti SEO (`.bukit/seo-report.json`) | `src/Bukit.Cli/Commands/SeoCommand.cs` |
| `version` | Cetak versi dan runtime | `src/Bukit.Cli/Commands/VersionCommand.cs` |

Subcommands:
- `config check`
- `config schema`
- `seo audit`
- `seo diff`
- `geo audit`
- `publish audit`
- `publish diff`

## Order override (Build config)

1. Pilihan CLI (`--output`, `--base-url`, `--site-url`, `--clean`)
2. Nilai fail config
3. Nilai lalai runtime

## build

```bash
bukit build --config site.yaml --clean
```

- `--output <dir>`
- `--base-url <path>`
- `--site-url <url>`
- `--clean` / `--no-clean`
- `--draft`
- `--incremental` / `--no-incremental`
- `--cache-dir <dir>`
- `--metrics <path>`
- `--jobs <n>`
- `--log-format text|json`
- `--ci`

## config

```bash
bukit config check --config site.yaml
bukit config schema --output site.schema.json
```

## doctor

```bash
bukit doctor --config site.yaml
```

Semua pemeriksaan meliputi:
- pengesahan config dan manifest tema
- pemeriksaan sintaks template Scriban dan rujukan include
- kemasukan kemampuan template
- semakan Markdown front matter/syntax
- semakan URL hardcode, plugin, katalog tema
- semakan token dan sambungan Notion (jika ditetapkan)

## preview

```bash
bukit preview --dir dist --port auto
```

- `--dir <path>` (default: `dist`)
- `--host <host>` (default: `localhost`)
- `--port <port|auto>` (default: `4173`, `auto` akan memilih yang kosong)
- `--strict-port`

## clean

```bash
bukit clean --config site.yaml
```

Padam:
- output directory terkonfigurasi atau `--dir`
- `.cache/`
- `.bukit/`

## deploy

```bash
bukit deploy --config site.yaml --dry-run
```

Parameter penting:
- `--dry-run`
- `--skip-build`
- `--force`
- `--branch`, `--message`
- `--base-url`, `--site-url`, `--output`
- `--ci`

Secara lalai, deploy akan run `build` dahulu kecuali `--skip-build`.

## seo / geo / publish

- `seo audit [--dir dist] [--report file] [--strict] [--external]`
- `seo diff --baseline <old> --current <new> ...`
- `geo audit [--dir dist]`
- `publish audit [--dir dist] [--report file] [--strict] [--external]`
- `publish diff --baseline <old> --current <new> ...`

## completion & version

```bash
bukit completion bash
bukit completion zsh
bukit completion fish
bukit version
```
