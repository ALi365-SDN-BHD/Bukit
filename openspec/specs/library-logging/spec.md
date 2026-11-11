# Caller-owned library logging

## Purpose

Route library diagnostics through the caller's logger, preserve command startup and global-format behavior, count existing build-interval events once and enforce the Core console-output boundary.

The 23-site inventory, twenty retained warning texts, three additive public members and 426-type/19-assembly evidence describe the original logger migration scope, not the entire current public surface or fresh closeout tests. Public compatibility below applies at that migration step; later accepted API changes, including the intentional Notion CLR migration, remain authoritative. Current accepted governance is 425 types / 18 assemblies after that migration and the later public-member update; this closeout does not recapture it or claim unknown private/binary/reflection consumers were tested or recompiled. Notion ownership follows `notion-compatibility-migration` in `Bukit.Content/Notion`, without restoring the removed `Bukit.Content.Notion` bridge.

## Requirements

### Requirement: Caller-owned library diagnostics

All 23 approved Core library warning sites SHALL call the supplied `ILogger.Warn`
with unprefixed payloads. Twenty prior warning texts SHALL retain exact text,
stderr, and newline behavior with the unchanged ConsoleLogger. Security and both
termination warnings SHALL gain the accepted text prefix. JSON and Error-level
filtering SHALL apply without changing validation, fallbacks, process cancellation,
timeouts, output bounds, captured protocol streams, or hash/event ordering.

#### Scenario: Public compatibility
- WHEN an existing public entry is called
- THEN its signature and behavior remain available with Warn/text defaults
- AND only the three approved logger-aware public members are added
- AND new logger parameters reject null.

### Requirement: One-pass command startup

Configured Core commands SHALL load once per invocation, including Deploy's build
and explicit/implicit Preview. A per-call ordered buffer SHALL replay once to the
final configured sink, with the existing CI Warn override. Load failure SHALL
replay with Warn/bootstrap and rethrow the same exception, retaining exception
precedence if output fails. Later validation SHALL not trigger failure replay.

#### Scenario: Startup filtering and failure
- WHEN a successful configuration selects Error
- THEN startup warnings are not displayed
- WHEN a late load failure follows an existing warning
- THEN that warning uses the selected format before the original error
- AND a later command invocation has no pending state from the first.

### Requirement: Explicit global format

Program SHALL select the raw global format before plugin discovery and explicitly
pass it through internal factories/closures. Non-Build normalization SHALL not lose
JSON. Old public defaults, Build's bound format, command stdout, error envelopes,
parser grammar, catches, and exit codes SHALL remain unchanged. Plugin discovery
SHALL use Warn/bootstrap without requiring site configuration or modifying protocol results.

#### Scenario: Raw JSON format reaches command startup
- **WHEN** a command starts with the raw global JSON format
- **THEN** Program selects it before plugin discovery and passes it through internal factories, retaining non-Build JSON and Build's bound format with the existing public defaults and protocol behavior.

### Requirement: Existing report interval

Each actual Warn/Error inside the existing build counter SHALL count once before
display filtering. Variants SHALL retain the supplied sink and shared counts.
Startup/pre-transaction events SHALL not be backfilled; security warnings SHALL
remain after report writing without rewriting it. Counts and derived report hashes
MAY change, but schema, public page bytes, transactions, and cache algorithms SHALL NOT.

#### Scenario: Build warnings are filtered after counting
- **WHEN** a variant emits an actual warning inside the existing build counter with an Error-level display filter
- **THEN** the supplied sink and shared count record it once before filtering, without backfilling startup/pre-transaction events or moving security warnings before report writing.

### Requirement: Architectural enforcement

A semantic source guard SHALL forbid direct Core library console output,
redirection, and colors, with only exact Cli/Cli.Shared/Shared Logger and generated
bin/obj exclusions. Its self-cases SHALL distinguish qualified/aliased/static calls
and real interpolation calls from comments, strings, and unrelated Console types.

#### Scenario: Semantic guard distinguishes output from lookalike text
- **WHEN** the guard analyzes qualified, aliased, static and interpolated Console output alongside comments, strings and unrelated Console types
- **THEN** it rejects real Core library output outside its exact exclusions without treating those lookalikes as output.

### Requirement: Report stage steps are timed in the build log

After rendering, the build MUST log one info line `event=build.report.timing scope=<variant|finalize> step=<name> duration_ms=<n>` for each report-stage step that actually executes, in execution order, and MUST end each scope with a `step=total` line whose duration is the wall-clock time of that scope.

- The variant scope MUST cover, in order, `projections`, `media-validate` (only when media localization is active), `output-record`, `seo-audit` and `analytics-report`, measured after the variant render timer stops; in multi-language builds each variant's lines MUST also carry `language=<code>`.
- The single-language finalize scope MUST cover, in order, `metrics-write` (only when metrics are requested), `collect-and-clean`, `output-inventory`, `security-data`, `security-report`, `writer:<writer type name>` for each report writer that runs, and `security-gate`.
- The multi-language finalize scope MUST cover, in order, `collect-and-clean`, `root-outputs`, `seo-audit-merged`, `metrics-write` (only when metrics are requested), `output-inventory`, `security-data`, `security-report`, `writer:<writer type name>` for each report writer that runs, and `security-gate`.
- A step whose call returns without doing any work because its feature is disabled MUST NOT emit a line; `security-report` is always written and MUST always emit a line. `seo-audit` always runs the audit, and `analytics-report` removes a stale report file when reports are disabled, which counts as work, so the variant scope MUST always emit both.
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
