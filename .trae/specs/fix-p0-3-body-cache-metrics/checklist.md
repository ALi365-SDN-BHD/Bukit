# Checklist

- [x] `BodyCacheDecorator` 新增 `_inlineBypasses` 字段，第 42 行用 `_inlineBypasses` 替代 `_cacheHits`
- [x] `BodyCacheMetrics` record 新增 `InlineBypasses` 字段
- [x] `MetricsWriter` 在 JSON bodyCache 段输出 `inlineBypasses` 字段
- [x] 内联路径测试：`InlineBypasses=1`，`CacheHits=0`
- [x] 缓存命中测试：正常路径 CacheHits 不受影响
- [x] 新增恒等式测试：`TotalRequests == CacheHits + CacheMisses + InlineBypasses`
- [x] `dotnet build bukit.slnx -c Release` 0 错误 0 警告
- [x] Content Tests 全部通过（544/544）
- [x] Engine Tests 全部通过（1072/1072）
- [x] MetricsWriter JSON schema 向后兼容（新增字段不破坏已有消费者）
