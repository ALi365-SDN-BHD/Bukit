# Tasks

- [x] Task 1: Extract theme spec fields from BukitCliSpecs.cs into BukitCliThemeSpecs.cs
  - [x] Create `src/Bukit.Cli/Cli/BukitCliThemeSpecs.cs` with the same `Bukit.Cli` namespace
  - [x] Move the 13 `internal static readonly CliCommandSpec` fields (`ThemeCreateSpec` through `ThemeExportCatalogSpec`, lines 9-133) into the new class
  - [x] Add `using` for `CliCommandSpec` if needed (same namespace, should resolve naturally)
  - [x] Update `BukitCliSpecs.cs`: replace each field reference (e.g., `ThemeCreateSpec`) with `BukitCliThemeSpecs.ThemeCreateSpec` in `CreateRegistry()` and `CreateDescriptors()`
  - [x] Verify `BukitCliSpecs.cs` is now fewer than 600 lines
  - [x] Additionally extracted `CreateDescriptors()` and `ResolveDescriptor()` into `BukitCliDescriptors.cs` to bring the file under 600 lines (now 580)

- [x] Task 2: Extract domain record types from HtmlToNotionBlockConverter.cs into NotionBlockTypes.cs
  - [x] Create `src/Bukit.Shared/Notion/NotionBlockTypes.cs` with the same `Bukit.Shared.Notion` namespace
  - [x] Move the following types (lines 1-34 of the original):
    - `NotionBlock` abstract record (line 6)
    - `Heading1Block` through `ToggleBlock` sealed records (lines 8-28)
    - `RichTextSegment` sealed record (lines 30-34)
  - [x] Remove these type declarations from `HtmlToNotionBlockConverter.cs`
  - [x] Ensure `HtmlToNotionBlockConverter.cs` compiles (same namespace, types resolve automatically)

- [x] Task 3: Extract HTML tokenizer from HtmlToNotionBlockConverter.cs into HtmlTokenizer.cs
  - [x] Create `src/Bukit.Shared/Notion/HtmlTokenizer.cs` with the same `Bukit.Shared.Notion` namespace
  - [x] Move the following from `HtmlToNotionBlockConverter`:
    - `HtmlTokenType` enum (lines 201-204)
    - `HtmlToken` class (lines 206-212)
    - `Tokenize()` method (lines 214-275)
    - `ExtractTagName()` method (lines 277-282)
  - [x] Update `HtmlToNotionBlockConverter.cs` to reference `HtmlTokenizer.Tokenize()`, `HtmlTokenizer.HtmlToken`, `HtmlTokenizer.HtmlTokenType`, `HtmlTokenizer.ExtractTagName()`
  - [x] Verify `HtmlToNotionBlockConverter.cs` is now fewer than 600 lines (now 532)

- [x] Task 4: Build, test, and verify quality gate
  - [x] Run `dotnet build bukit.slnx -c Release` to confirm compilation (succeeded)
  - [x] Run `dotnet test bukit.slnx -c Release` to confirm all tests pass (3292 passed, 0 failed)
  - [x] Run `bash scripts/quality-gate.sh Release` to confirm the size checks pass (no new oversized file errors)
  - [x] `StarterThemeResources.cs` remains in baseline (999 lines, pre-existing technical debt from prior split effort)

# Task Dependencies
- Task 2 and Task 3 are independent of each other (both touch the same file but different sections); run them sequentially or combine into one operation to avoid merge conflicts
- Task 2 + Task 3 must both complete before Task 4
- Task 1 is independent of Tasks 2-3 (different files in different directories)
