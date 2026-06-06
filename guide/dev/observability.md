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
| SEO | `BKT-0801` – `BKT-08FF` | `BKT-0801` SeoAuditFailed — SEO external link audit or index validation |
| GEO | `BKT-0810` – `BKT-081F` | `BKT-0810` GeoAuditFailed — GEO readiness audit |
| Media | `BKT-0901` – `BKT-09FF` | `BKT-0901` MediaDownloadFailed, `BKT-0902` ImageOptimizeFailed, `BKT-0903` ScssCompileFailed, `BKT-0904` MediaSsrfBlocked |

> **P3-1 修复记录**：诊断码范围从 SEO(0x0800-0x0804)/GEO(0x0810-0x0812)/Media(0x0900-0x0904) 扩展为完整的 256 个码位子范围（0x0800-0x08FF / 0x0810-0x081F / 0x0900-0x09FF），为 SEO/GEO/媒体诊断提供充足扩展空间。

DoctorCommand outputs errors formatted with diagnostic codes via `DiagnosticExceptionFormatter.Format()`. All 13 critical throw sites in the engine carry diagnostic codes; other throws remain backward-compatible (Code = null).

## CLI Exit Codes

Bukit CLI uses a layered exit code strategy. Implementation: `src/Bukit.Cli/Program.cs`.

| Exit Code | Meaning | Exception Types |
|---|---|---|
| `0` | Success | — |
| `1` | Unexpected error | Unhandled `Exception` (runtime failures, bugs) |
| `2` | Configuration or content error | `ConfigException`, `CommandArgumentException`, `ContentException` |
| `3` | Render error | `RenderException` |

> **v1.0.7+**: `ConfigPathResolver` path traversal (`--site ../../../etc/passwd`) now throws `ConfigException` with `BKT-0004` (exit code 2), consistent with `BuildPathUtils` and `ThemePathResolver`.

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
    "bodyCache": {
      "totalRequests": 100,
      "cacheHits": 72,
      "cacheMisses": 20,
      "inlineBypasses": 8,
      "uniqueBodies": 15,
      "amplification": 6.7,
      "maxSize": 256,
      "currentSize": 142
    },
    "reasons": { "new_page": 5, "template_changed": 3, "content_changed": 15, "unchanged": 18, "full_render": 1 }
  }],
  "plugins": { "sitemap": { "durationMs": 120 }, "rss": { "durationMs": 80 } }
}
```

**BodyCache 指标说明**（P0-3 修复）：
- `totalRequests`：构建期间请求 body 的总次数
- `cacheHits`：缓存命中次数（已缓存的 body 直接返回）
- `cacheMisses`：缓存未命中次数（需要从底层存储加载）
- `inlineBypasses`：内联 ContentHtml 直通次数（无需走缓存路径），独立计数保持 `totalRequests = cacheHits + cacheMisses + inlineBypasses` 恒等式
- `uniqueBodies`：缓存中不重复的 body 数量
- `amplification`：缓存放大比 = totalRequests / uniqueBodies（体现缓存复用的效果）
- `maxSize`：缓存容量上限（LRU 淘汰阈值）
- `currentSize`：当前缓存条目数

## Notion Statistics

When `maxRps` is active, a single summary line is output at the end of each content source:

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

## Build Reports

When `build.report.enabled: true` (or `--ci`), the engine writes structured build reports to `dist/.bukit/`:

- `build-report.json` — includes `schemaErrorCount` (content schema validation errors), page/route/asset counts, timing, and incremental stats.
- `publish-audit-report.json` — primary machine-readability and trust audit covering semantic HTML, provenance, review status, representation coverage, and aggregate output consistency.
- `seo-report.json` — compatibility SEO audit checks per route.
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
