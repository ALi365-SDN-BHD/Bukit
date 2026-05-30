# BodyCacheDecorator 指标修复 Spec

## Why
`BodyCacheDecorator.GetAsync` 中存在一个**指标语义错误**：当 `ContentItem.ContentHtml` 已预填充（由 `ContentImageRewritePipeline` 在 `ImageLocalize` 阶段写入）时，decorator 跳过缓存查询直接返回，但却将其计为 `CacheHits`。这不是缓存命中——数据来自 ContentItem 本身（内联直通路径），`_cache` 字典从未被查询。该 bug 导致缓存命中率虚高、amplification 被低估，监控数据失真。

## What Changes
- 新增 `_inlineBypasses` 计数器，区分 "内联直通" 与 "真正的缓存命中"
- `BodyCacheMetrics` record 新增 `InlineBypasses` 字段
- 第 42 行 `Interlocked.Increment(ref _cacheHits)` → `Interlocked.Increment(ref _inlineBypasses)`
- `MetricsWriter` 输出新增 `inlineBypasses` 字段（向后兼容：仅当 > 0 或 metrics 存在时输出）
- 现有测试适配——内联路径的预期 `CacheHits` 从 1 变为 0，新增 `InlineBypasses` 断言

## Impact
- Affected specs: `body-cache-decorator`（修改 BodyCacheMetrics 输出）
- Affected code: `src/Bukit.Content/BodyCacheDecorator.cs`、`src/Bukit.Engine/MetricsWriter.cs`、`tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs`

## MODIFIED Requirements

### Requirement: BodyCacheDecorator 内联直通路径独立计数

原需求（body-cache-decorator spec）：

> Scenario: 同一 BodyKey 只触发一次底层读取
> THEN `TotalRequests=3`，`CacheHits=3`，`CacheMisses=1`，`UniqueBodies=1`

修改为：

> THEN `TotalRequests=3`，`CacheHits=2`（两次 GetOrAdd 竞态命中），`CacheMisses=1`，`UniqueBodies=1`

原需求：

> Scenario: ContentHtml 内联不触发底层读取

修改为：

> - GIVEN ContentItem 的 `ContentHtml` 非空（内联模式）
> - WHEN 通过 `BodyCacheDecorator.GetAsync` 读取
> - THEN 不经过缓存字典，不触发底层 store
> - THEN `TotalRequests=1`，`InlineBypasses=1`，`CacheHits=0`，`CacheMisses=0`

### Requirement: bodyCache Metrics 输出扩展

原输出：

```json
{
  "bodyCache": {
    "totalRequests": 5240,
    "cacheHits": 4240,
    "cacheMisses": 1000,
    "uniqueBodies": 1000,
    "amplification": 5.24
  }
}
```

新增 `inlineBypasses` 字段：

```json
{
  "bodyCache": {
    "totalRequests": 5240,
    "cacheHits": 3240,
    "cacheMisses": 1000,
    "inlineBypasses": 1000,
    "uniqueBodies": 1000,
    "amplification": 5.24
  }
}
```

- `inlineBypasses`：内联 ContentHtml 直通次数（不经过缓存字典）
- `CacheHits + CacheMisses + InlineBypasses` = `TotalRequests`（语义恒等式）
