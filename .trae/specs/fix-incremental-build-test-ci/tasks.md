# Tasks

- [x] Task 1: Fix `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` test to pass `AllowExternalPlugins = true`
  - [x] Line 1403: change `new ConfigOverrides()` to `new ConfigOverrides { AllowExternalPlugins = true }`
  - [x] Line 1409: change `new ConfigOverrides { Clean = false }` to `new ConfigOverrides { Clean = false, AllowExternalPlugins = true }`

- [x] Task 2: Verify the fix
  - [x] Run `dotnet test bukit.slnx -c Release --filter "BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs"` locally — **1 passed, 0 failed**
  - [x] Confirm the CI external plugin guard in `BuildPlanner.cs` remains unchanged

# Task Dependencies
- Task 2 depends on Task 1
