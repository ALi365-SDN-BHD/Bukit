# Tasks

- [ ] Task 1: Extend `BuildCommandTests.cs`
  - [ ] Test missing config file returns exit code 2
  - [ ] Test with valid config calls build engine

- [ ] Task 2: Extend `CloneCommandTests.cs`
  - [ ] Test `RunCoreAsync` with missing `--tokens` returns 2
  - [ ] Test `RunCoreAsync` with invalid theme name returns 2
  - [ ] Test `RunCoreAsync` with existing theme and no `--force`
  - [ ] Test `ParseVisualThreshold` valid/invalid/boundary
  - [ ] Test `CountBehaviors` with various behaviors

- [ ] Task 3: Extend `CloneContentWriterTests.cs`
  - [ ] Test `NormalizeSection` with custom order/semantic
  - [ ] Test `PartialFor` for all partial types

- [ ] Task 4: Verify build and test pass

# Task Dependencies

All independent. Task 4 depends on 1-3.
