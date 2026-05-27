# Split 4 Remaining Oversized Files Spec

## Why

The final 4 files in the oversized baseline. This is the last batch.

## What Changes

### 1. SeoCommand.cs → Extract validators

- **SeoReportValidator.cs**: `ValidateReportContract` + all JSON helpers (`ReadString`, `ReadRequiredObject`, `ReadRequiredArray`, `ReadRequiredString`, `ReadOptionalString`, `ReadRequiredInt`, `ReadOptionalInt`, `ReadRequiredBool`, `ReadOptionalBool`, `EnsureObject`, `EnsureAllowedProperties`, `ReadOptionalInt(string)`, `SplitCsv`, `IsHttpUrl`), `SeoReportSnapshot`, `SeoRouteSnapshot`, `SeoIssueSnapshot`
- **SeoExternalAuditor.cs**: `RunExternalAuditAsync`, `CheckUrlAsync`, `AnalyzeExternalResponse`, `ExtractImageUrls`, `ExtractLinks`, plus generated regexes
- Keep: `RunAsync`, `Audit`, `AuditAsync`, `Diff`, constants

### 2. StarterThemeScaffold.cs → Extract resource constants

- **StarterThemeResources.cs**: all `internal const string` template constants (StyleCss, BaseLayout, SeoPartial, etc.) — move ~600 lines of string constants out
- Keep: `WriteTo`, `ApplyColorOverrides`, `WriteFile`

### 3. DoctorCommand.cs → Extract markdown + template checkers

- **DoctorMarkdownChecker.cs**: `CheckMarkdownFrontMatter`, `CheckMarkdownSyntax`, `CheckMarkdownEmptyBody`
- **DoctorTemplateChecker.cs**: `CheckHardcodedUrls`, `CheckHardcodedText`, `RemoveScribanBlocks`, `RemoveHtmlComments`, `RemoveTagContent`, `ExtractHtmlText`
- Keep: `RunAsync`, `CheckManifestCompleteness`, `AnalyzeTemplateChains`, `CheckTemplateVariables`, `CheckThemeParamsConsistency`, `CheckUnreferencedTemplates`, `CheckSchemaFieldCompleteness`, `CheckTemplateFieldsVsSchema`, `CheckExtraContentFields`, `CheckNotionAsync`, `DoctorContext` record, `WarnHeuristicFallback`, `ExtractDirectives`

### 4. ThemeCommand.cs → Extract info display

- **ThemeInfoPrinter.cs**: `PrintSections`, `PrintComponents`, `PrintTokens`, `PrintLayouts`, `PrintFileStats`
- Keep: everything else (orchestration, theme CRUD, config management)

## Impact

- All new classes are `internal`
- No public API changes

## Baseline Removal

After each file is split below 600 lines, remove all 4 from the baseline — clearing it.
