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

In the build report, `summary.warningCount` and `summary.errorCount` count the
`Warn` and `Error` diagnostic events emitted during that build. Counts are
isolated per build and aggregate concurrent language variants. They do not copy
the warning or error totals from the SEO, publish, or security reports, whose
issue definitions remain independent.

`generatedFiles` is the stable, root-relative public output inventory captured
before the build report is written. Paths use `/` separators. The inventory
does not follow symbolic links and excludes `.bukit/` report directories at any
level, `.bukit-build-state.json`, and `.bukit-output-marker`. Internal report
integrity remains owned by `.bukit/artifact-manifest.json`; internal reports
and marker files are not duplicated into `generatedFiles`.
