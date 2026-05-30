# Pemerhatian (Log dan Metrik)

Pelaksanaan: `src/Bukit.Shared/Logger.cs`, `src/Bukit.Engine/MetricsWriter.cs`, `src/Bukit.Shared/DiagnosticCode.cs`

## Pengelogan

Default log format: plain text. CI mode recommended: `--log-format json`.
Log level: controlled by `logging.level` (default `info`).
Log sources: `Bukit.Engine`, `Bukit.Content`, `Bukit.Cli`.

## Kod Diagnostik (BKT-XXXX)

Semua pengecualian Bukit membawa kod diagnostik stabil dalam format heks `BKT-XXXX`. Pelaksanaan: `src/Bukit.Shared/DiagnosticCode.cs`, `src/Bukit.Shared/DiagnosticCodeFormatter.cs`, `src/Bukit.Shared/DiagnosticExceptionFormatter.cs`.

| Kategori | Julat | Contoh |
|---|---|---|
| Config | `BKT-0001` – `BKT-00FF` | `BKT-0001` RequiredFieldMissing |
| Theme | `BKT-0101` – `BKT-01FF` | `BKT-0101` ManifestInvalid |
| Route | `BKT-0201` – `BKT-02FF` | `BKT-0201` RouteConflict |
| Render | `BKT-0301` – `BKT-03FF` | `BKT-0301` TemplateNotFound |
| Schema | `BKT-0401` – `BKT-04FF` | `BKT-0402` StrictModeBlocked |
| Content | `BKT-0501` – `BKT-05FF` | `BKT-0501` LoadFailed |
| Build | `BKT-0601` – `BKT-06FF` | `BKT-0601` OutputUnsafe |
| Plugin | `BKT-0701` – `BKT-07FF` | `BKT-0701` ExecutionFailed |
| SEO | `BKT-0801` – `BKT-0804` | |
| GEO | `BKT-0810` – `BKT-0812` | |
| Media | `BKT-0901` – `BKT-0904` | |

DoctorCommand mengeluarkan ralat yang diformat dengan kod diagnostik melalui `DiagnosticExceptionFormatter.Format()`. Semua 13 tapak lemparan kritikal dalam enjin membawa kod diagnostik; lemparan lain kekal serasi ke belakang (Code = null).

## Log Peringkat Saluran Paip Kandungan

Setiap peringkat pemuatan kandungan mencatat penyelesaiannya dengan nama dan tempoh:

```
event=content.stage stage=ContentLoad duration_ms=234
event=content.stage stage=ImageLocalize duration_ms=156
event=content.stage stage=DraftFilter duration_ms=1
event=content.stage stage=SchemaDefaults duration_ms=3
event=content.stage stage=SchemaValidate duration_ms=12
```

Peringkat: `ContentLoad` → `ImageLocalize` → `DraftFilter` → `SchemaDefaults` → `SchemaValidate`. Pelaksanaan: `src/Bukit.Engine/ContentPipeline.cs`, `src/Bukit.Engine/Stages/`.

## Metrik Binaan (`--metrics <path>`)

Menulis JSON metrik binaan untuk penjejakan prestasi CI:

```json
{
  "variants": [{
    "language": "zh-CN",
    "renderCount": 42,
    "skipCount": 18,
    "reasons": { "new_page": 5, "template_changed": 3, "content_changed": 15, "unchanged": 18, "full_render": 1 }
  }],
  "plugins": { "sitemap": { "durationMs": 120 }, "rss": { "durationMs": 80 } }
}
```

## Statistik Notion

Apabila `maxRps` aktif:

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

## Laporan Binaan

Apabila `build.report.enabled: true` (atau `--ci`), enjin menulis laporan binaan berstruktur ke `dist/.bukit/`:

- `build-report.json` — termasuk `schemaErrorCount` (ralat pengesahan skema kandungan), kiraan halaman/laluan/aset, pemasaan, dan statistik tokokan.
- `seo-report.json` — ~40 semakan audit SEO setiap laluan.
- `geo-report.json` — Skor GEO dan kesediaan perangkak LLM.

Laporan ini direka untuk integrasi CI/CD, papan pemuka pemantauan, dan penggunaan ejen AI.

## Metrik untuk Perenderan

`PageRenderDispatcher.DispatchAsync()` bersatu mengumpul metrik perenderan mengikut jenis:

| Kunci Metrik | Jenis | Penerangan |
|---|---|---|
| `pageRender` | Page | Kenaikan setiap halaman diberikan |
| `listBuild` | List | Kenaikan setiap halaman senarai diberikan |
| `staticRender` | Static | Kenaikan setiap HTML statik diberikan |
| `metadataHash` | Page | Kiraan pengiraan hash |
| `bodyLoad` | Page | Kiraan penghidratan badan |
| `listBodyLoad` | List | Kiraan penghidratan badan untuk item senarai |
| `listHash` | List | Kiraan pengiraan hash kandungan senarai |
