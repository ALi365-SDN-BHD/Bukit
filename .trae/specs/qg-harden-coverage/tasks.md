# Tasks

## Part A: Quality Gate Alignment

- [ ] Task 1: Fix quality-gate.sh threshold
  - [ ] Change `COVERAGE_THRESHOLD:-71` → `COVERAGE_THRESHOLD:-80`

- [ ] Task 2: Clean oversized baseline
  - [ ] Strip verbose celebration comments, keep concise one-per-line format with justifications

## Part B: CLI Coverage Boost

- [ ] Task 3: Write `CloneFidelityGeneratorTests.cs`
  - [ ] Test `Generate` with a temp HTML directory containing minimal HTML files
  - [ ] Verify template count, partial count, asset count in result

- [ ] Task 4: Write `CloneModelsTests.cs`
  - [ ] Test `CloneTokens.FromJson` with valid tokens JSON
  - [ ] Test `ClonePageInfo.FromJson` parses title/summary/seo
  - [ ] Test `CloneLayoutInfo.FromJson` with nav links
  - [ ] Test `CloneBehaviors.FromJson` parses behavior flags

- [ ] Task 5: Write `CloneYamlWriterTests.cs`
  - [ ] Test `YamlScalar` escaping
  - [ ] Test `AppendBlockScalar` multi-line output
  - [ ] Test `EnsureSourcesConfig` creates valid site.yaml sources section

- [ ] Task 6: Add `BuildCommand` error path tests
  - [ ] Test missing config file
  - [ ] Test invalid build options

## Verification

- [ ] Task 7: Run full build, test, coverage
  - [ ] `dotnet build` passes with 0 warnings
  - [ ] All tests pass
  - [ ] Coverage improves toward 80%

# Task Dependencies

- [Task 1-2] depend on none
- [Task 3-6] are independent of each other
- [Task 7] depends on all tasks
