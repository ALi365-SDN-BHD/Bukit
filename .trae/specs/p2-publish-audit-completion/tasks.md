# P2 Publish Audit Completion Tasks

## Phase 0: Spec And Plan

- [x] Create `docs/superpowers/plans/2026-06-05-p2-publish-audit-completion.md`.
- [x] Create `.trae/specs/p2-publish-audit-completion/spec.md`.
- [x] Create `.trae/specs/p2-publish-audit-completion/tasks.md`.
- [x] Create `.trae/specs/p2-publish-audit-completion/checklist.md`.

## Phase 1: Publish Report Contract

- [x] Add failing engine tests for a distinct publish report JSON shape.
- [x] Verify targeted engine tests fail for the expected reason.
- [x] Add `PublishAudit*` models and JSON source-generation context.
- [x] Add `PublishAuditReportWriter`.
- [x] Update `SeoAuditReportWriter` to call the publish writer.
- [x] Verify targeted engine tests pass.

## Phase 2: Publish Audit Rules

- [x] Add failing engine tests for JSON-LD mismatch, output presence, and AI
      crawler policy conflict publish checks.
- [x] Extract semantic HTML rules.
- [x] Extract trust rules.
- [x] Extract representation rules.
- [x] Extract SEO compatibility publish rules.
- [x] Verify targeted engine tests pass.

## Phase 3: Publish CLI

- [x] Add failing CLI tests for publish default report resolution.
- [x] Add failing CLI tests for SEO default resolution preferring SEO report.
- [x] Implement independent `PublishCommand`.
- [x] Verify targeted CLI tests pass.

## Phase 4: Docs And Skills

- [x] Add `docs/schemas/publish-audit-report.v1.schema.json`.
- [x] Add `docs/publish-audit-report-schema.md`.
- [x] Update user CLI docs in English, Chinese, and Malay.
- [x] Update user SEO/GEO docs in English, Chinese, and Malay.
- [x] Update developer output and GEO docs.
- [x] Update Bukit CLI/SEO/GEO skills.

## Phase 5: Verification

- [x] Run targeted engine tests.
- [x] Run targeted CLI tests.
- [x] Run SiteEngine integration filter.
- [ ] Run `bash scripts/quality-gate.sh`.
- [x] Run `dotnet format bukit.slnx --verify-no-changes`.

## Phase 6: Follow-Up Completion

- [x] Add failing CLI coverage for `bukit geo audit` reading publish report
      `documents[].schemaTypes`.
- [x] Fix GEO audit document enumeration for publish-first reports.
- [x] Add `PublishAuditBuilder` so publish report conversion lives outside the
      writer serialization layer.
- [x] Add failing logging coverage for `publish.audit` and `geo.audit` issue
      prefixes.
- [x] Add expanded publish rule coverage for JSON-LD description/author/date,
      header/nav/footer landmarks, figure captions, RSS route presence, wildcard
      AI crawler blocking, specific allow overrides, and robots group edges.
- [x] Implement expanded publish rules without adding dependencies or entering
      P3 projection registry scope.
- [x] Run follow-up targeted tests, SiteEngine integration, Release build, and
      format verification.
- [x] Keep `bash scripts/quality-gate.sh` out of the follow-up completion gate
      because the known aggregate coverage blocker remains below 80%.

## Phase 7: Machine Readability & Trust Audit Closure

- [x] Add failing tests for JSON-LD multi-field mismatch reporting without title
      short-circuiting.
- [x] Add failing tests for machine-readability summary bucket coverage of new
      P2 issue codes.
- [x] Add failing tests for canonical content trust graph gaps: summary,
      updated-at metadata, source references, and entity summaries.
- [x] Add failing CLI coverage for publish-first GEO audit JSON error wording.
- [x] Implement the closure checks while keeping GEO as a derived audit view and
      keeping P3 projection registry out of scope.
- [x] Run closure targeted tests, SiteEngine integration, Release build, and
      format verification.

## Phase 8: Strict Architecture Closure

- [x] Add failing tests that `seo audit` no longer defaults to
      `.bukit/publish-audit-report.json` while explicit `--report` remains
      compatible.
- [x] Add failing tests for rich publish document facts: summary, updated-at,
      source references, entity summaries, semantic outline, and structured data
      types.
- [x] Add failing tests for JSON Feed and agent manifest route-set consistency.
- [x] Add failing tests for publish-first duplicate content and unique value
      hints.
- [x] Introduce a Machine Readability & Trust audit result/facade so publish
      report writing uses a first-class audit result instead of writer-local SEO
      conversion.
- [x] Update publish audit schema and docs for the richer document contract.
- [x] Run strict closure targeted tests, integration, Release build, and format
      verification.

## Remaining Blocker

- [ ] `bash scripts/quality-gate.sh` runs build, tests, docs, smoke, and coverage,
      then fails because aggregate line coverage is 69.83%, below the required
      80% threshold.
