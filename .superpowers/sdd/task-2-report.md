# Task 2 Report: Remove bidirectional type/collection inference

## Scope completed

- Canonical normalization now resolves `type` only from an explicit type or the source-mode default (`module` for data, otherwise `page`).
- Canonical normalization now resolves `collection` only from an explicit collection or `string.Empty`.
- Applied the same contract in `CanonicalContentGraphBuilder`, `ContentDocument.Create`, and the `IContentBodyStore` raw-document adapter.
- Removed the unused `ContentFieldReader.GetEffectiveCollection` fallback helper.
- Content-model field-scope defaults, required checks, validation, and unknown-key allowlisting now select a scope only from an explicit collection.
- Did not add collection-required validation or change `RouteGenerator`; those remain Tasks 3 and 7.
- Preserved all pre-existing and concurrent CollectionWarning/dataIndex work without staging it.

## Changed files

- `src/Bukit-Core/Bukit.Engine/CanonicalContentGraphBuilder.cs`
- `src/Bukit-Core/Bukit.Engine/ContentDocumentNormalizer.cs`
- `src/Bukit-Core/Bukit.Engine/ContentModelSchemaValidator.cs`
- `src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocument.cs`
- `src/Bukit-Core/Bukit.Engine.Abstractions/ContentFieldReader.cs`
- `src/Bukit-Core/Bukit.Engine.Abstractions/IContentBodyStore.cs`
- `tests/Bukit.Engine.Tests/ContentStagesTests.cs`
- `tests/Bukit.Engine.Abstractions.Tests/ContentDocumentTests.cs`
- `tests/Bukit.Engine.Abstractions.Tests/ContentBodyStoreAdapterTests.cs`

## RED evidence

Task 1 normalizer contract command:

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --no-restore --filter "FullyQualifiedName~ContentDocumentNormalizer_CollectionOnly_DefaultsTypeToPage|FullyQualifiedName~ContentDocumentNormalizer_DistinctTypeAndCollection_PreservesBoth|FullyQualifiedName~ContentDocumentNormalizer_DataModeWithoutCollection_DefaultsTypeToModuleAndLeavesCollectionEmpty" --logger "console;verbosity=normal"
```

Result before production changes: expected RED, exit 1; 3 tests ran, 1 passed and 2 failed. Collection-only produced type `news` instead of `page`; data without collection produced collection `module` instead of empty.

Additional test-first RED evidence:

- `ContentDocument.Create` and the raw body-store adapter: 3 tests ran and all 3 failed on the old inference/default behavior.
- Field-scope selection: 2 tests ran and both failed because type-only content selected the matching field scope for defaults/required checks or unknown-key allowlisting.

## GREEN verification

Focused abstraction command:

```sh
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj --no-restore --filter "FullyQualifiedName~ContentDocumentTests|FullyQualifiedName~ContentBodyStoreAdapterTests" --logger "console;verbosity=minimal"
```

Result: exit 0; 10 passed, 0 failed.

Focused normalizer/schema command:

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --no-restore --filter "FullyQualifiedName~ContentStagesTests|FullyQualifiedName~ContentSchemaValidatorExtendedTests" --logger "console;verbosity=minimal"
```

Result: exit 0; 26 passed, 0 failed.

Expected later-task RED command:

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --no-restore --filter "FullyQualifiedName~ExecuteAsync_ContentModeTypeOnly_ThrowsConfigException|FullyQualifiedName~Generate_EmptyCollection_Throws" --logger "console;verbosity=normal"
```

Result: expected RED, exit 1; 2 tests ran and both failed because the Task 3 pipeline validation and Task 7 route validation are intentionally not implemented in Task 2.

No full, release, whole-solution, or post-change gate was run because the plan intentionally retains these two later-task RED tests.

## Fallback searches and diff check

- `rg -n "GetEffectiveCollection" src/Bukit-Core tests` returned no matches.
- A multiline search for `type ?? collection`, `collection ?? type`, and equivalent `FirstText` fallbacks in the affected Engine/Abstractions surfaces returned no matches.
- A multiline search for `collection` null-coalescing fallbacks in field-scope normalization/validation returned no matches.
- `git diff --check -- <Task 2 paths>` exited 0 with no whitespace errors.

## Commit

- Commit message: `refactor(content): remove type and collection inference`
- The final commit hash is reported in the Task 2 handoff. It cannot be embedded literally in a file contained by that same commit because changing the embedded hash changes the commit hash.

## Self-review

- The production diff is limited to canonical normalization and collection-scoped schema behavior.
- All three construction seams use the same source-mode default and never derive type from collection or collection from type.
- Field-scope defaults, normalizer-required diagnostics, validator-required checks, and strict unknown-key allowlisting share the same explicit-collection boundary.
- No collection-required validation, route behavior, backup/reference files, dataIndex work, or CollectionWarning work was modified by Task 2.
- Focused tests compile and pass; the two explicitly deferred contract tests remain RED for the expected reasons.
- The required read-only diff review flagged that removing public `GetEffectiveCollection` can break external source callers. The method remains removed because the Task 2 brief explicitly requires its removal and repository fallback search found no internal callers; this compatibility risk is called out in the handoff.

## Reviewer follow-up: collection-only body adapter coverage

- Added `GetAsync_RawCollectionWithoutTypeOrSourceMode_UsesPageTypeAndPreservesCollection` to cover the raw `IContentBodyStore` adapter when `collection=news` and both `type` and `sourceMode` are absent.
- The assertions require `ContentType` to default to `page` while preserving `Collection` as `news`.
- Focused verification command:

  ```sh
  dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj --no-restore --filter "FullyQualifiedName~ContentBodyStoreAdapterTests.GetAsync_RawCollectionWithoutTypeOrSourceMode_UsesPageTypeAndPreservesCollection" --logger "console;verbosity=normal"
  ```

- Result: exit 0; 1 test passed, 0 failed.
