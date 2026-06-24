# Codex 插件机制执行理解摘要

> 生成时间：2026-06-24
> 执行阶段：Phase 0
> 阅读范围：`docs/plugins/` 下全部插件化设计文档

## 1. Core / Plugin / Labs 边界理解

Core 是 Bukit 的稳定底座和插件宿主。Core 负责 CLI 宿主、构建引擎、配置系统、内容、渲染、路由、主题、诊断、Core 内置插件、插件配置解析、插件协议、插件安全校验、插件执行、lock 与报告写入。Core 不应堆积 Import、Clone 等成熟业务功能实现。

Core 内置插件可以继续采用 in-process 模式，并继续由 `site.plugins` 控制。这类插件只覆盖与构建引擎强绑定的基础能力，例如 taxonomy、archive、pagination、pages-index、sitemap、rss、search index 等。

Plugin 是正式发布功能模块。除 Core 内置插件外，正式插件必须是跨平台外部进程插件，必须实现 `bukit-plugin-v1` JSON 协议，提供 `plugin.yaml`，声明平台入口、sha256、命令、权限，并通过 Core PluginHost 加载和调用。Core 不引用插件实现，插件也不反向依赖 Core CLI 或 PluginHost。

Labs 是未成熟功能孵化区。Labs 允许快速变化、破坏性调整、临时实现和不完整文档测试，但不得作为稳定功能发布，不得默认进入 Core CLI，不得被 Core 直接依赖，也不得被正式 Plugin 依赖。成熟功能只能走 `Labs -> Plugin Candidate -> Official Plugin -> Core Release Integration`。

## 2. `.bukit/` 与 `plugins/` 目录边界

`.bukit/` 是 Bukit 系统工作目录，只允许存放配置、锁文件、报告、缓存、日志、临时文件和状态文件。允许示例包括 `.bukit/plugins.yaml`、`.bukit/plugins.lock.yaml`、`.bukit/reports/`、`.bukit/cache/`、`.bukit/logs/`、`.bukit/tmp/`、`.bukit/state/`。

`.bukit/` 内禁止存放任何插件程序或 Core 会执行的文件，包括 `.exe`、`.dll`、`.sh`、`.cmd`、`.bat`、`.ps1`、`.bukit/plugins/`、`.bukit/bin/`、`.bukit/tools/` 和 `.bukit/plugin-executables/`。Core PluginHost 必须拒绝任何解析到 `.bukit/` 内的插件 source 或 entry。

用户项目中的插件程序包必须位于项目根目录 `plugins/<plugin-id>/`。第一版只允许本地项目 `plugins/<id>/`，不允许绝对路径、`../`、home 目录、`node_modules/.bin`、`/tmp`、`.bukit/plugins` 或任意项目外路径。

仓库内官方正式插件源码应位于顶层 `plugins/Bukit.Plugin.<Name>/`。这和用户项目运行包 `plugins/<id>/` 是不同层面的目录：前者是源码项目，后者是站点项目中的分发包。

## 3. `bukit-plugin-v1` 协议核心要求

`bukit-plugin-v1` 是语言无关、跨平台、外部进程 JSON 协议。第一版只支持项目本地插件包，并至少包含三个操作：

- `handshake`：确认协议版本、插件身份、插件版本、平台和基础能力。
- `manifest`：返回命令、子命令、参数、选项、capabilities 和 `requiredPermissions`。
- `invoke`：执行插件命令，接收 command path、arguments、options、context 和 permissions，返回 exitCode、messages、diagnostics、artifacts。

通信方式是 Core 通过 stdin 写入 UTF-8 JSON request，插件通过 stdout 输出单个完整 JSON response，stderr 只用于日志。stdout 不得包含普通日志、调试文本、进度条、ANSI 控制符、多段 JSON 或非 JSON 内容。

Core 启动插件进程时不得使用 shell 拼接命令，不得使用 `sh -c`、`cmd /c`、`powershell -Command` 或 `bash script.sh`。必须直接启动解析后的插件 executable，使用 `UseShellExecute = false`，并重定向 stdin、stdout、stderr。

协议路径统一使用 `/`，插件返回的 artifacts 必须是项目内相对路径，禁止绝对路径和路径穿越。

## 4. `.bukit/plugins.yaml` 与 `plugins/<id>/plugin.yaml` 职责区别

`.bukit/plugins.yaml` 是项目级插件启用配置。它只声明插件是否启用、source、暴露命令、项目授予权限、timeout、输出限制、CI 策略和失败策略。它不得直接声明 executable entry。

`plugins/<id>/plugin.yaml` 是插件包 manifest。它声明插件 ID、名称、版本、协议、kind、distribution、平台入口、sha256、命令清单和 requiredPermissions。

`.bukit/plugins.lock.yaml` 是 Core 生成的解析结果锁文件，可以记录 source、manifestVersion、protocol、platform、entry、sha256、commands、resolvedAt 和 sha256Verified，但不能绕过 manifest 校验，也不能存放插件程序。每次执行前仍必须校验 source、plugin.yaml、entry、sha256、protocol 和 permissions。

## 5. Core PluginHost 职责

Core PluginHost 第一版职责包括：

- 读取 `.bukit/plugins.yaml`，缺失时安全降级为空插件集。
- 校验插件配置 schema、plugin id、enabled、source、permissions、timeout、output limit、allowInCi。
- 只允许 `plugins/<id>/` source，拒绝 `.bukit`、绝对路径、路径穿越和项目外路径。
- 读取 `plugins/<id>/plugin.yaml`，校验 protocol、kind、distribution、platforms、entry、sha256、commands、requiredPermissions。
- 解析当前平台 ID，解析平台 entry，并再次基于 full path 校验边界。
- 校验 sha256，CI 与正式发布缺失 sha256 应拒绝。
- 执行 handshake、manifest、invoke，并验证 response envelope、requestId、protocol、plugin id、plugin version、platform 和 JSON 格式。
- 校验 `requiredPermissions <= grantedPermissions`，控制 allowlist 环境变量。
- 将 manifest commands 转为 CLI command descriptor，处理 Core command 优先、插件命令冲突、alias 冲突和 disabled command diagnostic。
- 限制 timeout、stdout、stderr、response JSON 大小，处理非零 exit code、invalid JSON、timeout、output too large。
- 写入 `.bukit/plugins.lock.yaml` 和 `.bukit/reports/plugin-executions/*.json`，并对 secret 打码。

## 6. 外部进程插件安全要求

安全模型是外部进程隔离、manifest 校验、路径边界、权限声明、hash 完整性、运行时防护、执行审计和 CI 严格策略的组合。v1 权限模型是声明式和宿主边界校验，不是完整 OS 级沙箱。

必须拒绝的情况包括：source 在 `.bukit/`、source 不在 `plugins/`、source 是绝对路径、source 路径穿越、entry 是绝对路径、entry 路径穿越、entry 在 `.bukit/`、entry 不存在、protocol 不匹配、kind 不是 `process`、当前平台缺失、sha256 缺失或不匹配、权限越权、环境变量通配符、命令冲突、stdout 非 JSON、超时和输出过大。

CI 下外部插件默认更严格。插件要在 CI 中运行，必须 `allowInCi = true`，source 在 `plugins/<id>`，entry 不在 `.bukit`，sha256 已声明并校验通过，permissions 明确，protocol 匹配，manifest 校验通过。

## 7. Import 插件迁移前置条件

Import 是第一个正式插件候选，但必须在 Core PluginHost 基础设施完成后再迁移。前置条件包括：PluginHost 已可读取 `.bukit/plugins.yaml` 和 `plugins/<id>/plugin.yaml`，已支持路径校验、sha256、handshake、manifest、invoke、权限校验、命令注册、disabled command、lock 和 execution report。

Import 插件源码目标是 `plugins/Bukit.Plugin.Import/`，运行包目标是用户项目 `plugins/import/`。Import 插件可引用 `Bukit.Plugin.Abstractions`、`Bukit.Shared`、`Bukit.Importing`，但不得引用 `Bukit.Cli`、`Bukit.Engine`、`Bukit.Labs.Cli`、`Bukit.Labs.Import` 或 `Bukit.PluginHost`。

当前执行计划中 Phase 10 只准备 Import 插件骨架，不直接迁移 `html-demo` 或 `seed` 业务逻辑，不一次性迁移 Import。

## 8. Clone 迁移前置条件

Clone 是第二个正式插件候选，复杂度高于 Import，必须后置。Clone 需要先抽离稳定领域库 `src/Bukit.Clone/`，再创建 `plugins/Bukit.Plugin.Clone/` 外部进程插件。

Clone 插件可引用 `Bukit.Plugin.Abstractions`、`Bukit.Shared`、`Bukit.Clone`，但不得引用 `Bukit.Cli`、`Bukit.Engine`、`Bukit.Labs.Cli`、`Bukit.Labs.Clone` 或 `Bukit.PluginHost`。Clone 默认需要显式网络权限，且 network=false 时必须拒绝 remote assets。

当前执行计划中 Phase 11 只准备 Clone 领域库骨架，不迁移 Clone 标准模式、fidelity、verify、visual report 或 `--use` 行为。

## 9. 当前阶段明确不做的事项

当前阶段不做插件市场、远程插件安装、自动下载、全局插件目录、用户 home 插件缓存、动态 DLL 插件、WASM 插件、Docker 插件、热加载、自动更新、复杂依赖解析、插件签名服务、完整 OS 级沙箱、Import 业务迁移、Clone 业务迁移、Labs 整体迁移、BukitJalil UI 接入、浏览器截图采集、Visual Plugin。

当前阶段也不恢复 `site.externalPlugins`，不恢复 runtime DLL 插件，不让 Core 引用插件实现，不让 Plugin 依赖 Labs，不把插件程序放入 `.bukit/`，不使用 shell 启动插件。

## 10. 执行风险与防误操作清单

- 防止把用户项目插件程序放进 `.bukit/`；任何 executable 都只能来自 `plugins/<id>/`。
- 防止将 `.bukit/plugins.yaml` 设计成 executable entry 配置；entry 只能来自 `plugins/<id>/plugin.yaml`。
- 防止把旧 `site.externalPlugins` 或动态 DLL 机制重新接回 Core。
- 防止 `Bukit.Cli`、`Bukit.Engine`、`Bukit.PluginHost` 直接引用 `Bukit.Plugin.Import` 或 `Bukit.Plugin.Clone`。
- 防止正式插件引用 `Bukit.Labs.*`。
- 防止先迁移 Import 或 Clone 业务逻辑而跳过 Echo 插件闭环。
- 防止一次性迁移 Import 和 Clone。
- 防止通过 shell 字符串拼接执行插件。
- 防止 stdout 日志污染 JSON 协议。
- 防止 lock 文件成为执行信任来源；lock 只能辅助审计。
- 防止 v1 权限被误写成完整沙箱承诺。
- 防止修改 `guide-0.1/` 和 `scripts-0.1/` 等备份目录。
- 防止破坏现有 Core CLI 命令面；插件命令必须与 Core stable commands 分层合并，Core command 优先。

## 11. 执行顺序确认

后续执行必须按以下顺序串行推进：

1. Phase 1：审计当前代码结构，生成 `docs/plugins/Codex 当前代码结构审计报告.md`。
2. Phase 2：准备目录结构。
3. Phase 3：新增 `Bukit.Plugin.Abstractions`。
4. Phase 4：新增 `Bukit.PluginHost` 配置与路径校验。
5. Phase 5：实现安全外部进程调用器。
6. Phase 6：实现 `bukit-plugin-v1` 协议客户端。
7. Phase 7：新增 Echo 测试插件。
8. Phase 8：Core CLI 接入插件命令。
9. Phase 9：补齐 lock、报告、安全门禁。
10. Phase 10：准备 Import 插件骨架。
11. Phase 11：准备 Clone 领域库骨架。

每个开发阶段完成后必须运行任务适配的 build/test，并审计最终 diff 与影响面。规则定义和报告生成阶段不要求仓库 gate，但仍需要自审。
