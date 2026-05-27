# Coverage Boost to 80% Spec

## Why

Current coverage is 71.94%. Target is 80%. The lowest-coverage projects are Bukit.Engine.Abstractions (50.9%) and Bukit.Cli (64.2%). Adding targeted tests to the biggest untested files will close the gap.

## What Changes

### Step 1: New test project `tests/Bukit.Engine.Abstractions.Tests/`

**BREAKING**: None — only adding tests.

Add targeted tests for the three biggest untested files:

1. **ProcessPluginHost tests** — 137 lines, currently 0% covered
   - Subclass `ProcessPluginHost` with a concrete implementation
   - Test `HandleHandshake` via `RunAsync` with handshake JSON
   - Test `HandleHookAsync` (after-build hook dispatch)
   - Test error paths (empty stdin, invalid JSON, unsupported hook)
   - Test `WriteResponse` / `WriteError` output
   - Test `MaterializeRoutedPagesMeta` (JsonElement materialization in routed pages)

2. **JsonElementMaterializer tests** — 102 lines, currently 32.56%
   - `MaterializeElement`: string, bool (true/false), null, number (int/double), array, object, unknown
   - `MaterializeNumber`: integer path (TryGetInt64), double path
   - `MaterializeArray`: nested arrays, empty array, mixed types
   - `MaterializeObject`: simple object, nested object, empty object
   - `Materialize(JsonElement/IReadOnlyList<IReadOnlyDictionary>)`: top-level dispatch paths

3. **NullContentBodyStore tests** — 22 lines, currently 42.86%
   - `Instance` singleton returns same instance
   - `GetAsync` with content → returns ContentBody
   - `GetAsync` without content → throws InvalidOperationException
   - `GetAsync` with cancelled token → throws OperationCanceledException

**New project structure**:
```
tests/Bukit.Engine.Abstractions.Tests/
├── Bukit.Engine.Abstractions.Tests.csproj
├── ProcessPluginHostTests.cs
├── JsonElementMaterializerTests.cs
└── NullContentBodyStoreTests.cs
```

**New csproj must reference**:
- `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`
- `Bukit.Engine.Abstractions` project

### Step 2: Add CLI tests for uncovered files

Add tests to existing `tests/Bukit.Cli.Tests/`:

4. **PngImageReader tests** — pure logic, no test file exists
   - `Read` valid PNG (generated in test)
   - `Read` invalid file / non-PNG
   - Extract width/height from valid PNG

5. **SeoReportValidator tests** — JSON schema validator, no test file
   - `ValidateReportContract` with valid full report
   - `ValidateReportContract` with missing required fields
   - `ReadRequiredInt` / `ReadRequiredString` / `ReadRequiredBool` boundary cases
   - Schema type validation for WebSite, SearchAction, Article

6. **CloneCommand additional tests** — 39.76% currently
   - Clone verify/report methods (the ones in CloneVerifier that are easy to unit test)
   - `ComparePngScreenshots` with test PNG data

## Impact

- Affected: Bukit.Engine.Abstractions (50.9% → targeted 85%+), Bukit.Cli (64.2% → targeted 70%+)
- Overall coverage: 71.94% → targeted 78-80%
- All changes are test-only, zero risk to production code

## ADDED Requirements

### Requirement: Abstractions test project
The system SHALL have `tests/Bukit.Engine.Abstractions.Tests/` with tests for ProcessPluginHost, JsonElementMaterializer, and NullContentBodyStore.

### Requirement: PngImageReader tests
The system SHALL have unit tests for PNG binary parsing in `tests/Bukit.Cli.Tests/PngImageReaderTests.cs`.

### Requirement: SeoReportValidator tests
The system SHALL have unit tests for SEO report schema validation in `tests/Bukit.Cli.Tests/SeoReportValidatorTests.cs`.

### Requirement: Clone additional tests
The system SHALL have additional unit tests for `CloneVerifier.ComparePngScreenshots` in existing `CloneCommandTests.cs`.
