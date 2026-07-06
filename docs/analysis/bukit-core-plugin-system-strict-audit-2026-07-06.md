# Bukit Core 插件系统严格全方位审计报告

审计日期：2026-07-06  
审计对象：Bukit Core 插件协议、外部进程插件系统、Core 内置插件机制及相关边界  
审计方式：只读审计；未修改运行代码、配置、CI 或备份目录  
报告范围：当前仓库真实实现，不以历史设计稿、旧技能文档或旧目录命名为准

## 1. 总结结论

当前 Bukit 仓库中存在两类必须严格区分的“插件”机制：

1. **正式外部进程插件机制**
   - 主链路是 `Bukit.Cli -> Bukit.PluginHost -> 外部插件进程`。
   - 协议版本是 `bukit-plugin-v1`。
   - 传输方式是独立进程的 `stdin/stdout/stderr`。
   - 协议操作只有三类：`handshake`、`manifest`、`invoke`。
   - 插件以项目内本地包形式启用：项目配置是 `.bukit/plugins.yaml`，插件包静态清单是 `plugins/<id>/plugin.yaml`。
   - 目前暴露给插件的能力主要是“CLI 命令扩展”，不是 build hook，也不是 Core 运行时 SDK。

2. **Core 内置构建插件机制**
   - 主链路在 `Bukit.Engine` 内部。
   - 接口是 `IBukitPlugin`、`IDerivePagesPlugin`、`IAfterBuildPlugin` 等。
   - 运行上下文是 `BuildContext`，可以看到配置、路由文档、内容图、输出目录、SEO 索引、派生文档集合等。
   - 当前只注册仓库内置插件，注册入口是 `BuiltInPluginSource`，位于 `src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs`。
   - 这套机制不是外部进程插件协议的一部分；第三方进程插件不能通过 `bukit-plugin-v1` 直接拿到 `BuildContext`。

因此，当前核心事实是：**Bukit Core 的正式插件扩展面已经收敛到外部进程协议，协议 DTO 放在 `Bukit.Plugin.Abstractions`，Host 放在 `Bukit.PluginHost`，CLI 负责把通过验证的插件命令并入命令树；Core 构建内置插件机制仍存在，但它是内部机制，不等于对外插件 SDK。**

## 2. 审计证据范围

本次审计核对了以下当前主线文件与测试：

- 协议 DTO：`src/Bukit-Core/Bukit.Plugin.Abstractions/`
- 插件 Host：`src/Bukit-Core/Bukit.PluginHost/`
- CLI 插件装载与命令桥接：`src/Bukit-Core/Bukit.Cli/Cli/`
- Core 内置插件接口与运行器：`src/Bukit-Core/Bukit.Engine.Abstractions/Plugins/`、`src/Bukit-Core/Bukit.Engine/Plugins/`
- 构建管线接入点：`src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs`、`src/Bukit-Core/Bukit.Engine/PluginPipeline.cs`
- 官方进程插件样例：`src/Bukit-Plugins/Bukit.Plugin.Echo/`、`src/Bukit-Plugins/Bukit.Plugin.Import/`
- 插件配置与 manifest schema：`docs/schemas/bukit-plugin-config.v1.schema.json`、`docs/schemas/bukit-plugin-manifest.v1.schema.json`
- 插件主线文档：`docs/plugins/Bukit 插件协议 v1 规范.md`、`docs/plugins/Bukit 插件配置规范.md`、`docs/plugins/Bukit Core 插件机制设计文档.md`、`docs/plugins/Bukit 插件安全模型 ADR.md`、`docs/plugins/Codex 插件机制执行理解摘要.md`
- 官方插件包脚本：`scripts/checks/official-plugin-packages.sh`
- 插件相关测试：`tests/Bukit.Plugin.Abstractions.Tests/`、`tests/Bukit.PluginHost.Tests/`、`tests/Bukit.Plugin.Import.Tests/`、`tests/Bukit.Cli.Tests/`、`tests/Bukit.Engine.Abstractions.Tests/`、`tests/Bukit.Engine.Tests/`、`tests/Bukit.Architecture.Tests/`

本报告没有把 `guide-0.1/`、`scripts-0.1/` 作为修改目标，也没有把备份目录视为当前主线行为。

## 3. 当前插件体系分层

### 3.1 外部进程插件层

外部进程插件层由以下项目组成：

- `Bukit.Plugin.Abstractions`
  - 定义协议、配置、manifest、权限、运行时请求和结果 DTO。
  - 不引用其他 Bukit 运行时项目。
  - 插件实现可以引用该项目复用 DTO 和 JSON source generation，也可以自行按 JSON 协议实现。

- `Bukit.PluginHost`
  - 负责读取 `.bukit/plugins.yaml` 与 `plugin.yaml`。
  - 负责路径、平台、hash、权限、CI 策略、协议响应、artifact 路径和执行报告校验。
  - 只引用 `Bukit.Plugin.Abstractions` 与 `Bukit.Shared`。
  - 不引用 CLI、Engine、Rendering、Labs 或官方插件实现。

- `Bukit.Cli`
  - 负责把 Core 命令和插件命令组合成最终命令树。
  - 先装载 Core 命令；只有插件命令不与 Core/其他插件冲突时才允许进入命令树。
  - 对未知命令加载插件；对 `plugin` 管理命令使用容错装载，以便展示诊断。

- `src/Bukit-Plugins/*`
  - 放置官方插件实现和插件业务域。
  - 官方进程插件不得反向引用 `Bukit.PluginHost`、`Bukit.Cli`、`Bukit.Engine`、`Bukit.Labs` 等 Host/Core 运行时项目。

### 3.2 Core 内置构建插件层

Core 内置插件层由以下项目组成：

- `Bukit.Engine.Abstractions/Plugins`
  - 定义 `IBukitPlugin`、`IDerivePagesPlugin`、`IDerivePagesAsyncPlugin`、`IAfterBuildPlugin`、`IAfterBuildAsyncPlugin`、`IOrderedPlugin`、`ITemplateRequirementPlugin`、`BuildContext` 等内部构建插件接口。

- `Bukit.Engine/Plugins`
  - 定义 `PluginRegistry`、`BuiltInPluginSource`、`PluginRunner` 和内置插件实现。

这套机制当前只注册内置插件。它能参与构建生命周期，但它不是 `bukit-plugin-v1` 的外部插件接口。

### 3.3 主题 section 内部扩展点

仓库还存在内部 section 扩展点：

- `ISectionPlugin`
- `SectionContext`
- `SectionPluginRegistry`
- `ITemplateContextContributor`
- Scriban 渲染器中的 section render helper/context builder

这些类型当前是 internal 范围或渲染内部使用，不能被视为稳定外部插件协议。`WordCountSectionPlugin` 目前也没有被接入为正式 `ISectionPlugin` 外部机制；架构测试还明确要求它不能依赖 Core section plugin abstractions。

## 4. `bukit-plugin-v1` 协议

### 4.1 协议常量

协议常量定义在 `PluginProtocolConstants`：

- `ProtocolVersion = "bukit-plugin-v1"`
- `Handshake = "handshake"`
- `Manifest = "manifest"`
- `Invoke = "invoke"`

协议响应类型由 Host 期望值决定：

- `handshakeResponse`
- `manifestResponse`
- `invokeResponse`

Host 会校验响应的 `type`、`protocol`、`requestId`。`handshake` 和 `manifest` 还要求 `success=true`；`invoke` 可以返回失败响应，但仍必须是可解析的 `invokeResponse`。

### 4.2 传输模式

当前协议传输不是 HTTP、gRPC、WASM ABI，也不是 .NET assembly loading。实际传输是：

1. Host 启动插件 manifest 中指定的平台入口。
2. Host 通过标准输入写入单个 JSON 请求。
3. 插件通过标准输出返回单个 JSON 响应。
4. 插件可通过标准错误输出日志或错误文本。
5. Host 对 stdout/stderr 分别施加大小限制。
6. Host 对 handshake、manifest、invoke 分别施加超时。
7. 超时或输出超限时 Host kill 整个进程树。

进程启动由 `SystemProcessRunner` 实现，关键行为是：

- `UseShellExecute=false`
- 使用 `ArgumentList`，不通过 shell 拼接参数
- 重定向 stdin/stdout/stderr
- UTF-8 编码
- `Environment.Clear()` 后只注入允许读取的环境变量
- timeout 后 `process.Kill(entireProcessTree: true)`

### 4.3 请求和响应公共字段

请求公共字段包括：

- `type`
  - 操作类型：`handshake`、`manifest`、`invoke`。
- `protocol`
  - 必须是 `bukit-plugin-v1`。
- `requestId`
  - Host 生成的请求 id，响应必须原样返回。
- `host`
  - `PluginHostInfo`，字段是 `name`、`version`、`platform`。

响应公共字段包括：

- `type`
  - 响应类型，必须匹配操作。
- `protocol`
  - 必须是 `bukit-plugin-v1`。
- `requestId`
  - 必须匹配请求 id。
- `success`
  - 表示插件层面的处理是否成功。
- `error`
  - `PluginError`，包含 `code`、`message`、`details`。
- `messages`
  - `PluginMessage` 列表，用于给 CLI 用户输出信息。
- `diagnostics`
  - `PluginDiagnostic` 列表，用于结构化诊断。

## 5. 插件系统为外部插件提供的接口

本节只描述正式外部进程插件接口。

### 5.1 项目启用接口：`.bukit/plugins.yaml`

作用：项目选择要启用哪些插件、从哪里加载、暴露哪些命令、授予哪些权限以及设置执行限制。

对应 DTO：

- `PluginHostConfig`
  - `version`
  - `plugins`
- `PluginConfigEntry`
  - `enabled`
  - `source`
  - `exposeCommands`
  - `permissions`
  - `timeout`
  - `output`
  - `failMode`
  - `allowInCi`
  - `description`
  - `manifestPolicy`

当前约束：

- `version` 必须存在且等于 `1`。
- 配置文件不存在时返回空插件配置，不报错。
- 插件 id 必须通过 `PluginIdValidator`。
- `source` 必须是 `plugins/<id>`。
- `source` 的最后一段必须与插件 id 一致。
- `exposeCommands` 必须声明。
- `permissions` 必须声明。
- `manifestPolicy` 默认为 `static`。
- `manifestPolicy: runtime-only` 只允许调用方显式使用 development、Labs 或 test runtime-only context；默认 CLI 装载路径拒绝该值。

该接口的关键意义是：插件即使在 runtime manifest 中声明了更多命令，也只有项目配置 `exposeCommands` 允许的命令会进入 CLI 命令树。

### 5.2 插件包静态 Manifest 接口：`plugins/<id>/plugin.yaml`

作用：插件包声明自身身份、协议、类型、分发方式、平台入口、命令和所需权限。

对应 DTO：

- `PluginManifest`
  - `id`
  - `name`
  - `version`
  - `protocol`
  - `kind`
  - `distribution`
  - `platforms`
  - `commands`
  - `requiredPermissions`
- `PluginPlatformEntry`
  - `entry`
  - `sha256`

当前约束：

- `protocol` 必须是 `bukit-plugin-v1`。
- `kind` 必须是 `process`。
- `distribution` 必须是 `self-contained`。
- `platforms` 必须至少包含一个平台。
- 平台入口必须声明 `entry` 和 `sha256`。
- `plugin.yaml` 的 `id` 必须与 `.bukit/plugins.yaml` 中的插件 id 一致。
- 静态策略下，`plugin.yaml` 的 `commands` 必须至少包含一个命令。
- Host 会选择当前 RID 对应的平台入口。
- 当前支持 RID：`win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`。

该接口的关键意义是：Bukit 不信任 runtime manifest 作为唯一事实来源；正式路径下必须有可审计的静态 manifest。

### 5.3 命令声明接口：`PluginCommandSpec`

作用：插件把自己的 CLI 命令、子命令、参数、选项暴露给 Bukit CLI。

对应 DTO：

- `PluginCommandSpec`
  - `name`
  - `description`
  - `aliases`
  - `arguments`
  - `options`
  - `subcommands`
- `PluginArgumentSpec`
  - `name`
  - `description`
  - `required`
- `PluginOptionSpec`
  - `name`
  - `type`
  - `description`
  - `required`
  - `valueName`
  - `allowedValues`
  - `conflictWith`

当前行为：

- CLI 会根据 runtime manifest 生成插件命令 descriptor。
- Core 命令优先；插件命令或 alias 不能与 Core 命令冲突。
- 插件之间也不能声明冲突命令或 alias。
- 子命令通过 `PluginCommandInvoker` 在调用时解析。
- option 类型支持字符串、flag/bool/boolean、int/integer、number/float/double。
- 插件收到的 invoke command 包含叶子命令、命令路径、剩余参数和 option JSON 值。

### 5.4 Handshake 接口

作用：Host 验证插件进程是否能启动、是否支持当前协议、身份是否匹配当前解析出的插件。

请求：

- `PluginHandshakeRequest`
  - `type`
  - `protocol`
  - `requestId`
  - `host`

响应：

- `PluginHandshakeResponse`
  - `type`
  - `protocol`
  - `requestId`
  - `success`
  - `plugin`
  - `error`
  - `messages`
  - `diagnostics`
- `PluginIdentity`
  - `id`
  - `name`
  - `version`
  - `platform`
  - `capabilities`

Host 校验：

- 响应类型必须是 `handshakeResponse`。
- 协议必须是 `bukit-plugin-v1`。
- requestId 必须匹配。
- success 必须为 true。
- plugin identity 必须存在。
- `id`、`version`、`platform` 必须匹配 Host 已解析出的插件。

### 5.5 Runtime Manifest 接口

作用：Host 在运行时向插件查询当前命令能力与权限需求，并与静态 manifest 和项目授权做二次校验。

请求：

- `PluginManifestRequest`
  - `type`
  - `protocol`
  - `requestId`
  - `host`

响应：

- `PluginManifestResponse`
  - `type`
  - `protocol`
  - `requestId`
  - `success`
  - `capabilities`
  - `commands`
  - `requiredPermissions`
  - `error`
  - `messages`
  - `diagnostics`

Host 校验：

- 响应类型必须是 `manifestResponse`。
- 协议必须是 `bukit-plugin-v1`。
- requestId 必须匹配。
- success 必须为 true。
- runtime commands 必须非空。
- 正式静态策略下，runtime commands 必须是静态 manifest commands 的受控子集或匹配集合。
- runtime `requiredPermissions` 必须被项目授予的 permissions 覆盖。

### 5.6 Invoke 接口

作用：Host 把用户实际调用的插件 CLI 命令转成协议请求，并把插件响应映射回 CLI exit code、用户消息、诊断和执行报告。

请求：

- `PluginInvokeRequest`
  - `type`
  - `protocol`
  - `requestId`
  - `host`
  - `command`
  - `context`
  - `permissions`
- `PluginInvokeCommand`
  - `name`
  - `path`
  - `arguments`
  - `options`
- `PluginInvokeContext`
  - `rootDir`
  - `workingDir`
  - `configPath`
  - `outputDir`
  - `environment`

当前 CLI 实际填充：

- `rootDir = Directory.GetCurrentDirectory()`
- `workingDir = Directory.GetCurrentDirectory()`
- `configPath` 和 `outputDir` DTO 支持，但当前 CLI 命令桥接没有填充。
- `environment` DTO 支持，但当前 CLI 命令桥接没有放入 context；环境变量通过进程环境注入，而不是 context 字典。

响应：

- `PluginInvokeResponse`
  - `type`
  - `protocol`
  - `requestId`
  - `success`
  - `exitCode`
  - `error`
  - `messages`
  - `diagnostics`
  - `artifacts`

Host 行为：

- `invoke` 响应类型必须是 `invokeResponse`。
- 协议和 requestId 必须匹配。
- Host 不要求 invoke `success=true`，但响应必须可读、可解析、字段匹配。
- 如果插件进程退出码和响应 `exitCode` 不一致，Host 会追加 warning 诊断。
- Host 会校验 artifact path 必须是安全项目相对路径，不能是绝对路径，不能含 `.` 或 `..` traversal 段。
- CLI 输出 `messages`：`error`/`warn` 级别走 stderr，其余走 stdout。
- CLI 输出 `diagnostics` 到 stderr。
- CLI 返回 `response.ExitCode`。

### 5.7 权限接口

作用：声明插件需要什么能力，并让项目配置显式授予这些能力。

对应 DTO：

- `PluginPermissionSet`
  - `fileSystem`
  - `network`
  - `environment`
- `PluginFileSystemPermission`
  - `read`
  - `write`
- `PluginEnvironmentPermission`
  - `read`

当前校验：

- file system 权限路径必须是安全项目相对路径。
- 禁止绝对路径。
- 禁止 `..` traversal。
- `.bukit` 默认禁止。
- `.bukit/reports/plugin-output/<plugin-id>` 和 `.bukit/tmp/<plugin-id>` 允许作为插件自己的报告/临时空间。
- `environment.read` 禁止 `*`。
- `requiredPermissions` 必须是项目授予权限的子集。
- `network=true` 需要项目授予 `network=true`。
- Host 只把 `environment.read` 中列出的环境变量注入子进程。

重要边界：

- 当前权限模型是声明式校验、环境 allowlist、路径/manifest 校验和执行报告审计。
- 它不是完整 OS 级文件系统 sandbox。
- `network=true/false` 当前是声明和准入语义，不是由 Host 强制实施的网络隔离。

### 5.8 结果与诊断接口

作用：插件向 Host 和用户返回结构化结果。

接口：

- `PluginMessage`
  - 面向用户的信息输出。
- `PluginDiagnostic`
  - 结构化诊断，包含 code、severity、message、path 等。
- `PluginArtifact`
  - 插件生成物，包含 `type`、`path`、`description`。
- `PluginError`
  - 协议错误或业务错误，包含 `code`、`message`、`details`。

Host 额外做：

- artifact path 安全校验。
- 执行报告中 mask 环境变量和 stderr/diagnostic/artifact description 中出现的 secret value。
- 对包含 `NOTION_TOKEN`、`API_KEY`、`PASSWORD`、`TOKEN`、`SECRET` 等片段的环境变量 key 进行值掩码。

### 5.9 Lock 和执行报告接口

严格来说，lock 和 execution report 是 Host 输出物，不是插件输入接口，但它们是插件系统运行机制的一部分。

`PluginLockFileWriter` 写入：

- `.bukit/plugins.lock.yaml`
- 包含 resolved 插件 id、source、manifestVersion、protocol、platform、entry、sha256、commands、resolvedAt、sha256Verified。

`PluginExecutionReporter` 写入：

- `.bukit/reports/plugin-executions/<plugin>-invoke-<timestamp>.json`
- 包含 pluginId、pluginVersion、operation、protocol、platform、command、commandPath、entry、requestId、processExitCode、responseExitCode、sha256Verified、success、timedOut、outputLimitExceeded、stdout/stderr byte count、masked stderr、masked environment、permissions、diagnostics、artifacts、responseSummary。

这些文件用于审计和复现，但 lock 文件不会绕过 manifest/hash/权限校验。

## 6. 外部进程插件完整运行机制

### 6.1 命令装配入口

CLI 的基本策略是：

1. 先准备 Core 命令描述符。
2. 再在需要时加载插件命令描述符。
3. `BukitCliComposer` 合并 Core 命令和插件命令。
4. Core 命令名和 alias 占用优先级最高。
5. 插件命令或 alias 与 Core 冲突时失败。
6. 插件之间冲突时失败。

这保证插件不能覆盖 Core 命令。

### 6.2 配置加载

`PluginCliLoader.LoadAsync` 调用 `PluginConfigLoader.LoadAsync(projectRoot)`：

1. 定位 `.bukit/plugins.yaml`。
2. 文件不存在时返回空配置。
3. 文件存在时要求 YAML root 是 mapping。
4. 要求 `version: 1`。
5. 逐个读取 `plugins` entries。
6. 校验插件 id。
7. 读取 enabled/source/exposeCommands/permissions/timeout/output/failMode/allowInCi/description/manifestPolicy。
8. 校验权限路径与环境变量 wildcard。

如果是 `plugin` 管理命令，CLI 会使用 `toleratePluginFailures: true`，因此 config/manifest 错误会进入 list record，而不是让管理命令完全不可用。

### 6.3 source 和 identity 校验

每个插件 entry 进入加载时：

1. `PluginPathValidator.ValidatePluginSource` 校验 source。
2. source 必须是 `plugins/<id>`。
3. source 不能是绝对路径。
4. source 不能包含 `.` 或 `..`。
5. source 必须在项目 `plugins/` 下。
6. real path 也必须留在 `plugins/` 下，防止 symlink/realpath 绕出。
7. source 最后一段必须等于插件 id。
8. 插件 id 再次通过 `PluginIdValidator`。
9. `exposeCommands` 必须显式声明。

如果插件 disabled：

- Host 不启动插件进程。
- CLI 仍可为配置中声明的命令创建 disabled descriptor。
- `plugin list` 可显示 disabled 状态。

### 6.4 静态 Manifest 加载

插件 enabled 时：

1. `PluginManifestLoader.LoadAsync(source.FullPath)` 读取 `plugin.yaml`。
2. 要求 manifest 存在。
3. 要求 YAML root 是 mapping。
4. 读取 id/name/version/protocol/kind/distribution/platforms/commands/requiredPermissions。
5. 校验 `protocol == bukit-plugin-v1`。
6. 校验 `kind == process`。
7. 校验 `distribution == self-contained`。
8. 要求 platforms 非空。
9. 每个平台必须有 entry 和 sha256。
10. 校验 manifest id 与配置 id 一致。
11. 静态策略下要求 commands 非空。
12. 校验项目授予权限覆盖 static manifest `requiredPermissions`。

### 6.5 平台选择、入口路径和 hash

Host 使用 `PluginPlatformResolver.GetCurrentRid()` 得到当前 RID：

- Windows x64/arm64 -> `win-x64`/`win-arm64`
- Linux x64/arm64 -> `linux-x64`/`linux-arm64`
- macOS x64/arm64 -> `osx-x64`/`osx-arm64`

然后：

1. 在 manifest `platforms` 中查找当前 RID。
2. 缺少当前平台时报错。
3. `PluginPathValidator.ValidatePluginEntry` 校验 entry。
4. entry 必须是相对路径。
5. entry 必须留在插件目录内。
6. entry real path 也必须留在插件目录内。
7. entry 不能位于 `.bukit/`。
8. `PluginHashVerifier.VerifySha256Async` 校验二进制 sha256。
9. hash 不匹配时加载失败。

### 6.6 CI 策略

`PluginCiPolicy` 使用环境变量 `CI=true` 判定 CI。加载时会结合：

- `allowInCi`
- 平台 entry
- sha256 校验结果

进行准入。官方示例配置中 Import 插件 `allowInCi: true`，且脚本要求官方示例不得使用 `manifestPolicy: runtime-only`。

### 6.7 ResolvedPlugin 建立

前置校验通过后，CLI 构造 `ResolvedPlugin`，核心字段包括：

- 插件 id
- manifest version
- 当前 platform/RID
- executable path
- plugin working directory
- host info
- project root
- timeout options
- output options
- granted permissions
- allowlisted environment variables
- sha256Verified

Host info 是：

- name: `Bukit`
- version: CLI build version
- platform: 当前 RID

### 6.8 Handshake 阶段

Host 发送 `handshake` 请求：

1. 生成 requestId。
2. 构造 `PluginHandshakeRequest`。
3. 按 `timeout.handshakeMs` 启动插件进程。
4. 向 stdin 写入请求 JSON。
5. 从 stdout 读取响应 JSON。
6. 要求进程 exit code 为 0。
7. 要求没有超时和输出超限。
8. 反序列化为 `PluginHandshakeResponse`。
9. 校验 type/protocol/requestId/success。
10. 校验 plugin identity 存在。
11. 校验 id/version/platform 与已解析插件一致。

Handshake 失败意味着插件不能进入 CLI 命令树。

### 6.9 Runtime Manifest 阶段

Host 发送 `manifest` 请求：

1. 生成 requestId。
2. 构造 `PluginManifestRequest`。
3. 按 `timeout.manifestMs` 启动插件进程。
4. 校验进程成功、响应 JSON、type/protocol/requestId/success。
5. 要求 runtime commands 非空。
6. 静态策略下校验 runtime commands 与静态 manifest commands 一致或受控。
7. 校验 runtime `requiredPermissions` 仍被项目授予权限覆盖。
8. 只选择 `.bukit/plugins.yaml` 中 `exposeCommands` 指定的命令。

这一步确保插件运行时不能悄悄扩展出项目没有暴露的命令或权限。

### 6.10 命令 descriptor 生成

对于最终暴露的每个命令：

1. `PluginCommandDescriptorFactory` 创建 CLI command descriptor。
2. descriptor 持有 `ResolvedPlugin`、`PluginCommandSpec`、`IPluginProtocolClient`。
3. 用户调用该命令时进入 `PluginCommandInvoker.InvokeAsync`。

CLI command spec 的解析由 Bukit 的 CLI binding 系统完成，插件最终拿到的是协议层的 command path、arguments、options。

### 6.11 Invoke 阶段

用户实际执行插件命令时：

1. `PluginCommandInvoker` 解析子命令路径。
2. 读取 CLI arguments。
3. 根据 option 类型把 CLI option 转成 JSON element。
4. 构造 `PluginInvokeRequest`。
5. 请求中带上 command、context、granted permissions。
6. `PluginProtocolClient.InvokeAsync` 会重新生成 requestId，并覆盖 type/protocol/host，避免调用方伪造。
7. 按 `timeout.invokeMs` 启动插件进程。
8. 允许 invoke 进程非零退出，但响应必须可读、未超时、未输出超限。
9. 反序列化 `PluginInvokeResponse`。
10. 校验 type/protocol/requestId。
11. 校验 artifact paths。
12. 如果进程 exit code 与 response exitCode 不一致，追加 warning diagnostic。
13. 无论成功或失败，都尝试写 execution report。
14. CLI 输出 messages 和 diagnostics。
15. CLI 返回 response exitCode。

### 6.12 Lock 写入

插件装载完成后，如果存在成功解析的插件：

1. `PluginLockFileWriter` 写 `.bukit/plugins.lock.yaml`。
2. 内容按插件 id 排序。
3. lock 记录 resolved source、manifest version、protocol、platform、entry、sha256、commands、resolvedAt、sha256Verified。

该 lock 是解析结果快照，不是信任根。

## 7. Core 内置插件机制

### 7.1 内部接口清单

Core 内置插件接口定义在 `Bukit.Engine.Abstractions/Plugins`：

- `IBukitPlugin`
  - 提供 `Name` 与 `Version`。
  - 所有内置构建插件的基础身份接口。

- `IDerivePagesPlugin`
  - 同步派生页面 hook。
  - 输入 `BuildContext`。
  - 输出 `IReadOnlyList<RoutedContentDocument>`。

- `IDerivePagesAsyncPlugin`
  - 异步派生页面 hook。
  - 支持 cancellation token。

- `IAfterBuildPlugin`
  - 同步 build 后 hook。
  - 在页面渲染与资产同步后运行。

- `IAfterBuildAsyncPlugin`
  - 异步 build 后 hook。

- `IOrderedPlugin`
  - 通过 `Order` 控制插件执行顺序。

- `ITemplateRequirementPlugin`
  - 返回插件需要的 template kind。
  - `PluginRunner.CollectTemplateRequirementKinds` 会收集这些需求。

- `BuildContext`
  - Core 内部构建上下文。
  - 包含 config、root/output/layouts、routed documents、static HTML routes、content graph、body store、SEO index、derived documents、derived routes、plugin executions、data bag、template resolver、logger。

这些接口当前没有通过 `bukit-plugin-v1` 暴露给外部进程插件。

### 7.2 当前注册的内置插件

`BuiltInPluginSource.GetPlugins()` 当前注册以下 9 个内置插件：

1. `DataFilesPlugin`
   - 处理数据文件相关派生或输出。

2. `PagesIndexPlugin`
   - 生成 pages index 相关派生内容。

3. `TaxonomyPlugin`
   - 处理分类/标签等 taxonomy 页面与数据。

4. `PaginationPlugin`
   - 生成分页派生页面。

5. `ArchivePlugin`
   - 生成归档相关页面。

6. `RelatedContentPlugin`
   - 生成或补充关联内容数据。

7. `AliasPlugin`
   - 处理 alias/重定向类输出。

8. `MenuPlugin`
   - 处理菜单数据。

9. `ImageProcessingPlugin`
   - 处理图片处理相关输出。

注意：仓库中还存在 `FeedPlugin`、`SitemapPlugin`、`SearchIndexPlugin`、`LlmsTxtPlugin` 等类文件，但它们当前不在 `BuiltInPluginSource` 注册列表中。文件存在不等于当前内置插件注册启用。

### 7.3 内置插件执行链路

构建时的内置插件链路在 `VariantBuildPipeline` 中：

1. 构建主题 bootstrap。
2. 构建 data modules。
3. 构建 route pipeline，并得到 `BuildContext`。
4. 调用 `RunPluginDeriveStageAsync`。
5. `PluginRunner.RunDerivePagesAsync` 按 order/name/version 排序执行 derive-pages hook。
6. 派生页面通过冲突策略校验后加入 `pluginContext.DerivedDocuments` 与 `DerivedRoutes`。
7. 普通 routed documents 与 derived documents 一起进入渲染队列。
8. SEO 阶段执行并回填 `pluginContext.SeoIndex`。
9. 页面渲染阶段执行。
10. 资产同步阶段执行。
11. 调用 `RunPluginAfterBuildStageAsync`。
12. `PluginPipeline.ExecuteAsync` 先删除 stale manifest outputs，再调用 `PluginRunner.RunAfterBuildAsync`。
13. after-build 插件运行完成后，`BuildManifestTracker.TrackPluginOutputs` 追踪插件输出。
14. 增量构建开启时保存 build manifest。
15. 构建报告阶段把 plugin context 中的执行信息、派生结果等纳入最终报告。

### 7.4 内置插件控制策略

`PluginRunner` 支持以下行为：

- 根据 `site.plugins` 中的插件开关决定插件是否启用。
- 根据 `site.pluginFailMode` 控制失败策略。
  - `warn` 时记录错误并继续。
  - 非 `warn` 时抛出异常。
- 根据 `site.deriveConflictPolicy` 控制派生页面冲突。
  - `fail`
  - `warn`
  - `last-wins`
- 对实现 `IHookFilterPlugin` 的插件按 hook 名称过滤。
- 对实现 `IOrderedPlugin` 的插件按 `Order` 排序。
- 每次 hook 执行记录 `PluginExecutionInfo`，包括插件名、hook、耗时、成功状态和错误消息。

## 8. 外部插件与 Core 内置插件的边界

### 8.1 外部插件没有的能力

当前正式外部进程插件没有以下接口：

- 没有 `BuildContext`。
- 没有直接访问 `RoutedContentDocument`、`ContentDocument`、`RouteInfo`、`CanonicalContentGraph`。
- 没有 derive-pages hook。
- 没有 after-build hook。
- 没有 Theme renderer 或 Scriban context 的稳定外部接口。
- 没有 `ISectionPlugin` 外部注册协议。
- 没有动态 .NET DLL 加载。
- 没有 `site.externalPlugins`。
- 没有 WASM runtime。
- 没有远程插件市场、自动下载、全局插件目录、热加载、自动更新、签名服务或 Docker sandbox。

### 8.2 外部插件当前真正拥有的能力

当前正式外部插件拥有的能力是：

- 声明一个或多个 CLI 命令。
- 声明子命令、参数、选项。
- 从 Host 收到当前命令调用的参数和 options。
- 读取当前 root/working directory。
- 在项目授权范围内读取指定环境变量。
- 在项目授权语义下声明文件读写和网络需求。
- 返回 messages、diagnostics、artifacts 和 exitCode。
- 通过 artifact 和 execution report 留下审计记录。

换句话说，当前 v1 更像是“受控 CLI command plugin protocol”，而不是“构建时插件 SDK”。

## 9. 官方插件现状

### 9.1 Echo 插件

位置：

- `src/Bukit-Plugins/Bukit.Plugin.Echo/`

特征：

- 进程入口是 `Program.cs`。
- 读取 stdin 中的 JSON。
- 根据 `type` 分发到 handshake、manifest、invoke。
- handshake 返回 id `echo`、name、version、platform 和 capability `cli-command`。
- manifest 返回 `echo` 命令。
- invoke 回显 arguments、options 和 context。
- 项目只引用 `Bukit.Plugin.Abstractions`。

用途：

- 作为最小协议实现样例。
- 用于验证 Host/CLI 协议装载链路。

### 9.2 Import 插件

位置：

- `src/Bukit-Plugins/Bukit.Plugin.Import/`

特征：

- 进程入口 `Program.cs` 只读取 stdin 并调用 `ImportPluginApp.HandleAsync(input)`。
- 项目引用 `Bukit.Plugin.Abstractions` 和业务域 `Bukit.Importing`。
- 不引用 `Bukit.PluginHost`、`Bukit.Cli`、`Bukit.Engine`、`Bukit.Labs`。
- runtime manifest 由 `ImportPluginManifestProvider` 和 `ImportPluginCommandSpecs` 提供。
- 支持 `import html-demo` 和 `import seed`。
- 示例项目配置位于 `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/.bukit/plugins.yaml`。
- 示例插件包 manifest 位于 `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/plugins/import/plugin.yaml`。

Import 示例授权：

- `fileSystem.read: .`
- `fileSystem.write`
  - `./themes`
  - `./sites`
  - `./content`
  - `./data`
  - `./docs/research`
  - `.bukit/reports/plugin-output/import`
- `network: true`
- `environment.read: NOTION_TOKEN`

注意：

- 示例 `plugin.yaml` 中的 sha256 当前是全零占位值。
- 真正 Host 执行会校验 sha256；占位值不能作为真实可执行包通过 hash 校验。
- 官方包脚本主要校验示例配置结构和禁止字段，不等于验证真实发布二进制 hash。

## 10. 安全与治理分析

### 10.1 已实现的安全边界

当前已实现的安全边界包括：

- 外部插件以独立进程运行，不在 Core 进程内加载 DLL。
- 插件入口必须在 `plugins/<id>` 目录下。
- source 和 entry 都做 lexical path 与 realpath/symlink 边界校验。
- entry 不允许进入 `.bukit/`。
- `plugin.yaml` 必须声明协议、类型、分发方式、平台入口和 sha256。
- Host 校验当前平台 entry 的 sha256。
- 正式 CLI 默认拒绝 `manifestPolicy: runtime-only`。
- 静态 manifest commands 与 runtime manifest commands 之间有一致性校验。
- static/runtime required permissions 都必须被项目授权覆盖。
- 环境变量只注入 allowlist。
- `environment.read` 禁止 `*`。
- 子进程环境先清空再注入。
- 不通过 shell 执行插件。
- stdout/stderr 有大小限制。
- handshake/manifest/invoke 有超时。
- 超时或超限 kill 整个进程树。
- artifact path 必须是安全项目相对路径。
- execution report 对环境变量和文本中的 secret value 做 masking。
- 架构测试禁止 PluginHost、Plugin.Abstractions、官方插件互相越层依赖。
- 脚本检查官方插件示例不得使用 `runtime-only`、不得把 entry 放入项目配置、不得出现 `.bukit/plugins` 或 `site.externalPlugins`。

### 10.2 当前安全边界的限制

当前仍需清楚说明的限制：

- 没有 OS 级文件系统 sandbox。
  - 插件一旦作为进程启动，Host 主要通过配置/manifest/路径/报告治理，而不是系统调用拦截。

- `network` 不是强制网络隔离。
  - `network=false` 表示未授权声明，但当前代码没有建立网络 namespace、防火墙或 socket 拦截。

- fileSystem 权限是声明和准入校验。
  - Host 校验插件声明的 required permissions 是否被项目授予，但不拦截插件进程内任意文件 API。

- artifact path 校验只覆盖插件返回的 artifact metadata。
  - 它不能证明插件实际写入一定只发生在 artifact path 中。

- execution report 是审计记录，不是阻断机制。

这些限制不代表当前实现错误；它们定义了 v1 的真实安全等级。

## 11. 文档与实现一致性风险

当前主线文档大体已经指向外部进程协议，但仍能在部分历史/需求/迁移文档中看到旧方向或未来方向用语，例如：

- WASM 插件
- 动态 DLL 插件
- `site.externalPlugins`
- 旧式 runtime 插件
- 尚未实现的插件市场、远程安装、热加载、签名服务等

当前代码和测试的真实状态是：

- 不恢复 `site.externalPlugins`。
- 不恢复 runtime DLL 插件。
- 不使用 WASM 插件作为当前正式机制。
- 不把插件入口放入 `.bukit/`。
- 不让 Core 引用插件实现。
- 不让 PluginHost 引用 Engine/CLI/Labs/官方插件实现。
- 正式插件走 `plugins/<id>/plugin.yaml` + `.bukit/plugins.yaml` + `bukit-plugin-v1` 进程协议。

因此，阅读旧文档或历史技能时必须以当前 `src/Bukit-Core/Bukit.PluginHost`、`src/Bukit-Core/Bukit.Plugin.Abstractions`、`src/Bukit-Core/Bukit.Cli/Cli/PluginCliLoader.cs`、`docs/plugins/*` 主线文件和测试为准。

## 12. 审计发现

### 12.1 P0/P1 缺陷

未发现当前外部进程插件主链路存在 P0/P1 级别的代码边界破坏证据。

依据：

- PluginHost 依赖边界受架构测试保护。
- Plugin.Abstractions 不依赖 Bukit 运行时项目。
- 官方进程插件不依赖 Host、CLI、Engine、Labs。
- CLI 插件命令不能覆盖 Core 命令。
- Host 对协议、平台、hash、权限、路径、requestId、artifact path、超时和输出限制均有校验。

### 12.2 重要边界风险：外部插件能力容易被误解

风险级别：中

问题：

- 代码中同时存在 Core 内置构建插件接口和外部进程插件协议。
- 文档中也存在历史设计、迁移计划和未来方向。
- 如果把 `IBukitPlugin`、`BuildContext`、`ISectionPlugin` 当成外部进程插件 SDK，会得出错误架构判断。

当前真实结论：

- 外部进程插件当前是 CLI 命令插件。
- Core 内置插件当前是仓库内部构建 hook。
- section plugin 当前是渲染内部扩展点。

建议：

- 后续文档继续使用“外部进程插件”和“Core 内置插件”两套术语。
- 若未来要让外部插件参与构建 hook，应扩展 `bukit-plugin-v1` 或定义 v2 协议，而不是直接暴露内部 `BuildContext`。

### 12.3 重要安全限制：权限模型不是 OS sandbox

风险级别：中

问题：

- 当前权限模型能阻止未授权声明通过装载。
- 当前权限模型不能在进程运行后拦截任意文件或网络访问。

影响：

- 对“不可信第三方插件”的安全强度不能按 sandbox 宣称。
- 当前更适合“项目显式安装并信任的本地插件包”。

建议：

- 文档中持续明确“声明式权限 + 审计”，不要描述成完整沙箱。
- 若未来支持远程插件市场或不可信插件，必须新增 OS sandbox、容器、权限代理或签名/来源信任链。

### 12.4 当前正式机制未提供 build hook 给外部插件

风险级别：中

问题：

- 外部插件无法通过 v1 协议派生页面、修改渲染上下文、参与 after-build 或注册 section hook。

影响：

- 如果用户预期“插件系统可以扩展构建过程”，当前只能通过内置插件实现，不能通过外部进程插件实现。

建议：

- 将 v1 定位为 CLI command plugin protocol。
- 将 build-time plugin capability 放入单独路线图和协议版本管理。

### 12.5 官方插件示例 manifest 使用 sha256 占位值

风险级别：低到中

问题：

- Import 示例 `plugin.yaml` 的 `sha256` 是全零占位。
- Host 真实执行会校验 hash，因此该示例不能直接作为真实可执行包运行。

影响：

- 对阅读者可能造成“配置完整即可运行”的误解。

建议：

- 发布/打包文档中明确示例 hash 是占位，真实包必须由 release 流程写入实际 sha256。
- release asset 验证继续覆盖真实 hash。

### 12.6 历史文档陈旧风险

风险级别：低到中

问题：

- 部分文档仍保留旧机制或未来机制的描述。
- 对审计者和后续 agent 容易造成路径和机制混淆。

建议：

- 审计或开发时以当前代码、schema、测试和主线 `docs/plugins` 为准。
- 不把 `guide-0.1/`、`scripts-0.1/` 当成当前行为依据。

## 13. 已执行验证

本次审计执行了以下验证命令，均通过：

1. `dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj --no-restore -p:RunAnalyzers=false`
   - 结果：通过，8/8。

2. `dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj --no-restore -p:RunAnalyzers=false`
   - 结果：通过，164/164。

3. `dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj --no-restore -p:RunAnalyzers=false`
   - 结果：通过，16/16。

4. `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj --no-restore -p:RunAnalyzers=false --filter PluginBoundaryTests`
   - 结果：通过，12/12。

5. `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --no-restore -p:RunAnalyzers=false --filter PluginCliIntegrationTests`
   - 结果：通过，36/36。

6. `bash scripts/checks/official-plugin-packages.sh`
   - 结果：`Official plugin package configs OK`。

7. `dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj --no-restore -p:RunAnalyzers=false --filter FullyQualifiedName~Plugin`
   - 结果：通过，7/7。

8. `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --no-restore -p:RunAnalyzers=false --filter FullyQualifiedName~Plugin`
   - 结果：通过，110/110。

说明：

- 这些是插件系统相关 targeted verification。
- 本次任务是只读审计并输出报告，未运行完整仓库 gate。

## 14. 最终判断

Bukit Core 当前插件系统的实际形态可以概括为：

1. 外部插件协议是稳定收敛中的 `bukit-plugin-v1` 进程协议。
2. Host 对项目配置、静态 manifest、runtime manifest、平台入口、hash、权限、CI、命令暴露和协议响应做多层校验。
3. CLI 插件能力当前聚焦于命令扩展。
4. Core 内置插件仍然承担构建时扩展点，但只在 Core 内部注册运行。
5. section plugin/context contributor 属于渲染内部扩展点，不是正式外部协议。
6. 当前没有发现外部插件主链路越层依赖或旧 `site.externalPlugins` 机制被重新接回 Core 的证据。
7. 当前最大需要持续澄清的不是代码缺陷，而是“外部进程插件协议”和“Core 内置构建插件接口”的边界。

因此，本次审计结论是：**当前 Bukit 插件系统主线架构边界基本清晰，正式外部插件协议已形成以 `bukit-plugin-v1` 为核心的 CLI 命令插件机制；但它还不是 build-time plugin SDK，也不是完整安全沙箱。后续若要扩展到构建 hook 或不可信插件市场，需要新增协议能力和更强运行时隔离，而不能直接复用内部 `BuildContext` 或旧 `site.externalPlugins` 方向。**
