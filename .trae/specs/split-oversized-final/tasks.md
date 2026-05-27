# Tasks

- [ ] Task 1: Extract SeoReportValidator from SeoCommand.cs
  - [ ] Create `src/Bukit.Cli/Commands/SeoReportValidator.cs`
  - [ ] Update SeoCommand.cs to delegate

- [ ] Task 2: Extract SeoExternalAuditor from SeoCommand.cs
  - [ ] Create `src/Bukit.Cli/Commands/SeoExternalAuditor.cs`
  - [ ] Update SeoCommand.cs to delegate
  - [ ] Verify SeoCommand.cs < 600 lines

- [ ] Task 3: Extract StarterThemeResources from StarterThemeScaffold.cs
  - [ ] Create `src/Bukit.Cli/Commands/StarterThemeResources.cs`
  - [ ] Update StarterThemeScaffold.cs to reference new class
  - [ ] Verify StarterThemeScaffold.cs < 600 lines

- [ ] Task 4: Extract DoctorMarkdownChecker from DoctorCommand.cs
  - [ ] Create `src/Bukit.Cli/Commands/DoctorMarkdownChecker.cs`
  - [ ] Update DoctorCommand.cs to delegate

- [ ] Task 5: Extract DoctorTemplateChecker from DoctorCommand.cs
  - [ ] Create `src/Bukit.Cli/Commands/DoctorTemplateChecker.cs`
  - [ ] Update DoctorCommand.cs to delegate
  - [ ] Verify DoctorCommand.cs < 600 lines

- [ ] Task 6: Extract ThemeInfoPrinter from ThemeCommand.cs
  - [ ] Create `src/Bukit.Cli/Commands/ThemeInfoPrinter.cs`
  - [ ] Update ThemeCommand.cs to delegate
  - [ ] Verify ThemeCommand.cs < 600 lines

- [ ] Task 7: Run full build/test + update baseline + clear baseline

# Task Dependencies

All tasks are independent. Only Task 7 depends on all others.
