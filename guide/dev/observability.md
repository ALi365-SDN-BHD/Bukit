# Observability (Logs and Metrics)

Implementation: `src/Bukit.Shared/Logger.cs`, `src/Bukit.Engine/MetricsWriter.cs`, `src/Bukit.Shared/DiagnosticCode.cs`

## Logging

Default: <serilog-settings-style>
Default log format: plain text. CI mode recommended: `--log-format json`.
Log level: controlled by `logging.level` (default `info`).
Log sources: `Bukit.Engine`, `Bukit.Content`, `Bukit.Cli`.

## Diagnostic Codes (BKT-XXXX)

All Bukit exceptions carry stable diagnostic codes in `BKT-XXXX` hex format. Implementation: `src/Bukit.Shared/DiagnosticCode.cs`, `src/Bukit.Shared/DiagnosticCodeFormatter.cs`, `src/Bukit.Shared/DiagnosticExceptionFormatter.cs`.

| Category | Range | Example |
|---|---|---|
| Config | `BKT-0001` – `BKT-00FF` | `BKT-0001` RequiredFieldMissing |
| Theme | `BKT-0101` – `BKT-01FF` | `BKT-0101` ManifestInvalid |
| Route | `BKT-0201` – `BKT-02FF` | `BKT-0201` RouteConflict |
| Render | `BKT-0301` – `BKT-03FF` | `BKT-0301` TemplateNotFound |
| Schema | `BKT-0401` – `BKT-04FF` | `BKT-0402` StrictModeBlocked |
| Content | `BKT-0501` – `BKT-05FF` | `BKT-0501` LoadFailed |
| Build | `BKT-0601` – `BKT-06FF` | `BKT-0601` OutputUnsafe |
| Plugin | `BKT-0701` – `BKT-07FF` | `BKT-0701` ExecutionFailed |

DoctorCommand outputs errors formatted with diagnostic codes via `DiagnosticExceptionFormatter.Format()`. All 13 critical throw sites in the engine carry diagnostic codes; other throws remain backward-compatible (Code = null).

## Content Pipeline Stage Logs

Each content loading stage logs its completion with name and duration:

```
event=content.stage stage=ContentLoad duration_ms=234
event=content.stage stage=ImageLocalize duration_ms=156
event=content.stage stage=DraftFilter duration_ms=1
event=content.stage stage=SchemaDefaults duration_ms=3
event=content.stage stage=SchemaValidate duration_ms=12
```

Stages: `ContentLoad` → `ImageLocalize` → `DraftFilter` → `SchemaDefaults` → `SchemaValidate`. Implementation: `src/Bukit.Engine/ContentPipeline.cs`, `src/Bukit.Engine/Stages/`.

## Build Metrics (`--metrics <path>`)

Writing build metrics JSON provides structured data for CI performance tracking:

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

## Notion Statistics

When `maxRps` is active, a single summary line is output at the end of each content source:

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

## Build Reports

When `build.report.enabled: true` (or `--ci`), the engine writes structured build reports to `dist/.bukit/`:

- `build-report.json` — includes `schemaErrorCount` (content schema validation errors), page/route/asset counts, timing, and incremental stats.
- `seo-report.json` — ~40 SEO audit checks per route.
- `geo-report.json` — GEO Score and LLM crawler readiness.

These reports are designed for CI/CD integration, monitoring dashboards, and AI agent consumption.

## Metrics for Rendering

The unified `PageRenderDispatcher.DispatchAsync()` collects per-kind render metrics:

| Metric Key | Kind | Description |
|---|---|---|
| `pageRender` | Page | Increment per page rendered |
| `listBuild` | List | Increment per list page rendered |
| `staticRender` | Static | Increment per static HTML rendered |
| `metadataHash` | Page | Hash computation count |
| `bodyLoad` | Page | Body hydration count |
| `listBodyLoad` | List | Body hydration count for list items |
| `listHash` | List | List content hash computation count |
