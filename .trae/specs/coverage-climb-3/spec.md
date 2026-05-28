# Coverage Climb Batch 3 Spec

## Why

Coverage at 74.31%, need to push toward 76-77%. CLI (65.7%) is the main bottleneck. This batch targets the largest untested CLI files.

## What Changes

Add tests to `tests/Bukit.Cli.Tests/` for these CLI source files:

1. **CloneResearchWriter tests** — 173 lines, 0 tests. Write research markdown files.
2. **CloneContentWriter HTML tests** — 591 lines, 81% covered. Test section body generation, CSS generation.
3. **CloneVerifier tests** — 526 lines, minimal coverage. Test screenshot comparison, verify report generation.
4. **ThemeTemplateResource tests** — 89 lines, 0 tests. Test `Get`, `ApplyColorOverrides`, `ProcessPlaceholders`.
5. **CloneStyleSheetGenerator tests** — 331 lines, 0 tests. Test `GenerateStyleCss` output, color variable substitution (C helper).
6. **DoctorMarkdownChecker tests** — 245 lines, 0 tests. Test front matter validation, syntax checking, empty body detection.

All tests go in `tests/Bukit.Cli.Tests/` with namespace `Bukit.Cli.Tests`.

## Impact

- CLI coverage: 65.7% → targeted 70-72%
- Overall coverage: 74.31% → targeted 76-77%

## ADDED Requirements

### Requirement: CloneResearchWriter Tests
### Requirement: CloneContentWriter Extended Tests
### Requirement: CloneVerifier Extended Tests
### Requirement: ThemeTemplateResource Tests
### Requirement: CloneStyleSheetGenerator Tests
### Requirement: DoctorMarkdownChecker Tests
