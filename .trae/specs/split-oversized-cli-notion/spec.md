# Split Oversized CLI Specs and Notion Converter Spec

## Why
Two `.cs` files exceed the 600-line cohesion limit and are NOT in `scripts/.oversized-baseline.txt`, causing `scripts/quality-gate.sh` to fail:
- `src/Bukit.Cli/Cli/BukitCliSpecs.cs` (763 lines)
- `src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs` (657 lines)

## What Changes
- Extract theme spec fields from `BukitCliSpecs.cs` into `BukitCliThemeSpecs.cs`
- Extract domain record types from `HtmlToNotionBlockConverter.cs` into `NotionBlockTypes.cs`
- Extract HTML tokenizer from `HtmlToNotionBlockConverter.cs` into `HtmlTokenizer.cs`
- All new classes are `internal` and live in the same namespace/directory as the originals
- Pure refactoring — no behavior change, no API surface change

## Impact
- Affected specs: none (refactor only)
- Affected code: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`, `src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs`
- New files: `src/Bukit.Cli/Cli/BukitCliThemeSpecs.cs`, `src/Bukit.Shared/Notion/NotionBlockTypes.cs`, `src/Bukit.Shared/Notion/HtmlTokenizer.cs`
- Test impact: none (public API unchanged)

## ADDED Requirements

### Requirement: Theme Spec Extraction
The system SHALL provide a separate `BukitCliThemeSpecs` static class containing the 13 theme-related `CliCommandSpec` fields extracted from `BukitCliSpecs`.

#### Scenario: Theme specs accessible after extraction
- **WHEN** `BukitCliSpecs.CreateRegistry()` or `BukitCliSpecs.CreateDescriptors()` references a theme spec (e.g., `ThemeCreateSpec`)
- **THEN** the reference resolves through `BukitCliThemeSpecs.ThemeCreateSpec` transparently
- **AND** the `BukitCliThemeSpecs` fields remain `internal static readonly`

#### Scenario: Theme spec extraction verified
- **WHEN** `BukitCliSpecs.cs` is measured after the extraction
- **THEN** it drops below 600 lines

### Requirement: Domain Record Type Extraction
The system SHALL provide a separate `NotionBlockTypes.cs` file containing the `NotionBlock` abstract record and its 9 sealed record subtypes (`Heading1Block`, `Heading2Block`, `Heading3Block`, `ParagraphBlock`, `BulletedListItemBlock`, `NumberedListItemBlock`, `QuoteBlock`, `ImageBlock`, `ToggleBlock`), plus the `RichTextSegment` record.

#### Scenario: Block types accessible after extraction
- **WHEN** `HtmlToNotionBlockConverter` references `NotionBlock`, `ParagraphBlock`, etc.
- **THEN** the types resolve from `NotionBlockTypes.cs` in the same namespace
- **AND** the types remain `public` (they are part of the public return type)

### Requirement: HTML Tokenizer Extraction
The system SHALL provide a separate `HtmlTokenizer` static class containing the `HtmlTokenType` enum, `HtmlToken` class, `Tokenize()` method, and `ExtractTagName()` method extracted from `HtmlToNotionBlockConverter`.

#### Scenario: Tokenizer used after extraction
- **WHEN** `HtmlToNotionBlockConverter.ParseBlocks()` invokes `Tokenize(html)`
- **THEN** the call resolves through `HtmlTokenizer.Tokenize(html)`
- **AND** `HtmlTokenType`, `HtmlToken`, and `ExtractTagName()` are accessed through `HtmlTokenizer`

#### Scenario: Both extractions verified
- **WHEN** `HtmlToNotionBlockConverter.cs` is measured after both extractions
- **THEN** it drops below 600 lines

## REMOVED Requirements
None (pure refactoring).
