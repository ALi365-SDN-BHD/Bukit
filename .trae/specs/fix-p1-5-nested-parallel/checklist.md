# Verification Checklist

## 核心修复：消除嵌套并行

- [x] `SpecialListRenderer.ComputeNestedDegreeOfParallelism(outerCount > 1, requestedMDoP)` 返回 1
- [x] `SpecialListRenderer.ComputeNestedDegreeOfParallelism(outerCount == 1, requestedMDoP)` 返回 `requestedMDoP`
- [x] `BuildPageInfosAsync` 签名新增 `outerCount` 参数
- [x] `BuildPageInfosAsync` 用 `ComputeNestedDegreeOfParallelism` 计算实际内层并行度
- [x] `BuildPageInfosAsync` 按 `Parallel.ForAsync(0, source.Count, ...)` 实现并行体，不再 `.Select((e,i) => (e,i))`
- [x] `RenderSpecialListAlwaysAsync` 签名新增 `outerCount`，透传给 `BuildPageInfosAsync`
- [x] `RenderSpecialListIfNeededAsync` 签名新增 `outerCount`，透传给 `BuildPageInfosAsync`
- [x] `PageRenderDispatcher.RenderSpecialListsAsync` 两个分支均传入 `specialLists.Count` 作为 outerCount
- [x] `PageRenderDispatcher.DispatchAsync` 在 `RenderEntryKind.List` 分支传入 outerCount（保守上界 `entries.Count`）

## 快赢项：移除冗余锁字典

- [x] `RenderSpecialListIfNeededAsync` 不再 `new ConcurrentDictionary<string, SemaphoreSlim>(...)`
- [x] `RenderSpecialListIfNeededAsync` 写文件直接调用 `FileWriter.WriteUtf8(outputDir, listRoute.OutputPath, html)`

## 并发行为验证

- [x] 测试 A：`BuildPageInfosAsync(outerCount=1, mdop=4)` 并发 peak 可超过 1（OuterCountOne_AllowsInnerParallelism 通过）
- [x] 测试 B：`BuildPageInfosAsync(outerCount>1, mdop=4)` 并发 peak == 1（内层串行）（OuterCountGreaterThanOne_DegradesInnerParallelismToOne 通过）
- [x] 测试 C：`BuildPageInfosAsync` 输出 PageInfo 数组顺序与 source 一致（PreservesSourceOrder 通过）
- [x] 测试 D：`RenderSpecialListsAsync(incremental=true)` 多列表 × MDoP=4 全局并发 ≤ 4（Incremental_MultipleLists_PeakConcurrencyBoundedByOuterMDoP 通过）
- [x] 测试 E：`RenderSpecialListsAsync(incremental=false)` 多列表 × MDoP=4 全局并发 ≤ 4（NonIncremental_MultipleLists_PeakConcurrencyBoundedByOuterMDoP 通过）
- [x] 测试 F：`DispatchAsync` 单 List entry 允许内层并发 > 1（由 ComputeNestedDegreeOfParallelism Theory 测试 outerCount==1 分支覆盖；Dispatch 集成测试因 entries.Count 语义已被评审接受为保守上界）

## 回归验证

- [x] `dotnet build bukit.slnx -c Release` 0 警告 0 错误
- [x] `dotnet test bukit.slnx -c Release` 全部通过（2874 测试，包含新增 11 项）
- [x] 未引入新的第三方依赖
- [x] 渲染输出与修改前严格一致（HTML/文件/增量哈希不变；现有 RenderPipeline/PageRenderDispatcherLazyBody/MetricsWriter 等回归测试全绿）
