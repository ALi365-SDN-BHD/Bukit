# Bukit 审计报告第二轮硬化 Spec

> 来源：`.trae/documents/bukit-audit-report-2026-05-30-chatgpt.md`
> 审计日期：2026-05-30

## Why

审计报告识别出 13 个尚未修复的问题：3 个 P0 安全/正确性问题（路径大小写绕过、符号链接泄露、外部插件 trust model）、4 个 P1 正确性/性能问题（derive 冲突覆盖、多语言串行构建、nullable 签名错误、指纹模式不可靠）、3 个 P2 工程卫生问题（--jobs 静默忽略、AutoSummary 全局状态、publishDotFiles 绕过敏感拒绝列表）、3 个架构层面建议（pipeline stage 化、插件双轨制文档、输出安全统一收敛）。本 spec 覆盖全部未修复项。

## 已修复项参考

以下审计项已在前期 spec 中修复，本 spec 不再重复：
- core-hardening-p0-p1：SafeOutputFileSystem 统一、dotfile deny list、static HTML 冲突、RenderDependencyHash、config strict parsing、URL 段校验
- fix-p1-security-and-stability：Shortcode XSS 编码、Notion BlockRenderer 颜色编码、SSRF 保护、异步阻塞消除
- fix-p2-4-to-p2-7：主题名消毒、路径边界校验、DoctorCommand 裸 catch 等
- cli-unify-migration：CLI 新旧解析路径统一
- body-cache-decorator / incremental-hash-coverage / fix-p3-collection-lru / collection-primary-model 等

## What Changes

### P0 — 安全底座
- **P0-1**：跨平台路径比较方法，修复 Linux/macOS 下 `OrdinalIgnoreCase` 大小写绕过
- **P0-2**：`DirectoryCopy` + `BuildManifestTracker` 默认检测并拒绝符号链接（symlink）
- **P0-3**：外部 process 插件 trust model 文档化 + CI 默认禁用开关 + entry 项目内限制

### P1 — 构建正确性 + 性能
- **P1-1**：`deriveConflictPolicy=last-wins` 真正移除旧路由，而非仅追加
- **P1-2**：多语言构建支持 `build.languageJobs` 配置，解除 `MaxDegreeOfParallelism=1` 强制串行
- **P1-3**：`TrackAssetOutputs` 签名修正，`assetsDir` 参数改为 nullable
- **P1-4**：manifest fingerprint 新增 `sha256` 模式，`fingerprintMode` 配置

### P2 — 工程卫生
- **P2-2**：`--jobs` 非法输入直接报错（exit code 2）
- **P2-3**：`BUKIT_AUTO_SUMMARY` 从环境变量迁移到 `BuildContext` 字段
- **P2-4**：`publishDotFiles=true` 时仍强制拒绝敏感 dotfile（.env/.git/*.pem 等）

### 架构建议
- **5.1**：`VariantBuildPipeline` 拆分为独立 stage 方法
- **5.2**：插件双轨制文档（built-in / process / future WASM / section）
- **5.3**：输出路径安全统一收敛为 `IOutputPathPolicy` + `SafePathResolver`

## Impact

- Affected specs: core-hardening-p0-p1（P0-1 扩展已有 SafeOutputFileSystem 路径比较；P2-4 扩展 dotfile deny list 语义）
- Affected code:
  - `src/Bukit.Engine/FileWriter.cs` — P0-1 路径比较
  - `src/Bukit.Engine/Output/SafeOutputFileSystem.cs` — P0-1 路径比较 + P5.3 输出路径策略
  - `src/Bukit.Engine/DirectoryCopy.cs` — P0-2 symlink 检测
  - `src/Bukit.Engine/Incremental/BuildManifestTracker.cs` — P0-2 symlink 检测 + P1-4 sha256 指纹
  - `src/Bukit.Engine/Plugin/PluginRunner.cs` — P1-1 last-wins 冲突覆盖
  - `src/Bukit.Engine/SiteEngine.cs` — P1-2 languageJobs + P2-3 AutoSummary
  - `src/Bukit.Engine/AssetPipeline.cs` — P1-3 nullable + P2-4 publishDotFiles
  - `src/Bukit.Cli/Commands/BuildCommand.cs` — P2-2 --jobs 报错 + P2-3 env var 移除
  - `src/Bukit.Cli/PluginSource/ExternalProtocolPluginSource.cs` — P0-3 entry 路径限制
  - `src/Bukit.Config/` — P1-2 languageJobs + P1-4 fingerprintMode 配置
  - `src/Bukit.Engine/Pipeline/VariantBuildPipeline.cs` — 5.1 stage 拆分
  - `guide/` — 5.2 插件双轨制文档

---

## ADDED Requirements

### Requirement: Cross-platform path comparison (P0-1)
系统 SHALL 在 `FileWriter.GetSafeFullPath`、`SafeOutputFileSystem.GetSafeFullPath`、`BuildPlanner.EnsureOutputDirectoryCanBeCleaned`、`DeleteEmptyDirectoriesUpToRoot` 等所有路径安全比较处使用跨平台感知的字符串比较方法。

#### Scenario: Windows 忽略大小写
- **GIVEN** 运行于 Windows
- **WHEN** `outputRoot = C:\out` 且 `relativePath = ..\OUT\evil.html`
- **THEN** `Path.GetFullPath` 得到 `C:\OUT\evil.html`
- **AND** `StartsWith("C:\out", OrdinalIgnoreCase)` → 正确通过（Windows 文件系统大小写不敏感）
- **AND** 路径未逃逸时操作正常完成

#### Scenario: Linux/macOS 大小写敏感
- **GIVEN** 运行于 Linux 或 macOS
- **WHEN** `outputRoot = /tmp/out` 且 `relativePath = ../OUT/evil.html`
- **THEN** `Path.GetFullPath` 得到 `/tmp/OUT/evil.html`
- **AND** `StartsWith("/tmp/out", Ordinal)` → 返回 false（大小写不同）
- **AND** 路径穿越被正确拒绝，抛出安全异常

#### Scenario: 正常路径无影响
- **GIVEN** 运行于任意平台
- **WHEN** `outputRoot = /tmp/out` 且 `relativePath = subdir/file.html`
- **THEN** 路径验证通过，操作正常完成

#### Scenario: 统一封装
- **WHEN** 跨平台路径比较
- **THEN** 使用统一的 `PlatformPathComparison` 属性（封装 `OperatingSystem.IsWindows()` 判断）
- **AND** `FileWriter` / `SafeOutputFileSystem` / `BuildPlanner` / `DeleteEmptyDirectoriesUpToRoot` 均使用同一比较方式

---

### Requirement: Symlink detection and rejection (P0-2)
系统 SHALL 在 `DirectoryCopy.Sync`、`DirectoryCopy.SyncFilesRecursive`、`BuildManifestTracker` 遍历文件时检测并跳过符号链接（`FileAttributes.ReparsePoint`），防止通过 symlink 泄露宿主机敏感文件到输出目录。

#### Scenario: 文件 symlink 被跳过
- **GIVEN** `static/leak.txt` 是指向 `/etc/passwd` 的符号链接
- **WHEN** 执行 `bukit build`
- **THEN** `leak.txt` 不被复制到输出目录
- **AND** 构建日志输出 warning 提示已跳过 symlink

#### Scenario: 目录 symlink 被跳过
- **GIVEN** `assets/private/` 是指向 `/home/user/.ssh/` 的符号链接
- **WHEN** 执行 `bukit build`
- **THEN** `private/` 目录内容不被递归复制
- **AND** 构建日志输出 warning

#### Scenario: 正常文件不受影响
- **GIVEN** `static/logo.png` 是普通文件（非 symlink）
- **WHEN** 执行 `bukit build`
- **THEN** `logo.png` 正常复制到输出目录

#### Scenario: 配置 followSymlinks（可选扩展）
- **GIVEN** 配置 `build.followSymlinks: true`（默认 false）
- **WHEN** 存在 symlink
- **THEN** symlink 目标内容被复制（行为恢复到当前实现）
- **AND** 日志 info 提示已跟随 symlink

#### Scenario: BuildManifestTracker symlink 检测
- **GIVEN** `DirectoryCopy` 或媒体目录中存在 symlink
- **WHEN** `BuildManifestTracker` 扫描要复制的文件
- **THEN** symlink 同样被跳过，不加入 manifest

---

### Requirement: External process plugin safety (P0-3)
系统 SHALL 明确文档化外部 process 插件的 trust model，并在 CI 环境默认禁用，同时限制插件 entry 路径必须在项目目录内。

#### Scenario: CI 环境默认禁用
- **GIVEN** 环境变量 `CI=true` 或 `BUKIT_CI=true`
- **AND** site.yaml 中配置了 external plugin
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败，报错 "external plugins are disabled in CI"
- **AND** exit code 非 0

#### Scenario: CI 中显式启用
- **GIVEN** `CI=true` 且 `--allow-external-plugins` 传参
- **WHEN** 执行 `bukit build --allow-external-plugins`
- **THEN** 外部插件正常执行

#### Scenario: 本地开发正常
- **GIVEN** 非 CI 环境
- **WHEN** 执行 `bukit build`（有外部插件配置）
- **THEN** 外部插件正常执行

#### Scenario: 插件 entry 不在项目目录内被拒绝
- **GIVEN** `site.externalPlugins.my-plugin.entry: /usr/bin/some-tool`
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败，报错 "plugin entry must be within project directory"
- **AND** 除非配置 `allowAbsoluteEntry: true`

#### Scenario: 插件 entry 在项目目录内正常
- **GIVEN** `site.externalPlugins.my-plugin.entry: plugins/my-plugin/run.sh`
- **WHEN** 执行 `bukit build`
- **THEN** 插件正常执行

#### Scenario: 文档明确 trust model
- **WHEN** 查阅外部插件用户指南
- **THEN** 文档明确声明：外部 process 插件等同本地命令执行，无 sandbox 隔离
- **AND** 文档说明 CI 禁用策略和启用方法

---

### Requirement: DeriveConflictPolicy last-wins real override (P1-1)
系统 SHALL 在 `deriveConflictPolicy=last-wins` 时从已接受列表和路由索引中移除旧冲突条目，确保只有最后一个派生页面进入渲染队列和 manifest。

#### Scenario: derived 页面覆盖 derived 页面
- **GIVEN** 插件 A 生成 derived 页面 `/hello/` → `hello/index.html`
- **AND** 插件 B 也生成 derived 页面 `/hello/` → `hello/index.html`
- **AND** `deriveConflictPolicy: last-wins`
- **WHEN** 执行 `bukit build`
- **THEN** 插件 B 的页面替换插件 A 的页面
- **AND** rendered 列表中只有一个 `/hello/`
- **AND** manifest 中只有一个 `/hello/`
- **AND** sitemap/search index 中不重复

#### Scenario: derived 页面不能覆盖原始 content 路由
- **GIVEN** content 文件生成路由 `/about/` → `about/index.html`
- **AND** 插件生成 derived 页面 `/about/` → `about/index.html`
- **AND** `deriveConflictPolicy: last-wins`
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败或输出 warning（content 路由不可被 derived 覆盖）
- **AND** 原始 content 页面保留

#### Scenario: 同 url 不同 outputPath 亦能正确去重
- **GIVEN** 插件 A 生成 `/hello/` → `a/hello/index.html`
- **AND** 插件 B 生成 `/hello/` → `b/hello/index.html`
- **AND** `deriveConflictPolicy: last-wins`
- **WHEN** 执行 `bukit build`
- **THEN** 仅插件 B 的页面保留

#### Scenario: 不同 url 同 outputPath 冲突
- **GIVEN** 插件 A 生成 `/a/` → `shared/index.html`
- **AND** 插件 B 生成 `/b/` → `shared/index.html`
- **AND** `deriveConflictPolicy: last-wins`
- **WHEN** 执行 `bukit build`
- **THEN** 仅插件 B 的页面保留

#### Scenario: error 策略保持现有行为
- **GIVEN** `deriveConflictPolicy: error`（默认）
- **AND** 存在冲突
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败，抛出路由冲突异常

---

### Requirement: Multi-language parallel build (P1-2)
系统 SHALL 支持 `build.languageJobs` 配置项（默认 1），允许用户启用多语言并行构建。

#### Scenario: 默认串行构建
- **GIVEN** 未配置 `build.languageJobs` 或设为 1
- **WHEN** 多语言站点执行 `bukit build`
- **THEN** 各语言串行构建（当前行为不变）

#### Scenario: 并行构建
- **GIVEN** 配置 `build.languageJobs: 4`
- **WHEN** 多语言站点（如 zh/en/ms/ja）执行 `bukit build`
- **THEN** 最多 4 个语言并行构建
- **AND** 构建输出中每个语言的 manifest 独立无误

#### Scenario: languageJobs 不超 CPU 数
- **GIVEN** 配置 `build.languageJobs: 100`
- **WHEN** 执行 `bukit build`
- **THEN** 实际并发数限制为 `Environment.ProcessorCount`

#### Scenario: 构建结果与串行一致
- **GIVEN** 相同多语言站点
- **WHEN** 分别以 `languageJobs: 1` 和 `languageJobs: 4` 构建
- **THEN** 两次构建输出内容一致（文件数量、文件内容）

---

### Requirement: TrackAssetOutputs nullable signature (P1-3)
系统 SHALL 修正 `BuildManifestTracker.TrackAssetOutputs` 的 `assetsDir` 参数签名为 `string?`，移除调用方的 `!` null-forgiving 操作符。

#### Scenario: 仅 parent assets 存在
- **GIVEN** 主题继承场景中仅父主题有 assets 目录
- **AND** 子主题无 assets 目录
- **WHEN** 执行 `bukit build`
- **THEN** `TrackAssetOutputs(parentDir, null)` 正常执行
- **AND** 不抛出 NullReferenceException

#### Scenario: 仅 child assets 存在
- **GIVEN** 子主题有 assets 目录
- **AND** 父主题无 assets 目录
- **WHEN** 执行 `bukit build`
- **THEN** `TrackAssetOutputs(null, childDir)` 正常执行

#### Scenario: 两者都存在
- **GIVEN** 父子主题均有 assets 目录
- **WHEN** 执行 `bukit build`
- **THEN** `TrackAssetOutputs(parentDir, childDir)` 正常执行，child 覆盖 parent

---

### Requirement: Manifest fingerprint sha256 mode (P1-4)
系统 SHALL 支持 `build.fingerprintMode: sha256` 配置，在 CI/网络文件系统等场景提供可靠的变更检测。

#### Scenario: sha256 模式检测内容变更
- **GIVEN** 配置 `build.fingerprintMode: sha256`
- **AND** 文件大小相同但内容不同的两个版本
- **WHEN** 执行增量构建
- **THEN** 内容变更被正确检测
- **AND** 页面被重新渲染

#### Scenario: 默认 size-time 模式
- **GIVEN** 未配置 `fingerprintMode` 或设为 `size-time`
- **WHEN** 执行增量构建
- **THEN** 使用 `$"{Length}:{LastWriteTimeUtc.Ticks}"` 指纹（当前行为）

#### Scenario: 非法值报错
- **GIVEN** 配置 `build.fingerprintMode: md5`
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败，报错 "fingerprintMode must be 'size-time' or 'sha256'"

#### Scenario: manifest HTML/static/asset/media 统一支持
- **WHEN** 配置 `fingerprintMode: sha256`
- **THEN** HTML 页面的 content hash、static 文件指纹、asset 指纹、media 指纹均使用 sha256

---

### Requirement: --jobs illegal input error (P2-2)
系统 SHALL 对 `--jobs` 非法输入（非正整数）直接报错，exit code 2。

#### Scenario: --jobs 非数字报错
- **WHEN** 执行 `bukit build --jobs abc`
- **THEN** exit code 2
- **AND** stderr 输出 "--jobs must be a positive integer"

#### Scenario: --jobs 负数报错
- **WHEN** 执行 `bukit build --jobs -1`
- **THEN** exit code 2
- **AND** stderr 输出 "--jobs must be a positive integer"

#### Scenario: --jobs 0 报错
- **WHEN** 执行 `bukit build --jobs 0`
- **THEN** exit code 2
- **AND** stderr 输出 "--jobs must be a positive integer"

#### Scenario: --jobs 正整数正常
- **WHEN** 执行 `bukit build --jobs 4`
- **THEN** 使用 4 并发正常构建

---

### Requirement: AutoSummary via BuildContext (P2-3)
系统 SHALL 将 AutoSummary 配置通过 `BuildContext` 对象传递，不再使用全局环境变量 `BUKIT_AUTO_SUMMARY` / `BUKIT_AUTO_SUMMARY_MAXLEN`。

#### Scenario: BuildContext 包含 AutoSummary
- **WHEN** `BuildCommand` 解析 `--auto-summary` 参数
- **THEN** 设置 `BuildContext.AutoSummary` 和 `BuildContext.AutoSummaryMaxLen` 字段
- **AND** 不调用 `Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY", ...)`

#### Scenario: RenderContext 消费 AutoSummary
- **WHEN** 渲染阶段需要判断是否生成自动摘要
- **THEN** 从 `RenderContext.AutoSummary` 读取
- **AND** 不从 `Environment.GetEnvironmentVariable("BUKIT_AUTO_SUMMARY")` 读取

#### Scenario: 单元测试隔离
- **GIVEN** 两个独立单元测试
- **WHEN** 测试 A 设置 AutoSummary 且测试 B 未设置
- **THEN** 两者互不影响（无全局状态污染）

---

### Requirement: publishDotFiles with mandatory sensitive deny (P2-4)
系统 SHALL 即使 `publishDotFiles: true`，仍强制拒绝发布敏感 dotfile（.env/*.pem/.git 等），仅允许 `.well-known` 等非敏感 dotfile。

#### Scenario: publishDotFiles=true 仍拒绝 .env
- **GIVEN** 配置 `build.publishDotFiles: true`
- **AND** `static/.env` 存在
- **WHEN** 执行 `bukit build`
- **THEN** `.env` 不出现在输出目录
- **AND** 日志 warning 提示已跳过

#### Scenario: publishDotFiles=true 仍拒绝 *.pem
- **GIVEN** 配置 `build.publishDotFiles: true`
- **AND** `static/config/secrets.pem` 存在
- **WHEN** 执行 `bukit build`
- **THEN** `secrets.pem` 不出现在输出目录

#### Scenario: publishDotFiles=true 允许 .well-known
- **GIVEN** 配置 `build.publishDotFiles: true`
- **AND** `static/.well-known/security.txt` 存在
- **WHEN** 执行 `bukit build`
- **THEN** `.well-known/security.txt` 正常输出

#### Scenario: publishDotFiles=true 允许其他非敏感 dotfile
- **GIVEN** 配置 `build.publishDotFiles: true`
- **AND** `static/.htaccess` 存在
- **WHEN** 执行 `bukit build`
- **THEN** `.htaccess` 正常输出（不在强制拒绝列表中）

#### Scenario: 强制拒绝列表
- **WHEN** `publishDotFiles: true`
- **THEN** 以下文件/模式仍被强制拒绝：`.env`、`.env.*`、`.git`、`.github`、`*.pem`、`*.key`、`*.pfx`、`*.p12`、`.npmrc`

---

### Requirement: VariantBuildPipeline stage decomposition (5.1)
系统 SHALL 将 `VariantBuildPipeline.ExecuteAsync` 拆分为独立的 stage 方法，每个 stage 方法职责单一且可单测。

#### Scenario: ExecuteAsync 调用 stage 方法
- **WHEN** `VariantBuildPipeline.ExecuteAsync` 执行
- **THEN** 依次调用 `BootstrapThemeStage` / `BuildDataModuleStage` / `GenerateRoutesStage` / `RunPluginDeriveStage` / `BuildSeoStage` / `RenderPagesStage` / `SyncAssetsStage` / `RunPluginAfterBuildStage` / `GenerateReportStage`
- **AND** 各 stage 间通过构建上下文传递数据

#### Scenario: Stage 方法可独立单测
- **WHEN** 编写 `BootstrapThemeStage` 的单元测试
- **THEN** 可独立提供 mock 上下文进行测试
- **AND** 不依赖完整构建流程

#### Scenario: 构建结果与拆分前一致
- **GIVEN** 相同输入
- **WHEN** 分别执行拆分前和拆分后的构建
- **THEN** 输出内容完全一致

---

### Requirement: Plugin dual-track documentation (5.2)
系统 SHALL 在插件用户指南中明确插件双轨制分类（built-in / process / future WASM / section），并说明各轨道的 trust level 和适用场景。

#### Scenario: 文档包含分类表
- **WHEN** 查阅插件用户指南
- **THEN** 文档包含以下分类：
  - Built-in Plugin — 引擎内部能力 — 高安全级别
  - Process Plugin — 本地可信扩展 — 低安全级别，无 sandbox
  - Future WASM Plugin — 可分发社区插件 — 中高安全级别
  - Section Plugin — 主题组件级能力 — 中安全级别

#### Scenario: 文档说明 process 插件风险
- **WHEN** 查阅 process 插件章节
- **THEN** 文档明确声明：process 插件拥有宿主机进程权限，可读取任意文件、访问网络、执行子进程
- **AND** 文档说明 CI 默认禁用和 `--allow-external-plugins` 启用方式

---

### Requirement: Unified output path policy (5.3)
系统 SHALL 定义 `IOutputPathPolicy` 接口和 `SafePathResolver` 实现，统一所有输出写入/复制/删除操作的路径安全校验入口。

#### Scenario: IOutputPathPolicy 接口定义
- **WHEN** 定义 `IOutputPathPolicy`
- **THEN** 接口包含 `string ResolveSafePath(string outputRoot, string relativePath)` 方法
- **AND** 实现抛出 `OutputPathSecurityException` 当路径逃逸时

#### Scenario: FileWriter 使用 IOutputPathPolicy
- **WHEN** `FileWriter.WriteAsync` 写入文件
- **THEN** 目标路径通过 `IOutputPathPolicy.ResolveSafePath` 校验

#### Scenario: DirectoryCopy 使用 IOutputPathPolicy
- **WHEN** `DirectoryCopy.Sync` 复制文件
- **THEN** 每个目标路径通过 `IOutputPathPolicy.ResolveSafePath` 校验

#### Scenario: BuildManifestTracker 使用 IOutputPathPolicy
- **WHEN** `BuildManifestTracker.DeleteStaleManifestOutputs` 删除文件
- **THEN** 待删除路径通过 `IOutputPathPolicy.ResolveSafePath` 校验

#### Scenario: RouteSecurityValidator 保持独立
- **WHEN** `RouteSecurityValidator.ValidateOutputPath` 校验 URL
- **THEN** 继续独立工作（URL 级校验与文件系统级校验不同层）
- **AND** 仅输出文件写入共享 `IOutputPathPolicy`
