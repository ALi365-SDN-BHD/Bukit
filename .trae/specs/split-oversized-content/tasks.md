# Tasks

- [ ] Task 1: Extract MarkdownFrontMatterParser from MarkdownFolderProvider.cs
  - [ ] SubTask 1.1: Create `src/Bukit.Content/Markdown/MarkdownFrontMatterParser.cs` with `TryExtractFrontMatter`, `ParseFrontMatter`, `NormalizeTaxonomy`, `ToObject` as `internal static`
  - [ ] SubTask 1.2: Update `MarkdownFolderProvider.cs` to delegate to `MarkdownFrontMatterParser`

- [ ] Task 2: Extract MarkdownFieldBuilder from MarkdownFolderProvider.cs
  - [ ] SubTask 2.1: Create `src/Bukit.Content/Markdown/MarkdownFieldBuilder.cs` with `BuildFields`, `IsReservedMetaKey`, `TryConvertToField`, `TryConvertToList`, `TryParseDateTimeOffset`
  - [ ] SubTask 2.2: Update `MarkdownFolderProvider.cs` to delegate to `MarkdownFieldBuilder`

- [ ] Task 3: Extract MarkdownTextHelper from MarkdownFolderProvider.cs
  - [ ] SubTask 3.1: Create `src/Bukit.Content/Markdown/MarkdownTextHelper.cs` with `ExtractSummaryFromMarkdown`, `ExtractSummaryFromHtml`, `StripHtmlToText`, `CollapseWhitespace`, `TruncateAtWordBoundary`, `ExtractTitle`
  - [ ] SubTask 3.2: Update `MarkdownFolderProvider.cs` to delegate to `MarkdownTextHelper`
  - [ ] SubTask 3.3: Verify `MarkdownFolderProvider.cs` < 600 lines

- [ ] Task 4: Extract NotionPropertyParser from NotionContentProvider.cs
  - [ ] SubTask 4.1: Create `src/Bukit.Content/Notion/NotionPropertyParser.cs` with all property parsing methods
  - [ ] SubTask 4.2: Update `NotionContentProvider.cs` to delegate to `NotionPropertyParser`

- [ ] Task 5: Extract NotionCacheManager from NotionContentProvider.cs
  - [ ] SubTask 5.1: Create `src/Bukit.Content/Notion/NotionCacheManager.cs` with cache logic and `PageHtmlCache` record
  - [ ] SubTask 5.2: Update `NotionContentProvider.cs` to delegate to `NotionCacheManager`

- [ ] Task 6: Extract NotionMetaHelper from NotionContentProvider.cs
  - [ ] SubTask 6.1: Create `src/Bukit.Content/Notion/NotionMetaHelper.cs` with field promotion and cover/icon methods
  - [ ] SubTask 6.2: Update `NotionContentProvider.cs` to delegate to `NotionMetaHelper`

- [ ] Task 7: Extract NotionRelationResolver from NotionContentProvider.cs
  - [ ] SubTask 7.1: Create `src/Bukit.Content/Notion/NotionRelationResolver.cs` with relation resolution
  - [ ] SubTask 7.2: Update `NotionContentProvider.cs` to delegate to `NotionRelationResolver`
  - [ ] SubTask 7.3: Verify `NotionContentProvider.cs` < 600 lines

- [ ] Task 8: Extract MediaIndexManager from ImageAssetLocalizer.cs
  - [ ] SubTask 8.1: Create `src/Bukit.Content/Media/MediaIndexManager.cs` with index management methods
  - [ ] SubTask 8.2: Update `ImageAssetLocalizer.cs` to delegate to `MediaIndexManager`

- [ ] Task 9: Extract SsrfGuard from ImageAssetLocalizer.cs
  - [ ] SubTask 9.1: Create `src/Bukit.Content/Media/SsrfGuard.cs` with SSRF-safe methods
  - [ ] SubTask 9.2: Update `ImageAssetLocalizer.cs` to delegate to `SsrfGuard`
  - [ ] SubTask 9.3: Verify `ImageAssetLocalizer.cs` < 600 lines

- [ ] Task 10: Run full build and test suite + update baseline
  - [ ] SubTask 10.1: Run `dotnet build bukit.slnx -c Release -warnaserror`
  - [ ] SubTask 10.2: Run full test suite
  - [ ] SubTask 10.3: Update `scripts/.oversized-baseline.txt` to remove the 3 split files
  - [ ] SubTask 10.4: Verify no new oversized files (≥600 lines) were introduced

# Task Dependencies

- [Task 1], [Task 2], [Task 3] are independent of each other and of all other tasks
- [Task 4], [Task 5], [Task 6], [Task 7] are independent of each other and of all other tasks
- [Task 8], [Task 9] are independent of each other and of all other tasks
- [Task 10] depends on [Tasks 1-9]
