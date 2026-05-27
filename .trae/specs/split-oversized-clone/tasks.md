# Tasks

- [ ] Task 1: Extract CloneYamlWriter from CloneContentWriter.cs
  - [ ] SubTask 1.1: Create `src/Bukit.Cli/Commands/CloneYamlWriter.cs` with YAML utility methods
  - [ ] SubTask 1.2: Update CloneContentWriter.cs to delegate
  - [ ] SubTask 1.3: Verify CloneContentWriter.cs < 600 lines

- [ ] Task 2: Extract CloneStyleSheetGenerator from CloneThemeGenerator.cs
  - [ ] SubTask 2.1: Create `src/Bukit.Cli/Commands/CloneStyleSheetGenerator.cs` with `GenerateStyleCss`, `C`, `AddVar`, `Esc`
  - [ ] SubTask 2.2: Update CloneThemeGenerator.cs to delegate

- [ ] Task 3: Extract CloneBehaviorGenerator from CloneThemeGenerator.cs
  - [ ] SubTask 3.1: Create `src/Bukit.Cli/Commands/CloneBehaviorGenerator.cs` with `GenerateBehaviorCss`, `GenerateBehaviorsJs`, `CountBehaviors`
  - [ ] SubTask 3.2: Update CloneThemeGenerator.cs to delegate
  - [ ] SubTask 3.3: Verify CloneThemeGenerator.cs < 600 lines

- [ ] Task 4: Extract CloneVerifier from CloneCommand.cs
  - [ ] SubTask 4.1: Create `src/Bukit.Cli/Commands/CloneVerifier.cs` with all verify/report/visual comparison methods and associated records
  - [ ] SubTask 4.2: Update CloneCommand.cs to delegate

- [ ] Task 5: Extract PngImageReader from CloneCommand.cs
  - [ ] SubTask 5.1: Create `src/Bukit.Cli/Commands/PngImageReader.cs` with `PngImage` record, `Read`, `Unfilter`, `Paeth`
  - [ ] SubTask 5.2: Update CloneCommand.cs to delegate
  - [ ] SubTask 5.3: Verify CloneCommand.cs < 600 lines

- [ ] Task 6: Run full build and test suite + update baseline
  - [ ] SubTask 6.1: Build and test
  - [ ] SubTask 6.2: Remove 3 files from baseline

# Task Dependencies

- [Task 1] depends on none
- [Task 2], [Task 3] depend on none (same source file, independent methods)
- [Task 4], [Task 5] depend on none (same source file, independent methods)
- [Task 6] depends on [Tasks 1-5]
