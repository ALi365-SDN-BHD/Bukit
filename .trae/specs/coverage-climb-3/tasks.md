# Tasks

- [ ] Task 1: Write `CloneResearchWriterTests.cs`
  - [ ] Test `WriteTo` creates research output files in temp directory
  - [ ] Test generates section spec files

- [ ] Task 2: Write `CloneContentWriterTests.cs` (additional)
  - [ ] Test `GenerateIndexContent` output with brand/summary
  - [ ] Test `GenerateSectionData` with buttons, items, images
  - [ ] Test `BuildAssetMap` maps URLs correctly
  - [ ] Test `AssetFileName` / `AssetSubdir` / `LocalAssetPath`
  - [ ] Test `SanitizeFileName` / `SanitizeSlug`

- [ ] Task 3: Write `CloneVerifierTests.cs` (additional)
  - [ ] Test `CompareScreenshotFiles` finds matching pairs
  - [ ] Test `FindMissingScreenshotPairs` detects missing viewports
  - [ ] Test `ExtractViewportName` from screenshot filenames
  - [ ] Test `SectionLabel` fallback logic

- [ ] Task 4: Write `ThemeTemplateResourceTests.cs`
  - [ ] Test `Get` returns template string for valid keys
  - [ ] Test `Get` throws for invalid key
  - [ ] Test `ApplyColorOverrides` replaces primary/accent colors
  - [ ] Test `ProcessPlaceholders` substitutes brand and colors

- [ ] Task 5: Write `CloneStyleSheetGeneratorTests.cs`
  - [ ] Test `GenerateStyleCss` returns CSS containing :root variables
  - [ ] Test `C` helper returns fallback when value is null/empty
  - [ ] Test `C` helper returns trimmed value
  - [ ] Test CSS includes custom font family when specified
  - [ ] Test CSS includes responsive breakpoints

- [ ] Task 6: Write `DoctorMarkdownCheckerTests.cs`
  - [ ] Test `CheckMarkdownFrontMatter` with valid front matter → no warnings
  - [ ] Test `CheckMarkdownFrontMatter` with malformed → warning
  - [ ] Test `CheckMarkdownSyntax` detects unclosed code blocks
  - [ ] Test `CheckMarkdownSyntax` detects empty links
  - [ ] Test `CheckMarkdownEmptyBody` detects files with only front matter

- [ ] Task 7: Verify build and coverage
  - [ ] Build passes with 0 warnings
  - [ ] All tests pass
  - [ ] Coverage improves

# Task Dependencies

All tasks are independent. Only Task 7 depends on all others.
