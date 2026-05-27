# Tasks

- [ ] Task 1: Extract SeoJsonLdBuilder from SeoModelBuilder.cs
  - [ ] SubTask 1.1: Create `src/Bukit.Engine/SeoJsonLdBuilder.cs` with JSON-LD builder methods
  - [ ] SubTask 1.2: Update SeoModelBuilder.cs to delegate to SeoJsonLdBuilder

- [ ] Task 2: Extract SeoGeoMetaParser from SeoModelBuilder.cs
  - [ ] SubTask 2.1: Create `src/Bukit.Engine/SeoGeoMetaParser.cs` with geo meta parsing and ParsedGeoMeta record
  - [ ] SubTask 2.2: Update SeoModelBuilder.cs to delegate to SeoGeoMetaParser
  - [ ] SubTask 2.3: Verify SeoModelBuilder.cs < 600 lines

- [ ] Task 3: Extract SeoAuditModels from SeoAuditReportWriter.cs
  - [ ] SubTask 3.1: Create `src/Bukit.Engine/SeoAuditModels.cs` with record types and JSON context classes
  - [ ] SubTask 3.2: Update SeoAuditReportWriter.cs to use types from new file

- [ ] Task 4: Extract ImageMetadataReader from SeoAuditReportWriter.cs
  - [ ] SubTask 4.1: Create `src/Bukit.Engine/ImageMetadataReader.cs` with binary image detection
  - [ ] SubTask 4.2: Update SeoAuditReportWriter.cs to delegate

- [ ] Task 5: Extract SeoSchemaValidator from SeoAuditReportWriter.cs
  - [ ] SubTask 5.1: Create `src/Bukit.Engine/SeoSchemaValidator.cs` with schema validation
  - [ ] SubTask 5.2: Update SeoAuditReportWriter.cs to delegate
  - [ ] SubTask 5.3: Verify SeoAuditReportWriter.cs < 600 lines

- [ ] Task 6: Extract DefaultNotionPageFetcher from PagesIndexPlugin.cs
  - [ ] SubTask 6.1: Create `src/Bukit.Engine/Plugins/BuiltIn/DefaultNotionPageFetcher.cs`
  - [ ] SubTask 6.2: Update PagesIndexPlugin.cs to reference the new class

- [ ] Task 7: Extract PagesIndexCacheHelper from PagesIndexPlugin.cs
  - [ ] SubTask 7.1: Create `src/Bukit.Engine/Plugins/BuiltIn/PagesIndexCacheHelper.cs`
  - [ ] SubTask 7.2: Update PagesIndexPlugin.cs to delegate

- [ ] Task 8: Extract PagesIndexConfigHelper from PagesIndexPlugin.cs
  - [ ] SubTask 8.1: Create `src/Bukit.Engine/Plugins/BuiltIn/PagesIndexConfigHelper.cs`
  - [ ] SubTask 8.2: Update PagesIndexPlugin.cs to delegate
  - [ ] SubTask 8.3: Verify PagesIndexPlugin.cs < 600 lines

- [ ] Task 9: Extract SpecialListRenderer from PageRenderDispatcher.cs
  - [ ] SubTask 9.1: Create `src/Bukit.Engine/SpecialListRenderer.cs`
  - [ ] SubTask 9.2: Update PageRenderDispatcher.cs to delegate
  - [ ] SubTask 9.3: Verify PageRenderDispatcher.cs < 600 lines

- [ ] Task 10: Run full build and test suite + update baseline
  - [ ] SubTask 10.1: Build and test
  - [ ] SubTask 10.2: Remove 4 files from baseline

# Task Dependencies

- [Task 1], [Task 2] depend on none (same source file, but independent methods)
- [Task 3], [Task 4], [Task 5] depend on none (same source file, but independent methods)
- [Task 6], [Task 7], [Task 8] depend on none (same source file, but independent methods)
- [Task 9] depends on none
- [Task 10] depends on [Tasks 1-9]
