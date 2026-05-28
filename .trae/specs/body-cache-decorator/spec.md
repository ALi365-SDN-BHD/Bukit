# BodyCacheDecorator — 构建级 Body 读取缓存 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P1-1

## Why

当前 9 个独立的 body 读取路径（PageRenderDispatcher、SpecialListRenderer、RssGenerator、SearchIndexBuilder、DataModuleBuilder、LlmsTxtPlugin、IncrementalBuildEngine、I18nOutputMerger）各自直接调用 `bodyStore.GetAsync()`。Markdown provider 无缓存，每读一次就重新 Markdig 解析。1000 篇文章 × 5 路径 = 5000 次解析，AOT 下内存压力明显。

## What Changes

- 新建 `BodyCacheDecorator` — `IContentBodyStore` 装饰器，使用 `ConcurrentDictionary<string, Lazy<Task<ContentBody>>>` 按 `BodyKey` 缓存
- 插入 `ContentPipeline` — 在 `ImageLocalizeStage` 之后（确保图片已本地化后再缓存），成为后续所有读取的上游
- 在 `MetricsWriter` 中追加 `bodyCache` 段：totalRequests、cacheHits、cacheMisses、uniqueBodies、amplification
- 目标：amplification <= 1.5

## Impact

- Affected specs: 无
- Affected code:
  - `src/Bukit.Content/BodyCacheDecorator.cs` — **新建** 装饰器类
  - `src/Bukit.Engine/ContentPipeline.cs` — 插入装饰器到 pipeline
  - `src/Bukit.Engine/MetricsWriter.cs` — 追加 `bodyCache` 指标段

## ADDED Requirements

### Requirement: BodyCacheDecorator 构建级缓存

The system SHALL provide a `BodyCacheDecorator` that wraps any `IContentBodyStore` and caches body reads at the build level.

- 缓存键 = `item.BodyKey ?? item.Id`
- 使用 `ConcurrentDictionary<string, Lazy<Task<ContentBody>>>` 模式（`LazyThreadSafetyMode.ExecutionAndPublication`）
- 同步调用（`GetAwaiter().GetResult()`）兼容：`Lazy<>.Value` 对同步/异步均安全
- 线程安全（`GetOrAdd` 原子操作）
- 公开 metrics 属性：`TotalRequests`、`CacheHits`、`CacheMisses`、`UniqueBodies`

#### Scenario: 同一 BodyKey 只触发一次底层读取

- **GIVEN** `BodyCacheDecorator` 包装了一个 `IContentBodyStore`
- **WHEN** 同一个 `BodyKey` 被请求 3 次（并发或顺序）
- **THEN** 底层 store 的 `GetAsync` 只被调用 1 次
- **THEN** `TotalRequests=3`，`CacheHits=3`，`CacheMisses=1`，`UniqueBodies=1`

#### Scenario: 不同 BodyKey 各自缓存

- **GIVEN** `BodyCacheDecorator`
- **WHEN** 请求 `BodyKey="a"` 和 `BodyKey="b"` 各 1 次
- **THEN** 底层 store 被调用 2 次

#### Scenario: ContentHtml 内联不触发底层读取

- **GIVEN** ContentItem 的 `ContentHtml` 非空（内联模式）
- **WHEN** 通过 `ContentBodyResolver.GetHtmlAsync` 读取
- **THEN** 不经过 `BodyCacheDecorator` 即可返回

### Requirement: Pipeline 插入点

The system SHALL insert `BodyCacheDecorator` in `ContentPipeline` after `ImageLocalizeStage` and before `DraftFilterStage`.

```
Load → ImageLocalize → [BodyCacheDecorator] → DraftFilter → SchemaDefaults → SchemaValidate → CollectionWarning
```

### Requirement: bodyCache Metrics 输出

The system SHALL output a `bodyCache` section in `.bukit/metrics.json` when `--metrics` is enabled.

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

- `amplification = totalRequests / uniqueBodies`
- 当 `uniqueBodies == 0` 时 `amplification = 0`
