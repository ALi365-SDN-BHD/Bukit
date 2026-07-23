# Bukit Core G-04D2B1 PluginHost 错误码 diagnostic-contract migration 执行账本

日期：2026-07-23
基线：`2.0@2272156f054cb308028b57ba50cc65268a454e30`
范围：只迁移 `PluginHostErrorCodes` 的测试与协议词汇证据

## 决策

G-04D2B1 只把测试期望从 `PluginHostErrorCodes` public const CLR 引用迁移到
`PluginProtocolClient` public 入口和独立协议词汇 fixture。生产源码、类型与成员
可见性、六个字符串、异常格式、权限语义、public API baseline 和 closed candidate
manifest 均保持不变。

G-04D2B1 不授权 G-04D2B2，也不预先决定
`Bukit.PluginHost.PluginHostErrorCodes` 可以 internalize。

## 契约分类

- Host 当前实际输出为 `plugin.unsupportedProtocol`、
  `plugin.invalidResponse`、`plugin.timeout`、`plugin.executionFailed` 和
  `plugin.outputTooLarge`。
- `plugin.permissionDenied` 是保留协议词汇，Host 不发出该错误码。
- 权限拒绝继续使用 `DiagnosticCode.PluginCapabilityMissing`；
  `plugin.permissionDenied` 没有 Host 生产调用点。

## 测试迁移

第一组 RED 使用以下命令：

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests.PluginProtocolClientTests_DoNotConsumeErrorCodeClrType
```

该命令按预期得到 1 个失败、0 个通过，失败输出为
`Assert.DoesNotContain() Failure: Sub-string found`，并明确命中
`PluginProtocolClientTests.cs` 中的 `PluginHostErrorCodes`。这证明 RED 来自
测试层 CLR 依赖，而不是编译或仓库根目录错误。

迁移后，`PluginProtocolClientTests.cs` 对 `PluginHostErrorCodes` 的标识符引用为零。
handshake 协议错误、requestId 不匹配、插件 identity 不匹配、无效 JSON、超时、
输出过大和不安全 artifact path 都从 public 入口断言独立协议字面量与完整 detail。
新增 manifest 非零进程退出用例锁定
`plugin.executionFailed: Plugin process exited with code 7.`，新增 invoke 用例只锁定
inbound `plugin.permissionDenied` 的原样保留，不把它描述为 Host 发出。

第二组 RED 使用以下命令：

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~G04D2B1PluginHostErrorCodeContractTests.ProtocolVocabularyFixture_PreservesExactSixTermsAndActiveDocs
```

在空的 `tests/fixtures/plugin-contracts/` 目录中运行时，该命令按预期得到 1 个失败、
0 个通过，失败输出为缺少
`plugin-host-error-vocabulary.v1.json` 的 `System.IO.FileNotFoundException`。
父目录最初也不存在，因此第一次预检得到 `DirectoryNotFoundException`；创建空目录后
原命令重跑才形成上述被接受的 missing-fixture RED，预检错误未被计作契约 RED。

创建 schema 为 `bukit-plugin-host-error-vocabulary-v1` 的独立 fixture 后，单用例
GREEN 为 1/1。完整 targeted GREEN 的 `PluginProtocolClientTests` 为 16 个通过、
0 个失败、0 个跳过；`G04D2B1PluginHostErrorCodeContractTests` 为 4 个通过、
0 个失败、0 个跳过。

## 治理当前态

- public API baseline 保持 14 assemblies / 508 types / 104 candidates。
- `PluginHostErrorCodes` 仍为 exported public type，六个 public const 的名称、
  顺序和值保持不变。
- closed manifest 保持 136 entries。
- closed manifest blob 保持
  `7b07d6890562387010b52301e9f8716e9bf10ed1`。
- private consumer 仍为 `unknown-until-voluntary-declaration`。

## 明确排除

本任务不修改 `PluginHostErrorCodes`、其六个成员和值、`PluginProtocolClient`、
异常形状、`DiagnosticCode`、权限行为、timeout/output/path 行为和 invoke
business-failure 行为。它不增加 replacement constants、enum、facade、contract
assembly、`InternalsVisibleTo`、friendship、replacement API 或 schema，也不改变
协议、配置、CLI 或 public governance。

public API baseline、closed manifest、consumer declaration、public API governance
guide、插件协议文档、插件安全 ADR、受保护备份目录、CI、release 和 gate 均不在
变更范围内。G-04D2B2 的 eligibility 或 internalization 决策未被授权。

## 验证边界

Task 1 的 owner 验证边界包括 `Bukit.PluginHost.Tests` 完整项目、
`Bukit.Architecture.Tests` 完整项目、public API drift self-test、public API drift
Release 检查、active links 与 no-absolute-paths 文档检查。受保护 diff 和 closed
manifest Git blob 也必须从冻结基线单独核验。本任务只运行一次
`post-change-focused.sh`，并把设计、计划和四个 Task 1 路径一起传入。

实际 owner 结果为：`Bukit.PluginHost.Tests` 170 个通过、0 个失败、0 个跳过；
`Bukit.Architecture.Tests` 130 个通过、0 个失败、0 个跳过；public API drift
self-test 输出 `public API drift self-test OK`；Release drift 构建为 0 warnings、
0 errors；active links 输出 `active documentation links OK`；no-absolute-paths
输出 `public absolute path scan OK`。上述命令均以状态码 0 结束。

从冻结基线执行的 production、public API baseline 和 closed manifest 三项
`git diff --exit-code` 均以状态码 0 结束且没有输出。closed manifest 的
`git hash-object` 输出为
`7b07d6890562387010b52301e9f8716e9bf10ed1`。

本任务唯一一次 `post-change-focused.sh` 已按上述六个路径运行并以状态码 0
结束；其输出再次确认 `Bukit.PluginHost.Tests` 170 个通过、0 个失败、0 个跳过，
以及 `Bukit.Architecture.Tests` 130 个通过、0 个失败、0 个跳过。该 focused
命令没有重复执行。

父任务唯一 `post-change-targeted.sh` aggregate 与最终独立只读复审由 controller
在提交后执行；本账本不声明这些父任务证明已经通过，也不运行 full、release、AOT、
`test-all`、`smoke-all` 或 whole-solution gate。

## Stop conditions

以下任一情况都会使本任务立即停止，而不会扩大修复：

- 建立契约需要修改 production。
- 建立契约需要改变六值、完整 message、异常类型或 `DiagnosticCode`。
- 建立契约需要让 Host 实际发出 `plugin.permissionDenied`。
- 建立契约需要改变 invoke business failure。
- 新发现 public/protected signature、reflection、serialization、source-generator、
  AOT root 或直接消费者绑定该 full name。
- 新发现 private consumer 声明直接使用该类型。
- 建立契约需要增加 friendship、facade、replacement API 或 schema。
- baseline 发生任何 drift，或者计数不再是 14/508/104。
- closed manifest 任一字节发生变化。
- 测试必须弱化 timeout、output-limit、路径、权限或错误断言才能通过。

本次实施未触发上述停止条件；该结论只覆盖 Task 1 已执行的测试与保护性检查，
不替代父任务 aggregate 或独立只读复审。
