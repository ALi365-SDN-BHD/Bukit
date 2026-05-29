# 修复 P1-5 嵌套并行（SpecialListRenderer 线程池过载）Spec

## Why

审计报告 `bukit-deep-audit-report-2026-05-29.md` 标记 P1-5（高严重度性能问题）：`PageRenderDispatcher.RenderSpecialListsAsync` 与 `PageRenderDispatcher.DispatchAsync` 中已经在 `Parallel.ForEachAsync` 内执行特殊列表渲染，而 `SpecialListRenderer.BuildPageInfosAsync`（行 169）以及上游 `RenderSpecialListIfNeededAsync` / `RenderSpecialListAlwaysAsync` 又把同样的 `maxDegreeOfParallelism` 透传到内层 `Parallel.ForEachAsync`，对每条目逐个 await `bodyStore` 加载。

实际并行峰值约为 `MDoP × MDoP`，在 `MDoP = ProcessorCount`（默认）下：

- 线程池被超额申请 worker，触发线程池 starvation 与上下文切换风暴；
- 多个并行的特殊列表互相挤占 body 缓存读取槽位，热点 `Lazy<>` 等待时间变长；
- 取消信号传播延迟（嵌套循环各自检查 CancellationToken）；
- 写文件锁集合 `ConcurrentDictionary<string, SemaphoreSlim>` 增长更快（虽与本任务正交，但被嵌套放大）。

同时审计 §4.2 「快赢项」明确指出可顺手做两个微优化：
- 第 4 条：用 `Parallel.For` / 显式索引循环替代 `.Select((entry, i) => (entry, i)).Parallel.ForEachAsync(...)`，避免中间 tuple 分配；
- 第 5 条：`RenderSpecialListIfNeededAsync` L116 已经持有专属写路径，不需要每次 new 一个空的 `ConcurrentDictionary<string, SemaphoreSlim>` 当锁字典——直接调用 `FileWriter.WriteUtf8` 即可。

本 spec 把 P1-5 的根因（嵌套并行）与两个相关快赢项（避免中间 tuple、移除冗余 lock 字典）合并修复。

## What Changes

- **核心修复**：消除 `SpecialListRenderer.BuildPageInfosAsync` 与上层 `RenderSpecialListsAsync`/`DispatchAsync` 之间的嵌套并行：
  - 由 `BuildPageInfosAsync` 的调用方传入一个新的参数 `innerMaxDegreeOfParallelism`，并在「外层已经并行执行多个特殊列表」的语境下默认填 `1`（即按条目顺序串行加载 body），保留单一外层并行度。
  - 在「外层是串行迭代单个列表的语境」（例如 `Dispatch` 中只有一个 list 在跑、或 incremental disabled 单 list 场景）仍可保持内层多线程，便于充分利用 body 加载等待时间。
  - 通过新增的辅助 `ComputeNestedDegreeOfParallelism(outerCount, requestedMDoP)` 决定内层 MDoP：当 `outerCount > 1` 时压成 1；当 `outerCount == 1` 时回退到 `requestedMDoP`，避免单 list 情况下退化为串行。
- **快赢项 4**：`BuildPageInfosAsync` 内部去掉 `source.Select((entry, i) => (entry, i))` 中间投影；采用 `Parallel.ForAsync(0, source.Count, ...)` 或基于索引的循环结构，直接消费 `source[i]`，避免每个条目一次 tuple 装箱与额外枚举器分配。
- **快赢项 5**：`RenderSpecialListIfNeededAsync` 中 `new ConcurrentDictionary<string, SemaphoreSlim>(...)` 仅当写锁字典使用一次后即被丢弃，且只有一个 key。改为直接调用 `FileWriter.WriteUtf8`，并删除该字典分配。
- **配置/可观测性补强**：
  - `BuildStageMetricsCollector` 新增（或复用）一组指标，记录 `listInnerBodyLoad` 实际并行度，便于在 metrics.json 中验证降级行为；
  - 在 `BuildPageInfosAsync` 顶部增加 trace 日志（debug 级），输出 `outerCount`、`requestedMDoP`、`effectiveInnerMDoP`，仅在 debug 或 verbose 模式打印。
- **回归测试**：
  - 新增 `SpecialListRendererNestedParallelTests`，使用一个可观测 `IContentBodyStore`（CountingBodyStore，内部 Interlocked 跟踪 `_currentConcurrency` 峰值），断言：
    - 当 `BuildPageInfosAsync` 在 `outerCount > 1` 时，单次调用内并发不会超过 1（即内层串行）；
    - 当 `outerCount == 1` 时，并发可超过 1（保留内层并行）。
  - 新增 `PageRenderDispatcherNestedParallelTests`，用同样的 CountingBodyStore 跑 `RenderSpecialListsAsync(incrementalEnabled: true)` 与 `Dispatch(RenderEntryKind.List)`，断言整体峰值并发不超过 `MDoP`（外层）。
  - 现有 `RenderPipelineTests`、`PageRenderDispatcherLazyBodyTests`、`MetricsWriterTests` 等用例必须继续通过（基线 ≥ 2847 通过 / 0 失败）。
- **不改变**：渲染输出（HTML、文件内容）、增量哈希逻辑、写文件路径与编码。

## Impact

- **Affected specs**：core-hardening-p0-p1（性能审计跟进）、body-cache-decorator（body 加载行为）、incremental-hash-coverage（增量列表渲染）。
- **Affected code**：
  - `src/Bukit.Engine/SpecialListRenderer.cs`（修改 `BuildPageInfosAsync`、`RenderSpecialListIfNeededAsync`、`RenderSpecialListAlwaysAsync` 签名与实现）
  - `src/Bukit.Engine/PageRenderDispatcher.cs`（`DispatchAsync` 与 `RenderSpecialListsAsync` 调用点传入 outerCount）
  - 对应测试项目：`tests/Bukit.Engine.Tests`

## ADDED Requirements

### Requirement: 特殊列表渲染消除嵌套并行
系统 SHALL 在外层 `Parallel.ForEachAsync` 调用 `SpecialListRenderer` 时，把内层 `BuildPageInfosAsync` 的并行度限制为 1，使得并行峰值不超过外层 `MaxDegreeOfParallelism`。

#### Scenario: 多个特殊列表同时构建
- **WHEN** 一个站点同时有 5 个特殊列表（如 `/`、`/posts/`、`/tags/`、`/category/foo/`、`/category/bar/`），调用 `PageRenderDispatcher.RenderSpecialListsAsync(maxDegreeOfParallelism = 4)`
- **THEN** 通过仪表化 body store 观察到的并发 body 加载数量峰值 SHALL ≤ 4，而非旧实现的 4×4 = 16

#### Scenario: 单一特殊列表
- **WHEN** 站点只产生 1 个特殊列表，调用 `PageRenderDispatcher.DispatchAsync` 处理一条 `RenderEntryKind.List`
- **THEN** `BuildPageInfosAsync` 内层并行度回退到 `requestedMDoP`，并发 body 加载数量峰值 SHALL 仍可达到 `requestedMDoP`

### Requirement: BuildPageInfosAsync 避免中间 tuple 分配
系统 SHALL 在 `SpecialListRenderer.BuildPageInfosAsync` 的并行路径中按索引直接消费源集合，而非通过 `Select((entry, i) => (entry, i))` 制造中间序列。

#### Scenario: 大列表渲染
- **WHEN** `source.Count = 500`
- **THEN** `BuildPageInfosAsync` 内部 SHALL NOT 为每个条目分配一个 `(entry, i)` 值元组，并保持 PageInfo 顺序与 `source` 顺序一致

### Requirement: 增量特殊列表写文件移除冗余锁字典
系统 SHALL 在 `RenderSpecialListIfNeededAsync` 中，当确实需要写出 HTML 时，直接调用 `FileWriter.WriteUtf8`，不再为单次写入分配 `ConcurrentDictionary<string, SemaphoreSlim>`。

#### Scenario: 增量构建一次特殊列表渲染
- **WHEN** `RenderSpecialListIfNeededAsync` 走到 `canSkip == false` 分支
- **THEN** 该方法 SHALL 通过 `FileWriter.WriteUtf8(outputDir, listRoute.OutputPath, html)` 写文件，且不再创建 `new ConcurrentDictionary<string, SemaphoreSlim>(...)` 实例

## MODIFIED Requirements

### Requirement: `RenderSpecialListsAsync` 并发控制语义
`PageRenderDispatcher.RenderSpecialListsAsync` 与 `PageRenderDispatcher.DispatchAsync` SHALL 在调用 `SpecialListRenderer.*` 时显式传入「外层 outerCount」与「期望的内层 innerMDoP」，由 `SpecialListRenderer` 决定最终内层并行度（多于一项外层任务时压制为 1）。

#### Scenario: 增量分支多列表
- **WHEN** `RenderSpecialListsAsync(incrementalEnabled = true, maxDegreeOfParallelism = N)` 调用并 `specialLists.Count > 1`
- **THEN** 每个 `RenderSpecialListIfNeededAsync` 在内部调用 `BuildPageInfosAsync` 时收到 `innerMDoP = 1`

#### Scenario: 非增量分支多列表
- **WHEN** `RenderSpecialListsAsync(incrementalEnabled = false, maxDegreeOfParallelism = N)` 调用并 `specialLists.Count > 1`
- **THEN** 每个 `RenderSpecialListAlwaysAsync` 在内部调用 `BuildPageInfosAsync` 时收到 `innerMDoP = 1`

#### Scenario: 通用 dispatcher
- **WHEN** `DispatchAsync` 在外层 `Parallel.ForEachAsync` 内处理 `RenderEntryKind.List` 时
- **THEN** 调用 `SpecialListRenderer.BuildPageInfosAsync` 时 SHALL 传入 `innerMDoP = 1`

## REMOVED Requirements

无。

## 行为约束

- 现有所有测试必须继续通过（基线由当前 main 分支 `dotnet test bukit.slnx -c Release` 决定，预期 ≥ 2847 通过 / 0 失败）。
- `dotnet build bukit.slnx -c Release` 0 警告 0 错误。
- 渲染输出（HTML 字符串、文件字节、增量哈希值）必须与本次修改前严格一致。
- 不引入新的第三方依赖。
- 不破坏外部公开 API；`SpecialListRenderer` 与 `PageRenderDispatcher` 均为 `internal`，签名变更允许。
- 不回退用户在工作区已有的其他未提交修改。
