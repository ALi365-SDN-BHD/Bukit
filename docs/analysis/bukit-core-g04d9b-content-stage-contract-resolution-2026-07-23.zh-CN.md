# Bukit Core G-04D9B Content Validation / Stage Contract 受控收窄台账

> 日期：2026-07-23
>
> 任务：G-04 Group 4 / Task 34
>
> G4 基线：`2.0@729088dbc2faf1bf7a20fe670e96a09b7568e7ba`
>
> 前置提交：G-04D9A `df9edfc6`
>
> 状态：implementation-complete / g4-verification-pending

## 1. 结论

D9B 九项的最终治理结果为：

| 类型 | 终态 | 决策依据 |
|---|---|---|
| `ContentCollectionContractValidator` | internalized in 2.0 | Engine 内容加载/route validation 的静态实现 helper |
| `ContentSchemaValidator` | internalized in 2.0 | public type 原本没有 public/protected member，只由 Engine stage 使用 |
| `ContentValidationIssue` | retained public | public schema projection、pipeline result 与 stage output 传播 |
| `IContentProviderFactory` | retained public | public `ContentPipeline` constructor 与 default implementation 传播 |
| `ITemplateRenderer` | retained public | public renderer base 与 replacement seam |
| `ContentStageInput` | retained public | public `IContentStage.ExecuteAsync` 参数 |
| `ContentStageOutput` | retained public | public stage 返回类型并传播 validation issue |
| `IContentStage` | retained public | public `ContentPipeline` explicit-stage constructor 参数 |
| `TemplateRendererBase` | retained public | 真实 public/protected renderer extension surface |

本任务是 `2 internalized + 7 retained + 0 stable-parent drift`。没有通过收窄
`ContentPipeline` constructor 来人为制造 stage graph 的资格。

## 2. 最小 production diff

只修改两个 containing type access modifiers：

```text
ContentCollectionContractValidator:
  public static class -> internal static class

ContentSchemaValidator:
  public static class -> internal static class
```

成员、逻辑、错误文本和常量均不修改。特别是与 schema validator 位于同一文件的
`ContentValidationIssue` 继续是 public sealed positional record。

两个 validator 不出现在其他 stable public signature 中，不会形成 inconsistent
accessibility。`Bukit.Engine.Tests` 继续通过既有 friend boundary 测试 internal
实现，不新增 `InternalsVisibleTo`。

## 3. Retained public graph

下列 propagation 保持：

```text
ContentModelSchemaProjection.ValidateDocuments(...)
  -> IReadOnlyList<ContentValidationIssue>

ContentPipeline
  ├─ .ctor(IContentProviderFactory, ILogger)
  ├─ .ctor(IReadOnlyList<IContentStage>, ILogger)
  └─ ExecuteAsync(...) -> Task<ContentPipelineResult>

IContentStage.ExecuteAsync(ContentStageInput, CancellationToken)
  -> Task<ContentStageOutput>
     └─ IReadOnlyList<ContentValidationIssue>?

TemplateRendererBase : ITemplateRenderer
  ├─ protected constructor and state
  ├─ protected abstract parse/render/resolve/content hooks
  ├─ protected RenderWithLayout
  └─ protected virtual ExtractLayoutDirective
```

七项 retained 类型统一重分类为
`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`。
signature、public members 与 protected members 不修改。renderer interface/base
必须等待独立 replacement contract、迁移说明或 obsolete window；本任务不创建该
迁移任务。

## 4. Baseline 与消费者证据

Task 34 后 current baseline 为：

```text
14 assemblies / 478 public types / 40 candidates
```

从 D9A 终态 `14/480/49` 到本任务终态：

- 两个 validator 移出 exported baseline；
- 七项 retained types 保持 public，但退出 candidate；
- public types 减少 2，candidate 减少 9。

historical manifest 保持：

```text
declarationState = closed
candidateCount = 136
candidates.length = 136
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

九项 historical entries 继续保持
`consumer-declaration-pending / no-public-match-found /
unknown-until-voluntary-declaration / review-only`。private 或未索引消费者仍不可观察。

## 5. 架构与行为断言

新增 `G04D9BContentStageContractGraphTests`，进入 Task 42 的 G4 最终测试清单：

1. 两个 validator 仍存在、为 internal static、且不再 exported；
2. collection validator 的两个 `Validate` overload 保持；
3. schema validator 的 `ValidateFields`、`Validate` 和
   `ResolveSchemaFailMode` internal entry points 保持；
4. 七项 retained types 全部继续 exported；
5. 两个 `ContentPipeline` public constructors 与 public result 保持；
6. validation issue、stage input/output 与 interface method shape 保持；
7. provider default implementation 继续实现 public interface；
8. renderer public abstract/interface 与 protected hooks 保持；
9. current baseline 精确为 `14/478/40`；
10. 九项 historical records、136-entry 数量和 immutable blob 保持；
11. 两份活动治理文档记录 D9B 决策和 current baseline。

Task 42 必须复用、不得弱化的 owner behavior tests：

- `ContentCollectionContractValidatorTests`；
- `ContentSchemaValidatorExtendedTests`；
- `SchemaFailModeTests`；
- `ContentPipelineTests`；
- `ContentStagesTests`；
- `DefaultContentProviderFactoryTests`；
- `RenderPipelineTests` 和现有 renderer dispatcher tests；
- CLI doctor schema/data summary tests。

## 6. 兼容性

这是获批的 2.0 source/binary/reflection break，仅针对直接命名或调用两个 validator
implementation types 的 CLR consumer。

标准消费路径保持：

- collection/content validation 由 `ContentPipeline` 执行；
-公开 schema projection 使用 `ContentModelSchemaProjection`；
- provider composition 使用 public `IContentProviderFactory`；
- stage injection 和 renderer inheritance seam 本批继续 public。

不新增 facade、wrapper、reflection fallback、dynamic activation、friend assembly 或
第二套 validation pipeline。

## 7. 范围与漂移审计

本任务没有修改：

- `ContentPipeline` constructors、stage order、duration fallback 或 cancellation；
- schema required/type/enum/format/range/unknown-field/fail-mode 规则；
- issue code、message、source path 或 ordering；
- renderer cache、layout nesting、shortcode、layout directive 或 protected hooks；
- config/schema 文件、plugin protocol、build report、path/security policy；
- JSON/AOT roots、Labs 或外部插件。

任何 renderer extension 迁移、stage seam 删除或 public parent redesign 都必须另立
2.0 API 迁移任务，不属于 D9B。

## 8. 验证边界

G4 总计划规定 Task 31～41 不运行单 cluster tests、focused gate、aggregate、AOT 或
独立复审。本任务只提交实现、架构断言、baseline 与治理文档，不声明相关验证通过。

Task 42 将统一执行 Engine、CLI、Architecture、public API drift、G4 唯一 aggregate、
Native AOT/package smoke、G4 轻量复审和整个 G-04 最终只读复审。完成前状态保持
`g4-verification-pending`。
