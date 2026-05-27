# Split 3 Oversized Content Files Spec

## Why

The quality gate `.oversized-baseline.txt` lists 15 remaining files. This spec targets the 3 Bukit.Content files: `MarkdownFolderProvider.cs` (~622 lines), `NotionContentProvider.cs` (~1466 lines), `ImageAssetLocalizer.cs` (~690 lines).

## What Changes

### 1. MarkdownFolderProvider.cs (→ <600 lines)

Extract helper methods into internal classes:
- **MarkdownFrontMatterParser** — `TryExtractFrontMatter`, `ParseFrontMatter`, `NormalizeTaxonomy`, `ToObject`
- **MarkdownFieldBuilder** — `BuildFields`, `IsReservedMetaKey`, `TryConvertToField`, `TryConvertToList`, `TryParseDateTimeOffset`
- **MarkdownTextHelper** — `ExtractSummaryFromMarkdown`, `ExtractSummaryFromHtml`, `StripHtmlToText`, `CollapseWhitespace`, `TruncateAtWordBoundary`, `ExtractTitle`

Keep in `MarkdownFolderProvider`: `LoadAsync`, `ComputeBodyFingerprint`, `BuildGlobRegex`, `IsAutoSummaryEnabled`, `GetAutoSummaryMaxLength`, `RenderHtmlFromFileAsync`, plus the `MarkdownFolderProviderOptions` record.

### 2. NotionContentProvider.cs (→ <600 lines)

Extract helper methods into internal classes:
- **NotionPropertyParser** — `ExtractFields`, `TryParseNotionPropertyToField`, `ExtractPlainTextArray`, `TryParseRollupToField`, `TryParseFormulaToField`, `ExtractTitle`, `ExtractTitleProperty`, `ExtractSlug`, `ExtractType`, `ExtractPublishAt`, `ReadDateProperty`, `BuildUniqueIdString`, `ExtractUserNameOrId`, `IsReservedNotionField`, `NormalizeFieldKey`
- **NotionCacheManager** — `CreatePageHtmlCache`, `GetOrRenderPageHtmlAsync`, `NormalizeCacheMode`, `PageHtmlCache` record
- **NotionMetaHelper** — `PromoteFieldToMeta`, `PromoteTaxonomyFieldToMeta`, `NormalizePolicyMode`, `BuildAllowedSet`, `InjectPageCoverAndIcon`, `ExtractPageFileUrl`, `ExtractPageIconUrl`
- **NotionRelationResolver** — `ResolveMissingTaxonomyRelationTargetsAsync`

Keep in `NotionContentProvider`: `LoadAsync`, `Slugify`, `ExtractPlainText`, `GetString`, `TryGetPropertyIgnoreCase`, `TryParseDateTimeOffset`, `EnsureNoCaseInsensitiveConflicts`, `PageDraft` record.

### 3. ImageAssetLocalizer.cs (→ <600 lines)

Extract helper methods into internal classes:
- **MediaIndexManager** — `IsSafeFileName`, `TryGetUrlFromIndex`, `RememberIndex`, `EnsureIndexLoaded`, `PersistIndex`, `FindExistingFileByHash`
- **SsrfGuard** — `SsrfSafeConnectAsync`, `IsPrivateHostAsync`, `IsPrivateAddress`

Keep in `ImageAssetLocalizer`: `LocalizeAsync`, `DownloadCoreAsync`, `RecordFailure`, `ReadWithLimitAsync`, `IsAllowedContentType`, `DelayBeforeRetryAsync`, `ResolveExtension`, `BuildStableFileName`, `BuildHashPrefix`, `NormalizeSourceUrlForKey`, `CombineUrl`, `Dispose`, constants.

## Impact

- Affected specs: Quality Gate (oversized baseline)
- Affected code: `src/Bukit.Content/Markdown/`, `src/Bukit.Content/Notion/`, `src/Bukit.Content/Media/`
- No public API changes — all extracted classes are `internal`

## ADDED Requirements

### Requirement: MarkdownFrontMatterParser
The system SHALL provide `MarkdownFrontMatterParser` with `TryExtractFrontMatter`, `ParseFrontMatter`, `NormalizeTaxonomy`, `ToObject`.

### Requirement: MarkdownFieldBuilder
The system SHALL provide `MarkdownFieldBuilder` with `BuildFields`, `IsReservedMetaKey`, `TryConvertToField`, `TryConvertToList`, `TryParseDateTimeOffset`.

### Requirement: MarkdownTextHelper
The system SHALL provide `MarkdownTextHelper` with summary extraction and text utility methods.

### Requirement: NotionPropertyParser
The system SHALL provide `NotionPropertyParser` with Notion property → ContentField parsing logic.

### Requirement: NotionCacheManager
The system SHALL provide `NotionCacheManager` with page HTML caching logic.

### Requirement: NotionMetaHelper
The system SHALL provide `NotionMetaHelper` with field promotion and page cover/icon injection.

### Requirement: NotionRelationResolver
The system SHALL provide `NotionRelationResolver` with relation target resolution.

### Requirement: MediaIndexManager
The system SHALL provide `MediaIndexManager` with on-disk `.media-index.json` management.

### Requirement: SsrfGuard
The system SHALL provide `SsrfGuard` with SSRF-safe HTTP connection logic.

## REMOVED Requirements
None — all changes are internal refactoring.

## Baseline Removal
After each file is split below 600 lines, remove its entry from `scripts/.oversized-baseline.txt`.
