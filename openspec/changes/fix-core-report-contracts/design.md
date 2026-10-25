# Approved design

## SR-001: security-check schema parity

`BuildReporterSecurity` always emits `checks.publicOutputPrivacy`, including
`not_checked` and `not_applicable` outcomes. Declare it with `#/$defs/check` and
require it alongside the four existing checks. Unknown check names and unknown
fields within a check remain forbidden. Owner tests validate real serialized
output and reject missing privacy data, invalid check values and unknown fields.

## SR-002: unknown modification dates

`PublishDocumentBuilder.NormalizeLastModified` intentionally maps Unix epoch to
null. SEO serialization retains null and `PublishAuditBuilder` copies it into
the publish document. Both schemas will use `type: [string, null]` while keeping
`format: date-time` and required `lastModified`. The producer and existing CLI
nullable-string readers retain their behavior. Writer regressions cover real
unknown/known dates and reject malformed strings, wrong types and missing
fields; CLI owner tests confirm nullable-string compatibility.

## SR-003: release inventory boundary

Reporting precedes `BuildRecoveryTracker.MarkCompleted` and transaction commit.
The root state file is mutable recovery metadata, so hashing its intermediate
bytes cannot describe committed content. Filter that exact root-relative name
using existing normalized-path conventions before hashing. Do not filter by
basename: `nested/.bukit-build-state.json` is still content. Preserve existing
report-directory exclusion, symlink safety, sort order, report writer order and
transaction completion. An owner regression rewrites the real root state after
reporting and verifies retained file bytes, sizes, count and aggregate hash.

## SSR-001: portable test comparisons

Merged audit output paths can contain Windows backslashes; raw snapshot keys
use `/`. Replace backslashes at the SEO existence, publish existence and publish
semantic-HTML lookup sites. A focused test supplies Windows-form output paths
against slash-keyed files and exercises both audits and semantic hashing on any
host. Update the release helper's expected set only with the actual inventory
fix.

## Verification and handoff

Engine runs fixture-exclusive; CLI runs dotnet-serial; only the two disjoint
Python owner checks may overlap. Freeze relevant project/reference/source/test,
helper/config/fixture/schema inputs and the machine-readable closure. Record
exact commands, HEAD, environment states only, fresh SDK/runtime/platform,
timings, RED/GREEN logs and cache records externally. Leave the sole-writer queue
in `review_wait`; the controller owns the scheduled review and later phase-0
platform acceptance. OpenSpec sync/archive and unrelated inventory gaps are
outside this change.
