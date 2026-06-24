Bukit Core 插件机制 Codex 执行计划书

适用场景：交给 Codex / AI Coding Agent 执行
当前文档目录：docs/plugins
核心目标：先建立 Bukit Core 插件机制基础设施，再迁移 Import / Clone 等正式插件
技术原则：C# 规范、面向对象、面向接口、高内聚、低耦合、敏捷小步提交、Native AOT 友好

0. 执行前强制要求：先阅读并深度理解插件文档

Codex 在写任何代码前，必须先完整阅读并理解 docs/plugins 下的全部插件化文档。

0.1 必须阅读的文档清单
docs/plugins/Bukit 插件化需求分析说明书.md
docs/plugins/Bukit Core 插件机制设计文档.md
docs/plugins/Bukit 插件协议 v1 规范.md
docs/plugins/Bukit 插件配置规范.md
docs/plugins/Bukit Labs → Plugin → Core 发布准入规范.md
docs/plugins/Bukit 插件目录结构 ADR.md
docs/plugins/Bukit 插件安全模型 ADR.md
docs/plugins/Bukit Import 插件迁移计划.md
docs/plugins/Bukit Clone 插件迁移计划.md
0.2 阅读后必须输出理解摘要

在执行代码改动前，Codex 必须先生成一份内部执行摘要：

docs/plugins/Codex 插件机制执行理解摘要.md

摘要必须包含：

1. Core / Plugin / Labs 的边界理解
2. .bukit/ 与 plugins/ 的目录边界
3. bukit-plugin-v1 协议核心要求
4. .bukit/plugins.yaml 与 plugins/<id>/plugin.yaml 的职责区别
5. Core PluginHost 的职责
6. 外部进程插件安全要求
7. Import 插件迁移前置条件
8. Clone 插件迁移前置条件
9. 当前阶段明确不做的事项
10. 执行风险与防误操作清单
0.3 绝对禁止跳过阅读阶段

Codex 不得在未阅读上述文档前执行以下操作：

不得创建 PluginHost
不得迁移 Import
不得迁移 Clone
不得移动 Labs
不得修改 Core CLI 注册逻辑
不得新增外部进程调用器
不得新增插件项目
1. 总体执行路线

本计划采用敏捷小步执行策略。

整体顺序如下：

Phase 0：阅读并理解 docs/plugins 下全部文档
Phase 1：代码库现状审计
Phase 2：目录结构与 solution 准备
Phase 3：新增 Bukit.Plugin.Abstractions
Phase 4：新增 Bukit.PluginHost 配置加载与路径校验
Phase 5：实现外部进程调用器
Phase 6：实现 bukit-plugin-v1 协议客户端
Phase 7：新增 Echo 测试插件
Phase 8：Core CLI 接入插件命令
Phase 9：插件 lock、执行报告、安全门禁补齐
Phase 10：Import Plugin 迁移准备
Phase 11：Clone Plugin 迁移准备

必须遵循：

先文档理解
再底座
再 Echo 验证
再 Core CLI 接入
再 Import
最后 Clone
2. 总体技术规范
2.1 C# 语言规范

所有新增 C# 代码必须满足：

1. 启用 nullable。
2. 使用 file-scoped namespace。
3. 优先使用 sealed class。
4. DTO 优先使用 sealed record。
5. 集合类型优先使用 IReadOnlyList / IReadOnlyDictionary。
6. public API 命名必须清晰、稳定、语义明确。
7. 不使用 dynamic。
8. 不使用反射加载插件。
9. 不使用 Assembly.LoadFrom。
10. 不在 Core 中加载第三方插件 DLL。
11. 不使用 shell 拼接命令。
12. 不直接 Console.WriteLine 输出业务日志。
13. 不吞异常。
14. 不散落魔法字符串。
15. 协议常量集中定义。
16. 路径校验集中定义。
17. 错误码集中定义。
18. 保持 Native AOT 友好。
2.2 面向对象与接口规范

必须遵循：

1. 单一职责原则。
2. 面向接口编程。
3. 依赖倒置。
4. 高内聚、低耦合。
5. 组合优于继承。
6. 构造函数注入依赖。
7. IO、文件系统、进程、时间、hash 计算必须通过接口隔离。
8. 可测试逻辑不得直接绑定真实文件系统或真实进程。

推荐核心接口：

public interface IPluginConfigLoader { }
public interface IPluginManifestLoader { }
public interface IPluginPathValidator { }
public interface IPluginPlatformResolver { }
public interface IPluginHashVerifier { }
public interface IPluginPermissionEvaluator { }
public interface IPluginProcessInvoker { }
public interface IPluginProtocolClient { }
public interface IPluginCommandDescriptorFactory { }
public interface IPluginExecutionReporter { }
public interface IPluginLockFileWriter { }
public interface IClock { }
public interface IFileSystem { }
public interface IProcessRunner { }
2.3 架构依赖规范

允许：

Bukit.Cli
  -> Bukit.Cli.Shared
  -> Bukit.PluginHost
  -> Bukit.Plugin.Abstractions
  -> Bukit.Config
  -> Bukit.Engine
  -> Bukit.Shared

Bukit.PluginHost
  -> Bukit.Plugin.Abstractions
  -> Bukit.Cli.Shared
  -> Bukit.Shared

Bukit.Plugin.Import
  -> Bukit.Plugin.Abstractions
  -> Bukit.Importing
  -> Bukit.Shared

Bukit.Plugin.Clone
  -> Bukit.Plugin.Abstractions
  -> Bukit.Clone
  -> Bukit.Shared

禁止：

Bukit.Cli -> Bukit.Plugin.Import
Bukit.Cli -> Bukit.Plugin.Clone
Bukit.PluginHost -> Bukit.Plugin.Import
Bukit.PluginHost -> Bukit.Plugin.Clone
Bukit.Engine -> Bukit.Plugin.Import
Bukit.Engine -> Bukit.Plugin.Clone
Plugin -> Labs
Core -> Labs
Labs -> Core 发布路径绕过 Plugin
3. Phase 0：阅读并理解插件文档
3.1 目标

确保 Codex 在写代码之前完整理解现有插件化设计文档，防止实现偏离架构边界。

3.2 小任务
Task 0.1：确认文档存在

检查以下文件是否存在：

docs/plugins/Bukit 插件化需求分析说明书.md
docs/plugins/Bukit Core 插件机制设计文档.md
docs/plugins/Bukit 插件协议 v1 规范.md
docs/plugins/Bukit 插件配置规范.md
docs/plugins/Bukit Labs → Plugin → Core 发布准入规范.md
docs/plugins/Bukit 插件目录结构 ADR.md
docs/plugins/Bukit 插件安全模型 ADR.md
docs/plugins/Bukit Import 插件迁移计划.md
docs/plugins/Bukit Clone 插件迁移计划.md

若缺失，停止执行并报告。

Task 0.2：逐份阅读并提取关键约束

每份文档需提取：

1. 核心结论
2. 目录要求
3. 配置要求
4. 协议要求
5. 安全要求
6. 禁止事项
7. 对当前阶段的影响
Task 0.3：生成执行理解摘要

创建：

docs/plugins/Codex 插件机制执行理解摘要.md

内容必须包含：

Core / Plugin / Labs 边界
.bukit 与 plugins 边界
Core 内置插件与外部进程插件区别
bukit-plugin-v1 三个基础操作
PluginHost 职责
当前阶段不迁移 Import / Clone 的原因
后续执行顺序
Task 0.4：生成防误操作清单

在理解摘要中加入：

不得把插件程序放入 .bukit/
不得恢复 site.externalPlugins
不得恢复动态 DLL 插件
不得让 Core 引用插件实现
不得让 Plugin 依赖 Labs
不得跳过 Echo 插件闭环
不得一次性迁移 Import 和 Clone
3.3 Done Criteria
[ ] 所有 docs/plugins 文档已读取。
[ ] Codex 插件机制执行理解摘要.md 已生成。
[ ] 摘要中明确 .bukit 不能放插件程序。
[ ] 摘要中明确插件程序必须放 plugins/<id>/。
[ ] 摘要中明确第一阶段先做 PluginHost，不迁移业务插件。
[ ] 没有修改功能代码。
3.4 Codex Prompt
请先阅读并深度理解 docs/plugins 下的所有插件化文档。

必须阅读：
- Bukit 插件化需求分析说明书.md
- Bukit Core 插件机制设计文档.md
- Bukit 插件协议 v1 规范.md
- Bukit 插件配置规范.md
- Bukit Labs → Plugin → Core 发布准入规范.md
- Bukit 插件目录结构 ADR.md
- Bukit 插件安全模型 ADR.md
- Bukit Import 插件迁移计划.md
- Bukit Clone 插件迁移计划.md

阅读后创建：
docs/plugins/Codex 插件机制执行理解摘要.md

摘要必须说明：
1. Core / Plugin / Labs 的边界
2. .bukit/ 与 plugins/ 的边界
3. bukit-plugin-v1 协议要求
4. Core PluginHost 的职责
5. 当前阶段不得迁移 import / clone
6. 禁止事项清单

本阶段不得修改任何功能代码。
4. Phase 1：代码库现状审计
4.1 目标

在正式修改代码前，审计当前项目结构、依赖关系和测试情况。

4.2 小任务
Task 1.1：审计 solution

检查：

bukit.slnx
是否存在 bukit.plugins.slnx
是否存在 bukit.labs.slnx
是否存在 bukit.all.slnx

记录当前项目清单。

Task 1.2：审计 Core 项目

检查：

src/Bukit.Cli
src/Bukit.Cli.Shared
src/Bukit.Config
src/Bukit.Content
src/Bukit.Engine
src/Bukit.Engine.Abstractions
src/Bukit.Rendering
src/Bukit.Routing
src/Bukit.Shared
src/Bukit.Theme

记录引用关系。

Task 1.3：审计 Labs / experimental

检查是否存在：

experimental/Bukit.Labs.Cli
labs/Bukit.Labs.Cli

记录 Import / Clone 当前代码位置。

Task 1.4：审计 tests

记录当前测试项目。

确认是否已有：

Bukit.PluginHost.Tests
Bukit.Plugin.Abstractions.Tests
Bukit.Plugin.Import.Tests
Bukit.Plugin.Clone.Tests
Task 1.5：生成审计报告

创建：

docs/plugins/Codex 当前代码结构审计报告.md

必须包含：

当前 solution 项目清单
当前 Core 项目依赖
当前 Labs 项目位置
当前 Import 位置
当前 Clone 位置
当前缺失项目
建议新增项目
4.3 Done Criteria
[ ] 审计报告已生成。
[ ] 没有修改功能代码。
[ ] 没有移动目录。
[ ] 没有新增 PluginHost。
[ ] build 仍通过。
4.4 Codex Prompt
任务：审计当前 Bukit 代码结构，为插件机制实施做准备。

要求：
1. 检查 bukit.slnx 当前项目。
2. 检查 src/ 下 Core 项目。
3. 检查 experimental/ 或 labs/ 下 Labs 项目。
4. 检查 tests/ 下测试项目。
5. 生成 docs/plugins/Codex 当前代码结构审计报告.md。
6. 不修改任何功能代码。
7. 不移动目录。
8. 不新增 PluginHost。
9. 执行 dotnet build bukit.slnx -c Release。
5. Phase 2：目录结构准备
5.1 目标

创建正式插件体系所需基础目录，但不迁移业务功能。

5.2 小任务
Task 2.1：创建 plugins/
plugins/

用途：官方正式插件源码目录。

如果无内容，添加：

plugins/.gitkeep
Task 2.2：创建 labs/
labs/

用途：未成熟功能孵化目录。

如果无内容，添加：

labs/.gitkeep
Task 2.3：创建 schemas/
schemas/

用途：插件配置与协议 JSON Schema。

如果无内容，添加：

schemas/.gitkeep
Task 2.4：确认不创建仓库级 .bukit/

.bukit/ 是用户项目运行目录，不应作为源码仓库目录创建。

5.3 Done Criteria
[ ] plugins/ 存在。
[ ] labs/ 存在。
[ ] schemas/ 存在。
[ ] 未移动 experimental。
[ ] 未迁移 Import。
[ ] 未迁移 Clone。
[ ] 未创建仓库级 .bukit/。
[ ] build 通过。
5.4 Codex Prompt
任务：为 Bukit 插件机制准备基础目录结构。

要求：
1. 新增 plugins/、labs/、schemas/。
2. 空目录使用 .gitkeep。
3. 不创建仓库级 .bukit/。
4. 不移动 experimental。
5. 不迁移 Import / Clone。
6. 不修改 Core CLI。
7. 执行 dotnet build bukit.slnx -c Release。
6. Phase 3：新增 Bukit.Plugin.Abstractions
6.1 目标

创建插件协议抽象项目，只定义 DTO、协议常量、权限模型和结果模型。

不做任何 IO 或进程执行。

6.2 小任务
Task 3.1：创建项目
src/Bukit.Plugin.Abstractions/
src/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj

项目要求：

<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
Task 3.2：定义协议常量

创建：

Protocol/PluginProtocolConstants.cs

包含：

public static class PluginProtocolConstants
{
    public const string ProtocolVersion = "bukit-plugin-v1";
    public const string Handshake = "handshake";
    public const string Manifest = "manifest";
    public const string Invoke = "invoke";
}
Task 3.3：定义 Request / Response Envelope

创建：

Protocol/PluginRequestEnvelope.cs
Protocol/PluginResponseEnvelope.cs

要求：

sealed record
不可变
requestId 必填
protocol 必填
type 必填
Task 3.4：定义 handshake DTO
Protocol/PluginHandshakeRequest.cs
Protocol/PluginHandshakeResponse.cs
Task 3.5：定义 manifest DTO
Manifest/PluginManifest.cs
Manifest/PluginPlatformEntry.cs
Manifest/PluginCommandSpec.cs
Manifest/PluginOptionSpec.cs
Manifest/PluginArgumentSpec.cs
Protocol/PluginManifestRequest.cs
Protocol/PluginManifestResponse.cs
Task 3.6：定义 invoke DTO
Protocol/PluginInvokeRequest.cs
Protocol/PluginInvokeResponse.cs
Runtime/PluginInvokeCommand.cs
Runtime/PluginInvokeContext.cs
Task 3.7：定义权限模型
Security/PluginPermissionSet.cs
Security/PluginFileSystemPermission.cs
Security/PluginEnvironmentPermission.cs
Task 3.8：定义结果模型
Results/PluginMessage.cs
Results/PluginDiagnostic.cs
Results/PluginArtifact.cs
Results/PluginError.cs
Task 3.9：新增测试项目
tests/Bukit.Plugin.Abstractions.Tests/

测试：

DTO 可 JSON 序列化
常量值正确
默认集合不为 null 或明确允许 null
record equality 行为符合预期
Task 3.10：加入 solution

将项目加入：

bukit.slnx
6.3 Done Criteria
[ ] Bukit.Plugin.Abstractions 创建。
[ ] DTO 全部为 sealed record 或 static class。
[ ] 没有 IO。
[ ] 没有进程执行。
[ ] 没有依赖 PluginHost。
[ ] 测试通过。
[ ] build 通过。
6.4 Codex Prompt
任务：新增 src/Bukit.Plugin.Abstractions 项目。

要求：
1. 只定义插件协议 DTO、manifest DTO、config DTO、permission DTO、result DTO 和 constants。
2. 使用 sealed record。
3. 使用 nullable enable。
4. 集合使用 IReadOnlyList / IReadOnlyDictionary。
5. 不实现 IO。
6. 不实现 YAML 读取。
7. 不实现进程执行。
8. 不引用 Bukit.PluginHost。
9. 新增 tests/Bukit.Plugin.Abstractions.Tests。
10. 加入 bukit.slnx。
11. 执行 dotnet build 和 dotnet test。
7. Phase 4：新增 Bukit.PluginHost 配置与路径校验
7.1 目标

实现插件配置加载、manifest 加载、路径安全校验、平台解析、sha256 校验。

本阶段不执行插件进程。

7.2 小任务
Task 4.1：创建 PluginHost 项目
src/Bukit.PluginHost/

引用：

Bukit.Plugin.Abstractions
Bukit.Shared
Task 4.2：定义加载接口
public interface IPluginConfigLoader
{
    Task<PluginHostConfig> LoadAsync(string projectRoot, CancellationToken cancellationToken);
}

public interface IPluginManifestLoader
{
    Task<PluginManifest> LoadAsync(string pluginSourceDirectory, CancellationToken cancellationToken);
}
Task 4.3：实现 .bukit/plugins.yaml Loader

行为：

如果 .bukit/plugins.yaml 不存在，返回空插件集。
如果 YAML 无效，返回配置错误。
不创建 .bukit 目录。
不执行插件。
Task 4.4：实现 plugins/<id>/plugin.yaml Loader

行为：

读取 plugin.yaml
解析 manifest
校验 id/name/version/protocol/kind/platforms
Task 4.5：实现路径校验接口
public interface IPluginPathValidator
{
    PluginPathValidationResult ValidateSource(string projectRoot, string source);

    PluginPathValidationResult ValidateEntry(
        string projectRoot,
        string pluginSourceDirectory,
        string entry);
}
Task 4.6：实现 source 校验

必须允许：

plugins/import
plugins/clone

必须拒绝：

.bukit/plugins/import
../plugins/import
/tmp/plugin
plugins/../evil
/absolute/path
C:\tools\plugin
Task 4.7：实现 entry 校验

必须允许：

bin/osx-arm64/bukit-plugin-import
bin/win-x64/bukit-plugin-import.exe

必须拒绝：

../../evil
.bukit/bin/plugin
/usr/local/bin/plugin
C:\tools\plugin.exe
Task 4.8：实现平台解析

接口：

public interface IPluginPlatformResolver
{
    string GetCurrentPlatformId();
}

支持：

win-x64
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
Task 4.9：实现 sha256 校验

接口：

public interface IPluginHashVerifier
{
    Task<PluginHashVerificationResult> VerifySha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken);
}
Task 4.10：新增测试
tests/Bukit.PluginHost.Tests/

测试类：

PluginConfigLoaderTests
PluginManifestLoaderTests
PluginPathValidatorTests
PluginPlatformResolverTests
PluginHashVerifierTests
7.3 Done Criteria
[ ] Bukit.PluginHost 创建。
[ ] 可加载 plugins.yaml。
[ ] plugins.yaml 缺失时安全返回空配置。
[ ] 可加载 plugin.yaml。
[ ] source 只允许 plugins/<id>。
[ ] entry 只允许 plugin source 内路径。
[ ] .bukit 内 source/entry 被拒绝。
[ ] 绝对路径被拒绝。
[ ] 路径穿越被拒绝。
[ ] 平台 ID 可解析。
[ ] sha256 可校验。
[ ] 不执行插件进程。
[ ] 测试通过。
8. Phase 5：实现外部进程调用器
8.1 目标

实现通用外部进程调用能力，满足安全规则。

8.2 小任务
Task 5.1：定义进程接口
public interface IPluginProcessInvoker
{
    Task<PluginProcessResult> InvokeAsync(
        PluginProcessRequest request,
        CancellationToken cancellationToken);
}
Task 5.2：定义低层进程抽象
public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken);
}
Task 5.3：实现 SystemProcessRunner

要求：

UseShellExecute = false
RedirectStandardInput = true
RedirectStandardOutput = true
RedirectStandardError = true
不使用 shell
不拼接 shell 字符串
Task 5.4：支持 stdin JSON

Core 将 request JSON 写入 stdin。

Task 5.5：捕获 stdout / stderr

要求：

stdout 用于 JSON response
stderr 用于日志
Task 5.6：实现 timeout

默认：

handshake: 5000ms
manifest: 5000ms
invoke: 120000ms
Task 5.7：实现输出大小限制

默认：

stdoutMaxBytes: 4MB
stderrMaxBytes: 4MB
responseMaxBytes: 4MB
Task 5.8：测试

覆盖：

正常执行
非零退出
stderr 捕获
stdout 过大
stderr 过大
timeout
取消
8.3 Done Criteria
[ ] 进程调用器存在。
[ ] 不使用 shell。
[ ] stdin 可写。
[ ] stdout 可读。
[ ] stderr 可读。
[ ] timeout 生效。
[ ] output limit 生效。
[ ] 测试通过。
9. Phase 6：实现 bukit-plugin-v1 协议客户端
9.1 目标

基于外部进程调用器实现：

handshake
manifest
invoke
9.2 小任务
Task 6.1：定义协议客户端接口
public interface IPluginProtocolClient
{
    Task<PluginHandshakeResponse> HandshakeAsync(
        ResolvedPlugin plugin,
        CancellationToken cancellationToken);

    Task<PluginManifestResponse> GetManifestAsync(
        ResolvedPlugin plugin,
        CancellationToken cancellationToken);

    Task<PluginInvokeResponse> InvokeAsync(
        ResolvedPlugin plugin,
        PluginInvokeRequest request,
        CancellationToken cancellationToken);
}
Task 6.2：实现 requestId 生成

通过接口隔离：

public interface IPluginRequestIdFactory
{
    string Create();
}
Task 6.3：实现 handshake

校验：

protocol
requestId
plugin.id
plugin.version
plugin.platform
success
Task 6.4：实现 manifest

校验：

protocol
requestId
commands
requiredPermissions
success
Task 6.5：实现 invoke

校验：

protocol
requestId
exitCode
messages
diagnostics
artifacts
artifact path
Task 6.6：统一错误码

创建：

PluginHostErrorCodes.cs

包含：

plugin.unsupportedProtocol
plugin.invalidResponse
plugin.timeout
plugin.executionFailed
plugin.permissionDenied
plugin.outputTooLarge
Task 6.7：测试

覆盖：

handshake success
handshake invalid protocol
handshake mismatched requestId
handshake mismatched plugin id
manifest success
manifest invalid JSON
invoke success
invoke non-zero exit
invoke artifact path traversal
9.3 Done Criteria
[ ] handshake 可执行。
[ ] manifest 可执行。
[ ] invoke 可执行。
[ ] JSON response 严格校验。
[ ] requestId 校验。
[ ] protocol 校验。
[ ] 错误码稳定。
[ ] 测试通过。
10. Phase 7：新增 Echo 测试插件
10.1 目标

用最小插件验证 PluginHost 全链路。

10.2 小任务
Task 7.1：新增项目
plugins/Bukit.Plugin.Echo/
Task 7.2：实现 Echo Program

要求：

读取 stdin JSON
按 type 分发
stdout 输出 JSON
stderr 输出日志
Task 7.3：实现 handshake

返回：

id: echo
name: Bukit Echo Plugin
version: 1.0.0
capabilities: cli-command
Task 7.4：实现 manifest

暴露命令：

echo
Task 7.5：实现 invoke

返回收到的：

arguments
options
context
Task 7.6：创建测试 fixture

生成：

plugins/echo/plugin.yaml
plugins/echo/bin/<rid>/bukit-plugin-echo

用于 PluginHost 集成测试。

10.3 Done Criteria
[ ] Echo 插件存在。
[ ] Echo 支持 handshake。
[ ] Echo 支持 manifest。
[ ] Echo 支持 invoke。
[ ] stdout 只输出 JSON。
[ ] stderr 可输出日志。
[ ] PluginHost 可加载 Echo。
[ ] PluginHost 可调用 Echo。
11. Phase 8：Core CLI 接入插件命令
11.1 目标

Core CLI 支持：

Core commands + Enabled plugin commands
11.2 小任务
Task 8.1：新增 BukitCliComposer

职责：

合并 Core descriptors 与 Plugin descriptors
Task 8.2：新增 PluginCommandDescriptorFactory

将 plugin manifest command 转换为 CommandDescriptor。

Task 8.3：新增 PluginCommandInvoker

将 CLI 调用转换为 PluginInvokeRequest。

Task 8.4：实现命令冲突检测

规则：

Core command 优先
插件不得覆盖 Core command
插件之间不得冲突
alias 不得冲突
Task 8.5：实现 disabled command

禁用命令提示：

Command disabled by plugin config: <command>
Task 8.6：新增 bukit plugin list

输出：

Plugins:
  echo@1.0.0 enabled=true platform=osx-arm64 commands=echo
Task 8.7：测试

覆盖：

Core command 仍可用
Echo command 注册
Echo command invoke
disabled command
command conflict
plugin list
11.3 Done Criteria
[ ] Core CLI 可加载插件命令。
[ ] Core command 不被覆盖。
[ ] 插件冲突可检测。
[ ] disabled command 正确。
[ ] plugin list 可用。
[ ] Echo 端到端通过。
[ ] Core AOT 不破坏。
12. Phase 9：Lock、报告、安全门禁
12.1 目标

补齐插件执行审计与可重复解析能力。

12.2 小任务
Task 9.1：实现 PluginLockFileWriter

写入：

.bukit/plugins.lock.yaml
Task 9.2：实现 PluginExecutionReporter

写入：

.bukit/reports/plugin-executions/*.json
Task 9.3：实现 Secret Masking

打码：

NOTION_TOKEN
API_KEY
PASSWORD
TOKEN
SECRET
Task 9.4：实现 CI 策略

CI 运行外部插件必须满足：

allowInCi=true
sha256 present
sha256 verified
permissions explicit
Task 9.5：测试

覆盖：

lock 写入
report 写入
secret 打码
allowInCi=false 拒绝
allowInCi=true 允许
12.3 Done Criteria
[ ] plugins.lock.yaml 可生成。
[ ] execution report 可生成。
[ ] secret 不落盘。
[ ] CI 策略生效。
[ ] 测试通过。
13. Phase 10：Import Plugin 迁移准备
13.1 目标

只做 Import 插件骨架，不完整迁移业务逻辑。

13.2 小任务
1. 新增 plugins/Bukit.Plugin.Import。
2. 添加 Program.cs。
3. 添加 ImportPluginApp。
4. 添加 ImportPluginManifestProvider。
5. 添加 ImportPluginInvoker。
6. 先返回 NotImplemented diagnostic。
7. 不删除 Labs Import。
8. 不改变现有 bukit-labs import 行为。
9. 添加测试骨架。
13.3 Done Criteria
[ ] Import 插件项目存在。
[ ] 不改变现有 Import 行为。
[ ] 不删除 Labs Import。
[ ] 可 build。
[ ] 有迁移 TODO。
14. Phase 11：Clone Plugin 迁移准备
14.1 目标

只做 Clone 领域库骨架和依赖分析。

14.2 小任务
1. 审计 Labs Clone 类。
2. 分类 models/input/assets/generation/verification。
3. 新增 src/Bukit.Clone 空项目。
4. 新增 tests/Bukit.Clone.Tests。
5. 不迁移业务逻辑。
6. 不删除 Labs Clone。
7. 输出 Clone 类迁移清单。
14.3 Done Criteria
[ ] Bukit.Clone 项目存在。
[ ] 不改变现有 Clone 行为。
[ ] 有类迁移清单。
[ ] 有测试骨架。
15. 总体验收命令

每个阶段必须执行：

dotnet build bukit.slnx -c Release
dotnet test bukit.slnx -c Release

涉及插件 solution 后执行：

dotnet build bukit.plugins.slnx -c Release
dotnet test bukit.plugins.slnx -c Release

最终验证：

dotnet publish src/Bukit.Cli -c Release -p:PublishAot=true
16. 推荐 PR 拆分
PR-001 docs: add codex plugin execution understanding summary
PR-002 chore: add plugin directory scaffolding
PR-003 feat(plugin): add Bukit.Plugin.Abstractions
PR-004 feat(plugin): add PluginHost config and path validation
PR-005 feat(plugin): add safe external process invoker
PR-006 feat(plugin): add bukit-plugin-v1 protocol client
PR-007 test(plugin): add Echo plugin fixture
PR-008 feat(cli): compose Core and plugin commands
PR-009 feat(plugin): add plugin lock and execution reports
PR-010 chore(import): add Import plugin skeleton
PR-011 chore(clone): add Clone domain skeleton
17. Codex 总控 Prompt
你是 Bukit 项目的 Codex 执行 agent。

执行目标：
按照 docs/plugins 下的插件化设计文档，分阶段实现 Bukit Core 插件机制。

执行前必须：
1. 阅读 docs/plugins 下所有插件文档。
2. 生成 docs/plugins/Codex 插件机制执行理解摘要.md。
3. 生成 docs/plugins/Codex 当前代码结构审计报告.md。

必须遵守：
1. Core 是稳定底座和插件宿主。
2. Labs 是未成熟功能孵化区。
3. Plugin 是正式发布功能模块。
4. 除 Core 内置插件外，正式插件全部是外部进程插件。
5. 插件程序必须放在 plugins/<id>/。
6. .bukit/ 只能放配置、锁文件、报告、缓存、日志、状态。
7. .bukit/ 内禁止放可执行程序。
8. 不恢复 site.externalPlugins。
9. 不恢复动态 DLL 插件。
10. 不让 Core 引用插件实现。
11. 不让 Plugin 依赖 Labs。
12. 不使用 shell 拼接命令。
13. 保持 Native AOT 友好。
14. 所有代码必须符合 C# 规范、面向对象、面向接口、高内聚低耦合。
15. 每个阶段必须 build/test。
16. 不得一次性迁移 Import 和 Clone。

执行顺序：
Phase 0：阅读文档并生成理解摘要。
Phase 1：审计当前代码结构。
Phase 2：准备目录结构。
Phase 3：新增 Bukit.Plugin.Abstractions。
Phase 4：新增 Bukit.PluginHost 配置与路径校验。
Phase 5：实现安全外部进程调用器。
Phase 6：实现 bukit-plugin-v1 协议客户端。
Phase 7：新增 Echo 测试插件。
Phase 8：Core CLI 接入插件命令。
Phase 9：补齐 lock、报告、安全门禁。
Phase 10：准备 Import 插件骨架。
Phase 11：准备 Clone 领域库骨架。

禁止：
- 不得跳过文档阅读。
- 不得把插件程序放入 .bukit/。
- 不得直接迁移 Import。
- 不得直接迁移 Clone。
- 不得让 Bukit.Cli 引用 Bukit.Plugin.Import。
- 不得让 Bukit.Plugin.Import 引用 Labs。
- 不得用 shell 执行插件。
- 不得破坏现有 Core CLI 命令。
18. 最终结论

本执行计划的第一目标不是迁移业务功能，而是搭建可验证、可审计、可扩展的插件底座。

最重要的执行原则：

先读文档
先审计
先抽象
先安全
先 Echo
再 Core CLI
再 Import
最后 Clone

当 Echo 插件端到端跑通后，才允许进入 Import 插件迁移。

当 Import 插件稳定后，才允许进入 Clone 插件迁移。