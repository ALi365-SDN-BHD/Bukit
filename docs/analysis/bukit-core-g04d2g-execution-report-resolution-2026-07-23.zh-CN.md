# Bukit Core G-04D2G execution-report CLR 图处置

日期：2026-07-23

任务：G-04 Group 1 Task 7

状态：`group-verification-pending`

## 1. 结论

G-04D2R 的决策已按原子图落地：

- 受支持契约继续是 `.bukit/reports/plugin-executions/*.json`；
- `PluginExecutionReport`、`PluginExecutionReporter` 和
  `PluginExecutionResponseSummary` 是 Host 内部 CLR 实现，不是 SDK；
- `PluginProtocolClient` 只保留
  `IPluginProcessInvoker + IPluginRequestIdFactory` 两参数 public constructor；
- reporter injection constructor 改为 internal、三参数、不可选并保留 null guard；
- current baseline 更新为 14 assemblies / 501 public types / 89 candidates；
- closed 136-entry candidate manifest 及 Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1` 未修改。

本任务没有改变插件协议、配置、报告目录、文件名、字段、字段顺序、脱敏、failure
propagation 或 Native AOT writer reachability。

## 2. 持久化契约冻结

新增的 out-of-band 契约为：

- [v1 schema](../schemas/plugin-execution-report.v1.schema.json)；
- [deterministic golden](../../tests/fixtures/plugin-contracts/plugin-execution-report.v1.json)；
- [owner contract test](../../tests/Bukit.PluginHost.Tests/PluginLockAndReportTests.cs)。

schema 严格列出当前 writer 的所有 root 和 nested 字段，固定 required、
null/空集合语义、数组 item 和 `additionalProperties`。golden 使用现有 owner test
的固定输入和脱敏结果。测试用无第三方依赖的最小 validator 同时验证实际输出和
golden，并用 `JsonNode.DeepEquals` 作顺序无关的完整语义比较。

当前 JSON 没有新增 `schemaVersion`。这是有意的零 persisted drift：
版本标识位于 schema 和 fixture 文件名，不写入既有工件。

## 3. CLR 与构造传播处置

| 对象 | Task 7 处置 | 保持不变 |
| --- | --- | --- |
| `PluginExecutionReport` | internal | writer 输入与默认空集合 |
| `PluginExecutionResponseSummary` | internal | `responseSummary` JSON shape |
| `PluginExecutionReporter` | internal；`WriteAsync` internal | path、masking、flush 与异常传播 |
| `PluginProtocolClient` | public 两参 ctor；internal 三参 injection ctor | D2F process seam 与 public protocol methods |

`Bukit.PluginHost.Tests` 已有精准 test-only friend access；本任务未新增 friend，
也没有向 CLI 或其他生产程序集开放内部实现。

该收窄是经批准的 2.0 source、binary 和 reflection breaking change：直接构造三个
CLR 类型或调用旧三参数 public constructor 的消费者需要迁移。支持的 process
plugin 协议和 persisted JSON v1 不受影响。

## 4. 待 Group 1 统一验证

Task 7 按总计划不单独运行 test、build、gate、AOT 或复审。以下证据已加入
Task 10 的 G1 集合：

- 实际 writer JSON 与 golden 完整 DeepEquals；
- actual 与 golden 均通过独立最小 schema validator；
- secret 保持 masked，完整 stdout 字段不存在；
- 三个 CLR 类型存在但不 exported；
- public ctor 仅保留两个 D2F dependency；
- internal injection ctor non-optional、non-nullable；
- baseline 精确为 14 / 501 / 89；
- schema/golden 路径存在；
- historical manifest 三项及 blob 不变。

因此当前只能标记 `group-verification-pending`；只有 Task 10 的组级测试、targeted
gate、Native AOT 和一次轻量只读复审全部通过后，才可改为 closed。

## 5. 边界

本任务未处理 retention/cleanup、原子 rename、毫秒级文件名碰撞、redaction 策略
扩展或 best-effort write。这些均不是 CLR public-surface 收窄的必要条件，不得在
本任务中顺带修复。
