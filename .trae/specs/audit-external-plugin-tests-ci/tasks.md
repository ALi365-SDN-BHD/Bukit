# Tasks

- [x] Task 1: Fix `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` to pass `AllowExternalPlugins = true`
  - [x] Already completed in `fix-incremental-build-test-ci` spec

- [x] Task 2: Audit all Engine tests for external plugin usage
  - [x] Search `tests/Bukit.Engine.Tests` for `ExternalPlugins` — found 4 files with usage
  - [x] Search `tests/Bukit.Engine.Tests` for `.BuildAsync(` — only `SiteEngineIntegrationTests.cs`
  - [x] Verify `ExternalProtocolPluginTests.cs` does not call `BuildAsync` → confirmed, tests plugin protocol directly
  - [x] Verify `ConfigValidatorTests.cs`/`ConfigValidatorCapabilityTests.cs` do not call `BuildAsync` → confirmed, tests validation only
  - [x] Conclusion: only `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` needed fixing, already done

- [x] Task 3: Verify quality-gate passes
  - [x] Run `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release` — **1106 passed, 0 failed**
  - [x] Verify `BuildPlanner.cs` CI guard remains unchanged
  - [x] `bash scripts/quality-gate.sh Release` — all tests pass, 0 failures

# Task Dependencies
- Task 3 depends on Task 2
