# Tasks

## Step 1: New test project

- [ ] Task 1: Create `tests/Bukit.Engine.Abstractions.Tests/` project
  - [ ] SubTask 1.1: Create csproj with xunit, coverlet.collector, project reference to `Bukit.Engine.Abstractions`
  - [ ] SubTask 1.2: Add project to `bukit.slnx`

- [ ] Task 2: Write `JsonElementMaterializerTests.cs`
  - [ ] SubTask 2.1: Test `MaterializeElement` — string, true, false, null, integer, double, array, object, unknown
  - [ ] SubTask 2.2: Test `MaterializeNumber` — TryGetInt64 path, double fallback
  - [ ] SubTask 2.3: Test `MaterializeArray` — nested arrays, empty, mixed types
  - [ ] SubTask 2.4: Test `MaterializeObject` — simple, nested, empty
  - [ ] SubTask 2.5: Test `Materialize(dict)` — JsonElement in values, IReadOnlyList<JsonElement>, no-changes fast path

- [ ] Task 3: Write `NullContentBodyStoreTests.cs`
  - [ ] SubTask 3.1: Test `Instance` singleton identity
  - [ ] SubTask 3.2: Test `GetAsync` with `ContentHtml` set
  - [ ] SubTask 3.3: Test `GetAsync` without `ContentHtml` throws
  - [ ] SubTask 3.4: Test `GetAsync` with cancelled token

- [ ] Task 4: Write `ProcessPluginHostTests.cs`
  - [ ] SubTask 4.1: Create `TestablePluginHost` concrete subclass
  - [ ] SubTask 4.2: Test `RunAsync` with handshake hook
  - [ ] SubTask 4.3: Test `RunAsync` with after-build hook
  - [ ] SubTask 4.4: Test `RunAsync` with empty stdin
  - [ ] SubTask 4.5: Test `WriteResponse` and `WriteError`
  - [ ] SubTask 4.6: Test `MaterializeRoutedPagesMeta`

## Step 2: CLI test additions

- [ ] Task 5: Write `PngImageReaderTests.cs`
  - [ ] SubTask 5.1: Test `Read` with valid PNG (write minimal PNG bytes to temp file)
  - [ ] SubTask 5.2: Test `Read` with non-PNG file
  - [ ] SubTask 5.3: Test `Read` with missing file

- [ ] Task 6: Write `SeoReportValidatorTests.cs`
  - [ ] SubTask 6.1: Test `ValidateReportContract` with valid report JSON
  - [ ] SubTask 6.2: Test `ValidateReportContract` rejects missing schema
  - [ ] SubTask 6.3: Test `ReadRequiredInt` / `ReadRequiredString` / `ReadRequiredBool`
  - [ ] SubTask 6.4: Test `SeoReportSnapshot.From` with valid route/issue data

- [ ] Task 7: Add `CloneVerifier` tests to `CloneCommandTests.cs`
  - [ ] SubTask 7.1: Test `ComparePngScreenshots` with identical PNG images
  - [ ] SubTask 7.2: Test `ComparePngScreenshots` with different PNG images

## Verification

- [ ] Task 8: Run full build, test suite, and coverage
  - [ ] Verify `dotnet build` passes
  - [ ] Verify all tests pass
  - [ ] Run coverage and verify > 78%

# Task Dependencies

- [Task 2], [Task 3], [Task 4] depend on [Task 1]
- [Task 5], [Task 6], [Task 7] are independent of all other tasks
- [Task 8] depends on all tasks
