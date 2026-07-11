# Task 8 Report: Use collection as the sole downstream grouping key

## Scope completed

- `CollectionRouteIndex` now drops collectionless documents before building
  `AllOrdered` and collection groups, and never supplies content type as a
  collection default.
- Collection lists, pagination, filtered lists, and archives consume the real
  collection through the shared index.
- RSS/Atom/JSON feed eligibility remains collection-only. Focused RED design
  proved `RssGenerator` already met the locked behavior, so no RSS production
  change was made.
- Single-language and merged-i18n sitemaps now exclude routed and derived
  documents whose real collection has `output.sitemap=false`.
- The final `SitemapPublishProjection` reuses the same document exclusion
  policy, so the report/projection phase cannot reintroduce excluded URLs.
- Existing item-level `sitemapExclude` behavior is combined with collection
  exclusions in both sitemap flows.
- List-route sitemap policy already excluded collection list, pagination, and
  filtered-list routes by real collection, so no production change was needed.
- Collection-level schema fail modes are resolved per issue from the source
  document's real collection. `off`, `warn`, `strict`, and global fallback
  retain their locked semantics without content-type lookup.
- Task 9 SEO/search models and machine-readability logic were not modified.

## RED evidence

The first focused run compiled and executed 9 tests: 4 passed and 5 failed.

- `CollectionRouteIndex` incorrectly retained a collectionless module in
  `AllOrdered`.
- Single-language sitemap retained a routed `news` article and derived `news`
  archive when `news.output.sitemap=false`.
- Collection `news` strict over global warn did not block.
- Collection `news` off over global strict still returned/logged issues and
  blocked.
- Collection `news` warn over global strict still blocked.

The independently added merged-i18n test also failed because the merged sitemap
retained `/en/news/news-1/` under the disabled `news` collection.

A bounded diff review then found that the final single-site publish projection
rewrote the already-filtered sitemap. The direct projection regression failed
because `/news/news-1/` was reintroduced. The user authorized the minimally
necessary scope expansion to `PublishProjectionContract.cs`; the projection
now reuses the same collection/document exclusion policy.

The same first run established that RSS strict collection selection passed and
all three list-route sitemap plan kinds passed before production changes.

## GREEN evidence

- Core locked matrix: 10 passed, 0 failed.
- Distinct downstream list/pagination/filtered/archive tests: 2 passed, 0 failed.
- Brief-required Engine filter after the projection fix: 111 passed, 0 failed.
- Single-language and merged-i18n sitemap tests each cover:
  - collection-level exclusion of routed and derived `news` documents;
  - item-level exclusion inside an enabled collection;
  - preservation of an eligible `guides` document.
- Schema tests live in
  `tests/Bukit.Engine.Tests/ContentGraphValidateStageCollectionModeTests.cs`
  and use `type=article, collection=news` with conflicting `article` modes to
  prove lookup never falls back to content type.

## Post-change targeted gate

The brief-required command passed:

```sh
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/CollectionRouteIndex.cs \
  src/Bukit-Core/Bukit.Engine/RssGenerator.cs \
  src/Bukit-Core/Bukit.Engine/I18nOutputMerger.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/SitemapPlugin.cs \
  src/Bukit-Core/Bukit.Engine/ListRouteSitemapPolicy.cs \
  src/Bukit-Core/Bukit.Engine/Stages/ContentGraphValidateStage.cs \
  tests/Bukit.Engine.Tests
```

All contract/docs/self-tests passed, followed by 1277 passing Engine tests.
No full, release, or whole-solution gate was run.

## Production files changed

- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/CollectionRouteIndex.cs`
- `src/Bukit-Core/Bukit.Engine/I18nOutputMerger.cs`
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/SitemapPlugin.cs`
- `src/Bukit-Core/Bukit.Engine/Stages/ContentGraphValidateStage.cs`
- `src/Bukit-Core/Bukit.Engine/PublishProjectionContract.cs`

`PublishProjectionContract.cs` was added to scope after read-only review proved
that its sitemap projection runs after `SitemapPlugin` and rewrites the final
file. Mutating the shared SEO index would have affected Task 9 consumers, so
the projection instead reuses the real collection exclusion policy directly.

`RssGenerator.cs` and `ListRouteSitemapPolicy.cs` were verified but unchanged
because their strict tests were already green.

## Direct test files changed

- `tests/Bukit.Engine.Tests/CollectionRouteIndexTests.cs`
- `tests/Bukit.Engine.Tests/RssGeneratorTests.cs`
- `tests/Bukit.Engine.Tests/SitemapPluginTests.cs`
- `tests/Bukit.Engine.Tests/SitemapPublishProjectionCollectionTests.cs`
- `tests/Bukit.Engine.Tests/ListRouteSitemapPolicyTests.cs`
- `tests/Bukit.Engine.Tests/I18nMergedSitemapCollectionTests.cs`
- `tests/Bukit.Engine.Tests/ContentGraphValidateStageCollectionModeTests.cs`
- `tests/Bukit.Engine.Tests/RoutePipelineTests.cs`
- `tests/Bukit.Engine.Tests/ArchivePluginTests.cs`

## Audit and boundaries

- No collection fallback to content type remains in the changed consumers.
- Collectionless documents cannot leak into home/all lists through the shared
  index.
- Sitemap exclusion paths are normalized identically to SEO index paths.
- Schema `off` issues are removed before logging, output, and strict blocking.
- Unmapped validation issues use the global schema mode.
- All unrelated staged and unstaged dataIndex/config/render/docs changes were
  preserved.

## Commit

`refactor(engine): use collection as the sole grouping key` (this Task 8 commit)
