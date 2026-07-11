# Task 4 Report: Remove legacy collection fallback warnings

## Integration note

- Intentionally integrated the pre-existing staged and unstaged experiments in `CollectionWarningStage.cs` and `CollectionWarningStageTests.cs` into the Task 4 behavior.
- Removed all per-document inspection from `CollectionWarningStage`; `type`, `collection`, their same/distinct combinations, and `sourceMode: data` no longer produce compatibility warnings in this stage.
- Preserved the existing `filteredLists` behavior:
  - a configured parent `listRoute` emits one collection-level INFO;
  - a missing parent `listRoute` emits one WARN per configured filtered route.
- `ContentStageOutput` has no warning-count field: its fourth field is `DurationMs`. The final implementation leaves that duration override at zero for both INFO and WARN paths, rather than misreporting the WARN count as elapsed milliseconds; actual warning counts are verified against logger WARN entries.
- No data-index, documentation, routing, or other parallel-task path was modified for this task.

## TDD and verification

- RED: after rewriting the compatibility-warning tests first, the full `CollectionWarningStageTests` class reported 3 expected failures (`type-only`, same `type`/`collection`, and distinct `type`/`collection`) and 4 passes.
- GREEN: `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --no-restore --filter "FullyQualifiedName~CollectionWarningStageTests"` passed: 7 passed, 0 failed, 0 skipped.
- Whitespace: `git diff HEAD --check -- src/Bukit-Core/Bukit.Engine/Stages/CollectionWarningStage.cs tests/Bukit.Engine.Tests/CollectionWarningStageTests.cs` passed.
- Targeted gate: `bash scripts/checks/post-change-targeted.sh -- src/Bukit-Core/Bukit.Engine/Stages/CollectionWarningStage.cs tests/Bukit.Engine.Tests/CollectionWarningStageTests.cs` reached the `Bukit.Engine.Tests` project after all preceding diff, contract, docs, and self-tests passed, then failed with 23 unrelated planned RED tests that still expect the superseded collection/type fallback behavior:
  - `RouteGeneratorCoverageTests.ExpandPermalinkPattern_TypePlaceholder_ExplicitType`
  - `RouteGeneratorCoverageTests.ExpandPermalinkPattern_NoTypeField_UsesCanonicalPage`
  - `RouteGeneratorCoverageTests.ExpandPermalinkPattern_TypePlaceholder_NonStringType_UsesToString`
  - `RouteGeneratorCoverageTests.Generate_GetCollection_NoCollectionField_UsesCanonicalType`
  - `RouteGeneratorCoverageTests.Generate_GetCollection_EmptyCollectionField_UsesCanonicalType`
  - `RouteGeneratorCoverageTests.ExpandPermalinkPattern_TypePlaceholder_MissingTypeUsesCanonicalPage`
  - `RouteGeneratorCoverageTests.Generate_GetType_IntMetaValueWithoutRule_Throws`
  - `RouteGeneratorCoverageTests.Generate_GetType_NullMetaTypeWithoutRule_Throws`
  - `RouteGeneratorCoverageTests.Generate_NormalizeUrl_AlreadyNormalized_Unchanged`
  - `RouteGeneratorCoverageTests.ExpandPermalinkPattern_AllPlaceholders_ReplacesCorrectly`
  - `RouteGeneratorCoverageTests.Generate_GetCollection_WhitespaceCollectionField_UsesCanonicalType`
  - `RouteGeneratorCoverageTests.Generate_NormalizeUrl_MissingLeadingSlash_Added`
  - `RouteGeneratorCoverageTests.ExpandPermalinkPattern_NullTypeValue_UsesCanonicalPage`
  - `RouteGeneratorGoldenTests.GenerateWithSource_MatchesGoldenSnapshot`
  - `SeoIndexBuilderTests.Build_WithRoutedItems_CreatesEntriesAndModels`
  - `ContentPipelineTests.ExecuteAsync_WhenCanonicalSchemaStrict_ThrowsConfigException`
  - `ContentPipelineTests.ExecuteAsync_LoadsLocalizesFiltersDraftsAndBuildsCanonicalContent`
  - `RouteGeneratorTests.Generate_CollectionsRule_TypeOnly_UsesCanonicalCollection`
  - `ContentStagesTests.ContentLoadStage_ProjectsConfiguredCanonicalMappings`
  - `ContentStagesTests.ContentLoadStage_EnrichesCanonicalGraphFromConfiguredEntityAndRelationMappings`
  - `ContentStagesTests.ContentLoadStage_RoutesToProviderFactory`
  - `SiteEngineHelperTests.GetCollection_WithNeither_ReturnsPage`
  - `SiteEngineHelperTests.GetCollection_WithTypeMeta_ReturnsTypeValue`

## Commit

- Subject: `refactor(diagnostics): remove legacy collection fallback warnings`
- Scope: only `CollectionWarningStage.cs`, `CollectionWarningStageTests.cs`, and this report.

## Self-review

- The implementation is the minimum behavior change: the stage now delegates exclusively to the already-established `filteredLists` advisory logic.
- Tests cover all required no-warning metadata cases, assert INFO/WARN logging counts, and ensure warning counts are not written into the `DurationMs` output field.
- The final two-path diff contains no data-specific branch or legacy type/collection warning text.
- The targeted gate failures are outside Task 4 and were not hidden or modified.
- The required bounded read-only diff review found no remaining Critical or Important issues after the `DurationMs` correction.
