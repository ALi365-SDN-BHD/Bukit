# Bukit Core 插件机制设计文档

> 文档版本：v1.0
> 适用项目：Bukit 静态网站生成引擎
> 文档类型：Core 插件机制设计文档
> 目标机制：Language-Agnostic Cross-Platform External Process Plugin System
> 中文定义：语言无关、跨平台外部进程插件体系
> 状态：设计稿
> 优先级：P0
> 适用阶段：Bukit 二级开发插件化基础建设阶段

---

## 1. 文档目的

本文档用于定义 Bukit Core 的正式插件机制。

本机制用于支持 Bukit 从单一 CLI 工具升级为可扩展静态网站生成平台内核，使成熟功能能够通过正式插件接入 Core CLI，同时保持 Core 的稳定性、安全性、Native AOT 友好性和跨平台能力。

本文档重点回答：

1. Core 插件机制是什么。
2. Core、Plugin、Labs 的边界是什么。
3. 插件程序存放在哪里。
4. `.bukit/` 目录的职责是什么。
5. 插件是否限制开发语言。
6. 插件如何保证跨平台。
7. 插件如何配置、发现、加载、校验、执行。
8. Core 如何通过外部进程协议调用插件。
9. 插件如何接入 CLI 命令。
10. Labs 功能如何迁移为正式插件。
11. 后续 `import`、`clone` 等功能如何进入正式发布体系。

---

## 2. 背景

Bukit 当前已经具备稳定 Core 形态，包括：

* Core CLI
* Build Engine
* Config
* Content
* Rendering
* Routing
* Theme
* Shared
* Built-in Engine Plugins
* Labs 实验功能入口

当前 Core CLI 仍以静态命令注册为主。

当前 Labs 中存在若干尚未正式发布的功能模块，例如：

```text
import
clone
notion
intent
visual
webhook
data
theme
```

这些功能不应长期停留在 Labs 中。成熟后应迁移为正式插件，并通过 Core Plugin Host 接入 Core CLI。

因此，Bukit 需要建立统一的 Core 插件机制，用于承载未来所有正式发布的功能扩展。

---

## 3. 核心原则

## 3.1 Core 是稳定底座

Core 负责：

* CLI 宿主
* 插件宿主
* 插件协议
* 插件配置
* 插件执行
* 插件安全
* 构建引擎
* 配置系统
* 内容源抽象
* 路由系统
* 渲染系统
* 主题系统
* Core 内置插件
* 日志、诊断、报告
* Native AOT 发布

Core 不应成为所有业务功能的堆积区。

Core 的职责是：

```text
提供稳定底座
提供插件宿主
提供正式发布入口
```

---

## 3.2 Labs 是未发布功能孵化区

Labs 代表尚不可发布、尚不稳定、允许快速变化的功能模块。

Labs 中功能可以：

* 快速迭代
* 破坏性修改
* 暂时缺少完整文档
* 暂时缺少完整测试
* 暂时不遵守正式插件协议

Labs 中功能不应：

* 默认出现在 Core CLI
* 直接作为正式功能发布
* 被 Core 直接依赖
* 被正式插件依赖
* 绕过发布准入门禁

Labs 成熟后必须迁移为 Plugin。

---

## 3.3 Plugin 是已发布功能模块

Plugin 代表已成熟、可发布、可启用、可禁用、可由 Core CLI 接入的正式功能模块。

正式插件必须满足：

```text
外部进程运行
语言无关
跨平台
实现 bukit-plugin-v1
有 plugin.yaml manifest
有权限声明
有平台入口
可被 Core Plugin Host 校验和执行
```

---

## 3.4 除 Core 内置插件外，正式插件全部采用外部进程模式

Core 内置插件可以是 in-process。

除 Core 内置插件外，所有正式插件必须是：

```text
External Process Plugin
```

不采用：

```text
runtime DLL plugin
Assembly.LoadFrom plugin
in-process third-party plugin
dynamic assembly plugin
```

原因：

1. 保持 Native AOT 友好。
2. 保持 Core 安全边界清晰。
3. 支持任意语言开发。
4. 支持跨平台二进制分发。
5. 避免插件污染 Core 进程。
6. 避免第三方插件破坏 Core 内存空间。
7. 降低版本耦合。

---

## 3.5 插件语言无关

Core 不限制插件开发语言。

正式插件可以使用：

```text
.NET
Go
Rust
Node.js
Deno
Bun
Python
Java
Kotlin
Swift
C++
其他可生成跨平台可执行程序的语言
```

Core 不关心插件用什么语言实现。

Core 只关心：

```text
能否在当前平台运行
能否完成 handshake
能否提供 manifest
能否通过 invoke 执行
能否输出合法 JSON
能否遵守权限声明
能否在 timeout 内结束
```

---

## 3.6 插件必须具备跨平台能力

正式插件必须具备跨平台能力。

最低支持平台：

```text
windows-x64
linux-x64
osx-arm64
```

推荐支持平台：

```text
windows-x64
windows-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

如果插件只支持单一平台，不能作为正式插件进入 Core 发布体系，只能留在 Labs 或标记为 platform-limited preview。

---

## 3.7 `.bukit/` 不允许存放插件程序

`.bukit/` 是 Bukit 系统工作目录。

`.bukit/` 只能存放：

* 系统配置
* 锁文件
* 报告
* 缓存
* 日志
* 临时文件
* 状态文件
* 执行记录

`.bukit/` 禁止存放：

* 插件程序
* 可执行文件
* 插件包
* 插件 bin 目录
* DLL
* shell 脚本
* PowerShell 脚本
* cmd/bat 脚本
* 任何 Core 会执行的程序

---

## 3.8 插件程序必须放在项目根目录 `plugins/`

项目本地插件包必须存放于：

```text
plugins/<plugin-id>/
```

示例：

```text
plugins/import/
plugins/clone/
plugins/notion/
plugins/visual/
```

Core Plugin Host 第一版只允许从项目根目录 `plugins/` 加载插件。

---

## 4. 术语定义

## 4.1 Core

Bukit 稳定内核与正式发布入口。

包含：

```text
Bukit.Cli
Bukit.Cli.Shared
Bukit.PluginHost
Bukit.Plugin.Abstractions
Bukit.Config
Bukit.Content
Bukit.Engine
Bukit.Engine.Abstractions
Bukit.Rendering
Bukit.Routing
Bukit.Shared
Bukit.Theme
```

---

## 4.2 Core Built-in Plugin

编译进 Core 的 in-process 插件。

适用于基础构建能力。

示例：

```text
taxonomy
archive
pagination
pages-index
data-files
menu
alias
related-content
image-processing
```

---

## 4.3 External Process Plugin

外部进程插件。

特点：

```text
独立可执行
语言无关
跨平台
通过 JSON 协议通信
由 Core Plugin Host 启动
不被 Core 进程内加载
```

---

## 4.4 Labs

未成熟功能孵化区。

---

## 4.5 Plugin Host

Core 中负责插件发现、配置、校验、执行、报告的宿主模块。

建议项目：

```text
src/Bukit.PluginHost/
```

---

## 4.6 Plugin Abstractions

Core 与官方插件共享的协议模型项目。

建议项目：

```text
src/Bukit.Plugin.Abstractions/
```

第三方插件不强制依赖该项目，只需实现 JSON 协议。

---

## 5. 总体架构

## 5.1 架构图

```text
Bukit CLI
  |
  |-- Core Commands
  |
  |-- Plugin Commands
        |
        v
Bukit PluginHost
  |
  |-- read .bukit/plugins.yaml
  |-- read plugins/<id>/plugin.yaml
  |-- resolve platform entry
  |-- validate sha256
  |-- handshake
  |-- manifest
  |-- invoke
        |
        v
External Process Plugin
  |
  |-- any language
  |-- self-contained executable
  |-- bukit-plugin-v1 JSON protocol
```

---

## 5.2 Core CLI 命令组成

Core CLI 命令由两部分组成：

```text
Core Stable Commands
  +
Enabled Plugin Commands
```

Core Stable Commands 示例：

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
plugin
```

Plugin Commands 示例：

```text
import
clone
notion
visual
theme
```

---

## 6. 仓库目录结构设计

## 6.1 Bukit 仓库目录

推荐结构：

```text
Bukit/
├── src/
│   ├── Bukit.Cli/
│   ├── Bukit.Cli.Shared/
│   ├── Bukit.PluginHost/
│   ├── Bukit.Plugin.Abstractions/
│   ├── Bukit.Config/
│   ├── Bukit.Content/
│   ├── Bukit.Engine/
│   ├── Bukit.Engine.Abstractions/
│   ├── Bukit.Rendering/
│   ├── Bukit.Routing/
│   ├── Bukit.Shared/
│   └── Bukit.Theme/
│
├── plugins/
│   ├── Bukit.Plugin.Import/
│   ├── Bukit.Plugin.Clone/
│   ├── Bukit.Plugin.Notion/
│   ├── Bukit.Plugin.Visual/
│   ├── Bukit.Plugin.Theme/
│   └── Bukit.Plugin.Deploy/
│
├── labs/
│   ├── Bukit.Labs.Cli/
│   ├── Bukit.Labs.Import/
│   ├── Bukit.Labs.Clone/
│   ├── Bukit.Labs.Intent/
│   ├── Bukit.Labs.Visual/
│   ├── Bukit.Labs.Webhook/
│   └── Bukit.Labs.Theme/
│
├── tests/
│   ├── Bukit.PluginHost.Tests/
│   ├── Bukit.Plugin.Abstractions.Tests/
│   ├── Bukit.Plugin.Import.Tests/
│   ├── Bukit.Plugin.Clone.Tests/
│   ├── Bukit.Labs.Cli.Tests/
│   └── ...
│
├── schemas/
│   ├── bukit-plugin-config.schema.json
│   ├── bukit-plugin-manifest.schema.json
│   ├── bukit-plugin-handshake.schema.json
│   ├── bukit-plugin-invoke.schema.json
│   └── bukit-plugin-result.schema.json
│
├── docs/
│   ├── adr/
│   ├── specs/
│   └── plans/
│
├── examples/
├── bukit.slnx
├── bukit.plugins.slnx
├── bukit.labs.slnx
└── bukit.all.slnx
```

---

## 6.2 用户项目目录

推荐结构：

```text
project-root/
├── site.yaml
├── content/
├── themes/
├── plugins/
│   ├── import/
│   │   ├── plugin.yaml
│   │   ├── bin/
│   │   │   ├── win-x64/
│   │   │   │   └── bukit-plugin-import.exe
│   │   │   ├── win-arm64/
│   │   │   │   └── bukit-plugin-import.exe
│   │   │   ├── linux-x64/
│   │   │   │   └── bukit-plugin-import
│   │   │   ├── linux-arm64/
│   │   │   │   └── bukit-plugin-import
│   │   │   ├── osx-x64/
│   │   │   │   └── bukit-plugin-import
│   │   │   └── osx-arm64/
│   │   │       └── bukit-plugin-import
│   │   └── README.md
│   │
│   └── clone/
│       ├── plugin.yaml
│       ├── bin/
│       └── README.md
│
└── .bukit/
    ├── plugins.yaml
    ├── plugins.lock.yaml
    ├── reports/
    │   ├── plugin-executions/
    │   ├── security/
    │   ├── build-report.json
    │   ├── seo-report.json
    │   └── geo-report.json
    ├── cache/
    ├── logs/
    ├── tmp/
    └── state/
```

---

## 7. `.bukit/` 系统目录规范

## 7.1 允许内容

```text
.bukit/plugins.yaml
.bukit/plugins.lock.yaml
.bukit/reports/
.bukit/cache/
.bukit/logs/
.bukit/tmp/
.bukit/state/
```

---

## 7.2 禁止内容

```text
.bukit/plugins/
.bukit/bin/
.bukit/tools/
.bukit/plugin-executables/
.bukit/*.exe
.bukit/*.dll
.bukit/*.sh
.bukit/*.cmd
.bukit/*.bat
.bukit/*.ps1
```

---

## 7.3 强制规则

Core Plugin Host 必须拒绝任何解析到 `.bukit/` 内的插件执行路径。

错误示例：

```text
Plugin source cannot be inside .bukit: .bukit/plugins/import
```

```text
Plugin executable cannot be inside .bukit: .bukit/bin/bukit-plugin-import
```

---

## 8. `plugins/` 插件程序目录规范

## 8.1 允许来源

第一版只允许：

```text
plugins/<plugin-id>/
```

---

## 8.2 禁止来源

```text
.bukit/plugins/
../plugins/
~/plugins/
~/.bukit/plugins/
node_modules/.bin/
absolute/path/to/plugin/
/tmp/plugin/
```

---

## 8.3 插件包结构

```text
plugins/<plugin-id>/
├── plugin.yaml
├── bin/
│   ├── win-x64/
│   ├── win-arm64/
│   ├── linux-x64/
│   ├── linux-arm64/
│   ├── osx-x64/
│   └── osx-arm64/
├── README.md
├── schema/
└── assets/
```

---

## 9. 插件配置设计

## 9.1 `.bukit/plugins.yaml`

项目级插件启用配置。

示例：

```yaml
version: 1

plugins:
  import:
    enabled: true
    source: plugins/import
    exposeCommands:
      - import
    permissions:
      fileSystem:
        read:
          - .
        write:
          - ./sites
          - ./themes
          - ./content
      network: false
      environment:
        read:
          - NOTION_TOKEN

  clone:
    enabled: true
    source: plugins/clone
    exposeCommands:
      - clone
    permissions:
      fileSystem:
        read:
          - .
        write:
          - ./themes
          - ./content
          - ./data
          - ./docs/research
      network: true
      environment:
        read: []
```

---

## 9.2 配置字段说明

| 字段               | 类型     | 必填 | 说明                       |
| ---------------- | ------ | -: | ------------------------ |
| `version`        | int    |  是 | 配置版本                     |
| `plugins`        | map    |  是 | 插件配置集合                   |
| `enabled`        | bool   |  是 | 是否启用                     |
| `source`         | string |  是 | 插件包目录，只允许 `plugins/<id>` |
| `exposeCommands` | list   |  否 | 暴露到 Core CLI 的命令         |
| `permissions`    | object |  是 | 项目授予该插件的权限               |

---

## 9.3 禁用行为

如果配置：

```yaml
plugins:
  import:
    enabled: false
```

执行：

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

应输出：

```text
Command disabled by plugin config: import
```

不能输出：

```text
Unknown command: import
```

---

## 10. 插件 manifest 设计

## 10.1 `plugins/<id>/plugin.yaml`

示例：

```yaml
id: import
name: Bukit Import Plugin
version: 1.0.0
protocol: bukit-plugin-v1
kind: process
distribution: self-contained

platforms:
  win-x64:
    entry: bin/win-x64/bukit-plugin-import.exe
    sha256: "<hash>"

  win-arm64:
    entry: bin/win-arm64/bukit-plugin-import.exe
    sha256: "<hash>"

  linux-x64:
    entry: bin/linux-x64/bukit-plugin-import
    sha256: "<hash>"

  linux-arm64:
    entry: bin/linux-arm64/bukit-plugin-import
    sha256: "<hash>"

  osx-x64:
    entry: bin/osx-x64/bukit-plugin-import
    sha256: "<hash>"

  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-import
    sha256: "<hash>"

commands:
  - name: import
    description: Import external content into Bukit
    subcommands:
      - html-demo
      - seed

requiredPermissions:
  fileSystem:
    read:
      - .
    write:
      - ./sites
      - ./themes
      - ./content
  network: false
  environment:
    read:
      - NOTION_TOKEN
```

---

## 10.2 Manifest 字段说明

| 字段                    | 类型     | 必填 | 说明                                     |
| --------------------- | ------ | -: | -------------------------------------- |
| `id`                  | string |  是 | 插件 ID                                  |
| `name`                | string |  是 | 插件名称                                   |
| `version`             | string |  是 | 插件版本                                   |
| `protocol`            | string |  是 | 协议版本                                   |
| `kind`                | string |  是 | 必须为 `process`                          |
| `distribution`        | string |  是 | `self-contained` 或 `runtime-dependent` |
| `platforms`           | map    |  是 | 平台入口                                   |
| `commands`            | list   |  否 | CLI 命令声明                               |
| `requiredPermissions` | object |  是 | 插件所需权限                                 |

---

## 11. 插件锁文件设计

## 11.1 `.bukit/plugins.lock.yaml`

示例：

```yaml
version: 1

resolved:
  import:
    source: plugins/import
    version: 1.0.0
    platform: osx-arm64
    entry: plugins/import/bin/osx-arm64/bukit-plugin-import
    sha256: "<hash>"
    resolvedAt: "2026-06-17T00:00:00Z"

  clone:
    source: plugins/clone
    version: 1.0.0
    platform: osx-arm64
    entry: plugins/clone/bin/osx-arm64/bukit-plugin-clone
    sha256: "<hash>"
    resolvedAt: "2026-06-17T00:00:00Z"
```

---

## 11.2 锁文件规则

`.bukit/plugins.lock.yaml` 可以记录：

* source
* version
* platform
* resolved entry
* sha256
* resolvedAt

但不得存放插件程序。

---

## 12. 插件协议 v1

协议名称：

```text
bukit-plugin-v1
```

第一版支持：

```text
handshake
manifest
invoke
```

后续扩展：

```text
hook
doctor
install
upgrade
```

---

## 12.1 通信方式

第一版建议使用：

```text
stdin JSON request
stdout JSON response
stderr log stream
```

Core 不应通过 shell 拼接参数调用插件。

---

## 12.2 handshake

请求：

```json
{
  "type": "handshake",
  "protocol": "bukit-plugin-v1",
  "hostVersion": "1.0.0",
  "platform": "osx-arm64"
}
```

响应：

```json
{
  "protocol": "bukit-plugin-v1",
  "id": "import",
  "name": "Bukit Import Plugin",
  "version": "1.0.0",
  "capabilities": [
    "cli-command"
  ],
  "platform": "osx-arm64"
}
```

---

## 12.3 manifest

请求：

```json
{
  "type": "manifest",
  "protocol": "bukit-plugin-v1",
  "platform": "osx-arm64"
}
```

响应：

```json
{
  "commands": [
    {
      "name": "import",
      "description": "Import external content into Bukit",
      "subcommands": [
        {
          "name": "html-demo",
          "description": "Import static HTML demo"
        },
        {
          "name": "seed",
          "description": "Convert generated seed data"
        }
      ]
    }
  ],
  "requiredPermissions": {
    "fileSystem": {
      "read": ["."],
      "write": ["./sites", "./themes", "./content"]
    },
    "network": false,
    "environment": {
      "read": ["NOTION_TOKEN"]
    }
  }
}
```

---

## 12.4 invoke

请求：

```json
{
  "type": "invoke",
  "protocol": "bukit-plugin-v1",
  "command": "import",
  "arguments": ["html-demo", "./demo"],
  "options": {
    "--theme": "silkroadbiz",
    "--verify": true
  },
  "context": {
    "rootDir": "/project",
    "configPath": "/project/site.yaml",
    "platform": "osx-arm64"
  }
}
```

响应：

```json
{
  "exitCode": 0,
  "messages": [
    {
      "level": "info",
      "message": "Import completed."
    }
  ],
  "artifacts": [
    {
      "type": "file",
      "path": "sites/silkroadbiz/import-report.md"
    }
  ]
}
```

---

## 13. Core PluginHost 设计

建议新增项目：

```text
src/Bukit.PluginHost/
```

职责：

1. 读取 `.bukit/plugins.yaml`。
2. 校验插件配置 schema。
3. 校验 `source` 路径。
4. 禁止 `.bukit` 执行入口。
5. 读取 `plugins/<id>/plugin.yaml`。
6. 校验 plugin manifest schema。
7. 解析当前平台 RID。
8. 解析平台 entry。
9. 校验 sha256。
10. 执行 handshake。
11. 执行 manifest。
12. 校验 requiredPermissions。
13. 生成 plugin command descriptors。
14. 执行 invoke。
15. 管理 timeout。
16. 管理 stdout/stderr。
17. 解析 JSON response。
18. 写入 `.bukit/plugins.lock.yaml`。
19. 写入 `.bukit/reports/plugin-executions/*.json`。
20. 返回统一 exit code。

---

## 14. Plugin Abstractions 设计

建议新增项目：

```text
src/Bukit.Plugin.Abstractions/
```

包含：

```text
PluginConfig
PluginManifest
PluginPlatformEntry
PluginPermission
PluginHandshakeRequest
PluginHandshakeResponse
PluginManifestRequest
PluginManifestResponse
PluginInvokeRequest
PluginInvokeResponse
PluginMessage
PluginArtifact
PluginConstants
```

第三方插件不强制引用该项目。

---

## 15. CLI 接入设计

## 15.1 当前问题

当前 Core CLI 是静态 descriptors。

目标是改为：

```text
Core command descriptors
  +
Plugin command descriptors
```

---

## 15.2 Composer

建议新增：

```text
BukitCliComposer
```

伪代码：

```csharp
var coreDescriptors = CoreCommandRegistry.CreateDescriptors();
var pluginDescriptors = pluginHost.CreateCommandDescriptors(projectRoot);

var descriptors = CommandDescriptorMerger.Merge(
    coreDescriptors,
    pluginDescriptors);
```

---

## 15.3 命令冲突规则

1. Core command 优先。
2. 插件不得覆盖 Core command。
3. 插件之间不得注册同名 command。
4. alias 不得冲突。
5. disabled plugin 的命令应注册为 disabled descriptor 或由 resolver 返回 disabled diagnostic。
6. 禁用命令不得表现为 unknown command。

---

## 16. 外部进程启动规则

Core 启动插件时必须：

```text
UseShellExecute = false
RedirectStandardInput = true
RedirectStandardOutput = true
RedirectStandardError = true
```

禁止：

```text
sh -c
cmd /c
powershell -Command
bash script.sh
shell string concatenation
```

推荐：

```text
FileName = resolved plugin executable
Arguments = fixed protocol verb only, or no arguments
Request = stdin JSON
Response = stdout JSON
Logs = stderr
```

---

## 17. 安全模型

## 17.1 路径安全

必须拒绝：

```text
source: .bukit/plugins/import
source: ../plugins/import
source: /absolute/path
entry: ../../evil
entry: .bukit/bin/tool
```

---

## 17.2 权限模型

Core 比较：

```text
requiredPermissions <= grantedPermissions
```

如果插件要求超出项目配置授予权限，拒绝执行。

---

## 17.3 网络权限

```yaml
network: false
```

表示插件不应访问网络。

第一版 Core 可先做声明校验与报告，后续再考虑 OS 级沙箱。

---

## 17.4 环境变量权限

插件只能读取显式允许的环境变量：

```yaml
environment:
  read:
    - NOTION_TOKEN
```

Core 传递给插件的环境变量应采用 allowlist。

---

## 17.5 文件权限

插件只能读写声明范围内路径。

第一版至少做：

* 配置校验
* 报告记录
* 路径传入前校验
* 插件声明与项目授权比对

---

## 17.6 超时

每次插件调用必须有 timeout。

建议默认：

```text
handshake: 5s
manifest: 5s
invoke: 120s
```

可在 `.bukit/plugins.yaml` 中配置。

---

## 17.7 输出大小限制

必须限制：

```text
stdout max size
stderr max size
result JSON max size
```

---

## 17.8 sha256 校验

如果 manifest 声明 sha256，Core 必须校验。

正式发布插件必须提供 sha256。

---

## 18. 报告与审计

每次插件执行应写入：

```text
.bukit/reports/plugin-executions/<plugin-id>-<timestamp>.json
```

报告内容：

```json
{
  "pluginId": "import",
  "version": "1.0.0",
  "platform": "osx-arm64",
  "command": "import",
  "operation": "invoke",
  "entry": "plugins/import/bin/osx-arm64/bukit-plugin-import",
  "startedAt": "2026-06-17T00:00:00Z",
  "durationMs": 1234,
  "exitCode": 0,
  "success": true,
  "sha256Verified": true,
  "permissions": {
    "network": false
  }
}
```

---

## 19. Core Built-in Engine Plugin 兼容

Core 内置 Engine 插件继续保持现有 in-process 模式。

`site.plugins` 继续用于控制 Core 内置 Engine 插件。

示例：

```yaml
site:
  plugins:
    taxonomy:
      enabled: true
    archive:
      enabled: false
```

`.bukit/plugins.yaml` 不取代 `site.plugins`。

二者边界：

```text
site.plugins
  = Engine built-in plugins

.bukit/plugins.yaml
  = external process feature plugins
```

---

## 20. 非目标范围

第一版不做：

```text
插件市场
远程插件安装
自动下载插件
全局插件目录
动态 DLL 插件
WASM 插件
Docker 插件
插件热加载
插件自动更新
复杂插件依赖解析
OS 级完整沙箱
插件签名服务
```

第一版只做：

```text
项目本地 plugins/ 外部进程插件
```

---

## 21. Labs → Plugin → Core 准入标准

Labs 功能迁移为正式插件前必须满足：

| 准入项  | 要求                                   |
| ---- | ------------------------------------ |
| 命令稳定 | command / subcommand / options 稳定    |
| 协议稳定 | 支持 bukit-plugin-v1                   |
| 配置稳定 | 支持 `.bukit/plugins.yaml`             |
| 跨平台  | 至少 Windows x64、Linux x64、macOS arm64 |
| 领域边界 | CLI handler 不混业务核心                   |
| 安全   | 权限声明完整                               |
| 测试   | 单元、集成、协议、跨平台、禁用测试                    |
| 文档   | CLI reference、plugin guide、skills 更新 |
| AOT  | 不破坏 Core Native AOT                  |
| 发布   | 可随 Core 版本打包或单独发布                    |

---

## 22. 第一批插件迁移建议

## 22.1 Import Plugin

优先级：P0

原因：

* 已有导入领域逻辑基础。
* 比 clone 更容易作为第一个插件验证 Core Plugin Host。
* 可验证 CLI command plugin、permissions、manifest、invoke、report 全流程。

目标：

```text
plugins/Bukit.Plugin.Import
```

正式命令：

```text
bukit import
```

---

## 22.2 Clone Plugin

优先级：P1

原因：

* 功能复杂。
* 涉及资源下载、主题生成、视觉验证、行为脚本、构建验证。
* 应在 import 插件机制跑通后迁移。

目标：

```text
plugins/Bukit.Plugin.Clone
```

正式命令：

```text
bukit clone
```

---

## 22.3 Notion Plugin

优先级：P2

目标：

```text
plugins/Bukit.Plugin.Notion
```

正式命令：

```text
bukit notion
```

---

## 22.4 Visual Plugin

优先级：P3

目标：

```text
plugins/Bukit.Plugin.Visual
```

正式命令：

```text
bukit visual
```

---

## 23. 测试需求

## 23.1 PluginHost 测试

必须覆盖：

```text
load .bukit/plugins.yaml
reject missing plugins.yaml with safe default
reject source inside .bukit
reject source outside plugins/
load plugin.yaml
reject malformed plugin.yaml
resolve current platform
reject unsupported platform
resolve entry
reject path traversal entry
reject entry inside .bukit
validate sha256
reject sha256 mismatch
handshake success
handshake invalid JSON
handshake timeout
manifest success
manifest required permission overflow
invoke success
invoke non-zero exit
invoke timeout
stdout too large
stderr too large
write plugins.lock.yaml
write plugin execution report
disabled plugin command
command conflict
alias conflict
```

---

## 23.2 Cross-platform 测试

最低 CI：

```text
windows-latest
ubuntu-latest
macos-latest
```

测试内容：

```text
handshake
manifest
invoke
path separator normalization
permission config parsing
entry resolution
sha256
disabled command
```

---

## 24. 文档需求

新增：

```text
docs/adr/ADR-BUKIT-PROJECT-LAYOUT.md
docs/adr/ADR-CORE-PLUGIN-MECHANISM.md
docs/specs/bukit-plugin-config-v1.md
docs/specs/bukit-plugin-manifest-v1.md
docs/specs/bukit-plugin-protocol-v1.md
docs/specs/labs-to-plugin-promotion-gate.md
docs/plans/import-plugin-migration-plan.md
docs/plans/clone-plugin-migration-plan.md
```

更新：

```text
README.md
README.zh-CN.md
guide/user/12-cli-reference.md
guide/dev/plugins.md
guide/dev/architecture.md
guide/skills/README.md
guide/skills/bukit-cli-reference/SKILL.md
guide/skills/using-bukit/SKILL.md
```

---

## 25. 实施阶段

## Phase 1：文档与 ADR

交付：

```text
ADR-BUKIT-PROJECT-LAYOUT.md
ADR-CORE-PLUGIN-MECHANISM.md
bukit-plugin-config-v1.md
bukit-plugin-manifest-v1.md
bukit-plugin-protocol-v1.md
labs-to-plugin-promotion-gate.md
```

---

## Phase 2：目录与 solution 调整

交付：

```text
src/Bukit.PluginHost/
src/Bukit.Plugin.Abstractions/
plugins/
labs/
bukit.plugins.slnx
bukit.labs.slnx
bukit.all.slnx
```

---

## Phase 3：Plugin Abstractions

交付：

```text
protocol DTO
manifest model
config model
permission model
result model
schema context
```

---

## Phase 4：PluginHost 最小实现

交付：

```text
plugins.yaml loader
plugin.yaml loader
path validator
platform resolver
sha256 validator
process invoker
handshake
manifest
invoke
execution report
plugins.lock.yaml
```

---

## Phase 5：Core CLI 接入插件命令

交付：

```text
BukitCliComposer
PluginCommandDescriptorFactory
disabled command diagnostic
command conflict detection
plugin list command
```

---

## Phase 6：Import Plugin 迁移

交付：

```text
plugins/Bukit.Plugin.Import
plugins/import runtime package layout
import plugin tests
import plugin docs
```

---

## Phase 7：Clone Plugin 迁移

交付：

```text
src/Bukit.Clone
plugins/Bukit.Plugin.Clone
clone plugin tests
clone plugin docs
```

---

## 26. 验收标准

Core Plugin Mechanism v1 完成后，应满足：

```text
1. Core 可读取 .bukit/plugins.yaml。
2. Core 只允许 plugins/<id>/ 作为插件来源。
3. Core 拒绝 .bukit 内任何 executable。
4. Core 可读取 plugins/<id>/plugin.yaml。
5. Core 可解析当前平台入口。
6. Core 可校验 sha256。
7. Core 可执行 handshake。
8. Core 可执行 manifest。
9. Core 可注册插件命令。
10. Core 可 invoke 插件命令。
11. 禁用插件时命令提示清晰。
12. 插件执行报告写入 .bukit/reports/。
13. 插件 lock 写入 .bukit/plugins.lock.yaml。
14. Core Native AOT 发布不被破坏。
15. 跨 Windows / Linux / macOS 测试通过。
```

---

## 27. 最终结论

Bukit Core 插件机制 v1 应定义为：

```text
语言无关、跨平台、外部进程插件机制
```

核心边界：

```text
Core
  稳定基础底座
  插件宿主
  Core 内置插件

Plugin
  已成熟正式功能
  外部进程
  语言无关
  跨平台
  可启用/禁用
  存放于 plugins/

Labs
  未成熟孵化区
  不直接发布
  成熟后迁入 Plugin

.bukit
  系统配置、锁文件、报告、缓存、日志、状态
  禁止存放插件程序
```

本机制是 Bukit 后续二级开发、插件生态、Import/Clone 正式化、BukitJalil 调用、AI Agent 工作流扩展的基础。
