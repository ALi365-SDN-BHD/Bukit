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

- [x] Task 6: Fix ExternalProtocolPluginTests — JSON-based path assertions
  - [x] `ExternalProtocolPlugin_AfterBuild_DefaultEnvironmentDoesNotExposeHostSecrets`: replace `JsonEncodedPath` + `Assert.Contains` with `JsonDocument.Parse` + property equality checks

- [x] Task 7: Verify on macOS (all tests pass, no regressions)
  - [x] `dotnet test bukit.slnx -c Release` — all tests pass (Engine: 1106, Content: 649, CLI: 774, etc.)
  - [x] `bash scripts/smoke.sh Release` — **Smoke OK**

# Task Dependencies
- Tasks 1–6 are independent and can run in parallel
- Task 7 depends on Tasks 1–6
