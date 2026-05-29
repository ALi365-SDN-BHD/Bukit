# Tasks

- [x] Task 1: 设计内层并行度策略
  - [x] SubTask 1.1: 在 `SpecialListRenderer` 中新增私有静态 `ComputeNestedDegreeOfParallelism(int outerCount, int requestedMDoP)`：`outerCount > 1` 返回 1；`outerCount == 1` 返回 `requestedMDoP > 0 ? requestedMDoP : Environment.ProcessorCount`；`outerCount <= 0` 视作 1
  - [x] SubTask 1.2: 为 `ComputeNestedDegreeOfParallelism` 添加 4 条小型单元测试（outerCount=0/1/2/N × requestedMDoP=0/1/4），覆盖回退分支

- [x] Task 2: 让 `BuildPageInfosAsync` 接收 outerCount 并按索引消费 source
  - [x] SubTask 2.1: 修改 `BuildPageInfosAsync` 签名，新增参数 `int outerCount`，放在 `maxDegreeOfParallelism` 之后（保留语义：原 `maxDegreeOfParallelism` 仍代表「请求的内层 MDoP」）
  - [x] SubTask 2.2: 在方法体内通过 `ComputeNestedDegreeOfParallelism(outerCount, maxDegreeOfParallelism)` 计算 `effectiveMDoP`
  - [x] SubTask 2.3: 把 `Parallel.ForEachAsync(source.Select((entry, i) => (entry, i)), ...)` 替换为 `Parallel.ForAsync(0, source.Count, options, async (i, ct) => { ... pageInfos[i] = ...; })`，源数据直接通过 `source[i]` 访问
  - [x] SubTask 2.4: 保留 `metricsLock` 与 `stageMetrics` 行为；保留 PageInfo 数组顺序一致性
  - [x] SubTask 2.5: 在 `RenderSpecialListAlwaysAsync` 与 `RenderSpecialListIfNeededAsync` 的签名中各新增 `int outerCount` 参数，并将其透传给 `BuildPageInfosAsync`

- [x] Task 3: 调用方传入 outerCount
  - [x] SubTask 3.1: 修改 `PageRenderDispatcher.RenderSpecialListsAsync`：在两个 `Parallel.ForEachAsync` 分支（incremental / non-incremental）调用前先取 `var outerCount = specialLists.Count;`，调用 `RenderSpecialListIfNeededAsync` / `RenderSpecialListAlwaysAsync` 时把 `outerCount` 传入新参数
  - [x] SubTask 3.2: 修改 `PageRenderDispatcher.DispatchAsync`：在 `case RenderEntryKind.List` 的 `SpecialListRenderer.BuildPageInfosAsync(...)` 调用处，传入 `outerCount = entries.Count` 作为 outerCount（保守上界：包含 Page/Static 条目；评审已确认语义安全）
  - [x] SubTask 3.3: 检索仓库内其他 `BuildPageInfosAsync` 直接调用点，全部补 outerCount 参数

- [x] Task 4: 移除增量分支的冗余写锁字典（快赢项 5）
  - [x] SubTask 4.1: 在 `RenderSpecialListIfNeededAsync` 中删除 `new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase)` 实参
  - [x] SubTask 4.2: 直接调用 `FileWriter.WriteUtf8(outputDir, listRoute.OutputPath, html)`（同步即可，行为与现状一致；保留 cancellationToken 检查在前）
  - [x] SubTask 4.3: 保留对 manifest 的 `lock (manifest)` 行为不变

- [x] Task 5: 回归测试
  - [x] SubTask 5.1: 新增 `tests/Bukit.Engine.Tests/SpecialListRendererNestedParallelTests.cs`（9 个测试，含 ConcurrencyProbeBodyStore CAS 峰值跟踪）
  - [x] SubTask 5.2: 新增 `tests/Bukit.Engine.Tests/PageRenderDispatcherNestedParallelTests.cs`（2 个集成测试，增量/非增量分支 peak ≤ outer MDoP）
  - [x] SubTask 5.3: 复用 ContentItem/RouteInfo/SiteModel 测试工厂模式（参考 PageRenderDispatcherLazyBodyTests）

- [x] Task 6: 全量回归与构建验证
  - [x] SubTask 6.1: `dotnet build bukit.slnx -c Release` 0 警告 0 错误
  - [x] SubTask 6.2: `dotnet test bukit.slnx -c Release` 全部 2874 测试通过（基线增加 11 项新测试）
  - [x] SubTask 6.3: `dotnet format bukit.slnx --verify-no-changes` 通过（无格式问题）

- [x] Task 7: 代码评审
  - [x] SubTask 7.1: superpowers:code-reviewer 子代理评审：Ready to proceed，无关键/重要阻塞问题
  - [x] SubTask 7.2: 评审反馈：（可选优化项，非阻塞）listInnerBodyLoad 指标和 debug trace 日志属于附加可观测性补强，不在审计报告 P1-5 强制要求内，留作后续 issue；ConcurrencyProbeBodyStore 重复定义可后续抽取

# Task Dependencies

- Task 2 依赖 Task 1（需要 `ComputeNestedDegreeOfParallelism`）
- Task 3 依赖 Task 2（新签名）
- Task 4 与 Task 1-3 独立，可并行执行
- Task 5 依赖 Task 1-4 完成（行为已定型才能写并发断言）
- Task 6 依赖 Task 1-5 全部完成
- Task 7 依赖 Task 6 通过
