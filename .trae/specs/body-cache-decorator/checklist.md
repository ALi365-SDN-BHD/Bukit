# BodyCacheDecorator Checklist

## 装饰器实现
- [x] `BodyCacheDecorator` 实现 `IContentBodyStore` 接口
- [x] 使用 `ConcurrentDictionary<string, Lazy<Task<ContentBody>>>` 缓存
- [x] 缓存键 = `item.BodyKey ?? item.Id`
- [x] 公开 `BodyCacheMetrics` 属性
- [x] metrics 含 totalRequests、cacheHits、cacheMisses、uniqueBodies
- [x] 线程安全（`GetOrAdd` + `LazyThreadSafetyMode.ExecutionAndPublication`）

## Pipeline 集成
- [x] `ContentPipeline` 在 `ImageLocalizeStage` 之后插入 decorator
- [x] 不破坏现有 stage 顺序
- [x] `ContentPipelineResult.BodyCacheMetrics` 可访问

## Metrics 输出
- [x] `MetricsWriter` 输出 `bodyCache` JSON 段
- [x] 含 totalRequests、cacheHits、cacheMisses、uniqueBodies、amplification
- [x] amplification 计算正确
- [x] 仅当 bodyCacheMetrics 非 null 时输出

## 测试覆盖
- [x] 同一 BodyKey 多次请求 → 底层只调用 1 次
- [x] 不同 BodyKey → 各自缓存
- [x] metrics 计数正确
- [x] 并发安全测试
- [x] ContentHtml 内联模式 → 不经过 decorator

## 回归验证
- [x] `dotnet build bukit.slnx -c Release` 0 警告 0 错误
- [x] `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 全部 Bukit.Content.Tests 通过（524）
- [x] 全部 Bukit.Engine.Tests 通过（1028）
- [x] 全部 Bukit.Cli.Tests 通过（730/733）
- [x] 不破坏现有 CLI
- [x] 不破坏 examples/starter 构建
- [x] 无 AOT 不兼容代码
