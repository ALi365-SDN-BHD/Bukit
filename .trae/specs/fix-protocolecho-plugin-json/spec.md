# Fix ProtocolEchoPlugin to Produce Valid JSON on Windows

## Why
`ExternalProtocolPlugin_AfterBuild_DefaultEnvironmentDoesNotExposeHostSecrets` fails on Windows CI with `JsonReaderException: 'U' is an invalid escapable character`. The root cause is NOT in the test — it's in the ProtocolEchoPlugin which produces a `text` field that is invalid JSON when the path contains Windows backslashes.

## Root Cause Analysis

ProtocolEchoPlugin line 45 builds the inner JSON with manual string interpolation:

```csharp
Console.Out.Write($$"""...\"projectRoot\":\"{{projectRoot.Replace("\\", "\\\\")}}\"...""");
```

The escaping chain on Windows (projectRoot = `D:\a\Bukit`):

| Stage | Value |
|---|---|
| C# `projectRoot` | `D:\a\Bukit` (single `\`) |
| `.Replace("\\", "\\\\")` | `D:\\a\\Bukit` (double `\` chars) |
| Raw string stdout | `D:\\a\\Bukit` (literal `\` `\` bytes) |
| JSON deserialize outer | `D:\a\Bukit` (single `\`) |
| `File.WriteAllText` | `D:\a\Bukit` (single `\`) |
| `JsonDocument.Parse` | **FAILS** — `\a` is invalid JSON escape |

The `text` field claims `"contentType":"application/json"` but the content is NOT valid JSON on Windows.

## What Changes
- **ProtocolEchoPlugin**: Replace manual string interpolation with `JsonSerializer.Serialize` to produce valid JSON in the `text` field
- **ExternalProtocolPluginTests**: Revert to `JsonDocument.Parse` + value equality assertions (now works on all platforms)

## Impact
- Affected code:
  - `tests/ProtocolEchoPlugin/Program.cs` (env mode, line 45)
  - `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs` (line 390-396)

## MODIFIED Requirements
### Requirement: Plugin text output is valid JSON on all platforms
The ProtocolEchoPlugin `text` field SHALL be valid JSON on all platforms, produced by `JsonSerializer.Serialize`.

#### Scenario: Windows path in text field
- **GIVEN** `BUKIT_PROJECT_ROOT = D:\a\Bukit`
- **WHEN** the plugin builds the `text` field
- **THEN** the text contains `"projectRoot":"D:\\a\\Bukit"` (properly JSON-escaped)
- **AND** the written file is valid JSON parsable by `JsonDocument.Parse`
