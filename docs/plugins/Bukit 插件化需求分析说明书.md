# Bukit 插件化需求分析说明书

> 文档版本：v3.0
> 适用项目：Bukit 静态网站生成引擎
> 文档类型：需求分析说明书
> 目标方向：Core Plugin Host + 跨平台外部进程插件体系
> 核心原则：Core 稳定底座，Labs 功能孵化，Plugin 正式发布

---

## 1. 文档目的

本文档用于重新定义 Bukit 的插件化需求、边界、目录结构、配置规范、插件生命周期与后续实施方向。

本说明书不是直接开发任务清单，而是后续编写以下文档的基础：

* Bukit Core 插件机制设计文档
* Bukit 插件协议 v1 规范
* Bukit 插件配置规范
* Bukit Labs → Plugin → Core 发布准入规范
* Bukit Import 插件迁移计划
* Bukit Clone 插件迁移计划
* Bukit 插件目录结构 ADR
* Bukit 插件安全模型 ADR

---

## 2. 背景说明

Bukit 当前已经形成了较清晰的分层趋势：

```text
Core
  稳定基础能力

Labs
  实验功能与未发布模块

Future Plugins
  成熟后可发布的功能模块
```

当前 Core CLI 主要负责稳定命令，例如：

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

当前 Labs 中存在尚未正式发布的实验能力，例如：

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

这些能力不应长期停留在 Labs 中。成熟后的功能应迁移为正式插件，并通过 Core Plugin Host 接入 Core CLI 进行发布。

因此，Bukit 插件化的真正目标不是简单地把代码移动到 `plugins/` 目录，而是建立一套稳定、跨平台、安全、语言无关、可发布的插件机制。

---

## 3. 核心定义

## 3.1 Core

Core 是 Bukit 的稳定基础底座。

Core 包含：

* CLI 宿主
* 插件宿主
* 插件协议
* 插件配置解析
* 插件生命周期管理
* 构建引擎
* 配置系统
* 内容源抽象
* 路由系统
* 渲染系统
* 主题系统
* Core 内置插件
* 诊断系统
* 安全策略
* Native AOT 发布能力

Core 不应成为所有功能的堆积区。

Core 的职责是：

```text
提供稳定内核
提供插件接入机制
提供正式发布入口
```

---

## 3.2 Core 内置插件

Core 内置插件是编译进 Core 的 in-process 插件。

适用范围：

* 基础构建能力
* 不适合作为独立外部进程的核心渲染扩展
* 与构建引擎强绑定的基础功能

示例：

```text
taxonomy
archive
pagination
menu
alias
related-content
image-processing
pages-index
data-files
```

Core 内置插件继续由 `site.plugins` 控制启用和禁用。

---

## 3.3 Plugin

Plugin 是已成熟、可发布、可启用、可禁用、可由 Core CLI 接入的正式功能模块。

除 Core 内置插件外，所有正式插件必须采用：

```text
跨平台外部进程插件
```

正式插件可以由任意语言开发，例如：

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
```

但必须满足：

```text
可跨平台运行
实现 bukit-plugin-v1 JSON 协议
作为外部进程被 Core 启动
提供插件 manifest
声明平台入口
声明权限需求
通过 Core Plugin Host 校验
```

---

## 3.4 Labs

Labs 是未成熟功能孵化区。

Labs 中的功能具有以下特点：

* 命令可能不稳定
* 参数可能调整
* 数据结构可能变化
* 测试可能不完整
* 文档可能不完整
* 不进入正式发布
* 可进行破坏性修改

Labs 不是长期运行形态。

成熟后的 Labs 功能必须迁移为正式 Plugin。

---

## 4. 插件生命周期

Bukit 功能模块生命周期定义如下：

```text
Idea / Prototype
  ↓
Labs 实验开发
  ↓
功能稳定
  ↓
领域逻辑抽离
  ↓
封装为跨平台外部进程插件
  ↓
接入 Core Plugin Host
  ↓
通过发布准入门禁
  ↓
进入正式发布
```

## 4.1 Labs 阶段

Labs 阶段允许：

* 快速试验
* 参数频繁变化
* 内部 API 变化
* 使用临时实现
* 使用 permissive parsing
* 不进入 Core 文档

Labs 阶段不允许：

* 作为正式功能宣传
* 默认进入 Core CLI
* 被 Core 直接依赖
* 被正式 Plugin 依赖
* 绕过插件准入门禁

---

## 4.2 Plugin 阶段

Plugin 阶段要求：

* 命令稳定
* 参数稳定
* manifest 稳定
* 协议稳定
* 跨平台入口完整
* 测试完整
* 文档完整
* 安全策略明确
* 可启用/禁用
* 可由 Core CLI 接入
* 可发布

---

## 4.3 Core 发布阶段

当插件进入 Core 发布阶段后：

* Core CLI 可以暴露该插件命令
* 插件可通过 `plugins/` 安装
* 插件可通过 `.bukit/plugins.yaml` 启用或禁用
* 插件执行记录写入 `.bukit/reports/`
* 插件解析结果写入 `.bukit/plugins.lock.yaml`
* 插件行为纳入 CI / Release Gate

---

## 5. 插件化总目标

Bukit 插件化目标包括：

1. 建立 Core Plugin Host。
2. 建立语言无关的外部进程插件协议。
3. 建立跨平台插件包规范。
4. 建立 `.bukit/plugins.yaml` 插件配置规范。
5. 建立 `plugins/` 插件程序目录规范。
6. 建立插件权限模型。
7. 建立插件执行报告机制。
8. 建立 Labs → Plugin → Core 发布准入机制。
9. 迁移成熟 Labs 功能为正式插件。
10. 保持 Core 稳定、轻量、可维护。
11. 保持 Native AOT 友好。
12. 不恢复动态 DLL 插件机制。
13. 不将插件程序放入 `.bukit/`。
14. 不将正式插件与 Labs 混用。

---

## 6. 目录边界需求

## 6.1 Bukit 仓库源码目录

推荐 Bukit 仓库最终结构：

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
│   ├── Bukit.Cli.Tests/
│   ├── Bukit.Cli.Shared.Tests/
│   ├── Bukit.PluginHost.Tests/
│   ├── Bukit.Plugin.Import.Tests/
│   ├── Bukit.Plugin.Clone.Tests/
│   ├── Bukit.Labs.Cli.Tests/
│   ├── Bukit.Labs.Import.Tests/
│   └── Bukit.Labs.Clone.Tests/
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

用户站点项目目录推荐如下：

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

## 7. `.bukit/` 目录定义

`.bukit/` 是 Bukit 系统工作目录。

它只能存放：

* 系统配置
* 锁文件
* 缓存
* 报告
* 日志
* 临时文件
* 状态文件
* 执行记录

允许内容：

```text
.bukit/plugins.yaml
.bukit/plugins.lock.yaml
.bukit/reports/
.bukit/cache/
.bukit/logs/
.bukit/tmp/
.bukit/state/
```

禁止内容：

```text
.bukit/plugins/
.bukit/bin/
.bukit/tools/
.bukit/plugin-executables/
.bukit/*.exe
.bukit/*.dll
.bukit/*.sh
.bukit/*.cmd
.bukit/*.ps1
```

Core Plugin Host 必须拒绝任何位于 `.bukit/` 内的插件程序或执行入口。

---

## 8. `plugins/` 目录定义

`plugins/` 是项目根目录下的插件程序目录。

它用于存放：

* 插件包
* 插件 manifest
* 跨平台可执行程序
* 插件资源
* 插件 README
* 插件 schema

正式插件必须存放在：

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

第一版不允许插件来源指向：

```text
../plugins
/tmp/plugin
~/.bukit/plugins
.bukit/plugins
node_modules/.bin
absolute/path/to/plugin
```

---

## 9. 插件配置需求

## 9.1 `.bukit/plugins.yaml`

`.bukit/plugins.yaml` 是项目级插件启用配置。

它不直接存放可执行入口。

它只声明：

* 插件是否启用
* 插件来源目录
* 是否暴露命令
* 权限配置
* 运行策略

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

## 9.2 `plugins/<id>/plugin.yaml`

`plugin.yaml` 是插件包 manifest。

它声明：

* 插件 ID
* 插件名称
* 插件版本
* 协议版本
* 分发类型
* 支持平台
* 平台入口
* sha256
* 命令清单
* 权限需求

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

## 9.3 `.bukit/plugins.lock.yaml`

`.bukit/plugins.lock.yaml` 用于记录插件解析结果。

它可以记录：

* 插件来源
* 插件版本
* 当前平台
* 实际入口
* sha256
* 解析时间
* manifest 摘要

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

锁文件可以记录入口路径，但不能存放插件程序。

---

## 10. 插件协议需求

Core 外部进程插件必须实现：

```text
bukit-plugin-v1
```

第一版协议至少包含：

```text
handshake
manifest
invoke
```

后续可扩展：

```text
hook
install
upgrade
doctor
```

---

## 10.1 handshake

插件用于声明自身身份和协议兼容性。

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

## 10.2 manifest

插件用于返回命令和权限声明。

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

## 10.3 invoke

Core 调用插件命令。

请求：

```json
{
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

## 11. 插件跨平台需求

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

插件 manifest 必须声明支持平台：

```yaml
platforms:
  win-x64:
    entry: bin/win-x64/plugin.exe
    sha256: "<hash>"
  linux-x64:
    entry: bin/linux-x64/plugin
    sha256: "<hash>"
  osx-arm64:
    entry: bin/osx-arm64/plugin
    sha256: "<hash>"
```

如果某插件只支持单个平台，不能作为正式插件进入 Core 发布体系，只能留在 Labs 或标记为 platform-limited preview。

---

## 12. 插件语言无关需求

Core 不限制插件开发语言。

插件可以使用：

```text
.NET
Go
Rust
Node.js
Python
Deno
Bun
Java
Kotlin
Swift
C++
```

但正式插件必须满足：

1. 可作为外部进程运行。
2. 实现 bukit-plugin-v1 协议。
3. 提供跨平台入口。
4. 不依赖 shell 特性。
5. 不要求 Core 理解其语言运行时。
6. 不要求 Core 引用插件代码。
7. 不要求 Core 加载插件 DLL。
8. 不破坏 Native AOT。

推荐正式插件采用 self-contained distribution。

不推荐正式插件依赖用户本机安装 Node、Python、Java 或其他 runtime。

---

## 13. 插件执行需求

Core 启动插件时必须：

* 不使用 shell 拼接命令
* 使用 `UseShellExecute = false`
* 重定向 stdin
* 重定向 stdout
* 重定向 stderr
* 通过 stdin 或 request file 传输 JSON
* 对 stdout / stderr 设置大小限制
* 对进程设置 timeout
* 解析插件 JSON response
* 写入执行报告
* 处理非零 exit code
* 处理无效 JSON
* 处理超时
* 处理 sha256 mismatch
* 处理 unsupported platform

禁止：

```text
sh -c
cmd /c
powershell -Command
bash script.sh
直接拼接 shell 字符串
```

---

## 14. 插件安全需求

Core Plugin Host 必须具备以下安全规则。

## 14.1 路径安全

必须拒绝：

```text
source: .bukit/plugins/import
source: ../plugins/import
source: /absolute/path
entry: ../../evil
entry: .bukit/bin/tool
```

第一版只允许：

```text
plugins/<plugin-id>/
```

## 14.2 `.bukit` 执行禁止

任何插件源或可执行入口解析到 `.bukit/` 内，必须失败。

错误示例：

```text
Plugin executable cannot be inside .bukit
```

## 14.3 sha256 校验

正式插件必须支持 sha256 校验。

如果 `plugin.yaml` 中声明 sha256，Core 必须校验。

如果 hash 不匹配，必须拒绝执行。

## 14.4 权限声明

插件必须声明权限：

```yaml
permissions:
  fileSystem:
    read: []
    write: []
  network: false
  environment:
    read: []
```

Core 必须比较：

```text
plugin requiredPermissions
  <=
project granted permissions
```

如果插件要求权限超过项目授予权限，必须拒绝执行。

## 14.5 CI 默认策略

CI 环境中外部插件默认应更严格。

建议：

```text
CI 下默认只允许 sha256 完整、manifest 完整、权限明确的插件运行。
```

---

## 15. 插件命令注册需求

Core CLI 命令面应由两部分组成：

```text
Core stable commands
  +
Enabled plugin commands
```

Core stable commands 示例：

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

Plugin commands 示例：

```text
import
clone
notion
visual
theme
```

如果插件启用并 expose command：

```yaml
plugins:
  import:
    enabled: true
    exposeCommands:
      - import
```

Core CLI 应支持：

```bash
bukit import ...
```

如果插件被禁用，执行：

```bash
bukit import ...
```

应提示：

```text
Command disabled by plugin config: import
```

而不是：

```text
Unknown command: import
```

---

## 16. Core PluginHost 需求

建议新增项目：

```text
src/Bukit.PluginHost/
```

职责：

1. 读取 `.bukit/plugins.yaml`。
2. 校验插件配置。
3. 校验插件来源路径。
4. 读取 `plugins/<id>/plugin.yaml`。
5. 解析当前平台 RID。
6. 解析平台入口。
7. 校验 sha256。
8. 执行 handshake。
9. 读取 manifest。
10. 校验 manifest schema。
11. 校验权限声明。
12. 注册 CLI command descriptor。
13. 调用 invoke。
14. 处理 stdout / stderr。
15. 处理 timeout。
16. 处理非零 exit code。
17. 写入 `.bukit/plugins.lock.yaml`。
18. 写入 `.bukit/reports/plugin-executions/*.json`。

---

## 17. Plugin Abstractions 需求

建议新增项目：

```text
src/Bukit.Plugin.Abstractions/
```

用于存放：

* protocol DTO
* manifest model
* config model
* permission model
* invoke request model
* result model
* schema context
* constants

注意：

第三方插件不强制引用该项目。

只要实现 JSON 协议即可。

---

## 18. 项目依赖关系

推荐依赖：

```text
Bukit.Cli
  -> Bukit.Cli.Shared
  -> Bukit.PluginHost
  -> Bukit.Config
  -> Bukit.Engine
  -> Bukit.Shared

Bukit.PluginHost
  -> Bukit.Plugin.Abstractions
  -> Bukit.Cli.Shared
  -> Bukit.Shared

Bukit.Plugin.Import
  -> Bukit.Plugin.Abstractions
  -> Bukit.Shared
  -> Bukit.Importing

Bukit.Plugin.Clone
  -> Bukit.Plugin.Abstractions
  -> Bukit.Shared
  -> Bukit.Clone
```

禁止依赖：

```text
Bukit.Cli -> Bukit.Plugin.Import
Bukit.Cli -> Bukit.Plugin.Clone
Bukit.Engine -> Bukit.Plugin.Import
Bukit.Engine -> Bukit.Plugin.Clone
Bukit.Plugin.Import -> Bukit.Cli
Bukit.Plugin.Clone -> Bukit.Cli
Plugin -> Labs
Core -> Labs
```

---

## 19. Solution 分层需求

建议维护多个 solution。

## 19.1 Core Solution

```text
bukit.slnx
```

包含：

```text
src/Bukit.Cli
src/Bukit.Cli.Shared
src/Bukit.PluginHost
src/Bukit.Plugin.Abstractions
src/Bukit.Config
src/Bukit.Content
src/Bukit.Engine
src/Bukit.Engine.Abstractions
src/Bukit.Rendering
src/Bukit.Routing
src/Bukit.Shared
src/Bukit.Theme
tests/Core tests
```

## 19.2 Plugin Solution

```text
bukit.plugins.slnx
```

包含：

```text
plugins/Bukit.Plugin.Import
plugins/Bukit.Plugin.Clone
plugins/Bukit.Plugin.Notion
plugins/Bukit.Plugin.Visual
tests/Bukit.Plugin.*.Tests
```

## 19.3 Labs Solution

```text
bukit.labs.slnx
```

包含：

```text
labs/Bukit.Labs.Cli
labs/Bukit.Labs.Import
labs/Bukit.Labs.Clone
tests/Bukit.Labs.*.Tests
```

## 19.4 All Solution

```text
bukit.all.slnx
```

包含：

```text
Core + Plugins + Labs + Tests
```

---

## 20. Labs → Plugin 发布准入需求

Labs 功能迁入正式插件前必须满足：

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

## 21. 第一批插件候选

## 21.1 Import Plugin

来源：

```text
Labs Import
Bukit.Importing
```

目标插件：

```text
plugins/Bukit.Plugin.Import
```

正式命令：

```text
bukit import
```

优先级：最高。

原因：

* 已有领域库 `Bukit.Importing`
* 比 clone 更容易抽离
* 适合验证 Core Plugin Host

---

## 21.2 Clone Plugin

来源：

```text
Labs Clone
```

目标领域库：

```text
src/Bukit.Clone
```

目标插件：

```text
plugins/Bukit.Plugin.Clone
```

正式命令：

```text
bukit clone
```

优先级：第二。

原因：

* 复杂度高于 import
* 包含视觉验证、资源下载、主题生成、行为脚本等
* 应在 import 插件机制跑通后迁移

---

## 21.3 Notion Plugin

目标：

```text
plugins/Bukit.Plugin.Notion
```

正式命令：

```text
bukit notion
```

可作为 import 的辅助插件或独立插件。

---

## 21.4 Visual Plugin

目标：

```text
plugins/Bukit.Plugin.Visual
```

正式命令：

```text
bukit visual
```

建议后置。

---

## 22. 测试需求

## 22.1 PluginHost 测试

必须覆盖：

* `.bukit/plugins.yaml` 加载
* 插件 source 校验
* 禁止 `.bukit` executable
* plugin.yaml 加载
* 当前平台解析
* sha256 校验
* handshake 成功
* handshake 失败
* manifest 成功
* manifest invalid JSON
* invoke 成功
* invoke non-zero exit
* invoke timeout
* stdout 过大
* stderr 过大
* unsupported platform
* disabled plugin
* command conflict
* alias conflict
* permission denied
* plugins.lock.yaml 写入
* execution report 写入

---

## 22.2 跨平台测试

最低测试矩阵：

```text
windows-latest x64
ubuntu-latest x64
macos-latest arm64 或 macos-latest
```

如果条件允许，扩展：

```text
windows-arm64
ubuntu-arm64
macos-x64
```

---

## 22.3 插件协议测试

每个正式插件必须通过：

```text
handshake
manifest
invoke
invalid request
permission denied
timeout
missing config
```

---

## 23. 文档需求

需要新增：

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

需要更新：

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

## 24. 非目标范围

第一阶段不做：

* 插件市场
* 远程插件安装
* 自动下载插件
* 全局插件目录
* Docker 插件
* 动态 DLL 插件
* WASM 插件
* 插件热加载
* 插件自动更新
* 插件依赖解析
* 复杂权限沙箱
* 插件签名服务

第一阶段只做：

```text
项目本地 plugins/ 外部进程插件
```

---

## 25. 实施优先级

推荐执行顺序：

```text
Phase 1：制定 ADR 与 spec
Phase 2：调整仓库目录结构
Phase 3：新增 Bukit.Plugin.Abstractions
Phase 4：新增 Bukit.PluginHost
Phase 5：实现 .bukit/plugins.yaml loader
Phase 6：实现 plugins/<id>/plugin.yaml loader
Phase 7：实现 handshake / manifest / invoke
Phase 8：Core CLI 接入 enabled plugin commands
Phase 9：迁移 Import Plugin
Phase 10：迁移 Clone Plugin
Phase 11：补齐 CI / docs / skills
```

---

## 26. 关键约束总结

必须遵守：

```text
1. Core 是稳定底座和插件宿主。
2. Core 内置插件可以 in-process。
3. 除 Core 内置插件外，所有正式插件都是外部进程。
4. 外部进程插件不限制开发语言。
5. 外部进程插件必须跨平台。
6. 外部进程插件必须实现 bukit-plugin-v1。
7. 插件程序必须放在项目根目录 plugins/。
8. .bukit 只能放系统配置、报告、缓存、锁文件、日志、状态。
9. .bukit 内禁止存放任何可执行插件程序。
10. Labs 是未发布孵化区。
11. 成熟功能必须 Labs → Plugin → Core Host。
12. Core 不依赖 Plugin 实现。
13. Plugin 不依赖 Labs。
14. 不恢复动态 DLL 插件。
15. 不恢复 site.externalPlugins。
```

---

## 27. 最终结论

Bukit 插件化的新版需求应定义为：

```text
Core Plugin Host + Language-Agnostic Cross-Platform External Process Plugin System
```

中文可描述为：

```text
Bukit Core 语言无关、跨平台外部进程插件体系
```

它的目标不是让 Core 直接包含更多功能，而是让 Core 具备正式插件接入能力。

最终形态：

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

Labs
  未成熟功能孵化区
  不直接发布
```

第一批应迁移的正式插件：

```text
1. Import Plugin
2. Clone Plugin
3. Notion Plugin
4. Visual Plugin
```

但在迁移这些插件之前，必须先完成：

```text
Core Plugin Mechanism v1
```

这才是后续 Bukit 二级开发、功能扩展、插件生态、BukitJalil 接入的基础。
