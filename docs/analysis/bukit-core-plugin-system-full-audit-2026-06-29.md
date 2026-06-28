# Bukit Core 插件系统全方位严格审计报告

审计日期：2026-06-29

审计范围：Bukit Core 当前主线代码中的插件系统、插件协议、PluginHost、Plugin.Abstractions、CLI 接入路径、Engine 内置插件边界、历史/实验遗留扩展点、正式插件可调用接口与不可调用边界。

审计约束：本报告只审计并生成文档，不修改插件系统代码，不删除代码，不调整配置，不运行会改变构建产物或执行插件的命令。

## 1. 审计目标

本次审计回答以下问题：

1. Bukit Core 当前正式插件系统是什么。
2. Bukit 插件协议是什么，插件必须如何实现协议。
3. 插件系统为插件提供了哪些接口，每个接口的字段、作用、安全边界分别是什么。
4. PluginHost 在整个系统中承担什么职责。
5. Plugin.Abstractions 在整个系统中承担什么职责。
6. 外部插件是否可以直接调用 Core 代码。
7. Engine 内置插件、外部进程插件、历史 experimental 插件路径之间是否存在边界混淆。
8. 当前代码中哪些分层必须保留，哪些分层或旧路径不应再被当作正式插件系统开放。
9. 插件系统从 CLI 命令进入到进程执行、响应校验、报告输出的完整运行机制是什么。

## 2. 核心结论

Bukit 当前正式插件系统的正确调用链是：

```text
Bukit.Cli
  -> Bukit.PluginHost
      -> external process plugin under plugins/<id>/...
```

这个调用链的含义是：

1. `Bukit.Cli` 只负责命令入口、Core 命令优先级、插件命令描述符组合。
2. `Bukit.PluginHost` 负责插件配置读取、路径校验、manifest 校验、hash 校验、CI 策略、权限校验、协议请求、进程执行、响应校验、执行报告。
3. 正式外部插件必须是独立进程，必须实现 `bukit-plugin-v1` JSON 协议。
4. 插件不能直接引用或调用 `Bukit.Cli`、`Bukit.Engine`、`Bukit.PluginHost` 内部实现。
5. `Bukit.Plugin.Abstractions` 不是 Core SDK，它只是协议 DTO 与 JSON source generation 支撑包。
6. Engine 内置插件不是第三方插件协议，它们是 Core 内部构建流水线实现。
7. `site.externalPlugins`、动态 DLL、`Assembly.LoadFrom`、in-process 第三方插件、WASM、Docker、marketplace 自动下载都不是当前正式 v1 插件系统能力。

本次审计认为：当前正式主线路径整体符合“除 Core 内置插件外，外部插件严禁调用 Core 代码；所有插件必须按照插件协议进行开发调用”的架构要求。主要风险不在 PluginHost 主路径本身，而在历史遗留概念和 in-process 扩展点可能被误用，包括 `src/Bukit.Engine.Abstractions/Plugins/ISectionPlugin.cs`、`src/Bukit.Rendering/Scriban/ITemplateContextContributor.cs`、`experimental/Bukit.Labs.Protocol/EngineProtocol/ExternalProtocolPluginSource.cs`。

## 3. 术语和边界定义

### 3.1 Core 内置 Engine 插件

Core 内置 Engine 插件位于 `src/Bukit.Engine/Plugins/PluginRegistry.cs`。

当前 `BuiltInPluginSource` 返回的内置插件包括：

1. `DataFilesPlugin`
2. `PagesIndexPlugin`
3. `TaxonomyPlugin`
4. `PaginationPlugin`
5. `ArchivePlugin`
6. `RelatedContentPlugin`
7. `AliasPlugin`
8. `MenuPlugin`
9. `ImageProcessingPlugin`

这些插件由 Core 直接实例化、直接运行，属于构建流水线内部能力。它们不是第三方插件，不需要走 `bukit-plugin-v1`，也不能作为外部插件开发模型的依据。

边界判定：

1. Core 内置插件可以访问 Engine 内部上下文，因为它们是 Core 的一部分。
2. 第三方插件不允许复用这种 in-process 模型。
3. 外部进程插件不应插入 `PluginRegistry.GetAllPlugins`。
4. `site.plugins` 控制的是 Engine 内置插件开关，不等价于 `.bukit/plugins.yaml` 控制的外部进程插件。

### 3.2 外部进程插件

外部进程插件是当前正式插件协议承认的第三方/官方扩展模型。

它必须满足：

1. 插件程序是独立 executable。
2. 插件包位于项目内 `plugins/<id>`。
3. 插件包包含 `plugin.yaml`。
4. `.bukit/plugins.yaml` 显式启用并授权插件。
5. 插件实现 `handshake`、`manifest`、`invoke` 三类 JSON 协议请求。
6. 插件只通过 stdin/stdout/stderr/exit code 与 Host 交互。
7. 插件不通过 C# 进程内引用调用 Core。

### 3.3 PluginHost

`src/Bukit.PluginHost/` 是外部进程插件系统的 Host 层。它不是插件 SDK，也不是插件业务 API。它是 Core 侧的执行边界和校验边界。

PluginHost 的核心职责是：

1. 读取 `.bukit/plugins.yaml`。
2. 读取 `plugins/<id>/plugin.yaml`。
3. 校验插件 source。
4. 校验插件 entry。
5. 校验插件 ID、协议、kind、distribution。
6. 校验当前平台入口。
7. 校验 sha256。
8. 校验 CI 策略。
9. 校验权限。
10. 执行 handshake。
11. 执行 manifest。
12. 执行 invoke。
13. 校验 JSON 响应。
14. 校验 requestId。
15. 校验 protocol。
16. 校验插件身份。
17. 校验 artifact path。
18. 控制 timeout。
19. 控制 stdout/stderr/response 大小。
20. 清空并重建 allowlisted environment。
21. 写 `.bukit/plugins.lock.yaml`。
22. 写 `.bukit/reports/plugin-executions/*.json`。

### 3.4 Plugin.Abstractions

`src/Bukit.Plugin.Abstractions/` 是协议模型包。它包含 C# record DTO、协议常量和 JSON source generator context。

它的职责是：

1. 固化协议常量，例如 `bukit-plugin-v1`、`handshake`、`manifest`、`invoke`。
2. 固化 manifest/config/request/response/runtime/security/result 模型。
3. 为官方插件或测试探针提供类型安全的序列化模型。
4. 降低手写 JSON 漂移风险。

它不承担：

1. Core SDK 职责。
2. Engine SDK 职责。
3. 文件系统授权执行职责。
4. 插件生命周期管理职责。
5. 业务服务调用职责。
6. 插件调用 Core 内部对象的职责。

第三方插件不强制引用 `Bukit.Plugin.Abstractions`。第三方插件只要按 JSON 协议实现即可。

## 4. 正式插件协议

### 4.1 协议版本

正式协议版本是：

```text
bukit-plugin-v1
```

代码定义位置：

```text
src/Bukit.Plugin.Abstractions/Protocol/PluginProtocolConstants.cs
```

常量包括：

```text
ProtocolVersion = "bukit-plugin-v1"
Handshake = "handshake"
Manifest = "manifest"
Invoke = "invoke"
```

安全意义：

1. Host 可以拒绝未知协议版本。
2. 插件不能用旧协议或实验协议冒充正式插件。
3. Host 可以在 request 和 response 中双向校验协议版本。

### 4.2 协议传输方式

协议通过外部进程标准流传输：

1. Host 启动插件 executable。
2. Host 将 JSON request 写入 stdin。
3. 插件从 stdin 读取完整 JSON。
4. 插件执行请求。
5. 插件将 JSON response 写入 stdout。
6. 插件将日志写入 stderr。
7. 插件用 process exit code 表示进程级执行状态。

限制：

1. stdout 必须是协议 JSON。
2. stdout 不得混入日志。
3. stderr 可以包含日志，但 Host 会限制大小并写入报告。
4. Host 会限制 stdout 最大字节数。
5. Host 会限制 stderr 最大字节数。
6. Host 会限制 response 最大字节数。
7. Host 会在超时时 kill 整个进程树。
8. Host 不通过 shell 执行插件命令。

实际执行层在 `src/Bukit.PluginHost/SystemProcessRunner.cs`。其中 `UseShellExecute = false`、`CreateNoWindow = true`、`ArgumentList`、`Environment.Clear()`、`process.Kill(entireProcessTree: true)` 共同构成了当前 v1 的进程执行硬边界。

### 4.3 通用 request envelope

所有请求都具备通用 envelope。

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `type` | string | 请求类型，只允许 `handshake`、`manifest`、`invoke` |
| `protocol` | string | 协议版本，必须是 `bukit-plugin-v1` |
| `requestId` | string | 请求 ID，用于 response 关联 |
| `host.name` | string | Host 名称 |
| `host.version` | string | Host 版本 |
| `host.platform` | string | Host 当前运行平台 |

安全意义：

1. `type` 防止插件误处理请求。
2. `protocol` 防止协议版本错配。
3. `requestId` 防止 response 串包或复用旧响应。
4. `host.platform` 让插件知道当前平台，但不等于授权访问平台资源。

### 4.4 通用 response envelope

所有响应都具备通用 envelope。

字段：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `type` | string | 响应类型，必须匹配请求对应 response |
| `protocol` | string | 协议版本，必须是 `bukit-plugin-v1` |
| `requestId` | string | 必须与 request 一致 |
| `success` | bool | 协议层成功状态 |
| `error` | object/null | 失败错误 |
| `messages` | array/null | 普通消息 |
| `diagnostics` | array/null | 结构化诊断 |

Host 校验点：

1. response type 必须符合当前请求。
2. response protocol 必须是 `bukit-plugin-v1`。
3. response requestId 必须等于 request requestId。
4. success false 时必须有可解释的错误语义。
5. stdout 必须能反序列化为对应 response DTO。

## 5. handshake 接口

### 5.1 handshake 目的

`handshake` 是 Host 对插件执行的第一个协议请求，用于确认：

1. 插件能正常启动。
2. 插件能读取 stdin。
3. 插件能输出合法 JSON。
4. 插件实现了 `bukit-plugin-v1`。
5. 插件身份与 `plugin.yaml` 一致。
6. 插件版本与 `plugin.yaml` 一致。
7. 插件平台与 Host 解析的平台一致。
8. 插件基础 capabilities 可以被记录。

### 5.2 handshake request

模型：

```text
PluginHandshakeRequest(
  Type,
  Protocol,
  RequestId,
  Host
)
```

字段解释：

| 字段 | 来源 | 作用 |
| --- | --- | --- |
| `Type` | Host | 固定为 `handshake` |
| `Protocol` | Host | 固定为 `bukit-plugin-v1` |
| `RequestId` | Host | 单次请求唯一 ID |
| `Host` | Host | 包含 host name/version/platform |

插件必须读取该 request，并返回 handshake response。

### 5.3 handshake response

模型：

```text
PluginHandshakeResponse(
  Type,
  Protocol,
  RequestId,
  Success,
  Plugin,
  Error,
  Messages,
  Diagnostics
)
```

`Plugin` 身份模型：

```text
PluginIdentity(
  Id,
  Name,
  Version,
  Platform,
  Capabilities
)
```

Host 校验：

1. `Type` 必须是 handshake response 类型。
2. `Protocol` 必须是 `bukit-plugin-v1`。
3. `RequestId` 必须匹配 request。
4. `Success` 必须为 true 才进入下一步。
5. `Plugin` 不能为空。
6. `Plugin.Id` 必须等于 `.bukit/plugins.yaml` 中的 plugin key。
7. `Plugin.Id` 必须等于 `plugin.yaml` 中的 id。
8. `Plugin.Version` 必须等于 `plugin.yaml` 中的 version。
9. `Plugin.Platform` 必须等于 Host 当前选择的平台。

安全意义：

1. 防止 source 指向错误插件。
2. 防止 manifest 声明与实际 executable 不一致。
3. 防止替换二进制后仍假装是原插件。
4. 为后续 manifest 和 invoke 提供身份基线。

## 6. manifest 接口

### 6.1 manifest 目的

`manifest` 是 Host 对插件执行的第二个协议请求，用于获取运行时命令声明、运行时 requiredPermissions 和 capabilities。

它的设计目标不是让插件无限动态扩展命令，而是在默认 `static` 策略下验证 runtime manifest 没有超出 `plugin.yaml` 静态 manifest。

### 6.2 manifest request

模型：

```text
PluginManifestRequest(
  Type,
  Protocol,
  RequestId,
  Host
)
```

字段：

| 字段 | 作用 |
| --- | --- |
| `Type` | 固定为 `manifest` |
| `Protocol` | 固定为 `bukit-plugin-v1` |
| `RequestId` | 请求关联 |
| `Host` | Host 基础信息 |

### 6.3 manifest response

模型：

```text
PluginManifestResponse(
  Type,
  Protocol,
  RequestId,
  Success,
  Capabilities,
  Commands,
  RequiredPermissions,
  Error,
  Messages,
  Diagnostics
)
```

字段作用：

| 字段 | 作用 |
| --- | --- |
| `Capabilities` | 插件能力标签，用于 Host 和报告识别 |
| `Commands` | 插件运行时命令列表 |
| `RequiredPermissions` | 插件运行时声明所需权限 |
| `Error` | manifest 失败错误 |
| `Messages` | 普通消息 |
| `Diagnostics` | 结构化诊断 |

Host 校验：

1. response envelope 必须合法。
2. `Commands` 不能为空。
3. `RequiredPermissions` 不能超过 `.bukit/plugins.yaml` 授予权限。
4. 默认 `manifestPolicy: static` 时，runtime commands 必须是 `plugin.yaml` commands 的子集或等价集。
5. runtime command 不得增加静态 manifest 中不存在的 command。
6. runtime command 不得增加静态 manifest 中不存在的 alias。
7. runtime command 不得增加静态 manifest 中不存在的 argument。
8. runtime command 不得改变 argument required 语义。
9. runtime command 不得增加静态 manifest 中不存在的 option。
10. runtime command 不得改变 option type。
11. runtime command 不得改变 option required 语义。
12. runtime subcommands 也必须递归遵守静态约束。

### 6.4 command spec

模型：

```text
PluginCommandSpec(
  Name,
  Description,
  Aliases,
  Arguments,
  Options,
  Subcommands
)
```

字段作用：

| 字段 | 作用 | 风险边界 |
| --- | --- | --- |
| `Name` | CLI 命令名 | 不能覆盖 Core 命令，不能与其他插件冲突 |
| `Description` | 命令说明 | 仅展示，不应承载执行逻辑 |
| `Aliases` | 命令别名 | alias 也必须参与冲突检查 |
| `Arguments` | 位置参数 | 由 CLI binder 转换为 invoke arguments |
| `Options` | 命令选项 | 由 CLI binder 转换为 invoke options |
| `Subcommands` | 子命令 | 递归注册和校验 |

### 6.5 option spec

模型：

```text
PluginOptionSpec(
  Name,
  Type,
  Description,
  Required,
  ValueName,
  AllowedValues,
  ConflictWith
)
```

支持类型：

1. `string`
2. `integer`
3. `number`
4. `boolean`
5. `flag`

安全意义：

1. CLI 可以在进入插件前进行基本参数绑定。
2. Host 可以确保 runtime manifest 不改变静态 option 类型。
3. `allowedValues` 可以减少插件收到任意字符串的范围。
4. `conflictWith` 用于描述互斥选项，但仍应由插件自身执行最终业务校验。

### 6.6 argument spec

模型：

```text
PluginArgumentSpec(
  Name,
  Description,
  Required
)
```

作用：

1. 描述位置参数。
2. 让 CLI binder 知道参数是否必填。
3. 让 usage/help 能展示参数。
4. 作为 static manifest 和 runtime manifest 对齐的校验单位。

## 7. invoke 接口

### 7.1 invoke 目的

`invoke` 是插件系统真正执行命令的协议请求。

`handshake` 和 `manifest` 只完成身份与命令面验证，`invoke` 才会让插件执行用户请求的操作。

### 7.2 invoke request

模型：

```text
PluginInvokeRequest(
  Type,
  Protocol,
  RequestId,
  Host,
  Command,
  Context,
  Permissions
)
```

字段作用：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `Type` | string | 固定为 `invoke` |
| `Protocol` | string | 固定为 `bukit-plugin-v1` |
| `RequestId` | string | 请求关联 |
| `Host` | object | Host 信息 |
| `Command` | object | 用户执行的命令、子命令、参数、选项 |
| `Context` | object | 项目上下文 |
| `Permissions` | object | Host 授予权限 |

### 7.3 command runtime object

模型：

```text
PluginInvokeCommand(
  Name,
  Path,
  Arguments,
  Options
)
```

字段：

| 字段 | 作用 |
| --- | --- |
| `Name` | 顶层命令名 |
| `Path` | 子命令路径 |
| `Arguments` | 位置参数值 |
| `Options` | 选项值 |

安全边界：

1. 插件只能收到 CLI 绑定后的参数对象。
2. 插件不能通过该对象获得 Core command descriptor。
3. 插件不能通过该对象获得 Core service。
4. 插件仍需要自行进行业务级参数验证。

### 7.4 invoke context

模型：

```text
PluginInvokeContext(
  RootDir,
  WorkingDir,
  ConfigPath,
  OutputDir,
  Environment
)
```

字段作用：

| 字段 | 作用 | 安全说明 |
| --- | --- | --- |
| `RootDir` | 项目根目录 | 插件路径判断应以此为边界 |
| `WorkingDir` | 当前工作目录 | 不等于任意文件写权限 |
| `ConfigPath` | 配置文件路径 | 只说明配置位置，不自动授权读取 |
| `OutputDir` | 输出目录 | 只说明输出位置，不自动授权写入 |
| `Environment` | allowlisted 环境变量 | Host 只传被授权读取的变量 |

### 7.5 invoke permissions

`Permissions` 是 Host 实际授予插件的权限，不是插件自己要求的权限。

插件必须以 request 中的 `Permissions` 为准，不能假设 `plugin.yaml` 声明的权限全部被授予。

### 7.6 invoke response

模型：

```text
PluginInvokeResponse(
  Type,
  Protocol,
  RequestId,
  Success,
  ExitCode,
  Error,
  Messages,
  Diagnostics,
  Artifacts
)
```

字段作用：

| 字段 | 作用 |
| --- | --- |
| `Success` | 插件协议层是否成功 |
| `ExitCode` | CLI 最终可返回的执行码 |
| `Error` | 失败错误 |
| `Messages` | 普通消息 |
| `Diagnostics` | 结构化诊断 |
| `Artifacts` | 插件产物列表 |

Host 校验：

1. response envelope 必须合法。
2. `ExitCode` 会影响最终 CLI 返回。
3. artifact path 必须合法。
4. artifact path 不能是空。
5. artifact path 不能是绝对路径。
6. artifact path 不能是 Windows 绝对路径。
7. artifact path 不能包含 `.` 或 `..` 越界段。

## 8. 权限接口

### 8.1 权限模型

模型：

```text
PluginPermissionSet(
  FileSystem,
  Network,
  Environment
)
```

子模型：

```text
PluginFileSystemPermission(
  Read,
  Write
)

PluginEnvironmentPermission(
  Read
)
```

### 8.2 fileSystem.read

作用：

1. 声明插件允许读取的项目相对路径。
2. 约束插件需要访问的输入范围。
3. 用于 Host 校验 `requiredPermissions <= grantedPermissions`。

规则：

1. 必须是相对路径。
2. 不允许绝对路径。
3. 不允许路径穿越。
4. 不允许通过 symlink/realpath 逃出项目边界。
5. `.bukit` 是敏感目录，不应被普通插件读取。

### 8.3 fileSystem.write

作用：

1. 声明插件允许写入的项目相对路径。
2. 控制插件输出范围。
3. 防止插件写入 Core 配置、Host 报告、lock、敏感目录。

规则：

1. 必须是相对路径。
2. 不允许绝对路径。
3. 不允许路径穿越。
4. 插件自定义输出只能写入 `.bukit/reports/plugin-output/<plugin-id>/`。
5. 插件临时文件只能写入 `.bukit/tmp/<plugin-id>/`。
6. `.bukit/reports/plugin-executions/` 由 Core PluginHost 写，不由插件写。

### 8.4 network

作用：

1. 声明插件是否需要网络访问。
2. 让 Host、CI、审计报告可以识别插件网络需求。

边界：

1. v1 的 `network` 是声明式权限。
2. v1 尚不是完整 OS 级网络沙箱。
3. 插件即使声明 network，也不应该获得 Core 内部 HTTP client 或 credential。

### 8.5 environment.read

作用：

1. 声明插件需要读取哪些环境变量。
2. Host 只传 allowlist 中的变量。
3. 执行报告中应 mask 环境变量值。

规则：

1. 必须显式列出变量名。
2. 不允许 wildcard。
3. Host 启动插件前会 `Environment.Clear()`。
4. Host 再按 allowlist 重建环境变量。
5. 插件不能默认继承父进程完整环境。

### 8.6 requiredPermissions <= grantedPermissions

权限关系：

```text
plugin.yaml requiredPermissions
  <= .bukit/plugins.yaml granted permissions
```

以及：

```text
runtime manifest requiredPermissions
  <= .bukit/plugins.yaml granted permissions
```

安全意义：

1. 插件包不能单方面扩大权限。
2. 插件运行时 manifest 不能临时扩大权限。
3. 项目维护者通过 `.bukit/plugins.yaml` 控制最终授权。
4. Host 在加载阶段拒绝权限越界插件。

## 9. 配置接口：`.bukit/plugins.yaml`

`.bukit/plugins.yaml` 是项目级插件控制平面。

它不属于 `site.yaml`。它不控制站点内容源、主题、构建输出、taxonomy、deploy、logging 或 Core 内置 Engine 插件。

### 9.1 顶层字段

模型：

```text
PluginHostConfig(
  Version,
  Plugins
)
```

字段：

| 字段 | 作用 |
| --- | --- |
| `version` | 配置版本，当前要求为 1 |
| `plugins` | 插件配置 map |

### 9.2 单个插件配置

模型：

```text
PluginConfigEntry(
  Enabled,
  Source,
  ExposeCommands,
  Permissions,
  Timeout,
  Output,
  FailMode,
  AllowInCi,
  Description,
  PermissionsExplicit,
  ExposeCommandsDeclared,
  ManifestPolicy
)
```

字段详解：

| 字段 | 作用 | 安全意义 |
| --- | --- | --- |
| `enabled` | 是否启用插件 | disabled 时不执行插件进程 |
| `source` | 插件目录 | 必须是 `plugins/<id>` |
| `exposeCommands` | 暴露到 CLI 的命令 | 必须显式声明，不能缺省 |
| `permissions` | 授予权限 | 控制插件可访问资源 |
| `timeout` | 超时设置 | 防止插件 hang |
| `output` | 输出大小限制 | 防止 stdout/stderr/response 爆量 |
| `failMode` | 失败策略 | 默认 strict |
| `allowInCi` | 是否允许 CI 执行 | CI 安全开关 |
| `description` | 描述 | 人类可读信息 |
| `manifestPolicy` | manifest 策略 | 默认 static，runtime-only 仅限特殊场景 |

### 9.3 enabled

`enabled: false` 的含义不是删除插件配置，而是插件不应执行。

如果 disabled 插件仍声明了 `exposeCommands`，Core 应保留 disabled command diagnostic。这种设计比直接 unknown command 更清晰，因为用户能知道命令存在但插件被禁用。

### 9.4 source

`source` 必须满足：

1. 是相对路径。
2. 以 `plugins/` 开头。
3. 指向 `plugins/<id>`。
4. 不包含 `..`。
5. 不指向 `.bukit`。
6. realpath 不能逃出 `plugins/`。

安全意义：

1. 防止执行项目外二进制。
2. 防止通过 symlink 指向任意位置。
3. 防止把 `.bukit` 作为插件程序目录。

### 9.5 exposeCommands

`exposeCommands` 是插件命令面真正暴露到 Bukit CLI 的开关。

规则：

1. 必须显式声明。
2. 可以为空数组。
3. 不能缺失。
4. 每个命令必须存在于 plugin manifest commands。
5. 命令不能覆盖 Core 命令。
6. 命令不能与其他插件命令冲突。
7. alias 不能与 Core 命令或其他插件命令冲突。

安全意义：

1. 插件即使在 manifest 中声明多个命令，也只有被项目配置显式暴露的命令可用。
2. 项目维护者拥有最终命令面控制权。
3. 避免插件通过 runtime manifest 悄悄增加 CLI surface。

### 9.6 manifestPolicy

支持值：

| 值 | 含义 | 审计结论 |
| --- | --- | --- |
| `static` | 以 `plugin.yaml` 静态命令为准，runtime manifest 不能扩大命令面 | 正式插件必须使用 |
| `runtime-only` | 允许 runtime manifest 决定命令面 | 仅限 dev/Labs/兼容/临时动态插件 |

`runtime-only` 是当前风险最高的配置例外。它不应作为官方正式插件默认策略，也不应进入 release 插件包。

## 10. 插件包 manifest：`plugins/<id>/plugin.yaml`

`plugin.yaml` 是插件包自身 manifest，由插件作者维护。

### 10.1 顶层字段

模型：

```text
PluginManifest(
  Id,
  Name,
  Version,
  Protocol,
  Kind,
  Distribution,
  Platforms,
  Commands,
  RequiredPermissions
)
```

字段详解：

| 字段 | 作用 | Host 校验 |
| --- | --- | --- |
| `id` | 插件 ID | 必须合法，必须与 `.bukit/plugins.yaml` key 一致 |
| `name` | 插件名称 | 用于展示和报告 |
| `version` | 插件版本 | handshake 必须返回同版本 |
| `protocol` | 协议版本 | 必须是 `bukit-plugin-v1` |
| `kind` | 插件类型 | 必须是 `process` |
| `distribution` | 分发形态 | 当前必须是 `self-contained` |
| `platforms` | 平台入口 map | 当前平台必须可解析 |
| `commands` | 静态命令声明 | static policy 下必须非空 |
| `requiredPermissions` | 插件所需权限 | 必须小于等于项目授予权限 |

### 10.2 kind

当前正式值只接受：

```text
process
```

不接受：

1. `dll`
2. `wasm`
3. `docker`
4. `script` 作为独立 kind
5. `in-process`

### 10.3 distribution

当前正式值只接受：

```text
self-contained
```

这意味着插件包必须提供可执行程序及其运行所需依赖，Host 不负责运行时依赖解析。

### 10.4 platforms

`platforms` 声明不同运行平台的入口。

每个平台 entry 必须包含：

1. `entry`
2. `sha256`

Host 工作：

1. 根据当前 RID 选择平台。
2. 拼接 source 和 entry。
3. 校验 entry path。
4. 计算 sha256。
5. 与 manifest sha256 比对。

安全意义：

1. 防止平台入口缺失。
2. 防止二进制被替换。
3. 防止 entry 指向 `.bukit` 或项目外路径。

## 11. CLI 接入机制

### 11.1 Core 命令优先

`Bukit.Cli` 的命令解析策略是 Core 命令优先。插件命令只在 Core 描述符之后组合。

风险边界：

1. 插件不能覆盖 `build`、`doctor`、`deploy` 等 Core 命令。
2. 插件不能通过 alias 覆盖 Core 命令。
3. 插件不能让用户误以为执行的是 Core 命令。

### 11.2 PluginCliLoader

`src/Bukit.Cli/Cli/PluginCliLoader.cs` 是 CLI 到 PluginHost 的桥接层。

主要步骤：

1. `CreateDefault()` 组装 PluginHost 相关依赖。
2. `LoadAsync()` 读取插件配置。
3. 添加 `plugin` 管理命令。
4. 遍历 `.bukit/plugins.yaml` 中的插件配置。
5. 对 disabled 插件创建 disabled command descriptor。
6. 对 enabled 插件执行 source 校验。
7. 加载 `plugin.yaml`。
8. 校验 manifest identity。
9. 校验 static manifest commands。
10. 校验权限。
11. 解析平台。
12. 校验 entry path。
13. 校验 hash。
14. 校验 CI policy。
15. 创建 resolved plugin。
16. 执行 handshake。
17. 执行 manifest。
18. 校验 runtime commands。
19. 校验 runtime requiredPermissions。
20. 选择 exposed commands。
21. 创建 plugin command descriptor。
22. 写 lock file。

审计重点：

1. CLI 没有直接执行插件业务。
2. CLI 没有直接加载插件程序集。
3. CLI 通过 PluginHost 组件完成校验和协议执行。

### 11.3 PluginCommandDescriptorFactory

该层把 `PluginCommandSpec` 转换为 Bukit CLI command descriptor。

职责：

1. 转换 command name。
2. 转换 description。
3. 转换 arguments。
4. 转换 options。
5. 转换 subcommands。
6. 对 enabled 插件命令绑定 `PluginCommandInvoker`。
7. 对 disabled 插件命令返回 disabled diagnostic。

边界：

1. 它只做 CLI descriptor 映射。
2. 它不执行插件业务。
3. 它不赋予插件额外权限。

### 11.4 PluginCommandInvoker

该层把用户绑定后的 CLI 命令转换为 `PluginInvokeRequest`。

职责：

1. 收集命令名。
2. 收集子命令路径。
3. 收集 arguments。
4. 收集 options。
5. 构造 invoke context。
6. 调用 `IPluginProtocolClient.InvokeAsync()`。
7. 返回插件 response 中的 exit code。

边界：

1. 它不直接启动进程。
2. 它不解析插件 manifest。
3. 它不绕过 PluginHost。

### 11.5 BukitCliComposer

该层组合 Core descriptors 和 plugin descriptors。

职责：

1. Core descriptors 先进入命令表。
2. Plugin descriptors 后进入命令表。
3. 检查 command name 冲突。
4. 检查 alias 冲突。
5. 拒绝重复命令 key。

安全意义：

1. 保证 Core 命令优先。
2. 保证插件之间不能互相覆盖。
3. 保证 alias 不成为绕过点。

## 12. PluginHost 内部机制

### 12.1 PluginConfigLoader

职责：

1. 查找 `.bukit/plugins.yaml`。
2. 文件不存在时返回空配置。
3. 校验 `version`。
4. 解析每个 plugin entry。
5. 解析 enabled。
6. 解析 source。
7. 解析 exposeCommands。
8. 标记 exposeCommands 是否显式声明。
9. 解析 permissions。
10. 标记 permissions 是否显式声明。
11. 解析 timeout。
12. 解析 output limits。
13. 解析 failMode。
14. 解析 allowInCi。
15. 解析 manifestPolicy。
16. 校验 permission paths。
17. 校验 environment wildcard。

安全意义：

1. 配置缺失不等于启用插件。
2. 权限必须经过 Host 解析。
3. `exposeCommands` 缺失应被拒绝。
4. permissions 显式声明是 CI 安全要求的一部分。

### 12.2 PluginManifestLoader

职责：

1. 读取 `plugins/<id>/plugin.yaml`。
2. 解析 id。
3. 解析 name。
4. 解析 version。
5. 解析 protocol。
6. 解析 kind。
7. 解析 distribution。
8. 校验 `protocol == bukit-plugin-v1`。
9. 校验 `kind == process`。
10. 校验 `distribution == self-contained`。
11. 解析 platforms。
12. 解析 commands。
13. 解析 requiredPermissions。
14. 校验 requiredPermissions paths。
15. 校验 environment wildcard。

安全意义：

1. 阻断错误协议。
2. 阻断 in-process 插件。
3. 阻断 runtime-dependent 插件绕过依赖解析。
4. 阻断 manifest 权限路径越界。

### 12.3 PluginPathValidator

职责：

1. 校验 plugin source。
2. 校验 plugin entry。
3. 规范化相对路径。
4. 拒绝绝对路径。
5. 拒绝 Windows 绝对路径。
6. 拒绝空路径。
7. 拒绝 `..`。
8. 校验 source 在 `plugins/` 下。
9. 校验 source realpath 在 `plugins/` 下。
10. 校验 entry 不在 `.bukit` 下。
11. 校验 entry realpath 不在 `.bukit` 下。

安全意义：

1. 防止 source 指向任意目录。
2. 防止 entry 指向任意可执行文件。
3. 防止 `.bukit` 变成插件程序存放地。
4. 防止 symlink 绕过相对路径检查。

### 12.4 PluginPlatformResolver

职责：

1. 识别当前平台/RID。
2. 从 manifest platforms 中选择匹配 entry。
3. 返回平台 entry 和 sha256。

安全意义：

1. 插件必须显式支持当前平台。
2. Host 不猜测可执行入口。
3. Host 不执行 manifest 未声明的平台文件。

### 12.5 PluginHashVerifier

职责：

1. 读取 entry 文件。
2. 计算 SHA-256。
3. 与 manifest 中的 sha256 比对。
4. 返回校验结果。

安全意义：

1. 防止插件 executable 被替换。
2. 支持 lock 和 report 的审计证据。
3. 支持 CI policy 判断。

### 12.6 PluginCiPolicy

CI 环境下的额外要求：

1. `allowInCi` 必须为 true。
2. sha256 必须校验通过。
3. permissions 必须显式声明。

安全意义：

1. CI 不应默认执行任意本地插件。
2. CI 不应执行未 hash 固定的插件。
3. CI 不应执行未明确授权的插件。

### 12.7 PluginPermissionEvaluator

职责：

1. 校验 granted permissions。
2. 校验 required permissions。
3. 判断 required 是否超出 granted。
4. 校验 fileSystem read/write。
5. 校验 network。
6. 校验 environment read。

安全意义：

1. 插件包 manifest 无权自行扩大授权。
2. runtime manifest 无权临时扩大授权。
3. Host 是权限关系的最终裁决者。

### 12.8 PluginCommandManifestValidator

职责：

1. 在 `static` 策略下比较 static commands 和 runtime commands。
2. 拒绝 runtime-only 新增 command。
3. 拒绝 runtime-only 新增 alias。
4. 拒绝 runtime-only 新增 argument。
5. 拒绝 runtime-only 新增 option。
6. 拒绝 option type 变化。
7. 拒绝 required 语义变化。
8. 递归校验 subcommands。

安全意义：

1. 防止插件在运行时扩大 CLI surface。
2. 防止静态审计通过后 runtime manifest 偷换命令。
3. 防止 `exposeCommands` 与真实 runtime command 面脱节。

### 12.9 PluginProtocolClient

职责：

1. 构造 handshake request。
2. 构造 manifest request。
3. 构造 invoke request。
4. 序列化 request。
5. 调用 process invoker。
6. 反序列化 response。
7. 校验 common response。
8. 校验 invoke response。
9. 校验 process 成功性。
10. 校验 stdout JSON。
11. 校验 protocol。
12. 校验 requestId。
13. 校验 artifact path。
14. 生成执行报告数据。
15. 在 finally 中写执行报告。

安全意义：

1. 所有协议交互集中在一处。
2. 所有响应结构校验集中在一处。
3. invoke 无论成功失败都可记录报告。
4. 插件 stdout 异常会被识别为协议错误。

### 12.10 PluginProcessInvoker 和 SystemProcessRunner

职责：

1. 接收 `PluginProcessRequest`。
2. 转换为 `ProcessRunRequest`。
3. 启动系统进程。
4. 写 stdin。
5. 读 stdout。
6. 读 stderr。
7. 限制 stdout bytes。
8. 限制 stderr bytes。
9. 应用 timeout。
10. 必要时 kill process tree。
11. 返回 exit code、stdout、stderr、timeout/output limit 状态。

关键安全属性：

1. 不使用 shell。
2. 不拼接 shell command。
3. 使用 `ArgumentList`。
4. 清空默认环境变量。
5. 只传入 allowlisted environment。
6. stdout/stderr 有大小限制。
7. 超时后 kill 整个进程树。

### 12.11 PluginLockFileWriter

职责：

1. 写 `.bukit/plugins.lock.yaml`。
2. 记录 resolved plugin。
3. 记录 source。
4. 记录 manifestVersion。
5. 记录 protocol。
6. 记录 platform。
7. 记录 entry。
8. 记录 sha256。
9. 记录 commands。
10. 记录 resolvedAt。
11. 记录 sha256Verified。

安全意义：

1. lock 是审计证据，不是权限绕过。
2. 下次执行仍必须重新校验 source、plugin.yaml、entry、sha256、protocol、permissions。
3. lock 支持可复现性和追踪 resolved 状态。

### 12.12 PluginExecutionReporter

职责：

1. 写 `.bukit/reports/plugin-executions/*.json`。
2. 记录插件 ID。
3. 记录 operation。
4. 记录 protocol。
5. 记录 executable。
6. 记录 sha256Verified。
7. 记录 startedAt/finishedAt。
8. 记录 duration。
9. 记录 exit code。
10. 记录 stdout/stderr bytes。
11. 记录 stderr。
12. 记录 response summary。
13. 记录 diagnostics。
14. 记录 artifacts。
15. 记录 masked environment。

安全意义：

1. 插件执行可审计。
2. stderr 不直接丢失。
3. 环境变量值应被 mask。
4. artifact 和 diagnostics 形成追踪依据。

## 13. Plugin.Abstractions 详细清单

### 13.1 Config 模型

#### PluginHostConfig

含义：`.bukit/plugins.yaml` 顶层模型。

字段：

1. `Version`
2. `Plugins`

作用：

1. 承载插件配置版本。
2. 承载所有插件配置 entry。
3. 被 PluginConfigLoader 解析。

#### PluginConfigEntry

含义：单个插件在 `.bukit/plugins.yaml` 中的授权和暴露配置。

字段：

1. `Enabled`
2. `Source`
3. `ExposeCommands`
4. `Permissions`
5. `Timeout`
6. `Output`
7. `FailMode`
8. `AllowInCi`
9. `Description`
10. `PermissionsExplicit`
11. `ExposeCommandsDeclared`
12. `ManifestPolicy`

作用：

1. 表达插件是否启用。
2. 表达插件包位置。
3. 表达暴露哪些命令。
4. 表达授予哪些权限。
5. 表达超时和输出限制。
6. 表达 CI 是否允许运行。
7. 表达 manifest policy。

#### PluginTimeoutOptions

默认：

1. `HandshakeMs = 5000`
2. `ManifestMs = 5000`
3. `InvokeMs = 120000`

作用：

1. 防止 handshake 卡死。
2. 防止 manifest 卡死。
3. 防止 invoke 长时间阻塞 CLI。

#### PluginOutputLimitOptions

默认：

1. `StdoutMaxBytes = 4194304`
2. `StderrMaxBytes = 4194304`
3. `ResponseMaxBytes = 4194304`

作用：

1. 防止 stdout 爆量。
2. 防止 stderr 爆量。
3. 防止 JSON response 过大。

### 13.2 Manifest 模型

#### PluginManifest

含义：`plugins/<id>/plugin.yaml` 的主模型。

字段：

1. `Id`
2. `Name`
3. `Version`
4. `Protocol`
5. `Kind`
6. `Distribution`
7. `Platforms`
8. `Commands`
9. `RequiredPermissions`

作用：

1. 声明插件身份。
2. 声明协议。
3. 声明执行形态。
4. 声明平台入口。
5. 声明命令面。
6. 声明所需权限。

#### PluginPlatformEntry

字段：

1. `Entry`
2. `Sha256`

作用：

1. 指定平台 executable。
2. 指定 executable 完整性 hash。

#### PluginCommandSpec

字段：

1. `Name`
2. `Description`
3. `Aliases`
4. `Arguments`
5. `Options`
6. `Subcommands`

作用：

1. 定义 CLI 命令形态。
2. 支撑 CLI descriptor 转换。
3. 支撑 static/runtime command 校验。

#### PluginOptionSpec

字段：

1. `Name`
2. `Type`
3. `Description`
4. `Required`
5. `ValueName`
6. `AllowedValues`
7. `ConflictWith`

作用：

1. 定义 CLI option。
2. 支撑参数绑定。
3. 支撑 option type 稳定性校验。

#### PluginArgumentSpec

字段：

1. `Name`
2. `Description`
3. `Required`

作用：

1. 定义 CLI positional argument。
2. 支撑 usage/help。
3. 支撑 static/runtime 对齐校验。

### 13.3 Protocol 模型

#### PluginRequestEnvelope

含义：通用请求 envelope。

字段：

1. `Type`
2. `Protocol`
3. `RequestId`
4. `Host`

#### PluginResponseEnvelope

含义：通用响应 envelope。

字段：

1. `Type`
2. `Protocol`
3. `RequestId`
4. `Success`
5. `Error`
6. `Messages`
7. `Diagnostics`

#### PluginHostInfo

字段：

1. `Name`
2. `Version`
3. `Platform`

作用：

1. 让插件知道 Host 基础信息。
2. 不授予 Core API。

#### PluginHandshakeRequest / PluginHandshakeResponse

作用：

1. 完成插件身份确认。
2. 完成平台确认。
3. 完成协议确认。

#### PluginManifestRequest / PluginManifestResponse

作用：

1. 获取 runtime command manifest。
2. 获取 runtime requiredPermissions。
3. 获取 capabilities。

#### PluginInvokeRequest / PluginInvokeResponse

作用：

1. 执行插件命令。
2. 传递命令绑定结果。
3. 传递上下文。
4. 传递授权权限。
5. 返回 exitCode、diagnostics、artifacts。

### 13.4 Runtime 模型

#### PluginInvokeCommand

字段：

1. `Name`
2. `Path`
3. `Arguments`
4. `Options`

作用：

1. 表达用户实际执行的命令。
2. 表达子命令路径。
3. 表达参数和选项。

#### PluginInvokeContext

字段：

1. `RootDir`
2. `WorkingDir`
3. `ConfigPath`
4. `OutputDir`
5. `Environment`

作用：

1. 提供项目上下文。
2. 提供受控环境变量。
3. 不提供 Core object。
4. 不提供 Engine object。

### 13.5 Security 模型

#### PluginPermissionSet

字段：

1. `FileSystem`
2. `Network`
3. `Environment`

作用：

1. 表达插件权限边界。
2. 用于权限比较。
3. 用于 invoke request 传递最终授权。

#### PluginFileSystemPermission

字段：

1. `Read`
2. `Write`

作用：

1. 表达读路径。
2. 表达写路径。

#### PluginEnvironmentPermission

字段：

1. `Read`

作用：

1. 表达可读取环境变量名。
2. 支撑 Host allowlist。

### 13.6 Result 模型

#### PluginMessage

字段：

1. `Level`
2. `Message`

作用：

1. 插件返回普通消息。
2. level 可用于展示或报告。

#### PluginDiagnostic

字段：

1. `Code`
2. `Severity`
3. `Message`
4. `Path`

作用：

1. 插件返回结构化诊断。
2. 可关联项目相对路径。

#### PluginArtifact

字段：

1. `Type`
2. `Path`
3. `Description`

作用：

1. 插件声明产物。
2. Host 校验 path。
3. 报告记录产物。

#### PluginError

字段：

1. `Code`
2. `Message`
3. `Details`

作用：

1. 插件返回结构化错误。
2. Host 可写入报告和诊断。

### 13.7 PluginJsonSerializerContext

`PluginJsonSerializerContext` 注册了 config、manifest、protocol、runtime、security、result 全部协议 DTO。

作用：

1. 支持 source-generated System.Text.Json。
2. 降低 trim/AOT 场景反射风险。
3. 降低官方插件与 Host 序列化漂移。
4. 让测试可以直接覆盖 DTO JSON 形态。

## 14. 完整运行时序

### 14.1 命令启动阶段

1. 用户执行 `bukit <command> ...`。
2. `Program.cs` 读取命令名。
3. CLI 先解析 Core command descriptors。
4. 如果命令是 Core 命令，直接执行 Core 命令。
5. 如果需要插件命令，调用 `PluginCliLoader.CreateDefault().LoadAsync(...)`。

### 14.2 插件配置加载阶段

1. `PluginConfigLoader` 查找 `.bukit/plugins.yaml`。
2. 如果不存在，返回空插件列表。
3. 如果存在，解析 `version`。
4. 解析每个 `plugins.<id>`。
5. 校验 source。
6. 校验 exposeCommands 是否声明。
7. 校验 permissions 是否有效。
8. 校验 timeout 和 output。
9. 校验 manifestPolicy。

### 14.3 disabled 插件处理阶段

1. 如果插件 `enabled: false`，Host 不执行插件 executable。
2. 如果该插件声明了 `exposeCommands`，CLI 创建 disabled descriptor。
3. 用户执行该命令时得到 disabled diagnostic。
4. 这避免把 disabled command 混淆为 unknown command。

### 14.4 source 和 manifest 加载阶段

1. `PluginPathValidator.ValidatePluginSource()` 校验 source。
2. `PluginManifestLoader.LoadAsync()` 加载 `plugin.yaml`。
3. 校验 manifest id。
4. 校验 protocol。
5. 校验 kind。
6. 校验 distribution。
7. 校验 static commands。
8. 校验 requiredPermissions。

### 14.5 平台解析和 executable 校验阶段

1. `PluginPlatformResolver` 根据当前平台选择 entry。
2. `PluginPathValidator.ValidatePluginEntry()` 校验 entry。
3. `PluginHashVerifier` 计算 sha256。
4. Host 比对 expected sha256。
5. `PluginCiPolicy` 在 CI 下执行额外校验。

### 14.6 resolved plugin 创建阶段

Host 创建 resolved plugin，并携带：

1. project root。
2. plugin id。
3. plugin version。
4. platform。
5. executable path。
6. expected sha256。
7. sha256 verified 状态。
8. granted permissions。
9. timeout。
10. output limits。
11. host info。
12. allowlisted environment。

### 14.7 handshake 阶段

1. Host 构造 handshake request。
2. Host 启动插件进程。
3. Host 写入 stdin。
4. 插件返回 stdout JSON。
5. Host 反序列化 handshake response。
6. Host 校验 response envelope。
7. Host 校验 plugin identity。
8. Host 校验 version。
9. Host 校验 platform。

### 14.8 manifest 阶段

1. Host 构造 manifest request。
2. Host 启动插件进程。
3. 插件返回 runtime manifest response。
4. Host 校验 response envelope。
5. Host 校验 commands。
6. Host 校验 static/runtime command 关系。
7. Host 校验 runtime requiredPermissions。

### 14.9 command 暴露阶段

1. Host 根据 `.bukit/plugins.yaml` 中的 `exposeCommands` 过滤命令。
2. 未在 `exposeCommands` 中的命令不暴露给 CLI。
3. 暴露命令转换为 `CommandDescriptor`。
4. CLI composer 与 Core commands 合并。
5. Core command collision 被拒绝。
6. plugin command collision 被拒绝。
7. alias collision 被拒绝。

### 14.10 lock 写入阶段

1. Host 将 resolved 结果写入 `.bukit/plugins.lock.yaml`。
2. lock 记录插件身份、source、entry、sha256、commands、resolvedAt、sha256Verified。
3. lock 只是审计与复现辅助，不替代下次校验。

### 14.11 invoke 阶段

1. 用户执行插件命令。
2. CLI binder 解析 arguments/options。
3. `PluginCommandInvoker` 构造 invoke request。
4. invoke request 包含 command、context、permissions。
5. Host 启动插件进程。
6. 插件执行业务。
7. 插件 stdout 返回 invoke response。
8. 插件 stderr 返回日志。
9. Host 校验 response。
10. Host 校验 artifacts。
11. Host 写 execution report。
12. CLI 返回插件 exit code。

## 15. 正式插件不能调用的 Core 内容

正式外部插件不得调用：

1. `Bukit.Cli` 内部命令实现。
2. `Bukit.Engine` 构建上下文。
3. `Bukit.Engine` 内置插件 registry。
4. `BuildContext`。
5. `ContentDocument` 的内部可变处理流程。
6. `ThemeBootstrapper`。
7. `ScribanTemplateRendererAdapter`。
8. `TemplateContextBuilder`。
9. `SectionRenderHelper`。
10. `SectionPluginRegistry`。
11. `ISectionPlugin`。
12. `ITemplateContextContributor`。
13. Core 配置加载器内部对象。
14. Core deploy provider。
15. Core Notion/Import 历史实现细节。
16. `Bukit.PluginHost` 内部 service。
17. `IPluginProtocolClient`。
18. `IProcessRunner`。
19. `IPluginManifestLoader`。
20. `IPluginConfigLoader`。

原因：

1. 这些接口或对象属于 Core 内存内实现。
2. 暴露它们会破坏外部进程隔离。
3. 暴露它们会绕过权限模型。
4. 暴露它们会绕过 manifest/hash/CI policy。
5. 暴露它们会让插件版本与 Core 内部类型强耦合。

## 16. 当前存在的边界风险

### 16.1 P1：Engine.Abstractions 中的 in-process 插件接口可能被误认为正式插件协议

涉及文件：

```text
src/Bukit.Engine.Abstractions/Plugins/ISectionPlugin.cs
src/Bukit.Engine/ThemeBootstrapper.cs
src/Bukit.Rendering/Scriban/SectionRenderHelper.cs
src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs
src/Bukit.Rendering/Scriban/ITemplateContextContributor.cs
src/plugins/WordCountSectionPlugin/WordCountPlugin.cs
```

问题描述：

`ISectionPlugin` 和 `SectionPluginRegistry` 是 in-process extension 模型。`WordCountSectionPlugin` 也以 `ISectionPlugin` 的方式存在。这种模型与正式外部进程插件协议不同。

风险：

1. 第三方如果引用 `Bukit.Engine.Abstractions` 并注册 `ISectionPlugin`，就会进入 Core/Engine 内存。
2. 这绕过 `.bukit/plugins.yaml`。
3. 这绕过 `plugin.yaml`。
4. 这绕过 `PluginHost`。
5. 这绕过 sha256 校验。
6. 这绕过 CI policy。
7. 这绕过 execution report。
8. 这绕过 timeout/output limit。

审计结论：

这些接口不能作为正式插件系统对外开放。它们必须被视为 Core 内部或历史实验扩展点。若未来需要开放 Section/Template 扩展，应设计新的外部进程协议或独立 SDK，而不是让第三方直接使用这些 in-process 接口。

### 16.2 P1：experimental 中仍有 `site.externalPlugins` 历史路径

涉及文件：

```text
experimental/Bukit.Labs.Protocol/EngineProtocol/ExternalProtocolPluginSource.cs
```

问题描述：

该文件仍然读取 `site.externalPlugins` 并处理旧式 external protocol plugin source。当前正式配置规范已明确禁止恢复 `site.externalPlugins`。

风险：

1. 如果 experimental 路径被重新接入 Core，会绕过当前 `.bukit/plugins.yaml` 分层。
2. 会造成 `site.yaml` 与 `.bukit/plugins.yaml` 双控制面。
3. 会破坏插件协议 v1 的 source/entry/permission/manifest 约束。
4. 会让历史概念污染当前正式插件系统。

审计结论：

该路径必须保持 experimental 隔离，不得被 Core、CLI、Engine 正式路径引用。后续清理可删除或重命名，避免误导。

### 16.3 P2：`manifestPolicy: runtime-only` 是高风险例外

问题描述：

`runtime-only` 允许 runtime manifest 作为命令来源。这对开发、Labs、兼容或临时动态插件可能有用，但它削弱 static manifest 的命令面冻结能力。

风险：

1. 插件可以在运行时改变命令面。
2. 静态审计无法完整覆盖 runtime command。
3. `exposeCommands` 的有效性依赖 runtime response。
4. 官方插件如果使用该策略，会降低发布可审计性。

审计结论：

正式插件必须使用 `static`。CI、release、official plugin package 应拒绝 `runtime-only`。

### 16.4 P2：外部进程插件不是 build hook

问题描述：

协议 v1 不包含正式 build hook。外部进程插件目前是 CLI feature plugin，而不是 Engine build pipeline plugin。

风险：

1. 如果将外部插件直接插入 Engine `PluginRegistry`，会混淆内部插件和外部插件。
2. 如果用 `ISectionPlugin` 充当外部插件接口，会破坏协议隔离。
3. 如果让插件直接处理 BuildContext，会绕过权限模型。

审计结论：

当前不能把外部插件描述为构建钩子系统。未来如需 build hook，必须新增协议能力，并仍由 PluginHost 作为唯一边界。

### 16.5 P2：Plugin.Abstractions 名称可能被误解为 SDK

问题描述：

`Abstractions` 这个名称容易让插件作者以为它是 Core 抽象层或 SDK。

实际情况：

1. 它只包含 DTO。
2. 它只包含 JSON context。
3. 它不提供 Core service。
4. 它不提供 Engine service。
5. 它不提供文件操作 helper。
6. 它不提供网络 helper。

审计结论：

文档应持续强调：Plugin.Abstractions 是协议模型包，不是 SDK。真正 SDK 如后续需要，应独立设计，并且 SDK 只能封装协议调用和 DTO，不得暴露 Core 内部实现。

## 17. 必须保留的分层

### 17.1 Bukit.PluginHost 必须保留

原因：

1. 它是插件系统的安全边界。
2. 它集中执行路径校验。
3. 它集中执行 manifest 校验。
4. 它集中执行 sha256 校验。
5. 它集中执行权限校验。
6. 它集中执行 CI policy。
7. 它集中执行进程隔离。
8. 它集中执行协议响应校验。
9. 它集中执行报告输出。

如果删除 PluginHost，CLI 要么直接执行插件，要么直接加载插件代码，都会破坏当前安全模型。

### 17.2 Bukit.Plugin.Abstractions 必须保留

原因：

1. 它固化协议模型。
2. 它降低 Host 和官方插件 DTO 漂移。
3. 它支持 source-generated JSON。
4. 它支持 AOT/trim 友好序列化。
5. 它使协议测试有稳定类型目标。

保留条件：

1. 不向其中加入 Core service。
2. 不向其中加入 Engine object。
3. 不向其中加入 Host 内部接口。
4. 不把它升级为绕过协议的 SDK。

### 17.3 `.bukit/plugins.yaml` 必须保留

原因：

1. 它是项目维护者授权入口。
2. 它控制插件启用状态。
3. 它控制暴露命令。
4. 它控制 permissions。
5. 它控制 timeout。
6. 它控制 output limit。
7. 它控制 CI 是否允许运行。

如果将这些配置放回 `site.yaml`，会重新制造 `site.externalPlugins` 时代的边界混乱。

### 17.4 `plugins/<id>/plugin.yaml` 必须保留

原因：

1. 它声明插件包身份。
2. 它声明协议版本。
3. 它声明执行形态。
4. 它声明平台入口。
5. 它声明 sha256。
6. 它声明命令面。
7. 它声明 requiredPermissions。

如果没有 `plugin.yaml`，Host 无法在执行前完成静态审计。

### 17.5 `.bukit/plugins.lock.yaml` 必须保留

原因：

1. 它记录 resolved 状态。
2. 它记录实际平台 entry。
3. 它记录 sha256Verified。
4. 它记录暴露命令。
5. 它支持审计和复现。

限制：

1. lock 不能替代 manifest。
2. lock 不能替代 sha256 校验。
3. lock 不能授予权限。

### 17.6 `.bukit/reports/plugin-executions/*.json` 必须保留

原因：

1. 插件执行必须可审计。
2. 插件 stderr 必须可追踪。
3. 插件 artifacts 必须可追踪。
4. 插件 diagnostics 必须可追踪。
5. sha256Verified 和 environment masking 必须可追踪。

## 18. 不应作为正式插件系统保留或开放的内容

以下内容不应作为正式插件开发接口：

1. `site.externalPlugins`
2. `experimental/Bukit.Labs.Protocol/EngineProtocol/ExternalProtocolPluginSource.cs`
3. `Assembly.LoadFrom` 型插件加载。
4. 动态 DLL 插件。
5. in-process 第三方插件。
6. `ISectionPlugin` 作为第三方插件 API。
7. `SectionPluginRegistry` 作为第三方插件注册入口。
8. `ITemplateContextContributor` 作为第三方插件 API。
9. Engine `BuildContext` 作为第三方插件上下文。
10. Rendering 内部 helper 作为第三方插件 API。
11. Core service 作为第三方插件 API。
12. PluginHost 内部 service 作为第三方插件 API。
13. `runtime-only` 作为正式发布插件默认策略。
14. `.bukit` 内存放 executable。
15. `.bukit/plugins/` 作为插件程序目录。
16. 自动下载 marketplace。
17. 自动更新插件。
18. Host 解析插件依赖。
19. Host 执行 runtime-dependent 插件。
20. WASM/Docker 插件形态。

## 19. 官方插件与第三方插件开发边界

### 19.1 官方插件

官方插件可以引用：

1. `Bukit.Plugin.Abstractions`
2. 必要的共享 DTO 包

官方插件不应引用：

1. `Bukit.Cli`
2. `Bukit.PluginHost`
3. `Bukit.Engine`
4. `Bukit.Rendering`
5. `Bukit.Engine.Abstractions` 中的 in-process plugin API

官方插件也必须：

1. 提供 executable。
2. 提供 `plugin.yaml`。
3. 声明 `bukit-plugin-v1`。
4. 实现 handshake。
5. 实现 manifest。
6. 实现 invoke。
7. 声明 sha256。
8. 声明 requiredPermissions。
9. 被 `.bukit/plugins.yaml` 显式启用和授权。

### 19.2 第三方插件

第三方插件可以完全不引用 Bukit 程序集。

第三方插件只需要：

1. 能读取 stdin JSON。
2. 能写 stdout JSON。
3. 能写 stderr 日志。
4. 能按协议返回 response。
5. 能提供 `plugin.yaml`。
6. 能提供平台 executable。
7. 能提供 sha256。

第三方插件不得：

1. 引用 Core 内部项目。
2. 依赖 Engine in-process object。
3. 让 Host 加载 DLL。
4. 通过 `site.yaml` 注册。
5. 写入未授权路径。
6. 读取未授权环境变量。
7. 输出非 JSON 到 stdout。

## 20. 当前实现与协议的一致性判定

### 20.1 一致部分

当前主线代码与正式协议一致的方面：

1. 存在 `Bukit.PluginHost` 项目。
2. 存在 `Bukit.Plugin.Abstractions` 项目。
3. 协议常量为 `bukit-plugin-v1`。
4. 支持 handshake。
5. 支持 manifest。
6. 支持 invoke。
7. `.bukit/plugins.yaml` 与 `site.yaml` 分离。
8. `plugin.yaml` 是插件包 manifest。
9. 插件 kind 限制为 `process`。
10. 插件 distribution 限制为 `self-contained`。
11. source 限制在 `plugins/<id>`。
12. entry path 有校验。
13. sha256 有校验。
14. permissions 有校验。
15. CI 有额外 policy。
16. stdout/stderr 有大小限制。
17. invoke 有 timeout。
18. process 不通过 shell 启动。
19. process environment 被清空后按 allowlist 重建。
20. execution report 存在。
21. lock writer 存在。
22. Core command 优先。
23. plugin command collision 有组合层处理。

### 20.2 需要保持隔离的部分

当前存在但不能进入正式插件系统的部分：

1. experimental old external plugin source。
2. `site.externalPlugins` 相关历史测试或错误信息。
3. Engine.Abstractions 的 in-process plugin registry。
4. Rendering template contributor。
5. `src/plugins/WordCountSectionPlugin` 这类 in-process 示例。

这些内容的存在不代表正式插件协议允许它们。它们必须被文档、测试和架构边界继续隔离。

## 21. 后续清理建议

本报告不执行清理，只列出建议。

### 21.1 建议一：给 in-process Engine 扩展加内部边界说明

目标文件：

```text
src/Bukit.Engine.Abstractions/Plugins/ISectionPlugin.cs
src/Bukit.Rendering/Scriban/ITemplateContextContributor.cs
```

建议：

1. 明确标注它们不是正式插件协议。
2. 明确标注外部插件不得依赖。
3. 如果仅测试或内部使用，应考虑降低可见度或迁移到内部测试 fixture。

### 21.2 建议二：继续隔离或删除 experimental `ExternalProtocolPluginSource`

目标文件：

```text
experimental/Bukit.Labs.Protocol/EngineProtocol/ExternalProtocolPluginSource.cs
```

建议：

1. 不接入 Core 主线。
2. 不作为迁移基础。
3. 后续如清理 Labs，可删除。
4. 如果暂保留，应在文件头标注 legacy/experimental/not formal plugin protocol。

### 21.3 建议三：正式插件包禁止 runtime-only

建议：

1. 官方插件包 fixture 中禁止 `manifestPolicy: runtime-only`。
2. CI/release gate 中检测 runtime-only。
3. 文档中继续声明它只用于 dev/Labs/compat/temp。

### 21.4 建议四：SDK 后续独立设计

如果未来需要 SDK，应满足：

1. SDK 不引用 Core。
2. SDK 不引用 PluginHost。
3. SDK 不引用 Engine。
4. SDK 只封装 JSON 协议、DTO、stdio 处理、错误构造、manifest 构造。
5. SDK 不提供文件系统绕权 API。
6. SDK 不提供 Core service 代理。
7. SDK 不提供 in-process hook。

### 21.5 建议五：持续架构测试

建议测试继续覆盖：

1. Core 不引用 official plugin implementation。
2. PluginHost 不依赖插件实现项目。
3. Official plugin 不引用 PluginHost。
4. Official plugin 不引用 Bukit.Cli。
5. Official plugin 不引用 Bukit.Engine。
6. `site.externalPlugins` 不进入 current config schema。
7. `.bukit/plugins.yaml` 不允许 source 指向 `.bukit`。
8. manifest kind 只允许 `process`。
9. manifest protocol 只允许 `bukit-plugin-v1`。
10. runtime manifest 不得超过 static manifest。

## 22. 最终判定

Bukit Core 当前正式插件系统的核心边界是清晰的：

```text
Core 内置 Engine 插件
  = Core 内部构建能力
  = in-process
  = 不属于第三方插件协议

外部进程插件
  = 正式插件协议
  = bukit-plugin-v1
  = .bukit/plugins.yaml + plugins/<id>/plugin.yaml
  = PluginHost 统一加载、校验、执行、报告
```

必须长期坚持的原则：

1. 外部插件严禁直接调用 Core 代码。
2. 外部插件严禁通过 DLL/in-process 方式加载。
3. 外部插件严禁通过 `site.yaml` 注册。
4. 外部插件必须使用 `plugin.yaml`。
5. 外部插件必须使用 `bukit-plugin-v1`。
6. 外部插件必须通过 PluginHost 执行。
7. 外部插件必须接受 Host 权限、路径、hash、timeout、output、CI policy 校验。
8. Plugin.Abstractions 只能是协议模型，不应演变成 Core SDK。
9. 如未来需要 SDK，必须单独设计，并且不得暴露 Core 内部实现。

最终结论：当前正式主线插件系统设计可以作为 Bukit Core 插件边界的基础继续推进；必须重点防止 legacy experimental 路径、Engine in-process 扩展点和 runtime-only 例外进入正式外部插件开发模型。
