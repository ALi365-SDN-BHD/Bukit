# Tasks
- [x] Task 1: Fix ThemePack_PacksTheme to use absolute output path
  - [x] Read the test `ThemePack_PacksTheme` at `tests/Bukit.Cli.Tests/ThemeCommandExtendedTests.cs` line 622-643
  - [x] Modify the test to pass `--output` with an absolute path (e.g., `Path.Combine(_rootDir, "packable-1.0.0.tar.gz")`) to `ThemePackCommand.RunAsync`
  - [x] Update the `outputFile` variable to use the same absolute path for the `File.Exists` assertion
  - [x] Update `TestCleanup.DeleteFile` to use the same absolute path

- [x] Task 2: Verify test passes
  - [x] Run `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~ThemePack_PacksTheme"` — passed

# Task Dependencies
- Task 2 depends on Task 1
