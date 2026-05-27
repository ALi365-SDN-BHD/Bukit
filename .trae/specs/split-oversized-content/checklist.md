# Checklist

- [x] MarkdownFrontMatterParser created with TryExtractFrontMatter, ParseFrontMatter, NormalizeTaxonomy, ToObject
- [x] MarkdownFieldBuilder created with BuildFields, IsReservedMetaKey, TryConvertToField, TryConvertToList, TryParseDateTimeOffset
- [x] MarkdownTextHelper created with ExtractSummaryFromHtml, StripHtmlToText, CollapseWhitespace, TruncateAtWordBoundary, ExtractTitle
- [x] MarkdownFolderProvider.cs < 600 lines after split (222 lines)
- [x] NotionPropertyParser created with all property parsing methods (public, backward-compatible)
- [x] NotionPropertyTypeParser created with TryParseNotionPropertyToField and related parsers
- [x] NotionCacheManager created with cache logic and PageHtmlCache record
- [x] NotionMetaHelper created with field promotion and cover/icon methods
- [x] NotionRelationResolver created with relation resolution
- [x] NotionContentProvider.cs < 600 lines after split (342 lines)
- [x] MediaIndexManager created with index management methods
- [x] SsrfGuard created with SSRF-safe methods
- [x] ImageAssetLocalizer.cs < 600 lines after split (360 lines)
- [x] Full dotnet build passes with 0 warnings (treat-warnings-as-errors)
- [x] Full test suite passes (2361+ passed, 0 failed, 0 skipped)
- [x] scripts/.oversized-baseline.txt no longer contains the 3 split files
- [x] No new oversized files (≥600 lines) introduced — all new files below 600
