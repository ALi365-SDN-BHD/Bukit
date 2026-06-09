# Split Oversized BuildReporter & De-Duplicate NotionPropertyParser Spec

## Why

Quality gate `scripts/quality-gate.sh` fails because two `.cs` files exceed the 600-line cohesion limit and are not in `scripts/.oversized-baseline.txt`:

- `src/Bukit.Content/Notion/NotionPropertyParser.cs` (880 lines)
- `src/Bukit.Engine/BuildReporter.cs` (813 lines)

These were previously split but regressed above the limit.

## What Changes

### 1. NotionPropertyParser.cs (880 → <200 lines)

`NotionPropertyParser.cs` contains duplicated copies of six methods that already live in `NotionPropertyTypeParser.cs`:
- `TryParseNotionPropertyToField` (public, lines 377–681, ~305 lines) — identical to `NotionPropertyTypeParser.TryParseNotionPropertyToField`
- `ExtractPlainTextArray` (lines 683–712)
- `TryParseRollupToField` (lines 714–765)
- `TryParseFormulaToField` (lines 767–819)
- `BuildUniqueIdString` (lines 821–853)
- `ExtractUserNameOrId` (lines 855–878)

These duplicated methods have **zero external callers** — they are dead code. All internal callers already use `NotionPropertyTypeParser` directly (see `ExtractFields` at line 92).

**Action**: Remove lines 377–878 (the six duplicated methods). Keep only the public API methods:
- `ExtractFields` (3 overloads)
- `ExtractAllFields`
- `ExtractTitle`, `ExtractTitleProperty`
- `ExtractSlug`, `ExtractType`, `ExtractPublishAt`, `ReadDateProperty`
- `IsReservedNotionField`, `NormalizeFieldKey`
- `GetRichTextPlain`, `ProjectSeoFields`

This brings `NotionPropertyParser.cs` from 880 lines to approximately 375 lines.

### 2. BuildReporter.cs (813 → ~500 lines)

Extract security-related methods into a new `BuildReporterSecurity.cs`:
- `EnforceSecurityGate`
- `WriteSecurityReport`, `WriteSecurityCheck`, `WriteExternalPluginGovernance`
- `CreateSecurityReportData`
- `ResolveSecurityStatus`, `IsFailed`, `IsWarning`
- `ResolveSecurityFailMode`, `IsStrictSecurityContext`, `IsReleaseProfileContext`
- `CheckRoutes`, `CheckSlugs`, `CheckPluginOutputs`, `CheckRemoteThemeLock`
- `LooksRemoteThemeSource`

Keep in `BuildReporter.cs`: `WriteIfEnabled`, all JSON writer methods (`WriteBuildReport`, `WriteRoutes`, `WriteAssets`, etc.), helpers (`ComputeSha256`, `ComputeBundleHash`, `NormalizePath`, `WriteArtifactContract`, `WriteStringArray`, `EnumerateRoutes`, `EnumerateAssets`, `BuildRouteEntries`, `BuildPluginRouteUrl`, `GetSource`, `GetKind`, `IsUnderReportDirectory`), and records.

## Impact

- Affected specs: Quality Gate (oversized baseline)
- Affected code: `src/Bukit.Content/Notion/NotionPropertyParser.cs`, `src/Bukit.Engine/BuildReporter.cs`
- New file: `src/Bukit.Engine/BuildReporterSecurity.cs` (~320 lines, `internal static`)
- No public API changes
- No baseline entries added — files are split below 600

## REMOVED Requirements

### Requirement: Duplicated methods in NotionPropertyParser
**Reason**: `TryParseNotionPropertyToField`, `ExtractPlainTextArray`, `TryParseRollupToField`, `TryParseFormulaToField`, `BuildUniqueIdString`, `ExtractUserNameOrId` are dead duplicated code — zero external callers; all consumers use `NotionPropertyTypeParser` directly.
**Migration**: None needed — no external callers.

## ADDED Requirements

### Requirement: BuildReporterSecurity
The system SHALL provide `BuildReporterSecurity` as an `internal static` class containing all security-check and security-report-writing logic extracted from `BuildReporter`.
