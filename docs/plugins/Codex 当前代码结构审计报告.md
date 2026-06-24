# Codex 当前代码结构审计报告

> 生成时间：2026-06-24
> 执行阶段：Phase 1
> 审计范围：Core solution、项目引用、CLI 注册、Engine built-in plugin、Labs/Import/Clone、旧 external protocol 残留、schema/gate/test 面

## 1. 审计结论

当前仓库已经完成 Core / Labs 的第一轮边界收敛，但尚未具备新版 `bukit-plugin-v1` Core PluginHost。

已具备的基础：

- Core CLI 当前是稳定命令白名单，不包含 `import`、`clone`、`plugin` 等非 Core 命令。
- Core 配置和构建测试已经拒绝旧 `site.externalPlugins`，并确认不暴露 `--allow-external-plugins`。
- Core Engine 当前 `PluginRegistry` 只注册 built-in plugin source，没有加载旧 external protocol source。
- `src/Bukit.Importing` 已存在，可作为后续 Import 插件领域库。
- Labs 功能已位于 `experimental/Bukit.Labs.Cli`，Import/Clone 当前仍是 Labs 功能，不在 Core CLI 发布面。
- 架构测试已经覆盖 Core CLI 白名单、Core 不引用 Labs、Core CLI 不引用 Importing、PluginRegistry 不加载 external protocol source 等边界。

主要缺口：

- `src/Bukit.Plugin.Abstractions/` 不存在。
- `src/Bukit.PluginHost/` 不存在。
- `tests/Bukit.Plugin.Abstractions.Tests/` 和 `tests/Bukit.PluginHost.Tests/` 不存在。
- 顶层正式插件源码目录 `plugins/` 不存在；现有 `src/plugins/WordCountSectionPlugin` 是旧/样例式位置，不符合新 ADR 的顶层 `plugins/Bukit.Plugin.<Name>/` 结构。
- 仍没有 `bukit.plugins.slnx`、`bukit.labs.slnx`、`bukit.all.slnx`；当前只有 `bukit.slnx` 和 `bukit.experimental.slnx`。
- `docs/schemas/` 中已有 `bukit-plugin-config.v1.schema.json` 与 `bukit-plugin-manifest.v1.schema.json`；仍缺 `bukit-plugin-lock` 与 protocol request/response 相关 schema。
- Core CLI 入口仍一次性创建静态 descriptors，尚无 `Core descriptors + Plugin descriptors` 的 composer。
- `bukit plugin list` 目前在 Core 中不存在；现有架构测试明确禁止 Core 暴露旧 `plugin` 命令。

## 2. Solution 与项目结构

当前 solution：

- `bukit.slnx`：Core solution，包含 `src/Bukit.Cli`、`src/Bukit.Cli.Shared`、`src/Bukit.Config`、`src/Bukit.Content`、`src/Bukit.Engine.Abstractions`、`src/Bukit.Engine`、`src/Bukit.Rendering`、`src/Bukit.Routing`、`src/Bukit.Shared`、`src/Bukit.Theme` 和 Core 测试。
- `bukit.experimental.slnx`：Core + Labs/Importing solution，包含 `src/Bukit.Importing`、`experimental/Bukit.Labs.Cli`、`tests/Bukit.Importing.Tests` 等。

当前缺失的新插件分层 solution：

- `bukit.plugins.slnx`
- `bukit.labs.slnx`
- `bukit.all.slnx`

当前 Core 项目：

- `src/Bukit.Cli`
- `src/Bukit.Cli.Shared`
- `src/Bukit.Config`
- `src/Bukit.Content`
- `src/Bukit.Engine`
- `src/Bukit.Engine.Abstractions`
- `src/Bukit.Rendering`
- `src/Bukit.Routing`
- `src/Bukit.Shared`
- `src/Bukit.Theme`
- `src/Bukit.Importing` 只在 experimental solution 中，不在 Core solution 中。

后续 Phase 2/3/4 需要新增：

- `src/Bukit.Plugin.Abstractions`
- `src/Bukit.PluginHost`
- `tests/Bukit.Plugin.Abstractions.Tests`
- `tests/Bukit.PluginHost.Tests`

## 3. Core CLI 现状

`BukitCliSpecs.CreateRegistry()` 当前返回以下 Core stable commands：

```text
build
doctor
config
preview
dev
clean
version
completion
seo
geo
publish
deploy
```

`BukitCliDescriptors.CreateDescriptors()` 当前为上述命令绑定静态 handler。`Program.cs` 在启动时直接调用 `BukitCliDescriptors.CreateDescriptors()`，再按首个参数解析 descriptor。

这意味着 Phase 8 接入插件命令时不能直接把 `import`、`clone` 写入静态 Core registry。需要新增组合层，例如 `BukitCliComposer`：

```text
Core command descriptors
  +
Plugin command descriptors from PluginHost
```

并且必须保留以下行为：

- Core command 优先。
- 插件不得覆盖 Core command。
- disabled plugin command 不能表现为 unknown command。
- 缺少 `.bukit/plugins.yaml` 时 Core stable commands 不受影响。

当前测试也明确断言 `import`、`clone`、`plugin` 不在 Core registry 中。因此 Phase 8 添加 `plugin list` 和动态插件命令时，需要同步调整架构测试语义：禁止旧 Labs/plugin 命令泄漏，但允许新版 Core PluginHost 的 `plugin` 管理命令作为稳定 Core 命令。

## 4. Engine Built-in Plugin 现状

Core Engine 当前有 built-in in-process plugin pipeline：

- `src/Bukit.Engine/Plugins/PluginRegistry.cs`
- `src/Bukit.Engine/Plugins/PluginRunner.cs`
- `src/Bukit.Engine/Plugins/BuiltIn/*`
- `src/Bukit.Engine.Abstractions/Plugins/*`

`PluginRegistry` 当前只创建 `BuiltInPluginSource`，包含 DataFiles、PagesIndex、Taxonomy、Pagination、Archive、RelatedContent、Alias、Menu、ImageProcessing 等 built-in plugins。架构测试 `PluginRegistry_DoesNotLoadExternalProtocolSource` 已保证 Core Engine 不加载旧 external protocol source。

这部分应继续保持为 Core 内置 in-process 插件，由 `site.plugins` 控制。新版外部进程插件不能混入 Engine `PluginRegistry`，应由 `Bukit.PluginHost` 作为 CLI feature plugin host 独立承载。

## 5. Config 与旧 external plugin 状态

当前 Core config 仍支持：

- `site.plugins`：Core built-in Engine plugin toggles。
- `site.pluginFailMode`：built-in plugin failure policy，当前 schema 和 validator 中存在。

当前 Core config 已拒绝旧：

- `site.externalPlugins`
- `--allow-external-plugins`

相关测试：

- `ConfigLoaderTests.Load_ExternalPlugins_ThrowsConfigException`
- `BuildCommandTests.RunAsync_ExternalPluginsConfig_ThrowsConfigException`
- `BuildCommandTests.BuildSpec_DoesNotExposeAllowExternalPluginsFlag`

后续实现必须避免把 `.bukit/plugins.yaml` 与 `site.yaml` 混在一起。不能恢复 `site.externalPlugins`，也不能把外部进程插件入口放回 `site.yaml`。

## 6. Labs / Import / Clone 现状

Labs CLI 位于：

```text
experimental/Bukit.Labs.Cli/
```

当前 Labs 功能包括：

- `Commands/Import/`
- `Commands/Clone/`
- `Commands/Notion/`
- `Commands/Theme/`
- `Commands/Intent/`
- `Commands/Visual/`
- `Commands/Webhook/`
- `Commands/Data/`

Import 现状：

- Labs 入口在 `experimental/Bukit.Labs.Cli/Commands/Import/`。
- 稳定领域库 `src/Bukit.Importing/` 已存在。
- `experimental/Bukit.Labs.Cli` 引用 `src/Bukit.Importing`。
- `src/Bukit.Cli` 不引用 `Bukit.Importing`，已有架构测试锁定。

Clone 现状：

- Labs 入口和主要实现仍在 `experimental/Bukit.Labs.Cli/Commands/Clone/`。
- 还没有 `src/Bukit.Clone/` 领域库。
- 还没有 `plugins/Bukit.Plugin.Clone/`。

迁移约束：

- Phase 10 只能准备 Import 插件骨架，不迁移 `html-demo` 或 `seed` 业务。
- Phase 11 只能准备 Clone 领域库骨架，不迁移完整 Clone 行为。
- 不能让 `Bukit.Plugin.Import` 或未来 `Bukit.Plugin.Clone` 依赖 `experimental/Bukit.Labs.Cli`。

## 7. 旧 external protocol 残留

旧 external protocol 代码已经从 Core 移入 experimental：

```text
experimental/Bukit.Labs.Protocol/
experimental/Bukit.Labs.Protocol.Tests/
```

其中包含：

- `AbstractionsProtocol/ProcessPluginHost.cs`
- `EngineProtocol/ExternalProtocolPluginSource.cs`
- `EngineProtocol/ProcessPluginInvoker.cs`
- legacy external protocol tests
- sample plugins

这些代码仍提及 `site.externalPlugins`，但位于 experimental，不能直接搬入 Core。新版 `Bukit.PluginHost` 应按 `docs/plugins` 新协议重新实现，不应复用旧 runtime DLL/external protocol 配置模型，也不应把旧 `ExternalProtocolPluginSource` 接回 Core Engine。

## 8. 插件源码目录现状

当前存在：

```text
src/plugins/WordCountSectionPlugin/
```

以及若干 experimental sample plugins 和测试 fixture 输出目录。这些不是新版正式插件源码目录。

根据 ADR，正式官方插件源码应使用顶层：

```text
plugins/Bukit.Plugin.Import/
plugins/Bukit.Plugin.Clone/
```

用户项目运行包应使用：

```text
plugins/import/
plugins/clone/
```

后续 Phase 2 应创建顶层 `plugins/` 作为正式插件源码目录，避免继续扩展 `src/plugins/`。

## 9. Schema、lock、报告现状

当前 `docs/schemas/` 包含 build、release、seo、geo、coverage、skills 等 schema，并已补充插件配置/manifest schema；lock/protocol schema 仍待冻结后补齐。

当前缺失：

- `docs/schemas/bukit-plugin-lock.v1.schema.json`
- protocol request/response schema

当前没有：

- `.bukit/plugins.yaml` loader
- `.bukit/plugins.lock.yaml` writer
- `.bukit/reports/plugin-executions/*.json` writer
- PluginHost 安全报告模型

这些应分别落在 Phase 4、Phase 6、Phase 9。

## 10. Gate 与测试现状

主线 gate：

- `scripts/gates/ci-fast.sh`：file-size、repo hygiene、workflow pin、encoding、Core CLI contract、skills、coverage schema、release fixtures、CLI docs sync、restore、build、test、format、docs consistency、README sync。
- `scripts/gates/ci-full.sh` 与 `scripts/gates/release.sh`：用于更宽验证。
- `scripts/security/security-regression.sh`：包含 Core boundary、ExternalPlugin/ConfigException、PathTraversal、Plugin 等过滤测试。

现有架构测试对后续插件机制有价值：

- `CoreBoundaryTests.CoreCliCommands_MatchStableWhitelist`
- `CoreBoundaryTests.CoreCliAssembly_DoesNotContainExperimentalCommandTypes`
- `CoreBoundaryTests.CoreCliProject_DoesNotReferenceImporting`
- `CoreBoundaryTests.PluginRegistry_DoesNotLoadExternalProtocolSource`
- `DependencyMatrixTests.CoreCli_MustNotDependOn_LabsCli`
- `DependencyMatrixTests.LabsCli_MustNotDependOn_CoreCli`
- `CoreUserFacingTextTests.CoreUserFacingText_DoesNotLeakNonCoreCommands`

后续需要新增的 gate/test 面：

- `Bukit.Plugin.Abstractions.Tests`
- `Bukit.PluginHost.Tests`
- `.bukit/plugins.yaml` 缺失安全降级测试
- source/entry 路径安全测试
- `.bukit` executable 拒绝测试
- plugin.yaml manifest 测试
- platform resolver 测试
- sha256 测试
- permission comparison 测试
- process invoker 不使用 shell 测试
- stdout/stderr/timeout/output limit 测试
- protocol handshake/manifest/invoke 测试
- lock/report 写入测试
- command conflict / disabled command 测试

## 11. Native AOT 与进程执行现状

`src/Bukit.Cli/Bukit.Cli.csproj` 已包含：

```xml
<PublishAot>true</PublishAot>
<PublishSingleFile>true</PublishSingleFile>
```

Core 中已有若干直接 `ProcessStartInfo` 使用场景，例如 image/scss/git 等，均使用 `UseShellExecute = false`。这些不是新版插件执行器，但可作为“不经 shell 直接进程启动”的实现风格参考。

新版插件调用器必须独立封装，不复用 shell 字符串拼接，不依赖动态 assembly，不破坏 Native AOT。

## 12. Phase 2 前置建议

Phase 2 只准备目录结构时，建议最小落地：

- 创建顶层 `plugins/`。
- 创建顶层 `labs/` 占位或按计划保留 `experimental/`，并在报告中明确是否暂不物理迁移 Labs。
- 创建 `docs/schemas/` 插件 schema 占位的目标路径，或等 Phase 9 再生成 schema。
- 新增空/占位 solution 应谨慎，确保 `dotnet build/test` 不被空项目破坏。

Phase 2 不应：

- 创建 PluginHost 功能代码。
- 迁移 Import。
- 迁移 Clone。
- 移动 Labs 大量代码。
- 修改 Core CLI 注册逻辑。

## 13. 当前自审

本阶段只新增文档报告，未修改功能代码、项目文件、solution、gate 或测试。

已遵守：

- 未修改 `guide-0.1/`。
- 未修改 `scripts-0.1/`。
- 未恢复 `site.externalPlugins`。
- 未新增动态 DLL 插件。
- 未让 Core 引用插件实现。
- 未迁移 Import 或 Clone。

后续 Phase 2 开始前必须先处理执行分支/工作区问题：当前仓库在 `main` 分支，且存在用户未跟踪文件 `docs/plans/Bukit Core 插件机制 Codex 执行计划书.md`。开发阶段不应直接在 `main` 上继续实现。
