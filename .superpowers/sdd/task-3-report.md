# Task 3 Report: Require collection for routed content

## Status

Implemented the strict routed-content collection contract within the Task 3 scope.

## RED evidence

- `ContentPipelineTests.ExecuteAsync_ContentModeTypeOnly_ThrowsConfigException`
  failed because no exception was thrown.
- `RouteGeneratorTests.Generate_EmptyCollection_Throws` failed because no
  exception was thrown.
- The new validator and load-stage tests initially failed to compile because
  `ContentCollectionContractValidator` and
  `DiagnosticCode.ContentCollectionMissing` did not exist.

## GREEN evidence

- Focused Engine run: 14 passed, 0 failed. This covered the raw validator,
  ContentLoadStage, RouteInventoryValidator, the Task 1 pipeline RED, and the
  RouteGenerator empty-collection RED.
- Focused Shared formatter run: 58 passed, 0 failed.
- Focused RouteGenerator defense run: 3 passed, 0 failed. This covered empty
  collection, full override bypass prevention, and type-permalink bypass
  prevention.

## Reviewer closure evidence

- Focused raw-validator edge run: 3 passed, 0 failed. This covered missing
  `sourceMode` plus missing collection, `Properties`-only input, and
  `CustomFields` precedence when both raw maps contain conflicting values.
- Focused RouteGenerator override defense run: 2 passed, 0 failed. This covered
  both full and partial route overrides without collection and confirmed both
  fail with `ContentCollectionMissing` before override resolution.

## Changed files

- `src/Bukit-Core/Bukit.Shared/DiagnosticCode.cs`
- `src/Bukit-Core/Bukit.Shared/DiagnosticCodeFormatter.cs`
- `src/Bukit-Core/Bukit.Engine/ContentCollectionContractValidator.cs`
- `src/Bukit-Core/Bukit.Engine/Stages/ContentLoadStage.cs`
- `src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs`
- `src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`
- `tests/Bukit.Shared.Tests/DiagnosticCodeFormatterTests.cs`
- `tests/Bukit.Engine.Tests/ContentCollectionContractValidatorTests.cs`
- `tests/Bukit.Engine.Tests/ContentStagesTests.cs`
- `tests/Bukit.Engine.Tests/SiteEngineHelperTests.cs`
- `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`

## Remaining expected REDs

The following focused tests still encode the pre-Task-7 type-to-collection
fallback and fail with `ContentCollectionMissing` as expected:

- `RouteGeneratorCoverageTests.Generate_GetCollection_EmptyCollectionField_UsesCanonicalType`
- `RouteGeneratorCoverageTests.Generate_GetCollection_WhitespaceCollectionField_UsesCanonicalType`
- `RouteGeneratorTests.Generate_CollectionsRule_TypeOnly_UsesCanonicalCollection`

Per the task brief, the repository post-change gate was not run before the
planned Task 7 distinct-type behavior is green.

## P1 fixture follow-up

Audited every `RouteGeneratorTests` fixture after the strict collection
contract landed. Fixtures that are expected to route successfully or exercise
another validation path now declare an explicit collection matching the rule
under test (`post`, `page`, or the intentional custom collection). The four
tests that intentionally exercise `ContentCollectionMissing` and the planned
Task 7 type-only fallback test were left unchanged.

The initial full `RouteGeneratorTests` filter had 16 failures because missing
collection metadata intercepted unrelated assertions. After the fixture-only
update, the same 45-test filter produced 44 passed and 1 failed. The only
remaining failure in this class is the expected Task 7 RED:

- `RouteGeneratorTests.Generate_CollectionsRule_TypeOnly_UsesCanonicalCollection`

Together with the two expected Task 7 REDs in
`RouteGeneratorCoverageTests`, the exact old fallback failures remain:

- `RouteGeneratorCoverageTests.Generate_GetCollection_EmptyCollectionField_UsesCanonicalType`
- `RouteGeneratorCoverageTests.Generate_GetCollection_WhitespaceCollectionField_UsesCanonicalType`
- `RouteGeneratorTests.Generate_CollectionsRule_TypeOnly_UsesCanonicalCollection`

No production routing behavior or type/collection route separation was
implemented by this follow-up.

## Commit

`feat(content): require collection for routed content` (this Task 3 commit)

## Self-review

- Validation runs on raw provider documents immediately after source-level
  injection and before normalization in both required load paths.
- Data-mode documents are explicitly exempt; missing or content mode requires
  a nonblank raw `collection` field.
- RouteGenerator validates before full/partial override and permalink
  resolution, so those paths cannot bypass the invariant.
- Error code and message are consistent across pipeline and routing defenses,
  with stable `unknown` source fallback.
- Raw validation preserves normalizer input semantics: properties-only fields
  are accepted and CustomFields take precedence when both maps are present.
- No compatibility mode or type fallback was introduced.
- No config/docs/dataIndex/CollectionWarning or backup/reference path was
  modified by Task 3.
