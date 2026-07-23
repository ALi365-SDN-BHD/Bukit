# Bukit Core G-04D8 Theme 决策汇总

> 日期：2026-07-23
>
> 任务：G-04 Group 4 / Task 31
>
> G4 基线：`2.0@6f10269c515f328628955f706075d70cc3a21977`
>
> 状态：decision-consolidated / g3-verified / g4-final-review-pending

## 1. 范围

本汇总只登记 G-04D8 三个 Bukit Core Theme historical `2.0-candidate` 的最终状态：

1. `Bukit.Theme.SchemaValidationError`；
2. `Bukit.Theme.SchemaValidationException`；
3. `Bukit.Theme.ThemeDoctorCommand.DoctorResult`。

Task 31 不修改 production、tests、public API baseline、JSON/AOT roots、Theme schema、
Core CLI、Labs 或外部插件，也不重新运行 tests、aggregate、AOT 或复审。G3 已完成的
实现与验证证据保持原样；本任务只把它们汇入 G4 和 G-04 最终关闭矩阵。

## 2. 三项最终决策

| Historical candidate | 当前终态 | 最终决策 | 主要约束 |
|---|---|---|---|
| `SchemaValidationError` | public sealed positional record；`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review` | retained public | public `SectionSchemaValidator.Validate(...)` 的返回元素类型 |
| `SchemaValidationException` | internal sealed exception；runtime full name 和 string constructor 保持 | internalized in 2.0 | 不在 public/protected signature 中；strict 首错与 message 保持 |
| `ThemeDoctorCommand.DoctorResult` | public nested sealed positional record；`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review` | retained public | public `Diagnose(...)` 返回、`PrintReport(...)` 参数及已知 Labs 编译期消费者 |

Theme 终态不是“三项全部收窄”。一个类型具备独立收窄资格，另外两个是 public facade
的必要 companion。保留 public 是明确治理结论，不是等待未来再次默认 internalize。

## 3. Validation graph 关闭事实

`SchemaValidationError` 继续保持：

- `Section`、`Message`、record equality、deconstruction 与 `ToString()` shape；
- warn 模式的返回列表和日志顺序；
- public `Validate(...) -> List<SchemaValidationError>` 元数据合同。

`SchemaValidationException` 只改变 type accessibility，继续保持：

- exact runtime full name；
- `Exception` 基类；
- public string constructor；
- strict 模式第一条错误立即抛出；
- `Message` 精确等于第一条 `SchemaValidationError.ToString()`；
- Rendering 通过 generic exception/message 传播的既有行为。

没有新增 friend assembly、异常成员、排序、schema 规则或 Rendering error code。

## 4. Doctor graph 关闭事实

`DoctorResult` 继续是下列 public facade 的必要类型：

```text
public ThemeDoctorCommand
  ├─ public Diagnose(...) -> public DoctorResult
  └─ public PrintReport(public DoctorResult)
```

Core CLI doctor 是独立实现，没有接入该 graph。D8B 固定了：

- `bool / bool / List<string>` constructor 和 properties；
- issue 阶段顺序、glyph、缩进、summary 文本与 error 优先级；
- `Issues` 的现有可变 list 与 record equality 行为；
- Core CLI success/invalid-theme exit code 与文本隔离；
- Theme JSON contexts 不 root `DoctorResult`。

范围外 Labs 直接消费该 public facade 的事实只用于 retained 决策，不授权修改 Labs。

## 5. G3 已完成验证

G3 已从 `b4a60b7ebeef34eda9f53e72a10a76ebc10c8544` 对完整组 diff 完成：

| 验证 | 结果 |
|---|---|
| `Bukit.Theme.Tests` | 74/74 |
| `Bukit.Cli.Tests` | 618/618 |
| `Bukit.Engine.Tests` | 1595/1595 |
| `Bukit.Architecture.Tests` | 215/215 |
| public API drift | 通过 |
| final replacement aggregate | 通过 |
| Native AOT package / release artifact smoke | 通过 |
| published CLI doctor | valid exit 0；invalid theme exit 1；均无 Theme doctor JSON |
| G3 独立轻量复审 | Critical/Important/Minor `0/0/0` |

这些结果来自 G3 正式关闭台账。Task 31 不重复运行或改写其调用历史。

## 6. 公共面与历史证据

G4 入场当前 baseline：

```text
14 assemblies / 484 public types / 56 candidates
```

Theme 当前 candidate 数为零。两项 retained public 仍存在于 current baseline，但
`compatibility` 已是 `1.x-do-not-narrow`；internal exception 不再是 exported type。

历史 candidate manifest 保持：

```text
declarationState = closed
candidateCount = 136
candidates.length = 136
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

三项 historical entries 均继续保留，用于追踪其 retained/internalized 去向。

## 7. G4 待复核集合

Task 42 应在 G4 完整验证与最终全量只读复审中确认：

1. Theme 三项终态与 current baseline 一致；
2. `SchemaValidationError` 和 `DoctorResult` 没有被 Engine D9 任务顺带收窄；
3. internal exception 的 runtime identity、首错时机和 message 不回归；
4. Theme doctor 与 Core CLI doctor 继续隔离；
5. Theme JSON/AOT roots、schema、manifest 和输出文本无漂移；
6. owner 行为测试没有因公共面治理被删除或弱化；
7. historical 136-entry manifest 内容和 Git blob 不变。

Task 31 本身不产生新的验证通过声明，状态保持
`g4-final-review-pending`，直到 Task 42 对完整 G4 diff 和 G-04 决策链完成验证。

## 8. 关闭边界

G-04D8 的实现和 G3 组级验证已经完成。G4 不得借 Theme 收口：

- internalize `SchemaValidationError` 或 `DoctorResult`；
- 修改 `SectionSchemaValidator.Validate(...)` 或 Theme doctor public facade；
- 把 Theme doctor 接入 Core CLI；
- 新增 JSON/persisted report；
- 修改 theme schema、模板字段或诊断文本；
- 修改 Labs 或外部插件。

任何父 facade 重新设计必须作为独立 2.0 API 迁移任务，不属于 G-04D9 或 Task 42。
