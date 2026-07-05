# Bukit Core 插件边界严格审计报告

日期：2026-06-28  
仓库：`/Users/ali/mydev/Git/Github/Bukit`  
报告类型：只读审计报告文件  
审计主题：Bukit Core 分层、正式外部插件协议边界、`Bukit.PluginHost`、`Bukit.Plugin.Abstractions`、遗留进程内扩展点和不需要继续保留的代码层。  

## 1. 用户要求逐项拆解

本报告按以下明确要求审计，不把目标缩小为普通代码风格审查。

| 要求 | 本报告对应章节 | 判定方式 |
|---|---|---|
| 对 Bukit Core 代码进行最严格、最全面审计 | 第 4、5、6、7、8、9 章 | 按项目、引用、运行入口、协议入口、测试门禁逐层核对。 |
| 除内置插件外，外部插件严禁调用 Core 代码 | 第 5、8、10 章 | 检查 `plugins/` 项目引用、`PluginHost` 引用、Engine 插件注册、旧进程内扩展点。 |
| 所有插件必须按照插件协议开发调用 | 第 3、6、8、10 章 | 以 `bukit-plugin-v1` 文档、`.bukit/plugins.yaml`、`plugin.yaml`、`PluginHost` 实现为准。 |
| 对 Core 各个分层作用进行审计 | 第 5 章 | 对 `src/` 下每个主项目给出职责、允许依赖、禁止依赖、保留/隔离判断。 |
| 对 `Bukit.PluginHost` 充分分析和说明 | 第 6 章 | 按配置、路径、manifest、hash、CI、权限、进程、协议、lock/report 拆解。 |
| 对 `Bukit.Plugin.Abstractions` 充分分析和说明 | 第 7 章 | 按 DTO、协议常量、schema context、依赖关系、SDK 边界拆解。 |
| 将所有不需要的分层或代码进行移除 | 第 10、11、12 章 | 由于本次要求生成报告文件，报告列出必须删除/隔离对象；未执行代码删除。 |
| 如需调用能力，独立开发 SDK 开放给插件，SDK 暂时不做 | 第 13 章 | 明确 `Engine.Abstractions` 不能当 SDK；未来 SDK 应独立。 |
| 不可概括，必须详细描述 | 全文 | 每个判断都给出证据、影响和处理建议。 |

## 2. 审计依据和只读命令

本次报告以当前工作树为准。审计时读取了以下权威来源：

1. 解决方案文件：`bukit.slnx`、`bukit.experimental.slnx`。
2. Core 项目文件：`src/Bukit.*/*.csproj`。
3. 正式插件项目：`plugins/Bukit.Plugin.Echo`、`plugins/Bukit.Plugin.Import`。
4. PluginHost 源码：`src/Bukit.PluginHost/*.cs`。
5. Plugin.Abstractions 源码：`src/Bukit.Plugin.Abstractions/**/*.cs`。
6. Engine 内置插件注册和执行：`src/Bukit.Engine/Plugins/PluginRegistry.cs`、`src/Bukit.Engine/Plugins/PluginRunner.cs`。
7. CLI 插件入口：`src/Bukit.Cli/Program.cs`、`src/Bukit.Cli/Cli/PluginCliLoader.cs`、`src/Bukit.Cli/Cli/BukitCliComposer.cs`。
8. 插件协议和目录规范文档：`docs/plugins/*`。
9. 架构测试：`tests/Bukit.Architecture.Tests/*`。
10. 插件主机测试：`tests/Bukit.PluginHost.Tests/*`。
11. 官方插件包检查脚本：`scripts/checks/official-plugin-packages.sh`。

只读扫描使用了 `rg`、`sed`、`nl`、`git status --short`、`wc -l`。没有执行 `dotnet build`、`dotnet test` 或质量门禁，因为这些命令会写入 `bin/obj/TestResults`，不属于只读报告生成。

## 3. 权威协议边界

### 3.1 插件协议的硬边界

`docs/plugins/Bukit 插件协议 v1 规范.md` 定义了 `bukit-plugin-v1`。该文档第 5-7 行说明协议名称、协议类型和适用对象：协议是语言无关、跨平台、外部进程 JSON 协议，适用于 Bukit Core Plugin Host 与外部进程插件。

该文档第 35-45 行列出正式插件必须满足的要求：

- 插件作为外部进程运行。
- 插件实现 `bukit-plugin-v1` JSON 协议。
- 插件提供跨平台可执行入口。
- 插件提供 `plugin.yaml` manifest。
- 插件通过 `handshake`、`manifest`、`invoke` 协议调用。
- 插件只向 stdout 输出标准 JSON 响应。
- 插件遵守 Core Plugin Host 的安全、权限、路径、超时和输出限制。

该文档第 93-119 行明确 v1 不包含动态 DLL、WASM、Docker、热加载、自动下载、远程市场、OS 级完整沙箱和正式 build hook。v1 只支持项目本地 `plugins/<id>/` 外部进程插件，并只包含 `handshake`、`manifest`、`invoke` 三类协议操作。

该文档第 127-138 行非常关键：协议不得依赖某种语言、运行时或 SDK；第三方插件不需要引用任何 Bukit 程序集，只需实现 JSON 协议即可。官方插件可以引用 `Bukit.Plugin.Abstractions` 和 `Bukit.Shared`，但这不是协议强制要求。

该文档第 167-178 行明确禁止：

- `Assembly.LoadFrom`
- 动态 DLL 加载
- in-process 第三方插件
- 运行时反射加载插件程序集

审计结论：任何外部正式插件引用 `Bukit.Engine`、`Bukit.Cli`、`Bukit.PluginHost`、`Bukit.Engine.Abstractions`，或绕过 `bukit-plugin-v1` 直接使用 Core 类型，均违反协议精神。官方 .NET 插件引用 `Bukit.Plugin.Abstractions` 可接受；第三方插件原则上连这个引用也不是必须。

### 3.2 插件目录 ADR 的硬边界

`docs/plugins/Bukit 插件目录结构 ADR.md` 第 23-43 行把架构分为三类：

- Core：稳定基础底座、插件宿主、Core 内置插件。
- Plugin：已成熟、正式发布、跨平台外部进程插件。
- Labs：未成熟功能孵化区。

同一段还要求：

- 除 Core 内置插件外，所有正式插件均采用外部进程插件。
- 外部插件通过 `bukit-plugin-v1` JSON 协议与 Core 通信。
- 插件程序不得放在 `.bukit/` 内。
- 插件程序必须放在项目根目录 `plugins/` 下。
- `.bukit/` 只用于系统配置、锁文件、报告、缓存、日志、临时文件和状态文件。

该 ADR 第 91-114 行定义 `src/` 只存放 Core 稳定底座，不应包含正式业务功能插件实现。

该 ADR 第 117-149 行定义仓库根目录 `plugins/` 存放官方正式插件源码，每个正式插件必须是独立外部进程项目，正式插件不得作为 class library 被 Core 直接引用。

该 ADR 第 383-409 行定义 `Bukit.PluginHost` 职责：读取 `.bukit/plugins.yaml`、校验插件配置、校验 source 路径、读取 `plugins/<id>/plugin.yaml`、解析平台入口、校验 sha256、执行 handshake、读取 manifest、注册 CLI command descriptor、执行 invoke、写入 lock、写入 report、处理 timeout/stdout/stderr/exit code。`Bukit.Cli` 只调用 `Bukit.PluginHost`，不直接管理插件执行细节。

该 ADR 第 413-435 行定义 `Bukit.Plugin.Abstractions` 职责：协议 DTO、manifest model、config model、permission model、invoke request/response model、plugin constants、schema context。第三方插件不强制引用它，只要实现 JSON 协议即可。

该 ADR 第 439-458 行明确禁止：

- `Bukit.Cli -> Bukit.Plugin.Import`
- `Bukit.Cli -> Bukit.Plugin.Clone`
- `Bukit.PluginHost -> Bukit.Plugin.Import`
- `Bukit.PluginHost -> Bukit.Plugin.Clone`
- `Bukit.Engine -> Bukit.Plugin.Import`
- `Bukit.Engine -> Bukit.Plugin.Clone`

正确关系是：

```text
Bukit.Cli
  -> Bukit.PluginHost
      -> external process: plugins/<id>/bin/<rid>/...
```

审计结论：当前审计应以这组文档为准，而不是以旧技能文件或旧实验代码为准。凡是 `site.externalPlugins`、动态 DLL、WASM 或进程内第三方插件的实现，都只能是历史/实验/待删除资料，不应接回 Core。

## 4. 解决方案和项目边界审计

### 4.1 `bukit.slnx` 当前组成

`bukit.slnx` 第 2-16 行包含以下 `src/` 项目：

- `src/Bukit.Cli`
- `src/Bukit.Cli.Shared`
- `src/Bukit.Clone`
- `src/Bukit.Config`
- `src/Bukit.Content`
- `src/Bukit.Engine.Abstractions`
- `src/Bukit.Engine`
- `src/Bukit.Plugin.Abstractions`
- `src/Bukit.PluginHost`
- `src/Bukit.Rendering`
- `src/Bukit.Routing`
- `src/Bukit.Shared`
- `src/Bukit.Theme`

`bukit.slnx` 第 17-20 行包含正式插件项目：

- `plugins/Bukit.Plugin.Echo`
- `plugins/Bukit.Plugin.Import`

`bukit.slnx` 第 21-37 行包含测试项目，其中包括：

- Core 测试。
- PluginHost 和 Plugin.Abstractions 测试。
- Plugin.Import 测试。
- `tests/PluginProcessProbe`。
- `tests/Bukit.Labs.Cli.Tests`。
- `tests/Bukit.Architecture.Tests`。

### 4.2 `bukit.slnx` 的问题

如果 `bukit.slnx` 被定义为“Core solution”，它现在并不纯粹，因为它包含：

1. 正式插件实现项目：`plugins/Bukit.Plugin.Echo`、`plugins/Bukit.Plugin.Import`。
2. 正式插件测试：`tests/Bukit.Plugin.Import.Tests`。
3. Labs 测试：`tests/Bukit.Labs.Cli.Tests`。
4. 架构测试项目又直接引用 `experimental/Bukit.Labs.Cli`，见 `tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj` 第 20 行。

`tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj` 第 11 行直接引用 `experimental/Bukit.Labs.Cli/Bukit.Labs.Cli.csproj`。这说明当前主 solution 会把 Labs 测试项目纳入同一层级。该问题不是“运行时代码依赖 Labs”，而是“solution/gate 边界混杂”。如果未来把 `bukit.slnx` 当作 Core-only release gate，就会把 Labs 状态混入 Core 结论。

### 4.3 解决方案拆分建议

建议形成四个边界清晰的 solution 或脚本入口：

1. `bukit.core.slnx`：只包含 Core runtime 项目、Core tests、architecture tests 中不需要引用 Labs 的部分。
2. `bukit.plugins.slnx`：包含 `Bukit.PluginHost`、`Bukit.Plugin.Abstractions`、正式插件项目、插件测试和进程 probe。
3. `bukit.labs.slnx`：包含 Labs/experimental 项目和 Labs tests。
4. `bukit.all.slnx`：用于本地或 nightly 全量检查，不用于证明 Core-only 边界。

若继续保留 `bukit.slnx` 作为全量 solution，也应在文档和脚本中明确其不是 Core-only gate。

## 5. Core 分层职责和保留判断

### 5.1 `Bukit.Shared`

职责：最低层共享基础设施，包括异常、日志、诊断码、路径工具、URL 安全、SSRF guard、slug、平台路径、Notion HTML/token 工具等。

依赖判断：`Bukit.Shared` 应保持最底层，不应依赖 Config、Engine、Cli、Content、Rendering、Routing。现有架构测试 `DependencyMatrixTests` 对 Shared 依赖方向已有约束。

保留判断：必须保留。它是 Core、PluginHost、Importing 等上层项目的基础设施层。

对外插件边界：第三方外部插件不应必须引用它。官方插件文档允许官方插件引用 `Bukit.Shared`，但协议不应以它为强制依赖。若未来 SDK 存在，也不应简单把 `Shared` 整包暴露给第三方。

### 5.2 `Bukit.Cli.Shared`

职责：CLI 元数据、解析、绑定、help/error rendering、配置路径解析等 CLI 基础组件。

依赖判断：它可以依赖 `Bukit.Shared`，不应依赖 `Bukit.Cli.Commands`、Engine、Content、Rendering、Routing 或 Labs。当前 `DependencyMatrixTests` 已有 `CliShared_MustOnlyDependOn_Shared` 约束。

保留判断：必须保留。它使 Core CLI 和 Labs CLI 可以共享命令元数据模型，同时避免 Labs 直接依赖 Core CLI。

对外插件边界：外部插件不应引用该层。插件命令 descriptor 由 Core `PluginCliLoader` 根据 runtime manifest 生成，插件进程本身只返回协议 JSON。

### 5.3 `Bukit.Config`

职责：`site.yaml`、多站点配置、默认值、严格字段校验、JSON schema、部署配置、主题 manifest 严格校验、i18n 校验、collection 校验、环境变量覆盖。

关键证据：

- `src/Bukit.Config/AppConfig.cs` 包含 `SiteConfig.Plugins`，用于 Core 内置插件开关。
- `src/Bukit.Config/ConfigStrictFieldValidator.cs` 允许 `site.plugins`、`site.pluginFailMode`、`site.deriveConflictPolicy`，但当前主线不应允许旧 `site.externalPlugins`。
- `tests/Bukit.Config.Tests/ConfigLoaderTests.cs` 第 846 行存在 `Load_ExternalPlugins_ThrowsConfigException`。
- `tests/Bukit.Cli.Tests/BuildCommandTests.cs` 第 296 行存在 `RunAsync_ExternalPluginsConfig_ThrowsConfigException`。
- `tests/Bukit.Cli.Tests/BuildCommandTests.cs` 第 359 行存在 `BuildSpec_DoesNotExposeAllowExternalPluginsFlag`。

保留判断：必须保留。

对外插件边界：`site.yaml` 不应重新承担外部插件配置。外部插件配置属于 `.bukit/plugins.yaml`，由 `Bukit.PluginHost.PluginConfigLoader` 读取。`site.plugins` 只能理解为 Core 内置插件开关，不应复活旧外部插件机制。

### 5.4 `Bukit.Engine.Abstractions`

职责：Core 内部的内容文档、路由信息、构建上下文、内置插件接口、插件执行信息等抽象。

关键证据：

- `src/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj` 第 4-5 行引用 `Bukit.Config` 和 `Bukit.Shared`。
- 它包含 `IBukitPlugin`、`IDerivePagesPlugin`、`IAfterBuildPlugin`、`IOrderedPlugin`、`ITemplateRequirementPlugin` 等 Engine 内置插件接口。
- 它还包含 `ISectionPlugin` 与 `SectionPluginRegistry`，这是本报告认定的高风险遗留扩展点。

保留判断：作为 Core 内部抽象层，当前仍需保留。它支撑 Engine、Content、Routing、Theme、Rendering 的共享模型。

禁止判断：不能把它作为外部插件 SDK。理由有三点：

1. 它依赖 `Bukit.Config`，不是纯协议包。
2. 它暴露的是构建上下文和进程内插件接口，和 `bukit-plugin-v1` 外部进程协议相冲突。
3. 第三方插件一旦引用它，就会绕过“第三方插件只实现 JSON 协议”的边界。

整改建议：保留构建/内容/路由抽象；移除或内部化 `ISectionPlugin`、`SectionPluginRegistry` 等进程内插件扩展点，除非明确作为 Core 内置主题能力。

### 5.5 `Bukit.Content`

职责：内容源抽象、Markdown 内容源、Notion 内容源、媒体本地化、body store、内容组合 provider。

依赖判断：`src/Bukit.Content/Bukit.Content.csproj` 依赖 `Bukit.Engine.Abstractions`、`Bukit.Config`、`Bukit.Shared`，并使用 `Markdig`。这是合理的 Core 内容层依赖方向。

保留判断：必须保留。内容读取是 Core build 的输入层。

对外插件边界：外部插件不应直接调用 Content provider。若未来插件需要读取内容，应由 PluginHost 在 `invoke` request 中提供受控上下文，或由未来 SDK 提供稳定只读能力；不能让插件直接引用 `Bukit.Content`。

### 5.6 `Bukit.Routing`

职责：route generation、路径构造、路径安全校验。

依赖判断：`src/Bukit.Routing/Bukit.Routing.csproj` 依赖 `Bukit.Engine.Abstractions` 和 `Bukit.Shared`。该层不应依赖 Engine、Cli、Rendering。

保留判断：必须保留。

对外插件边界：外部插件不应直接调用 Routing。插件返回 artifact 或命令结果时，只能返回项目相对安全路径，Core PluginHost 负责校验路径。

### 5.7 `Bukit.Theme`

职责：主题 manifest、主题组件、section schema、主题 tokens、主题 catalog、page composer、theme doctor 等。

依赖判断：`src/Bukit.Theme/Bukit.Theme.csproj` 依赖 `Bukit.Config`、`Bukit.Engine.Abstractions`、`Bukit.Shared`。这个依赖方向符合 Core theme runtime。

风险点：`src/Bukit.Theme/Models/ThemeSectionDefinition.cs` 包含 `Plugin` 字段，Engine `ThemeBootstrapper` 会通过 `SectionPluginRegistry` 解析 section plugin。这一链路使主题 manifest 可以指向进程内 section plugin。若该能力不是明确的 Core internal 功能，应删除或改名，避免被理解为外部插件入口。

保留判断：Theme 主体必须保留；`ThemeSectionDefinition.Plugin` 与 section plugin 接入链需要整改。

### 5.8 `Bukit.Rendering`

职责：Scriban 渲染、模板模型绑定、component/section 渲染函数、上下文构建、图片函数、文件模板加载。

风险点：

- `src/Bukit.Rendering/Scriban/SectionRenderHelper.cs` 第 21 行持有 `_sectionPlugins`。
- `src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs` 第 23 行持有 `ITemplateContextContributor` 列表。
- `src/Bukit.Rendering/Scriban/ITemplateContextContributor.cs` 第 12 行定义模板上下文贡献者接口。

这些能力在 Core 内部可解释为扩展点，但它们的命名和形态很容易被误认为插件 SDK。若没有生产实现者，应删除；若保留，应明确 `internal` 或改文档，声明仅限 Core 内部组合，不对外部插件开放。

保留判断：Rendering 主体必须保留；`ISectionPlugin` 接入链和 `ITemplateContextContributor` 对外语义需要收紧。

### 5.9 `Bukit.Engine`

职责：站点构建引擎、build pipeline、variant pipeline、asset pipeline、route pipeline、SEO/GEO/publish 报告、sitemap、robots、内置插件注册和执行。

关键证据：

- `src/Bukit.Engine/Bukit.Engine.csproj` 第 11-16 行依赖 `Bukit.Engine.Abstractions`、`Bukit.Config`、`Bukit.Content`、`Bukit.Rendering`、`Bukit.Routing`、`Bukit.Shared`。
- `src/Bukit.Engine/Plugins/PluginRegistry.cs` 第 12 行定义 `BuiltInPluginSource`。
- `PluginRegistry.cs` 第 16-24 行只注册内置插件：`DataFilesPlugin`、`PagesIndexPlugin`、`TaxonomyPlugin`、`PaginationPlugin`、`ArchivePlugin`、`RelatedContentPlugin`、`AliasPlugin`、`MenuPlugin`、`ImageProcessingPlugin`。
- `PluginRegistry.cs` 第 74-76 行的 sources 数组只有 `BuiltInPluginSource`，没有 external protocol source、DLL source 或 reflection source。
- `src/Bukit.Engine/Plugins/PluginRunner.cs` 第 43 行执行 derive-pages， 第 249 行执行 after-build，第 299 行根据 `site.plugins` 判断内置插件是否启用。

保留判断：必须保留。Engine 内置插件管线是 Core build 内部机制，不等同于正式外部插件机制。

边界判断：Engine 不应加载正式外部插件。当前 `PluginRegistry` 未加载外部协议源，这是正确状态。若未来需要 build hook，也应通过 PluginHost 协议设计，而不是把 external protocol source 接回 Engine。

### 5.10 `Bukit.Cli`

职责：Core CLI 入口、稳定命令 registry、命令 dispatch、插件命令动态装载入口、build/doctor/config/preview/dev/clean/version/completion/seo/geo/publish/deploy 命令。

关键证据：

- `src/Bukit.Cli/Bukit.Cli.csproj` 第 4-8 行引用 `Bukit.Cli.Shared`、`Bukit.Engine`、`Bukit.Config`、`Bukit.PluginHost`、`Bukit.Shared`。
- `src/Bukit.Cli/Program.cs` 第 24 行在未知 core command 时调用 `PluginCliLoader.CreateDefault().LoadAsync(...)`。
- `src/Bukit.Cli/Program.cs` 第 28 行通过 `BukitCliComposer.Compose` 合并 Core descriptors 和 plugin descriptors。
- `src/Bukit.Cli/Cli/BukitCliSpecs.cs` 注册稳定 Core 命令，不直接注册 Import/Clone/Notion 等正式插件命令。
- `src/Bukit.Cli/Cli/BukitCliComposer.cs` 检查插件命令不得和 Core 命令冲突。

保留判断：必须保留。CLI 调用 PluginHost 是正确边界。

风险判断：CLI 可以引用 `Bukit.PluginHost`，但不得引用 `plugins/Bukit.Plugin.*` 实现项目。当前 csproj 未引用正式插件实现，合规。

### 5.11 `Bukit.PluginHost`

见第 6 章。结论：必须保留，是外部插件机制的核心宿主。

### 5.12 `Bukit.Plugin.Abstractions`

见第 7 章。结论：必须保留，但只能定位为协议模型包，不是 Core SDK。

### 5.13 `Bukit.Clone`

职责：Clone 领域库骨架。当前 `src/Bukit.Clone/Bukit.Clone.csproj` 没有 ProjectReference；源码包含 `ICloneDomainBlueprint`、`CloneDomainArea`、`CloneDomainAreaDescriptor`、`CloneDomainBlueprint`。

文档证据：`docs/plugins/Codex Clone 类迁移清单.md` 明确本阶段只准备 `Bukit.Clone` 领域库骨架，不迁移 Clone 业务逻辑。`docs/plugins/Bukit Clone 插件迁移计划.md` 将 `Bukit.Clone` 定义为未来 Clone 外部进程插件可引用的领域库。

保留判断：可以保留，但应定义为未来官方插件领域库，不是 Core runtime 必需层。如果 `bukit.slnx` 被定义为 Core-only，则 `Bukit.Clone` 是否放入 Core solution 需要重新确认。若它是 plugin-domain library，应进入 plugins solution 或 tracked-only gate。

对外插件边界：未来 `Bukit.Plugin.Clone` 可以引用 `Bukit.Clone`，但第三方插件不应默认引用。第三方插件仍应通过协议与 Core 通信。

### 5.14 `Bukit.Importing`

职责：Import 领域库，包括 HTML demo scan/import、内容抽取、资产导入、模板生成、seed 生成、route map、import report、安全扫描等。

关键证据：

- `src/Bukit.Importing/Bukit.Importing.csproj` 第 19 行只引用 `Bukit.Shared`，并使用 AngleSharp/YamlDotNet。
- `bukit.experimental.slnx` 第 9 行包含 `src/Bukit.Importing`，而 `bukit.slnx` 不包含它。
- `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` 第 81 行断言 Core CLI project 不包含 `Bukit.Importing`。
- `docs/plugins/Codex 插件机制执行理解摘要.md` 第 77 行说明 Import 插件可引用 `Bukit.Plugin.Abstractions`、`Bukit.Shared`、`Bukit.Importing`，但不得引用 `Bukit.Cli`、`Bukit.Engine`、`Bukit.Labs.Cli`、`Bukit.Labs.Import` 或 `Bukit.PluginHost`。

保留判断：可以保留为官方 Import 插件领域库。它不是 Core runtime 必需层，当前不在 `bukit.slnx`，这个边界比 `Bukit.Clone` 更清晰。

对外插件边界：官方 `Bukit.Plugin.Import` 未来可以引用它；第三方插件不应把它当通用 SDK。

## 6. `Bukit.PluginHost` 深入审计

### 6.1 项目依赖

`src/Bukit.PluginHost/Bukit.PluginHost.csproj` 第 14-15 行只引用：

- `Bukit.Plugin.Abstractions`
- `Bukit.Shared`

它没有引用：

- `Bukit.Engine`
- `Bukit.Cli`
- `Bukit.Content`
- `Bukit.Rendering`
- `Bukit.Routing`
- `plugins/Bukit.Plugin.Echo`
- `plugins/Bukit.Plugin.Import`
- `experimental/Bukit.Labs.Cli`

审计判断：项目引用层面合规。PluginHost 没有直接依赖插件实现，也没有把 Engine 进程内插件接口作为外部插件机制。

### 6.2 配置读取：`.bukit/plugins.yaml`

`src/Bukit.PluginHost/PluginConfigLoader.cs` 负责读取项目级插件配置。关键点：

- 第 89 行读取 `source`。
- 第 93 行读取 `manifestPolicy`，默认 `static`。
- 第 94-97 行允许 `static` 或 `runtime-only`。
- 第 126 行以后读取权限配置。
- 第 145 行校验文件系统权限路径。
- 第 152 行禁止环境变量 wildcard。

审计判断：

1. `.bukit/plugins.yaml` 被 PluginHost 读取，和 `site.yaml` 分离，这是正确边界。
2. `manifestPolicy: runtime-only` 在 loader 层仍被允许。文档允许其用于开发、Labs、兼容或临时动态插件，但正式发布插件必须使用 `static`。因此 loader 层允许不一定违规，但 official package gate 必须禁止。
3. 如果目标是最严格边界，可以考虑只在测试/dev 模式允许 `runtime-only`，正式用户路径默认拒绝。当前最小改动建议是补架构/脚本门禁，不急于改 loader。

### 6.3 插件 source 路径校验

`src/Bukit.PluginHost/PluginPathValidator.cs` 负责路径边界：

- 第 8 行入口 `ValidatePluginSource`。
- 第 23 行要求 source 必须是 `plugins/<id>`。
- 第 30 行要求 source 保持在 `plugins/` 下。
- 第 35 行要求 source 的 real path 也保持在 `plugins/` 下。
- 第 41 行入口 `ValidatePluginEntry`。
- 第 59-61 行拒绝 entry 指向 `.bukit/`。

审计判断：这是正确的外部进程插件路径边界。它防止 `.bukit/plugins`、绝对路径、路径穿越、符号链接逃逸等路径风险。PluginHost 没有把 `.bukit` 当作插件程序目录。

### 6.4 manifest 读取和协议校验

`src/Bukit.PluginHost/PluginManifestLoader.cs` 校验 `plugin.yaml`：

- 第 39 行要求 protocol 必须是 `bukit-plugin-v1`。
- 第 44 行要求 kind 必须是 `process`。
- 第 49 行要求 distribution 必须是 `self-contained`。
- 第 97 行读取 `plugin.platforms`。
- 第 107-108 行要求每个平台声明 `entry` 和 `sha256`。
- 第 114 行要求至少一个 platform。

审计判断：PluginHost 在 manifest 层没有支持 DLL、WASM 或 in-process 插件。它只接受外部进程、自包含分发、带平台入口和 sha256 的插件包。

### 6.5 hash 和 CI 策略

`src/Bukit.PluginHost/PluginHashVerifier.cs`：

- 第 19 行空 sha256 会失败。
- 第 30 行 sha256 不匹配会失败。

`src/Bukit.PluginHost/PluginCiPolicy.cs`：

- 第 23 行要求 CI 中 `allowInCi=true`。
- 第 26-28 行要求 CI 中 sha256 已验证。
- 第 33 行要求 CI 中权限必须显式声明。

审计判断：CI 模式是严格策略。它不能证明本地开发绝对安全，但能防止 CI 中执行未声明、未校验、未授权的插件。

### 6.6 外部进程启动

`src/Bukit.PluginHost/SystemProcessRunner.cs`：

- 第 92 行 `UseShellExecute = false`。
- 第 103 行清空环境变量。
- 第 149 行通过 stdin 写入协议 JSON。

审计判断：

1. 宿主不通过 shell 拼接命令字符串，降低命令注入风险。
2. 宿主清空环境变量后只设置允许的变量，符合权限最小化方向。
3. 插件 stdout/stderr/output limit 和 timeout 由进程 runner 与 protocol client 联合处理。

### 6.7 协议客户端

`src/Bukit.PluginHost/PluginProtocolClient.cs`：

- 第 32 行 `HandshakeAsync`。
- 第 72 行 `GetManifestAsync`。
- 第 105 行 `InvokeAsync`。
- 第 55、95 行校验 common response。
- 第 140 行校验 artifact paths。
- 第 344 行 `ValidateCommonResponse`。
- 第 396 行 `ValidateArtifactPaths`。

审计判断：

1. PluginHost 使用 handshake/manifest/invoke 三段协议，这和 v1 协议一致。
2. response 校验包含 type、protocol、requestId、success。
3. invoke artifact path 只能是项目相对安全路径，避免插件回传绝对路径或路径穿越。

### 6.8 CLI 插件加载

`src/Bukit.Cli/Cli/PluginCliLoader.cs`：

- 第 47 行 `CreateDefault` 组装 `PluginConfigLoader`、`PluginManifestLoader`、`PluginPathValidator`、`PluginPlatformResolver`、`PluginHashVerifier`、`PluginProtocolClient`。
- 第 59 行 `LoadAsync` 读取项目插件配置。
- 第 147 行读取插件 manifest。
- 第 331 行 `runtime-only` 判断。

`src/Bukit.Cli/Program.cs`：

- 第 24 行在 core descriptor 找不到时加载 plugin CLI。
- 第 28 行通过 composer 合并 descriptors。

`src/Bukit.Cli/Cli/BukitCliComposer.cs`：

- 该类检查插件命令不得和 core command 或其他 plugin command 冲突。

审计判断：CLI 调用路径是 `Bukit.Cli -> Bukit.PluginHost -> external process`。当前没有发现 `Bukit.Cli -> plugins/Bukit.Plugin.*` 项目引用。

### 6.9 lock 和 report

`src/Bukit.PluginHost/PluginLockFileWriter.cs` 第 8 行负责写 lock。第 34、39、42 行写 source、sha256、sha256Verified 等字段。

`src/Bukit.PluginHost/PluginExecutionReporter.cs` 第 9 行负责写 execution report，第 60-66 行写 sha256 验证状态。

审计判断：lock/report 是外部插件审计链的一部分，应保留。它们写入 `.bukit` 系统目录，不应和插件可执行程序目录混淆。

### 6.10 PluginHost 保留结论

`Bukit.PluginHost` 是必要层，不应删除。它承担安全边界、协议边界、路径边界和 CLI 扩展边界。删除它会迫使 CLI 或 Engine 直接处理插件，反而更容易破坏协议。

需要补强的是：

1. 增加架构测试，确保 PluginHost 永远不引用 `Bukit.Plugin.*` 实现项目。
2. 增加架构测试，确保 PluginHost 永远不引用 Engine 进程内插件接口。
3. 明确 `runtime-only` 只用于开发/Labs/测试，不进入 official package。
4. 文档中把 PluginHost 说明为 external process host，不允许外部插件调用 Core API。

## 7. `Bukit.Plugin.Abstractions` 深入审计

### 7.1 项目依赖

`src/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj` 第 4 行只声明 `TargetFramework`，没有 ProjectReference，也没有 PackageReference。

审计判断：该项目当前是轻量协议模型包。它不依赖 Core，不依赖 Engine，不依赖 CLI，不依赖 PluginHost。

### 7.2 文件组成

`src/Bukit.Plugin.Abstractions` 包含：

- `Config/PluginHostConfig.cs`
- `Config/PluginConfigEntry.cs`
- `Config/PluginTimeoutOptions.cs`
- `Config/PluginOutputLimitOptions.cs`
- `Manifest/PluginManifest.cs`
- `Manifest/PluginPlatformEntry.cs`
- `Manifest/PluginCommandSpec.cs`
- `Manifest/PluginOptionSpec.cs`
- `Manifest/PluginArgumentSpec.cs`
- `Protocol/PluginProtocolConstants.cs`
- `Protocol/PluginHandshakeRequest.cs`
- `Protocol/PluginHandshakeResponse.cs`
- `Protocol/PluginManifestRequest.cs`
- `Protocol/PluginManifestResponse.cs`
- `Protocol/PluginInvokeRequest.cs`
- `Protocol/PluginInvokeResponse.cs`
- `Protocol/PluginHostInfo.cs`
- `Runtime/PluginInvokeContext.cs`
- `Runtime/PluginInvokeCommand.cs`
- `Security/PluginPermissionSet.cs`
- `Security/PluginFileSystemPermission.cs`
- `Security/PluginEnvironmentPermission.cs`
- `Results/PluginMessage.cs`
- `Results/PluginDiagnostic.cs`
- `Results/PluginArtifact.cs`
- `Results/PluginError.cs`
- `PluginJsonSerializerContext.cs`

### 7.3 协议常量

`src/Bukit.Plugin.Abstractions/Protocol/PluginProtocolConstants.cs` 第 5 行把协议版本固定为 `bukit-plugin-v1`。

审计判断：协议常量集中定义是合理的。它帮助官方 .NET 插件避免字符串漂移，但第三方插件仍可直接按 JSON 协议实现。

### 7.4 JSON source generation

`src/Bukit.Plugin.Abstractions/PluginJsonSerializerContext.cs` 第 12-36 行为 config、manifest、request、response、runtime、permission、result 类型声明 `JsonSerializable`。

审计判断：这是 AOT/Native-friendly 的协议序列化支持。它不暴露 Core 能力，不构成 SDK。

### 7.5 不是 SDK 的原因

`Bukit.Plugin.Abstractions` 不是 SDK，原因如下：

1. 它只定义协议形状，不提供读取站点、读写内容、调用渲染、访问路由、访问 Engine build context 的能力。
2. 协议文档明确第三方插件不需要引用任何 Bukit 程序集，只需实现 JSON 协议。
3. 如果把它宣传为 SDK，会造成插件作者误以为可以依赖 Bukit 程序集。
4. 它目前没有能力封装、权限代理、版本兼容层、稳定 API surface、sample package policy 等 SDK 必备元素。

保留结论：应保留为协议模型包。未来 SDK 不能在它的基础上直接扩大 Core 访问能力，而应另建独立项目，例如 `Bukit.Plugin.Sdk`，且只开放稳定、受控、协议化能力。

## 8. 正式外部插件审计

### 8.1 `plugins/Bukit.Plugin.Echo`

`plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj`：

- 第 4 行 `OutputType` 是 `Exe`。
- 第 8 行 assembly name 是 `bukit-plugin-echo`。
- 第 13 行只引用 `src/Bukit.Plugin.Abstractions`。

审计判断：Echo 是外部进程插件形态，不是 class library 插件。它没有引用 Engine/Cli/PluginHost/Labs，合规。

### 8.2 `plugins/Bukit.Plugin.Import`

`plugins/Bukit.Plugin.Import/Bukit.Plugin.Import.csproj`：

- 第 4 行 `OutputType` 是 `Exe`。
- 第 8 行 assembly name 是 `bukit-plugin-import`。
- 第 13 行只引用 `src/Bukit.Plugin.Abstractions`。

审计判断：Import 当前也是外部进程插件形态。它没有引用 `Bukit.Importing`，说明当前实现仍是 skeleton/protocol shell，不是完整 import 业务迁移。这个状态不违反插件边界，但不能声明 Import 功能完整。

### 8.3 官方插件包示例门禁

`scripts/checks/official-plugin-packages.sh`：

- 第 47-48 行禁止官方插件示例使用 `manifestPolicy: runtime-only`。
- 第 52 行禁止示例配置包含 `entry:`、`.bukit/plugins`、`site.externalPlugins`。

`tests/Bukit.PluginHost.Tests/PluginSchemaContractTests.cs`：

- 第 28 行断言 plugin config schema 中 `manifestPolicy` 默认是 `static`。
- 第 101 行断言官方插件 manifest 不含 `manifestPolicy: runtime-only`。
- 第 127 行断言加载后的官方示例 config 不是 `runtime-only`。
- 第 170-173 行断言官方示例 config 不含 `runtime-only`、`.bukit/plugins`、`site.externalPlugins`。

审计判断：正式官方插件包的示例配置有门禁保护。该保护方向正确，但仍需要补 ProjectReference 架构测试，防止未来官方插件 csproj 引用 Core。

### 8.4 当前正式插件合规结论

当前顶层 `plugins/` 下的正式插件项目没有直接调用 Core 代码的证据。它们只引用 `Bukit.Plugin.Abstractions`，符合官方 .NET 插件允许引用协议模型包的规则。

需要注意：这只是当前项目引用层面合规，不等于完整生态合规。必须继续通过架构测试和脚本门禁防止未来插件项目引用 `Bukit.Engine`、`Bukit.Cli`、`Bukit.PluginHost`、`experimental/Bukit.Labs.Cli` 或 `Bukit.Engine.Abstractions`。

## 9. Core 内置插件审计

### 9.1 内置插件注册

`src/Bukit.Engine/Plugins/PluginRegistry.cs` 第 12 行定义 `BuiltInPluginSource`，第 16-24 行注册当前 Core 内置插件：

1. `DataFilesPlugin`
2. `PagesIndexPlugin`
3. `TaxonomyPlugin`
4. `PaginationPlugin`
5. `ArchivePlugin`
6. `RelatedContentPlugin`
7. `AliasPlugin`
8. `MenuPlugin`
9. `ImageProcessingPlugin`

该文件第 74-76 行的 source 列表只有 `BuiltInPluginSource`。

审计判断：Engine 当前只加载内置插件，没有加载外部协议源、DLL 源或 reflection 源。这里符合“除内置插件外，外部插件不进入 Core 进程内插件管线”的要求。

### 9.2 内置插件执行

`src/Bukit.Engine/Plugins/PluginRunner.cs`：

- 第 43 行开始执行 derive-pages。
- 第 249 行开始执行 after-build。
- 第 299 行读取 `site.plugins` 判断插件启用。
- 第 316 行从 `PluginRegistry.GetAllPlugins(context)` 获取插件列表。

审计判断：`site.plugins` 当前控制的是 Core 内置插件开关，不应解释为外部插件配置。外部插件配置应只通过 `.bukit/plugins.yaml` 进入 PluginHost。

### 9.3 内置插件和外部插件的边界

内置插件可以使用 `IBukitPlugin`、`IDerivePagesPlugin`、`IAfterBuildPlugin` 等进程内接口，因为它们是 Core 自己编译和发布的一部分。

外部插件不允许使用这些接口。外部插件应作为独立进程，通过 stdin/stdout JSON 与 PluginHost 交互。如果外部插件引用这些接口，就会在类型系统上把自己绑定到 Core 内存模型，破坏进程隔离和语言无关协议。

## 10. 违规或高风险遗留点

### F-01：`src/plugins/WordCountSectionPlugin` 违反新插件目录和协议边界

证据：

- `src/plugins/WordCountSectionPlugin/WordCountSectionPlugin.csproj` 第 8 行引用 `src/Bukit.Engine.Abstractions`。
- `src/plugins/WordCountSectionPlugin/WordCountPlugin.cs` 第 5 行实现 `ISectionPlugin`。

问题：

1. 它位于 `src/plugins/`，不是 ADR 要求的顶层 `plugins/Bukit.Plugin.<Name>/`。
2. 它是进程内插件形态，不是外部进程。
3. 它引用 `Bukit.Engine.Abstractions`，外部插件若模仿它会直接调用 Core 抽象。
4. 它不在 `bukit.slnx` 的正式插件列表中，容易成为无人维护的边界噪音。

建议：

- 若没有生产用途，删除整个 `src/plugins/WordCountSectionPlugin`。
- 若仍要保留作为 Core 内置 theme/section 能力，应迁入明确的 Core internal 或测试 fixture 区，并改名避免出现 `plugins` 目录语义。
- 不应迁入顶层 `plugins/` 作为正式插件，除非改造成 `bukit-plugin-v1` 外部进程。

### F-02：`ISectionPlugin` 和 `SectionPluginRegistry` 暴露进程内扩展机制

证据：

- `src/Bukit.Engine.Abstractions/Plugins/ISectionPlugin.cs` 第 21 行定义 `ISectionPlugin`。
- 同文件第 27 行定义 `SectionPluginRegistry`。
- 第 29 行使用全局 concurrent dictionary 存储插件实例。
- 第 31 行提供 `Register`。
- 第 49 行提供 `TryResolve`。
- `src/Bukit.Engine/ThemeBootstrapper.cs` 第 90、96 行通过 `SectionPluginRegistry` 解析 section plugin。
- `src/Bukit.Rendering/Scriban/SectionRenderHelper.cs` 第 21 行持有 section plugin 字典。

问题：

1. 这是进程内扩展点，与 `bukit-plugin-v1` 外部进程协议不同。
2. 它位于 `Engine.Abstractions`，容易被外部插件误用。
3. 当前扫描到的生产注册者只有旧 `WordCountSectionPlugin`，测试中也有 `SectionPluginRegistryTests`，说明该机制更像早期扩展设计残留。
4. 主题 manifest 中的 section plugin 字段会把主题渲染和进程内插件注册耦合。

建议：

- 若没有当前产品需求，删除 `ISectionPlugin`、`SectionPluginRegistry`、`ThemeSectionDefinition.Plugin`、`ThemeBootstrapper` 中的 section plugin resolve、`SectionRenderHelper` 中的 before/after render hook。
- 若有当前产品需求，必须改为 Core internal，不允许外部插件或第三方主题作者通过该机制注入代码。
- 对外文档中不得把该机制描述为插件开发方式。

当前补充（2026-07-05）：

- 已采用 Core internal 方案：`ISectionPlugin`、`SectionPluginRegistry`、`SectionHook`、`SectionContext` 不再作为 public API 暴露，只通过受控 `InternalsVisibleTo` 给 Core 内部的 Engine/Rendering 使用。
- `theme.yaml.sections.*.plugin` 不再是合法主题 manifest 字段，第三方主题作者不能通过 manifest 指定进程内插件名。
- `WordCountSectionPlugin` 暂时保留，但已移除对 `Bukit.Engine.Abstractions` 的引用，不再实现或注册 Core 内部 section plugin 接口。

### F-03：`ITemplateContextContributor` 存在外部扩展语义

证据：

- `src/Bukit.Rendering/Scriban/ITemplateContextContributor.cs` 第 12 行定义接口。
- `src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs` 第 23 行持有 contributors。
- `src/Bukit.Engine/TemplateRendererBase.cs` 第 17 行文档注释提到 `ITemplateContextContributor`。

问题：

1. 它可以作为 Core 内部模板上下文组合点，但名称和注释容易被理解为插件或扩展机制。
2. 如果外部插件可以实现并注入该接口，就会绕过外部进程协议。
3. 当前扫描未确认有明确生产实现者，存在“为未来扩展预留但边界未定”的风险。

建议：

- 若没有生产实现者，删除该接口和构造参数。
- 若有内部用途，将其保持 internal 或放在 Core internal namespace，不对插件文档公开。
- 不要把它作为未来 SDK 的基础。未来 SDK 应通过协议提供上下文，而不是让插件注入 Scriban runtime。

### F-04：`experimental/Bukit.Labs.Protocol` 保留旧 `site.externalPlugins` 模型

证据：

- `experimental/Bukit.Labs.Protocol/EngineProtocol/ExternalProtocolPluginSource.cs` 第 22 行读取 `_context.Config.Site.ExternalPlugins`。
- 同文件第 41 行遍历 `ExternalPlugins`。
- 同文件第 59 行错误信息使用 `site.externalPlugins`。
- `experimental/Bukit.Labs.Protocol.Tests/LegacyCoreTests` 下存在多个 `ExternalPlugins` 旧模型测试。

问题：

1. 该代码位于 `experimental/`，不是当前 Core 运行时路径。
2. 但它保留了旧 `site.externalPlugins` 和 Engine protocol source 思路。
3. 若后续有人把它迁回 Core，会直接违反“不恢复 `site.externalPlugins`、不恢复 dynamic DLL/runtime protocol source”的当前插件协议。

建议：

- 将整个 `experimental/Bukit.Labs.Protocol` 标记为 legacy/deprecated。
- 若不再需要，删除。
- 若保留用于历史对照，必须在 README 或目录级说明中写明不得迁回 Core，不得作为新插件实现模板。

### F-05：`manifestPolicy: runtime-only` 在 loader 层仍可用

证据：

- `src/Bukit.PluginHost/PluginConfigLoader.cs` 第 93-97 行允许 `manifestPolicy` 为 `static` 或 `runtime-only`。
- `src/Bukit.Cli/Cli/PluginCliLoader.cs` 第 331 行判断 runtime-only。
- `tests/Bukit.PluginHost.Tests/PluginConfigLoaderTests.cs` 第 35、68 行测试 runtime-only 能被读取。
- `docs/plugins/Bukit 插件配置规范.md` 第 339-341 行说明 runtime-only 仅用于开发、Labs、兼容或临时动态插件，正式发布插件必须使用 static。
- `scripts/checks/official-plugin-packages.sh` 第 47-48 行禁止官方插件示例使用 runtime-only。

问题：

1. loader 允许 runtime-only，官方包门禁禁止 runtime-only。这两者并不矛盾，但需要清晰边界。
2. 如果用户项目可以随意启用 runtime-only，PluginHost 会以 runtime manifest 作为命令来源，静态 `plugin.yaml` 对命令面的约束变弱。
3. 在最严格边界下，runtime-only 应被视为 dev/Labs 特权，不应是普通正式插件路径。

建议：

- 保留当前 loader 行为可以接受，但必须通过文档和脚本声明 runtime-only 非正式发布路径。
- 更严格方案：新增环境变量或 config mode，只在 dev/Labs/test 场景允许 runtime-only。
- 架构测试或 schema 测试继续保证 official examples 不使用 runtime-only。

### F-06：架构测试未覆盖新插件边界

证据：

- `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` 第 13 行检查 Core CLI command whitelist。
- `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` 第 87 行检查 `PluginRegistry` 不加载 `ExternalProtocolPluginSource`。
- `tests/Bukit.Architecture.Tests/DependencyMatrixTests.cs` 覆盖 Shared/Config/Engine/Cli/Labs 的部分依赖方向。
- `tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj` 第 20 行引用 `experimental/Bukit.Labs.Cli`，用于测试 Labs 边界。

缺口：

1. 没有测试 `Bukit.PluginHost` 不得引用 `Bukit.Engine`、`Bukit.Cli`、`Bukit.Content`、`Bukit.Rendering`、`Bukit.Routing`、`Bukit.Plugin.*`。
2. 没有测试 `Bukit.Plugin.Abstractions` 不得引用任何 Core 项目。
3. 没有测试顶层 `plugins/Bukit.Plugin.*` 不得引用 `Bukit.Engine`、`Bukit.Cli`、`Bukit.PluginHost`、`experimental/Bukit.Labs.*`。
4. 没有测试 `src/plugins/` 旧位置不得存在正式插件源码。
5. 没有测试 `bukit.slnx` 是否应为 Core-only solution。

建议：

- 新增 `PluginBoundaryTests`。
- 读取 `.csproj` 文本和 compiled assembly references 双重检查。
- 明确 official plugin 可引用列表：`Bukit.Plugin.Abstractions`，可选 `Bukit.Shared`，可选稳定领域库如 `Bukit.Importing` 或 `Bukit.Clone`。禁止 `Bukit.Engine`、`Bukit.Engine.Abstractions`、`Bukit.Cli`、`Bukit.PluginHost`、`Bukit.Labs`。

### F-07：`Bukit.Clone` 在 `bukit.slnx` 中的边界需要明确

证据：

- `bukit.slnx` 第 5 行包含 `src/Bukit.Clone/Bukit.Clone.csproj`。
- `src/Bukit.Clone` 是未来 Clone 插件领域库骨架。
- `docs/plugins/Bukit Clone 插件迁移计划.md` 将它定义为 Clone 插件领域库。

问题：

1. 如果 `Bukit.Clone` 是 Core runtime 层，则需要说明它为什么属于 Core。
2. 如果它是未来官方插件领域库，则放在 Core solution 会使 Core/Plugin 领域边界不够清晰。
3. 它当前很小且无依赖，风险不高，但分类需要明确。

建议：

- 将 `Bukit.Clone` 标注为 plugin-domain library，而不是 Core runtime。
- 若拆 solution，把它移入 plugin/clone 相关 solution 或 tracked-only gate。
- 不允许外部第三方插件默认引用它；它只应服务官方 Clone 插件迁移。

当前补充（2026-07-05）：

- 当前仓库已拆分为 `bukit-core.slnx`、`bukit-plugins.slnx`、`bukit-labs.slnx`、`bukit-test.slnx`，`Bukit.Clone` 已归入 plugin-domain solution，原始 `bukit.slnx` 混入 Core 的问题已不再是当前主要风险。
- F-07 的剩余问题转为：`bukit-plugins.slnx` 仍包含 `WordCountSectionPlugin`，会继续混淆“正式外部插件”和“旧进程内插件”。
- 当前处理策略：暂时保留，不在本轮 F-07 修复中移动或删除；后续按单项任务逐项处理旧式进程内插件残留。

## 11. 删除、保留、隔离清单

### 11.1 应保留

| 对象 | 保留理由 |
|---|---|
| `src/Bukit.PluginHost` | 外部进程插件宿主，是协议、安全、路径和 CLI 扩展边界核心。 |
| `src/Bukit.Plugin.Abstractions` | 协议 DTO 和序列化模型包，官方 .NET 插件可用，但不是 SDK。 |
| `src/Bukit.Engine/Plugins/PluginRegistry.cs` | Core 内置插件注册表，只注册 built-in source。 |
| `src/Bukit.Engine/Plugins/PluginRunner.cs` | Core 内置插件执行器，服务 derive-pages 和 after-build 内置流程。 |
| `.bukit/plugins.yaml` 机制 | 项目级外部插件启用配置，和 `site.yaml` 分离。 |
| `plugins/Bukit.Plugin.Echo` | 合规的外部进程插件示例/测试插件。 |
| `plugins/Bukit.Plugin.Import` | 合规的 Import 插件协议 shell；功能完整性另行判断。 |
| `scripts/checks/official-plugin-packages.sh` | 官方插件包示例配置门禁，禁止 runtime-only、entry、`.bukit/plugins`、`site.externalPlugins`。 |

### 11.2 应删除或迁移

| 对象 | 当前问题 | 建议处理 |
|---|---|---|
| `src/plugins/WordCountSectionPlugin` | 旧式进程内插件，引用 `Engine.Abstractions`，位于旧插件路径。 | 删除；若仍需作为测试资产，迁到 tests fixture；若作为内置能力，迁到 Core internal。 |
| `src/Bukit.Engine.Abstractions/Plugins/ISectionPlugin.cs` 中的 `SectionPluginRegistry` | 全局进程内插件注册表，容易成为第三方插件入口。 | 删除或 internal 化；不对外部插件开放。 |
| `ThemeSectionDefinition.Plugin` | 主题 manifest 可指向进程内插件名。 | 若无内置需求则删除字段和解析逻辑。 |
| `ThemeBootstrapper` section plugin resolve | 把主题引导和进程内插件注册表耦合。 | 随 `SectionPluginRegistry` 一起删除或 internal 化。 |
| `SectionRenderHelper` section plugin hook | 渲染阶段执行进程内插件。 | 随 section plugin 机制一起删除或 internal 化。 |
| `ITemplateContextContributor` 对外扩展语义 | 容易被误用为模板插件 SDK。 | 无生产实现则删除；有内部用途则 internal 化。 |
| `experimental/Bukit.Labs.Protocol` 旧协议 | 仍使用 `site.externalPlugins`、旧 ExternalProtocol source。 | 标记 deprecated 或删除；不得迁回 Core。 |

### 11.3 应保留但改分类

| 对象 | 当前状态 | 建议分类 |
|---|---|---|
| `src/Bukit.Clone` | 在 `bukit.slnx`，未来 Clone 领域库骨架。 | plugin-domain library，不是 Core runtime 必需层。 |
| `src/Bukit.Importing` | 在 experimental solution，Import 领域库。 | official plugin-domain library，供 `Bukit.Plugin.Import` 后续引用。 |
| `tests/ThrowingPlugin` | 引用 `Engine.Abstractions`，用于 Engine tests。 | 测试夹具，不是外部插件；建议改名避免误导。 |
| `tests/PluginProcessProbe` | 插件进程 probe。 | PluginHost 测试工具，保留。 |

## 12. 需要新增的门禁和测试

### 12.1 插件项目引用白名单测试

新增测试应扫描 `plugins/Bukit.Plugin.*/*.csproj`：

允许引用：

- `src/Bukit.Plugin.Abstractions`
- 可选 `src/Bukit.Shared`
- 可选官方领域库：`src/Bukit.Importing`、`src/Bukit.Clone`

禁止引用：

- `src/Bukit.Cli`
- `src/Bukit.Engine`
- `src/Bukit.Engine.Abstractions`
- `src/Bukit.Content`
- `src/Bukit.Rendering`
- `src/Bukit.Routing`
- `src/Bukit.Theme`
- `src/Bukit.PluginHost`
- `experimental/Bukit.Labs.*`

### 12.2 PluginHost 依赖边界测试

新增测试应确认 `src/Bukit.PluginHost/Bukit.PluginHost.csproj` 只引用：

- `src/Bukit.Plugin.Abstractions`
- `src/Bukit.Shared`

并禁止引用：

- `plugins/Bukit.Plugin.*`
- `src/Bukit.Engine`
- `src/Bukit.Cli`
- `experimental/*`

### 12.3 Plugin.Abstractions 零 Core 依赖测试

新增测试应确认 `src/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj` 没有任何 ProjectReference。它应保持纯协议模型包。

### 12.4 旧机制禁止回归测试

新增测试应扫描 Core 源码，禁止出现：

- `site.externalPlugins`
- `ExternalProtocolPluginSource`
- `Assembly.LoadFrom`
- `externalAssemblyAllowlist`
- `ExternalAssembly`
- `src/plugins/` 下正式插件源码

注意：文档和 experimental 可能保留历史说明；测试范围应限定 Core runtime、PluginHost runtime 和正式 plugin source，不要误伤历史文档。

### 12.5 Solution 边界测试

如果定义 `bukit.core.slnx`：

- 不应包含 `plugins/Bukit.Plugin.*`。
- 不应包含 `experimental/*`。
- 不应包含 `tests/Bukit.Labs.Cli.Tests`。
- 可以包含 PluginHost 和 Plugin.Abstractions，因为它们是 Core plugin host 基础设施。

如果继续使用 `bukit.slnx` 作为 all-in-one solution，则文档和脚本必须说明它不是 Core-only 证明面。

## 13. SDK 边界

用户要求明确：如果需要调用能力，应独立开发 SDK 开放给插件，SDK 暂时不做。

本次审计结论如下：

1. 当前不应开发 SDK。
2. 当前不应把 `Bukit.Engine.Abstractions` 当 SDK。
3. 当前不应把 `Bukit.Shared` 当 SDK。
4. 当前不应把 `Bukit.Plugin.Abstractions` 宣传成 SDK。
5. 当前正式外部插件只应依赖协议：stdin request、stdout response、stderr log、exit code、`plugin.yaml`、`.bukit/plugins.yaml`。

未来如果开发 SDK，至少应满足：

- 独立项目，例如 `src/Bukit.Plugin.Sdk` 或独立 package。
- 不引用 `Bukit.Engine`、`Bukit.Cli`、`Bukit.PluginHost`。
- API 只封装协议模型、请求解析、响应写入、诊断输出、artifact helper。
- 不提供直接访问 Core build context、rendering context、content provider、routing generator 的能力。
- 明确版本兼容策略，不能随 Core internal model 任意变化。
- SDK 只是插件开发便利层，不是协议本身；第三方仍可不使用 SDK。

## 14. 当前状态逐项判定

| 项目 | 当前状态 | 结论 |
|---|---|---|
| 正式外部插件是否直接引用 Core | `Echo` 和 `Import` 只引用 `Plugin.Abstractions`。 | 当前合规。 |
| Core 是否直接引用正式插件实现 | `Bukit.Cli`、`Bukit.Engine`、`PluginHost` csproj 未引用 `plugins/Bukit.Plugin.*`。 | 当前合规。 |
| PluginHost 是否按协议调用插件 | 有 config、manifest、path、hash、process、handshake、manifest、invoke、report 实现。 | 当前合规。 |
| Engine 是否加载外部插件 | `PluginRegistry` 只有 `BuiltInPluginSource`。 | 当前合规。 |
| `site.externalPlugins` 是否仍在 Core config | Core 测试覆盖其拒绝；旧模型只在 docs/experimental 中。 | Core 主路径合规，experimental 需隔离。 |
| 是否存在进程内插件残留 | `WordCountSectionPlugin`、`SectionPluginRegistry` 存在。 | 不合规或高风险，应删除/隔离。 |
| `Plugin.Abstractions` 是否是 SDK | 不是；它是协议 DTO 包。 | 保留但不能对外宣传为 SDK。 |
| solution 是否体现严格 Core 边界 | `bukit.slnx` 包含 plugins 和 Labs tests。 | 不严格，应拆分或改名定位。 |
| 架构测试是否覆盖插件边界 | 当前覆盖不足。 | 需要新增测试。 |

## 15. 推荐整改顺序

### 第一阶段：不改业务逻辑，先补边界门禁

1. 新增 `PluginBoundaryTests`，锁死 PluginHost、Plugin.Abstractions、official plugins 的 ProjectReference 边界。
2. 新增 `src/plugins` 禁止正式插件源码测试。
3. 新增 old mechanism scan，禁止 Core runtime 出现 `site.externalPlugins`、`ExternalProtocolPluginSource`、`Assembly.LoadFrom`。
4. 明确 `bukit.slnx` 是 all-in-one 还是 Core-only。如果是 all-in-one，新建 core-only solution。

### 第二阶段：删除或隔离旧式进程内插件残留

1. `WordCountSectionPlugin` 暂时保留；后续作为单独任务决定删除、迁移到测试夹具，或改造为明确的 Core internal 能力。
2. `ISectionPlugin`、`SectionPluginRegistry` 已 internal 化，并增加架构测试防止重新 public 暴露。
3. `theme.yaml.sections.*.plugin` 已禁止；渲染链 section plugin hook 仅保留为 Core internal 路径，不允许第三方主题通过 manifest 注入。
4. 清理或 deprecated `experimental/Bukit.Labs.Protocol`。

### 第三阶段：文档和技能对齐

1. 更新文档中任何把 `site.externalPlugins`、动态 DLL、WASM 描述成当前机制的内容。
2. 保留历史文档时加上 legacy/deprecated 标记。
3. 明确 `Plugin.Abstractions` 不是 SDK。
4. 明确未来 SDK 暂不做。

### 第四阶段：正式插件迁移继续推进

1. Import 插件仍是协议 shell；功能迁移应另开任务。
2. Clone 插件应先稳定 `Bukit.Clone` 领域库，再创建外部进程插件。
3. Notion 插件若恢复，应遵循 PluginHost 协议，不复用旧 `site.externalPlugins`。

## 16. 最终审计结论

当前 Bukit 已经建立了正确的正式外部插件主线：`Bukit.Cli -> Bukit.PluginHost -> external process plugin`。`Bukit.PluginHost` 和 `Bukit.Plugin.Abstractions` 都是必要层，不应删除。顶层正式插件项目当前没有直接引用 Core 代码的证据。

但是，仓库还不能宣称插件边界已经达到最严格状态。原因不是当前正式插件已经违规，而是以下遗留和门禁缺口仍存在：

1. `src/plugins/WordCountSectionPlugin` 是旧式进程内插件残留。
2. `ISectionPlugin` 和 `SectionPluginRegistry` 仍暴露进程内扩展机制。
3. Rendering/Theme 仍有 section plugin 接入链。
4. `experimental/Bukit.Labs.Protocol` 保留旧 `site.externalPlugins` 模型。
5. `manifestPolicy: runtime-only` 在 loader 层可用，需要继续限定在 dev/Labs/test 或用 official gate 禁止。
6. `bukit.slnx` 同时包含 Core、Plugins、Labs tests，不是严格 Core-only 边界。
7. 架构测试缺少 PluginHost、Plugin.Abstractions、official plugins 的硬引用边界。

因此，正确整改方向不是开发 SDK，也不是让插件引用 `Engine.Abstractions`。正确方向是：

```text
先锁边界 -> 再删旧机制 -> 再清文档 -> 最后推进官方插件功能迁移
```

在 SDK 暂不做的前提下，外部插件唯一稳定接入方式必须继续保持为：

```text
.bukit/plugins.yaml
plugins/<id>/plugin.yaml
bukit-plugin-v1 JSON protocol
stdin request
stdout response
stderr logs
PluginHost path/hash/permission/timeout/output/report controls
```

任何让外部插件直接引用 `Bukit.Engine`、`Bukit.Engine.Abstractions`、`Bukit.Cli`、`Bukit.PluginHost` 的做法，都应视为边界破坏。
