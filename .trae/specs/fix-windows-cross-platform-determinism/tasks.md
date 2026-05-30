# Tasks

- [x] Task 1: Fix `BuildPathUtils.SanitizeFileSegment` — fixed cross-platform invalid chars
  - [x] Replace `Path.GetInvalidFileNameChars()` with fixed char set: `< > : " / \ | ? *` + control chars (c < 32)
  - [x] Update `SanitizeFileSegment_RemovesInvalidChars` test to expect `"hello_world__test.txt"`

- [x] Task 2: Fix `BuildPathUtilsTests.MakeAbsolute_ReturnsRootedPathUnchanged`
  - [x] Rename to `MakeAbsolute_TreatsUrlLikeRelativePath_ForLegacyBehavior`
  - [x] Use `Path.GetFullPath(Path.Combine(root, input))` for assertion instead of hardcoded Unix path

- [x] Task 3: Fix Notion renderers — deterministic `\n` newlines
  - [x] Replace all `StringBuilder.AppendLine(...)` with `builder.Append(...).Append('\n')` in `NotionBlocksRenderer.cs` (7 occurrences)
  - [x] Replace all `StringBuilder.AppendLine(...)` with `builder.Append(...).Append('\n')` in `TableBlockRenderer.cs` (2 occurrences)

- [x] Task 4: Fix ProcessPluginInvoker — case-insensitive env lookup
  - [x] Build a `Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)` from `hostEnvironment` first
  - [x] Look up using dictionary instead of direct `IDictionary` indexer

- [x] Task 5: Fix ProtocolEchoPlugin — Windows HOME fallback
  - [x] `env-allowlist` mode: fallback `HOME` to `USERPROFILE` when `HOME` is empty

- [x] Task 6: Fix ExternalProtocolPluginTests — JSON-based path assertions (initial attempt)
  - [x] `ExternalProtocolPlugin_AfterBuild_DefaultEnvironmentDoesNotExposeHostSecrets`: replace `JsonEncodedPath` + `Assert.Contains` with `JsonDocument.Parse` + property equality checks

- [x] Task 7: Verify on macOS (all tests pass, no regressions)
  - [x] `dotnet test bukit.slnx -c Release` — all tests pass (Engine: 1106, Content: 649, CLI: 774, etc.)
  - [x] `bash scripts/smoke.sh Release` — **Smoke OK**

- [x] Task 8: Fix ExternalProtocolPluginTests for Windows — unescaped backslashes in plugin output
  - **Root cause**: `ProtocolOutputWriter.WriteOutputs` writes the plugin's `text` field as-is. On Windows, the text contains paths with single backslashes (e.g. `D:\a\Bukit...`), so `JsonDocument.Parse` fails with "`\U` is invalid escapable character".
  - [x] Revert all assertions to `Assert.Contains` — the entire file is invalid JSON on Windows, so partial JSON parse is not viable

- [x] Task 9: Verify on macOS (no regressions from Task 8)
  - [x] `dotnet test tests/Bukit.Engine.Tests -c Release --no-build --filter "DefaultEnvironmentDoesNotExpose"` — **1 passed, 0 failed**

# Task Dependencies
- Tasks 1–6 are independent and can run in parallel
- Task 7 depends on Tasks 1–6
- Task 8 depends on Task 6 (reverts part of it)
- Task 9 depends on Task 8
