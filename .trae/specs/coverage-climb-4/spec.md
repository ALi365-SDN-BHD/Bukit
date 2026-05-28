# Coverage Climb Batch 4 Spec

## Why

CLI coverage at ~47%, main blockers are CloneCommand (39% covered, 543 lines) and BuildCommand (54%, 99 lines). Add targeted tests for the largest uncovered code paths.

## What Changes

Add tests to `tests/Bukit.Cli.Tests/`:

1. **CloneCommand additional tests** — 39% covered, 543 lines
   - `RunCoreAsync` error paths: missing `--tokens`, invalid theme name, directory exists without `--force`, missing files
   - `VerifyCloneAsync` verify paths
   - `ParseVisualThreshold` / `CountBehaviors`

2. **BuildCommand additional tests** — 54% covered, 99 lines
   - Missing config file error
   - Output directory creation
   - Clean flag behavior

3. **CloneContentWriter additional tests** — 81% covered, 591 lines
   - `GenerateSectionData` full output verification
   - `GenerateIndexContent` edge cases
   - `NormalizeSections` / `PartialFor`

## Impact

- CLI coverage: ~47% → targeted 52-55%

## ADDED Requirements

### Requirement: CloneCommand Error Path Tests
### Requirement: BuildCommand Extended Tests
### Requirement: CloneContentWriter Extended Tests
