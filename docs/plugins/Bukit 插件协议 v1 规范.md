# Bukit 插件协议 v1 规范

> 文档版本：v1.0
> 适用项目：Bukit 静态网站生成引擎
> 协议名称：`bukit-plugin-v1`
> 协议类型：语言无关、跨平台、外部进程 JSON 协议
> 适用对象：Bukit Core Plugin Host 与外部进程插件
> 状态：设计稿
> 优先级：P0

---

## 1. 文档目的

本文档定义 Bukit 外部进程插件协议 v1。

协议目标是让 Bukit Core 可以安全、稳定、跨平台地加载和调用正式插件，同时不限制插件开发语言。

插件可以使用任意语言开发，例如：

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

但插件必须满足：

```text
1. 作为外部进程运行。
2. 实现 bukit-plugin-v1 JSON 协议。
3. 提供跨平台可执行入口。
4. 提供 plugin.yaml manifest。
5. 通过 handshake / manifest / invoke 协议调用。
6. 输出标准 JSON 响应。
7. 遵守 Core Plugin Host 的安全、权限、路径、超时、输出限制。
```

---

## 2. 协议边界

## 2.1 Core 负责什么

Bukit Core Plugin Host 负责：

```text
读取 .bukit/plugins.yaml
读取 plugins/<id>/plugin.yaml
校验插件路径
解析当前平台入口
校验 sha256
启动插件外部进程
发送 JSON request
读取 JSON response
处理 stderr 日志
处理超时
处理非零退出码
校验权限
注册 CLI 命令
写入 plugins.lock.yaml
写入 plugin execution report
```

---

## 2.2 Plugin 负责什么

外部插件负责：

```text
接收 stdin JSON request
识别 request.type
执行 handshake / manifest / invoke
向 stdout 输出 JSON response
向 stderr 输出日志
返回合适 exit code
不输出非 JSON 内容到 stdout
遵守协议路径规范
遵守权限声明
```

---

## 2.3 v1 不包含什么

协议 v1 不包含：

```text
远程插件市场
自动下载插件
全局插件目录
动态 DLL 插件
WASM 插件
Docker 插件
插件热加载
插件自动更新
插件依赖解析
插件签名服务
OS 级完整沙箱
build hook 正式支持
```

v1 只支持：

```text
项目本地 plugins/<id>/ 外部进程插件
handshake
manifest
invoke
```

---

## 3. 协议基础原则

## 3.1 语言无关

协议不得依赖某种编程语言、运行时或 SDK。

第三方插件不需要引用任何 Bukit 程序集，只需实现 JSON 协议即可。

Bukit 官方插件可以引用：

```text
Bukit.Plugin.Abstractions
Bukit.Shared
```

但这不是协议强制要求。

---

## 3.2 跨平台

正式插件必须在 manifest 中声明平台入口。

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

---

## 3.3 外部进程

除 Core 内置插件外，所有正式插件必须作为外部进程运行。

禁止：

```text
Assembly.LoadFrom
动态 DLL 加载
in-process 第三方插件
运行时反射加载插件程序集
```

---

## 3.4 stdout 只允许 JSON response

插件必须将协议响应写入 stdout。

stdout 必须是一个完整 JSON object。

不得向 stdout 写入：

```text
普通日志
进度文本
调试信息
ANSI 动画
多段 JSON
非 JSON 内容
```

普通日志必须写入 stderr。

---

## 3.5 stderr 用于日志

插件可以向 stderr 写入日志。

Core Plugin Host 应捕获 stderr 并写入执行报告。

stderr 不参与协议解析。

---

## 3.6 不使用 shell

Core 启动插件时不得使用 shell 拼接命令。

禁止：

```text
sh -c
cmd /c
powershell -Command
bash script.sh
shell string concatenation
```

推荐：

```csharp
new ProcessStartInfo
{
    FileName = resolvedEntry,
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true
}
```

---

## 4. 插件包结构

插件包必须位于项目根目录 `plugins/` 下。

示例：

```text
project-root/
├── site.yaml
├── plugins/
│   └── import/
│       ├── plugin.yaml
│       ├── bin/
│       │   ├── win-x64/
│       │   │   └── bukit-plugin-import.exe
│       │   ├── linux-x64/
│       │   │   └── bukit-plugin-import
│       │   └── osx-arm64/
│       │       └── bukit-plugin-import
│       └── README.md
└── .bukit/
    ├── plugins.yaml
    ├── plugins.lock.yaml
    └── reports/
```

---

## 5. 禁止执行位置

插件程序不得位于 `.bukit/` 内。

Core 必须拒绝：

```text
.bukit/plugins/import
.bukit/bin/plugin
.bukit/tools/plugin
.bukit/plugin-executables/plugin
```

错误示例：

```text
Plugin source cannot be inside .bukit: .bukit/plugins/import
```

```text
Plugin executable cannot be inside .bukit: .bukit/bin/bukit-plugin-import
```

---

## 6. `.bukit/plugins.yaml`

`.bukit/plugins.yaml` 是项目级插件启用配置。

它不直接声明可执行文件路径，只声明插件 source。

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
    timeout:
      handshakeMs: 5000
      manifestMs: 5000
      invokeMs: 120000
```

---

## 7. `plugins/<id>/plugin.yaml`

`plugin.yaml` 是插件包 manifest 文件。

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
    subcommands:
      - name: html-demo
        description: Import static HTML demo
      - name: seed
        description: Convert generated seed data

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

## 8. 平台 ID

Bukit 插件协议 v1 使用以下平台 ID。

| 平台 ID         | 含义                  |
| ------------- | ------------------- |
| `win-x64`     | Windows x64         |
| `win-arm64`   | Windows ARM64       |
| `linux-x64`   | Linux x64           |
| `linux-arm64` | Linux ARM64         |
| `osx-x64`     | macOS Intel         |
| `osx-arm64`   | macOS Apple Silicon |

Core 必须根据当前运行环境解析当前 platform id。

---

## 9. 通信方式

## 9.1 基本方式

Core 启动插件进程后，通过 stdin 写入 JSON request。

插件处理后，通过 stdout 输出 JSON response。

stderr 用于日志。

```text
Core PluginHost
  stdin  -> JSON request  -> Plugin
  stdout <- JSON response <- Plugin
  stderr <- logs          <- Plugin
```

---

## 9.2 推荐进程调用方式

Core 可以以固定参数启动插件，例如：

```text
plugins/import/bin/osx-arm64/bukit-plugin-import --protocol bukit-plugin-v1
```

但 request 类型必须由 stdin JSON 中的 `type` 字段决定。

插件不得依赖复杂 shell 参数。

---

## 9.3 字符编码

协议 JSON 必须使用：

```text
UTF-8
```

不得输出 BOM。

---

## 9.4 换行规则

stdout 可以包含结尾换行。

Core 应允许：

```json
{"type":"handshakeResponse","success":true}
```

或：

```json
{"type":"handshakeResponse","success":true}

```

但 stdout 中不得包含多段 JSON 或非 JSON 文本。

---

## 10. 通用 Request Envelope

所有请求必须包含：

```json
{
  "type": "handshake",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HY0000000000000000000000",
  "host": {
    "name": "bukit",
    "version": "1.0.0",
    "platform": "osx-arm64"
  }
}
```

字段说明：

| 字段              | 类型     | 必填 | 说明                         |
| --------------- | ------ | -: | -------------------------- |
| `type`          | string |  是 | 请求类型                       |
| `protocol`      | string |  是 | 协议版本，必须是 `bukit-plugin-v1` |
| `requestId`     | string |  是 | 请求 ID                      |
| `host`          | object |  是 | Host 信息                    |
| `host.name`     | string |  是 | Host 名称                    |
| `host.version`  | string |  是 | Bukit 版本                   |
| `host.platform` | string |  是 | 当前平台 ID                    |

---

## 11. 通用 Response Envelope

所有响应必须包含：

```json
{
  "type": "handshakeResponse",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HY0000000000000000000000",
  "success": true
}
```

字段说明：

| 字段            | 类型     | 必填 | 说明      |
| ------------- | ------ | -: | ------- |
| `type`        | string |  是 | 响应类型    |
| `protocol`    | string |  是 | 协议版本    |
| `requestId`   | string |  是 | 对应请求 ID |
| `success`     | bool   |  是 | 是否成功    |
| `error`       | object |  否 | 错误信息    |
| `messages`    | list   |  否 | 用户可见消息  |
| `diagnostics` | list   |  否 | 诊断信息    |

---

## 12. Error Object

错误对象格式：

```json
{
  "code": "plugin.invalidRequest",
  "message": "Missing required field: command",
  "details": {
    "field": "command"
  }
}
```

字段说明：

| 字段        | 类型     | 必填 | 说明     |
| --------- | ------ | -: | ------ |
| `code`    | string |  是 | 稳定错误码  |
| `message` | string |  是 | 用户可读错误 |
| `details` | object |  否 | 结构化详情  |

---

## 13. Message Object

插件可返回消息：

```json
{
  "level": "info",
  "message": "Import completed."
}
```

`level` 可选：

```text
debug
info
warn
error
```

---

## 14. Diagnostic Object

诊断对象：

```json
{
  "code": "import.missingTheme",
  "severity": "error",
  "message": "Missing required option: --theme",
  "path": "options.--theme"
}
```

`severity` 可选：

```text
info
warning
error
```

---

## 15. handshake

## 15.1 目的

`handshake` 用于确认：

```text
协议版本
插件身份
插件版本
当前平台
基础能力
```

---

## 15.2 Request

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

---

## 15.3 Response

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

## 15.4 失败响应

```json
{
  "type": "handshakeResponse",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYHANDSHAKE00000000000000",
  "success": false,
  "error": {
    "code": "plugin.unsupportedProtocol",
    "message": "Unsupported protocol: bukit-plugin-v1"
  }
}
```

---

## 15.5 Host 校验规则

Core 必须校验：

```text
response.protocol == request.protocol
response.requestId == request.requestId
response.success == true
plugin.id == plugin.yaml id
plugin.version == plugin.yaml version
plugin.platform == current platform
```

如果不满足，拒绝加载插件。

---

## 16. manifest

## 16.1 目的

`manifest` 用于让插件声明运行时能力，包括：

```text
CLI commands
subcommands
options
required permissions
capabilities
```

---

## 16.2 Request

```json
{
  "type": "manifest",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYMANIFEST00000000000000",
  "host": {
    "name": "bukit",
    "version": "1.0.0",
    "platform": "osx-arm64"
  }
}
```

---

## 16.3 Response

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
      "arguments": [],
      "options": [
        {
          "name": "--config",
          "type": "string",
          "description": "Config file path",
          "required": false
        }
      ],
      "subcommands": [
        {
          "name": "html-demo",
          "description": "Import static HTML demo",
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
            }
          ]
        },
        {
          "name": "seed",
          "description": "Convert generated seed data",
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
            }
          ]
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

## 16.4 Command Spec

Command object 字段：

| 字段            | 类型     | 必填 | 说明   |
| ------------- | ------ | -: | ---- |
| `name`        | string |  是 | 命令名  |
| `description` | string |  是 | 命令说明 |
| `aliases`     | list   |  否 | 别名   |
| `arguments`   | list   |  否 | 参数   |
| `options`     | list   |  否 | 选项   |
| `subcommands` | list   |  否 | 子命令  |

---

## 16.5 Option Spec

Option object 字段：

| 字段              | 类型     | 必填 | 说明               |
| --------------- | ------ | -: | ---------------- |
| `name`          | string |  是 | 选项名，例如 `--theme` |
| `type`          | string |  是 | 类型               |
| `description`   | string |  是 | 描述               |
| `required`      | bool   |  否 | 是否必填             |
| `valueName`     | string |  否 | 值名称              |
| `allowedValues` | list   |  否 | 允许值              |
| `conflictWith`  | string |  否 | 冲突选项             |

Option type 可选：

```text
string
integer
number
boolean
flag
```

---

## 16.6 Argument Spec

Argument object 字段：

| 字段            | 类型     | 必填 | 说明   |
| ------------- | ------ | -: | ---- |
| `name`        | string |  是 | 参数名  |
| `description` | string |  是 | 参数说明 |
| `required`    | bool   |  否 | 是否必填 |

---

## 17. invoke

## 17.1 目的

`invoke` 用于执行插件命令。

---

## 17.2 Request

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

## 17.3 Request 字段说明

| 字段                    | 类型     | 必填 | 说明             |
| --------------------- | ------ | -: | -------------- |
| `command.name`        | string |  是 | 顶层命令           |
| `command.path`        | list   |  是 | 命令路径           |
| `command.arguments`   | list   |  否 | 位置参数           |
| `command.options`     | object |  否 | 选项             |
| `context.rootDir`     | string |  是 | 项目根目录          |
| `context.configPath`  | string |  否 | site.yaml 路径   |
| `context.workingDir`  | string |  是 | 插件工作目录         |
| `context.outputDir`   | string |  否 | 构建输出目录         |
| `context.environment` | object |  否 | allowlist 环境变量 |
| `permissions`         | object |  是 | 授予权限           |

---

## 17.4 Response

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

## 17.5 失败响应

```json
{
  "type": "invokeResponse",
  "protocol": "bukit-plugin-v1",
  "requestId": "01HYINVOKE000000000000000",
  "success": false,
  "exitCode": 2,
  "error": {
    "code": "import.missingTheme",
    "message": "Missing required option: --theme"
  },
  "diagnostics": [
    {
      "code": "import.missingTheme",
      "severity": "error",
      "message": "Missing required option: --theme",
      "path": "command.options.--theme"
    }
  ]
}
```

---

## 18. Artifact Object

插件可返回 artifacts。

```json
{
  "type": "file",
  "path": "sites/silkroadbiz/import-report.md",
  "description": "Import report"
}
```

Artifact type 可选：

```text
file
directory
url
report
```

Artifact path 必须是项目内相对路径。

---

## 19. 权限模型

## 19.1 权限对象

```json
{
  "fileSystem": {
    "read": ["."],
    "write": ["./sites", "./themes", "./content"]
  },
  "network": false,
  "environment": {
    "read": ["NOTION_TOKEN"]
  }
}
```

---

## 19.2 权限校验

Core 必须校验：

```text
plugin.requiredPermissions <= project.grantedPermissions
```

如果插件要求权限超过项目授予权限，则拒绝加载或拒绝 invoke。

---

## 19.3 环境变量传递

Core 只传递 allowlist 中允许的环境变量。

例如：

```yaml
environment:
  read:
    - NOTION_TOKEN
```

Core 可在 invoke request 中传递：

```json
"environment": {
  "NOTION_TOKEN": "actual-value"
}
```

也可以通过进程环境变量传递。

建议 v1 优先通过进程环境变量传递，但执行报告中必须打码。

---

## 19.4 敏感值打码

执行报告中不得记录 secret 原文。

应记录为：

```json
{
  "NOTION_TOKEN": "***"
}
```

---

## 20. 路径规范

## 20.1 协议路径

协议中路径统一使用 `/` 分隔。

示例：

```text
sites/silkroadbiz/import-report.md
```

Core 在 Windows 下负责转换为系统路径。

---

## 20.2 禁止绝对路径返回

插件返回 artifacts 时，不得返回绝对路径。

禁止：

```text
/Users/a/project/report.md
C:\project\report.md
```

必须返回：

```text
sites/silkroadbiz/import-report.md
```

---

## 20.3 禁止路径穿越

插件 request / response 中不得包含：

```text
../
..\
```

Core 必须校验插件返回的 path。

---

## 21. Exit Code 规范

| Exit Code | 含义     |
| --------: | ------ |
|         0 | 成功     |
|         1 | 插件内部错误 |
|         2 | 用户输入错误 |
|         3 | 协议错误   |
|         4 | 权限错误   |
|         5 | 配置错误   |
|         6 | 不支持的平台 |
|         7 | 超时     |
|         8 | 安全校验失败 |

插件进程 exit code 和 response.exitCode 应保持一致。

如果不一致，Core 应以进程 exit code 为准，并记录协议不一致诊断。

---

## 22. 稳定错误码建议

| 错误码                          | 含义          |
| ---------------------------- | ----------- |
| `plugin.unsupportedProtocol` | 不支持协议       |
| `plugin.invalidRequest`      | 请求格式错误      |
| `plugin.invalidResponse`     | 响应格式错误      |
| `plugin.unsupportedPlatform` | 不支持当前平台     |
| `plugin.permissionDenied`    | 权限不足        |
| `plugin.timeout`             | 插件超时        |
| `plugin.executionFailed`     | 插件执行失败      |
| `plugin.sha256Mismatch`      | hash 不匹配    |
| `plugin.invalidManifest`     | manifest 无效 |
| `plugin.commandNotFound`     | 插件不支持该命令    |
| `plugin.pathTraversal`       | 路径穿越        |
| `plugin.outputTooLarge`      | 输出超过限制      |

---

## 23. Timeout 规范

默认 timeout：

| 操作        |      默认超时 |
| --------- | --------: |
| handshake |   5000 ms |
| manifest  |   5000 ms |
| invoke    | 120000 ms |

可通过 `.bukit/plugins.yaml` 覆盖：

```yaml
timeout:
  handshakeMs: 5000
  manifestMs: 5000
  invokeMs: 180000
```

---

## 24. 输出大小限制

默认限制：

| 输出            | 默认限制 |
| ------------- | ---: |
| stdout        | 4 MB |
| stderr        | 4 MB |
| response JSON | 4 MB |

超过限制应终止执行并返回：

```text
plugin.outputTooLarge
```

---

## 25. Execution Report

每次插件执行必须写入报告。

路径：

```text
.bukit/reports/plugin-executions/<plugin-id>-<operation>-<timestamp>.json
```

示例：

```json
{
  "pluginId": "import",
  "pluginVersion": "1.0.0",
  "protocol": "bukit-plugin-v1",
  "platform": "osx-arm64",
  "operation": "invoke",
  "command": "import",
  "entry": "plugins/import/bin/osx-arm64/bukit-plugin-import",
  "startedAt": "2026-06-17T00:00:00Z",
  "durationMs": 1234,
  "processExitCode": 0,
  "responseExitCode": 0,
  "success": true,
  "sha256Verified": true,
  "stdoutBytes": 1024,
  "stderrBytes": 128,
  "permissions": {
    "network": false,
    "environment": {
      "read": ["NOTION_TOKEN"]
    }
  },
  "artifacts": [
    {
      "type": "file",
      "path": "sites/silkroadbiz/import-report.md"
    }
  ]
}
```

---

## 26. plugins.lock.yaml

Core 成功解析插件后，应写入：

```text
.bukit/plugins.lock.yaml
```

示例：

```yaml
version: 1

resolved:
  import:
    source: plugins/import
    version: 1.0.0
    protocol: bukit-plugin-v1
    platform: osx-arm64
    entry: plugins/import/bin/osx-arm64/bukit-plugin-import
    sha256: "<sha256>"
    resolvedAt: "2026-06-17T00:00:00Z"
```

---

## 27. CLI 命令注册

Core 读取 manifest 后，将 manifest.commands 转换为 CLI command descriptors。

规则：

```text
1. Core command 优先。
2. 插件不得覆盖 Core command。
3. 插件之间不得注册同名 command。
4. alias 不得冲突。
5. disabled plugin command 应提示 Command disabled by plugin config。
```

---

## 28. 禁用插件行为

如果 `.bukit/plugins.yaml` 中：

```yaml
plugins:
  import:
    enabled: false
    source: plugins/import
    exposeCommands:
      - import
```

执行：

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

应返回：

```text
Command disabled by plugin config: import
```

Exit code：

```text
2
```

---

## 29. plugin list 行为

Core 应提供：

```bash
bukit plugin list
```

输出示例：

```text
Plugins:
  import@1.0.0 [process] enabled=true platform=osx-arm64 commands=import
  clone@1.0.0 [process] enabled=false platform=osx-arm64 commands=clone

Built-in engine plugins:
  taxonomy enabled=true
  archive enabled=true
  pagination enabled=true
```

---

## 30. Host 校验流程

Core Plugin Host 加载插件时，必须按以下顺序执行：

```text
1. Load .bukit/plugins.yaml
2. Validate config schema
3. For each plugin config:
   3.1 Validate source path
   3.2 Reject source inside .bukit
   3.3 Reject source outside plugins/
   3.4 Load plugins/<id>/plugin.yaml
   3.5 Validate manifest file schema
   3.6 Resolve current platform entry
   3.7 Reject entry path traversal
   3.8 Reject entry inside .bukit
   3.9 Validate entry exists
   3.10 Validate sha256
   3.11 Execute handshake
   3.12 Validate handshake response
   3.13 Execute manifest
   3.14 Validate manifest response
   3.15 Validate permissions
   3.16 Register command descriptors
   3.17 Write lock file
```

---

## 31. Invoke 流程

```text
1. User runs bukit <plugin-command>
2. Core resolves plugin command descriptor
3. Core parses arguments/options
4. Core builds invoke request
5. Core starts plugin process
6. Core sends JSON request through stdin
7. Plugin writes JSON response to stdout
8. Plugin writes logs to stderr
9. Core validates response
10. Core writes execution report
11. Core prints messages
12. Core returns exit code
```

---

## 32. 安全失败处理

| 场景                    | 行为       |
| --------------------- | -------- |
| source 在 `.bukit`     | 拒绝       |
| source 不在 `plugins/`  | 拒绝       |
| entry 路径穿越            | 拒绝       |
| entry 不存在             | 拒绝       |
| sha256 不匹配            | 拒绝       |
| unsupported platform  | 拒绝       |
| permissions 不足        | 拒绝       |
| handshake timeout     | 拒绝       |
| manifest invalid JSON | 拒绝       |
| invoke invalid JSON   | 执行失败     |
| stdout 过大             | 终止进程     |
| stderr 过大             | 终止或截断并记录 |

---

## 33. 跨平台要求

正式插件发布前必须通过：

```text
Windows x64
Linux x64
macOS arm64
```

最低测试。

推荐测试：

```text
Windows x64
Windows ARM64
Linux x64
Linux ARM64
macOS x64
macOS ARM64
```

---

## 34. 非目标

协议 v1 不支持：

```text
远程插件仓库
插件自动安装
插件版本依赖解析
插件市场
插件签名服务
全局插件缓存
用户 home 插件目录
Docker runtime
WASM runtime
动态 DLL runtime
```

---

## 35. 向后兼容

由于当前 Core 中没有正式外部插件机制，协议 v1 不需要兼容旧 `site.externalPlugins`。

明确不恢复：

```yaml
site:
  externalPlugins:
```

外部插件配置必须使用：

```text
.bukit/plugins.yaml
```

插件程序必须位于：

```text
plugins/<plugin-id>/
```

---

## 36. 与 Core 内置插件关系

Core 内置 Engine 插件继续使用 in-process 模型。

它们不走 `bukit-plugin-v1`。

它们仍由 `site.plugins` 控制。

外部进程插件由 `.bukit/plugins.yaml` 控制。

边界：

```text
site.plugins
  = Core built-in Engine plugins

.bukit/plugins.yaml
  = External process feature plugins
```

---

## 37. 第一版验收标准

协议 v1 完成后必须满足：

```text
1. 能加载 .bukit/plugins.yaml。
2. 能加载 plugins/<id>/plugin.yaml。
3. 能拒绝 .bukit 内插件程序。
4. 能拒绝 plugins/ 外插件来源。
5. 能解析当前平台入口。
6. 能校验 sha256。
7. 能执行 handshake。
8. 能执行 manifest。
9. 能执行 invoke。
10. 能处理 timeout。
11. 能处理 invalid JSON。
12. 能处理 non-zero exit code。
13. 能写 plugins.lock.yaml。
14. 能写 plugin execution report。
15. 能注册插件 CLI 命令。
16. 能处理 disabled plugin command。
17. 能通过 Windows / Linux / macOS 测试。
```

---

## 38. 推荐第一批测试插件

建议先实现一个最小测试插件：

```text
plugins/echo/
```

功能：

```text
handshake 返回身份
manifest 返回 echo 命令
invoke echo 返回 arguments/options/context
```

用于验证 Plugin Host 全链路。

随后再迁移：

```text
plugins/import/
plugins/clone/
```

---

## 39. 总结

`bukit-plugin-v1` 是 Bukit Core 正式插件体系的基础协议。

它必须坚持：

```text
语言无关
跨平台
外部进程
JSON 协议
项目本地 plugins/
.bukit 仅存系统文件
Core 不加载插件代码
Core 不依赖插件实现
插件不污染 Core 进程
```

最终目标：

```text
Core 提供稳定底座和插件宿主。
Labs 孵化新功能。
成熟功能封装为跨平台外部进程插件。
正式插件通过 Core CLI 发布。
```
