# P1 Canonical Content Completion Tasks

## Phase 0: Spec And Baseline

- [x] Create `.trae/specs/p1-canonical-content-completion/spec.md`.
- [x] Create `.trae/specs/p1-canonical-content-completion/tasks.md`.
- [x] Create `.trae/specs/p1-canonical-content-completion/checklist.md`.
- [x] Run baseline scan for direct `Meta` reads.

## Phase 1: Canonical Access Helpers

- [x] Add a failing xUnit test proving structured field values beat legacy meta
      for canonical summary, taxonomy, provenance, trust, and entity fields.
- [x] Add the minimal pure helper or builder change needed to pass.
- [x] Run the targeted engine test filter.

## Phase 2: Main Consumers Canonical-First

- [x] Add a failing taxonomy test for canonical tags/categories sourced from
      structured fields.
- [x] Add a failing related-content test for canonical tags/categories/type.
- [x] Add a failing feed or LLMS test for canonical summary/source metadata.
- [x] Implement the smallest consumer changes needed to pass each test.

## Phase 3: Provider Mapping Contract

- [x] Add or strengthen Notion provider tests for auto-summary field/meta
      synchronization.
- [x] Verify Markdown/Notion field precedence remains structured-first.

## Phase 4: Canonical Validation And Reporting

- [x] Add canonical validation tests for media alt gaps.
- [x] Integrate media alt diagnostics into the existing pipeline error flow.
- [x] Add canonical validation tests for empty relations and missing provenance
      or trust gaps.

## Quality Gate Blockers

- [x] Split `src/Bukit.Cli/Cli/BukitCliSpecs.cs` below 600 lines.
- [x] Split `src/Bukit.Engine/SeoAuditReportWriter.cs` below 600 lines.
- [x] Stabilize local Release build by disabling Roslyn shared compilation.

## Final Verification

- [x] Run `dotnet build bukit.slnx -c Release -warnaserror`.
- [x] Run the four project test commands in Release.
- [x] Run `dotnet format bukit.slnx --verify-no-changes`.
- [ ] Run `bash scripts/quality-gate.sh`.

## Remaining Blocker

- [ ] Raise aggregate quality-gate line coverage from 69.26% to at least 80%
      with real tests. The gate now passes encoding, skills strict validation,
      build, full test execution, doc asset consistency, and smoke, then fails
      at the coverage threshold.
