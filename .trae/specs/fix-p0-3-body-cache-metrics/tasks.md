# Tasks

- [x] Task 1: BodyCacheDecorator 新增 `_inlineBypasses` 计数器
  - 添加 `private long _inlineBypasses;` 字段
  - 第 42 行 `Interlocked.Increment(ref _cacheHits)` → `Interlocked.Increment(ref _inlineBypasses)`
  - `BodyCacheMetrics` record 新增 `long InlineBypasses` 字段（位于 CacheMisses 之后）
  - `Metrics` 属性暴露 `Volatile.Read(ref _inlineBypasses)`
  - 验证：`dotnet build src/Bukit.Content/Bukit.Content.csproj -c Release` ✅

- [x] Task 2: MetricsWriter 输出 `inlineBypasses` 字段
  - 读取 `BodyCacheMetrics.InlineBypasses`
  - 在 JSON `bodyCache` 段中输出 `inlineBypasses` 字段
  - 验证：`dotnet build src/Bukit.Engine/Bukit.Engine.csproj -c Release` ✅

- [x] Task 3: 更新现有测试
  - `BodyCacheDecoratorTests` 中内联路径测试：预期 `InlineBypasses=1`、`CacheHits=0`
  - 同 BodyKey 多次请求测试：`CacheHits=2`（两次 GetOrAdd 竞态命中）、`CacheMisses=1`
  - 新增：验证 `InlineBypasses=0` 的正常缓存路径测试
  - 新增：验证 `TotalRequests == CacheHits + CacheMisses + InlineBypasses` 恒等式测试
  - 验证：544/544 Content Tests 通过 ✅

- [x] Task 4: 全量回归验证
  - `dotnet build bukit.slnx -c Release` 0 错误 0 警告 ✅
  - `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release` 1072/1072 ✅
  - `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release` 544/544 ✅

# Task Dependencies
- Task 2 依赖 Task 1 完成（需要新的 record 字段）
- Task 3 依赖 Task 1 完成
- Task 4 依赖 Task 1、2、3 完成
