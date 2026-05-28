# Tasks

## Task 1: 新建 BodyCacheDecorator + 插入 ContentPipeline ✅
- [x] 1.1 新建 `BodyCacheDecorator.cs`
- [x] 1.2 `ConcurrentDictionary<string, Lazy<Task<ContentBody>>>` 缓存
- [x] 1.3 `BodyCacheMetrics` record 公开
- [x] 1.4 在 `ContentPipeline` 的 `ImageLocalize` stage 之后插入
- [x] 1.5 `ContentPipelineResult` 携带 `BodyCacheMetrics?`
- [x] 1.6 build 通过

## Task 2: MetricsWriter 追加 bodyCache 段 ✅
- [x] 2.1 `ContentPipelineResult.BodyCacheMetrics` 暴露
- [x] 2.2 `MetricsWriter.WriteIfRequested` 追加 `bodyCache` JSON 段
- [x] 2.3 build + test 通过

## Task 3: 单元测试 ✅
- [x] 3.1 同 BodyKey 只触发一次底层读取
- [x] 3.2 不同 BodyKey 各自缓存
- [x] 3.3 metrics 计数正确
- [x] 3.4 并发安全（10 线程）
- [x] 3.5 ContentHtml 内联不触发（7 tests, ContentPipelineTests adapted）

## Task 4: 验证整体正确性 ✅
- [x] 4.1 build 0 警告 0 错误
- [x] 4.2 format 通过
- [x] 4.3 524 Content + 1028 Engine + 730 Cli tests pass
- [x] 4.4 checklist 全部通过
