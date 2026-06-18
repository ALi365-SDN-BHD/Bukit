# Bukit 插件配置规范

> 文档版本：v1.0
> 适用项目：Bukit 静态网站生成引擎
> 配套协议：`bukit-plugin-v1`
> 配套机制：Bukit Core 外部进程插件机制
> 文档类型：配置规范
> 状态：设计稿
> 优先级：P0

---

## 1. 文档目的

本文档定义 Bukit 插件体系中的配置文件、插件清单文件、锁文件、目录边界、字段结构、默认值、校验规则和错误处理规范。

本文档重点规范以下文件：

```text
.bukit/plugins.yaml
plugins/<plugin-id>/plugin.yaml
.bukit/plugins.lock.yaml
```

其中：

```text
.bukit/plugins.yaml
  = 项目级插件启用配置

plugins/<plugin-id>/plugin.yaml
  = 插件包自身 manifest

.bukit/plugins.lock.yaml
  = 插件解析结果锁文件
```

---

## 2. 核心原则

## 2.1 `.bukit/` 是系统工作目录

`.bukit/` 只能用于存放 Bukit 系统配置、锁文件、报告、缓存、日志、临时文件、状态文件。

允许：

```text
.bukit/plugins.yaml
.bukit/plugins.lock.yaml
.bukit/reports/
.bukit/cache/
.bukit/logs/
.bukit/tmp/
.bukit/state/
```

禁止：

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

`.bukit/` 内不得存放任何插件程序或 Core 会执行的文件。

---

## 2.2 `plugins/` 是插件程序目录

项目根目录下的 `plugins/` 是插件包存放目录。

允许：

```text
plugins/import/
plugins/clone/
plugins/notion/
plugins/visual/
```

每个插件包必须位于：

```text
plugins/<plugin-id>/
```

第一版不允许从以下路径加载插件：

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

## 2.3 `.bukit/plugins.yaml` 不直接声明可执行文件

`.bukit/plugins.yaml` 只负责声明插件是否启用、插件来源目录、暴露命令、授权权限、运行策略。

它不得直接声明：

```yaml
entry: plugins/import/bin/osx-arm64/bukit-plugin-import
```

正确方式：

```yaml
source: plugins/import
```

真实可执行入口必须由：

```text
plugins/<plugin-id>/plugin.yaml
```

中的 `platforms.<rid>.entry` 解析。

---

## 2.4 插件配置和站点配置分离

`site.yaml` 负责站点构建配置。

`.bukit/plugins.yaml` 负责插件启用配置。

边界：

```text
site.yaml
  = site / content / build / theme / taxonomy / logging / deploy / Core built-in Engine plugins

.bukit/plugins.yaml
  = external process feature plugins
```

不得在 `site.yaml` 中恢复或新增：

```yaml
site:
  externalPlugins:
```

---

## 3. 文件类型总览

| 文件                                        | 位置                | 作用           | 是否用户可编辑 |
| ----------------------------------------- | ----------------- | ------------ | ------- |
| `.bukit/plugins.yaml`                     | 项目根目录 `.bukit/`   | 项目级插件启用配置    | 是       |
| `plugins/<id>/plugin.yaml`                | 插件包目录             | 插件包 manifest | 插件作者维护  |
| `.bukit/plugins.lock.yaml`                | 项目根目录 `.bukit/`   | 插件解析锁文件      | 不建议手动编辑 |
| `.bukit/reports/plugin-executions/*.json` | `.bukit/reports/` | 插件执行报告       | 否       |

---

## 4. `.bukit/plugins.yaml` 规范

## 4.1 文件定位

`.bukit/plugins.yaml` 是项目级插件配置文件。

它控制：

* 插件是否启用
* 插件来源目录
* 插件命令是否暴露到 Core CLI
* 插件权限授权
* 插件超时策略
* 插件失败策略
* 插件输出限制
* 插件是否允许在 CI 中运行

它不控制：

* 站点内容源
* 主题路径
* 构建输出目录
* Core built-in Engine plugin
* 插件真实 executable entry
* 插件 manifest 内容

---

## 4.2 最小示例

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

---

## 4.3 多插件示例

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
    output:
      stdoutMaxBytes: 4194304
      stderrMaxBytes: 4194304
      responseMaxBytes: 4194304

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
      invokeMs: 180000

  visual:
    enabled: false
    source: plugins/visual
    exposeCommands:
      - visual
    permissions:
      fileSystem:
        read:
          - .
        write:
          - ./.bukit/reports
      network: false
      environment:
        read: []
```

---

## 4.4 顶层字段

| 字段        | 类型      | 必填 | 默认值  | 说明              |
| --------- | ------- | -: | ---- | --------------- |
| `version` | integer |  是 | 无    | 配置版本，v1 固定为 `1` |
| `plugins` | map     |  否 | `{}` | 插件配置集合          |

---

## 4.5 `plugins.<id>` 字段

| 字段               | 类型       | 必填 | 默认值      | 说明                        |
| ---------------- | -------- | -: | -------- | ------------------------- |
| `enabled`        | boolean  |  是 | 无        | 是否启用插件                    |
| `source`         | string   |  是 | 无        | 插件包目录，必须是 `plugins/<id>`  |
| `exposeCommands` | string[] |  否 | `[]`     | 暴露到 Core CLI 的命令          |
| `permissions`    | object   |  是 | 无        | 项目授予插件的权限                 |
| `timeout`        | object   |  否 | 默认超时     | 超时配置                      |
| `output`         | object   |  否 | 默认输出限制   | stdout/stderr/response 限制 |
| `failMode`       | string   |  否 | `strict` | 插件失败策略                    |
| `allowInCi`      | boolean  |  否 | `false`  | 是否允许 CI 默认运行              |
| `description`    | string   |  否 | null     | 项目本地说明                    |

---

## 4.6 `source` 规则

`source` 必须满足：

```text
1. 必须是相对路径。
2. 必须以 plugins/ 开头。
3. 必须指向 plugins/<plugin-id>。
4. 不得包含 .. 路径穿越。
5. 不得指向 .bukit。
6. 不得是绝对路径。
```

正确：

```yaml
source: plugins/import
source: plugins/clone
```

错误：

```yaml
source: .bukit/plugins/import
source: ../plugins/import
source: /opt/bukit/plugins/import
source: plugins/../evil
source: node_modules/.bin/plugin
```

错误提示建议：

```text
Plugin source must be inside project plugins/ directory: <source>
```

或：

```text
Plugin source cannot be inside .bukit: <source>
```

---

## 4.7 `enabled` 规则

如果：

```yaml
enabled: true
```

Core 可以加载该插件。

如果：

```yaml
enabled: false
```

Core 不执行插件，但如果 `exposeCommands` 声明了命令，Core 应保留 disabled command diagnostic。

例如：

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

不得返回：

```text
Unknown command: import
```

---

## 4.8 `exposeCommands` 规则

`exposeCommands` 用于控制插件 manifest 中哪些命令暴露到 Core CLI。

示例：

```yaml
exposeCommands:
  - import
```

规则：

```text
1. exposeCommands 中的命令必须存在于 plugin manifest commands。
2. 插件不得暴露 Core 内置命令同名命令。
3. 插件之间不得暴露同名命令。
4. alias 也不得冲突。
```

如果为空或缺失：

```yaml
exposeCommands: []
```

表示插件可被加载，但不向 Core CLI 暴露命令。

---

## 4.9 `permissions` 规则

`permissions` 表示项目授予插件的权限。

插件 manifest 中的 `requiredPermissions` 必须小于或等于 `.bukit/plugins.yaml` 中授予的权限。

校验关系：

```text
requiredPermissions <= granted permissions
```

如果插件要求的权限超过项目授予权限，Core 必须拒绝执行。

---

## 5. 权限配置规范

## 5.1 权限结构

```yaml
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

---

## 5.2 `fileSystem.read`

允许插件读取的项目内路径。

示例：

```yaml
fileSystem:
  read:
    - .
    - ./content
    - ./themes
```

规则：

```text
1. 必须是项目内相对路径。
2. 不得是绝对路径。
3. 不得包含路径穿越。
4. 不得指向 .bukit 内部敏感文件，除非明确允许只读报告。
5. 不得默认允许读取用户 home 目录。
```

---

## 5.3 `fileSystem.write`

允许插件写入的项目内路径。

示例：

```yaml
fileSystem:
  write:
    - ./sites
    - ./themes
    - ./content
```

规则：

```text
1. 必须是项目内相对路径。
2. 不得是绝对路径。
3. 不得包含路径穿越。
4. 不得写入 .bukit/plugins 或 .bukit/bin 等可执行区域。
5. 不得写入项目根目录外。
```

`.bukit/reports` 可允许写入，但建议由 Core 写报告，不建议插件直接写。

---

## 5.4 `network`

是否允许插件访问网络。

```yaml
network: false
```

表示插件不应访问网络。

第一版可以只做权限声明和报告记录；后续可扩展 OS 级沙箱或网络隔离。

正式插件如果需要网络，应明确写：

```yaml
network: true
```

例如 `clone` 可能需要网络下载资源。

---

## 5.5 `environment.read`

允许插件读取的环境变量。

示例：

```yaml
environment:
  read:
    - NOTION_TOKEN
```

规则：

```text
1. Core 只传递 allowlist 中的环境变量。
2. 未授权环境变量不应传入插件进程。
3. 执行报告中所有环境变量值必须打码。
4. 插件不得要求读取所有环境变量。
```

错误示例：

```yaml
environment:
  read:
    - "*"
```

第一版应拒绝通配符。

---

## 6. 超时配置规范

## 6.1 示例

```yaml
timeout:
  handshakeMs: 5000
  manifestMs: 5000
  invokeMs: 120000
```

---

## 6.2 字段说明

| 字段            | 类型      |    默认值 | 说明           |
| ------------- | ------- | -----: | ------------ |
| `handshakeMs` | integer |   5000 | handshake 超时 |
| `manifestMs`  | integer |   5000 | manifest 超时  |
| `invokeMs`    | integer | 120000 | invoke 超时    |

---

## 6.3 校验规则

```text
1. 所有 timeout 必须为正整数。
2. 不得小于 1000。
3. 不得超过 Core 允许的最大值。
```

建议最大值：

| 字段            |    最大值 |
| ------------- | -----: |
| `handshakeMs` |  30000 |
| `manifestMs`  |  30000 |
| `invokeMs`    | 600000 |

---

## 7. 输出限制配置规范

## 7.1 示例

```yaml
output:
  stdoutMaxBytes: 4194304
  stderrMaxBytes: 4194304
  responseMaxBytes: 4194304
```

---

## 7.2 字段说明

| 字段                 | 类型      |     默认值 | 说明                  |
| ------------------ | ------- | ------: | ------------------- |
| `stdoutMaxBytes`   | integer | 4194304 | stdout 最大字节数        |
| `stderrMaxBytes`   | integer | 4194304 | stderr 最大字节数        |
| `responseMaxBytes` | integer | 4194304 | JSON response 最大字节数 |

---

## 7.3 校验规则

```text
1. 必须为正整数。
2. 不得超过 Core 允许最大值。
3. 超过限制应终止进程或拒绝结果。
```

建议最大值：

```text
16777216
```

即 16 MB。

---

## 8. 失败策略配置规范

## 8.1 示例

```yaml
failMode: strict
```

可选值：

```text
strict
warn
```

---

## 8.2 `strict`

插件加载、校验或执行失败时，命令失败。

推荐默认值：

```text
strict
```

---

## 8.3 `warn`

仅用于非关键插件。

如果插件失败，Core 输出 warning 并继续。

注意：

```text
CLI command plugin 不建议使用 warn。
Build hook 类插件未来可考虑 warn。
```

v1 只做 CLI command plugin，因此建议全部默认 strict。

---

## 9. CI 运行配置

## 9.1 `allowInCi`

```yaml
allowInCi: false
```

默认值：

```text
false
```

CI 环境中，外部插件默认不应自动运行，除非满足：

```text
1. allowInCi = true
2. sha256 已声明并校验通过
3. permissions 明确
4. source 在 plugins/<id>
5. entry 不在 .bukit
```

---

## 10. `plugins/<id>/plugin.yaml` Manifest 规范

## 10.1 文件定位

`plugins/<id>/plugin.yaml` 是插件包自身 manifest。

由插件作者维护。

`.bukit/plugins.yaml` 授权项目使用插件。

`plugin.yaml` 声明插件自身能力。

---

## 10.2 示例

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

## 10.3 顶层字段

| 字段                    | 类型     | 必填 | 说明                          |
| --------------------- | ------ | -: | --------------------------- |
| `id`                  | string |  是 | 插件 ID                       |
| `name`                | string |  是 | 插件名称                        |
| `version`             | string |  是 | 插件版本                        |
| `protocol`            | string |  是 | 协议版本，v1 为 `bukit-plugin-v1` |
| `kind`                | string |  是 | v1 固定为 `process`            |
| `distribution`        | string |  是 | 分发类型                        |
| `platforms`           | map    |  是 | 平台入口                        |
| `commands`            | list   |  否 | CLI 命令清单                    |
| `requiredPermissions` | object |  是 | 插件需要的权限                     |

---

## 10.4 `id` 规则

插件 ID 必须满足：

```text
1. 小写字母、数字、短横线。
2. 不能包含空格。
3. 不能包含路径分隔符。
4. 不能是 "." 或 ".."。
5. 应与目录名一致。
```

正确：

```text
import
clone
notion
visual
site-importer
```

错误：

```text
Import
clone/plugin
../evil
my plugin
```

---

## 10.5 `version` 规则

`version` 应使用 SemVer：

```text
1.0.0
1.2.3
2.0.0-beta.1
```

第一版 Core 至少应校验基础版本格式。

---

## 10.6 `protocol` 规则

v1 固定为：

```text
bukit-plugin-v1
```

如果不匹配，Core 必须拒绝加载。

---

## 10.7 `kind` 规则

v1 只允许：

```text
process
```

不允许：

```text
dll
wasm
docker
script
```

---

## 10.8 `distribution` 规则

允许：

```text
self-contained
runtime-dependent
```

推荐正式插件使用：

```text
self-contained
```

如果为 `runtime-dependent`，必须在 manifest 中声明 runtime。

示例：

```yaml
runtime:
  type: node
  version: ">=20"
```

v1 可以先拒绝 `runtime-dependent`，只支持 `self-contained`。

---

## 11. 平台入口配置

## 11.1 `platforms`

示例：

```yaml
platforms:
  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-import
    sha256: "<sha256>"
```

---

## 11.2 支持的平台 ID

| 平台 ID         | 含义                  |
| ------------- | ------------------- |
| `win-x64`     | Windows x64         |
| `win-arm64`   | Windows ARM64       |
| `linux-x64`   | Linux x64           |
| `linux-arm64` | Linux ARM64         |
| `osx-x64`     | macOS Intel         |
| `osx-arm64`   | macOS Apple Silicon |

---

## 11.3 `entry` 规则

`entry` 必须满足：

```text
1. 相对于插件包根目录。
2. 不得是绝对路径。
3. 不得包含路径穿越。
4. 不得指向 .bukit。
5. 必须存在。
6. 在 Unix-like 系统上应具备可执行权限。
```

正确：

```yaml
entry: bin/osx-arm64/bukit-plugin-import
```

错误：

```yaml
entry: /usr/local/bin/plugin
entry: ../../evil
entry: .bukit/bin/plugin
entry: C:\tools\plugin.exe
```

---

## 11.4 `sha256` 规则

正式插件必须提供 `sha256`。

Core 必须校验 entry 文件 hash。

如果不匹配，拒绝加载。

错误码建议：

```text
plugin.sha256Mismatch
```

---

## 12. Commands Manifest 配置

## 12.1 示例

```yaml
commands:
  - name: import
    description: Import external content into Bukit
    aliases: []
    arguments: []
    options:
      - name: --config
        type: string
        description: Config file path
        required: false
    subcommands:
      - name: html-demo
        description: Import static HTML demo
        arguments:
          - name: demo-dir
            description: HTML demo directory
            required: true
        options:
          - name: --theme
            type: string
            description: Target theme name
            required: true
          - name: --force
            type: flag
            description: Overwrite existing theme
            required: false
```

---

## 12.2 Command 字段

| 字段            | 类型       | 必填 | 说明   |
| ------------- | -------- | -: | ---- |
| `name`        | string   |  是 | 命令名  |
| `description` | string   |  是 | 命令说明 |
| `aliases`     | string[] |  否 | 别名   |
| `arguments`   | list     |  否 | 参数   |
| `options`     | list     |  否 | 选项   |
| `subcommands` | list     |  否 | 子命令  |

---

## 12.3 Option 字段

| 字段              | 类型       | 必填 | 说明   |
| --------------- | -------- | -: | ---- |
| `name`          | string   |  是 | 选项名  |
| `type`          | string   |  是 | 选项类型 |
| `description`   | string   |  是 | 说明   |
| `required`      | boolean  |  否 | 是否必填 |
| `valueName`     | string   |  否 | 值名称  |
| `allowedValues` | string[] |  否 | 允许值  |
| `conflictWith`  | string   |  否 | 冲突选项 |

Option type：

```text
string
integer
number
boolean
flag
```

---

## 12.4 Argument 字段

| 字段            | 类型      | 必填 | 说明   |
| ------------- | ------- | -: | ---- |
| `name`        | string  |  是 | 参数名  |
| `description` | string  |  是 | 参数说明 |
| `required`    | boolean |  否 | 是否必填 |

---

## 12.5 命令冲突规则

Core 必须校验：

```text
1. 插件命令不得覆盖 Core command。
2. 插件之间不得注册同名 command。
3. alias 不得和 Core command 冲突。
4. alias 不得和其他插件 command 或 alias 冲突。
```

冲突时拒绝加载插件命令。

---

## 13. `requiredPermissions` 配置

`requiredPermissions` 表示插件自身所需权限。

示例：

```yaml
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

Core 必须比较：

```text
plugin.yaml requiredPermissions
  <=
.bukit/plugins.yaml permissions
```

如果插件要求更高权限，拒绝执行。

---

## 14. `.bukit/plugins.lock.yaml` 规范

## 14.1 文件定位

`.bukit/plugins.lock.yaml` 是插件解析锁文件。

它由 Core 生成。

不建议用户手动编辑。

---

## 14.2 示例

```yaml
version: 1

resolved:
  import:
    source: plugins/import
    manifestVersion: 1.0.0
    protocol: bukit-plugin-v1
    platform: osx-arm64
    entry: plugins/import/bin/osx-arm64/bukit-plugin-import
    sha256: "<sha256>"
    commands:
      - import
    resolvedAt: "2026-06-17T00:00:00Z"

  clone:
    source: plugins/clone
    manifestVersion: 1.0.0
    protocol: bukit-plugin-v1
    platform: osx-arm64
    entry: plugins/clone/bin/osx-arm64/bukit-plugin-clone
    sha256: "<sha256>"
    commands:
      - clone
    resolvedAt: "2026-06-17T00:00:00Z"
```

---

## 14.3 Lock 字段说明

| 字段                | 类型       | 说明             |
| ----------------- | -------- | -------------- |
| `source`          | string   | 插件 source      |
| `manifestVersion` | string   | 插件 manifest 版本 |
| `protocol`        | string   | 协议版本           |
| `platform`        | string   | 当前平台           |
| `entry`           | string   | 解析后的执行入口       |
| `sha256`          | string   | 已校验 hash       |
| `commands`        | string[] | 已暴露命令          |
| `resolvedAt`      | string   | 解析时间           |

---

## 14.4 Lock 文件规则

```text
1. lock 文件可以记录 entry。
2. lock 文件不能存放插件程序。
3. lock 文件中的 entry 必须仍然指向 plugins/<id>/。
4. 如果 plugin.yaml 变化，lock 应更新。
5. 如果 sha256 变化，lock 应更新。
```

---

## 15. 默认行为

## 15.1 `.bukit/plugins.yaml` 不存在

如果 `.bukit/plugins.yaml` 不存在：

```text
Core 不加载任何外部进程插件。
Core stable commands 仍正常可用。
```

这可以保证安全默认。

---

## 15.2 `plugins/` 不存在

如果项目根目录没有 `plugins/`：

```text
Core 不加载任何外部进程插件。
```

不应报错，除非用户执行了某个插件命令。

---

## 15.3 插件配置存在但 source 不存在

如果 `.bukit/plugins.yaml` 中启用了插件：

```yaml
plugins:
  import:
    enabled: true
    source: plugins/import
```

但 `plugins/import` 不存在，Core 应报告配置错误。

错误码建议：

```text
plugin.sourceNotFound
```

---

## 16. 错误码建议

| 错误码                           | 含义                       |
| ----------------------------- | ------------------------ |
| `plugin.configMissingVersion` | plugins.yaml 缺少 version  |
| `plugin.configInvalidVersion` | plugins.yaml version 不支持 |
| `plugin.sourceNotFound`       | 插件 source 不存在            |
| `plugin.sourceOutsidePlugins` | source 不在 plugins/ 下     |
| `plugin.sourceInsideBukitDir` | source 位于 .bukit 内       |
| `plugin.manifestNotFound`     | plugin.yaml 不存在          |
| `plugin.manifestInvalid`      | plugin.yaml 无效           |
| `plugin.protocolUnsupported`  | 协议不支持                    |
| `plugin.platformUnsupported`  | 当前平台不支持                  |
| `plugin.entryNotFound`        | entry 文件不存在              |
| `plugin.entryPathTraversal`   | entry 包含路径穿越             |
| `plugin.entryInsideBukitDir`  | entry 位于 .bukit 内        |
| `plugin.sha256Mismatch`       | hash 不匹配                 |
| `plugin.permissionDenied`     | 权限不足                     |
| `plugin.commandConflict`      | 命令冲突                     |
| `plugin.commandDisabled`      | 命令被禁用                    |
| `plugin.invalidTimeout`       | timeout 配置无效             |
| `plugin.invalidOutputLimit`   | output limit 配置无效        |

---

## 17. 配置加载流程

Core Plugin Host 应按以下顺序加载配置：

```text
1. Resolve project root.
2. Check .bukit/plugins.yaml.
3. If missing, return empty plugin set.
4. Parse YAML.
5. Validate top-level version.
6. Validate plugins map.
7. For each plugin:
   7.1 Validate plugin id.
   7.2 Validate enabled.
   7.3 Validate source.
   7.4 Reject source inside .bukit.
   7.5 Reject source outside plugins/.
   7.6 Load plugins/<id>/plugin.yaml.
   7.7 Validate plugin manifest.
   7.8 Resolve platform entry.
   7.9 Validate entry.
   7.10 Validate sha256.
   7.11 Validate permissions.
   7.12 Register command descriptors.
8. Write .bukit/plugins.lock.yaml.
```

---

## 18. 配置与协议关系

配置文件负责声明静态信息：

```text
.bukit/plugins.yaml
plugins/<id>/plugin.yaml
```

协议负责运行时交互：

```text
handshake
manifest
invoke
```

Core 必须同时校验：

```text
plugin.yaml manifest
  和
runtime manifest response
```

如果两者不一致，应拒绝加载或记录诊断。

---

## 19. 安全校验规则汇总

必须拒绝：

```text
source 是绝对路径
source 包含 ..
source 位于 .bukit
source 不在 plugins/
plugin.yaml 缺失
plugin.yaml 协议不匹配
platforms 当前平台缺失
entry 是绝对路径
entry 包含 ..
entry 位于 .bukit
entry 文件不存在
sha256 不匹配
requiredPermissions 超过 granted permissions
exposeCommands 不存在于 manifest commands
插件命令覆盖 Core command
插件命令互相冲突
```

---

## 20. 示例：Import 插件完整配置

`.bukit/plugins.yaml`：

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
    output:
      stdoutMaxBytes: 4194304
      stderrMaxBytes: 4194304
      responseMaxBytes: 4194304
    failMode: strict
    allowInCi: true
```

`plugins/import/plugin.yaml`：

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

## 21. 示例：禁用 Clone 插件

```yaml
version: 1

plugins:
  clone:
    enabled: false
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
      network: true
      environment:
        read: []
```

执行：

```bash
bukit clone --tokens tokens.json --theme cloned
```

输出：

```text
Command disabled by plugin config: clone
```

Exit code：

```text
2
```

---

## 22. JSON Schema 文件要求

应新增以下 schema：

```text
schemas/bukit-plugin-config.schema.json
schemas/bukit-plugin-manifest.schema.json
schemas/bukit-plugin-lock.schema.json
```

---

## 23. 验收标准

本配置规范落地后，应满足：

```text
1. Core 能读取 .bukit/plugins.yaml。
2. Core 能拒绝无效 source。
3. Core 能拒绝 .bukit 内插件程序。
4. Core 能读取 plugins/<id>/plugin.yaml。
5. Core 能解析平台 entry。
6. Core 能校验 sha256。
7. Core 能校验 permissions。
8. Core 能生成 plugins.lock.yaml。
9. Core 能注册 exposeCommands。
10. Core 能正确处理 disabled plugin command。
11. Core 能检测 command conflict。
12. Core 能在 plugins.yaml 缺失时安全降级为空插件集。
```

---

## 24. 总结

Bukit 插件配置体系由三部分组成：

```text
.bukit/plugins.yaml
  项目插件启用配置

plugins/<id>/plugin.yaml
  插件包 manifest

.bukit/plugins.lock.yaml
  插件解析锁文件
```

核心边界：

```text
.bukit/
  系统配置、锁文件、报告、缓存、日志、状态

plugins/
  插件程序、插件包、插件 manifest、跨平台可执行入口
```

核心原则：

```text
Core 不从 .bukit 执行任何程序。
Core 只从 plugins/<id>/ 加载插件包。
Core 通过 plugin.yaml 解析平台入口。
Core 通过 bukit-plugin-v1 调用外部进程插件。
Core 通过 .bukit/plugins.yaml 控制插件启用、命令暴露和权限授权。
```
