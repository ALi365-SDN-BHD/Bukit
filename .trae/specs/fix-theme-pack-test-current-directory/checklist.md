# Checklist
- [x] `ThemePack_PacksTheme` passes `--output` with an absolute path to `ThemePackCommand.RunAsync`
- [x] `outputFile` variable uses the same absolute path for `File.Exists` assertion
- [x] `TestCleanup.DeleteFile` uses the same absolute path
- [x] `dotnet test --filter ThemePack_PacksTheme` passes locally
