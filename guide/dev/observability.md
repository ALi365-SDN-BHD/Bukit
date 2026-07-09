# Observability

Bukit emits structured information through logs, metrics, reports, and audit
artifacts.

## Logs

`ConsoleLogger` supports `debug`, `info`, `warn`, and `error`. CI mode lowers
normal output by using warning-oriented behavior in command paths.

## Metrics

`--metrics` writes JSON stage timing. Variant stage metrics include content
preparation, route generation, taxonomy setup, derive pages, rendering, asset
sync, plugin stages, and total variant time.

## Reports

Use `.bukit/build-report.json` for build health, `.bukit/seo-report.json` for
SEO, `.bukit/publish-audit-report.json` for release readiness, and
`.bukit/security-report.json` for output safety.
