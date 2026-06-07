# P3 Publish Projections Completion Tasks

## Phase 0: Spec And Baseline

- [x] Create `.trae/specs/p3-publish-projections-completion/spec.md`.
- [x] Create `.trae/specs/p3-publish-projections-completion/tasks.md`.
- [x] Create `.trae/specs/p3-publish-projections-completion/checklist.md`.
- [x] Run a baseline scan for hard-coded representation lists and duplicate manifest writers.

## Phase 1: Projection Registry Contract

- [x] Add failing tests proving document representation kinds come from one registry.
- [x] Add failing tests proving aggregate representations are registered with existing output paths.
- [x] Implement `IPublishProjection`, `PublishProjectionContext`, `PublishProjectionResult`, and `PublishRepresentationRegistry`.
- [x] Decide external plugin ABI status: keep `IPublishProjection` internal because it depends on internal engine context; external process plugins continue to emit files through after-build protocol outputs.
- [x] Verify targeted projection tests pass.

## Phase 2: Projection Writer Refactor

- [x] Add failing integration coverage for manifest representation URLs resolving to real files.
- [x] Split JSON, Markdown, and agent manifest generation behind projection classes.
- [x] Replace hard-coded `html/json/markdown` lists in rendering and audit paths with registry calls.
- [x] Remove duplicate audit-owned agent manifest writing.
- [x] Verify targeted integration tests pass.

## Phase 3: Audit Inventory Verification

- [x] Add failing audit tests for missing `content/*.json` or `content/*.md`.
- [x] Add failing audit tests for manifest route-set consistency using projection output.
- [x] Implement file and manifest verification rules.
- [x] Verify targeted publish audit tests pass.

## Phase 3b: Full Multi-Projection Closure

- [x] Add failing tests proving built-in projection classes implement `IPublishProjection`.
- [x] Add failing tests proving document projection contracts write real files and return output inventory.
- [x] Add aggregate output adapters for feed, jsonfeed, sitemap, search, llms, robots, and agent manifest without rewriting their generators.
- [x] Add failing tests for structured `representations[]` publish audit inventory.
- [x] Add failing tests for JSON, Markdown, and agent manifest content mismatches.
- [x] Add failing tests proving expired content is excluded from sitemap, search, RSS, and agent manifest.
- [x] Add failing tests proving Atom is represented in registry, audit inventory, and noindex/expired aggregate filtering.
- [x] Implement representation inventory, projection consistency audit, and lifecycle-aware indexability.
- [x] Add route-level `llms`, `robots`, and `agent-manifest` representation inventory and audit tests.
- [x] Execute aggregate projection adapters through the projection pipeline and return per-route output results.
- [x] Generate merged i18n RSS, Atom, and JSON Feed from the same merged feed post set.
- [x] Add `publish.llms_missing_route` diagnostics and noindex/expired `llms.txt` exclusion coverage.
- [x] Promote `semantic-html` to a first-class document representation kind.
- [x] Generate agent manifest document representations from `PublishRepresentationRegistry`, including `semantic-html`.
- [x] Make aggregate projections call existing feed, sitemap, search, llms, and robots generators.
- [x] Pass projection results into publish audit before file-inspection fallback.
- [x] Move aggregate expected-kind mapping behind `PublishRepresentationRegistry`.
- [x] Generate i18n root `agent-manifest.json` from variant publish documents.
- [x] Return i18n root aggregate projection results and pass them into merged publish audit.
- [x] Remove built-in feed, sitemap, search, llms, and robots double-generation from the pre-audit plugin stage.
- [x] Add i18n root aggregate projection adapters implementing `IPublishProjection`.
- [x] Generate i18n root `llms.txt` and `robots.txt` through projection inventory.
- [x] Update dev docs and Bukit skills so aggregate outputs are documented as projection-owned rather than after-build-plugin-owned.

## Phase 4: Docs And Skills

- [x] Update user docs in English, Chinese, and Malay.
- [x] Update `guide/dev/engine-outputs.md`.
- [x] Update relevant Bukit skills.

## Phase 5: Verification

- [x] Run targeted engine projection tests.
- [x] Run targeted SiteEngine integration tests.
- [x] Run targeted publish CLI tests.
- [x] Run Release build.
- [x] Run Engine, Content, CLI, and Rendering tests.
- [x] Run format verification.
- [x] Run quality gate or record the known coverage blocker.

## Remaining Blocker

- [ ] `bash scripts/quality-gate.sh` runs skills validation, Release build,
      full test execution, doc/smoke checks, and coverage, then fails because
      aggregate line coverage is 69.25%, below the required 80% threshold.
