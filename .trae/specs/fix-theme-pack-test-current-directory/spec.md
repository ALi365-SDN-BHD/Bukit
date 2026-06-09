# Fix ThemePack Test Current Directory Race Spec

## Why
`ThemeCommandExtendedTests.ThemePack_PacksTheme` fails on CI (Windows) with `Assert.True() Failure` at line 641 because the tar.gz output file is not found. The root cause is a race condition: other test classes in the same DLL change `Directory.GetCurrentDirectory()` (via `CurrentDirectoryScope`), and xUnit runs test classes in parallel. `ThemePackCommand.RunAsync` writes the output relative to the process current directory, but the test checks for the file using `Directory.GetCurrentDirectory()` which may not match by the time the assertion runs.

## What Changes
- Modify `ThemePack_PacksTheme` to pass `--output` with an absolute path, eliminating dependency on the mutable process current directory

## Impact
- Affected specs: none
- Affected code: `tests/Bukit.Cli.Tests/ThemeCommandExtendedTests.cs` (line ~623-643)

## MODIFIED Requirements

### Requirement: ThemePack integration test is environment-robust
The `ThemePack_PacksTheme` test SHALL produce and verify the output archive using an absolute path, independent of the process current directory.

#### Scenario: Test runs correctly regardless of current directory
- **WHEN** `ThemePack_PacksTheme` executes in any environment (local, CI) with any test ordering/parallelism
- **THEN** the tar.gz output is written to and verified at a deterministic absolute path
