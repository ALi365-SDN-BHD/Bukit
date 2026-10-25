# Fix Core report contracts and release checksum inventory

## Authorization and context

The user explicitly approved this independent Core task for SR-001, SR-002 and
SR-003, followed by the directly affected SSR-001 test-helper repair. The design
below is approved; this is separate from the phase-0 website/test infrastructure
task and preserves its existing work.

The retained phase-0 Engine run (`engine-final.trx`) has 36 passing and four
failing tests. All four real-site scenarios expose the undeclared privacy check
and stale release-state checksum; the empty site also exposes unknown-date
schema mismatches. The specialty review separately traces Windows audit paths
to slash-keyed snapshot lookup failures.

## Changes

- Require `checks.publicOutputPrivacy` using the existing security-check schema.
- Permit `lastModified: null` for unknown dates in SEO and publish audit reports;
  retain date-time validation for strings.
- Exclude only root `.bukit-build-state.json` from the release inventory in
  addition to the existing report-directory exclusion. Preserve nested files
  with that name and all listed-file lengths, hashes and aggregate semantics.
- Normalize backslashes at the three audit/semantic-hash test-helper lookups.

## Scope and non-goals

Only the 16 paths mapped by the approved Core verification closure may change.
No transaction/report reordering, producer date fabrication, schema-version
bump, relaxed unknown-property validation, new dependencies, historical-area
changes, full gates, or remote runs belong to this repair. The missing
agent-manifest schema and PluginHost optional-field schema gap remain inventory
context outside this task.

## Acceptance

Add direct owner regressions before the fix and retain expected RED evidence.
Run only the four exact specialties in external `scope.json`: Engine report
owners plus infrastructure, CLI SEO report validators, report-inventory
self-test, and platform-wiring self-test. Preserve input identities, logs/TRX,
timings and GREEN cache evidence for the controller's scheduled scoped review.
