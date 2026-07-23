# Bukit Core G-04D9H List / Template Capability Graph 受控收窄台账

> 日期：2026-07-24
>
> 任务：G-04 Group 4 / Task 40
>
> 状态：implementation-complete / g4-verification-pending

`SpecialListRouteBuilder` 由 public 改为 internal。下列四项 retained public：

- `TemplateCapabilitiesResolver.ListPageContentResolution`；
- `TemplateCapabilitiesResolver.TemplateCapabilityFlags`；
- `TemplateCapabilitiesResolver.TemplateFieldDeclaration`；
- `TemplateVariableWarning`。

四项被 stable public resolver/linter methods 传播，统一重分类为
`cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`。

current baseline 从 `14/444/5` 变为：

```text
14 assemblies / 443 public types / 0 candidates
```

historical manifest 仍为 `closed / 136 / 136`，五项历史记录与 blob
`7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

新增 `G04D9HListTemplateCapabilityGraphTests` 锁定 builder internal、四个 companion
public propagation、baseline 零 candidate、历史 blob 与活动文档。

production 只改一个 modifier；不修改 taxonomy/list routing、route precedence、
template field names、capability detection 或 lint warning text。parent facade redesign
必须另立迁移任务。

按 G4 规则，本任务不单独运行 tests/gates/AOT/review，统一留到 Task 42。
