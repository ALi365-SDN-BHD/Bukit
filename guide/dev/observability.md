# Observability (Logs and Metrics)

Implementation: `src/Bukit.Shared/Logger.cs`, `src/Bukit.Engine/MetricsWriter.cs`

## Logging

Default: <serilog-settings-style>
Default log format: plain text. CI mode recommended: `--log-format json`.
Log level: controlled by `logging.level` (default `info`).
Log sources: `Bukit.Engine`, `Bukit.Content`, `Bukit.Cli`.

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
