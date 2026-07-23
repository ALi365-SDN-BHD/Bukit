# Bukit Core G-04D9A Build Orchestration Graph 受控收窄台账

> 日期：2026-07-23
>
> 任务：G-04 Group 4 / Task 33
>
> G4 基线：`2.0@729088dbc2faf1bf7a20fe670e96a09b7568e7ba`
>
> 状态：implementation-complete / g4-verification-pending

## 1. 范围与结论

本任务只处理 D9A 七个 historical/current Engine candidates，不修改 Labs 或外部插件：

| 类型 | 终态 | 原因 |
|---|---|---|
| `BuildPipeline` | internalized in 2.0 | 只由 `SiteEngine` 创建；与 context 构成内部执行图 |
| `BuildPipelineContext` | internalized in 2.0 | 只在 pipeline/executor 与 `SiteEngine` 内传播 |
| `RoutePipeline` | internalized in 2.0 | 只由 Engine variant route stage 创建 |
| `RoutePipelineResult` | internalized in 2.0 | 只在 route/variant 内部图传播 |
| `BuildOptions` | retained public | public `SiteEngine.BuildAsync(IContentProvider, BuildOptions, ...)` 参数 |
| `BuildVariantSummary` | retained public | public `BuildResult.Variants`、constructor 与 deconstruction companion |
| `ContentPipelineResult` | retained public | public `ContentPipeline.ExecuteAsync(...)` 返回类型 |

因此本任务是 `4 internalized + 3 retained + 0 parent API drift`，不是七项批量收窄。

## 2. 原子修改

production 只改变四个顶层类型的 accessibility：

```text
BuildPipeline.cs
  public BuildPipelineContext -> internal BuildPipelineContext
  public BuildPipeline        -> internal BuildPipeline

RoutePipeline.cs
  public RoutePipelineResult  -> internal RoutePipelineResult
  public RoutePipeline        -> internal RoutePipeline
```

四个类型仍然存在且保持 sealed。其 constructors、methods、properties、record equality、
deconstruction、executor delegate、optional defaults 和内部调用点均不修改。

两对必须原子处理：

- context 若先 internal 而 pipeline 仍 public，会形成 inconsistent accessibility；
- result 若先 internal 而 route pipeline 仍 public，同样会形成 public return type 泄漏；
- 只收窄 parent 又会留下孤立 public companion，偏离批准终态。

## 3. 明确保留的公共合同

以下 production 文件没有因 D9A 修改：

- `BuildOptions.cs`；
- `BuildResult.cs`；
- `ContentPipeline.cs`；
- `SiteEngine.cs`。

保留的 exact propagation：

```text
SiteEngine.BuildAsync(IContentProvider, BuildOptions, CancellationToken)
BuildResult.Variants -> IReadOnlyList<BuildVariantSummary>
ContentPipeline.ExecuteAsync(...) -> Task<ContentPipelineResult>
```

三个 companion 在 current baseline 中改为
`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`。这只是治理分类收口；
它们的 signature、public members、record shape 和 runtime behavior 不变。

## 4. Baseline 与历史证据

Task 33 后的 current baseline 投影为：

```text
14 assemblies / 480 public types / 49 candidates
```

变化解释：

- 四个 internalized 类型移出 exported baseline；
- 三个 retained 类型仍在 baseline，但不再处于 `2.0-candidate`；
- 因此 public type 减少 4，未关闭 candidate 减少 7。

closed historical manifest 不修改：

```text
declarationState = closed
candidateCount = 136
candidates.length = 136
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

七个 D9A historical entries 继续记录
`consumer-declaration-pending / no-public-match-found /
unknown-until-voluntary-declaration`。公开搜索阴性不被改写为“不存在消费者”。

## 5. 新增架构断言

`G04D9ABuildOrchestrationGraphTests` 已加入 G4 最终测试清单，锁定：

1. 四个批准类型仍可由 exact full name 取得，但为 internal、sealed 且不再 exported；
2. build executor、context constructor/properties 与 cancellation optional default；
3. route execute 参数/返回、result 三列表 shape 与 internal `ListRouteGraph`；
4. `BuildOptions`、`BuildVariantSummary`、`ContentPipelineResult` 继续 exported；
5. 三个 stable parent 的 exact type propagation；
6. Engine friend boundary 仍只有 `Bukit.Engine.Tests` 与 `bukit`；
7. current baseline 精确为 `14/480/49`；
8. 七项 historical entries、136 项清单和 Git blob 不变；
9. 两份活动治理文档记录相同 current baseline 与 D9A 兼容性结论。

既有 Engine behavior tests 不删除、不弱化：

- `BuildPipelineTests`：executor/context identity、结果返回、cancellation token 透传；
- `RoutePipelineTests`：内容/data 过滤、template、list、pagination、collision 与 i18n；
- `VariantBuildPipelineTests`：route result 在 variant planner 中的内部传播；
- `BuildOptionsTests`、`BuildOptionsMapperTests`：retained options 默认值与映射；
- build/report/integration tests：`BuildResult` variant/report shape。

## 6. 兼容性与迁移

这是获批的 2.0 CLR source/binary/reflection break。直接构造或显式引用四个 internalized
类型的消费者必须迁移到 retained public entry points：

- 站点构建使用 `SiteEngine`；
- build result/variant 读取使用 `BuildResult`；
- content acquisition pipeline 使用 `ContentPipeline`。

本任务不删除 `BuildOptions` overload，不新增替代 DTO、facade、friend assembly、
reflection fallback 或 dynamic activation。

## 7. 边界与风险复核

本任务没有修改：

- build、variant、route、list、pagination、cancel 或 report 行为；
- config/schema、plugin protocol、asset URL 或 persisted format；
- output/path/security policy，包括已独立关闭的 JSON Feed P1；
- JSON source-generation 或 Native AOT roots；
- CLI、Content、Rendering、Routing、Theme 的 production；
- Labs 或外部插件。

残余兼容性风险只来自 2.0 访问级别变化本身，已由 closed declaration window、明确
迁移入口和精确元数据断言管理。private、未索引、未自愿声明的直接 CLR consumers
仍不可观察。

## 8. 验证边界

按照 G4 总计划，Task 31～41 只建立实现与待验证断言，不在单个 cluster 运行 tests、
focused gate、aggregate、AOT 或独立复审。本任务不声明这些验证已通过。

Task 42 必须一次性验证：

- `Bukit.Engine.Tests`；
- `Bukit.Cli.Tests`；
- `Bukit.Architecture.Tests`；
- public API drift 与 active docs contract；
- G4 唯一 aggregate targeted gate；
- Native AOT/package smoke；
- G4 轻量复审和整个 G-04 最终只读复审。

在 Task 42 完成前，本台账状态保持 `g4-verification-pending`。
