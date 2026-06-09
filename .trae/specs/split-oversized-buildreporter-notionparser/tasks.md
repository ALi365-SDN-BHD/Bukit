# Tasks

- [x] Task 1: Remove duplicated methods from NotionPropertyParser.cs
  - Remove `TryParseNotionPropertyToField` (public duplicate, lines 377–681)
  - Remove `ExtractPlainTextArray` (duplicate, lines 683–712)
  - Remove `TryParseRollupToField` (duplicate, lines 714–765)
  - Remove `TryParseFormulaToField` (duplicate, lines 767–819)
  - Remove `BuildUniqueIdString` (duplicate, lines 821–853)
  - Remove `ExtractUserNameOrId` (duplicate, lines 855–878)
  - Verify `NotionPropertyParser.cs` < 600 lines

- [x] Task 2: Extract BuildReporterSecurity from BuildReporter.cs
  - Create `src/Bukit.Engine/BuildReporterSecurity.cs` with all security-related methods
  - Update `BuildReporter.cs` to delegate to `BuildReporterSecurity`
  - Verify `BuildReporter.cs` < 600 lines

- [x] Task 3: Build, test, and verify quality gate
  - Run `dotnet build bukit.slnx -c Release -warnaserror`
  - Run full test suite
  - Verify both files below 600 lines
  - Verify no new oversized files introduced

# Task Summary

- **NotionPropertyParser.cs**: 880 → 376 lines (duplicated methods removed, public API preserved)
- **BuildReporter.cs**: 813 → 495 lines (security methods extracted to BuildReporterSecurity.cs)
- **BuildReporterSecurity.cs**: 340 lines (new file, `internal static`)
- All tests: 1863 passed, 0 failed
- Quality gate: passes oversized file check

# Task Dependencies

- [Task 1] and [Task 2] are independent — can run in parallel
- [Task 3] depends on [Task 1] and [Task 2]
