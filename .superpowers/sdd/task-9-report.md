# Task 9 Report: Separate content type and collection projections

## Scope completed

- `SeoIndexEntry` now exposes `Collection` as the last optional constructor
  parameter while preserving the existing constructor call shape.
- Content SEO entries keep the canonical content type in `ContentType` and
  project the canonical collection independently into `Collection`.
- List-route graph entries retain their generated `list` or `taxonomy` type
  and copy the nullable route collection without fallback.
- Legacy list entries identify a collection only by an exact normalized match
  against configured `listRoute`; home and unmatched routes remain
  collectionless.
- Search JSON now includes `collection` for content and generated list records
  without changing the existing `type` or `contentType` meanings.
- Machine-readability RSS, Atom, and JSON Feed expectations now require a
  non-derived entry with an explicit collection whose collection output policy
  enables RSS. Atom and JSON Feed additionally retain their format checks.
- SEO schema selection was locked with both type/collection conflict
  directions. Its existing type-only implementation passed RED design checks,
  so `SeoModelBuilder.cs` was not changed.
- `RssGenerator` was not changed because the locked task only required the SEO
  index projection and machine-readability consumers.

## RED evidence

- The new public-contract test initially failed because `SeoIndexEntry` had no
  `Collection` property.
- The first focused SEO/search run executed 9 tests: 3 passed and 6 failed.
  Failures proved that content SEO stored collection in `ContentType`, graph
  list entries did not expose collection, legacy list entries did not resolve
  configured `listRoute`, and search JSON omitted collection.
- The machine-readability collection matrix initially failed because an
  `article` in the RSS-enabled `news` collection was not treated as feed
  content.
- Both strict SEO schema direction tests passed before production changes:
  `type=post, collection=news` emits BlogPosting, while
  `type=page, collection=post` does not. This proved no
  `SeoModelBuilder.cs` production change was warranted.

The first brief-required Engine filter then exposed three stale tests rather
than production defects: two SEO assertions still treated collection as
`ContentType`, and a publish-audit `post` fixture omitted its explicit
collection. Those fixtures and assertions were migrated to the separated
contract. Atom/JSON Feed fixtures also now declare both the `post` collection
and its RSS-enabled output policy.

## GREEN evidence

- `Bukit.Engine.Abstractions.Tests`: 56 passed, 0 failed.
- Brief-required Engine filter after stale-fixture migration: 117 passed,
  0 failed.
- The matrix covers constructor compatibility, explicit content projection,
  graph and legacy lists, content and list search JSON, both schema directions,
  and feed eligibility for enabled, disabled, missing, and derived collection
  cases.
- No `Collection ?? ContentType` or other runtime compatibility fallback was
  introduced.

## Post-change targeted gate

The brief-required command passed:

```sh
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine.Abstractions/Plugins/SeoIndexEntry.cs \
  src/Bukit-Core/Bukit.Engine/SeoIndexBuilder.cs \
  src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs \
  src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Helpers.cs \
  src/Bukit-Core/Bukit.Engine/SeoModelBuilder.cs \
  tests/Bukit.Engine.Abstractions.Tests \
  tests/Bukit.Engine.Tests
```

All contract/docs/self-tests passed, followed by 56 passing Abstractions tests
and 1281 passing Engine tests. No full, release, smoke-all, test-all, or
whole-solution gate was run.

## Production files changed

- `src/Bukit-Core/Bukit.Engine.Abstractions/Plugins/SeoIndexEntry.cs`
- `src/Bukit-Core/Bukit.Engine/SeoIndexBuilder.cs`
- `src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs`
- `src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Helpers.cs`

`SeoModelBuilder.cs` was verified but left unchanged because its strict
type-only behavior was already correct.

## Direct test files changed

- `tests/Bukit.Engine.Abstractions.Tests/RouteAndIndexTests.cs`
- `tests/Bukit.Engine.Tests/SeoIndexBuilderTests.cs`
- `tests/Bukit.Engine.Tests/SearchIndexBuilderTests.cs`
- `tests/Bukit.Engine.Tests/SeoModelBuilderTests.cs`
- `tests/Bukit.Engine.Tests/MachineReadabilityCollectionProjectionTests.cs`
- `tests/Bukit.Engine.Tests/PublishAuditReportWriterTests.cs`

## Audit and boundaries

- The required bounded read-only sub-agent review returned `no findings`.
- Content type remains the schema/type discriminator; collection is used only
  as the grouping/output-policy key.
- Derived entries cannot become feed content merely by carrying a collection.
- Legacy home/unmatched list routes do not infer a collection.
- Existing unrelated staged and unstaged dataIndex/config/render/docs changes
  were preserved.

## Commit

The verified Task 9 code and tests were committed by the concurrent data-index
work in `f75205b2` before the isolated Task 9 commit could be created. The
history was not rewritten or split because that commit also contains user-owned
parallel work. This report is committed separately under the planned Task 9
subject to preserve the task audit trail.
