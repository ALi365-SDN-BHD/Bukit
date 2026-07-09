# Engine Outputs

Outputs are written by render, asset, plugin, projection, SEO, and build report
stages.

## Projection Contract

`PublishProjectionContract` defines per-page and aggregate representations:
HTML, semantic HTML, JSON, Markdown, JSON-LD, feed, Atom, JSON Feed, sitemap,
search, llms, robots, and agent manifest.

## Reports

| Report | Writer |
|---|---|
| Build report | `BuildReporter` |
| SEO report | `SeoAuditReportWriter` |
| Publish audit report | `PublishAuditReportWriter` |
| Security report | `BuildReporter` security report data helper |
| Metrics | `MetricsWriter` |

Reports are not optional prose. CLI audit commands consume them directly.
