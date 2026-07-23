# Bukit Core G-04D2B2 PluginHost Error Codes Internalization Design

日期：2026-07-23

基线：`2.0@757fb14976ad7337edc2a6fbf925b986222dea6f`

任务属性：G-04D2B 的第二阶段；独立的 2.0-only 单类型可见性收窄任务

## 1. 决策摘要

推荐在 2.0 分支只将
`Bukit.PluginHost.PluginHostErrorCodes` 从 public static class 收窄为
internal static class，并保持六个 const 成员、字符串值和所有运行时行为不变。

该结论依赖 G-04D2B1 已完成的诊断契约迁移，但不是 B1 的自动延伸授权：

- 五个 Host 实际错误码已由 `PluginProtocolClient` public 入口行为测试独立锁定；
- `plugin.permissionDenied` 已由独立 fixture 和活动文档锁定为保留协议词汇；
- 测试程序集不再编译期引用目标 CLR 类型；
- 当前仓内所有生产调用都在 `Bukit.PluginHost` owning assembly 内；
- 当前登记的 CLI/process 消费者没有声明该类型的直接 CLR 依赖；
- 2026-07-22 认证公开搜索快照没有发现公共匹配。

本机没有 `gh`，而本轮可用连接器的校准查询不可靠，因此不能把公开搜索刷新为
2026-07-23 的新治理快照。此限制不应被写成“全域零消费者”。closed manifest 中
`unknown-until-voluntary-declaration` 必须保留；若实施前出现具体直接、反射或
私有消费者证据，立即停止 internalization。

## 2. 当前事实基线

### 2.1 类型与生产调用

[PluginHostErrorCodes.cs](../../../src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs)
当前定义一个 public static class 和六个 public const string：

- `UnsupportedProtocol = "plugin.unsupportedProtocol"`
- `InvalidResponse = "plugin.invalidResponse"`
- `Timeout = "plugin.timeout"`
- `ExecutionFailed = "plugin.executionFailed"`
- `PermissionDenied = "plugin.permissionDenied"`
- `OutputTooLarge = "plugin.outputTooLarge"`

仓内生产引用全部位于同一程序集的
[PluginProtocolClient.cs](../../../src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs)：

- `UnsupportedProtocol`：2 处；
- `InvalidResponse`：11 处；
- `Timeout`：2 处；
- `ExecutionFailed`：2 处；
- `OutputTooLarge`：2 处；
- `PermissionDenied`：0 处。

没有 Core 跨程序集、Labs、官方插件、示例、脚本或测试程序集的直接编译期引用。
Architecture tests 只通过类型名字符串和 assembly metadata 验证可见性，不形成
目标类型的 CLR 编译依赖。

### 2.2 B1 诊断契约

[G04D2B1PluginHostErrorCodeContractTests.cs](../../../tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs)
和
[plugin-host-error-vocabulary.v1.json](../../../tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json)
已经将 CLR owner 与外部可观察的错误词汇分离。

[PluginProtocolClientTests.cs](../../../tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs)
通过 public entry 锁定：

- 五个实际 Host error prefix；
- `ConfigException` 类型；
- `DiagnosticCode.PluginExecutionFailed`；
- 完整异常 message；
- invalid JSON inner exception；
- timeout、output-limit、path safety、合法 business failure 和非零 exit 行为；
- inbound `plugin.permissionDenied` 的 wire vocabulary 保真。

B2 不需要也不允许修改这些行为测试、词汇 fixture、协议规范或安全 ADR。

### 2.3 公共面与消费者证据

当前 governed public API baseline 是：

- 14 个 assemblies；
- 508 个 exported types；
- 104 个 `2.0-candidate` entries；
- 目标类型记录包含六个 public const members。

closed candidate manifest：

- `declarationState = closed`；
- 136 个历史 candidates；
- 目标仍为 `consumer-declaration-pending`；
- private consumer 状态为 `unknown-until-voluntary-declaration`；
- 2026-07-22 认证公开搜索为 `no-public-match-found`；
- Git blob 为 `7b07d6890562387010b52301e9f8716e9bf10ed1`。

本轮重新读取消费者声明窗口，没有发现 `PluginHostErrorCodes`、reflection、
serialization、assembly 或 AOT 依赖的新声明。已登记的 SRBiz-bukit、sitegen、
ALi365WebSiteBuilder 及站点使用证据属于 CLI/config/theme/process consumer
证据，不是目标类型的直接 CLR consumer 证据。

本轮没有能力排除私有、未索引、未声明、DLL、源码复制或 reflection consumer。
因此 eligibility 是“在已审范围内有条件成立”，不是“不存在任何消费者”的证明。

### 2.4 Reflection、serialization 与 Native AOT

在 `HEAD` 的 tracked production source 中，没有发现目标 full name 被
`Type.GetType`、`Assembly.GetType`、Activator、serializer/source generator、
trim/AOT root、配置或 plugin manifest 使用。Architecture test 中的
`Assembly.GetType` 是 test-only current-state assertion，不是 production
consumer。这项静态检索不能覆盖 binary、复制源码、未提交生成代码或私有 consumer。

`Bukit.Cli` 通过 project reference 吸收 `Bukit.PluginHost`，并以 Native AOT
发布。静态证据说明 access narrowing 不改变 AOT root，但不能替代真实 Native AOT
package、发布产物 smoke 和 process-plugin invocation/report 证明。

## 3. 兼容性边界

这是明确的 2.0 breaking change：

- **source compatibility**：外部源码引用类型或成员后重新编译会失败；
- **reflection/type lookup compatibility**：依赖 exported type、public metadata、
  `typeof` 或类型名查找的消费者会失败或观察到不同结果；
- **binary compatibility**：普通 public const 使用通常已把字符串内联到 consumer
  binary，因此既有 binary 可能继续运行；但不能据此宣称 binary compatibility，
  因为 metadata/reflection 消费仍会破坏；
- **wire compatibility**：六个字符串、异常消息、`DiagnosticCode` 和
  `bukit-plugin-v1` 行为保持不变；
- **1.x compatibility**：本任务不回移到 1.x，也不改变 1.x 公共面。

不提供 replacement public API。受支持的外部插件边界是
`bukit-plugin-v1` process protocol 和活动诊断词汇，不是这个同程序集常量 owner。
新增 facade、enum 或 contract class 会扩大公共面并制造第二个 owner。

## 4. 方案比较

### 方案 A：单类型 internalization（推荐）

只实施：

```text
public static class PluginHostErrorCodes
→
internal static class PluginHostErrorCodes
```

同步机械 public API snapshot、current-state Architecture assertions、两份活动治理
文档和 B2 决策账本。closed manifest、协议、fixture 和运行时行为保持不变。

优点：

- 精确减少一个 implementation-public CLR owner；
- 不改变诊断协议或 Host 行为；
- diff 可逐项证明，不把其他 PluginHost candidates 批量带入；
- B1 已先消除“测试依赖 public 才能验证协议”的障碍。

风险：

- 对未知 direct/reflection consumer 是 2.0 breaking；
- 需要真实 AOT/process-plugin 证明和严格 baseline delta 审计。

### 方案 B：继续保留 public（备选）

如果出现具体 CLR consumer、公开搜索刷新发现匹配，或 baseline/AOT 证明出现非目标
drift，则保留 public。可以把该类型继续标为 `2.0-candidate`，不强行为了减少数量而
实施。

优点是零兼容破坏；代价是继续把 implementation owner 暴露为受兼容性约束的 public
surface。

### 方案 C：新增 replacement API 后收窄（拒绝）

新增 public enum、constants class、facade 或 friend assembly 会扩大受治理公共面，
并不能改善 wire contract。增加 `InternalsVisibleTo` 也会重新制造测试耦合，违反
B1 已建立的 public-entry/fixture 证据边界。

## 5. 精确变更边界

### 5.1 唯一生产变更

只修改：

- `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`

生产 diff 必须只有 class access modifier 的 `public` → `internal`。六个成员仍保持
相同名称、顺序、类型、值和 const 语义；`PluginProtocolClient.cs` 零 diff。

### 5.2 测试与 baseline 变更

修改：

- `tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`
  - 保留 B1 的零直接引用、六值 vocabulary、文档和 closed-manifest facts；
  - 将 current public-surface fact 改为 internal/non-exported；
  - baseline 断言改为 14 / 507 / 103；
  - 断言 baseline 不含目标类型。
- 只更新下列历史 fixture 中指向 live current-state 的 508/104 断言为 507/103：
  - `G04CPublicSurfacePilotTests.cs`
  - `G04D1AStaticNotionFacadeRemovalTests.cs`
  - `G04D1BBlockRendererFacadeRemovalTests.cs`
  - `G04D1CM2AtomicRemovalTests.cs`
  - `G04D2APluginSecretMaskerInternalizationTests.cs`
- `docs/governance/bukit-core-public-api-baseline.v1.json`
  - 只能由现行 snapshot 工具机械生成；
  - 预期只删除目标 type record 及其六-member array；
  - 精确结果必须是 14 assemblies / 507 types / 103 candidates。

历史 decision sentence、当时的 remainder count 和历史 ledger snapshot 不得重写。
只有“current baseline”断言随 live state 更新。

### 5.3 治理与交付文档

新增：

- 本设计；
- G-04D2B2 implementation plan；
- G-04D2B2 中文执行/决策账本。

修改：

- `docs/governance/bukit-core-2.0-consumer-declaration.md`
- `guide/dev/public-api-governance.md`

活动治理文档必须记录：

```text
G-04D2B2 single-type internalization decision: only
`Bukit.PluginHost.PluginHostErrorCodes` is narrowed from public to internal in
2.0; the other 103 candidates are not batch-approved.
```

并明确：

- current baseline 为 14 / 507 / 103；
- closed 136-entry manifest 仍是 immutable historical cohort；
- private consumers 仍为 unknown until voluntary declaration；
- 2026-07-23 没有完成新的治理级 GitHub Code Search refresh；
- narrowing 对 direct/reflection consumer 是 2.0 breaking；
- 六个 wire vocabulary 和五个实际 Host 行为不变；
- 其他 PluginHost candidates 未获批。

### 5.4 明确禁止修改

- `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- `src/Bukit-Core/Bukit.PluginHost/PluginPermissionEvaluator.cs`
- `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
- `tests/fixtures/plugin-contracts/plugin-host-error-vocabulary.v1.json`
- `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- `docs/plugins/Bukit 插件协议 v1 规范.md`
- `docs/plugins/Bukit 插件安全模型 ADR.md`
- plugin DTO、schema、config、CLI semantics、official plugins；
- CI、release、gate 和路径工具；
- `InternalsVisibleTo`、replacement API 或新 public contract；
- 其他 PluginHost type visibility；
- G-04D2B1 的历史 spec、plan 和 ledger；
- 受保护的 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。

## 6. TDD 与实施顺序

1. 记录 clean baseline：
   - PluginHost tests 170/170；
   - Architecture tests 130/130；
   - closed manifest blob 精确不变。
2. 将 B1 current-surface Architecture fact 改为 internal/non-exported 和
   14/507/103，形成真实 RED。
3. 只修改目标 class 的访问修饰符，证明 assembly visibility assertion GREEN；
   此时 baseline assertion 应继续 RED。
4. 使用
   `scripts/checks/public-api-drift.sh snapshot` 生成临时 snapshot。
5. 审计临时 snapshot：
   - 14 / 507 / 103；
   - 只删除目标 type record 及六个成员；
   - assembly mapping、其他 type/member、classification 和 metadata 零 drift。
6. 用已审 snapshot 替换 governed baseline。
7. 更新五个 live current-state fixture、两份活动治理文档和 B2 ledger。
8. 运行定向 GREEN、owner checks 和一次 focused check。
9. 对变更执行任务合规与代码质量两轮独立只读复审；如复审要求修改，只能在范围内
   修复并重新完成 focused/owner checks。
10. 在 `/tmp` 独立输出根运行 bounded Native AOT owner proof、发布产物 smoke 和
    Echo process-plugin handshake/manifest/invoke/report 证明；如发生与本任务直接
    相关的失败，只能在范围内修复，并重新完成 focused/owner/AOT proofs。
11. 全部 owner proof 通过后冻结最终 diff。冻结后不得再修改文件。
12. 父任务对冻结 diff 运行一次且仅一次 aggregate targeted check。
13. aggregate 后进行最终只读复审并提供集成选项；如 aggregate 或最终复审发现必须
    修改的问题，停止 parent completion，不得修改后沿用旧 aggregate 结果，也不得
    在同一任务中重跑 aggregate。

实现过程中不得为了让测试通过而修改协议行为或批量重写历史文本。

## 7. 验证边界

定向验证包括：

- `Bukit.PluginHost.Tests` 全项目；
- 相关 Architecture governance/plugin-boundary tests；
- public API drift self-test；
- public API real check；
- closed manifest Git blob；
- active docs links 与 absolute-path scan；
- 一次 `post-change-focused.sh`，传入本任务全部实际变更路径。

Native AOT owner proof 只能使用下列 bounded entrypoint：

```text
bash scripts/build/native-aot.sh \
  2.0.0-g04d2b2 osx-arm64 /tmp/bukit-g04d2b2-aot Release

bash scripts/smoke/release-artifacts.sh \
  /tmp/bukit-g04d2b2-aot/bukit-2.0.0-g04d2b2-osx-arm64.tar.gz osx-arm64
```

process-plugin proof 必须使用上述 archive 或其
`/tmp/bukit-g04d2b2-aot/publish/osx-arm64/bukit` 已发布 CLI，并在 `/tmp` 下准备
独立 Echo plugin fixture；必须分别断言 handshake、runtime manifest、invoke
result 和 `.bukit/reports/plugin-executions` execution report。binding
implementation plan 必须给出 fixture 构造、SHA-256、CLI invocation 和报告断言的
完整命令，不能用测试程序集中的 `PluginProtocolClient` 直接调用替代 published CLI
proof。

明确禁止：

- `scripts/smoke-all.sh`、full/release gate、`test-all` 或 whole-solution tests；
- release asset 创建、上传或发布；
- 修改 build、smoke、release、CI 或 gate scripts；
- 将输出写入工作树或把 `/tmp` 产物纳入 Git diff。

全部 owner proof 通过并冻结 diff 后，父任务运行：

- 从固定基线 `757fb14976ad7337edc2a6fbf925b986222dea6f`
  对全部最终变更运行一次且仅一次
  `bash scripts/checks/post-change-targeted.sh --base 757fb14976ad7337edc2a6fbf925b986222dea6f -- <all final changed paths>`。

如果 AOT owner proof 在 aggregate 前失败，只能修复与本任务 diff 有直接因果关系的
问题。既有、环境或基础设施失败必须单独分类。环境 blocker 时状态只能是
`implementation-complete / qualification-blocked` 或 `proof-blocked`，不得关闭或
宣称 owner proof 通过。

aggregate 之后不得再修复。若 aggregate 或最终只读复审失败，状态保持未关闭并进入
新的、明确授权的 replacement qualification task。

## 8. Stop conditions

出现任一情况立即停止，不扩大修复：

- 发现 direct CLR、reflection/type lookup、serialization、source-generator、
  trim/AOT root 或 private consumer 依赖目标类型；
- public API snapshot 不是精确 14 / 507 / 103；
- snapshot 除目标 type record 和六个成员外还有任何 drift；
- closed 136-entry manifest 任一字节变化；
- 需要修改六个字符串、完整异常 message、`DiagnosticCode`、异常类型或行为；
- 需要让 Host 新发出 `plugin.permissionDenied`；
- 需要修改 timeout、output-limit、path、permission 或 invoke business failure；
- 需要增加 friendship、facade、replacement API、schema 或协议变化；
- 需要修改其他 PluginHost type、CLI semantics、官方插件或 gate；
- 真实 AOT/process-plugin 证明暴露目标 access change 导致的运行回归。

## 9. 完成定义

G-04D2B2 只有同时满足以下条件才能关闭：

- 唯一生产 diff 是目标 class 的 public → internal；
- 六个 const 名称、顺序、类型和值不变；
- B1 的五个 runtime error contracts 与六值 vocabulary 全部通过；
- 目标仍存在于 assembly 内但不再 exported；
- governed baseline 精确为 14 / 507 / 103，且 delta 只有目标；
- closed manifest blob 仍为
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- 活动治理文档同步且历史记录未被重写；
- focused 必须通过；
- 真实 AOT package、release-artifact smoke 和 published CLI
  process-plugin/report proof 必须通过；
- 唯一 aggregate 必须对最终冻结 diff 通过且只执行一次；
- 任务合规、代码质量和最终 aggregate diff 的独立只读复审无 Critical/Important；
- 不存在超限修复或范围漂移。

若真实 AOT/process-plugin proof 因环境、权限、工具链或基础设施原因不可用，必须
诚实记录 blocker，但 G-04D2B2 只能标为 `qualification-blocked`，不能标为完成或
关闭。
