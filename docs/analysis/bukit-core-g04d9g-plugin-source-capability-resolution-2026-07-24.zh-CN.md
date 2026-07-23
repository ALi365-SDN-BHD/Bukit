# Bukit Core G-04D9G Plugin Source / Capability Graph 受控收窄台账

> 日期：2026-07-24
>
> 任务：G-04 Group 4 / Task 39
>
> 状态：implementation-complete / g4-verification-pending

`BuiltInPluginSource`、`IPluginSource`、`PluginCapability` 原子改为 internal；
成员、注册、capability strings 和 `PluginRegistry` public facade 不变。

current baseline 从 `14/447/8` 变为 `14/444/5`。historical manifest 保持
`closed / 136 / 136` 与 blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`。

新增 `G04D9GPluginSourceCapabilityGraphTests` 锁定 internal graph、两个 capability
常量、registry stable tuple、baseline、historical blob 与活动文档。

Task 42 必须验证 static built-in registration、ordering、Native AOT 和 CG-019。
不扩张为通用 CLR plugin SDK，不引入 reflection/dynamic assembly，不修改 process
protocol、Labs 或外部插件。本任务不单独运行 tests/gates/AOT/review。
