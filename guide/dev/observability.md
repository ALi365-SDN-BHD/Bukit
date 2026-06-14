# Observability

Core observability is built around structured logs, diagnostic codes, metrics,
and report files.

Source anchors:

- `src/Bukit.Shared/Logger.cs`
- `src/Bukit.Shared/DiagnosticCode.cs`
- `src/Bukit.Engine/MetricsWriter.cs`
- `src/Bukit.Engine/BuildReporter.cs`

## Logging

```yaml
logging:
  level: info
```

Supported levels are `debug`, `info`, `warn`, and `error`.

CLI flags:

```bash
bukit build --log-format text
bukit build --log-format json
bukit build --ci
```

Use JSON logs in CI when downstream tooling parses failures.

## Metrics

```bash
bukit build --metrics .cache/build-metrics.json
```

Metrics are useful for:

- incremental render reasons;
- stage timing;
- output directories and language variants;
- plugin and projection behavior.

## Reports

| Report | Path |
|---|---|
| Build report | `dist/.bukit/build-report.json` |
| Security report | `dist/.bukit/security-report.json` |
| SEO report | `dist/.bukit/seo-report.json` |
| GEO report | `dist/.bukit/geo-report.json` |
| Publish audit report | `dist/.bukit/publish-audit-report.json` |

## Diagnostic Workflow

1. Use `config check` for config-shaped failures.
2. Use `doctor` for route, provider, and template diagnostics.
3. Use `build --metrics` for timing and incremental behavior.
4. Use `seo audit`, `geo audit`, and `publish audit` for output quality gates.
5. If logs and reports disagree, treat report generation and CLI validators as
   separate proof surfaces.

