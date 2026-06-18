# Bukit Labs → Plugin → Core 发布准入规范

> 文档版本：v1.0
> 适用项目：Bukit 静态网站生成引擎
> 配套机制：Bukit Core 外部进程插件机制
> 配套协议：`bukit-plugin-v1`
> 文档类型：发布准入规范
> 状态：设计稿
> 优先级：P0

---

## 1. 文档目的

本文档用于定义 Bukit 新功能从实验开发到正式发布的完整准入标准。

Bukit 采用三阶段功能生命周期：

```text
Labs
  ↓
Plugin
  ↓
Core Release
```

其中：

```text
Labs
  = 未成熟功能孵化区

Plugin
  = 已成熟、可发布、可启用/禁用、跨平台外部进程插件

Core
  = 稳定底座、插件宿主、正式发布入口
```

本文档用于确保新功能不会直接从实验代码进入正式发布，而是必须经过明确的工程质量、协议质量、安全质量、跨平台质量、文档质量和测试质量门禁。

---

## 2. 核心原则

## 2.1 Labs 不是发布形态

Labs 中的功能代表：

* 尚未稳定
* 尚未完整测试
* 尚未完整文档
* 尚未完成插件协议适配
* 可进行破坏性修改
* 不能作为正式功能承诺

Labs 功能不得直接进入 Core CLI 正式发布。

---

## 2.2 Plugin 是正式功能模块

Plugin 代表功能已经成熟。

正式 Plugin 必须：

* 采用外部进程插件模式
* 不限制开发语言
* 具备跨平台能力
* 实现 `bukit-plugin-v1`
* 提供 `plugin.yaml`
* 可通过 `.bukit/plugins.yaml` 启用 / 禁用
* 通过 Core Plugin Host 校验和调用
* 有完整测试
* 有完整文档
* 有发布准入记录

---

## 2.3 Core 是稳定底座与插件宿主

Core 不应成为实验功能堆积区。

Core 负责：

* 稳定 CLI
* 插件宿主
* 插件协议
* 插件配置
* 插件安全
* 插件执行
* 插件报告
* 插件发布入口

Core 可以接入正式 Plugin，但不应直接接入 Labs 模块。

---

## 2.4 成熟功能必须迁出 Labs

当 Labs 功能成熟后，必须迁移到正式插件项目中，例如：

```text
labs/Bukit.Labs.Import
  ↓
plugins/Bukit.Plugin.Import
```

或者先抽离领域库：

```text
labs/Bukit.Labs.Clone
  ↓
src/Bukit.Clone
  ↓
plugins/Bukit.Plugin.Clone
```

---

## 3. 生命周期定义

## 3.1 Stage 0：Idea / Prototype

功能仍处于想法或快速原型阶段。

特点：

* 需求可能不完整
* 技术路线可能未确定
* 不要求完整测试
* 不要求完整文档
* 不要求 CLI 稳定
* 不进入发布计划

产物可以是：

```text
设计草案
实验脚本
demo
临时命令
研究文档
```

---

## 3.2 Stage 1：Labs

功能进入 Labs 实验区。

特点：

* 有初步命令入口
* 有初步业务逻辑
* 可以被开发者试用
* 可以破坏性调整
* 不保证参数稳定
* 不保证输出格式稳定
* 不保证跨平台完整
* 不进入 Core 正式命令面

典型路径：

```text
labs/Bukit.Labs.<Feature>/
experimental/Bukit.Labs.Cli/
```

---

## 3.3 Stage 2：Plugin Candidate

Labs 功能准备迁移为正式插件。

要求：

* 功能边界明确
* 命令形态基本稳定
* 领域逻辑可抽离
* 权限需求可描述
* 平台支持计划明确
* 插件协议适配可行
* 已确定是否进入正式发布路线

此阶段应输出：

```text
插件迁移计划
插件权限说明
插件命令规范
插件 manifest 草案
测试计划
文档计划
```

---

## 3.4 Stage 3：Official Plugin

功能成为正式插件。

特点：

* 位于 `plugins/Bukit.Plugin.<Name>/`
* 采用外部进程模式
* 实现 `bukit-plugin-v1`
* 提供跨平台入口
* 提供 `plugin.yaml`
* 可由 Core Plugin Host 加载
* 可通过 `.bukit/plugins.yaml` 启用 / 禁用
* 有完整测试和文档

---

## 3.5 Stage 4：Core Release Integration

正式插件接入 Core 发布体系。

特点：

* Core CLI 可暴露插件命令
* `bukit plugin list` 可显示插件状态
* 插件纳入 Release Gate
* 插件命令写入 CLI Reference
* 插件配置写入用户文档
* 插件行为写入 Skills
* 插件可随 Core 版本一起发布，或作为官方插件包发布

---

## 4. 准入总览

## 4.1 Labs → Plugin Candidate 准入

Labs 功能进入 Plugin Candidate 前必须满足：

| 准入项           | 要求                                  |
| ------------- | ----------------------------------- |
| 需求明确          | 功能目标、用户场景、输入输出清晰                    |
| 命令方向明确        | command / subcommand / options 基本确定 |
| 领域边界初步清晰      | 业务逻辑可与 CLI handler 分离               |
| 安全风险已识别       | 文件、网络、环境变量、执行风险已列出                  |
| 插件化可行         | 可封装为外部进程                            |
| 跨平台计划明确       | 至少明确最低支持平台                          |
| 不依赖 Labs 私有状态 | 后续可迁出 Labs                          |
| 有迁移计划         | 已编写 Plugin Migration Plan           |

---

## 4.2 Plugin Candidate → Official Plugin 准入

Plugin Candidate 成为 Official Plugin 前必须满足：

| 准入项      | 要求                                           |
| -------- | -------------------------------------------- |
| 外部进程化    | 必须作为 external process 运行                     |
| 协议实现     | 必须实现 `handshake / manifest / invoke`         |
| Manifest | 必须提供 `plugins/<id>/plugin.yaml`              |
| 配置接入     | 必须支持 `.bukit/plugins.yaml`                   |
| 权限声明     | 必须声明 `requiredPermissions`                   |
| 权限校验     | requiredPermissions 不得超过 granted permissions |
| 跨平台      | 至少 Windows x64、Linux x64、macOS arm64         |
| 测试完整     | 单元、协议、集成、跨平台、禁用测试                            |
| 错误码稳定    | 必须使用稳定错误码                                    |
| 文档完整     | 用户文档、开发文档、CLI reference 完整                   |
| 安全通过     | 路径、环境变量、sha256、timeout 校验通过                  |
| AOT 不受影响 | 不破坏 Core Native AOT                          |
| 不依赖 Labs | Official Plugin 不得依赖 Labs 项目                 |

---

## 4.3 Official Plugin → Core Release 准入

Official Plugin 接入 Core Release 前必须满足：

| 准入项                 | 要求                                          |
| ------------------- | ------------------------------------------- |
| Core Plugin Host 兼容 | 可被 Core Plugin Host 加载                      |
| CLI 命令可注册           | manifest commands 可转换为 CommandDescriptor    |
| 命令无冲突               | 不覆盖 Core command，不与其他插件冲突                   |
| 禁用行为正确              | disabled plugin 命令提示清晰                      |
| Lock 文件正确           | 可写入 `.bukit/plugins.lock.yaml`              |
| 执行报告正确              | 可写入 `.bukit/reports/plugin-executions/`     |
| Release Gate 通过     | build/test/aot/cross-platform/doc checks 通过 |
| 文档发布                | README、guide、skills 已更新                     |
| 版本策略明确              | 插件版本与 Core 版本兼容策略明确                         |
| 回滚策略明确              | 插件失败时可禁用或回滚                                 |

---

## 5. Labs 阶段要求

## 5.1 Labs 允许事项

Labs 中可以允许：

```text
命令参数不稳定
输出格式不稳定
临时文件结构
临时实现
permissive parsing
不完整文档
不完整测试
功能快速重构
```

---

## 5.2 Labs 禁止事项

Labs 中禁止：

```text
默认进入 Core CLI
作为正式功能对外宣传
被 Core 直接引用
被 Official Plugin 引用
写入 Core CLI Reference 作为稳定命令
绕过安全门禁
绕过测试门禁
绕过发布准入
```

---

## 5.3 Labs 文档要求

Labs 功能至少需要一份说明文档：

```text
guide/labs/<feature>.md
```

文档必须包含：

```text
Status: Labs / not Core
Feature goal
Current limitations
Known risks
Promotion requirements
```

---

## 6. Plugin Candidate 阶段要求

## 6.1 必须输出迁移计划

每个 Plugin Candidate 必须有迁移计划：

```text
docs/plans/<feature>-plugin-migration-plan.md
```

迁移计划必须包含：

* 当前 Labs 代码位置
* 目标 Plugin 项目位置
* 是否需要领域库
* 命令设计
* 配置设计
* Manifest 设计
* 权限设计
* 协议实现计划
* 测试计划
* 文档计划
* 风险与回滚方案

---

## 6.2 必须完成领域边界梳理

需要判断是否应抽出领域库。

例如：

```text
Import:
  src/Bukit.Importing
  plugins/Bukit.Plugin.Import

Clone:
  src/Bukit.Clone
  plugins/Bukit.Plugin.Clone
```

原则：

```text
CLI handler 只做参数绑定、协议输入输出、服务调用。
业务核心逻辑应进入领域库或插件内部 service。
```

---

## 6.3 必须完成权限分析

每个插件必须明确：

```text
是否读文件
读哪些目录
是否写文件
写哪些目录
是否访问网络
是否读取环境变量
是否执行外部命令
是否生成报告
是否修改 site.yaml
```

输出：

```text
requiredPermissions 草案
granted permissions 示例
安全风险说明
```

---

## 7. Official Plugin 项目要求

## 7.1 项目位置

正式插件源码必须位于：

```text
plugins/Bukit.Plugin.<Name>/
```

示例：

```text
plugins/Bukit.Plugin.Import/
plugins/Bukit.Plugin.Clone/
plugins/Bukit.Plugin.Notion/
plugins/Bukit.Plugin.Visual/
```

---

## 7.2 插件项目类型

正式插件必须是可执行项目。

例如 .NET 插件：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

但协议不限制语言。

Go / Rust / Node / Python / Java 等语言也可实现插件，只要最终能提供跨平台可执行入口。

---

## 7.3 插件不得被 Core 引用

禁止：

```text
Bukit.Cli -> Bukit.Plugin.Import
Bukit.Engine -> Bukit.Plugin.Import
Bukit.PluginHost -> Bukit.Plugin.Import
```

正确关系：

```text
Bukit.Cli -> Bukit.PluginHost -> external process

Bukit.Plugin.Import
  独立编译
  独立运行
  通过 JSON 协议与 Core 通信
```

---

## 7.4 插件不得依赖 Labs

禁止：

```text
Bukit.Plugin.Import -> Bukit.Labs.Import
Bukit.Plugin.Clone -> Bukit.Labs.Clone
```

如果需要复用 Labs 逻辑，应将稳定部分抽入：

```text
src/Bukit.Importing
src/Bukit.Clone
src/Bukit.Plugin.Abstractions
src/Bukit.Shared
```

---

## 8. 插件包要求

## 8.1 用户项目插件包位置

正式插件安装包必须放在用户项目根目录：

```text
plugins/<plugin-id>/
```

示例：

```text
plugins/import/
plugins/clone/
```

---

## 8.2 插件包结构

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

## 8.3 `.bukit` 禁止存放插件程序

禁止：

```text
.bukit/plugins/
.bukit/bin/
.bukit/tools/
.bukit/*.exe
.bukit/*.dll
.bukit/*.sh
.bukit/*.cmd
.bukit/*.ps1
```

Core Plugin Host 必须拒绝从 `.bukit` 内执行任何程序。

---

## 9. 插件 Manifest 准入

正式插件必须提供：

```text
plugins/<id>/plugin.yaml
```

必须包含：

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
    read: []
```

---

## 10. 协议准入

正式插件必须实现：

```text
bukit-plugin-v1
```

至少支持：

```text
handshake
manifest
invoke
```

## 10.1 handshake 准入

必须返回：

* plugin id
* plugin name
* plugin version
* protocol
* platform
* capabilities

## 10.2 manifest 准入

必须返回：

* commands
* subcommands
* options
* arguments
* requiredPermissions

## 10.3 invoke 准入

必须支持：

* command path
* arguments
* options
* context
* permissions
* exitCode
* messages
* diagnostics
* artifacts

---

## 11. 跨平台准入

正式插件最低必须通过：

```text
windows-x64
linux-x64
osx-arm64
```

推荐通过：

```text
windows-x64
windows-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

跨平台测试必须覆盖：

* handshake
* manifest
* invoke
* 路径分隔符
* 可执行入口解析
* sha256 校验
* 禁用插件
* timeout
* invalid JSON
* non-zero exit

---

## 12. 安全准入

正式插件必须通过以下安全门禁。

## 12.1 路径安全

必须拒绝：

```text
source: .bukit/plugins/import
source: ../plugins/import
source: /absolute/path
entry: ../../evil
entry: .bukit/bin/tool
```

## 12.2 权限安全

必须满足：

```text
requiredPermissions <= grantedPermissions
```

## 12.3 环境变量安全

必须满足：

```text
只读取 allowlist 环境变量
执行报告中 secret 打码
不得支持 environment.read: ["*"]
```

## 12.4 网络安全

如果插件需要网络，必须明确：

```yaml
network: true
```

如果配置为：

```yaml
network: false
```

插件不得声明网络需求。

## 12.5 执行安全

必须满足：

```text
不使用 shell 拼接命令
不依赖 bash/cmd/powershell
不从 .bukit 执行程序
stdout 只输出 JSON
stderr 用于日志
```

---

## 13. CLI 准入

正式插件命令进入 Core CLI 前必须满足：

* 命令名稳定
* 子命令稳定
* 参数稳定
* option 类型稳定
* required option 明确
* help 文档完整
* 不覆盖 Core command
* 不与其他插件 command 冲突
* disabled plugin 命令提示清晰

如果插件禁用：

```yaml
plugins:
  import:
    enabled: false
    exposeCommands:
      - import
```

执行：

```bash
bukit import ...
```

必须返回：

```text
Command disabled by plugin config: import
```

不得返回：

```text
Unknown command: import
```

---

## 14. 配置准入

正式插件必须提供 `.bukit/plugins.yaml` 示例。

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
        read: []
```

配置准入要求：

* source 只能是 `plugins/<id>`
* permissions 完整
* timeout 合理
* output limit 合理
* allowInCi 明确
* failMode 明确

---

## 15. 测试准入

## 15.1 单元测试

必须覆盖：

* 参数解析
* manifest 生成
* invoke request 解析
* 业务核心逻辑
* 错误码
* 权限声明
* 路径处理

---

## 15.2 协议测试

必须覆盖：

* handshake success
* handshake invalid protocol
* manifest success
* manifest invalid response
* invoke success
* invoke invalid request
* invoke non-zero exit
* invoke diagnostics
* stdout invalid JSON
* stderr log capture

---

## 15.3 PluginHost 集成测试

必须覆盖：

* 加载 `.bukit/plugins.yaml`
* 加载 `plugin.yaml`
* 解析平台 entry
* 校验 sha256
* 校验权限
* 注册命令
* 禁用命令
* 命令冲突
* 执行 invoke
* 写 lock
* 写 execution report

---

## 15.4 跨平台测试

最低：

```text
windows-latest
ubuntu-latest
macos-latest
```

必须验证：

* binary 可执行
* 路径分隔符兼容
* stdout/stderr 行为一致
* exit code 一致
* JSON 编码一致

---

## 15.5 AOT 测试

Core 必须保持：

```text
PublishAot=true
PublishSingleFile=true
```

正式插件不得破坏 Core Native AOT 发布。

---

## 16. 文档准入

正式插件发布前必须更新：

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

插件自身必须提供：

```text
plugins/<id>/README.md
docs/specs/<id>-plugin.md
docs/plans/<id>-plugin-migration-plan.md
```

---

## 17. 报告准入

正式插件执行后必须支持 Core 写入：

```text
.bukit/reports/plugin-executions/<plugin-id>-<timestamp>.json
```

报告至少包含：

* plugin id
* version
* platform
* command
* operation
* entry
* startedAt
* durationMs
* processExitCode
* responseExitCode
* success
* sha256Verified
* permissions
* diagnostics
* artifacts

---

## 18. Lock 文件准入

正式插件解析后必须写入：

```text
.bukit/plugins.lock.yaml
```

记录：

* source
* manifestVersion
* protocol
* platform
* entry
* sha256
* commands
* resolvedAt

---

## 19. Release Gate 准入

正式插件进入 Core Release 前，必须通过：

```text
dotnet build bukit.slnx -c Release
dotnet test bukit.slnx -c Release
dotnet build bukit.plugins.slnx -c Release
dotnet test bukit.plugins.slnx -c Release
Core Native AOT publish
Plugin package build
Cross-platform plugin smoke tests
Docs check
Skills consistency check
Plugin schema validation
Protocol schema validation
Security regression tests
```

---

## 20. 版本准入

正式插件必须有独立版本。

版本格式：

```text
SemVer
```

示例：

```text
1.0.0
1.1.0
2.0.0-beta.1
```

Core 与 Plugin 兼容关系应记录在：

```text
plugins/<id>/plugin.yaml
```

建议字段：

```yaml
requires:
  bukit: ">=1.1.0 <2.0.0"
```

---

## 21. 回滚准入

正式插件必须支持安全回滚。

回滚方式：

```yaml
plugins:
  import:
    enabled: false
```

禁用后：

* Core 不执行插件
* 命令提示 disabled
* 不影响 Core stable commands
* 不影响 site.yaml 构建
* 不删除用户数据

---

## 22. Promotion Review Checklist

每个功能从 Labs 迁移为 Plugin 前，必须完成以下 checklist。

```text
[ ] 功能需求已稳定
[ ] 命令名称已稳定
[ ] 子命令已稳定
[ ] options 已稳定
[ ] 输出格式已稳定
[ ] 领域逻辑已抽离
[ ] 不再依赖 Labs
[ ] 已实现外部进程插件
[ ] 已实现 handshake
[ ] 已实现 manifest
[ ] 已实现 invoke
[ ] 已提供 plugin.yaml
[ ] 已提供 .bukit/plugins.yaml 示例
[ ] 已提供跨平台 binaries
[ ] 已提供 sha256
[ ] 已声明 requiredPermissions
[ ] 权限校验通过
[ ] 禁止 .bukit executable 通过
[ ] source 路径校验通过
[ ] command conflict 测试通过
[ ] disabled command 测试通过
[ ] Windows 测试通过
[ ] Linux 测试通过
[ ] macOS 测试通过
[ ] Core AOT publish 通过
[ ] 插件执行报告生成通过
[ ] plugins.lock.yaml 生成通过
[ ] 用户文档已更新
[ ] 开发文档已更新
[ ] skills 已更新
[ ] Release Gate 通过
```

---

## 23. Import Plugin 准入特别要求

Import Plugin 从 Labs 迁出时必须额外满足：

* `html-demo` 行为稳定
* `seed` 行为稳定
* 内容抽取逻辑稳定
* site.yaml 生成规则稳定
* route map 规则稳定
* Notion seed 规则稳定
* import-report 生成稳定
* 安全扫描稳定
* 不直接依赖 Labs ThemeCommand
* 不直接依赖 Labs NotionCommand
* 不直接调用 Core 内部 command handler
* 通过 Core Plugin Host invoke

---

## 24. Clone Plugin 准入特别要求

Clone Plugin 从 Labs 迁出时必须额外满足：

* tokens schema 稳定
* layout schema 稳定
* sections schema 稳定
* assets schema 稳定
* theme generation 稳定
* content generation 稳定
* fidelity mode 稳定
* visual verify report 稳定
* behavior verify script 稳定
* 网络下载权限明确
* 不直接依赖 Labs ThemeCommand
* 不直接调用 Core 内部 command handler
* 通过 Core Plugin Host invoke

---

## 25. 不允许的发布路径

禁止以下路径：

```text
Labs → Core
Labs → README Stable Feature
Labs → Plugin without protocol
Labs → Plugin without cross-platform support
Labs → Plugin without tests
Labs → Plugin with in-process DLL loading
Labs → Plugin stored under .bukit
Plugin → Core without release gate
Plugin → Core with command conflict
Plugin → Core with undocumented options
```

唯一允许路径：

```text
Labs → Plugin Candidate → Official Plugin → Core Release Integration
```

---

## 26. 最终结论

Bukit 的正式功能发布必须遵循：

```text
Labs 是孵化区
Plugin 是成熟发布单元
Core 是稳定底座和插件宿主
```

任何新功能进入正式发布前，都必须完成：

```text
外部进程化
协议化
跨平台化
配置化
权限化
测试化
文档化
发布门禁化
```

最终目标：

```text
Bukit Core 保持稳定和精简。
Labs 负责快速创新。
Plugin 负责正式功能扩展。
Core Plugin Host 负责统一接入。
```
