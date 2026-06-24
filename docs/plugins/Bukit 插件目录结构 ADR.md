# ADR: Bukit 插件目录结构规范

> ADR 编号：ADR-BUKIT-PLUGIN-DIRECTORY-LAYOUT
> 状态：Proposed
> 日期：2026-06-18
> 适用项目：Bukit 静态网站生成引擎
> 决策类型：架构目录结构 / 插件边界 / 安全边界
> 关联文档：
>
> * Bukit Core 插件机制设计文档
> * Bukit 插件协议 v1 规范
> * Bukit 插件配置规范
> * Bukit Labs → Plugin → Core 发布准入规范
> * Bukit Import 插件迁移计划
> * Bukit Clone 插件迁移计划

---

## 1. 背景

Bukit 正在从单一 CLI 工具演进为可扩展的静态网站生成平台内核。

新的插件体系要求：

```text
Core = 稳定基础底座 + 插件宿主 + Core 内置插件

Plugin = 已成熟、正式发布、跨平台外部进程插件

Labs = 未成熟功能孵化区
```

同时，新的 Core 插件机制要求：

```text
1. 除 Core 内置插件外，所有正式插件均采用外部进程插件。
2. 外部插件不限制开发语言。
3. 外部插件必须具备跨平台能力。
4. 插件通过 bukit-plugin-v1 JSON 协议与 Core 通信。
5. 插件程序不得放在 .bukit/ 内。
6. 插件程序必须放在项目根目录 plugins/ 下。
7. .bukit/ 只用于系统配置、锁文件、报告、缓存、日志、临时文件和状态文件。
```

因此，需要通过 ADR 明确 Bukit 仓库内部目录结构、用户项目目录结构、插件源码目录、插件运行目录、Labs 孵化目录和 `.bukit/` 系统目录之间的边界。

---

## 2. 问题

当前需要解决以下问题：

1. Bukit 官方插件源码应放在哪里？
2. 用户项目中的插件程序应放在哪里？
3. `.bukit/` 是否可以存放插件程序？
4. Labs 是否应继续使用 `experimental/` 目录，还是应独立为 `labs/`？
5. Core、Plugin、Labs 的源码边界如何定义？
6. Core 是否可以直接引用正式插件实现？
7. Plugin 是否可以依赖 Labs？
8. 是否需要单独的 PluginHost 项目？
9. 是否需要单独的 Plugin.Abstractions 项目？
10. 是否需要拆分 Core / Plugins / Labs solution？
11. 如何保证插件目录结构可审计、可测试、可发布、跨平台、安全？

---

## 3. 决策

### 3.1 Bukit 仓库采用四层源码目录

Bukit 仓库顶层目录应划分为：

```text
src/
plugins/
labs/
tests/
```

含义如下：

| 目录         | 含义                         |
| ---------- | -------------------------- |
| `src/`     | Core 稳定基础底座源码              |
| `plugins/` | 官方正式插件源码                   |
| `labs/`    | 未成熟实验功能源码                  |
| `tests/`   | Core / Plugins / Labs 测试项目 |

---

### 3.2 `src/` 只存放 Core 稳定底座

`src/` 用于存放 Bukit Core 稳定能力。

推荐结构：

```text
src/
├── Bukit.Cli/
├── Bukit.Cli.Shared/
├── Bukit.PluginHost/
├── Bukit.Plugin.Abstractions/
├── Bukit.Config/
├── Bukit.Content/
├── Bukit.Engine/
├── Bukit.Engine.Abstractions/
├── Bukit.Rendering/
├── Bukit.Routing/
├── Bukit.Shared/
└── Bukit.Theme/
```

`src/` 中的项目代表 Core 基础能力，不应包含正式业务功能插件实现。

---

### 3.3 `plugins/` 存放官方正式插件源码

Bukit 仓库根目录下的 `plugins/` 用于存放官方正式插件源码。

推荐结构：

```text
plugins/
├── Bukit.Plugin.Import/
├── Bukit.Plugin.Clone/
├── Bukit.Plugin.Notion/
├── Bukit.Plugin.Visual/
├── Bukit.Plugin.Theme/
└── Bukit.Plugin.Deploy/
```

每个正式插件必须是独立外部进程项目。

示例：

```text
plugins/Bukit.Plugin.Import/
├── Bukit.Plugin.Import.csproj
├── Program.cs
├── ImportPluginApp.cs
├── ImportPluginManifestProvider.cs
├── ImportPluginInvoker.cs
├── plugin.yaml.template
└── README.md
```

正式插件不得作为 class library 被 Core 直接引用。

---

### 3.4 `labs/` 存放未成熟功能

Labs 是未发布功能孵化区。

推荐将现有 `experimental/` 逐步迁移为：

```text
labs/
├── Bukit.Labs.Cli/
├── Bukit.Labs.Import/
├── Bukit.Labs.Clone/
├── Bukit.Labs.Intent/
├── Bukit.Labs.Visual/
├── Bukit.Labs.Webhook/
└── Bukit.Labs.Theme/
```

Labs 中的功能：

```text
可以快速变化
可以破坏性调整
可以不稳定
不可作为正式发布功能
不可默认进入 Core CLI
不可被正式 Plugin 依赖
不可被 Core 直接依赖
```

成熟功能必须迁移到 `plugins/` 或先抽出领域库到 `src/`。

---

### 3.5 用户项目中的插件程序必须放在根目录 `plugins/`

用户站点项目中的插件包必须存放在项目根目录：

```text
project-root/plugins/<plugin-id>/
```

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
    ├── cache/
    ├── logs/
    ├── tmp/
    └── state/
```

---

### 3.6 `.bukit/` 是系统工作目录，不得存放插件程序

`.bukit/` 的定义：

```text
Bukit 系统配置、锁文件、缓存、报告、日志、临时文件、状态文件目录
```

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
.bukit/*.bat
.bukit/*.ps1
```

Core Plugin Host 必须拒绝任何解析到 `.bukit/` 内的插件程序或可执行入口。

---

### 3.7 `.bukit/plugins.yaml` 只声明插件启用配置

`.bukit/plugins.yaml` 是项目级插件启用配置。

它只声明：

```text
插件是否启用
插件 source
暴露命令
权限授权
超时策略
输出限制
CI 策略
失败策略
```

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
```

`.bukit/plugins.yaml` 不得直接声明可执行入口：

```yaml
entry: plugins/import/bin/osx-arm64/bukit-plugin-import
```

真实执行入口必须由：

```text
plugins/<plugin-id>/plugin.yaml
```

中的 `platforms.<rid>.entry` 解析。

---

### 3.8 `plugins/<id>/plugin.yaml` 是插件包 manifest

插件包自身必须提供：

```text
plugins/<plugin-id>/plugin.yaml
```

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
    sha256: "<sha256>"

  linux-x64:
    entry: bin/linux-x64/bukit-plugin-import
    sha256: "<sha256>"

  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-import
    sha256: "<sha256>"

commands:
  - name: import
    description: Import external content into Bukit

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

### 3.9 新增 `Bukit.PluginHost` 项目

新增：

```text
src/Bukit.PluginHost/
```

职责：

```text
读取 .bukit/plugins.yaml
校验插件配置
校验 source 路径
读取 plugins/<id>/plugin.yaml
解析平台入口
校验 sha256
执行 handshake
读取 manifest
注册 CLI command descriptor
执行 invoke
写入 plugins.lock.yaml
写入 plugin execution report
处理 timeout / stderr / stdout / exit code
```

`Bukit.Cli` 只调用 `Bukit.PluginHost`，不直接管理插件执行细节。

---

### 3.10 新增 `Bukit.Plugin.Abstractions` 项目

新增：

```text
src/Bukit.Plugin.Abstractions/
```

职责：

```text
定义 protocol DTO
定义 manifest model
定义 config model
定义 permission model
定义 invoke request / response model
定义 plugin constants
定义 schema context
```

第三方插件不强制引用 `Bukit.Plugin.Abstractions`。

只要实现 `bukit-plugin-v1` JSON 协议即可。

---

### 3.11 Core 不依赖插件实现

禁止依赖：

```text
Bukit.Cli -> Bukit.Plugin.Import
Bukit.Cli -> Bukit.Plugin.Clone
Bukit.PluginHost -> Bukit.Plugin.Import
Bukit.PluginHost -> Bukit.Plugin.Clone
Bukit.Engine -> Bukit.Plugin.Import
Bukit.Engine -> Bukit.Plugin.Clone
```

正确关系：

```text
Bukit.Cli
  -> Bukit.PluginHost
      -> external process: plugins/<id>/bin/<rid>/...
```

---

### 3.12 Plugin 不依赖 Labs

正式插件不得依赖 Labs。

禁止：

```text
Bukit.Plugin.Import -> Bukit.Labs.Import
Bukit.Plugin.Clone -> Bukit.Labs.Clone
Bukit.Plugin.Notion -> Bukit.Labs.Cli
```

如果需要复用 Labs 逻辑，必须先抽出稳定领域库：

```text
src/Bukit.Importing
src/Bukit.Clone
src/Bukit.Notion
```

或抽到：

```text
src/Bukit.Shared
src/Bukit.Plugin.Abstractions
```

---

### 3.13 Labs 不进入正式发布

Labs 代码不应进入正式发布包。

Labs 不应被 Core 默认加载。

Labs 不应出现在正式 CLI reference 中作为稳定命令。

---

## 4. 目录结构决策详情

## 4.1 Bukit 仓库最终推荐结构

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
│   ├── Bukit.Plugin.Abstractions.Tests/
│   ├── Bukit.Plugin.Import.Tests/
│   ├── Bukit.Plugin.Clone.Tests/
│   ├── Bukit.Labs.Cli.Tests/
│   ├── Bukit.Labs.Import.Tests/
│   └── Bukit.Labs.Clone.Tests/
│
├── docs/schemas/
│   ├── bukit-plugin-config.v1.schema.json
│   ├── bukit-plugin-manifest.v1.schema.json
│   ├── bukit-plugin-handshake.v1.schema.json
│   ├── bukit-plugin-invoke.v1.schema.json
│   └── bukit-plugin-result.v1.schema.json
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

## 5. Solution 分层决策

### 5.1 Core Solution

文件：

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

用途：

```text
Core build
Core test
Core AOT publish
Core release gate
```

---

### 5.2 Plugin Solution

文件：

```text
bukit.plugins.slnx
```

包含：

```text
plugins/Bukit.Plugin.Import
plugins/Bukit.Plugin.Clone
plugins/Bukit.Plugin.Notion
plugins/Bukit.Plugin.Visual
plugins/Bukit.Plugin.Theme
tests/Bukit.Plugin.*.Tests
```

用途：

```text
官方插件开发
插件协议测试
插件跨平台 package build
```

---

### 5.3 Labs Solution

文件：

```text
bukit.labs.slnx
```

包含：

```text
labs/Bukit.Labs.Cli
labs/Bukit.Labs.Import
labs/Bukit.Labs.Clone
labs/Bukit.Labs.Intent
labs/Bukit.Labs.Visual
tests/Bukit.Labs.*.Tests
```

用途：

```text
实验功能开发
不进入 Core release gate
可单独运行 labs CI
```

---

### 5.4 All Solution

文件：

```text
bukit.all.slnx
```

包含：

```text
Core + Plugins + Labs + Tests
```

用途：

```text
本地全量开发
Nightly CI
整体兼容性检查
```

---

## 6. 用户项目目录决策

用户项目根目录中：

```text
plugins/
```

用于存放实际插件程序包。

```text
.bukit/
```

用于存放系统配置、锁文件、报告、缓存、日志和状态。

二者不可混用。

---

## 7. 安全决策

### 7.1 插件 source 必须在 `plugins/`

`.bukit/plugins.yaml` 中：

```yaml
source: plugins/import
```

是合法的。

以下非法：

```yaml
source: .bukit/plugins/import
source: ../plugins/import
source: /absolute/path
source: node_modules/.bin
source: /tmp/plugin
```

---

### 7.2 插件 entry 不得路径穿越

`plugins/<id>/plugin.yaml` 中：

```yaml
entry: bin/osx-arm64/bukit-plugin-import
```

是合法的。

以下非法：

```yaml
entry: ../../evil
entry: /usr/local/bin/tool
entry: .bukit/bin/tool
entry: C:\tools\plugin.exe
```

---

### 7.3 `.bukit` 内不得执行任何程序

Core Plugin Host 必须硬拒绝：

```text
.bukit/**/*.exe
.bukit/**/*.dll
.bukit/**/*.sh
.bukit/**/*.cmd
.bukit/**/*.bat
.bukit/**/*.ps1
```

无论该路径来自：

```text
plugins.yaml
plugin.yaml
plugins.lock.yaml
runtime manifest
```

都必须拒绝。

---

## 8. 架构依赖决策

### 8.1 允许依赖

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

Bukit.Labs.*
  -> Bukit.Shared
  -> Bukit.Cli.Shared
  -> Core libraries as needed
```

---

### 8.2 禁止依赖

```text
Core -> Plugin implementation
Core -> Labs
Plugin -> Labs
PluginHost -> Plugin implementation
Engine -> Plugin implementation
Cli -> Plugin implementation
```

---

## 9. 迁移决策

### 9.1 `experimental/` 迁移为 `labs/`

现有实验目录应逐步迁移：

```text
experimental/Bukit.Labs.Cli
```

到：

```text
labs/Bukit.Labs.Cli
```

该迁移应单独执行，不与 Import / Clone 正式插件迁移混在一个 PR 中。

---

### 9.2 Import 迁移路径

```text
labs/Bukit.Labs.Cli/Commands/Import
  ↓
src/Bukit.Importing
  ↓
plugins/Bukit.Plugin.Import
```

---

### 9.3 Clone 迁移路径

```text
labs/Bukit.Labs.Cli/Commands/Clone
  ↓
src/Bukit.Clone
  ↓
plugins/Bukit.Plugin.Clone
```

---

## 10. 后果

### 10.1 正面影响

该目录结构带来的收益：

```text
Core 边界清晰
Plugin 边界清晰
Labs 边界清晰
.bukit 安全边界清晰
plugins/ 可审计
插件程序位置统一
跨平台包结构统一
CI 可分层
Release Gate 可分层
Labs 不污染 Core
Core 不依赖插件实现
插件可独立发布
```

---

### 10.2 代价

该决策带来的成本：

```text
需要新增 PluginHost 项目
需要新增 Plugin.Abstractions 项目
需要维护多个 solution
需要迁移 experimental 到 labs
需要调整 CI
需要调整测试目录
需要更新文档
需要维护插件 package build
```

---

### 10.3 风险

主要风险：

```text
短期目录调整较大
Import / Clone 迁移需要分阶段
PluginHost 未稳定前插件迁移不能开始
旧 Labs 文档可能与新目录冲突
CI 需要重新分层
```

缓解策略：

```text
先写 ADR 和 specs
再新增空目录和 solution
再新增 PluginHost
再迁移 Import
最后迁移 Clone
```

---

## 11. 备选方案

### 11.1 方案 A：插件源码放在 `src/plugins/`

示例：

```text
src/plugins/Bukit.Plugin.Import
```

拒绝原因：

```text
会混淆 Core 与 Plugin 边界
src/ 应只代表 Core 稳定底座
插件是可发布功能模块，不应伪装为 Core
```

---

### 11.2 方案 B：继续使用 `experimental/`

拒绝原因：

```text
experimental 语义不够稳定
Labs 是正式生命周期概念
Labs → Plugin → Core 更清晰
```

---

### 11.3 方案 C：插件程序放 `.bukit/plugins/`

拒绝原因：

```text
.bukit 是系统工作目录
隐藏目录中存放可执行程序会造成安全边界混乱
不利于审计
不利于版本管理
不利于用户理解
```

---

### 11.4 方案 D：Core 直接引用官方插件项目

拒绝原因：

```text
破坏外部进程插件原则
破坏语言无关原则
破坏 Native AOT 边界
导致 Core 与插件实现强耦合
不利于插件独立发布
```

---

## 12. 实施计划

### Phase 1：文档落地

新增：

```text
docs/adr/ADR-BUKIT-PLUGIN-DIRECTORY-LAYOUT.md
```

并同步更新：

```text
docs/adr/ADR-CORE-PLUGIN-MECHANISM.md
docs/specs/bukit-plugin-config-v1.md
docs/specs/bukit-plugin-protocol-v1.md
```

---

### Phase 2：新增目录结构

新增：

```text
plugins/
labs/
schemas/
docs/specs/
docs/plans/
```

---

### Phase 3：新增 Core 插件项目

新增：

```text
src/Bukit.PluginHost/
src/Bukit.Plugin.Abstractions/
```

---

### Phase 4：Solution 分层

新增：

```text
bukit.plugins.slnx
bukit.labs.slnx
bukit.all.slnx
```

更新：

```text
bukit.slnx
```

---

### Phase 5：迁移 Labs

将：

```text
experimental/Bukit.Labs.Cli
```

逐步迁移为：

```text
labs/Bukit.Labs.Cli
```

---

### Phase 6：迁移正式插件

按顺序迁移：

```text
Import Plugin
Clone Plugin
Notion Plugin
Visual Plugin
```

---

## 13. 验收标准

该 ADR 落地后，应满足：

```text
1. src/ 只包含 Core 稳定基础项目。
2. plugins/ 只包含官方正式插件源码项目。
3. labs/ 只包含未成熟实验功能。
4. tests/ 中 Core / Plugin / Labs 测试分层清晰。
5. 用户项目插件程序只允许位于 plugins/<id>/。
6. .bukit/ 不允许存放任何插件程序。
7. Core PluginHost 拒绝 .bukit 内 executable。
8. Core 不引用插件实现。
9. Plugin 不引用 Labs。
10. Labs 不进入正式发布。
11. solution 分层清晰。
12. 文档和 CI 规则与目录结构一致。
```

---

## 14. 最终结论

Bukit 插件目录结构正式定义为：

```text
src/
  Core 稳定底座

plugins/
  官方正式插件源码

labs/
  未成熟功能孵化区

project-root/plugins/
  用户项目中的插件程序包

project-root/.bukit/
  系统配置、锁文件、报告、缓存、日志、状态
```

核心决策：

```text
插件程序不得存放于 .bukit/
正式插件必须存放于 plugins/
Labs 不代表发布功能
Core 不依赖插件实现
Plugin 不依赖 Labs
```

该目录结构是 Bukit Core 插件机制、跨平台外部进程插件体系、Labs → Plugin → Core 发布模型的基础。
