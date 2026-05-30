# Fix CI Failure in BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs

## Why
The test `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` uses external protocol plugins but calls `SiteEngine.BuildAsync` with default `ConfigOverrides` that don't set `AllowExternalPlugins = true`. In CI, `BuildPlanner` detects the `CI` environment variable and rejects external plugins, causing the test to fail with `ConfigException`.

## What Changes
- Update `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` test: add `AllowExternalPlugins = true` to both `ConfigOverrides` instances

## Impact
- Affected specs: test suite, CI
- Affected code: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs` lines 1403, 1409

## MODIFIED Requirements
### Requirement: External plugin tests explicitly opt in via AllowExternalPlugins
Tests that use external protocol plugins SHALL pass `AllowExternalPlugins = true` in their `ConfigOverrides` to bypass the CI safety guard.

#### Scenario: Incremental build test in CI
- **GIVEN** the test uses `CreatePluginOutputConfig` which defines an external protocol plugin
- **AND** the test runs in a CI environment
- **WHEN** `SiteEngine.BuildAsync` is called with `ConfigOverrides { AllowExternalPlugins = true }`
- **THEN** the build proceeds without throwing `ConfigException`

#### Scenario: Incremental build test locally
- **GIVEN** the test uses `CreatePluginOutputConfig` which defines an external protocol plugin
- **AND** the test runs locally (no CI environment)
- **WHEN** `SiteEngine.BuildAsync` is called with `ConfigOverrides { AllowExternalPlugins = true }`
- **THEN** the build proceeds normally
