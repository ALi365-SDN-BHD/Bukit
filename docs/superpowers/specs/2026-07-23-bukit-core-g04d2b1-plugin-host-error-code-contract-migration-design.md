# Bukit Core G-04D2B1 PluginHost Error Code Contract Migration Design

日期：2026-07-23

基线：`2.0@2272156f054cb308028b57ba50cc65268a454e30`

任务属性：G-04D2B 的第一阶段；只迁移 diagnostic contract 证据，不修改
CLR 可见性

## 1. 背景与问题

`Bukit.PluginHost.PluginHostErrorCodes` 当前是 public static class，包含六个
public const string。生产代码和测试都从同一组常量取得期望值，因此即使某个
字符串被错误修改，生产代码和测试也可能一起重新编译并继续通过，无法独立证明
协议字符串兼容性。

六个字符串并非同一种运行时行为：

- Host 当前实际构造并抛出的五个诊断前缀：
  - `plugin.unsupportedProtocol`
  - `plugin.invalidResponse`
  - `plugin.timeout`
  - `plugin.executionFailed`
  - `plugin.outputTooLarge`
- 仅作为现行协议/文档词汇保留的一个值：
  - `plugin.permissionDenied`

`plugin.permissionDenied` 当前没有 Host 生产调用点。权限拒绝仍由
`PluginPermissionEvaluator` 以 `DiagnosticCode.PluginCapabilityMissing`
报告。本任务不得把保留词汇变成新的 Host 运行时行为。

## 2. 目标

G-04D2B1 建立独立于 `PluginHostErrorCodes` CLR 类型的诊断契约证据：

1. 通过 `PluginProtocolClient` 的 public 入口锁定五个 Host 实际输出；
2. 通过机器可读 fixture 和活动协议文档锁定六个协议词汇，明确
   `plugin.permissionDenied` 只是保留词汇；
3. 消除测试程序集对 `PluginHostErrorCodes` 的直接编译期引用；
4. 补齐 `plugin.executionFailed` 的真实入口行为测试；
5. 保留 invoke business failure、权限拒绝、timeout、output limit、异常类型、
   `DiagnosticCode` 和完整 message 的现有语义；
6. 为后续独立 G-04D2B2 eligibility/internalization 决策提供可信输入。

## 3. 非目标与红线

本任务不：

- 修改 `PluginHostErrorCodes.cs` 或 `PluginProtocolClient.cs`；
- 修改类型或成员访问级别；
- 修改、增加、删除或重命名任一错误码；
- 让 Host 新增 `plugin.permissionDenied` 输出；
- 改变 `DiagnosticCode.PluginExecutionFailed` 或
  `DiagnosticCode.PluginCapabilityMissing`；
- 改变 handshake、manifest、invoke、timeout、output-limit、非零 exit、
  business failure 或 artifact path 行为；
- 新增 public/internal replacement constants、facade、enum 或 contract
  assembly；
- 增加 `InternalsVisibleTo`；
- 修改 `bukit-plugin-v1` DTO、schema、插件协议含义、配置或官方插件；
- 修改 public API baseline、136-entry closed candidate manifest、
  consumer declaration 或 public API governance guide；
- 运行 full、release、`test-all`、`smoke-all` 或 whole-solution gate。

G-04D2B1 不授权 G-04D2B2，也不预先声称
`PluginHostErrorCodes` 可以 internalize。

## 4. 方案比较

### 方案 A：入口行为与协议词汇分层迁移（采用）

- 测试直接断言固定协议字符串、完整异常消息和 `DiagnosticCode`；
- 新增一个无 CLR 类型名/成员名的 JSON vocabulary fixture；
- Architecture test 锁定 fixture、活动文档、测试层零直接 CLR 引用、
  baseline/manifest 当前态；
- production 保持零变化。

优点：最小、可审计，不扩大 API，不提前制造 breaking change，能够独立发现
字符串漂移。

### 方案 B：新增诊断 enum、contract class 或公共 facade（拒绝）

这会新增未治理的公共面或把相同常量换一个位置，不能解决同源自证，并扩大未来
兼容负担。

### 方案 C：在同一任务直接 internalize（拒绝）

这会跳过 B1/B2 决策边界，造成 2.0 source/reflection breaking change，并迫使
baseline 从 508/104 提前变化。本任务没有该授权。

## 5. 契约模型

### 5.1 Host 实际输出矩阵

| 错误码 | public 入口与触发条件 | 完整异常消息 |
|---|---|---|
| `plugin.unsupportedProtocol` | `HandshakeAsync` 收到不支持的 response protocol | `plugin.unsupportedProtocol: Plugin response protocol is unsupported.` |
| `plugin.invalidResponse` | `GetManifestAsync` 收到非法 JSON | `plugin.invalidResponse: Plugin stdout was not valid protocol JSON.` |
| `plugin.timeout` | `InvokeAsync` 得到 `TimedOut = true` | `plugin.timeout: Plugin process timed out.` |
| `plugin.executionFailed` | `GetManifestAsync` 得到非零 process exit | `plugin.executionFailed: Plugin process exited with code 7.` |
| `plugin.outputTooLarge` | `InvokeAsync` 得到 `OutputLimitExceeded = true` | `plugin.outputTooLarge: Plugin process output exceeded configured limits.` |

五个异常都必须保持：

- 类型为 `ConfigException`；
- `exception.Code == DiagnosticCode.PluginExecutionFailed`；
- message 与上表逐字符一致。

非法 JSON 路径还必须保留 `JsonException` inner exception。

### 5.2 保留协议词汇

新增 fixture：

`tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json`

其内容只表达协议词汇，不表达 CLR owner：

```json
{
  "schema": "bukit-plugin-host-error-vocabulary-v1",
  "codes": [
    "plugin.unsupportedProtocol",
    "plugin.invalidResponse",
    "plugin.timeout",
    "plugin.executionFailed",
    "plugin.permissionDenied",
    "plugin.outputTooLarge"
  ]
}
```

Architecture fixture 必须验证：

- schema 精确匹配；
- codes 精确有序、无重复、无增删；
- 六值均存在于现行插件协议稳定错误码章节；
- `plugin.permissionDenied` 及安全相关四值存在于安全 ADR；
- fixture 不包含 `PluginHostErrorCodes` CLR 类型名或成员名。

该 fixture 只证明协议词汇被保留，不证明 Host 当前会发出
`plugin.permissionDenied`。

### 5.3 Invoke business failure

invoke 的合法 `success=false` response 继续作为 response 返回，不得改为
`plugin.executionFailed` 异常。现有 business-failure 与 nonzero-valid-response
测试必须保留。

若补充 inbound `plugin.permissionDenied` 用例，只能证明
`PluginError.Code` 的 wire vocabulary 被原样保留；测试名称和台账必须明确
“inbound reserved vocabulary”，不得写成 Host emits。

## 6. 测试设计与 TDD

### 6.1 第一组 RED：测试层直接 CLR 引用

新增 Architecture test，只读取当前唯一直接消费者
`tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`，并断言它不再包含
`PluginHostErrorCodes` 标识符。基线应因
`tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
中的直接引用失败。

GREEN 只允许修改该测试文件：

- 用固定协议字符串替代 `PluginHostErrorCodes.*`；
- 使用测试本地 helper 精确断言 exception type、`Code` 和完整 message；
- 新增 manifest 非零 exit 的 `executionFailed` 用例；
- 保留路径穿越、invoke business failure 和 nonzero valid response 行为。

### 6.2 第二组 RED：协议词汇 fixture

先添加读取 fixture 的 Architecture test；fixture 尚不存在时必须失败。
随后创建精确 JSON fixture，并验证活动协议/安全文档。现有协议文档已经包含这些
词汇，因此不得为制造 GREEN 而修改协议含义。

### 6.3 当前态保护

同一 Architecture test class 必须锁定：

- `PluginHostErrorCodes` 仍是 exported public type；
- public baseline 仍为 14 assemblies / 508 exported types /
  104 `2.0-candidate` entries；
- baseline 仍包含该类型和六个精确 public const；
- closed manifest 仍为 136 entries；
- closed manifest Git blob 仍为
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- closed manifest 中 private consumer 状态仍是
  `unknown-until-voluntary-declaration`。

这些断言应在 B1 保持 GREEN；“type 不再 export”与 507/103 只能属于未来 B2。

## 7. 文件边界

预计新增：

- `docs/superpowers/specs/2026-07-23-bukit-core-g04d2b1-plugin-host-error-code-contract-migration-design.md`
- `docs/superpowers/plans/2026-07-23-bukit-core-g04d2b1-plugin-host-error-code-contract-migration.md`
- `docs/analysis/bukit-core-g04d2b1-plugin-host-error-code-contract-migration-2026-07-23.zh-CN.md`
- `tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json`
- `tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`

预计修改：

- `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`

明确不得修改：

- `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`
- `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- `docs/governance/bukit-core-public-api-baseline.v1.json`
- `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- `docs/governance/bukit-core-2.0-consumer-declaration.md`
- `guide/dev/public-api-governance.md`

## 8. 验证与复审

实施子任务只运行：

- `PluginProtocolClientTests` 定向测试；
- `G04D2B1PluginHostErrorCodeContractTests` 定向测试；
- 两个相关测试项目完整回归；
- public API drift self-test 与真实 check；
- docs active-links 与 no-absolute-paths；
- `post-change-focused.sh`，只传本任务变更路径。

父任务完成时：

- 对冻结基线到 HEAD 的全部变更运行一次且仅一次
  `post-change-targeted.sh --base 2272156f...`；
- 进行任务级和全分支独立只读复审；
- 检查生产源码零 diff、baseline 零 diff、closed manifest 零 diff；
- 不运行 full、release、AOT、`test-all`、`smoke-all` 或 whole-solution
  gate。B1 没有生产或 AOT root 变化。

## 9. Stop conditions

出现任一情况立即停止，不扩大修复：

- 需要修改 production 才能建立契约；
- 需要改变六值、完整 message、异常类型或 `DiagnosticCode`；
- 需要让 Host 实际发出 `plugin.permissionDenied`；
- 需要改变 invoke business failure；
- 新发现 public/protected signature、reflection、serialization、
  source-generator、AOT root 或直接消费者绑定该 full name；
- 新发现 private consumer 声明直接使用该类型；
- 需要增加 friendship、facade、replacement API 或 schema；
- baseline 发生任何 drift，或不是 14/508/104；
- closed manifest 任一字节变化；
- 必须弱化 timeout、output-limit、路径、权限或错误断言才能通过。

## 10. B1 完成定义

只有同时满足以下条件，才能把 B1 标为完成：

- 测试程序集对 `PluginHostErrorCodes` 的直接引用为零；
- 五个 Host 实际错误码均由 public 入口精确锁定；
- `plugin.permissionDenied` 被独立记录为保留协议词汇；
- production、baseline、closed manifest 无 diff；
- focused、唯一 aggregate 与独立只读复审全部通过；
- 台账明确 B2 尚未授权。

完成 B1 后，下一步只能是独立 G-04D2B2 eligibility/internalization
决策任务，而不是在 B1 内追加访问级别变更。
