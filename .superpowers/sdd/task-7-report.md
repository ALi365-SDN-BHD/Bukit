# Task 7 Report: Strictly separate routing type and collection

## Scope completed

- `RouteGenerator.GetType()` now reads only the canonical content type.
- Collection rule lookup continues to use only the explicit/canonical collection.
- Type permalink lookup now uses the canonical type independently from collection.
- `{type}` and `{collection}` expand independently.
- Missing, empty, and whitespace collection cases remain blocked with
  `ContentCollectionMissing` before route override or rule resolution.
- No compatibility fallback, switch, warning-only path, or non-routing
  production change was added.

## RED evidence

Before test migration and production implementation, the four legacy fallback
tests ran and failed 0/4 because strict collection validation threw
`ContentCollectionMissing` instead of routing by type.

After rewriting those tests and strengthening the distinct-value contract, the
three new behavior assertions failed 0/3 against the old `GetType()` behavior:

- distinct placeholders expected `article/news` but produced `news/news`;
- the collection rule expected `post/article` but produced `article/article`;
- type permalink lookup failed because it searched for collection `news`
  instead of type `article`.

## GREEN evidence

- Exact strict-contract tests: 8 passed, 0 failed.
- Required Engine filter (`RouteGenerator|RoutePipeline`): 93 passed, 0 failed.
- Required `Bukit.Routing.Tests`: 23 passed, 0 failed.

The first required verification run exposed two stale fixtures. Root-cause
review showed that the golden permalink map was still keyed by collection and
the Routing project test constructed a document without explicit collection
metadata. The fixtures were migrated without changing production semantics;
the same required commands then passed.

## Post-change targeted gate

The required command passed:

```sh
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Routing/RouteGenerator.cs \
  tests/Bukit.Engine.Tests/RouteGeneratorTests.cs \
  tests/Bukit.Engine.Tests/RouteGeneratorCoverageTests.cs \
  tests/Bukit.Engine.Tests/RoutePipelineTests.cs \
  tests/Bukit.Routing.Tests
```

Results included all contract/docs/self-tests, 23 passing Routing tests, and
1264 passing Engine tests. No full, release, or whole-solution gate was run.

## Changed files

- `src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`
- `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`
- `tests/Bukit.Engine.Tests/RouteGeneratorCoverageTests.cs`
- `tests/Bukit.Engine.Tests/RouteGeneratorGoldenTests.cs`
- `tests/Bukit.Routing.Tests/RouteGeneratorTests.cs`
- `.superpowers/sdd/task-7-report.md`

`RouteGeneratorGoldenTests.cs` was adjusted because it is selected by the
brief's required Engine filter: its permalink map now correctly keys the
default canonical type `page`, while the snapshot remains unchanged.

## Audit and boundaries

- The final production diff is one line and removes the last route-level
  type-to-collection fallback.
- The four old fallback assertions now explicitly require rejection.
- Distinct `type=article, collection=news` behavior is covered in placeholder
  and type-permalink tests; collection-rule precedence uses distinct values.
- Full and partial override rejection tests remain present and passing.
- A requested read-only sub-agent review could not start because the shared
  agent thread limit was reached. The main thread audited the complete scoped
  diff and fallback search and found no unresolved issue.
- All unrelated staged and unstaged changes were preserved.

## Commit

`fix(routing): enforce explicit collection routing` (this Task 7 commit)
