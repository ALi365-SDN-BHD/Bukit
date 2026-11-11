# Observability

Bukit emits structured information through logs, metrics, reports, and audit
artifacts.

## Logs

`ConsoleLogger` supports `debug`, `info`, `warn`, and `error`. CI mode lowers
normal output by using warning-oriented behavior in command paths.

Custom `ILogger` implementations must be thread-safe: build workers may call
any log level concurrently, and ordering between workers is not guaranteed.
`ConsoleLogger` writes complete events under concurrent calls. The Engine's
atomic diagnostic counters do not serialize callbacks into a custom logger;
protect mutable collections in that logger itself.

## Metrics

`--metrics` writes JSON stage timing. Variant stage metrics include content
preparation, route generation, taxonomy setup, derive pages, rendering, asset
sync, plugin stages, and total variant time.

Render metrics come from `PageRenderDispatcher`:

- `pageRender`, `listBuild`, `staticRender`: time per content, list, and
  static page, from after the body is loaded to after the file is written.
- `renderCall` (template renderer call), `htmlTransform` (HTML transforms),
  `renderWrite` (output file write): the parts of those three, for all kinds.
- `renderModelBind`: template model conversion inside `renderCall`. Only the
  built-in Scriban renderer reports it.
- `bodyLoad`, `listBodyLoad`, `metadataHash` (incremental metadata hash per
  content page) and `listHash` (incremental hash per special list): further
  per-page values inside the render dispatch.
- `renderDispatch`: wall-clock time of the whole render dispatch.
- `variantSetup` (media normalization and theme loading), `mediaResolve`
  (only with media localization), `renderPlan` (renderer, site model,
  manifest and asset plans), `assetPrepare` and `seoPrepare`: wall-clock
  time of the variant steps before rendering that have no other stage.

Per-page values are summed across parallel workers, so they can exceed
`renderDispatch`; compare `renderDispatch`, not the per-page sums, with
`variantTotal`. The wall-clock stages, without the per-page sums, add up to
`variantTotal`. Per-page durations are summed in `Stopwatch` ticks and
converted to milliseconds once, so sub-millisecond pages are not lost.

Report-stage steps run after rendering and are not in `metrics.json`. Each one
logs an info line `event=build.report.timing scope=<variant|finalize> step=<name> duration_ms=<n>`,
in execution order, and each scope ends with `step=total`, the wall-clock time
of that scope:

- Variant scope (`language=<code>` in multi-language builds): `projections`,
  `media-validate` (only with media localization; it rescans every rendered
  HTML file), `output-record`, `seo-audit`, `analytics-report`. `seo-audit`
  and `analytics-report` are logged even when reports are disabled: the audit
  always runs, and `analytics-report` then removes a stale report file.
- Single-language finalize: `metrics-write` (only with `--metrics`),
  `collect-and-clean`, `output-inventory`, `security-data`,
  `security-report`, one `writer:<type name>` per report writer that runs,
  `security-gate`.
- Multi-language finalize: `collect-and-clean`, `root-outputs`,
  `seo-audit-merged`, `metrics-write` (only with `--metrics`),
  `output-inventory`, `security-data`, `security-report`, the writer lines,
  `security-gate`. `seo-audit-merged` also logs the diagnostics the language
  variants hand back: their audit issues and the merged
  `incremental.signed_links` warning.

A step that throws is not logged, and neither is its scope's `total`.

## Reports

Use `.bukit/build-report.json` for build health, `.bukit/seo-report.json` for
SEO, `.bukit/publish-audit-report.json` for release readiness, and
`.bukit/security-report.json` for output safety.

In the build report, the `warningCount` and `errorCount` fields in the `summary` object count the
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

The count snapshot is taken before build-report writing. Diagnostics produced
by report writing itself are not retroactively included. A cancelled or failed
build is not guaranteed to leave a complete report.

See [Core Safety And Reliability Invariants](core-safety-reliability-invariants.md)
for the logger, inventory, symlink, and schema boundaries that maintainers must
preserve.
