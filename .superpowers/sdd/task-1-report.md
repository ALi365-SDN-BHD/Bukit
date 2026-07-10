# Task 1 Report: Strict type/collection contract tests

## Scope completed

- Modified tests only; no production code was changed by Task 1.
- Preserved the pre-existing changes in `src/Bukit-Core/Bukit.Engine/Stages/CollectionWarningStage.cs` and `tests/Bukit.Engine.Tests/CollectionWarningStageTests.cs` without staging them.
- Added contract coverage at the pipeline, normalizer, routing, Markdown provider, and composite provider seams.

## Changed files

- `tests/Bukit.Engine.Tests/ContentPipelineTests.cs`
  - Specifies content-mode `type: article` without `collection` as invalid at the pipeline seam.
- `tests/Bukit.Engine.Tests/ContentStagesTests.cs`
  - Specifies collection-only `news` as `type: page`, `collection: news`.
  - Specifies distinct `type: article`, `collection: news` values remain distinct.
  - Specifies data without collection as `type: module` with an empty collection.
- `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`
  - Specifies a direct call with an empty canonical collection as invalid, even if an empty-key collection rule exists.
- `tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs`
  - Specifies explicit `type: article`, `collection: news` front matter is preserved.
- `tests/Bukit.Content.Tests/CompositeContentProviderTests.cs`
  - Specifies source collection `news` overrides item collection while preserving item type `article`, in both custom fields and raw properties.

## TDD commands and results

Engine exact-new-test command:

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --no-restore --filter "FullyQualifiedName~ExecuteAsync_ContentModeTypeOnly_ThrowsConfigException|FullyQualifiedName~ContentDocumentNormalizer_CollectionOnly_DefaultsTypeToPage|FullyQualifiedName~ContentDocumentNormalizer_DistinctTypeAndCollection_PreservesBoth|FullyQualifiedName~ContentDocumentNormalizer_DataModeWithoutCollection_DefaultsTypeToModuleAndLeavesCollectionEmpty|FullyQualifiedName~Generate_EmptyCollection_Throws" --logger "console;verbosity=normal"
```

Result: expected RED, exit 1; the project compiled and 5 tests ran (1 passed, 4 failed).

Expected contract-gap failures:

- `ContentPipelineTests.ExecuteAsync_ContentModeTypeOnly_ThrowsConfigException`: no `ConfigException` was thrown.
- `ContentStagesTests.ContentDocumentNormalizer_CollectionOnly_DefaultsTypeToPage`: expected type `page`, actual `news`.
- `ContentStagesTests.ContentDocumentNormalizer_DataModeWithoutCollection_DefaultsTypeToModuleAndLeavesCollectionEmpty`: expected empty collection, actual `module`.
- `RouteGeneratorTests.Generate_EmptyCollection_Throws`: no `ConfigException` was thrown.

Passing preservation test:

- `ContentStagesTests.ContentDocumentNormalizer_DistinctTypeAndCollection_PreservesBoth`.

Content exact-new-test command:

```sh
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --no-restore --filter "FullyQualifiedName~LoadAsync_WithDistinctTypeAndCollection_PreservesBoth|FullyQualifiedName~LoadRawAsync_SourceCollectionOverridesItemCollectionWithoutChangingType" --logger "console;verbosity=normal"
```

Result: exit 0; the project compiled and both tests passed (2 passed, 0 failed), confirming existing provider preservation/override behavior.

Additional check:

```sh
git diff --check
```

Result: exit 0 with no whitespace errors.

## Commit

- Message: `test(content): define strict type and collection contract`
- Base commit before Task 1: `eb6ceae46b5d1f0e0a2301dc54827d2c3797d7e5`
- Final commit hash is reported in the Task 1 handoff. It cannot be embedded literally in a file contained by that same commit because changing the embedded hash changes the commit hash.

## Self-review

- All required values and boundary cases are represented, including the distinct `type=article, collection=news` fixture.
- The RouteGenerator test constructs an explicitly empty canonical collection and supplies an empty-key rule so the failure cannot be mistaken for an ordinary missing-route-rule failure.
- The tests exercise real production seams and use no behavior mocks.
- No production files, backup/reference directories, broad gates, or unrelated tests were modified.
- Expected RED failures are assertion failures caused by missing contract behavior, not compilation, discovery, or test setup errors.
