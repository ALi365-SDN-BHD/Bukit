# Split 4 Oversized Engine Files Spec

## Why

12 files remain in the oversized baseline. This spec targets the 4 Bukit.Engine files: `SeoModelBuilder.cs` (~989), `SeoAuditReportWriter.cs` (~1189), `PagesIndexPlugin.cs` (~1069), `PageRenderDispatcher.cs` (~718).

## What Changes

### 1. SeoModelBuilder.cs → Extract SeoJsonLdBuilder + SeoGeoMetaParser

- **SeoJsonLdBuilder**: `BuildJsonLd`, `BuildArticleJsonLd`, `BuildFaqPageJsonLd`, `BuildHowToJsonLd`, `BuildPersonJsonLd`, `BuildCitationsJsonLd`, `BuildSpeakableJsonLd`, `BuildItemList`, `TryGetListField`, `TryReadListEntry`, `ReadMapString`, `ToJson`, `WriteJsonValue`
- **SeoGeoMetaParser**: `ParseGeoMeta`, `ReadGeoDateTime`, `ReadGeoString`, `ReadGeoStringList`, `ReadGeoFaqItems`, `ReadGeoHowToSteps`, `ReadGeoCitations`, `ReadGeoAuthor`, `ParsedGeoMeta` record
- Keep in SeoModelBuilder: `BuildForContent`, `BuildForList`, `BuildAbsoluteUrl`, `BuildAlternateKey`, `BuildListAlternateKey`, `IsIndexable`, plus helper accessors (`FirstTextOrMeta`, etc.), `GetStringList`, `BuildMaybeAbsoluteUrl`, `TryGetUpdateTime`, `NormalizeBaseUrl`, `ToTitle`

### 2. SeoAuditReportWriter.cs → Extract models + image reader + helpers

- **SeoAuditModels.cs**: Move all record types (`SeoAuditReportJsonContext`, `GeoReportJsonContext`, `GeoReport`, `GeoRouteEntry`, `SeoAuditReport`, `SeoAuditRoute`, `SeoAuditIssue`, `SeoAuditSummary`) and common constants
- **ImageMetadataReader.cs**: `TryReadImageMetadata`, `TryReadJpegMetadata`, `TryReadWebpMetadata`, binary helpers, `ImageMetadata` record
- **SeoSchemaValidator.cs**: `ExtractSchemaTypes` (both overloads), `ValidateSchemaObject`, `ValidateSchemaNode`, `ReadTypes`, `ValidateWebSite`, `ValidateSearchAction`, `ValidateArticle`, `ValidateItemList`, `HasNonEmptyString`, `HasAbsoluteUrl`, `IsSchemaType`, `IsEmptySchemaValue`
- Keep in SeoAuditReportWriter: `Write`, `WriteMerged`, `Build`, `WriteReport`, `WriteGeoReport`, `ComputeGeoScore`, route/image/hreflang analyzers, utility helpers

### 3. PagesIndexPlugin.cs → Extract fetcher + cache helpers

- **DefaultNotionPageFetcher.cs**: `DefaultNotionPageFetcher` inner class → own file
- **PagesIndexCacheHelper.cs**: `TryLoadCache`, `TrySaveCache`, `WriteJsonValue`, `ToObject`, `ToDictionary`, `NormalizeCacheMode`, `ResolveCachePath`
- **PagesIndexConfigHelper.cs**: `TryGetMap`, `TryGetString`, `TryGetBool`, `TryGetInt`, `TryGetNullableInt`, `TryGetStringList`, `HasNotionContent`, `CollectRelationIds`, `BuildKnownRawIdSet`
- Keep in PagesIndexPlugin: `DerivePages`, `GetOrCreateIndex`, `AddRoutedToIndex`, `BuildPageObject`, `BuildFieldsObject`, `ResolveNotionRelationsIfConfiguredAsync`, `GetTypeFromFields`, `LocalizeResolvedPageFieldsAsync`, `BuildResolveMediaConfig`, records/interfaces

### 4. PageRenderDispatcher.cs → Extract SpecialListRenderer

- **SpecialListRenderer.cs**: `RenderSpecialListAlwaysAsync`, `RenderSpecialListIfNeededAsync`, `BuildPageInfosAsync`, `GetTableOfContents`, `CreateListPageInfo`, `BuildListSummary`, `BuildListTitle`, `MergeCollectors`
- Keep in PageRenderDispatcher: `DispatchAsync`, `RenderPagesAsync`, `RenderSpecialListsAsync` (delegating), `WriteUtf8LockedAsync`, records

## Impact

- Affected specs: Quality Gate (oversized baseline)
- Affected code: `src/Bukit.Engine/` and `src/Bukit.Engine/Plugins/BuiltIn/`
- All extracted classes are `internal`
- No public API changes

## ADDED Requirements

### Requirement: SeoJsonLdBuilder
The system SHALL provide `SeoJsonLdBuilder` with all JSON-LD construction methods.

### Requirement: SeoGeoMetaParser
The system SHALL provide `SeoGeoMetaParser` with geo metadata parsing.

### Requirement: ImageMetadataReader
The system SHALL provide `ImageMetadataReader` with binary image metadata detection.

### Requirement: SeoSchemaValidator
The system SHALL provide `SeoSchemaValidator` with JSON-LD schema validation.

### Requirement: SeoAuditModels
The system SHALL extract SEO audit record types to `SeoAuditModels.cs`.

### Requirement: DefaultNotionPageFetcher
The system SHALL extract `DefaultNotionPageFetcher` to its own file.

### Requirement: SpecialListRenderer
The system SHALL provide `SpecialListRenderer` with special list rendering logic.

## REMOVED Requirements
None — all internal refactoring.

## Baseline Removal
After each file is split below 600 lines, remove its entry from `scripts/.oversized-baseline.txt`.
