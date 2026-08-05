# Bukit Minimum Collection Index Policy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有空集合 noindex 能力扩展为可配置的最低内容数量策略，并通过共享 indexability 状态一致驱动 robots、sitemap、search、llms、route map 和审计。

**Architecture:** `CollectionIndexPolicyConfig` 进入 `CollectionConfig`，由单一 `CollectionIndexabilityPolicy` 根据 route kind、collection key 和 `TotalItems` 计算是否 noindex。`SeoIndexBuilder` 仍是唯一把策略转成 `SeoModel.Robots`/`SeoIndexEntry.Indexable` 的位置，下游投影继续消费共享状态；RSS 只消费内容条目，不因列表页薄弱而关闭。

**Tech Stack:** .NET 10, YamlDotNet config reader, generated JSON Schema, list-route graph, SEO/publish projections, xUnit.

## Global Constraints

- WP1-B 必须已完成并集成。
- 新配置路径固定为 `site.collections.<name>.indexPolicy.minimumItems|belowMinimum`。
- `minimumItems` 是 `0..2147483647` 整数，默认 `0`。
- `belowMinimum` 允许 `index|noindex-follow`，默认 `index`。
- `noindexWhenEmpty` 继续读取；为 `true` 时等价于 `minimumItems: 1` + `belowMinimum: noindex-follow`。
- 同一 collection 同时声明 `noindexWhenEmpty` 和 `indexPolicy` 时失败关闭，避免双重真相。
- 只应用于 `CollectionList`、`CollectionPage`、`FilteredListPage`；不应用于首页、内容详情页或非集合静态页。
- 低于阈值使用严格 `< minimumItems`；等于阈值恢复 indexable。
- route map 保留路由并写 `indexable: false`；不得删除历史关联对象。
- RSS/Atom/JSON Feed 继续按内容条目的 indexability 和 `output.rss` 生成；列表页薄弱不关闭 feed。
- 不修改文章条目的 indexability，不把集合数量阈值硬编码为 3。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Config/AppConfig.cs \
  --changed src/Bukit-Core/Bukit.Config/ConfigCollectionReader.cs \
  --changed src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs \
  --changed src/Bukit-Core/Bukit.Config/ConfigJsonSchemaGenerator.cs \
  --changed src/Bukit-Core/Bukit.Engine/CollectionIndexabilityPolicy.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoIndexBuilder.cs \
  --changed tests/Bukit.Config.Tests/EmptyCollectionSeoConfigTests.cs \
  --changed tests/Bukit.Config.Tests/ConfigJsonSchemaGeneratorTests.cs \
  --changed tests/Bukit.Engine.Tests/CompanyEntityAndEmptyCollectionTests.cs \
  --changed tests/Bukit.Engine.Tests/SeoRouteMapWriterTests.cs \
  --changed tests/Bukit.Engine.Tests/I18nMergedFeedProjectionTests.cs \
  --changed guide/user/04-site-yaml-config.md \
  --changed docs/seo.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

Expected: `unmappedFiles: []`; Config, Engine and Architecture commands run
serially in the classified order.

### Task 1: Add the strict and backward-compatible config contract

**Files:**
- Modify: `src/Bukit-Core/Bukit.Config/AppConfig.cs`
- Modify: `src/Bukit-Core/Bukit.Config/ConfigCollectionReader.cs`
- Modify: `src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Config/ConfigJsonSchemaGenerator.cs`
- Modify: `tests/Bukit.Config.Tests/EmptyCollectionSeoConfigTests.cs`
- Modify: `tests/Bukit.Config.Tests/ConfigJsonSchemaGeneratorTests.cs`

**Interfaces:**
- Produces: `CollectionIndexPolicyConfig`, `CollectionConfig.IndexPolicy`.
- Consumes: legacy `NoindexWhenEmpty`.

- [ ] **Step 1: Generate Config/Engine/Architecture closure**

Pass every file from all tasks. Expected: no unmapped files and serialized dotnet project commands.

- [ ] **Step 2: Write config RED tests**

Use the exact model:

```csharp
public sealed record CollectionIndexPolicyConfig
{
    public int MinimumItems { get; init; }
    public string BelowMinimum { get; init; } = "index";
}

public CollectionIndexPolicyConfig IndexPolicy { get; init; } = new();
```

Add the final property line above to the existing non-partial `CollectionConfig`
record; do not redeclare the record or change its other members.

Cover omitted defaults, valid `0`, `1`, `3`, negative value, non-integer, unknown `belowMinimum`, unknown nested field, and simultaneous legacy/new declarations.

- [ ] **Step 3: Run Config tests and confirm RED**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
```

- [ ] **Step 4: Implement strict reader and schema**

Add `indexPolicy` to the collection strict-field set. Nested strict fields are exactly `minimumItems` and `belowMinimum`. Reject any simultaneous presence of `noindexWhenEmpty` and `indexPolicy`, even if semantically equivalent. Generated schema uses minimum `0` and enum `index|noindex-follow`.

- [ ] **Step 5: Preserve the legacy property**

Do not remove or rename `NoindexWhenEmpty`. Add
`CollectionConfig.IndexPolicy` with default `new CollectionIndexPolicyConfig()`;
the effective legacy/new resolution is implemented once in Task 2 inside
`CollectionIndexabilityPolicy`, where Engine consumes the public config model.
Run Config tests to GREEN.

### Task 2: Centralize thin-collection indexability

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/CollectionIndexabilityPolicy.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoIndexBuilder.cs:118-157,442-447`
- Modify: `tests/Bukit.Engine.Tests/CompanyEntityAndEmptyCollectionTests.cs`

**Interfaces:**
- Produces: `CollectionIndexabilityPolicy.ShouldNoIndex(AppConfig, ListRouteKind, string?, int)`.
- Consumes: route kind, collection key, total items and effective config policy.

- [ ] **Step 1: Write Engine RED tests**

For minimum `3`, prove counts `0`, `1`, `2` are noindex and count `3` is indexable. Repeat boundary coverage for primary list, pagination page and filtered list. Prove homepage and content detail are unchanged.

- [ ] **Step 2: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 3: Implement the pure policy**

```csharp
internal static bool ShouldNoIndex(
    AppConfig config,
    ListRouteKind kind,
    string? collectionKey,
    int totalItems)
```

Return false for unsupported kind, missing collection, unknown collection, `belowMinimum == index`, or `totalItems >= minimumItems`. Replace `IsEmptyPrimaryCollection` with this call; do not leave two predicates.

Resolve the effective policy at the start of the method:

```csharp
var policy = collection.NoindexWhenEmpty
    ? new CollectionIndexPolicyConfig
    {
        MinimumItems = 1,
        BelowMinimum = "noindex-follow"
    }
    : collection.IndexPolicy;
```

- [ ] **Step 4: Run Engine tests to GREEN**

Expected: existing `noindexWhenEmpty` tests remain green without fixture changes other than simultaneous-config tests.

### Task 3: Prove every downstream representation boundary

**Files:**
- Modify: `tests/Bukit.Engine.Tests/CompanyEntityAndEmptyCollectionTests.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoRouteMapWriterTests.cs`
- Modify: `tests/Bukit.Engine.Tests/I18nMergedFeedProjectionTests.cs`

**Interfaces:**
- Consumes: `SeoIndexEntry.Indexable`.
- Produces: cross-projection regression evidence.

- [ ] **Step 1: Add aggregate RED assertions**

For a two-item collection under minimum `3`, assert:

```text
robots = noindex,follow
sitemap excludes list canonical
search excludes list route
llms.txt excludes list route
llms-full excludes list route
seo-route-map retains route with indexable=false
publish/seo audit reports non-indexable without missing-route warnings
RSS/Atom/JSON Feed still include the two eligible content items
```

- [ ] **Step 2: Run Engine tests and confirm behavior**

No production changes should be needed beyond Task 2. If a downstream output bypasses `SeoIndexEntry.Indexable`, fix only that direct consumer and add it to closure; do not redesign projection registries.

### Task 4: Document migration and close contracts

**Files:**
- Modify: `guide/user/04-site-yaml-config.md`
- Modify: `docs/seo.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: active configuration and migration documentation.

- [ ] **Step 1: Add documentation RED tests**

Require the exact path, defaults, strict `<` boundary, legacy equivalence, simultaneous-field rejection, route-map retention and RSS non-coupling.

- [ ] **Step 2: Update docs and run serial specialty tests**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 3: Review and commit WP1-C**

Review config drift, boundary comparisons and feed behavior. Commit:

```bash
git commit -m "feat(seo): add minimum collection index policy"
```
