## ADDED Requirements

### Requirement: Report stage steps are timed in the build log

After rendering, the build MUST log one info line `event=build.report.timing scope=<variant|finalize> step=<name> duration_ms=<n>` for each report-stage step that actually executes, in execution order, and MUST end each scope with a `step=total` line whose duration is the wall-clock time of that scope.

- The variant scope MUST cover, in order, `projections`, `media-validate` (only when media localization is active), `output-record`, `seo-audit` and `analytics-report`, measured after the variant render timer stops; in multi-language builds each variant's lines MUST also carry `language=<code>`.
- The single-language finalize scope MUST cover, in order, `metrics-write` (only when metrics are requested), `collect-and-clean`, `output-inventory`, `security-data`, `security-report`, `writer:<writer type name>` for each report writer that runs, and `security-gate`.
- The multi-language finalize scope MUST cover, in order, `collect-and-clean`, `root-outputs`, `seo-audit-merged`, `metrics-write` (only when metrics are requested), `output-inventory`, `security-data`, `security-report`, `writer:<writer type name>` for each report writer that runs, and `security-gate`.
- A step whose call returns without doing any work because its feature is disabled MUST NOT emit a line; `security-report` is always written and MUST always emit a line.
- When a step throws, the exception MUST propagate unchanged; that step and the scope's `total` MUST NOT be logged.

Timing MUST NOT change any report file, the order in which steps run, their concurrency, error propagation, warning and error counts, or the structure of `metrics.json`.

#### Scenario: Single-language build
- **WHEN** a single-language site with reports enabled is built
- **THEN** the log contains, in order, one line for each executed variant step followed by a variant `step=total`, then one line for each executed finalize step and every enabled report writer followed by a finalize `step=total`

#### Scenario: Multi-language build
- **WHEN** a site with two languages and reports enabled is built
- **THEN** each language emits its variant-scope lines with `language=<code>` and its own variant `step=total`, and the finalize scope emits `collect-and-clean`, `root-outputs`, `seo-audit-merged`, `metrics-write` (only when metrics are requested), `output-inventory`, `security-data`, `security-report`, the writer lines and `security-gate` without `language=`, followed by one finalize `step=total`

#### Scenario: Reports disabled
- **WHEN** `build.report.enabled` is false
- **THEN** finalize lines appear for `security-report` and only for the writers that actually run

#### Scenario: Step sum matches total
- **WHEN** a scope completes
- **THEN** the sum of its step durations is within 10% of its `step=total`, or within 50 milliseconds for scopes shorter than 500 milliseconds

#### Scenario: Reports unchanged
- **WHEN** the same site is built before and after this change
- **THEN** every report file has the same content apart from fields that already vary per build, and warning and error counts are unchanged
