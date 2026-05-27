# Quality Gate Hardening + Coverage Boost Spec

## Why

CI sets `COVERAGE_THRESHOLD=80` but `quality-gate.sh` defaults to 71. The project rules already mandate 80%. The oversized baseline needs cleanup. And we need more CLI tests to reach 80% coverage (currently 73.45%).

## What Changes

### Part A: Quality Gate Alignment

1. **Fix quality-gate.sh** — change `COVERAGE_THRESHOLD:-71` to `COVERAGE_THRESHOLD:-80` to match CI and project rules
2. **Clean oversized baseline** — strip verbose celebration comments, keep concise format

### Part B: CLI Coverage Boost

Target the biggest CLI blind spots:

3. **CloneFidelityGenerator tests** — 0% covered, ~258 complexity. Test `Generate` method output.
4. **CloneModels tests** — no test file. Test `CloneTokens.FromJson`, `ClonePageInfo.FromJson`, `CloneLayoutInfo.FromJson`, `CloneBehaviors.FromJson`.
5. **CloneYamlWriter tests** — no test file. Test `YamlScalar`, `AppendBlockScalar`, `EnsureSourcesConfig`.
6. **BuildCommand additional tests** — 54.39% currently. Test error paths, CLI option validation.

## Impact

- quality-gate.sh now matches CI threshold (80%)
- Baseline format simplified
- CLI coverage: 64.2% → targeted 70%+
- Overall coverage: 73.45% → targeted 76%+

## MODIFIED Requirements

### Requirement: Quality Gate Threshold
quality-gate.sh SHALL use 80% as the default coverage threshold, matching the CI and project rules.

## ADDED Requirements

### Requirement: CloneFidelityGenerator Tests
The system SHALL have unit tests for `CloneFidelityGenerator.Generate` in `tests/Bukit.Cli.Tests/CloneFidelityGeneratorTests.cs`.

### Requirement: CloneModels Tests
The system SHALL have unit tests for Clone JSON models in `tests/Bukit.Cli.Tests/CloneModelsTests.cs`.

### Requirement: CloneYamlWriter Tests
The system SHALL have unit tests for `CloneYamlWriter` in `tests/Bukit.Cli.Tests/CloneYamlWriterTests.cs`.

### Requirement: BuildCommand Coverage
The system SHALL have additional tests for `BuildCommand` error paths.
