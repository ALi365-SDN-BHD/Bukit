# Bukit Import 插件迁移计划

> 文档版本：v1.0
> 适用项目：Bukit 静态网站生成引擎
> 目标插件：`Bukit.Plugin.Import`
> 目标命令：`bukit import`
> 插件类型：跨平台外部进程插件
> 协议版本：`bukit-plugin-v1`
> 配套机制：Bukit Core Plugin Host
> 状态：设计稿
> 优先级：P0

---

## 1. 文档目的

本文档用于定义 Bukit Import 功能从 Labs 迁移为正式外部进程插件的完整计划。

迁移目标不是简单移动 `ImportCommand` 文件，而是将当前 Labs 中的 Import 功能升级为：

```text
正式插件
跨平台外部进程
语言无关
可启用/禁用
可由 Core Plugin Host 加载
可通过 bukit import 进入正式发布
```

本计划覆盖：

1. 当前状态分析。
2. 迁移目标。
3. 目标目录结构。
4. 插件包结构。
5. 命令设计。
6. 协议设计。
7. 配置设计。
8. 权限设计。
9. 代码迁移步骤。
10. 测试计划。
11. 文档计划。
12. 验收标准。
13. 风险与回滚方案。

---

## 2. 当前状态

## 2.1 当前 Import 位于 Labs

当前 Import 功能位于 Labs CLI 中，属于尚未正式发布的功能模块。

当前命令形态：

```text
bukit-labs import html-demo ...
bukit-labs import seed ...
```

当前入口大致为：

```text
experimental/Bukit.Labs.Cli/Commands/Import/ImportCommand.cs
```

当前 Import 支持两个子命令：

```text
html-demo
seed
```

---

## 2.2 当前已有领域库

当前已有导入领域逻辑项目：

```text
src/Bukit.Importing/
```

该项目应继续作为 Import 领域库保留。

它负责：

```text
HTML demo 扫描
页面识别
Route Map 加载
Layout 提取
Asset 导入
Content 抽取
Seed 生成
Site YAML 生成
Import Report 生成
安全扫描
硬编码内容残留分析
```

---

## 2.3 当前耦合问题

当前 Labs Import 命令仍存在较多 CLI / Labs / Core 内部耦合。

主要问题：

```text
ImportCommand 直接处理大量参数解析
ImportCommand 直接调用 ThemeCommand
ImportCommand 直接调用 NotionCommand
ImportCommand 直接调用 ConfigLoader / ConfigValidator
ImportCommand 直接调用 SiteEngine.BuildAsync
ImportCommand 输出直接使用 Console
ImportCommand 尚未实现 bukit-plugin-v1
ImportCommand 尚未支持 plugin.yaml manifest
ImportCommand 尚未支持 .bukit/plugins.yaml 权限授权
ImportCommand 尚未支持跨平台插件包结构
ImportCommand 尚未写入插件执行报告
ImportCommand 尚未写入 plugins.lock.yaml
```

---

## 3. 迁移目标

## 3.1 总体目标

将 Import 功能从 Labs 迁移为正式插件：

```text
Labs Import
  ↓
Bukit.Importing 领域库保留
  ↓
Bukit.Plugin.Import 外部进程插件
  ↓
Core Plugin Host 加载
  ↓
bukit import 正式发布
```

---

## 3.2 功能目标

正式 Import 插件应支持：

```text
bukit import html-demo <demo-dir> --theme <name>
bukit import seed <seed-dir> --output <content-dir>
```

并保持当前 Labs Import 的主要能力：

```text
HTML demo 迁移为 Bukit 主题
内容抽取
Notion seed 生成
JSON/YAML seed 生成
route-map 支持
site.yaml 生成
import-report 生成
原始 HTML 保留
严格模式检查
导入安全扫描
导入后 verify
可选 Notion push
```

---

## 3.3 架构目标

Import 插件必须满足：

```text
1. 插件作为外部进程运行。
2. 插件不被 Core 以 DLL 方式加载。
3. 插件实现 bukit-plugin-v1 协议。
4. 插件可跨平台分发。
5. 插件程序放在用户项目 plugins/import/。
6. 插件配置放在 .bukit/plugins.yaml。
7. 插件 manifest 放在 plugins/import/plugin.yaml。
8. 插件执行报告写入 .bukit/reports/plugin-executions/。
9. 插件解析锁写入 .bukit/plugins.lock.yaml。
10. 插件可被 enabled / disabled。
11. Core 不直接引用 Bukit.Plugin.Import。
12. Bukit.Plugin.Import 不依赖 Labs。
```

---

## 4. 非目标范围

本次迁移不做：

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
Import UI
BukitJalil UI 接入
```

本次迁移只做：

```text
项目本地 plugins/import/ 外部进程 Import 插件
```

---

## 5. 目标仓库结构

## 5.1 源码结构

推荐新增：

```text
plugins/
└── Bukit.Plugin.Import/
    ├── Bukit.Plugin.Import.csproj
    ├── Program.cs
    ├── ImportPluginApp.cs
    ├── ImportPluginManifestProvider.cs
    ├── ImportPluginInvoker.cs
    ├── ImportCommandSpecFactory.cs
    ├── ImportOptionsMapper.cs
    ├── ImportSeedCommandHandler.cs
    ├── ImportHtmlDemoCommandHandler.cs
    ├── ImportPluginErrors.cs
    ├── plugin.yaml.template
    └── README.md
```

保留：

```text
src/Bukit.Importing/
```

新增测试：

```text
tests/
└── Bukit.Plugin.Import.Tests/
    ├── ImportPluginHandshakeTests.cs
    ├── ImportPluginManifestTests.cs
    ├── ImportPluginInvokeTests.cs
    ├── ImportPluginConfigTests.cs
    ├── ImportPluginPermissionsTests.cs
    ├── ImportPluginCrossPlatformPathTests.cs
    └── ImportPluginSmokeTests.cs
```

---

## 5.2 用户项目运行包结构

正式插件安装到用户项目时应为：

```text
project-root/
├── site.yaml
├── plugins/
│   └── import/
│       ├── plugin.yaml
│       ├── bin/
│       │   ├── win-x64/
│       │   │   └── bukit-plugin-import.exe
│       │   ├── win-arm64/
│       │   │   └── bukit-plugin-import.exe
│       │   ├── linux-x64/
│       │   │   └── bukit-plugin-import
│       │   ├── linux-arm64/
│       │   │   └── bukit-plugin-import
│       │   ├── osx-x64/
│       │   │   └── bukit-plugin-import
│       │   └── osx-arm64/
│       │       └── bukit-plugin-import
│       └── README.md
│
└── .bukit/
    ├── plugins.yaml
    ├── plugins.lock.yaml
    └── reports/
        └── plugin-executions/
```

---

## 6. Project Reference 设计

## 6.1 `Bukit.Plugin.Import` 可引用

允许引用：

```text
Bukit.Plugin.Abstractions
Bukit.Shared
Bukit.Importing
```

可选引用：

```text
Bukit.Cli.Shared
```

仅用于复用命令 spec DTO 或基础模型时使用。

---

## 6.2 `Bukit.Plugin.Import` 禁止引用

禁止引用：

```text
Bukit.Cli
Bukit.Engine
Bukit.Labs.Cli
Bukit.Labs.Import
Bukit.PluginHost
```

原因：

```text
Import 插件应作为独立外部进程运行。
Core 通过协议调用插件。
插件不应直接调用 Core CLI 内部 command handler。
插件不应依赖 Labs。
插件不应反向依赖 PluginHost。
```

---

## 6.3 Core 禁止引用 Import 插件实现

禁止：

```text
Bukit.Cli -> Bukit.Plugin.Import
Bukit.PluginHost -> Bukit.Plugin.Import
Bukit.Engine -> Bukit.Plugin.Import
```

正确关系：

```text
Bukit.Cli
  -> Bukit.PluginHost
      -> external process: plugins/import/bin/<rid>/bukit-plugin-import
```

---

## 7. 插件命令设计

## 7.1 顶层命令

正式命令：

```text
bukit import
```

命令来源：

```text
plugins/import/plugin.yaml
bukit-plugin-v1 manifest response
```

---

## 7.2 子命令：`html-demo`

命令：

```bash
bukit import html-demo <demo-dir> --theme <name> [options]
```

必填参数：

```text
<demo-dir>
--theme <name>
```

建议支持 options：

```text
--theme <name>
--force
--use
--verify
--no-extract-content
--no-seed
--content-source <notion|json|yaml>
--build-source <markdown|notion>
--route-map <file>
--site-path <dir>
--language <lang>
--dry-run
--strict <fail|warn>
--overwrite
--no-preserve-html
--no-report
--base-url <url>
--push-notion
--notion-database-id <id>
--notion-database-map <file>
--create-missing-notion-databases
--notion-parent-page-id <id>
--notion-generated-database-map <file>
--notion-token-env <name>
--notion-report <file>
--no-validate-notion-schema
--config <file>
--site <name>
```

---

## 7.3 子命令：`seed`

命令：

```bash
bukit import seed <seed-dir> --output <content-dir> [options]
```

必填参数：

```text
<seed-dir>
--output <content-dir>
```

Options：

```text
--force
--config <file>
--site <name>
```

---

## 8. 插件 Manifest 设计

用户项目插件包中的：

```text
plugins/import/plugin.yaml
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

  win-arm64:
    entry: bin/win-arm64/bukit-plugin-import.exe
    sha256: "<sha256>"

  linux-x64:
    entry: bin/linux-x64/bukit-plugin-import
    sha256: "<sha256>"

  linux-arm64:
    entry: bin/linux-arm64/bukit-plugin-import
    sha256: "<sha256>"

  osx-x64:
    entry: bin/osx-x64/bukit-plugin-import
    sha256: "<sha256>"

  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-import
    sha256: "<sha256>"

commands:
  - name: import
    description: Import external content into Bukit
    subcommands:
      - name: html-demo
        description: Import static HTML demo into Bukit theme
      - name: seed
        description: Convert generated seed data into markdown content

requiredPermissions:
  fileSystem:
    read:
      - .
    write:
      - ./sites
      - ./themes
      - ./content
      - ./data
      - ./docs/research
  network: false
  environment:
    read:
      - NOTION_TOKEN
```

---

## 9. `.bukit/plugins.yaml` 示例

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
          - ./data
          - ./docs/research
      network: false
      environment:
        read:
          - NOTION_TOKEN
    timeout:
      handshakeMs: 5000
      manifestMs: 5000
      invokeMs: 120000
    output:
      stdoutMaxBytes: 4194304
      stderrMaxBytes: 4194304
      responseMaxBytes: 4194304
    failMode: strict
    allowInCi: true
```

---

## 10. 协议实现要求

`Bukit.Plugin.Import` 必须实现：

```text
handshake
manifest
invoke
```

---

## 10.1 handshake

请求：

```json
{
  "type": "handshake",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYHANDSHAKE00000000000000",
  "host": {
    "name": "bukit",
    "version": "1.0.0",
    "platform": "osx-arm64"
  }
}
```

响应：

```json
{
  "type": "handshakeResponse",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYHANDSHAKE00000000000000",
  "success": true,
  "plugin": {
    "id": "import",
    "name": "Bukit Import Plugin",
    "version": "1.0.0",
    "platform": "osx-arm64",
    "capabilities": [
      "cli-command"
    ]
  }
}
```

---

## 10.2 manifest

响应必须返回完整 command spec。

核心示例：

```json
{
  "type": "manifestResponse",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYMANIFEST00000000000000",
  "success": true,
  "capabilities": [
    "cli-command"
  ],
  "commands": [
    {
      "name": "import",
      "description": "Import external content into Bukit",
      "subcommands": [
        {
          "name": "html-demo",
          "description": "Import static HTML demo into Bukit theme",
          "arguments": [
            {
              "name": "demo-dir",
              "description": "HTML demo directory",
              "required": true
            }
          ],
          "options": [
            {
              "name": "--theme",
              "type": "string",
              "description": "Target theme name",
              "required": true
            },
            {
              "name": "--force",
              "type": "flag",
              "description": "Overwrite existing theme",
              "required": false
            },
            {
              "name": "--verify",
              "type": "flag",
              "description": "Run verification after import",
              "required": false
            }
          ]
        },
        {
          "name": "seed",
          "description": "Convert generated seed data into markdown content",
          "arguments": [
            {
              "name": "seed-dir",
              "description": "Seed directory",
              "required": true
            }
          ],
          "options": [
            {
              "name": "--output",
              "type": "string",
              "description": "Output content directory",
              "required": true
            },
            {
              "name": "--force",
              "type": "flag",
              "description": "Overwrite existing markdown files",
              "required": false
            }
          ]
        }
      ]
    }
  ],
  "requiredPermissions": {
    "fileSystem": {
      "read": ["."],
      "write": ["./sites", "./themes", "./content", "./data", "./docs/research"]
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

Core 调用：

```json
{
  "type": "invoke",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYINVOKE000000000000000",
  "host": {
    "name": "bukit",
    "version": "1.0.0",
    "platform": "osx-arm64"
  },
  "command": {
    "name": "import",
    "path": ["import", "html-demo"],
    "arguments": ["./demo"],
    "options": {
      "--theme": "silkroadbiz",
      "--verify": true,
      "--force": false
    }
  },
  "context": {
    "rootDir": "/project",
    "configPath": "/project/site.yaml",
    "workingDir": "/project",
    "outputDir": null,
    "environment": {
      "NOTION_TOKEN": "***"
    }
  },
  "permissions": {
    "fileSystem": {
      "read": ["."],
      "write": ["./sites", "./themes", "./content", "./data", "./docs/research"]
    },
    "network": false,
    "environment": {
      "read": ["NOTION_TOKEN"]
    }
  }
}
```

插件响应：

```json
{
  "type": "invokeResponse",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYINVOKE000000000000000",
  "success": true,
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
      "path": "sites/silkroadbiz/import-report.md",
      "description": "Import report"
    }
  ],
  "diagnostics": []
}
```

---

## 11. 参数映射设计

## 11.1 invoke → HtmlDemoImportOptions

`ImportOptionsMapper` 负责将 invoke request 转换为 `HtmlDemoImportOptions`。

映射关系：

```text
command.arguments[0]                 -> InputPath
options["--theme"]                    -> ThemeName
context.rootDir                       -> RootDir
options["--force"]                    -> Force
options["--use"]                      -> Use
options["--verify"]                   -> Verify
!options["--no-extract-content"]      -> ExtractContent
!options["--no-seed"]                 -> GenerateSeed
options["--content-source"]           -> ContentSource
options["--build-source"]             -> BuildSource
options["--site-path"]                -> SitePath
options["--language"]                 -> Language
options["--dry-run"]                  -> DryRun
options["--strict"]                   -> StrictMode
options["--overwrite"]                -> Overwrite
!options["--no-preserve-html"]        -> PreserveHtml
!options["--no-report"]               -> GenerateReport
options["--base-url"]                 -> BaseUrl
options["--route-map"]                -> RouteMapPath
options["--notion-database-id"]       -> NotionDatabaseId
options["--notion-database-map"]      -> NotionDatabaseMap
options["--notion-token-env"]         -> NotionTokenEnv
```

---

## 11.2 invoke → seed import

Seed command 映射：

```text
command.arguments[0]      -> seed-dir
options["--output"]       -> output content dir
options["--force"]        -> overwrite existing markdown
```

---

## 12. 与 Core 服务边界

## 12.1 Import 插件不直接调用 Core command handler

禁止：

```text
ThemeCommand.SetThemeAsync
NotionCommand.RunAsync
DoctorCommand.RunAsync
BuildCommand.RunAsync
```

---

## 12.2 `--verify` 处理策略

推荐第一版由插件内部直接使用稳定领域 API 或通过协议请求 Core 执行验证。

可选方案：

### 方案 A：插件内部独立验证

插件引用必要领域库，自行调用配置和构建相关稳定 API。

优点：

```text
实现简单
迁移成本低
```

缺点：

```text
插件与 Core 内部 API 版本耦合较高
```

### 方案 B：Core 提供 host service request

插件通过协议返回 `requestedActions`，由 Core 执行：

```json
{
  "requestedActions": [
    {
      "type": "core.build",
      "configPath": "sites/silkroadbiz/site.yaml"
    }
  ]
}
```

优点：

```text
Core 能控制 Build / Doctor
插件更独立
```

缺点：

```text
协议复杂度增加
```

第一版建议采用：

```text
方案 A 过渡
方案 B 作为 v1.1 / v2 规划
```

---

## 12.3 `--use` 处理策略

当前 `--use` 涉及修改 site.yaml theme。

第一版建议由插件直接写入生成目标站点配置，避免调用 Labs `ThemeCommand`。

后续可引入 Core Host Action：

```text
core.theme.set
```

---

## 12.4 `--push-notion` 处理策略

当前 `--push-notion` 直接调用 NotionCommand。

迁移后建议：

### 第一阶段

保留在 Import 插件内部，但改为 Import 插件内部服务，不再调用 Labs NotionCommand。

### 第二阶段

拆为独立 Notion Plugin：

```text
Bukit.Plugin.Notion
```

Import 插件只生成 seed 和 artifacts，不直接 push Notion。

### 推荐长期方案

```text
import plugin
  -> generate seed

notion plugin
  -> push seed

workflow / user command
  -> compose both
```

---

## 13. 权限设计

Import 插件默认 requiredPermissions：

```yaml
requiredPermissions:
  fileSystem:
    read:
      - .
    write:
      - ./sites
      - ./themes
      - ./content
      - ./data
      - ./docs/research
  network: false
  environment:
    read:
      - NOTION_TOKEN
```

## 13.1 文件读取

需要读取：

```text
demo directory
route-map
site.yaml
existing content / theme state
```

建议第一版授权：

```text
.
```

## 13.2 文件写入

需要写入：

```text
sites/
themes/
content/
data/
docs/research/
```

## 13.3 网络

默认：

```text
network: false
```

仅当启用 `--push-notion` 或未来远程抓取能力时才需要：

```text
network: true
```

建议第一版将 `--push-notion` 标记为需要网络权限。

## 13.4 环境变量

需要：

```text
NOTION_TOKEN
```

只有启用 `--push-notion` 时才读取。

---

## 14. 插件错误码

建议错误码：

```text
import.missingDemoDir
import.demoDirNotFound
import.missingTheme
import.invalidThemeName
import.unsupportedContentSource
import.unsupportedBuildSource
import.invalidBuildSourceCombination
import.themeAlreadyExists
import.pushNotionRequiresSeed
import.pushNotionRequiresParentPage
import.dryRunConflictsWithPushNotion
import.importFailed
import.verifyFailed
import.seedDirNotFound
import.missingOutput
import.permissionDenied
import.invalidRouteMap
```

协议通用错误继续使用：

```text
plugin.invalidRequest
plugin.permissionDenied
plugin.executionFailed
plugin.timeout
plugin.invalidResponse
```

---

## 15. 输出与报告

Import 插件成功后应返回 artifacts。

示例：

```json
"artifacts": [
  {
    "type": "file",
    "path": "sites/silkroadbiz/import-report.md",
    "description": "Import report"
  },
  {
    "type": "directory",
    "path": "themes/silkroadbiz",
    "description": "Generated theme"
  },
  {
    "type": "directory",
    "path": "sites/silkroadbiz",
    "description": "Generated site"
  }
]
```

Core Plugin Host 负责写入：

```text
.bukit/reports/plugin-executions/import-invoke-<timestamp>.json
```

---

## 16. 跨平台要求

正式 Import 插件必须支持：

```text
windows-x64
linux-x64
osx-arm64
```

推荐支持：

```text
windows-x64
windows-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

必须测试：

```text
路径分隔符
可执行权限
文件复制
目录创建
UTF-8 文件写入
YAML 读写
JSON 读写
stdout JSON
stderr logs
exit code
```

---

## 17. Package Build 设计

## 17.1 输出包

构建后生成：

```text
artifacts/plugins/import/
├── plugin.yaml
├── bin/
│   ├── win-x64/
│   │   └── bukit-plugin-import.exe
│   ├── linux-x64/
│   │   └── bukit-plugin-import
│   └── osx-arm64/
│       └── bukit-plugin-import
└── README.md
```

## 17.2 Hash 生成

构建后应自动计算每个平台入口 sha256，并写入 `plugin.yaml`。

## 17.3 Native AOT

如果 Import 插件使用 .NET 实现，建议也使用：

```text
PublishAot=true
PublishSingleFile=true
```

但协议不强制语言或 runtime。

---

## 18. 测试计划

## 18.1 单元测试

覆盖：

```text
ImportOptionsMapper
ImportCommandSpecFactory
ImportPluginManifestProvider
ImportPluginInvoker
html-demo 参数校验
seed 参数校验
错误码映射
artifact 生成
permissions 判断
```

---

## 18.2 协议测试

覆盖：

```text
handshake success
handshake invalid protocol
manifest success
manifest commands 完整
invoke html-demo success
invoke seed success
invoke missing theme
invoke missing demo dir
invoke unsupported content-source
invoke invalid build-source combination
invoke import exception
invoke response JSON valid
stderr log 不污染 stdout
```

---

## 18.3 PluginHost 集成测试

覆盖：

```text
.bukit/plugins.yaml 加载 import 插件
plugins/import/plugin.yaml 加载
platform entry 解析
sha256 校验
permissions 校验
exposeCommands 注册 import
disabled import command
invoke import html-demo
invoke import seed
plugins.lock.yaml 写入
plugin execution report 写入
```

---

## 18.4 跨平台 smoke test

最低：

```text
windows-latest
ubuntu-latest
macos-latest
```

执行：

```bash
bukit plugin list
bukit import --help
bukit import html-demo ./examples/demo --theme demo --dry-run
bukit import seed ./examples/seed --output ./content --force
```

---

## 19. 文档更新计划

需要新增：

```text
docs/plans/import-plugin-migration-plan.md
docs/specs/import-plugin.md
plugins/Bukit.Plugin.Import/README.md
```

需要更新：

```text
README.md
README.zh-CN.md
guide/user/12-cli-reference.md
guide/dev/plugins.md
guide/dev/architecture.md
guide/skills/bukit-cli-reference/SKILL.md
guide/skills/using-bukit/SKILL.md
```

---

## 20. 迁移阶段

## Phase 1：准备 Core Plugin Host

前置要求：

```text
Bukit.PluginHost 已可加载 .bukit/plugins.yaml
Bukit.PluginHost 已可加载 plugins/<id>/plugin.yaml
Bukit.PluginHost 已支持 handshake / manifest / invoke
Core CLI 已可注册插件命令
```

如果 Core Plugin Host 未完成，不进入 Import 插件迁移。

---

## Phase 2：创建 `Bukit.Plugin.Import`

任务：

```text
新建 plugins/Bukit.Plugin.Import
新增 Program.cs
新增 ImportPluginApp
新增 handshake handler
新增 manifest handler
新增 invoke handler
引用 Bukit.Importing
引用 Bukit.Plugin.Abstractions
```

验收：

```bash
bukit-plugin-import handshake
bukit-plugin-import manifest
```

或通过 stdin JSON 协议完成测试。

---

## Phase 3：迁移 seed 子命令

任务：

```text
迁移 ImportSeedRecordReader 调用
迁移 ImportSeedContentWriter 调用
实现 invoke import seed
实现 seed 参数校验
实现 seed artifacts
实现 seed 错误码
```

验收：

```bash
bukit import seed ./seed --output ./content --force
```

---

## Phase 4：迁移 html-demo 子命令

任务：

```text
迁移 HtmlDemoImportOptions 映射
调用 HtmlDemoImporter.Import
实现参数校验
实现 import artifacts
实现 import diagnostics
实现 import error mapping
```

验收：

```bash
bukit import html-demo ./demo --theme silkroadbiz --dry-run
bukit import html-demo ./demo --theme silkroadbiz --force
```

---

## Phase 5：处理 verify / use / push-notion

任务：

```text
--verify 改造为插件内部过渡实现或 Core action
--use 改造为插件内部 site.yaml 修改或 Core action
--push-notion 暂时保留或拆出 Notion Plugin
```

建议：

```text
第一版支持 --verify
第一版支持 --use
第一版将 --push-notion 标记为 experimental 或暂缓
```

---

## Phase 6：跨平台包构建

任务：

```text
构建 win-x64
构建 linux-x64
构建 osx-arm64
生成 plugin.yaml
计算 sha256
生成 artifacts/plugins/import 包
```

---

## Phase 7：Core CLI 正式接入

任务：

```text
.bukit/plugins.yaml exposeCommands: import
Core CLI 注册 import
bukit import --help 可用
bukit plugin list 显示 import
disabled import 行为正确
```

---

## Phase 8：Release Gate

执行：

```bash
dotnet build bukit.slnx -c Release
dotnet test bukit.slnx -c Release

dotnet build bukit.plugins.slnx -c Release
dotnet test bukit.plugins.slnx -c Release

dotnet publish src/Bukit.Cli -c Release -p:PublishAot=true
```

以及跨平台 smoke。

---

## 21. 回滚方案

如果 Import 插件出现问题，可通过：

```yaml
plugins:
  import:
    enabled: false
```

禁用。

禁用后：

```text
Core stable commands 不受影响
site.yaml 构建不受影响
用户数据不删除
import 命令提示 disabled
```

---

## 22. 风险分析

## 22.1 风险：`--push-notion` 过度耦合

当前 Import 直接调用 Notion 逻辑。

解决：

```text
短期：保留内部实现但不依赖 Labs NotionCommand
长期：拆为 Bukit.Plugin.Notion
```

---

## 22.2 风险：`--verify` 依赖 Core 构建 API

解决：

```text
短期：插件引用必要稳定 API
长期：Core 提供 host action
```

---

## 22.3 风险：跨平台路径问题

解决：

```text
所有协议 path 用 /
内部统一 NormalizePath
跨平台测试必须覆盖 Windows/Linux/macOS
```

---

## 22.4 风险：stdout 被日志污染

解决：

```text
stdout 只输出 JSON response
日志全部 stderr
测试强制校验
```

---

## 22.5 风险：权限边界不清

解决：

```text
requiredPermissions 明确
granted permissions 明确
Core Plugin Host 强制比对
```

---

## 23. 验收标准

Import 插件迁移完成后必须满足：

```text
1. Import 不再作为 Labs 正式入口发布。
2. plugins/Bukit.Plugin.Import 存在。
3. Import 插件实现 bukit-plugin-v1。
4. Import 插件支持 handshake。
5. Import 插件支持 manifest。
6. Import 插件支持 invoke。
7. Import 插件支持 html-demo。
8. Import 插件支持 seed。
9. Import 插件可由 Core Plugin Host 加载。
10. Import 插件可通过 .bukit/plugins.yaml 启用。
11. Import 插件可通过 .bukit/plugins.yaml 禁用。
12. 禁用后 bukit import 提示 Command disabled by plugin config: import。
13. Import 插件程序位于 plugins/import/。
14. .bukit 不存放任何 Import 可执行程序。
15. Import 插件支持至少 Windows x64、Linux x64、macOS arm64。
16. Import 插件 sha256 校验通过。
17. Import 插件权限校验通过。
18. Import 插件执行报告生成。
19. plugins.lock.yaml 生成。
20. Core Native AOT 发布不受影响。
21. 文档和 skills 更新完成。
```

---

## 24. 最终结论

Import 是 Bukit 插件体系的第一个正式迁移候选。

原因：

```text
已有 Bukit.Importing 领域库
功能边界相对清晰
比 clone 更适合验证 Core Plugin Host
能覆盖配置、协议、权限、命令注册、跨平台包、报告等全流程
```

推荐将 Import 作为：

```text
Core Plugin Mechanism v1 的第一个官方插件样板
```

最终目标：

```text
bukit import
  不再来自 Labs
  不再是 Core 内置命令
  而是由 Bukit.Plugin.Import 外部进程插件提供
  通过 Core Plugin Host 正式接入
```
