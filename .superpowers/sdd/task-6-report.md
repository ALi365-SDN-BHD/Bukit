# Task 6 Report: Migrate repository fixtures and generators to explicit collections

## Scope and boundaries

- Migrated repository-owned Engine test fixtures whose purpose is unrelated to missing collection.
- Kept the four tests that explicitly assert legacy `type -> collection` routing behavior RED for Task 7.
- Updated only the Labs clone `site.yaml` writer in production code; no Core routing, downstream collection consumer, SEO/search, active guide, backup, or parallel dataIndex/config/docs path was changed.
- Preserved intentional `ContentCollectionMissing` coverage.

## Before and after failure inventory

The Task 4 full Engine run recorded 23 failures.

- 19 fixture/helper failures were resolved in Task 6:
  - 11 RouteGenerator URL, placeholder, error-path, and golden fixtures now declare explicit collections.
  - 2 direct collection helper assertions now expect `string.Empty` for collection-less canonical documents rather than restoring fallback.
  - 1 SEO test document now declares its explicit page collection.
  - 2 content pipeline tests now use explicit post collections.
  - 3 content load stage fixtures now declare explicit collections.
- 4 failures remain intentionally RED for Task 7:
  - `RouteGeneratorTests.Generate_CollectionsRule_TypeOnly_UsesCanonicalCollection`
  - `RouteGeneratorCoverageTests.Generate_GetCollection_NoCollectionField_UsesCanonicalType`
  - `RouteGeneratorCoverageTests.Generate_GetCollection_EmptyCollectionField_UsesCanonicalType`
  - `RouteGeneratorCoverageTests.Generate_GetCollection_WhitespaceCollectionField_UsesCanonicalType`

The full Engine result after Task 6 was 1259 passed, 4 failed, 0 skipped, 1263 total.

## Labs generator migration

- `CloneYamlWriter` now writes `collection: page` for the generated content-mode markdown source.
- The generated data-mode modules source remains collection-optional and continues to use `markdown.defaultType: module` only for type classification.
- P2 follow-up: when clone updates an existing modules data source, it now removes any stale `collection` key instead of preserving a content-collection assignment on a data-mode source.
- Existing theme-init YAML already emitted explicit collections for every content-mode source, so no generator behavior change was needed there.
- Added a drift test that generates all six official non-`none` init templates (`minimal`, `blog`, `docs`, `landing`, `portfolio`, and `bare`) and verifies every content-mode source has a non-empty collection while generated data-mode sources have no collection.
- Clone YAML tests parse the generated YAML and verify the same content/data distinction directly.

## Final review follow-up

- Replaced the five remaining `ContentFieldReader.GetCollection` assertions that expected `type`, `page`, or a caller-supplied default fallback. Missing, empty, and whitespace collections now explicitly assert `string.Empty`; only an explicit collection returns a value.
- Renamed those tests so their names state the strict collection contract rather than the removed fallback behavior.
- Added `collection: post` to the metadata-first/body-hydration Markdown fixture and asserted that the loaded item retains the explicit collection.

## TDD and verification

- Baseline affected Engine classes reproduced all 23 Task 4 failures: 137 passed, 23 failed.
- RouteGenerator coverage after fixture migration: 30 passed, with only the 3 planned fallback tests RED.
- Migrated Engine fixture/helper classes: 82 passed, 0 failed.
- Clone YAML RED: the new assertion expected `collection: page` and observed `null` before the writer change.
- Clone YAML GREEN: 3 passed, 0 failed.
- P2 follow-up RED: an existing modules data source with `collection: legacy-modules` retained that key after clone; the parsed-YAML absence assertion failed.
- P2 follow-up GREEN: the focused existing-source regression passed after null/empty target collections began removing the key.
- P2 follow-up full `Bukit.Labs.Cli.Tests`: 152 passed, 0 failed.
- Final review RED: `ContentFieldReaderItemTests` recorded 5 expected assertion failures and 2 passes because the strict implementation returned `string.Empty` while stale tests still expected fallback values.
- Final review baseline: `MarkdownRawContentLoadResultTests` passed 1 test; its non-error fixture was then made contract-explicit.
- Final review GREEN: `ContentFieldReaderItemTests` passed 7 tests and `MarkdownRawContentLoadResultTests` passed 1 test with their respective class filters.
- Final review targeted gate passed all contract checks plus 54 `Bukit.Engine.Abstractions.Tests` and 662 `Bukit.Content.Tests`; no full or release gate was run.
- Theme-init drift test: 1 passed, 0 failed across all six generated templates.
- Full `Bukit.Labs.Cli.Tests`: 152 passed, 0 failed.
- Full `Bukit.Engine.Tests`: 1259 passed, 4 expected Task 7 failures.
- `git diff --check` for the nine Task 6 code/test paths passed.
- Exact post-change gate:
  - The first run stopped during restore because the default NuGet HTTP cache was not writable.
  - Re-running the same gate with `NUGET_HTTP_CACHE_PATH=/tmp/bukit-task6-nuget-http-cache` passed diff, contract, documentation, self-test, script, and all 152 Labs tests.
  - The gate then reached full Engine tests and returned non-zero only for the same four documented Task 7 REDs (1259 passed, 4 failed).

## Commit

- Subject: `test(content): migrate fixtures to explicit collections`
- Scope: only the nine Task 6 code/test paths and this report.
- P2 follow-up subject: `fix(clone): clear collection from data sources`
- P2 follow-up scope: only `CloneYamlWriter.cs`, `CloneYamlWriterTests.cs`, and this report.
- Final review follow-up subject: `test(content): remove remaining collection fallback fixtures`
- Final review follow-up scope: only `ContentFieldReaderItemTests.cs`, `MarkdownRawContentLoadResultTests.cs`, and this report.

## Self-review

- No production Core behavior was changed.
- The change does not implement or restore `type -> collection` inference.
- Empty and whitespace collections remain invalid and their old fallback assertions remain visible for Task 7.
- Data sources remain collection-optional; `defaultType` remains type-only.
- Active guide changes were deferred to Task 10 and backup directories were not touched.
- Existing staged and unstaged parallel work was excluded from the Task 6 commit by exact paths.
