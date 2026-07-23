# Bukit Core G-04D2 PluginHost 最终 aggregate 处置台账

日期：2026-07-23

范围：G-04D2、D2A、D2B1、D2B2、D2R、D2C、D2D、D2E、D2F、D2G

状态：`group-verification-pending`

> 本文只汇总 PluginHost 16 项原始候选的实现终态和 Group 1 待验证输入。
> Task 10 尚未执行统一测试、targeted gate、Native AOT 或轻量复审，因此本文
> 不使用 `closed`，也不预判任何组级验证结果。

## 1. Aggregate 边界

| 项目 | 当前事实 |
| --- | --- |
| D2 parent base | `21072f4f45fdb23c0f3a95f03c837c1dab4665b5` |
| 当前实现 HEAD | `c0195b92c32d46a2700fff603bc0f67bf5be469f` |
| aggregate diff | 48 files；8,031 insertions；232 deletions |
| current public API baseline | 14 assemblies / 501 public types / 89 `2.0-candidate` |
| current PluginHost | 32 public types / 0 `2.0-candidate` |
| 历史候选 manifest | 136 entries；PluginHost 16 entries |
| 历史 manifest Git blob | `7b07d6890562387010b52301e9f8716e9bf10ed1` |

Aggregate 提交链按治理职责划分如下：

| 提交 | 职责 |
| --- | --- |
| `a07bcb32`～`2272156f` | D2 资格审计与 D2A `PluginSecretMasker` 收窄 |
| `b63528db`～`2fa89026` | D2B1 错误码契约迁移、D2B2 收窄及既有验证/复审留痕 |
| `10bfead3`、`15f0bd35` | 总计划与 G1 基线校准 |
| `db8bab77`、`527685d2` | D2R report contract 决策与 D2C construction boundary |
| `c54671fa` | D2D permission graph |
| `8f1ec3e7` | D2E runtime-only context |
| `94d28cfa` | D2F retain-by-design 分类 |
| `c0195b92` | D2G execution-report CLR graph |

该 diff 包含 D2 的分析/计划、PluginHost 源码、owner/architecture tests、current
baseline、治理说明、两个 plugin-contract fixture 及 out-of-band report v1
schema。Task 8 没有扩大到 Content、Engine、插件 wire DTO、CI、release 或 gate。

## 2. 16 项正式终态矩阵

状态统计：

- `internalized`：8 项；
- `retained-by-design`：8 项；
- `removed`、`blocked`：0 项；
- current PluginHost `2.0-candidate`：0 项。

| 原始候选 | Owner / 原传播 | 最终状态 | 迁移与当前证据 | Task 10 待验证 |
| --- | --- | --- | --- | --- |
| `PluginSecretMasker` | report security；只由 Reporter 同程序集调用 | `internalized`，D2A 已先前独立验证 | containing type 收窄；算法、secret fragments、report shape 不变；baseline 删除该项 | 当前 aggregate 中仍不导出；masking owner tests 保持 |
| `PluginHostErrorCodes` | Host diagnostic；五个实际输出和一个保留词汇 | `internalized`，D2B2 已先前独立验证 | B1 先迁移入口/fixture 契约，B2 后收窄；六个字符串和值不变 | 当前 aggregate 中五个入口码和 `permissionDenied` fixture 均保持 |
| `PluginFileSystemPermissionEvaluator` | permission implementation；由 retained evaluator public optional ctor 传播 | `internalized`，G1 pending | retained evaluator 改 public 无参 + internal non-optional 注入；permission 算法不变 | export、constructor、读写/拒绝行为、安全断言 |
| `PluginPermissionPathNormalizer` | permission implementation；由 filesystem evaluator 传播 | `internalized`，G1 pending | 与 evaluator 原子收窄；仍仅为词法声明规范化 | export、传播关闭、路径/权限语义无漂移 |
| `PluginRuntimeOnlyContext` | config implementation；由 retained loader public optional ctor 传播 | `internalized`，G1 pending | loader 改 public 无参固定 `None` + internal enum ctor；仅两个 test assembly 为 friend | default reject、Development/Labs/Test allow、精确 IVT、config shape |
| `IPluginProcessInvoker` | process orchestration；`PluginProtocolClient` ctor 与 `PluginProcessInvoker` base interface | `retained-by-design`，G1 pending | 分类迁移为 `cross-assembly-implementation / 1.x-do-not-narrow`；签名不变 | exported、完整 public propagation、invoke behavior |
| `IPluginRequestIdFactory` | request correlation；protocol ctor 与 request-id factory base interface | `retained-by-design`，G1 pending | 同 D2F 完整 seam；不制造不可构造 public companion | exported、request ID 行为和传播 |
| `IProcessRunner` | OS process seam；invoker ctor 与 runner base interface | `retained-by-design`，G1 pending | 同 D2F；不增加 IVT | exported、runner public seam |
| `PluginProcessRequest` | invoker interface/concrete public method 参数 | `retained-by-design`，G1 pending | constructor/member/record surface 不变；治理分类迁移 | exported、public method 参数形状 |
| `PluginProcessResult` | invoker interface/concrete public 返回值 | `retained-by-design`，G1 pending | 与 request/result 图整体保留 | exported、exit/timeout/output 结果语义 |
| `ProcessOutputStream` | 两级 result 的 ctor/property/`Deconstruct` | `retained-by-design`，G1 pending | 与两个 result 原子保留 | exported、stdout/stderr/output-limit 行为 |
| `ProcessRunRequest` | runner interface/concrete public method 参数 | `retained-by-design`，G1 pending | process launch seam 保留；不改 quoting/shell 行为 | exported、working directory/cancel/timeout |
| `ProcessRunResult` | runner interface/concrete public 返回值 | `retained-by-design`，G1 pending | result/disposal seam 保留 | exported、exit/output/disposal characterization |
| `PluginExecutionReport` | report writer input；record surface | `internalized`，G1 pending | persisted JSON v1 先冻结，随后与 Reporter/Summary 原子收窄 | 不导出；actual/golden/schema 完整一致 |
| `PluginExecutionReporter` | `PluginProtocolClient` 原 public optional ctor 参数 | `internalized`，G1 pending | protocol client 保留 public 两依赖 ctor；reporter 三参注入 ctor 改 internal、non-optional | public ctor 不传播 Reporter；report path/masking/failure behavior |
| `PluginExecutionResponseSummary` | report ctor/property 与 persisted `responseSummary` | `internalized`，G1 pending | CLR identity 收窄，persisted nested shape 不变 | 不导出；null/default 与完整 summary shape |

历史 manifest 中上述 16 项仍保留原始
`consumer-declaration-pending / unknown-until-voluntary-declaration /
review-only` 记录。它是不可改写的历史调查 cohort，不是 current baseline；
internalized 或重分类不会授权修改其历史状态。

## 3. Contract 与漂移边界

### 3.1 Execution report

`.bukit/reports/plugin-executions/*.json` 是受支持的版本化诊断工件；三个
`PluginExecution*` CLR identity 不是 SDK。

新增的
[plugin-execution-report.v1.schema.json](../schemas/plugin-execution-report.v1.schema.json)
是**现有 persisted shape 的 out-of-band 版本化描述**。版本标识只存在于 schema
与 fixture 文件名；现有 JSON 未新增 `schemaVersion`，writer 字段、类型、
null/空集合、路径、脱敏、failure propagation 和输出字节形状未被主动修改。
这不是 `bukit-plugin-v1` wire schema drift。

### 3.2 其它受保护边界

| 边界 | 当前实现结论 | Task 10 状态 |
| --- | --- | --- |
| `bukit-plugin-v1` protocol、handshake、DTO、错误码 | 未修改 | 待统一验证 |
| 已有 plugin/config schema 与默认值 | 未修改 | 待统一验证 |
| permission、path、secret 与 process security 语义 | 未主动修改；D2D 未新增 symlink/reparse 物理语义 | 待统一验证 |
| report persisted format | writer 零有意漂移；out-of-band v1 描述既有 shape | 待 golden/schema/真实运行验证 |
| production friend access | 未新增；D2E 只加 `Bukit.PluginHost.Tests` 与 `Bukit.Cli.Tests` | 待 architecture 验证 |
| historical manifest | 内容未改；当前 blob 精确匹配 | 待 Task 10 再核验 |

因此目前只能陈述“实现意图和静态投影无协议/schema/config/security 漂移”；
不能在 Task 10 之前写成运行验证已通过。

## 4. Group 1 Task 10 必须消费的 PluginHost 输入

Task 10 应把以下输入并入 G1 唯一完整验证，不在 Task 8 单独执行：

1. 完整 `Bukit.PluginHost.Tests`、`Bukit.Cli.Tests` 和
   `Bukit.Architecture.Tests`；
2. D2A masking、D2B1 五个实际错误码和一个保留词汇；
3. D2D export/constructor 关闭及既有 permission/path 拒绝行为；
4. D2E default `None`、三个 privileged context、配置序列化与精确 test-only IVT；
5. D2F 八项仍 exported、public propagation 完整，以及 timeout、cancel、
   stdout/stderr、output limit、exit code、request ID、resource release；
6. D2G actual/golden JSON 顺序无关完整一致、独立 v1 validator、nullable/default、
   redaction、no-full-stdout、report path/filename；
7. compiled PluginHost 不导出 8 项 internalized CLR identity，同时仍导出 D2F 八项；
8. current baseline 精确为 14 / 501 / 89，PluginHost remaining candidate 为 0；
9. historical manifest 精确为 136 entries，Git blob 仍为
   `7b07d6890562387010b52301e9f8716e9bf10ed1`；
10. `public-api-drift.sh check Release`、以 G1 `GROUP_BASE` 执行的一次
    `post-change-targeted.sh`、`git diff --check`；
11. 真实 Native AOT CLI/process-plugin proof，覆盖 process invoke 与 execution
    report 写入；
12. G1 唯一一次轻量只读复审，检查路径范围、真实测试证据、baseline、历史
    manifest 及 protocol/schema/config/security/runtime 漂移。

## 5. Task 10 停止条件

出现以下任一情况，D2 不得申请组级关闭，也不得为追求计数扩大修改：

- current baseline 不是精确 14 / 501 / 89，或 PluginHost 仍有未解释 candidate；
- historical manifest 数量、内容或 blob 变化；
- internalized 类型仍被导出，或 retained D2F 类型/公共传播被意外收窄；
- public/protected、reflection、serializer、AOT 或消费者证据出现未映射传播；
- D2E 需要 production IVT、public privileged factory 或改变默认 `None`；
- permission/path/secret/process behavior 断言需要弱化才能通过；
- report schema/golden 不能描述当前 writer，或 persisted JSON 必须先改 bytes；
- `bukit-plugin-v1`、已有 schema、配置默认值、错误码或 security 语义发生漂移；
- owner/architecture/public API/targeted/AOT 任一验证未执行、失败或环境阻塞；
- G1 轻量复审发现未解决的正确性、安全性或兼容性阻断项。

## 6. 当前判定

16/16 原始 PluginHost 候选均已有明确终态和下一验证动作：

- 8 项实现已 internalize；
- 8 项按完整 process seam retained-by-design 并已从 current candidate 分类迁出；
- 0 项悬空、0 项 blocked；
- current PluginHost `2.0-candidate=0`。

这是**实现与治理投影完成**，不是组级关闭。正式状态继续为
`group-verification-pending`，必须进入 Task 9，并在 Task 10 完成 G1 唯一完整验证
和轻量复审后，才能申请把 D2 标记为 closed。
