# Bukit Clone 插件迁移计划

> 文档版本：v1.0
> 适用项目：Bukit 静态网站生成引擎
> 目标插件：`Bukit.Plugin.Clone`
> 目标领域库：`Bukit.Clone`
> 目标命令：`bukit clone`
> 插件类型：跨平台外部进程插件
> 协议版本：`bukit-plugin-v1`
> 配套机制：Bukit Core Plugin Host
> 状态：设计稿
> 优先级：P1

---

## 1. 文档目的

本文档用于定义 Bukit Clone 功能从 Labs 迁移为正式外部进程插件的完整计划。

Clone 功能当前属于 Labs 孵化能力，功能复杂度高于 Import，涉及：

```text
设计令牌读取
布局读取
页面元数据读取
区块读取
交互行为读取
图标读取
资源下载
主题生成
内容生成
数据模块生成
fidelity 模式
主题切换
构建验证
视觉 diff 报告
行为验证脚本
```

因此 Clone 插件迁移不能简单移动 `CloneCommand`，而应分两步：

```text
第一步：抽离 Clone 领域库
第二步：封装为外部进程插件
```

最终目标是：

```text
Labs Clone
  ↓
Bukit.Clone 领域库
  ↓
Bukit.Plugin.Clone 外部进程插件
  ↓
Core Plugin Host 加载
  ↓
bukit clone 正式发布
```

---

## 2. 当前状态

## 2.1 当前 Clone 位于 Labs

当前 Clone 功能位于 Labs CLI 中，属于尚未正式发布的实验模块。

当前命令形态：

```bash
bukit-labs clone ...
```

当前逻辑大致位于：

```text
experimental/Bukit.Labs.Cli/Commands/Clone/
```

当前入口包括：

```text
CloneCommand
CloneCommandOptions
CloneInputLoader
CloneAssetDownloader
CloneThemeGenerator
CloneContentWriter
CloneFidelityRunner
CloneVerifier
CloneScreenshotComparer
CloneBehaviorVerifyScript
```

---

## 2.2 当前 Clone 的主要能力

当前 Clone 支持：

```text
从 tokens JSON 生成主题
从 layout JSON 生成布局
从 page JSON 生成页面内容
从 sections JSON 生成区块内容
从 behaviors JSON 生成行为脚本
从 icons JSON 写入 SVG 图标
从 assets JSON 下载资源
生成 themes/<theme>/
生成 content/
生成 data/
更新 site.yaml
可选 --use 设置当前主题
可选 --verify 执行验证
可选 --fidelity 迁移 HTML 目录
可选视觉 diff 报告
可选行为验证脚本
```

---

## 2.3 当前耦合问题

当前 Clone 功能仍高度耦合 Labs CLI。

主要问题：

```text
CloneCommand 同时承担参数解析、业务编排、输出和验证
CloneVerifier 直接使用 ConfigLoader / ConfigValidator / SiteEngine
CloneCommand 直接调用 ThemeCommand.SetThemeAsync
CloneVerifier 直接写 docs/research/VERIFY_REPORT.md
CloneVerifier 直接写 docs/research/VERIFY_REPORT.json
CloneVerifier 直接写 docs/research/BEHAVIORS_VERIFY.js
Clone 逻辑没有独立领域库
Clone 没有实现 bukit-plugin-v1
Clone 没有 plugin.yaml manifest
Clone 没有 .bukit/plugins.yaml 权限授权模型
Clone 没有跨平台插件包结构
Clone 没有 PluginHost execution report
Clone 没有 plugins.lock.yaml 解析结果
```

---

## 3. 迁移目标

## 3.1 总体目标

将 Clone 功能迁移为正式插件：

```text
Labs Clone
  ↓
src/Bukit.Clone
  ↓
plugins/Bukit.Plugin.Clone
  ↓
Core Plugin Host
  ↓
bukit clone
```

---

## 3.2 功能目标

正式 Clone 插件应支持：

```bash
bukit clone --tokens <file> --theme <name> [options]
bukit clone --fidelity <html-dir> --theme <name> [options]
```

并逐步支持当前 Labs Clone 的主要能力：

```text
tokens 驱动主题生成
layout 驱动主题生成
page / sections 内容生成
behaviors 脚本生成
icons 写入
assets 下载
theme assets 生成
content 生成
data modules 生成
fidelity HTML 迁移
visual verify report
behavior verify script
clone verify
theme use
```

---

## 3.3 架构目标

Clone 插件必须满足：

```text
1. 插件作为外部进程运行。
2. 插件不被 Core 以 DLL 方式加载。
3. 插件实现 bukit-plugin-v1 协议。
4. 插件可跨平台分发。
5. 插件程序放在用户项目 plugins/clone/。
6. 插件配置放在 .bukit/plugins.yaml。
7. 插件 manifest 放在 plugins/clone/plugin.yaml。
8. 插件执行报告写入 .bukit/reports/plugin-executions/。
9. 插件解析锁写入 .bukit/plugins.lock.yaml。
10. 插件可被 enabled / disabled。
11. Core 不直接引用 Bukit.Plugin.Clone。
12. Bukit.Plugin.Clone 不依赖 Labs。
13. Clone 领域逻辑从 Labs 中抽离。
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
浏览器自动截图能力
真实浏览器 visual capture
BukitJalil UI 接入
AI 自动抓取源站
```

本次迁移只做：

```text
项目本地 plugins/clone/ 外部进程 Clone 插件
```

视觉截图采集仍应由外部工具、AI Agent 或未来 Visual Plugin 负责。

Clone 插件第一版只负责：

```text
读取已有 target/local screenshot
执行 diff
生成 verify report
```

---

## 5. 目标仓库结构

## 5.1 新增领域库

新增：

```text
src/
└── Bukit.Clone/
    ├── Bukit.Clone.csproj
    ├── Models/
    │   ├── CloneCommandOptions.cs
    │   ├── CloneGenerationSummary.cs
    │   ├── CloneTokens.cs
    │   ├── CloneLayoutInfo.cs
    │   ├── ClonePageInfo.cs
    │   ├── CloneSectionInfo.cs
    │   ├── CloneBehaviors.cs
    │   ├── CloneIcons.cs
    │   └── CloneAssets.cs
    │
    ├── Input/
    │   └── CloneInputLoader.cs
    │
    ├── Assets/
    │   └── CloneAssetDownloader.cs
    │
    ├── Generation/
    │   ├── CloneThemeGenerator.cs
    │   ├── CloneContentWriter.cs
    │   ├── CloneFidelityRunner.cs
    │   └── TemplateScopeExtensions.cs
    │
    ├── Verification/
    │   ├── CloneVerifier.cs
    │   ├── CloneScreenshotComparer.cs
    │   ├── CloneVerificationReportWriter.cs
    │   ├── CloneBehaviorVerifyScriptWriter.cs
    │   ├── CloneBehaviorVerifyScript.cs
    │   └── CloneVerifyModels.cs
    │
    └── Serialization/
        └── CloneJson.cs
```

---

## 5.2 新增正式插件项目

新增：

```text
plugins/
└── Bukit.Plugin.Clone/
    ├── Bukit.Plugin.Clone.csproj
    ├── Program.cs
    ├── ClonePluginApp.cs
    ├── ClonePluginManifestProvider.cs
    ├── ClonePluginInvoker.cs
    ├── CloneCommandSpecFactory.cs
    ├── CloneOptionsMapper.cs
    ├── CloneStandardCommandHandler.cs
    ├── CloneFidelityCommandHandler.cs
    ├── ClonePluginErrors.cs
    ├── plugin.yaml.template
    └── README.md
```

---

## 5.3 新增测试项目

新增：

```text
tests/
├── Bukit.Clone.Tests/
│   ├── CloneInputLoaderTests.cs
│   ├── CloneThemeGeneratorTests.cs
│   ├── CloneContentWriterTests.cs
│   ├── CloneFidelityRunnerTests.cs
│   ├── CloneAssetDownloaderTests.cs
│   ├── CloneScreenshotComparerTests.cs
│   └── CloneVerifierTests.cs
│
└── Bukit.Plugin.Clone.Tests/
    ├── ClonePluginHandshakeTests.cs
    ├── ClonePluginManifestTests.cs
    ├── ClonePluginInvokeTests.cs
    ├── ClonePluginConfigTests.cs
    ├── ClonePluginPermissionsTests.cs
    ├── ClonePluginCrossPlatformPathTests.cs
    └── ClonePluginSmokeTests.cs
```

---

## 5.4 用户项目运行包结构

正式插件安装到用户项目时应为：

```text
project-root/
├── site.yaml
├── plugins/
│   └── clone/
│       ├── plugin.yaml
│       ├── bin/
│       │   ├── win-x64/
│       │   │   └── bukit-plugin-clone.exe
│       │   ├── win-arm64/
│       │   │   └── bukit-plugin-clone.exe
│       │   ├── linux-x64/
│       │   │   └── bukit-plugin-clone
│       │   ├── linux-arm64/
│       │   │   └── bukit-plugin-clone
│       │   ├── osx-x64/
│       │   │   └── bukit-plugin-clone
│       │   └── osx-arm64/
│       │       └── bukit-plugin-clone
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

## 6.1 `Bukit.Clone` 可引用

允许引用：

```text
Bukit.Shared
```

根据实际需要可引用：

```text
Bukit.Config
```

但应尽量避免直接依赖：

```text
Bukit.Cli
Bukit.Engine
Bukit.Labs.Cli
Bukit.PluginHost
```

---

## 6.2 `Bukit.Plugin.Clone` 可引用

允许引用：

```text
Bukit.Plugin.Abstractions
Bukit.Shared
Bukit.Clone
```

可选引用：

```text
Bukit.Cli.Shared
```

仅用于复用命令 spec DTO 或基础模型。

---

## 6.3 `Bukit.Plugin.Clone` 禁止引用

禁止引用：

```text
Bukit.Cli
Bukit.Engine
Bukit.Labs.Cli
Bukit.Labs.Clone
Bukit.PluginHost
```

原因：

```text
Clone 插件应作为独立外部进程运行。
Core 通过协议调用插件。
插件不应直接调用 Core CLI 内部 command handler。
插件不应依赖 Labs。
插件不应反向依赖 PluginHost。
```

---

## 6.4 Core 禁止引用 Clone 插件实现

禁止：

```text
Bukit.Cli -> Bukit.Plugin.Clone
Bukit.PluginHost -> Bukit.Plugin.Clone
Bukit.Engine -> Bukit.Plugin.Clone
```

正确关系：

```text
Bukit.Cli
  -> Bukit.PluginHost
      -> external process: plugins/clone/bin/<rid>/bukit-plugin-clone
```

---

## 7. 插件命令设计

## 7.1 顶层命令

正式命令：

```text
bukit clone
```

命令来源：

```text
plugins/clone/plugin.yaml
bukit-plugin-v1 manifest response
```

---

## 7.2 标准模式

命令：

```bash
bukit clone --tokens <file> --theme <name> [options]
```

必填：

```text
--tokens <file>
```

默认：

```text
--theme cloned
```

支持 options：

```text
--tokens <file>
--theme <name>
--layout <file>
--page <file>
--sections <file>
--behaviors <file>
--icons <file>
--assets <file>
--brand <name>
--use
--force
--verify
--visual-threshold <ratio>
--fail-on-visual-diff
--template <full|bare|none>
--config <file>
--site <name>
```

---

## 7.3 Fidelity 模式

命令：

```bash
bukit clone --fidelity <html-dir> --theme <name> [options]
```

支持 options：

```text
--fidelity <html-dir>
--theme <name>
--force
--use
--verify
--template <full|bare|none>
--config <file>
--site <name>
```

---

## 8. 插件 Manifest 设计

用户项目插件包中的：

```text
plugins/clone/plugin.yaml
```

示例：

```yaml
id: clone
name: Bukit Clone Plugin
version: 1.0.0
protocol: bukit-plugin-v1
kind: process
distribution: self-contained

platforms:
  win-x64:
    entry: bin/win-x64/bukit-plugin-clone.exe
    sha256: "<sha256>"

  win-arm64:
    entry: bin/win-arm64/bukit-plugin-clone.exe
    sha256: "<sha256>"

  linux-x64:
    entry: bin/linux-x64/bukit-plugin-clone
    sha256: "<sha256>"

  linux-arm64:
    entry: bin/linux-arm64/bukit-plugin-clone
    sha256: "<sha256>"

  osx-x64:
    entry: bin/osx-x64/bukit-plugin-clone
    sha256: "<sha256>"

  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-clone
    sha256: "<sha256>"

commands:
  - name: clone
    description: Generate Bukit themes and content from structured clone inputs

requiredPermissions:
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

## 9. `.bukit/plugins.yaml` 示例

```yaml
version: 1

plugins:
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
    timeout:
      handshakeMs: 5000
      manifestMs: 5000
      invokeMs: 180000
    output:
      stdoutMaxBytes: 4194304
      stderrMaxBytes: 4194304
      responseMaxBytes: 4194304
    failMode: strict
    allowInCi: false
```

---

## 10. 协议实现要求

`Bukit.Plugin.Clone` 必须实现：

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
    "id": "clone",
    "name": "Bukit Clone Plugin",
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
      "name": "clone",
      "description": "Generate Bukit themes and content from structured clone inputs",
      "options": [
        {
          "name": "--tokens",
          "type": "string",
          "description": "Design tokens JSON file",
          "required": false
        },
        {
          "name": "--theme",
          "type": "string",
          "description": "Target theme name",
          "required": false
        },
        {
          "name": "--layout",
          "type": "string",
          "description": "Layout JSON file",
          "required": false
        },
        {
          "name": "--page",
          "type": "string",
          "description": "Page metadata JSON file",
          "required": false
        },
        {
          "name": "--sections",
          "type": "string",
          "description": "Page sections JSON file",
          "required": false
        },
        {
          "name": "--behaviors",
          "type": "string",
          "description": "Behaviors JSON file",
          "required": false
        },
        {
          "name": "--icons",
          "type": "string",
          "description": "Icons JSON file",
          "required": false
        },
        {
          "name": "--assets",
          "type": "string",
          "description": "Assets JSON file",
          "required": false
        },
        {
          "name": "--brand",
          "type": "string",
          "description": "Brand name",
          "required": false
        },
        {
          "name": "--fidelity",
          "type": "string",
          "description": "HTML directory for fidelity mode",
          "required": false
        },
        {
          "name": "--use",
          "type": "flag",
          "description": "Switch site theme after clone",
          "required": false
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
          "description": "Run verification after clone",
          "required": false
        },
        {
          "name": "--visual-threshold",
          "type": "number",
          "description": "Visual diff threshold between 0 and 1",
          "required": false
        },
        {
          "name": "--fail-on-visual-diff",
          "type": "flag",
          "description": "Fail when visual diff exceeds threshold",
          "required": false
        },
        {
          "name": "--template",
          "type": "string",
          "description": "Template generation scope",
          "required": false,
          "allowedValues": ["full", "bare", "none"]
        },
        {
          "name": "--config",
          "type": "string",
          "description": "Config file path",
          "required": false
        },
        {
          "name": "--site",
          "type": "string",
          "description": "Multi-site name",
          "required": false
        }
      ]
    }
  ],
  "requiredPermissions": {
    "fileSystem": {
      "read": ["."],
      "write": ["./themes", "./content", "./data", "./docs/research"]
    },
    "network": true,
    "environment": {
      "read": []
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
    "name": "clone",
    "path": ["clone"],
    "arguments": [],
    "options": {
      "--tokens": "tokens.json",
      "--theme": "silkroadbiz",
      "--verify": true,
      "--force": false,
      "--visual-threshold": "0.03"
    }
  },
  "context": {
    "rootDir": "/project",
    "configPath": "/project/site.yaml",
    "workingDir": "/project",
    "outputDir": null,
    "environment": {}
  },
  "permissions": {
    "fileSystem": {
      "read": ["."],
      "write": ["./themes", "./content", "./data", "./docs/research"]
    },
    "network": true,
    "environment": {
      "read": []
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
      "message": "Theme cloned: silkroadbiz"
    }
  ],
  "artifacts": [
    {
      "type": "directory",
      "path": "themes/silkroadbiz",
      "description": "Generated theme"
    },
    {
      "type": "file",
      "path": "docs/research/VERIFY_REPORT.md",
      "description": "Clone verification report"
    }
  ],
  "diagnostics": []
}
```

---

## 11. 参数映射设计

## 11.1 invoke → CloneCommandOptions

`CloneOptionsMapper` 负责将 invoke request 转换为 `CloneCommandOptions`。

映射关系：

```text
options["--tokens"]                -> Tokens
options["--layout"]                -> Layout
options["--page"]                  -> Page
options["--sections"]              -> Sections
options["--theme"]                 -> Theme, default "cloned"
options["--brand"]                 -> Brand
options["--behaviors"]             -> Behaviors
options["--icons"]                 -> Icons
options["--assets"]                -> Assets
options["--visual-threshold"]      -> VisualThreshold, default 0.03
options["--fidelity"]              -> Fidelity
options["--use"]                   -> Use
options["--force"]                 -> Force
options["--verify"]                -> Verify
options["--fail-on-visual-diff"]   -> FailOnVisualDiff
options["--template"]              -> TemplateScope
context.rootDir                    -> RootDir
context.configPath                 -> ConfigPath
```

---

## 11.2 标准模式参数校验

标准模式要求：

```text
--tokens 必须存在
--fidelity 不得存在
--theme 可缺省，默认 cloned
--visual-threshold 必须在 0 到 1 之间
--template 必须是 full / bare / none
```

---

## 11.3 Fidelity 模式参数校验

Fidelity 模式要求：

```text
--fidelity 必须存在
--tokens 可不存在
--theme 可缺省，默认 cloned
--template 必须是 full / bare / none
```

---

## 12. 与 Core 服务边界

## 12.1 Clone 插件不直接调用 Core command handler

禁止：

```text
ThemeCommand.SetThemeAsync
DoctorCommand.RunAsync
BuildCommand.RunAsync
```

---

## 12.2 `--verify` 处理策略

Clone 当前 verify 逻辑包括：

```text
配置验证
模板存在性检查
Scriban parse
Build
视觉 diff 报告
行为验证脚本
```

建议分阶段处理。

### 第一阶段：插件内部过渡实现

Clone 插件通过 `Bukit.Clone` 领域库完成：

```text
CloneVerificationReportWriter
CloneScreenshotComparer
CloneBehaviorVerifyScriptWriter
```

构建验证可先通过稳定 Core API 或简化 verify 实现。

### 第二阶段：Core Host Action

未来 Core Plugin Host 可提供 host action：

```text
core.config.check
core.build
core.theme.set
```

插件返回 requestedActions，由 Core 执行。

---

## 12.3 `--use` 处理策略

当前 `--use` 涉及设置 site.yaml theme。

第一版建议：

```text
Clone 插件内部修改 site.yaml
```

但必须遵守：

```text
只允许写入授权路径
必须生成 diagnostics
必须备份或原子写入
```

长期建议：

```text
Core 提供 core.theme.set host action
```

---

## 12.4 资源下载策略

Clone 插件可能需要下载图片等资源。

因此 Clone 插件默认需要：

```yaml
network: true
```

如果用户禁用网络：

```yaml
network: false
```

则：

```text
--assets 中包含 remote URL 时必须失败
只允许本地 assets
```

---

## 13. 权限设计

Clone 插件默认 requiredPermissions：

```yaml
requiredPermissions:
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

## 13.1 文件读取

需要读取：

```text
tokens JSON
layout JSON
page JSON
sections JSON
behaviors JSON
icons JSON
assets JSON
fidelity HTML directory
site.yaml
existing themes/content/data
existing screenshots
```

建议第一版授权：

```text
.
```

---

## 13.2 文件写入

需要写入：

```text
themes/
content/
data/
docs/research/
```

---

## 13.3 网络

Clone 可能需要根据 assets JSON 下载资源。

默认：

```yaml
network: true
```

若用户授权为 false，则不允许下载 remote assets。

---

## 13.4 环境变量

Clone 第一版不需要环境变量。

默认：

```yaml
environment:
  read: []
```

---

## 14. 插件错误码

建议错误码：

```text
clone.missingTokens
clone.tokensFileNotFound
clone.invalidTokens
clone.layoutFileNotFound
clone.invalidLayout
clone.pageFileNotFound
clone.invalidPage
clone.sectionsFileNotFound
clone.invalidSections
clone.behaviorsFileNotFound
clone.invalidBehaviors
clone.iconsFileNotFound
clone.invalidIcons
clone.assetsFileNotFound
clone.invalidAssets
clone.invalidThemeName
clone.themeAlreadyExists
clone.invalidVisualThreshold
clone.invalidTemplateScope
clone.fidelityDirNotFound
clone.networkPermissionRequired
clone.assetDownloadFailed
clone.themeGenerationFailed
clone.contentGenerationFailed
clone.verifyFailed
clone.visualDiffExceeded
clone.permissionDenied
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

Clone 插件成功后应返回 artifacts。

示例：

```json
"artifacts": [
  {
    "type": "directory",
    "path": "themes/silkroadbiz",
    "description": "Generated theme"
  },
  {
    "type": "directory",
    "path": "content",
    "description": "Generated content"
  },
  {
    "type": "directory",
    "path": "data",
    "description": "Generated data modules"
  },
  {
    "type": "file",
    "path": "docs/research/VERIFY_REPORT.md",
    "description": "Clone verification report"
  },
  {
    "type": "file",
    "path": "docs/research/VERIFY_REPORT.json",
    "description": "Clone verification JSON report"
  },
  {
    "type": "file",
    "path": "docs/research/BEHAVIORS_VERIFY.js",
    "description": "Browser behavior verification script"
  }
]
```

Core Plugin Host 负责写入：

```text
.bukit/reports/plugin-executions/clone-invoke-<timestamp>.json
```

---

## 16. 跨平台要求

正式 Clone 插件必须支持：

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
JSON 读取
远程资源下载
stdout JSON
stderr logs
exit code
fidelity 目录处理
视觉报告路径
```

---

## 17. Package Build 设计

## 17.1 输出包

构建后生成：

```text
artifacts/plugins/clone/
├── plugin.yaml
├── bin/
│   ├── win-x64/
│   │   └── bukit-plugin-clone.exe
│   ├── linux-x64/
│   │   └── bukit-plugin-clone
│   └── osx-arm64/
│       └── bukit-plugin-clone
└── README.md
```

---

## 17.2 Hash 生成

构建后应自动计算每个平台入口 sha256，并写入 `plugin.yaml`。

---

## 17.3 Native AOT

如果 Clone 插件使用 .NET 实现，建议使用：

```text
PublishAot=true
PublishSingleFile=true
```

但协议不强制语言或 runtime。

---

## 18. 测试计划

## 18.1 `Bukit.Clone.Tests`

覆盖：

```text
CloneInputLoader
CloneCommandOptions
CloneAssetDownloader
CloneThemeGenerator
CloneContentWriter
CloneFidelityRunner
CloneScreenshotComparer
CloneVerificationReportWriter
CloneBehaviorVerifyScriptWriter
TemplateScopeExtensions
VisualThreshold parsing
Safe theme name validation
```

---

## 18.2 `Bukit.Plugin.Clone.Tests`

覆盖：

```text
handshake success
handshake invalid protocol
manifest success
manifest commands 完整
invoke clone standard success
invoke clone fidelity success
invoke missing tokens
invoke invalid visual threshold
invoke invalid template scope
invoke network permission denied
invoke asset download failed
invoke verify failed
invoke response JSON valid
stderr log 不污染 stdout
```

---

## 18.3 PluginHost 集成测试

覆盖：

```text
.bukit/plugins.yaml 加载 clone 插件
plugins/clone/plugin.yaml 加载
platform entry 解析
sha256 校验
permissions 校验
exposeCommands 注册 clone
disabled clone command
invoke clone standard
invoke clone fidelity
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
bukit clone --help
bukit clone --tokens ./examples/clone/tokens.json --theme cloned --force
bukit clone --fidelity ./examples/html --theme cloned-fidelity --force
```

---

## 19. 文档更新计划

需要新增：

```text
docs/plans/clone-plugin-migration-plan.md
docs/specs/clone-plugin.md
plugins/Bukit.Plugin.Clone/README.md
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

如果 Core Plugin Host 未完成，不进入 Clone 插件迁移。

---

## Phase 2：抽离 `Bukit.Clone`

任务：

```text
新建 src/Bukit.Clone
迁移 Clone Models
迁移 CloneInputLoader
迁移 CloneAssetDownloader
迁移 CloneThemeGenerator
迁移 CloneContentWriter
迁移 CloneFidelityRunner
迁移 CloneScreenshotComparer
迁移 CloneBehaviorVerifyScript
迁移 CloneJson
补充 Bukit.Clone.Tests
```

验收：

```text
Bukit.Clone 不依赖 Labs
Bukit.Clone 不依赖 Bukit.Cli
Bukit.Clone 单元测试通过
```

---

## Phase 3：创建 `Bukit.Plugin.Clone`

任务：

```text
新建 plugins/Bukit.Plugin.Clone
新增 Program.cs
新增 ClonePluginApp
新增 handshake handler
新增 manifest handler
新增 invoke handler
引用 Bukit.Clone
引用 Bukit.Plugin.Abstractions
```

验收：

```text
Clone 插件可完成 handshake
Clone 插件可完成 manifest
Clone 插件 stdout 只输出 JSON
```

---

## Phase 4：迁移标准 clone 模式

任务：

```text
实现 invoke clone --tokens
实现 CloneOptionsMapper
实现 tokens/layout/page/sections/behaviors/icons/assets 参数映射
实现主题生成
实现内容生成
实现 data 生成
实现 artifacts
实现错误码
```

验收：

```bash
bukit clone --tokens tokens.json --theme cloned --force
```

---

## Phase 5：迁移 fidelity 模式

任务：

```text
实现 invoke clone --fidelity
迁移 CloneFidelityRunner
实现 fidelity 参数校验
实现 fidelity artifacts
```

验收：

```bash
bukit clone --fidelity ./html --theme cloned-fidelity --force
```

---

## Phase 6：迁移 verify / visual report

任务：

```text
迁移 CloneScreenshotComparer
迁移 CloneVerificationReportWriter
迁移 CloneBehaviorVerifyScriptWriter
实现 --verify
实现 --fail-on-visual-diff
实现 --visual-threshold
```

验收：

```bash
bukit clone --tokens tokens.json --theme cloned --verify --visual-threshold 0.03
```

---

## Phase 7：处理 --use

任务：

```text
实现 --use site.yaml 修改
保证原子写入
保证 path 安全
生成 diagnostics
```

长期优化：

```text
由 Core Plugin Host 提供 core.theme.set host action
```

---

## Phase 8：跨平台包构建

任务：

```text
构建 win-x64
构建 linux-x64
构建 osx-arm64
生成 plugin.yaml
计算 sha256
生成 artifacts/plugins/clone 包
```

---

## Phase 9：Core CLI 正式接入

任务：

```text
.bukit/plugins.yaml exposeCommands: clone
Core CLI 注册 clone
bukit clone --help 可用
bukit plugin list 显示 clone
disabled clone 行为正确
```

---

## Phase 10：Release Gate

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

如果 Clone 插件出现问题，可通过：

```yaml
plugins:
  clone:
    enabled: false
```

禁用。

禁用后：

```text
Core stable commands 不受影响
site.yaml 构建不受影响
用户数据不删除
clone 命令提示 disabled
```

---

## 22. 风险分析

## 22.1 风险：Clone 功能复杂度高

Clone 同时涉及主题、内容、资源、视觉、行为验证。

解决：

```text
分阶段迁移
先抽 Bukit.Clone
再做 Bukit.Plugin.Clone
先迁移标准模式
再迁移 fidelity
最后迁移 verify
```

---

## 22.2 风险：网络下载权限复杂

Clone assets 可能需要网络。

解决：

```text
network 权限显式声明
network=false 时禁止 remote assets
测试 network permission denied
```

---

## 22.3 风险：verify 依赖 Core 构建能力

解决：

```text
短期采用过渡实现
长期引入 Core Host Action
```

---

## 22.4 风险：视觉截图采集不属于插件职责

解决：

```text
Clone 插件只比较已有截图
截图采集由外部工具、AI Agent 或未来 Visual Plugin 负责
```

---

## 22.5 风险：路径跨平台问题

解决：

```text
协议路径统一使用 /
内部使用 Path.Combine
测试 Windows / Linux / macOS
```

---

## 22.6 风险：stdout 被日志污染

解决：

```text
stdout 只输出 JSON response
日志全部 stderr
测试强制校验
```

---

## 22.7 风险：--use 修改 site.yaml

解决：

```text
原子写入
写入前备份或可恢复
严格路径校验
失败时不破坏原配置
```

---

## 23. 验收标准

Clone 插件迁移完成后必须满足：

```text
1. Clone 不再作为 Labs 正式入口发布。
2. src/Bukit.Clone 存在。
3. plugins/Bukit.Plugin.Clone 存在。
4. Clone 插件实现 bukit-plugin-v1。
5. Clone 插件支持 handshake。
6. Clone 插件支持 manifest。
7. Clone 插件支持 invoke。
8. Clone 插件支持标准 tokens 模式。
9. Clone 插件支持 fidelity 模式。
10. Clone 插件支持 --verify。
11. Clone 插件支持 visual threshold。
12. Clone 插件可由 Core Plugin Host 加载。
13. Clone 插件可通过 .bukit/plugins.yaml 启用。
14. Clone 插件可通过 .bukit/plugins.yaml 禁用。
15. 禁用后 bukit clone 提示 Command disabled by plugin config: clone。
16. Clone 插件程序位于 plugins/clone/。
17. .bukit 不存放任何 Clone 可执行程序。
18. Clone 插件支持至少 Windows x64、Linux x64、macOS arm64。
19. Clone 插件 sha256 校验通过。
20. Clone 插件权限校验通过。
21. Clone 插件执行报告生成。
22. plugins.lock.yaml 生成。
23. Core Native AOT 发布不受影响。
24. 文档和 skills 更新完成。
```

---

## 24. 最终结论

Clone 是 Bukit 插件体系的第二个正式迁移候选。

原因：

```text
功能价值高
适合 AI Demo-to-Bukit / 主题生成 / 网站迁移场景
但复杂度高于 Import
需要先完成 Core Plugin Host 和 Import Plugin 验证
```

推荐顺序：

```text
先迁移 Import Plugin
再迁移 Clone Plugin
```

最终目标：

```text
bukit clone
  不再来自 Labs
  不再是 Core 内置命令
  而是由 Bukit.Plugin.Clone 外部进程插件提供
  通过 Core Plugin Host 正式接入
```
