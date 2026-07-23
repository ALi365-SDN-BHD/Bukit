# Bukit Core G-04D2E runtime-only context 决策

日期：2026-07-23

状态：`group-verification-pending`

## 决策

本任务仅处理 `Bukit.PluginHost` 的 runtime-only config construction graph：

- `PluginConfigLoader` 保持 public；
- public construction 收窄为唯一无参构造，并继续委托
  `PluginRuntimeOnlyContext.None`；
- 接收 `PluginRuntimeOnlyContext` 的构造改为 internal、非 optional；
- `PluginRuntimeOnlyContext` 类型 internalize；
- 仅向 `Bukit.PluginHost.Tests` 与 `Bukit.Cli.Tests` 授予精确 test-only
  `InternalsVisibleTo`，不向任何 production assembly 授予 friend access；
- current baseline 由 14 assemblies / 505 public types / 101 candidates
  更新为 14 / 504 / 100；
- 关闭的 136-entry candidate manifest 保持历史记录与 Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

这是获准发生在 2.0 分支的 CLR source/binary access narrowing。直接引用
`PluginRuntimeOnlyContext` 或调用原 public enum constructor 的外部 CLR
consumer 需要迁移。普通 `new PluginConfigLoader()` 调用保持有效，且其安全默认值
仍为 `None`。

## 行为与契约不变

本任务没有新增 public factory、boolean 替代参数或其他 privileged public seam。
没有修改：

- `.bukit/plugins.yaml` 配置 schema；
- `manifestPolicy` 字段、`static` / `runtime-only` 允许值和默认值；
- default context 对 `runtime-only` 的拒绝行为；
- Development、Labs、Test 三个 privileged context 的允许行为；
- `PluginHostConfig`、`PluginConfigEntry` 或其序列化形状；
- 插件协议、权限、secret、路径、错误码或诊断消息。

现有 `PluginConfigLoaderTests` 已固定 default reject 和
Development/Labs/Test allow 矩阵；`PluginSchemaContractTests` 已固定 schema
默认值与正式生成配置不输出 `runtime-only`；CLI integration test 已固定 Test
context 与默认 context 的差异。本任务只通过精确 test-only friendship 保留这些
白盒断言，不改变它们的输入、输出或断言强度。

## 构造与 friend boundary

批准后的 CLR 构造图是：

```text
external/production consumer
  -> public PluginConfigLoader()
  -> internal PluginConfigLoader(PluginRuntimeOnlyContext.None)

Bukit.PluginHost.Tests / Bukit.Cli.Tests only
  -> internal PluginConfigLoader(Development | Labs | Test)
```

`Bukit.Cli` production assembly 不是 friend，并继续只使用 public
`new PluginConfigLoader()`。没有加入通配、签名不明确或 production
`InternalsVisibleTo`。

## Baseline delta

current baseline 只发生以下变化：

1. 从 `PluginConfigLoader.publicMembers` 删除带
   `PluginRuntimeOnlyContext` optional 参数的 public constructor；
2. 为 retained `PluginConfigLoader` 记录 `public .ctor()`；
3. 删除 `Bukit.PluginHost.PluginRuntimeOnlyContext` 的 exported-type 记录。

除这一候选及其构造传播外，不应出现其他 public type/member 或治理 metadata
变化。活动 consumer declaration 中的 current baseline 同步为 14 / 504 / 100；
既有历史任务数字、旧决策 remainder 与关闭 candidate manifest 均保留。

## 待验证证据

已添加架构断言，固定以下预期：

- enum 仍存在于 owning assembly，但不是 public/exported；
- retained loader 只有 public 无参构造；
- enum constructor 存在且为 assembly-internal；
- friend assembly 精确等于 `Bukit.PluginHost.Tests` 与 `Bukit.Cli.Tests`，不存在
  production friend；
- current baseline 为 14 / 504 / 100，且只按目标形状记录 loader 并删除 enum；
- 历史 candidate manifest 仍保留 enum 记录，精确 blob 不变。

按 G1 总计划，本任务未运行 test、build、gate 或 Native AOT。上述新断言及现有
PluginHost、CLI、Architecture、schema/secret 场景统一留待 Task 10 执行；验证前
不得把本状态改为关闭。

## Stop conditions

出现以下任一情况时停止，不扩大本任务范围：

- 必须向 production assembly 授予 friend access；
- 必须新增 public privileged factory 或参数才能保留现有行为；
- schema、默认值、过滤逻辑、序列化或 wire contract 发生变化；
- current baseline 不是精确 14 / 504 / 100；
- closed manifest blob 不再是
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- 需要修改 CI、release、gate 或无关 PluginHost graph 才能通过。
