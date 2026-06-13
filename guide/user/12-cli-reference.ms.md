# 12 Rujukan CLI: Perintah & Parameter Paling Lazim (Edisi Pengguna, Versi Semasa)

Dokumen ini mengikuti set perintah `bukit` yang benar-benar didaftarkan pada repositori ini.

Rujuk [guide/dev/cli](./../dev/cli.md) untuk maklumat pengesanan dan perincian pembangun.

> Nota: Semua perintah (kecuali `version`) akan memaparkan `bukit <version>` lebih dahulu ke stderr untuk pengesahan versi.

## Gambaran Perintah

| Perintah | Tujuan | Parameter Penting |
|---|---|---|
| `build` | Jana tapak statik | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`/`--no-clean`, `--draft`, `--ci`, `--incremental`/`--no-incremental`, `--cache-dir`, `--jobs`, `--metrics`, `--log-format` |
| `config check` | Sahkan `site.yaml` sahaja | `--config`, `--site`, `--site-url` |
| `config schema` | Jana JSON Schema untuk `site.yaml` | `--output` |
| `doctor` | Diagnostik konfigurasi dan templat | `--config`, `--site`, `--site-url` |
| `preview` | Pratonton direktori keluaran | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site` |
| `clean` | Padam output dan cache | `--config`, `--site`, `--dir` |
| `seo audit` | Audit `seo-report.json` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | Bandingkan dua laporan SEO | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `geo audit` | Audit `geo-report.json` dan kehadiran fail llms | `--dir` |
| `publish audit` | Audit `publish-audit-report.json` | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | Bandingkan dua laporan publish | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `deploy` | Bina (lalai) dan deploy ke GitHub Pages | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |
| `completion` | Jana skrip pelengkapan shell | `<shell>` (`bash|zsh|fish`) |
| `version` | Papar versi CLI | tiada |

## Menjalankan CLI

Jika memasang binari `bukit`:

```bash
bukit build --config site.yaml --clean
```

Jika menjalankan dari sumber:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean
```

## build

```bash
bukit build --config site.yaml --clean --site-url https://example.com
```

- `--config <path>`: laluan `site.yaml` (default `site.yaml`)
- `--site <name>`: baca `sites/<name>.yaml`
- `--output <dir>`: ganti direktori output
- `--base-url <path>`: ganti baseUrl
- `--site-url <url>`: ganti `site.url`
- `--clean` / `--no-clean`: bersihkan dahulu sebelum bina
- `--draft`: sertakan draf kandungan
- `--ci`: mod log CI
- `--incremental` / `--no-incremental`: toggle increment
- `--cache-dir <dir>`: cache override
- `--metrics <path>`: JSON metrik binaan
- `--jobs <n>`: serentak rendering
- `--log-format text|json`: format output

## config check / config schema

```bash
bukit config check --config site.yaml
bukit config schema --output site.schema.json
```

`config schema` tanpa `--output` akan mencetak ke stdout.

## doctor

```bash
bukit doctor --config site.yaml
```

## preview

```bash
bukit preview --dir dist --port auto
```

Parameter:
- `--dir <path>`: lalai `dist` (atau auto-didakrikan dari config)
- `--host <host>`: lalai `localhost`
- `--port <port|auto>`: lalai `4173`, `auto` auto-pilih port
- `--strict-port`: gagal jika port sibuk (tanpa auto-shift)
- `--config`/`--site`: jika diberi, output dir diambil dari config

## clean

```bash
bukit clean --config site.yaml
```

Parameter:
- `--config` / `--site`: bersihkan output dari config
- `--dir <path>`: padam direktori output tertentu (lalai `dist`)

## seo / geo / publish

```bash
bukit seo audit --dir dist --strict
bukit seo diff --baseline old/seo-report.json --current dist/.bukit/seo-report.json
bukit geo audit --dir dist
bukit publish audit --dir dist
bukit publish diff --baseline old/publish-audit-report.json --current dist/.bukit/publish-audit-report.json
```

## deploy

```bash
bukit deploy --config site.yaml --dry-run
```

- `--skip-build`: lepas bina semula sebelum deploy
- `--force`: paksa push yang non-fast-forward
- `--branch`, `--message`, `--base-url`, `--site-url`, `--output`, `--ci`

## completion & version

```bash
bukit completion bash
bukit completion zsh
bukit completion fish
bukit version
```
