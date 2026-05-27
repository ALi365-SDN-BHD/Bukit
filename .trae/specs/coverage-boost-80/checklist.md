# Checklist

## Step 1: New test project
- [x] `tests/Bukit.Engine.Abstractions.Tests/` project created and added to slnx
- [x] `JsonElementMaterializerTests.cs` — 20 tests covering all value kinds (string, bool, null, int, double, array, object, nested, empty)
- [x] `NullContentBodyStoreTests.cs` — 4 tests: singleton, content path, no-content path, cancellation
- [x] `ProcessPluginHostTests.cs` — 5 tests: handshake, after-build, empty stdin, invalid JSON, unsupported hook

## Step 2: CLI tests
- [x] `PngImageReaderTests.cs` — 3 tests: valid PNG, non-PNG, missing file
- [x] `SeoReportValidatorTests.cs` — 8 tests: valid report, missing fields, ReadRequired* helpers, snapshot
- [x] `CloneCommandTests` extended — 2 tests: identical and different `ComparePngScreenshots`

## Verification
- [x] `dotnet build` passes with 0 warnings
- [x] All tests pass (28 Abstractions + 13 CLI new = 41 new, 2402 total)
- [x] Overall coverage: 73.45% (was 71.94%, +1.51%)
- [x] Bukit.Engine.Abstractions: 83.4% (was 50.9%, +32.5%) ✅

## Coverage Gains

| Project | Before | After | Δ |
|---------|--------|-------|---|
| Bukit.Engine.Abstractions | 50.9% | **83.4%** | +32.5% |
| bukit (CLI) | 64.2% | 64.2% | — |
| **Overall** | 71.94% | **73.45%** | +1.5% |
