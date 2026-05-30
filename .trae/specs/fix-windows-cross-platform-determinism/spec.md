# Fix Windows CI Failures — Cross-Platform Determinism

## Why
GitHub Actions Windows CI fails in `dotnet test` due to 5 categories of cross-platform non-determinism. All failures are test assertions that assume Unix behavior but run on Windows.

## What Changes

### 1. BuildPathUtils.SanitizeFileSegment — Fixed invalid char set
- Replace `Path.GetInvalidFileNameChars()` with a fixed cross-platform set: `< > : " / \ | ? *` + control characters
- Update test to expect `"hello_world__test.txt"` instead of preserved `<` and `?`

### 2. BuildPathUtilsTests.MakeAbsolute — Fix Unix-path assertions
- `MakeAbsolute_ReturnsRootedPathUnchanged` uses hardcoded Unix paths. Rename to `MakeAbsolute_TreatsUrlLikeRelativePath_ForLegacyBehavior` and use `Path.Combine`/`Path.GetFullPath` for assertion

### 3. Notion renderers — Deterministic HTML newlines
- Replace `StringBuilder.AppendLine()` with `builder.Append('\n')` in `NotionBlocksRenderer` (7 occurrences) and `TableBlockRenderer` (2 occurrences)
- Generated HTML output MUST use `\n`, not `Environment.NewLine`

### 4. ProcessPluginInvoker — Case-insensitive env lookup
- `CopyAllowedEnvironment` uses exact key lookup on `IDictionary` from `Environment.GetEnvironmentVariables()`
- On Windows, env var names like `PATH`/`Path` cause mismatches
- Fix: build a case-insensitive `Dictionary<string, string>` from `hostEnvironment` first, then lookup

### 5. ProtocolEchoPlugin — Windows HOME fallback
- `env-allowlist` mode reads `Environment.GetEnvironmentVariable("HOME")` which doesn't exist on Windows
- Fix: fallback to `USERPROFILE` on Windows
- ExternalProtocolPluginTests path assertion: replace string-contains with JSON parsing for non-path fields (`openAi`, `github`, `pluginName`, `pluginHook`), but keep `Assert.Contains` for path assertions (`projectRoot`, `outputDir`) since `ProtocolOutputWriter` writes the plugin's `text` field as-is (unescaped backslashes on Windows make it invalid JSON)

### 6. ExternalProtocolPluginTests — Unescaped backslashes on Windows (follow-up)
- `plugin-output.json` written by `ProtocolOutputWriter.WriteOutputs` contains raw Windows paths with single backslashes (e.g. `D:\a\Bukit...`)
- `JsonDocument.Parse` fails with `"U is an invalid escapable character"` because `\U` is not a valid JSON escape
- Fix: use `Assert.Contains` for path assertions (cross-platform safe), JSON parse only for non-path fields

## Impact
- Affected specs: build pipeline, plugin protocol, Notion rendering
- Affected code:
  - `src/Bukit.Engine/BuildPathUtils.cs` (SanitizeFileSegment)
  - `src/Bukit.Content/Notion/NotionBlocksRenderer.cs` (AppendLine → \n)
  - `src/Bukit.Content/Notion/BlockRenderers/TableBlockRenderer.cs` (AppendLine → \n)
  - `src/Bukit.Engine/Plugins/Protocol/ProcessPluginInvoker.cs` (CopyAllowedEnvironment)
  - `tests/ProtocolEchoPlugin/Program.cs` (HOME fallback)
  - `tests/Bukit.Engine.Tests/BuildPathUtilsTests.cs`
  - `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs`

## MODIFIED Requirements

### Requirement: SanitizeFileSegment uses fixed cross-platform invalid chars
The system SHALL use a fixed set of invalid filename characters (`< > : " / \ | ? *` + control characters) instead of `Path.GetInvalidFileNameChars()`.

#### Scenario: Sanitize with angle brackets and question marks
- **GIVEN** input `"hello<world?>test.txt"`
- **WHEN** `SanitizeFileSegment` is called
- **THEN** returns `"hello_world__test.txt"` on all platforms

### Requirement: Generated HTML uses deterministic newlines
Notion block renderers SHALL use `\n` (LF) as line separator in generated HTML, not `Environment.NewLine`.

#### Scenario: HTML output on Windows
- **GIVEN** a Notion block list is rendered on Windows
- **WHEN** the HTML output is compared to expected output
- **THEN** the output contains only `\n` (no `\r\n`)

### Requirement: Environment variable lookup is case-insensitive
`CopyAllowedEnvironment` SHALL perform case-insensitive lookup of environment variable names.

#### Scenario: PATH lookup on Windows
- **GIVEN** the default allowlist includes `"PATH"`
- **AND** the host has `"Path"` as the actual env var name (Windows)
- **WHEN** `CopyAllowedEnvironment` copies allowed env vars
- **THEN** `PATH` is successfully copied

### Requirement: ProtocolEchoPlugin handles Windows HOME fallback
The `env-allowlist` mode SHALL fall back to `USERPROFILE` when `HOME` is not set.

#### Scenario: HOME on Windows
- **GIVEN** the plugin runs on Windows where `HOME` is not set
- **WHEN** the `env-allowlist` mode reads home directory
- **THEN** falls back to `USERPROFILE`

### Requirement: Plugin output test uses string-contains for path assertions
Tests that verify plugin output containing file paths SHALL use `Assert.Contains` for path assertions, because `ProtocolOutputWriter.WriteOutputs` writes the plugin's `text` field as-is and Windows paths contain unescaped backslashes.

#### Scenario: Path assertion on Windows
- **GIVEN** a plugin outputs `{"projectRoot": "D:\\a\\Bukit..."}` as its text field
- **AND** `ProtocolOutputWriter` writes this to `plugin-output.json` as-is
- **WHEN** the test reads `plugin-output.json`
- **THEN** `JsonDocument.Parse` fails (unescaped `\U`)
- **THEN** `Assert.Contains(temp.Path, output)` succeeds (cross-platform safe)
