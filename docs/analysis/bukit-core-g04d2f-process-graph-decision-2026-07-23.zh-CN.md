# G-04D2F process/protocol 八类型图治理决策

> 日期：2026-07-23
>
> 实施基线：`codex/g04-group1-pluginhost-content-a@8f1ec3e70bff`
>
> 状态：`group-verification-pending`

## 1. 决策

以下八项作为一个不可拆散的 process implementation seam，终态统一为
**retain-by-design**：

- `IPluginProcessInvoker`
- `IPluginRequestIdFactory`
- `IProcessRunner`
- `PluginProcessRequest`
- `PluginProcessResult`
- `ProcessOutputStream`
- `ProcessRunRequest`
- `ProcessRunResult`

八项继续是 `public`、继续由 `Bukit.PluginHost` 导出，且公共 constructor、method、
record property、`Deconstruct`、enum value 与 base-interface 传播均不改变。本任务不修改
任何 PluginHost 生产 C#。

治理 baseline 只纠正分类：

| 字段 | 修改前 | 修改后 |
|---|---|---|
| `classification` | `implementation-public` | `cross-assembly-implementation` |
| `compatibility` | `2.0-candidate` | `1.x-do-not-narrow` |
| `migrationHorizon` | `2.0-review` | `2.0-review` |

因此当前 baseline 仍为 14 assemblies / 504 public types，候选数由 100 降为 92。
这不是类型删除，而是明确承认现存跨类型、跨 facade 的公共传播关系。

## 2. 保留原因与影响图

`PluginProtocolClient` 的 public construction seam 传播
`IPluginProcessInvoker` 与 `IPluginRequestIdFactory`；
`PluginProcessInvoker` 的 public constructor/method/base interface 传播
`IProcessRunner`、`PluginProcessRequest` 与 `PluginProcessResult`；
`SystemProcessRunner` 的 public method/base interface 传播
`ProcessRunRequest` 与 `ProcessRunResult`；两个 result 又传播
`ProcessOutputStream`。

若只收窄八项而保留 companion type visibility，需要把 public method 改 explicit
interface implementation、收窄 constructor，并让
`PluginProcessInvoker`、`SystemProcessRunner` 变成没有可用 public 行为的 empty
shell。这样会同时造成 source、binary、reflection 和测试替身破坏，却没有形成更清晰的
替代公共契约。公共类型数量下降不是本治理任务的验收目标，所以本轮不制造该迁移债务。

## 3. 行为、安全与协议边界

- process 启动仍使用 `UseShellExecute = false` 与 `ArgumentList`，本轮不引入 shell
  拼接、命令解释器或新的 quoting 规则；
- timeout、cancel、stdout/stderr 捕获、output limit、exit code 与 request ID 的现有
  owner tests 保留，不重写；
- `SystemProcessRunnerTests` 仅增加正常完成后临时 working directory 可立即删除的
  resource-release characterization；
- 不修改 `bukit-plugin-v1` JSON DTO、handshake、错误码、serializer root 或 wire
  bytes，协议 drift 为 0；
- 不新增 IVT。现有测试 seam 继续由 `Bukit.PluginHost.Tests` 所有，公共面治理断言由
  `Bukit.Architecture.Tests` 所有。

## 4. Reflection 与 Native AOT

新增 architecture test 固定八项仍由 `Assembly.GetExportedTypes()` 返回，并固定
`PluginProtocolClient`、`PluginProcessInvoker`、`PluginRequestIdFactory`、
`SystemProcessRunner` 的完整 public 传播图。由于生产类型与 public member token
均未改变，本任务没有新增 reflection root、dynamic activation 或 serializer metadata。

Native AOT 风险不因本任务增加；真实 PluginHost、CLI、Architecture 与 AOT 验证按照
G1 Task 10 统一执行，本任务不提前声明通过。

## 5. 证据与待统一验证

新增：

- `G04D2FProcessGraphTests`：export、传播图、baseline exact metadata、14/504/92
  current counts、历史 136-entry manifest blob 不变；
- `SystemProcessRunnerTests.RunAsync_ReleasesCompletedProcessWorkingDirectory`：正常完成后
  working directory 无残留 process handle。

本内部任务按总计划不单独运行 test/build/gate/AOT。当前状态保持
`group-verification-pending`；只有 Task 10 的 G1 统一测试、targeted gate、AOT 与轻量
只读复审全部取得证据后，才能改为 closed。
