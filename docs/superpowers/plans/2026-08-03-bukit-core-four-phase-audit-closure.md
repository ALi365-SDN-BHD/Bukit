# Bukit Core Four-Phase Audit Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不扩大产品语义、不触碰历史参考目录的前提下，闭合四阶段复审确认的 11 项 Important、6 项 Conditional Important、7 项 Minor，并同步消除 1 项 BodyCache 并发加固缺口；所有生产变更均以 Native AOT 可发布为硬门槛。

**Architecture:** 采用 12 个串行、单写者任务。每个任务先建立机器可读 verification closure，再以失败测试固定缺陷语义，实施最小修复，运行完整专项测试并进入一次专项复审。跨任务共用的边界只允许四项最小架构扩展：插件资源配置可达性、基于文件句柄的 no-follow 复制、ImageSharp 完整解码验证、不可约束 injected transport 的 fail-closed 边界。最后仅做 delta-only 统一复审、已列明专项测试与 CLI Native AOT 发布/烟雾测试；不运行全量、发布或未命名 gate。

**Tech Stack:** .NET/C#、xUnit、System.Text.Json source generation、YAML 配置、SixLabors.ImageSharp 3.1.12、Windows/POSIX 原生文件句柄、Bukit codex-workflow、Native AOT (`dotnet publish -p:PublishAot=true`)。

## Global Constraints

- 基线固定为 `main@122c90775c1900cd95256bbe37ad94dd9c178a06`；执行开始时若 `main` 已移动，先记录新 HEAD 并重新生成全部 closure，不得机械套用旧证据。
- 范围只包含下方 `I-01..I-11`、`C-01..C-06`、`M-01..M-07` 与 `H-01`。任何相邻重构、API 美化、性能优化、文档重写均另行报告，不得顺手实施。
- 不修改或重建 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。
- 采用独立 `codex/` worktree；建立单写者队列。任一时刻只有当前任务可处于 `writing`、`testing` 或 `review_wait`。
- 每个任务必须运行 `closure`，列出 changed files、direct consumers、public/serialized-contract consumers 与 exact specialty command；所有 `unmapped` 路径必须在写代码前解决。
- 所有测试先 RED 后 GREEN。RED 必须因计划中的缺陷失败；编译错误、测试夹具错误或环境错误不算有效 RED。
- 每个任务只进行一次专项复审；只有 Critical/Important 才回到实现并 scoped re-review，Minor 记录但不阻断。
- Task 2-11 开始前依次执行 `queue acquire --task task-NN-<slug>`；RED 可在 `writing` 执行，生产修改完成后必须依次转为 `testing`、执行最终 GREEN、转为 `review_wait`、完成专项复审，再转为 `done`。Task 3-11 的 slug 固定为 `process-tools`、`runtime-ownership`、`list-hash`、`image-variants`、`data-budgets`、`media-atomicity`、`feed-determinism`、`dev-capacity`、`transport-copy`。若 acquire 或 transition 失败，停止写入，不得绕过队列。
- 每个 task 将 closure JSON 保存到 `/tmp/codex-reports/bukit-core-task-NN-closure.json`，将符合 `review-scope` schema 的专项证据保存到 `/tmp/codex-reports/bukit-core-task-NN-evidence.json`。每个 GREEN command 仅在 HEAD、closure、exact command、相关环境状态和 SDK 均一致时 `cache check`；执行后以真实 exit code/duration `cache record`，不得记录环境值。
- 每个 implementation/test/review phase 结束后执行 `metrics add`，`--duration-ms` 使用实际毫秒，`--command-label` 只用 `taskNN-implementation`、`taskNN-specialty`、`taskNN-review` 这类短标签，不保存 raw command。
- 不运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、whole-solution tests、full/release gates、`post-change-*` 或未列明聚合矩阵。
- 不 push、不部署、不发布包。本计划的审定只授权本地实现与所列验证；本地 merge 仍需用户另行指令。
- 测试和生产代码不得使用动态程序集加载、运行时生成代码、反射式未声明序列化或其他破坏 Native AOT 的机制。

## Finding Ledger and Closure Ownership

| ID | 等级 | 问题 | 唯一闭合任务 |
|---|---|---|---|
| I-01 | Important | workflow policy 缺少 `.csproj` 映射 | Task 1 |
| I-02 | Important | 子进程超时只等待父进程退出，输出 drain 可永久挂起 | Tasks 2-3 |
| I-03 | Important | ContentPipeline/Doctor body store 所有权泄漏 | Task 4 |
| I-04 | Important | list hash 使用旧 page manifest，产生一轮陈旧 | Task 5 |
| I-05 | Important | 图片 variants 陈旧、无所有权、可递归输入 | Task 6 |
| I-06 | Important | PluginRunner hook 前后缺失取消检查 | Task 4 |
| I-07 | Important | DataFiles 在 entry limit 前全量排序缓冲 | Task 7 |
| I-08 | Important | MediaIndexManager 先截断 live index 且跨实例无协调 | Task 8 |
| I-09 | Important | Atom 非零 offset 被直接格式化为字面 `Z` | Task 9 |
| I-10 | Important | taxonomy root baseUrl 生成双斜杠 | Task 9 |
| I-11 | Important | WebSocket 与请求 gate 同为 64，拒绝路径不可达并阻塞静态请求 | Task 10 |
| C-01 | Conditional Important | Git/External 输出捕获无界 | Task 3 |
| C-02 | Conditional Important | DataFiles 原始字节预算不能约束 UTF-16、preflight、DOM、结果图放大 | Task 7 |
| C-03 | Conditional Important | Notion factory/injected transport 的 redirect/Host canonical gap | Task 11 |
| C-04 | Conditional Important | media move collision 后未复核赢家 | Task 8 |
| C-05 | Conditional Important | DirectoryCopy validate-to-open symlink TOCTOU | Task 11 |
| C-06 | Conditional Important | 插件 CPU/内存限制在生产配置链不可达 | Task 2 |
| M-01 | Minor | BodyCache metrics 在 render 前快照 | Task 4 |
| M-02 | Minor | ResolveGit 忽略 WaitForExit bool 后阻塞 ReadToEnd | Task 3 |
| M-03 | Minor | resize exit 0 后接受任意已有输出 | Task 8 |
| M-04 | Minor | 图片 magic 检测过浅且夹具不可解码 | Task 8 |
| M-05 | Minor | injected ImageAssetLocalizer HttpClient DNS fail-open/重绑定/redirect seam | Task 8 |
| M-06 | Minor | Markdown `OrdinalIgnoreCase` 并列遇到 `MaxItems` 不确定 | Task 9 |
| M-07 | Minor | Dev accept loop 的 TCS 在 `Task.WhenAll` fault 时可能不完成 | Task 10 |
| H-01 | Hardening | BodyCache failure+trim 并发可留下孤儿 LRU node | Task 4 |

## Approved Minimal Architecture Decisions

### AD-1: 插件资源限制只增加可达性，不强加默认额度

新增 public config DTO：

```csharp
public sealed record PluginResourceLimitOptions(
    int? MaxCpuTimeMs = null,
    long? MaxMemoryBytes = null);
```

`PluginConfigEntry` 与 `ResolvedPlugin` 增加 nullable `Resources`。YAML 键固定为 `resources.maxCpuTimeMs` 和 `resources.maxMemoryBytes`；未配置保持 `null`，已配置必须大于零。`PluginProtocolClient` 映射为 `PluginProcessRequest.MaxCpuTime = TimeSpan.FromMilliseconds(...)` 与 `MaxMemoryBytes`。如 source-generated context 无法从根类型覆盖 DTO，则显式加入 `[JsonSerializable(typeof(PluginResourceLimitOptions))]`；禁止反射回退。

### AD-2: DirectoryCopy 以已打开句柄建立信任边界

新增 internal `ISafeSourceFileOpener`/`VerifiedSourceFile`。Windows 用 `CreateFileW` + `FILE_FLAG_OPEN_REPARSE_POINT`，拒绝 reparse point，并通过 `GetFinalPathNameByHandleW` 验证句柄实际目标。Linux/macOS 用 `open(O_RDONLY | O_CLOEXEC | O_NOFOLLOW)`，由 `SafeFileHandle` 构造 `FileStream`；Linux 使用 `/proc/self/fd/<fd>`，macOS 使用 `fcntl(F_GETPATH)` 取得已打开目标，确认仍在捕获的 source root 内。禁止以第二次 pathname 检查冒充原子修复。

### AD-3: 图片安全判断升级为“签名匹配且可完整识别”

在 `Bukit.Content` 引用中央锁定的 `SixLabors.ImageSharp` 3.1.12。新增 internal `ImageContentValidator`：先校验允许 MIME 与文件签名，再调用 `Image.IdentifyAsync`，确认容器可解码且检测格式与允许 MIME 一致。下载缓存、move winner 与 resize 临时输出共用该验证器。ImageSharp 3.1.12 未提供解码器的 AVIF/ICO 不再被本地化，返回现有安全失败结果；禁止仅凭 magic 保留接受。因 Core 新增包引用，Task 8 与最终 Task 12 均必须通过 CLI Native AOT publish。

### AD-4: 不再把任意 injected HttpClient 当作可约束的安全传输

`NotionClient(NotionClientOptions, HttpClient)` public 签名为 source/binary compatibility 保留并标记 obsolete，但对任意 injected client fail closed，抛出稳定的 `NotSupportedException`，因为既有实例无法可靠关闭内部 redirect handler。新增 `NotionClient(NotionClientOptions, HttpMessageHandler)` 安全入口：Bukit 拥有由该 handler 构造的 HttpClient；对 `SocketsHttpHandler`/`HttpClientHandler` 强制 `AllowAutoRedirect=false`，其他自定义 handler 明确属于调用方 trusted transport。所有 Bukit factory 使用 owned default handler；内部测试使用单跳 fake handler。ImageAssetLocalizer 的 injected `HttpClient` 仅为 internal test seam，直接替换为单跳 `HttpMessageHandler` seam，生产 handler 关闭自动 redirect，并由 localizer 对每个允许的 redirect 重新做 scheme/host/DNS/connect 验证。

### Compatibility Impact Accepted by This Approval

- 未配置插件资源限制的行为不变；非正数新字段现在配置失败。
- Git/ExternalTool 任一 stdout/stderr 超过 4 MiB 时 fail closed，不再允许用无界内存换取成功。
- 可疑或无法由已批准 decoder 识别的图片 fail closed；AVIF/ICO 本地化属于本轮明确接受的兼容性收紧。
- 任意 public injected Notion `HttpClient` 不再执行请求；调用方需改用安全 handler overload。默认构造器和 Bukit 内部 factory 行为保持兼容。
- DataFiles 原有正常预算内输入不变；超过新增 decoded/result-graph budget 的输入现在以稳定配置错误失败。

---

## Task 1: Restore Verification-Closure Coverage

**Closes:** I-01.

**Files:**

- Modify: `scripts/checks/codex-workflow-policy.v1.json`
- Modify: `scripts/checks/codex-workflow-self-test.sh`
- Test: `scripts/checks/codex-workflow-self-test.sh`

- [ ] **Step 1: 建立队列与 Task 1 closure**

```bash
python3 scripts/checks/codex-workflow.py queue init --state /tmp/bukit-core-audit-closure-queue.json
python3 scripts/checks/codex-workflow.py queue acquire --state /tmp/bukit-core-audit-closure-queue.json --task task-01-workflow-policy
python3 scripts/checks/codex-workflow.py closure --repo . --policy scripts/checks/codex-workflow-policy.v1.json --changed src/Bukit-Core/Bukit.Content/Bukit.Content.csproj --changed src/Bukit-Core/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj
```

Expected: 当前命令报告两个 `.csproj` 为 `unmapped`，作为 I-01 的有效 RED 证据。

- [ ] **Step 2: 写 self-test 固定项目文件映射合同**

在 self-test 中构造 Core `.csproj` changed set，断言 closure 非空、无 `unmapped`，且至少映射对应项目的专项测试；同时断言 `Directory.Packages.props` 映射到 Architecture + 所有直接受影响的 Core consumer tests。

- [ ] **Step 3: 运行 RED**

```bash
bash scripts/checks/codex-workflow-self-test.sh
```

Expected: FAIL，失败原因仅为新增 `.csproj`/central package mapping 断言。

- [ ] **Step 4: 最小扩展 policy**

为 `src/Bukit-Core/*/*.csproj` 增加项目级规则；为 `Directory.Packages.props` 增加 central-package 规则。规则必须生成 `static-parallel` 或 `dotnet-serial` 分类、直接 consumer tests 和 Architecture tests，不得触发 full/release gate。

- [ ] **Step 5: 运行 GREEN、分类与专项复审**

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-01-workflow-policy --to testing
bash scripts/checks/codex-workflow-self-test.sh
python3 scripts/checks/codex-workflow.py classify --policy scripts/checks/codex-workflow-policy.v1.json --path scripts/checks/codex-workflow-policy.v1.json --path scripts/checks/codex-workflow-self-test.sh --test-command "bash scripts/checks/codex-workflow-self-test.sh"
```

Expected: self-test PASS；classify 将 policy/self-test owner 验证标成单独执行，不带历史 fixtures。

- [ ] **Step 6: 提交并释放队列**

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-01-workflow-policy --to review_wait
git add scripts/checks/codex-workflow-policy.v1.json scripts/checks/codex-workflow-self-test.sh
git commit -m "test(workflow): map Core project verification closures"
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-01-workflow-policy --to done
```

## Task 2: Make Plugin Process Termination and Resource Limits End-to-End

**Closes:** I-02 的 PluginHost 部分、C-06。

**Files:**

- Create: `src/Bukit-Core/Bukit.Plugin.Abstractions/Config/PluginResourceLimitOptions.cs`
- Modify: `src/Bukit-Core/Bukit.Plugin.Abstractions/Config/PluginConfigEntry.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginConfigLoader.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/ResolvedPlugin.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- Modify: `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs`
- Modify: `src/Bukit-Core/Bukit.Plugin.Abstractions/PluginJsonSerializerContext.cs`
- Test: `tests/Bukit.Plugin.Abstractions.Tests/PluginConfigDtoTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginConfigLoaderTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs`

- [ ] **Step 1: 生成 closure 并确认 config consumers**

```bash
python3 scripts/checks/codex-workflow.py queue acquire --state /tmp/bukit-core-audit-closure-queue.json --task task-02-plugin-process
python3 scripts/checks/codex-workflow.py closure --repo . --policy scripts/checks/codex-workflow-policy.v1.json --changed src/Bukit-Core/Bukit.Plugin.Abstractions/Config/PluginConfigEntry.cs --changed src/Bukit-Core/Bukit.PluginHost/PluginConfigLoader.cs --changed src/Bukit-Core/Bukit.PluginHost/ResolvedPlugin.cs --changed src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs --changed src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs
```

Expected: closure 包含 Plugin.Abstractions、PluginHost、Config/CLI 直接 consumers；无 unmapped。

- [ ] **Step 2: 写资源配置与超时 drain 的 RED tests**

增加以下合同测试：

```csharp
[Fact] public void Load_Resources_MapsPositiveCpuAndMemoryLimits();
[Theory] public void Load_Resources_RejectsNonPositiveLimits(long value);
[Fact] public async Task InvokeProcessAsync_ForwardsConfiguredResourceLimits();
[Fact] public async Task RunAsync_Timeout_KillsTreeAndBoundsStreamDrain();
[Fact] public async Task RunAsync_Cancellation_KillsTreeAndBoundsStreamDrain();
```

最后两个测试的 probe 必须保留继承 stdout/stderr 的孙进程；断言调用在 `termination grace + drain grace + tolerance` 内结束，且异常类别分别保持 timeout/cancellation。

- [ ] **Step 3: 运行 RED**

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-02-plugin-process --to testing
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
```

Expected: 仅新增配置映射、limits forwarding 与 drain deadline 测试失败。

- [ ] **Step 4: 实施 AD-1 与有界终止状态机**

`SystemProcessRunner` 的完成顺序固定为：正常等待；timeout/cancel 后 kill entire tree；有界等待父进程退出；有界等待 stdout/stderr pump；超出 drain grace 时停止等待并返回原始 timeout/cancel 结果。所有后台 pump 异常必须被观察；禁止无限 `ReadToEndAsync`。

- [ ] **Step 5: 运行 GREEN 与完整专项集**

```bash
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

Expected: 全部 PASS；新增 YAML 字段缺省时旧配置序列化与加载结果不变。

- [ ] **Step 6: 专项复审、提交、释放队列**

审查只覆盖 process state transitions、public/serialized config compatibility、AOT source-gen reachability。随后：

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-02-plugin-process --to review_wait
git add src/Bukit-Core/Bukit.Plugin.Abstractions/Config/PluginResourceLimitOptions.cs src/Bukit-Core/Bukit.Plugin.Abstractions/Config/PluginConfigEntry.cs src/Bukit-Core/Bukit.Plugin.Abstractions/PluginJsonSerializerContext.cs src/Bukit-Core/Bukit.PluginHost/PluginConfigLoader.cs src/Bukit-Core/Bukit.PluginHost/ResolvedPlugin.cs src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs tests/Bukit.Plugin.Abstractions.Tests/PluginConfigDtoTests.cs tests/Bukit.PluginHost.Tests/PluginConfigLoaderTests.cs tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs
git commit -m "fix(plugins): bound termination and expose resource limits"
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-02-plugin-process --to done
```

## Task 3: Bound Git and External Tool Process Lifecycles

**Closes:** I-02 的 CLI/Engine 部分、C-01、M-02。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Git.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/ExternalToolProcessRunner.cs`
- Inspect as direct consumers, no planned edit: `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Validation.cs`, `src/Bukit-Core/Bukit.Engine/ImageOptimizer.cs`, `src/Bukit-Core/Bukit.Engine/ScssCompiler.cs`
- Test: `tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs`
- Test: `tests/Bukit.Engine.Tests/ExternalToolProcessRunnerTests.cs`

- [ ] **Step 1: 写 RED tests**

```csharp
[Fact] public async Task RunGitAsync_OutputBeyondLimit_FailsWithoutUnboundedCapture();
[Fact] public async Task RunGitAsync_Timeout_WithInheritedPipes_ReturnsWithinDrainDeadline();
[Fact] public void ResolveGit_ProbeTimeout_DoesNotBlockOnReadToEnd();
[Fact] public async Task ExternalTool_OutputBeyondLimit_TerminatesTreeAndReturnsBoundedDiagnostic();
[Fact] public async Task ExternalTool_Timeout_WithInheritedPipes_ReturnsWithinDrainDeadline();
```

限制按 UTF-8 byte count，而不是 `string.Length`。Git 与 ExternalTool 各流 hard cap 固定为 4 MiB；超限立即终止进程树。异常诊断最多保留 32 KiB head + 32 KiB tail + truncation marker，不把完整输出留在第二个缓冲区。

- [ ] **Step 2: 运行 RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

Expected: 新增 output cap、probe timeout 与 inherited-pipe deadline 测试失败。

- [ ] **Step 3: 统一有限状态机语义**

实现有界 byte collector；`ResolveGit` 必须检查等待结果，在 probe 超时后 kill/有界 drain 并返回不可用，不得先无限 `ReadToEnd`。Git 与 ExternalTool 维持各自公开异常/结果合同，禁止借此引入新的公共抽象。

- [ ] **Step 4: 运行 GREEN 与 direct-consumer tests**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

Expected: 全部 PASS；现有短输出文本与 exit-code diagnostics 不变。

- [ ] **Step 5: 专项复审与提交**

重点检查 byte cap 是否在读取时生效、所有 pump task 是否被观察、timeout/cancel 是否保留原始语义。提交：

```bash
git add src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Git.cs src/Bukit-Core/Bukit.Engine/ExternalToolProcessRunner.cs tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs tests/Bukit.Engine.Tests/ExternalToolProcessRunnerTests.cs
git commit -m "fix(process): bound tool output and stream draining"
```

## Task 4: Close Body Ownership, Cancellation, Metrics, and LRU Races

**Closes:** I-03、I-06、M-01、H-01。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Engine/ContentPipeline.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/PluginRunner.cs`
- Modify: `src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs`
- Test: `tests/Bukit.Engine.Tests/ContentPipelineTests.cs`
- Test: `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`
- Test: `tests/Bukit.Engine.Tests/PluginRunnerTests.cs`
- Test: `tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs`
- Test: `tests/Bukit.Engine.Tests/PageRenderDispatcherMetricsTests.cs`

- [ ] **Step 1: 写 RED tests 固定所有权与取消边界**

增加 disposable tracking body store，断言 pipeline/doctor 对内部创建对象 exactly-once dispose、对外部注入对象 never dispose、异常路径也 dispose。PluginRunner 分别在 before hook 前已取消、before 后取消、after 前取消时断言后续 hook/核心动作不执行。

- [ ] **Step 2: 写 metrics/LRU 并发 RED tests**

```csharp
[Fact] public async Task DispatchAsync_ReportsCacheMetricsAfterRenderingCompletes();
[Fact] public async Task FailedFactoryConcurrentWithTrim_LeavesNoOrphanLruNode();
```

第二项使用 barrier 控制失败 factory 与 trim 交错，并通过 internal test seam/asserted diagnostic count 同时确认 dictionary 与 LRU 一致，不靠重复循环碰运气。

- [ ] **Step 3: 运行 RED**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 4: 实施最小修复**

用显式 `ownsBodyStore` + `try/finally` 表达生命周期；PluginRunner 在每个用户 hook 与核心阶段边界调用 `ThrowIfCancellationRequested`。metrics 在所有 render tasks 完成后投影。BodyCache 的 dictionary 删除与 LRU unlink 必须在同一锁域/同一线性化点完成，且 trim 对已被失败路径删除的 node 为幂等 no-op。

- [ ] **Step 5: GREEN、专项复审、提交**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
git add src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs src/Bukit-Core/Bukit.Engine/ContentPipeline.cs src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs src/Bukit-Core/Bukit.Engine/Plugins/PluginRunner.cs src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs tests/Bukit.Engine.Tests/ContentPipelineTests.cs tests/Bukit.Engine.Tests/PluginRunnerTests.cs tests/Bukit.Engine.Tests/PageRenderDispatcherMetricsTests.cs tests/Bukit.Cli.Tests/DoctorCommandTests.cs
git commit -m "fix(runtime): close ownership cancellation and cache races"
```

## Task 5: Hash Lists from Current Build Inputs

**Closes:** I-04。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Incremental/IncrementalBuildEngine.cs`
- Test: `tests/Bukit.Engine.Tests/IncrementalBuildEngineTests.cs`
- Test: `tests/Bukit.Engine.Tests/IncrementalBuildEngineAsyncTests.cs`
- Test: `tests/Bukit.Engine.Tests/PageRenderDispatcherLazyBodyTests.cs`

- [ ] **Step 1: 写两轮构建 RED test**

首轮生成列表；第二轮只修改现有 page 的 list-visible metadata/content，断言列表在第二轮立即失效并更新；第三轮无变化，断言列表复用。再覆盖 page add/remove。

- [ ] **Step 2: RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "FullyQualifiedName~IncrementalBuildEngine|FullyQualifiedName~PageRenderDispatcher"
```

- [ ] **Step 3: 实施 current-input fingerprint**

在 page 当前输入 hash 已确定后构造 list dependency fingerprint；禁止从 prior manifest entry 回填当前 dependency。保持 manifest schema 不变；若必须变更 serialized contract，则在实施前停止并请求 scope extension。

- [ ] **Step 4: GREEN 与完整 Engine 专项**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 5: 专项复审与提交**

```bash
git add src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs src/Bukit-Core/Bukit.Engine/Incremental/IncrementalBuildEngine.cs tests/Bukit.Engine.Tests/IncrementalBuildEngineTests.cs tests/Bukit.Engine.Tests/IncrementalBuildEngineAsyncTests.cs tests/Bukit.Engine.Tests/PageRenderDispatcherLazyBodyTests.cs
git commit -m "fix(incremental): hash lists from current page inputs"
```

## Task 6: Give Generated Image Variants Explicit Ownership and Freshness

**Closes:** I-05。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ImageProcessingPlugin.cs`
- Test: `tests/Bukit.Engine.Tests/ImageProcessingPluginTests.cs`

- [ ] **Step 1: 写 RED tests**

覆盖：源图改变时已有 variant 重建；尺寸配置减少时旧 variant 删除；generated variant 不再次作为 source；失败构建不把半成品登记为 output；`__plugin_outputs` 与 `__image_srcsets` 一致。

- [ ] **Step 2: RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~ImageProcessingPluginTests
```

- [ ] **Step 3: 实施 ownership manifest**

只使用现有 plugin output channel：成功生成后发布 normalized variant paths 到 `__plugin_outputs`，并从 source discovery 排除当前/历史受管 outputs。freshness 至少包含 source hash、目标宽度、格式与处理器配置；原子替换后才登记。

- [ ] **Step 4: GREEN 与 Engine 专项**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 5: 专项复审与提交**

```bash
git add src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ImageProcessingPlugin.cs tests/Bukit.Engine.Tests/ImageProcessingPluginTests.cs
git commit -m "fix(images): own and refresh generated variants"
```

## Task 7: Bound DataFiles Enumeration, Decoding, Parsing, and Projection

**Closes:** I-07、C-02。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/DataFilesPlugin.cs`
- Test: `tests/Bukit.Engine.Tests/DataFilesPluginTests.cs`

- [ ] **Step 1: 写 deterministic RED tests**

用计数 enumerable/受控文件流断言读取到 `MaxEntries + 1` 即停止，不先全量 `OrderBy`。加入 ASCII、UTF-8 多字节、UTF-16、深层 JSON/YAML、大 scalar、结果节点数超限测试；每项断言稳定的用户诊断，不以 OOM 为测试机制。

- [ ] **Step 2: RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~DataFilesPluginTests
```

- [ ] **Step 3: 实施分层预算**

在枚举阶段用 bounded top-N/deterministic selection，最多保留 `MaxEntries + 1`。读取阶段保持现有 16 MiB/file 与 64 MiB/build raw-byte 默认，并用严格 BOM/encoding 规则将 decoded chars 限为 `min(raw byte limit, int.MaxValue)`。解析前保持 64 depth、250,000 nodes，并增加单 scalar ≤ decoded-char limit；投影结果累计 string chars ≤ 64 MiB、collection entries ≤ 250,000。超限统一 fail closed；preflight 必须是单遍且不复制完整内容，禁止先构造第二份完整 DOM 后再计数。

- [ ] **Step 4: GREEN 与 Engine 专项**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 5: 专项复审与提交**

```bash
git add src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/DataFilesPlugin.cs tests/Bukit.Engine.Tests/DataFilesPluginTests.cs
git commit -m "fix(data): enforce bounded parse and projection budgets"
```

### Task 7 执行台账（2026-08-04）

- **状态：COMPLETE。** I-07、C-02 的实现、专项验证和独立复审已经完成；本任务未暂存、未提交。
- 初轮实现将 DataFiles 的枚举、严格解码、JSON/YAML 增量解析与结果投影纳入分层预算；专项 `DataFilesPluginTests` 44/44、完整 `Bukit.Engine.Tests` 2,125/2,125 GREEN。
- Fix round 1 关闭两项 Important：拒绝四种完整 UTF-7 BOM 签名；JSON 属性名按解码后字符数、数字按跨读取块保留的原始词素字符数执行 scalar budget。未扩大两文件实现范围。
- Fix round 1 专项：`dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~DataFilesPluginTests`，51/51 GREEN。
- Fix round 1 完整 Engine：`dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`，2,132/2,132 GREEN。
- 独立 scoped rereview：Critical 0 / Important 0 / Minor 0；证据见 `/tmp/codex-reports/bukit-core-task-07-reentry-rereview-1.md`。
- 实施与 RED/GREEN/cache/metrics 证据见 `/tmp/codex-reports/bukit-core-task-07-reentry-report.md`；Task 7 仍不声明 full/release gate 或 Native AOT 结果。

## Task 8: Make Media Writes Atomic, Winners Verified, and Images Decodable

**Closes:** I-08、C-04、M-03、M-04、M-05。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Content/Bukit.Content.csproj`
- Create: `src/Bukit-Core/Bukit.Content/Media/ImageContentValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Media/MediaIndexManager.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ImageProcessingPlugin.cs`
- Test: `tests/Bukit.Content.Tests/ImageAssetLocalizerTests.cs`
- Create: `tests/Bukit.Content.Tests/MediaIndexManagerTests.cs`
- Test: `tests/Bukit.Engine.Tests/ImageProcessingPluginTests.cs`
- Test: `tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj`

- [ ] **Step 1: 写 atomicity/collision RED tests**

两个 `MediaIndexManager` 实例并发更新同一目录，断言最终 JSON 可解析且包含双方成功提交；在 replace 前注入失败，断言旧 index 字节不变。move collision 测试让竞争者写入错误内容，断言 loser 不把赢家当成功。

- [ ] **Step 2: 写真实图片与 transport RED tests**

用 ImageSharp 在测试中编码 1x1 PNG/JPEG/WebP；损坏容器仅保留合法 magic，断言拒绝；合法 AVIF/ICO signature 也必须因缺少已批准 decoder fail closed。resize exit 0 但不创建/不更新/创建不可解码输出，全部失败。injected handler 路径覆盖 redirect 到 private host、Host header 与 URI host 不一致、DNS validation unavailable，全部 fail closed。

- [ ] **Step 3: RED**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --filter "FullyQualifiedName~ImageAssetLocalizer|FullyQualifiedName~MediaIndexManager"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~ImageProcessingPluginTests
```

- [ ] **Step 4: 实施原子 index 与 winner revalidation**

以目标 index path 为键建立进程内协调；跨进程使用 lock file/独占 handle。锁内重新读取最新 index、merge、写同目录临时文件、flush、atomic replace/rename。绝不 `File.Create(livePath)`。move collision 后重新打开 winner，以 expected hash/length/MIME/decode 全部验证后才成功。

- [ ] **Step 5: 实施 AD-3 与 injected transport fail-closed**

所有临时输出必须在本次运行创建且 mtime/identity 可归因；调用 `ImageContentValidator` 后才 atomic move。internal injected `HttpClient` constructor 改为 injected single-hop `HttpMessageHandler`，测试 fake 不执行隐式 redirect；生产 handler 设 `AllowAutoRedirect=false`，localizer 对每个 3xx `Location` 重新执行 SSRF guard 后显式发送，最多 5 跳。不得把 factory 安全性推断到任意 injected handler。

- [ ] **Step 6: GREEN、AOT 与专项复审**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
aot_dir="$(mktemp -d /tmp/bukit-aot-media.XXXXXX)"
dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj -c Release -r osx-arm64 --self-contained true -p:PublishAot=true -o "$aot_dir/publish"
"$aot_dir/publish/bukit" version
```

Expected: tests PASS；AOT publish 与 version smoke PASS；不保留临时 publish 产物到仓库。

- [ ] **Step 7: 提交**

```bash
git add src/Bukit-Core/Bukit.Content/Bukit.Content.csproj src/Bukit-Core/Bukit.Content/Media/ImageContentValidator.cs src/Bukit-Core/Bukit.Content/Media/MediaIndexManager.cs src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/ImageProcessingPlugin.cs tests/Bukit.Content.Tests/ImageAssetLocalizerTests.cs tests/Bukit.Content.Tests/MediaIndexManagerTests.cs tests/Bukit.Engine.Tests/ImageProcessingPluginTests.cs
git commit -m "fix(media): atomically persist and validate image artifacts"
```

## Task 9: Normalize Feed Time, Root URLs, and Markdown Ordering

**Closes:** I-09、I-10、M-06。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Engine/AtomFeedGenerator.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyFeedWriter.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- Test: `tests/Bukit.Engine.Tests/RssGeneratorTests.cs`
- Test: `tests/Bukit.Engine.Tests/TaxonomyFeedWriterTests.cs`
- Test: `tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs`

- [ ] **Step 1: 写 RED tests**

Atom 输入 `2026-08-03T08:00:00+08:00` 必须输出等价 UTC `2026-08-03T00:00:00Z`。taxonomy 分别用 `https://example.test` 与尾斜杠版本，根路径只能有一个 `/`。Markdown 建立大小写折叠相同的文件名并设置 `MaxItems=1`，多次运行选择一致。

- [ ] **Step 2: RED**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --filter FullyQualifiedName~MarkdownFolderProviderTests
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "FullyQualifiedName~Atom|FullyQualifiedName~TaxonomyFeedWriterTests"
```

- [ ] **Step 3: 实施 canonical formatting**

Atom 对 `DateTimeOffset` 调用 `ToUniversalTime()` 后以 invariant UTC 格式输出。URL 使用现有 canonical join helper；若不存在，只在 TaxonomyFeedWriter 内用 `TrimEnd('/') + relative`，不新建公共 URL framework。Markdown 排序增加 `StringComparer.Ordinal` 作为最终 tie-breaker，再应用 `Take(MaxItems)`。

- [ ] **Step 4: GREEN、复审、提交**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
git add src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs src/Bukit-Core/Bukit.Engine/AtomFeedGenerator.cs src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyFeedWriter.cs tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs tests/Bukit.Engine.Tests/RssGeneratorTests.cs tests/Bukit.Engine.Tests/TaxonomyFeedWriterTests.cs
git commit -m "fix(feeds): canonicalize timestamps urls and ordering"
```

## Task 10: Separate WebSocket Capacity from Request Admission

**Closes:** I-11、M-07。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevServerHost.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/IDevWebSocketHub.cs`
- Test: `tests/Bukit.Cli.Tests/DevCommandTests.cs`

- [ ] **Step 1: 写 RED concurrency tests**

占满 64 个 WebSocket 后，第 65 个 WebSocket 必须到达 hub 并快速获得 429/明确拒绝；同一时刻普通静态请求仍成功。另让一个 accept/connection task fault，断言 host completion task 必然完成为 fault/cancel，不悬挂。

- [ ] **Step 2: RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~DevServer|FullyQualifiedName~DevWebSocket"
```

- [ ] **Step 3: 实施两级 admission**

请求 gate 不再被长生命周期 WebSocket 持有：仅保护解析/路由/普通请求工作；升级后释放 request lease，再由 hub 独立 64-seat gate 控制连接。用 `try/catch/finally` 或 completion continuation 确保 accept loop TCS 在成功、取消、fault 三种终态都 exactly-once 完成。

- [ ] **Step 4: GREEN、复审、提交**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
git add src/Bukit-Core/Bukit.Cli/Commands/Dev/DevServerHost.cs src/Bukit-Core/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs src/Bukit-Core/Bukit.Cli/Commands/Dev/IDevWebSocketHub.cs tests/Bukit.Cli.Tests/DevCommandTests.cs
git commit -m "fix(dev): isolate websocket capacity and complete faults"
```

## Task 11: Enforce Canonical Notion Transport and Handle-Based Copy Safety

**Closes:** C-03、C-05。

**Files:**

- Modify: `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs`
- Modify: `src/Bukit-Core/Bukit.Notion/Transport/NotionRequestSemantics.cs`
- Modify: `src/Bukit-Core/Bukit.Notion/Transport/NotionClientOptions.cs`
- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionContentClient.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/DoctorNotionChecker.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/DirectoryCopy.cs`
- Create: `src/Bukit-Core/Bukit.Engine/IO/ISafeSourceFileOpener.cs`
- Create: `src/Bukit-Core/Bukit.Engine/IO/VerifiedSourceFile.cs`
- Create: `src/Bukit-Core/Bukit.Engine/IO/PlatformSafeSourceFileOpener.cs`
- Test: `tests/Bukit.Notion.Tests/NotionClientTests.cs`
- Test: `tests/Bukit.Content.Notion.Tests/NotionCancellationTests.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyFollowSymlinksTests.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
- Test: `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`

- [ ] **Step 1: 写 Notion canonical transport RED tests**

factory 与 injected paths 都覆盖：redirect 到非 canonical host、redirect 到 loopback/private endpoint、URI host 与 Host header 不一致、相对 URI/自定义 BaseAddress 逃逸。所有请求必须在发送敏感 header/body 前拒绝；合法 Notion API 请求保持成功。

- [ ] **Step 2: 写可控的 TOCTOU RED tests**

通过 injectable `ISafeSourceFileOpener` 在 enumeration validation 后把路径替换为 symlink，断言复制拒绝且目标不创建。平台 integration test 创建真实 symlink/reparse point，断言 opener 的 no-follow 行为；不支持创建 symlink 的环境必须显式 skip，不可当 PASS。

- [ ] **Step 3: RED**

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "FullyQualifiedName~DirectoryCopy"
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 4: 实施 canonical request gate**

将 scheme/host/port/explicit Host header policy 放在唯一 canonical send boundary；Notion API 对所有 3xx 直接失败，不跟随 redirect。实施 AD-4：默认/factory handler 一律关闭自动 redirect；任意 public injected `HttpClient` overload 保留签名但 fail closed；新增安全 handler overload。`NotionClientOptions.HttpHandlerFactory` 返回的 handler 也必须经过相同构造规则。credential 只在 target validation 通过后添加。

- [ ] **Step 5: 实施 AD-2**

`DirectoryCopy` 只从 `VerifiedSourceFile.Stream` 读取，不再按已验证 pathname 二次 `File.OpenRead`。所有 `SafeHandle` ownership 与异常路径由 `using/await using` 固定。P/Invoke 使用静态签名与常量，禁止 runtime-generated marshalling；对不支持的平台 fail closed。

- [ ] **Step 6: GREEN、AOT、专项复审**

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
aot_dir="$(mktemp -d /tmp/bukit-aot-safe-copy.XXXXXX)"
dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj -c Release -r osx-arm64 --self-contained true -p:PublishAot=true -o "$aot_dir/publish"
"$aot_dir/publish/bukit" --help
```

Expected: all PASS；AOT binary 启动成功；review 明确检查每一 Notion construction path 与每个 OS handle path。

- [ ] **Step 7: 提交**

```bash
git add src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs src/Bukit-Core/Bukit.Notion/Transport/NotionRequestSemantics.cs src/Bukit-Core/Bukit.Notion/Transport/NotionClientOptions.cs src/Bukit-Core/Bukit.Content.Notion/NotionContentClient.cs src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs src/Bukit-Core/Bukit.Cli/Commands/DoctorNotionChecker.cs src/Bukit-Core/Bukit.Engine/DirectoryCopy.cs src/Bukit-Core/Bukit.Engine/IO/ISafeSourceFileOpener.cs src/Bukit-Core/Bukit.Engine/IO/VerifiedSourceFile.cs src/Bukit-Core/Bukit.Engine/IO/PlatformSafeSourceFileOpener.cs tests/Bukit.Notion.Tests/NotionClientTests.cs tests/Bukit.Content.Notion.Tests/NotionCancellationTests.cs tests/Bukit.Engine.Tests/DirectoryCopyFollowSymlinksTests.cs tests/Bukit.Engine.Tests/DirectoryCopyTests.cs tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs
git commit -m "fix(security): enforce canonical transport and safe file handles"
```

## Task 12: Delta-Only Unified Review and Native AOT Acceptance

**Closes:** 跨任务交叉风险、全部 evidence 汇总；不新增产品范围。

**Files:**

- Review: 从 Task 1 基线到 Task 11 HEAD 的 changed files
- Create outside repository: `/tmp/codex-reports/bukit-core-four-phase-closure-final.md`
- No production edits unless unified review returns Critical/Important within the locked ledger

- [ ] **Step 1: 确认 finding ledger 无遗漏**

先取得最终任务写者槽，再逐项记录 `I-01..I-11`、`C-01..C-06`、`M-01..M-07`、`H-01` 的 test、commit、GREEN evidence。任何无证据项视为 open，不得以“代码已改”代替。

```bash
mkdir -p /tmp/codex-reports
python3 scripts/checks/codex-workflow.py queue acquire --state /tmp/bukit-core-audit-closure-queue.json --task task-12-final-review
```

- [ ] **Step 2: 生成 review-scope**

使用 Task 1-11 的 specialty evidence、逐路径 changed list 和 finding records：

```bash
review_scope=(python3 scripts/checks/codex-workflow.py review-scope --findings /tmp/codex-reports/bukit-core-four-phase-findings.json)
for evidence in /tmp/codex-reports/bukit-core-task-{01..11}-evidence.json; do review_scope+=(--evidence "$evidence"); done
while IFS= read -r changed_path; do review_scope+=(--changed "$changed_path"); done < /tmp/codex-reports/bukit-core-four-phase-changed.txt
"${review_scope[@]}"
```

Expected: scope 只包含 cross-task intersections、invalidated evidence、uncovered changed files、public/serialized contracts 与 open Critical/Important；不重复历史审计。

- [ ] **Step 3: 转入 testing 并运行最终列明专项测试**

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-12-final-review --to testing
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Expected: 9 个项目全部 PASS。若 closure 在执行时新增了直接 consumer 项目，只能追加该 consumer 的完整项目测试并更新 evidence；不得用 whole-solution 替代。

- [ ] **Step 4: 通过 Native AOT 最终硬门**

```bash
aot_dir="$(mktemp -d /tmp/bukit-aot-audit-closure.XXXXXX)"
dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj -c Release -r osx-arm64 --self-contained true -p:PublishAot=true -o "$aot_dir/publish"
"$aot_dir/publish/bukit" version
"$aot_dir/publish/bukit" --help
```

Expected: publish exit 0、无 AOT blocker、两个 smoke command exit 0。仅“编译成功”而 binary 无法启动不算通过。

- [ ] **Step 5: 转入 review_wait 并运行唯一统一复审**

```bash
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-12-final-review --to review_wait
```

重点交叉点：process lifecycle 三实现一致性；Plugin config source-gen/AOT；BodyCache/render metrics；image output ownership + validation；Notion/media transport；native file handles；serialized/config compatibility。将完整证据写入 `/tmp/codex-reports/bukit-core-four-phase-closure-final.md`。

- [ ] **Step 6: 只处理阻断级复审发现**

若有 Critical/Important，从 `review_wait` 转回 `writing`，只在唯一 owning task 的文件范围补 RED/修复，再依次经过 `testing` 与 scoped `review_wait`。Minor 记录为 residual，不扩大本计划。无 Critical/Important 后继续。

- [ ] **Step 7: 记录 metrics 与完成态**

```bash
python3 scripts/checks/codex-workflow.py metrics report --state /tmp/bukit-core-audit-closure-metrics.json
python3 scripts/checks/codex-workflow.py queue transition --state /tmp/bukit-core-audit-closure-queue.json --task task-12-final-review --to done
```

最终报告必须分别给出：实现状态、9 个专项项目状态、AOT publish 状态、AOT smoke 状态、统一复审 Critical/Important/Minor 数量、未验证环境条件。任何一项缺证据，整体结论只能是 `partial` 或 `blocked`。

## Execution Stop Conditions

执行者遇到以下任一条件必须停止当前任务并报告，不得自动扩大范围：

- 需要改变 public API 的既有必填参数、manifest schema 或现有用户配置默认语义。
- 安全闭合必须依赖本计划未批准的第三方包；唯一已批准新增依赖是现有中央 pin 的 ImageSharp。
- 需要运行 full/release gate、whole-solution tests、真实部署、网络发布、push 或修改 CI/release logic。
- 平台缺少实现 AD-2 所需的安全 no-follow/handle identity primitive，且只能退回 pathname recheck。
- 任一测试只能通过放宽 fail-closed、安全预算、取消或原子性语义。
- 工作树出现与本计划重叠的用户修改，无法在不覆盖的前提下继续。

## 审定结论

**状态：APPROVED FOR EXECUTION（技术审定通过）**

**审定日期：** 2026-08-03

**审定基线：** `main@122c90775c1900cd95256bbe37ad94dd9c178a06`

**批准范围：** 24 项计数 finding + H-01；四项最小架构决定 AD-1、AD-2、AD-3、AD-4；明确列出的兼容性收紧；12 个串行任务；所列专项测试与 Native AOT publish/smoke。

**未授权事项：** 相邻重构、全量/发布 gate、whole-solution tests、push、部署、包发布、本地 merge。

审定理由：每项 finding 均有唯一 owner、有效 RED 设计、最小生产变更、完整 direct-consumer 验证和终局证据；四项架构扩展分别是资源限制可达性、TOCTOU 原子闭合、真实图片解码和不可约束 injected transport 的 fail-closed 边界所必需。兼容性变化已经逐项显式审定，不允许执行者再自行扩大。计划以 AOT 发布和 binary smoke 为硬门，不允许普通 JIT 测试替代。

## Plan Review Record

- Finding coverage：25 个 ledger rows，覆盖 11 Important、6 Conditional Important、7 Minor、1 Hardening；每项都有唯一 owning task。
- Execution shape：12 个 task、66 个可追踪 checkbox；单写者 queue 状态与 specialty/final review 次序已固定。
- Placeholder review：占位符、动态“执行时再找文件”和未定测试路径扫描均无命中。
- Command review：纠正并核对 `closure` repeated `--changed`、`classify`、queue transition、`review-scope` repeated evidence/changed、metrics 与 AOT 命令合同。
- Path review：所有 `Modify`/`Test` 路径在审定基线存在；6 个仓库内计划新增路径明确标记为 `Create`。
- Formatting review：Markdown code fences 成对，untracked-file diff whitespace check 无错误。
- Verification boundary：本轮仅审定计划，未执行生产修改、专项测试或 AOT publish；这些证据必须由执行阶段产生。
