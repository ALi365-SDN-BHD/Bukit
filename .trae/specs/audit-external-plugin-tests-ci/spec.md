# Audit External Plugin Usage in Engine Tests for CI Compatibility

## Why
`quality-gate.sh` fails when `dotnet test` hits `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` because the test triggers `BuildPlanner.Plan()` which rejects external plugins in CI without `AllowExternalPlugins = true`.

## What Changes
- Fix already applied: `BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs` now passes `AllowExternalPlugins = true` in both `ConfigOverrides`
- Audit confirms no other Engine tests require changes

## Impact
- Affected specs: CI pipeline
- Affected code: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs` (already fixed)

## Audit Results

| Test File | Uses ExternalPlugins | Calls BuildAsync | Needs Fix |
|---|---|---|---|
| `SiteEngineIntegrationTests.cs` | ✅ (via `CreatePluginOutputConfig`) | ✅ | **Already fixed** |
| `ExternalProtocolPluginTests.cs` | ✅ | ❌ | Not affected |
| `ConfigValidatorTests.cs` | ✅ | ❌ | Not affected |
| `ConfigValidatorCapabilityTests.cs` | ✅ | ❌ | Not affected |

Non-BuildAsync tests (`ExternalProtocolPluginTests`, `ConfigValidatorTests`, `ConfigValidatorCapabilityTests`) test plugin config validation and protocol-level invocation directly, bypassing `BuildPlanner`. They are immune to the CI guard.

## MODIFIED Requirements
### Requirement: External plugin tests explicitly opt in via AllowExternalPlugins
Tests that use external protocol plugins and call `SiteEngine.BuildAsync()` SHALL pass `AllowExternalPlugins = true` in their `ConfigOverrides`.

#### Scenario: BuildAsync test with external plugins in CI
- **GIVEN** the test defines `ExternalPlugins` in its config
- **AND** calls `SiteEngine.BuildAsync()`
- **AND** the test runs in a CI environment
- **WHEN** `ConfigOverrides` includes `AllowExternalPlugins = true`
- **THEN** the build proceeds without `ConfigException`
