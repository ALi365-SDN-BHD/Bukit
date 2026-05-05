# Pemerhatian (Log dan Metrik)

Pelaksanaan: `src/Bukit.Shared/Logger.cs`, `src/Bukit.Engine/MetricsWriter.cs`

## Pengelogan
Format log lalai: teks biasa. Mod CI disyorkan: `--log-format json`.
Tahap log: dikawal oleh `logging.level` (lalai `info`).

## Metrik Binaan (`--metrics <path>`)
Menulis JSON metrik binaan untuk penjejakan prestasi CI:

```json
{
  "variants": [{
    "language": "zh-CN",
    "renderCount": 42,
    "skipCount": 18,
    "reasons": { "new_page": 5, "template_changed": 3, "content_changed": 15, "unchanged": 18 }
  }],
  "plugins": { "sitemap": { "durationMs": 120 } }
}
```

## Statistik Notion
Apabila `maxRps` aktif: `event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890`
