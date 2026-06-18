# ADR: Bukit 插件安全模型

> ADR 编号：ADR-BUKIT-PLUGIN-SECURITY-MODEL
> 状态：Proposed
> 日期：2026-06-18
> 适用项目：Bukit 静态网站生成引擎
> 决策类型：插件安全边界 / 外部进程执行 / 权限模型 / 路径安全
> 关联文档：
>
> * Bukit Core 插件机制设计文档
> * Bukit 插件协议 v1 规范
> * Bukit 插件配置规范
> * Bukit Labs → Plugin → Core 发布准入规范
> * ADR-BUKIT-PLUGIN-DIRECTORY-LAYOUT
> * Bukit Import 插件迁移计划
> * Bukit Clone 插件迁移计划

---

## 1. 背景

Bukit 正在从单一 CLI 工具演进为支持正式插件体系的静态网站生成引擎。

新的插件模型已经确定：

```text
Core
  稳定基础底座
  插件宿主
  Core 内置插件

Plugin
  正式发布功能模块
  外部进程
  语言无关
  跨平台

Labs
  未成熟功能孵化区
```

新的插件机制要求：

```text
1. 除 Core 内置插件外，正式插件全部采用外部进程模式。
2. Core 不加载插件 DLL。
3. Core 不依赖插件实现。
4. 插件不限制开发语言。
5. 插件必须具备跨平台能力。
6. 插件通过 bukit-plugin-v1 JSON 协议与 Core 通信。
7. 插件程序必须放在项目根目录 plugins/。
8. .bukit/ 只存系统配置、锁文件、报告、缓存、日志、临时文件和状态文件。
9. .bukit/ 内禁止存放任何可执行插件程序。
```

由于插件是外部进程，理论上可能执行文件读写、网络访问、环境变量读取、生成代码、下载资源、修改配置等操作，因此必须在 Core 层建立明确的插件安全模型。

---

## 2. 问题

Bukit 插件体系需要解决以下安全问题：

1. 插件程序允许放在哪里？
2. `.bukit/` 是否允许存放可执行文件？
3. Core 是否允许加载插件 DLL？
4. Core 如何防止插件路径穿越？
5. Core 如何防止插件从项目外部执行？
6. Core 如何校验插件来源？
7. Core 如何校验插件二进制未被篡改？
8. Core 如何控制插件读取文件？
9. Core 如何控制插件写入文件？
10. Core 如何控制插件访问网络？
11. Core 如何控制插件读取环境变量？
12. Core 如何处理插件 stdout / stderr？
13. Core 如何处理插件超时？
14. Core 如何处理插件错误、崩溃和非零退出码？
15. Core 如何在 CI 环境下安全运行插件？
16. Core 如何记录插件执行审计报告？
17. Labs 功能迁移为正式 Plugin 前需要满足哪些安全门禁？

---

## 3. 决策

Bukit 正式采用以下插件安全模型：

```text
External Process + Manifest Verification + Path Boundary + Permission Declaration + Runtime Guard + Execution Report
```

中文定义：

```text
外部进程隔离 + Manifest 校验 + 路径边界 + 权限声明 + 运行时防护 + 执行审计报告
```

核心决策如下：

```text
1. Core 不加载第三方插件代码。
2. 正式插件只能作为外部进程运行。
3. 插件只能从项目根目录 plugins/<id>/ 加载。
4. .bukit/ 内禁止存放和执行任何插件程序。
5. 插件必须提供 plugin.yaml。
6. 插件必须声明平台入口和 sha256。
7. Core 必须校验插件路径、平台入口、sha256、协议版本和权限。
8. 插件必须实现 bukit-plugin-v1。
9. 插件 stdout 只允许输出 JSON response。
10. 插件 stderr 只用于日志。
11. 插件必须受 timeout 和输出大小限制。
12. 插件权限必须显式授权。
13. 插件执行必须生成审计报告。
14. CI 环境默认采用更严格策略。
```

---

## 4. 安全目标

Bukit 插件安全模型的目标是：

```text
1. 防止 Core 进程被插件代码污染。
2. 防止插件绕过项目目录边界。
3. 防止插件从 .bukit/ 隐藏目录执行。
4. 防止路径穿越攻击。
5. 防止插件覆盖 Core 内置命令。
6. 防止插件读取未授权环境变量。
7. 防止插件写入未授权目录。
8. 防止插件二进制被篡改后执行。
9. 防止插件 stdout 日志污染协议解析。
10. 防止插件无限运行或输出过大。
11. 保证插件执行可追踪、可审计、可回滚。
12. 保持 Core Native AOT 友好。
13. 保持插件语言无关与跨平台能力。
```

---

## 5. 非目标

本 ADR 第一版不承诺提供完整 OS 级沙箱。

第一版不做：

```text
1. 容器级隔离。
2. Docker 插件运行时。
3. WASM 沙箱。
4. Seccomp / AppArmor / SELinux 策略生成。
5. macOS sandbox-exec 集成。
6. Windows Job Object 完整隔离。
7. 网络 namespace 隔离。
8. 远程插件签名服务。
9. 插件市场信任体系。
10. 插件自动下载。
11. 全局插件目录。
12. 用户 Home 插件缓存。
13. 插件自动更新。
14. 插件依赖解析。
```

第一版重点是：

```text
项目本地 plugins/ 外部进程插件的安全边界、校验、权限声明和审计。
```

---

## 6. 插件执行模式安全决策

### 6.1 正式插件必须是外部进程

正式插件必须作为 external process 运行。

允许：

```text
plugins/import/bin/osx-arm64/bukit-plugin-import
plugins/clone/bin/linux-x64/bukit-plugin-clone
```

禁止：

```text
Assembly.LoadFrom(...)
反射加载插件 DLL
运行时加载第三方 .NET assembly
in-process 第三方插件
动态脚本注入 Core 进程
```

理由：

```text
1. 避免插件破坏 Core 进程。
2. 保持 Native AOT 友好。
3. 支持语言无关插件。
4. 支持跨平台二进制分发。
5. 降低 Core 与插件实现的版本耦合。
```

---

### 6.2 Core 不依赖插件实现

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
      -> external process plugin
```

---

### 6.3 插件不依赖 Labs

正式插件不得依赖 Labs。

禁止：

```text
Bukit.Plugin.Import -> Bukit.Labs.Import
Bukit.Plugin.Clone -> Bukit.Labs.Clone
Bukit.Plugin.Notion -> Bukit.Labs.Cli
```

如果需要复用 Labs 逻辑，必须先抽离到稳定领域库：

```text
src/Bukit.Importing
src/Bukit.Clone
src/Bukit.Shared
src/Bukit.Plugin.Abstractions
```

---

## 7. 目录安全模型

### 7.1 `.bukit/` 是系统工作目录

`.bukit/` 只允许存放：

```text
.bukit/plugins.yaml
.bukit/plugins.lock.yaml
.bukit/reports/
.bukit/cache/
.bukit/logs/
.bukit/tmp/
.bukit/state/
```

`.bukit/` 禁止存放：

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

### 7.2 插件程序必须位于 `plugins/`

用户项目中的插件包必须位于：

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

第一版只允许项目本地插件目录：

```text
project-root/plugins/<plugin-id>/
```

---

### 7.3 禁止插件来源路径

Core Plugin Host 必须拒绝以下 source：

```yaml
source: .bukit/plugins/import
source: ../plugins/import
source: /absolute/path/to/plugin
source: C:\tools\plugin
source: ~/plugins/import
source: ~/.bukit/plugins/import
source: node_modules/.bin/plugin
source: /tmp/plugin
source: plugins/../evil
```

错误码建议：

```text
plugin.sourceInsideBukitDir
plugin.sourceOutsidePlugins
plugin.sourcePathTraversal
plugin.sourceAbsolutePath
```

---

### 7.4 禁止插件入口路径

Core Plugin Host 必须拒绝以下 entry：

```yaml
entry: ../../evil
entry: /usr/local/bin/tool
entry: C:\tools\plugin.exe
entry: .bukit/bin/tool
entry: ../plugin
```

错误码建议：

```text
plugin.entryPathTraversal
plugin.entryAbsolutePath
plugin.entryInsideBukitDir
plugin.entryNotFound
```

---

### 7.5 解析后路径必须再次校验

Core 不能只校验字符串。

必须执行：

```text
1. 解析 projectRoot。
2. 解析 source full path。
3. Normalize / GetFullPath。
4. 确认 source 在 projectRoot/plugins/ 内。
5. 确认 source 不在 projectRoot/.bukit/ 内。
6. 解析 plugin.yaml 中 entry。
7. Normalize / GetFullPath。
8. 确认 entry 在 source 内。
9. 确认 entry 不在 .bukit/ 内。
10. 确认 entry 存在。
```

---

## 8. Manifest 安全模型

### 8.1 插件必须提供 `plugin.yaml`

正式插件必须提供：

```text
plugins/<plugin-id>/plugin.yaml
```

缺失时拒绝加载。

错误码：

```text
plugin.manifestNotFound
```

---

### 8.2 Manifest 必须声明协议

必须为：

```yaml
protocol: bukit-plugin-v1
```

不匹配时拒绝加载。

错误码：

```text
plugin.protocolUnsupported
```

---

### 8.3 Manifest 必须声明类型

v1 只允许：

```yaml
kind: process
```

拒绝：

```yaml
kind: dll
kind: wasm
kind: docker
kind: script
```

错误码：

```text
plugin.kindUnsupported
```

---

### 8.4 Manifest 必须声明平台入口

示例：

```yaml
platforms:
  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-import
    sha256: "<sha256>"
```

当前平台不存在时拒绝加载。

错误码：

```text
plugin.platformUnsupported
```

---

### 8.5 Manifest 必须声明 sha256

正式插件必须为每个平台入口声明 sha256。

示例：

```yaml
sha256: "8f4c..."
```

缺失时：

```text
非 CI 可警告或拒绝，建议拒绝。
CI 必须拒绝。
正式发布必须拒绝。
```

推荐第一版统一拒绝缺失 sha256。

错误码：

```text
plugin.sha256Missing
```

---

## 9. 二进制完整性校验

### 9.1 Core 必须校验 sha256

Core Plugin Host 必须对 entry 文件计算 sha256，并与 `plugin.yaml` 中声明的 sha256 比对。

不匹配时拒绝执行。

错误码：

```text
plugin.sha256Mismatch
```

---

### 9.2 Lock 文件记录已校验结果

成功解析后写入：

```text
.bukit/plugins.lock.yaml
```

示例：

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
    sha256Verified: true
    resolvedAt: "2026-06-18T00:00:00Z"
```

---

### 9.3 Lock 文件不能绕过 Manifest 校验

`.bukit/plugins.lock.yaml` 只能作为审计和可重复构建辅助。

Core 不得只根据 lock 文件直接执行插件。

每次执行前仍必须校验：

```text
source
plugin.yaml
entry
sha256
protocol
permissions
```

---

## 10. 权限模型

### 10.1 权限采用声明式模型

插件 manifest 声明所需权限：

```yaml
requiredPermissions:
  fileSystem:
    read:
      - .
    write:
      - ./themes
      - ./content
  network: false
  environment:
    read: []
```

项目配置授予权限：

```yaml
permissions:
  fileSystem:
    read:
      - .
    write:
      - ./themes
      - ./content
  network: false
  environment:
    read: []
```

Core 必须校验：

```text
requiredPermissions <= grantedPermissions
```

---

### 10.2 权限不足时拒绝执行

如果插件要求：

```yaml
requiredPermissions:
  network: true
```

但项目授权：

```yaml
permissions:
  network: false
```

则必须拒绝执行。

错误码：

```text
plugin.permissionDenied
```

---

### 10.3 权限不是完整沙箱

第一版权限模型主要负责：

```text
1. 明确风险。
2. 阻止明显越权配置。
3. 控制 Core 传递给插件的上下文。
4. 控制 Core 传递给插件的环境变量。
5. 记录执行审计报告。
```

第一版不承诺 OS 级强沙箱。

文档必须明确：

```text
Plugin permissions in v1 are declarative and host-enforced at configuration,
request, environment, and reporting boundaries. They are not a complete OS-level sandbox.
```

---

## 11. 文件系统权限

### 11.1 读权限

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
4. 不得默认允许读取用户 home。
5. 不得默认允许读取系统目录。
6. 不得默认读取 .bukit 中敏感文件。
```

---

### 11.2 写权限

示例：

```yaml
fileSystem:
  write:
    - ./themes
    - ./content
    - ./data
```

规则：

```text
1. 必须是项目内相对路径。
2. 不得是绝对路径。
3. 不得包含路径穿越。
4. 不得写入项目根目录外。
5. 不得写入 .bukit/plugins、.bukit/bin、.bukit/tools 等可执行区域。
6. 不得写入 Core 可执行目录。
```

---

### 11.3 `.bukit/reports` 写入策略

推荐由 Core Plugin Host 写插件执行报告。

插件不应直接写：

```text
.bukit/reports/plugin-executions/
```

插件可以通过 invoke response 返回 artifacts / diagnostics，由 Core 写入报告。

如需允许插件写特定报告，应显式授权：

```yaml
fileSystem:
  write:
    - ./.bukit/reports/custom-plugin-output
```

但第一版不推荐。

---

### 11.4 文件路径规范

协议中的路径统一使用 `/`。

允许：

```text
themes/silkroadbiz
content/posts/hello.md
docs/research/VERIFY_REPORT.md
```

禁止：

```text
../outside
C:\Users\name\file
/Users/name/file
.bukit/bin/tool
```

---

## 12. 网络权限

### 12.1 默认网络关闭

默认：

```yaml
network: false
```

需要网络的插件必须明确声明：

```yaml
network: true
```

例如 Clone 插件下载远程 assets 时需要：

```yaml
network: true
```

---

### 12.2 Core 校验网络权限

如果 manifest 要求：

```yaml
requiredPermissions:
  network: true
```

但 `.bukit/plugins.yaml` 授权：

```yaml
permissions:
  network: false
```

Core 必须拒绝执行。

---

### 12.3 v1 网络权限限制

第一版不提供 OS 级网络封锁。

但必须做到：

```text
1. manifest 声明网络需求。
2. project config 显式授权。
3. execution report 记录 network 权限。
4. CI 中网络插件默认需要 allowInCi=true。
```

---

## 13. 环境变量权限

### 13.1 环境变量采用 allowlist

示例：

```yaml
environment:
  read:
    - NOTION_TOKEN
```

Core 只允许传递 allowlist 中的环境变量。

禁止：

```yaml
environment:
  read:
    - "*"
```

错误码：

```text
plugin.environmentWildcardDenied
```

---

### 13.2 默认不传递环境变量

默认：

```yaml
environment:
  read: []
```

Core 启动插件进程时，应尽量构建最小环境变量集合。

建议只传递：

```text
1. 必需系统环境变量。
2. 明确 allowlist 的业务环境变量。
3. Bukit 协议需要的环境变量。
```

---

### 13.3 Secret 必须打码

执行报告中不得记录 secret 原文。

示例：

```json
{
  "environment": {
    "read": ["NOTION_TOKEN"],
    "values": {
      "NOTION_TOKEN": "***"
    }
  }
}
```

---

## 14. 外部进程启动安全

### 14.1 禁止 Shell 拼接

Core 禁止使用：

```text
sh -c
cmd /c
powershell -Command
bash script.sh
shell string concatenation
```

---

### 14.2 必须使用直接进程启动

.NET 示例：

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

### 14.3 参数传递策略

推荐：

```text
stdin JSON request
```

可选：

```text
--protocol bukit-plugin-v1
```

但实际命令参数、路径、权限、上下文必须通过 stdin JSON request 传递。

不得通过 shell 字符串拼接用户输入。

---

## 15. stdout / stderr 安全

### 15.1 stdout 只允许 JSON response

插件 stdout 必须是一个完整 JSON object。

禁止输出：

```text
普通日志
调试文本
进度条
ANSI 控制符
多段 JSON
非 JSON 文本
```

错误码：

```text
plugin.invalidResponse
```

---

### 15.2 stderr 用于日志

插件日志必须写入 stderr。

Core 捕获 stderr 并写入 execution report。

---

### 15.3 输出大小限制

默认限制：

```text
stdout: 4 MB
stderr: 4 MB
response JSON: 4 MB
```

超过限制时，Core 应终止插件进程并报告：

```text
plugin.outputTooLarge
```

---

## 16. Timeout 安全

### 16.1 默认超时

| 操作        |      默认超时 |
| --------- | --------: |
| handshake |   5000 ms |
| manifest  |   5000 ms |
| invoke    | 120000 ms |

---

### 16.2 超时行为

插件超时时，Core 必须：

```text
1. 终止插件进程。
2. 标记执行失败。
3. 写入 execution report。
4. 返回 plugin.timeout。
```

---

### 16.3 超时配置上限

建议最大值：

| 操作        |       最大值 |
| --------- | --------: |
| handshake |  30000 ms |
| manifest  |  30000 ms |
| invoke    | 600000 ms |

---

## 17. 命令注册安全

### 17.1 Core 命令优先

插件不得覆盖 Core 内置命令。

Core commands 示例：

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

如果插件 manifest 声明同名命令，拒绝暴露。

错误码：

```text
plugin.commandConflict
```

---

### 17.2 插件命令不得互相冲突

两个插件不得注册同名命令。

例如：

```text
import plugin -> import
another plugin -> import
```

必须拒绝后加载者或拒绝整体加载。

---

### 17.3 Alias 也必须校验

插件 alias 不得与：

```text
1. Core command
2. Core alias
3. 其他插件 command
4. 其他插件 alias
```

冲突。

---

### 17.4 Disabled command 行为

如果插件配置为：

```yaml
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

## 18. CI 安全模型

### 18.1 CI 默认更严格

在 CI 环境中，外部插件默认不应自动运行。

插件要在 CI 中运行必须满足：

```text
1. allowInCi = true
2. source 在 plugins/<id>
3. entry 不在 .bukit
4. sha256 已声明
5. sha256 校验通过
6. permissions 明确
7. protocol 匹配
8. manifest 校验通过
```

---

### 18.2 CI 禁止 runtime-dependent 插件

第一版建议 CI 拒绝：

```yaml
distribution: runtime-dependent
```

除非 CI 明确安装 runtime 并且配置允许。

正式发布插件推荐：

```yaml
distribution: self-contained
```

---

### 18.3 CI 报告

CI 中插件执行必须产出：

```text
.bukit/reports/plugin-executions/*.json
```

并纳入 artifacts 或测试报告。

---

## 19. Labs → Plugin 安全准入

Labs 功能迁移为正式 Plugin 前，必须通过安全准入：

```text
[ ] 不依赖 Labs。
[ ] 不被 Core 直接引用。
[ ] 实现 external process。
[ ] 实现 bukit-plugin-v1。
[ ] 提供 plugin.yaml。
[ ] 插件包位于 plugins/<id>/。
[ ] .bukit 不存放可执行程序。
[ ] source 路径校验通过。
[ ] entry 路径校验通过。
[ ] sha256 校验通过。
[ ] permissions 声明完整。
[ ] requiredPermissions <= grantedPermissions。
[ ] stdout 只输出 JSON。
[ ] stderr 只输出日志。
[ ] timeout 测试通过。
[ ] output limit 测试通过。
[ ] disabled command 行为正确。
[ ] command conflict 测试通过。
[ ] CI allowInCi 策略明确。
[ ] execution report 生成。
[ ] secrets 打码。
```

---

## 20. Import 插件安全要求

Import Plugin 需要重点关注：

```text
1. HTML demo 输入目录路径安全。
2. route-map 路径安全。
3. theme 名称安全。
4. site.yaml 写入安全。
5. content / themes / sites 写入安全。
6. import-report artifact 路径安全。
7. Notion token 读取必须 allowlist。
8. push-notion 必须要求 network=true。
9. dry-run 不得写入真实文件。
10. stdout 不得输出导入日志。
```

默认权限建议：

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

当启用 `--push-notion` 时，需要：

```yaml
network: true
```

---

## 21. Clone 插件安全要求

Clone Plugin 需要重点关注：

```text
1. tokens/layout/page/sections JSON 路径安全。
2. fidelity HTML 目录路径安全。
3. assets 下载 URL 风险。
4. theme 名称安全。
5. themes/content/data 写入安全。
6. docs/research verify report 写入安全。
7. network 权限必须显式授权。
8. remote assets 在 network=false 时必须失败。
9. --use 修改 site.yaml 必须原子写入。
10. visual diff artifact 路径必须是项目内相对路径。
```

默认权限建议：

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

## 22. 执行报告安全

### 22.1 每次插件调用必须记录

报告路径：

```text
.bukit/reports/plugin-executions/<plugin-id>-<operation>-<timestamp>.json
```

---

### 22.2 报告内容

至少包含：

```json
{
  "pluginId": "import",
  "pluginVersion": "1.0.0",
  "protocol": "bukit-plugin-v1",
  "platform": "osx-arm64",
  "operation": "invoke",
  "command": "import",
  "entry": "plugins/import/bin/osx-arm64/bukit-plugin-import",
  "startedAt": "2026-06-18T00:00:00Z",
  "durationMs": 1234,
  "processExitCode": 0,
  "responseExitCode": 0,
  "success": true,
  "sha256Verified": true,
  "stdoutBytes": 1024,
  "stderrBytes": 128,
  "permissions": {
    "fileSystem": {
      "read": ["."],
      "write": ["./themes"]
    },
    "network": false,
    "environment": {
      "read": ["NOTION_TOKEN"]
    }
  },
  "artifacts": [],
  "diagnostics": []
}
```

---

### 22.3 报告不得包含 Secret

禁止记录：

```text
NOTION_TOKEN 原文
API_KEY 原文
PASSWORD 原文
COOKIE 原文
Authorization header 原文
```

必须打码：

```text
***
```

---

## 23. 错误码

建议安全相关错误码：

| 错误码                                | 含义                     |
| ---------------------------------- | ---------------------- |
| `plugin.sourceInsideBukitDir`      | source 位于 `.bukit/` 内  |
| `plugin.sourceOutsidePlugins`      | source 不在 `plugins/` 下 |
| `plugin.sourcePathTraversal`       | source 包含路径穿越          |
| `plugin.sourceAbsolutePath`        | source 是绝对路径           |
| `plugin.manifestNotFound`          | plugin.yaml 缺失         |
| `plugin.manifestInvalid`           | plugin.yaml 无效         |
| `plugin.protocolUnsupported`       | 协议不支持                  |
| `plugin.kindUnsupported`           | kind 不支持               |
| `plugin.platformUnsupported`       | 当前平台不支持                |
| `plugin.entryNotFound`             | entry 不存在              |
| `plugin.entryPathTraversal`        | entry 包含路径穿越           |
| `plugin.entryAbsolutePath`         | entry 是绝对路径            |
| `plugin.entryInsideBukitDir`       | entry 位于 `.bukit/` 内   |
| `plugin.sha256Missing`             | sha256 缺失              |
| `plugin.sha256Mismatch`            | sha256 不匹配             |
| `plugin.permissionDenied`          | 权限不足                   |
| `plugin.environmentWildcardDenied` | 环境变量通配符被拒绝             |
| `plugin.commandConflict`           | 命令冲突                   |
| `plugin.commandDisabled`           | 插件命令被禁用                |
| `plugin.timeout`                   | 插件超时                   |
| `plugin.outputTooLarge`            | 输出过大                   |
| `plugin.invalidResponse`           | stdout 不是合法 JSON       |
| `plugin.executionFailed`           | 插件执行失败                 |

---

## 24. 测试要求

### 24.1 路径安全测试

必须覆盖：

```text
source: .bukit/plugins/import
source: ../plugins/import
source: /tmp/plugin
source: plugins/../evil
entry: ../../evil
entry: .bukit/bin/tool
entry: /usr/local/bin/tool
```

---

### 24.2 Manifest 安全测试

必须覆盖：

```text
plugin.yaml 缺失
protocol 不匹配
kind 非 process
platform 缺失
entry 缺失
sha256 缺失
sha256 mismatch
```

---

### 24.3 权限测试

必须覆盖：

```text
required read 超出 granted read
required write 超出 granted write
required network=true 但 granted network=false
required env 超出 allowlist
environment.read: ["*"]
```

---

### 24.4 进程安全测试

必须覆盖：

```text
handshake timeout
manifest timeout
invoke timeout
stdout invalid JSON
stdout mixed logs and JSON
stderr too large
stdout too large
non-zero exit code
process crash
```

---

### 24.5 命令安全测试

必须覆盖：

```text
插件命令覆盖 Core command
两个插件同名 command
alias 冲突
disabled command diagnostic
```

---

### 24.6 CI 安全测试

必须覆盖：

```text
allowInCi=false 时 CI 拒绝运行
allowInCi=true 且 sha256 通过时 CI 允许运行
runtime-dependent 插件默认拒绝
execution report 生成
secret 打码
```

---

## 25. Release Gate 要求

插件安全模型落地后，Release Gate 必须包含：

```text
dotnet test Bukit.PluginHost.Tests
插件路径安全测试
插件权限安全测试
插件协议安全测试
插件进程安全测试
插件命令冲突测试
插件 CI 策略测试
插件 execution report 测试
Core Native AOT publish 测试
跨平台 smoke 测试
```

---

## 26. 后果

### 26.1 正面影响

```text
1. Core 与插件实现彻底解耦。
2. 插件不会污染 Core 进程。
3. .bukit/ 安全边界清晰。
4. plugins/ 插件程序目录可审计。
5. 插件执行可追踪。
6. 插件权限风险可见。
7. CI 安全策略可控。
8. Native AOT 不受插件机制破坏。
9. 第三方语言插件可接入。
10. Import / Clone / Notion / Visual 可按统一安全模型迁移。
```

---

### 26.2 代价

```text
1. 需要实现 PluginHost。
2. 需要实现路径校验。
3. 需要实现权限校验。
4. 需要实现 sha256 校验。
5. 需要实现 execution report。
6. 需要维护插件 schema。
7. 插件开发者需要提供 plugin.yaml。
8. 插件构建需要生成跨平台包和 hash。
```

---

### 26.3 风险

```text
1. v1 权限模型不是完整 OS 沙箱。
2. network=false 不能在所有平台强制阻断网络。
3. fileSystem 权限无法完全阻止恶意二进制直接访问文件系统。
4. runtime-dependent 插件可能引入运行时供应链风险。
5. 用户手动修改 plugin.yaml 可能导致校验失败。
```

缓解策略：

```text
1. 文档明确 v1 权限边界。
2. 正式插件默认 self-contained。
3. CI 强制 sha256。
4. 执行报告审计。
5. 后续引入签名、沙箱、插件 registry。
```

---

## 27. 备选方案

### 27.1 动态 DLL 插件

拒绝。

原因：

```text
1. 破坏 Native AOT。
2. 插件运行在 Core 进程内。
3. 安全边界差。
4. 第三方插件可破坏 Core 内存空间。
5. 语言不再无关。
```

---

### 27.2 插件放 `.bukit/plugins/`

拒绝。

原因：

```text
1. .bukit/ 是隐藏系统目录。
2. 隐藏目录中存放可执行程序安全边界混乱。
3. 不利于审计。
4. 不利于版本管理。
5. 与系统配置/报告目录职责冲突。
```

---

### 27.3 允许任意本地路径插件

拒绝。

原因：

```text
1. 供应链风险高。
2. 路径穿越风险高。
3. 不利于项目可复制构建。
4. 不利于 CI 审计。
5. 不利于 lock 文件稳定。
```

---

### 27.4 第一版引入完整 OS 沙箱

暂缓。

原因：

```text
1. 跨平台复杂度高。
2. Windows / macOS / Linux 沙箱能力差异大。
3. 会拖慢 Core Plugin Host v1 落地。
4. 第一版先完成路径、hash、权限声明、进程边界和审计。
```

---

## 28. 实施计划

### Phase 1：安全文档与 Schema

新增：

```text
docs/adr/ADR-BUKIT-PLUGIN-SECURITY-MODEL.md
schemas/bukit-plugin-config.schema.json
schemas/bukit-plugin-manifest.schema.json
schemas/bukit-plugin-protocol.schema.json
```

---

### Phase 2：PluginHost 路径安全

实现：

```text
source path validator
entry path validator
.bukit executable rejection
plugins/ boundary enforcement
path traversal rejection
absolute path rejection
```

---

### Phase 3：Manifest 与 Hash 校验

实现：

```text
plugin.yaml loader
protocol check
kind check
platform resolver
sha256 check
lock file writing
```

---

### Phase 4：权限模型

实现：

```text
requiredPermissions parser
granted permissions parser
permission comparison
environment allowlist
network permission check
fileSystem permission check
```

---

### Phase 5：进程执行安全

实现：

```text
UseShellExecute=false
stdin JSON
stdout JSON
stderr logs
timeout
output size limit
exit code mapping
invalid JSON handling
```

---

### Phase 6：审计报告

实现：

```text
.bukit/reports/plugin-executions/*.json
secret masking
permissions snapshot
sha256Verified
stdoutBytes / stderrBytes
diagnostics
artifacts
```

---

### Phase 7：安全测试与 Release Gate

实现：

```text
PluginHost security tests
permission tests
path tests
process tests
CI tests
cross-platform smoke tests
```

---

## 29. 验收标准

本 ADR 落地后必须满足：

```text
1. Core 不加载插件 DLL。
2. Core 只从 plugins/<id>/ 加载插件。
3. Core 拒绝 .bukit/ 内任何 executable。
4. Core 拒绝 source 路径穿越。
5. Core 拒绝 entry 路径穿越。
6. Core 拒绝绝对路径 source。
7. Core 拒绝绝对路径 entry。
8. Core 校验 plugin.yaml。
9. Core 校验 protocol=bukit-plugin-v1。
10. Core 校验 kind=process。
11. Core 校验当前平台 entry。
12. Core 校验 sha256。
13. Core 校验 requiredPermissions <= grantedPermissions。
14. Core 只传递 allowlist 环境变量。
15. Core 对 secret 打码。
16. Core 不使用 shell 启动插件。
17. Core 限制 stdout/stderr/response 大小。
18. Core 对 handshake / manifest / invoke 设置 timeout。
19. Core 记录 plugin execution report。
20. Core 在 CI 中执行更严格策略。
21. disabled plugin command 行为正确。
22. command conflict 检测正确。
23. Windows / Linux / macOS 安全 smoke 测试通过。
```

---

## 30. 最终结论

Bukit 插件安全模型正式定义为：

```text
外部进程隔离
Manifest 校验
路径边界
权限声明
Hash 完整性
运行时防护
执行审计
CI 严格策略
```

核心安全边界：

```text
Core
  不加载插件代码
  只执行经过校验的外部进程

plugins/
  存放项目插件程序包

.bukit/
  存放配置、锁文件、报告、缓存、日志、状态
  禁止存放任何插件程序

plugin.yaml
  声明插件身份、协议、平台入口、hash、权限

.bukit/plugins.yaml
  声明项目启用、授权、timeout、输出限制、CI 策略

Core PluginHost
  负责校验、执行、限制、报告
```

该安全模型是 Bukit 后续 Import、Clone、Notion、Visual 等正式插件迁移和第三方插件生态建设的基础。
