# Audit Hardening Round 2 Tasks

## Task 1: Cross-platform path comparison (P0-1)
- [x] 1.1 在 `src/Bukit.Shared/` 新增 `PlatformPathHelper` 静态类，包含 `PathComparison` 属性（`OperatingSystem.IsWindows() ? OrdinalIgnoreCase : Ordinal`）
- [x] 1.2 修改 `FileWriter.GetSafeFullPath`：`StartsWith` 使用 `PlatformPathHelper.PathComparison`
- [x] 1.3 修改 `SafeOutputFileSystem.GetSafeFullPath`：同上
- [x] 1.4 搜索并修改所有 `StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase)` 使用统一属性（`BuildPlanner`、`DeleteEmptyDirectoriesUpToRoot` 等）
- [x] 1.5 新增 `PlatformPathHelperTests`：测试 Windows/Linux 行为（Windows 用 `OrdinalIgnoreCase`，Linux/macOS 用 `Ordinal`）
- [x] 1.6 新增 `SafeOutputFileSystemTests`：Linux/macOS 下大写路径穿越被拒绝

## Task 2: Symlink detection and rejection (P0-2)
- [x] 2.1 在 `DirectoryCopy` 新增 `IsSymlink(string path)` 私有方法（检测 `FileAttributes.ReparsePoint`）
- [x] 2.2 修改 `DirectoryCopy.Sync`：遍历文件时 `IsSymlink` → 跳过 + warning 日志
- [x] 2.3 修改 `DirectoryCopy.SyncFilesRecursive`：目录和文件 symlink 均跳过
- [x] 2.4 修改 `BuildManifestTracker` 文件遍历：symlink 跳过
- [x] 2.5 新增 `build.followSymlinks` 配置项（默认 false）
- [x] 2.6 `followSymlinks: true` 时跳过 symlink 检测（恢复当前行为）
- [x] 2.7 新增 `DirectoryCopySymlinkTests`：文件/目录 symlink 被跳过、普通文件正常复制

## Task 3: publishDotFiles with mandatory sensitive deny (P2-4)
- [x] 3.1 在 `DirectoryCopyOptions` 或 `ShouldSkipDotfile` 中拆分语义：`IgnoreDotPrefixedFiles` 与 `AlwaysDenySensitiveDotfiles` 独立判断
- [x] 3.2 `AlwaysDenySensitiveDotfiles` 默认为 `true`，不受 `publishDotFiles` 影响
- [x] 3.3 强制拒绝列表：`.env`、`.env.*`、`.git`、`.github`、`*.pem`、`*.key`、`*.pfx`、`*.p12`、`.npmrc`
- [x] 3.4 修改 `DirectoryCopy.ShouldSkipDotfile`：即使 `IgnoreDotPrefixedFiles=false`，若文件匹配强制拒绝模式仍跳过
- [x] 3.5 修改 `AssetPipeline.BuildCopyOptions`：`publishDotFiles=true` 时正确传递强制拒绝逻辑
- [x] 3.6 新增/更新 dotfile 测试：验证 `publishDotFiles=true` 仍拒绝 .env，但允许 .htaccess / .well-known

## Task 4: External process plugin safety (P0-3)
- [x] 4.1 新增 `--allow-external-plugins` CLI flag（`BuildCommand` + `BukitCliSpecs`）
- [x] 4.2 CI 环境检测：`CI=true` 或 `BUKIT_CI=true` 时，若无 `--allow-external-plugins` 则拒绝外部插件执行
- [x] 4.3 在 `ExternalProtocolPluginSource` 或插件加载阶段校验 entry 路径：entry 必须是相对于项目根目录的相对路径
- [x] 4.4 新增 `allowAbsoluteEntry` 字段于 external plugin 配置 schema（默认 false）
- [x] 4.5 更新外部插件用户指南文档（`guide/user/20-external-plugins.md`）：添加 trust model 声明、CI 禁用说明、entry 路径限制说明
- [x] 4.6 新增 CLI 集成测试：CI 环境默认拒绝、`--allow-external-plugins` 启用、entry 路径校验

## Task 5: DeriveConflictPolicy last-wins real override (P1-1)
- [x] 5.1 修改 `PluginRunner.ApplyDeriveConflictPolicy`：`last-wins` 策略下从 `acceptedPages` 和路由索引中移除旧冲突条目
- [x] 5.2 实现冲突来源判断：content 路由优先级高于 derived 路由，derived 不可覆盖 content
- [x] 5.3 url 和 outputPath 双索引同步更新（`usedRouteUrls` 和 `usedOutputPaths`）
- [x] 5.4 新增 `DeriveConflictPolicyTests`：
  - derived vs derived 覆盖
  - derived vs content 不允许覆盖
  - same url different outputPath 去重
  - different url same outputPath 冲突
  - `error` 策略回归

## Task 6: TrackAssetOutputs nullable signature (P1-3)
- [x] 6.1 修改 `BuildManifestTracker.TrackAssetOutputs` 签名：`string assetsDir` → `string? assetsDir`
- [x] 6.2 修改 `AssetPipeline.TrackAssetOutputs` 调用处：移除 `!` null-forgiving 操作符
- [x] 6.3 新增/更新测试：仅 parent assets、仅 child assets、两者都 null、两者都存在

## Task 7: --jobs illegal input error (P2-2)
- [x] 7.1 修改 `BuildCommand.TryParsePositiveInt` 或 `BuildCommand.ValidateJobs`：非法值抛出 `CommandArgumentException`
- [x] 7.2 确保非数字/负数/零 → exit code 2 + "--jobs must be a positive integer"
- [x] 7.3 更新 CLI 测试：非法 --jobs 输入验证

## Task 8: AutoSummary via BuildContext (P2-3)
- [x] 8.1 在 `BuildContext` 新增 `AutoSummary` (bool) 和 `AutoSummaryMaxLen` (int?) 属性
- [x] 8.2 修改 `BuildCommand`：设置 `BuildContext.AutoSummary` 而非 `Environment.SetEnvironmentVariable`
- [x] 8.3 修改消费方（`RenderContext` 或 `ContentPipeline`）：从 `BuildContext` 读取而非 `Environment.GetEnvironmentVariable`
- [x] 8.4 移除 `BUKIT_AUTO_SUMMARY` / `BUKIT_AUTO_SUMMARY_MAXLEN` 环境变量设置代码
- [x] 8.5 新增测试：验证 AutoSummary 不污染全局环境

## Task 9: Multi-language parallel build (P1-2)
- [x] 9.1 新增 `build.languageJobs` 配置项（默认 1，上限 `Environment.ProcessorCount`）
- [x] 9.2 修改 `SiteEngine.BuildMultiLanguageAsync`：使用 `languageJobs` 而非固定 `MaxDegreeOfParallelism=1`
- [x] 9.3 验证线程安全：`DirectoryHashCache`、`IContentBodyStore`、`ThemeBootstrapper`、`BuildManifest`
- [x] 9.4 新增集成测试：`languageJobs: 4` 多语言并构建结果一致性验证

## Task 10: Manifest fingerprint sha256 mode (P1-4)
- [x] 10.1 新增 `build.fingerprintMode` 配置项（`size-time` | `sha256`，默认 `size-time`）
- [x] 10.2 修改 `BuildManifestTracker.ComputeFileFingerprint`：`sha256` 模式使用 `SHA256.HashData`
- [x] 10.3 HTML content hash / static 文件 / asset / media 统一使用 `fingerprintMode`
- [x] 10.4 `fingerprintMode` 非法值报错（`ConfigException`）
- [x] 10.5 新增 `FingerprintModeTests`：sha256 检测内容变更、size-time 默认行为、非法值报错

## Task 11: Unified output path policy (5.3)
- [x] 11.1 新增 `IOutputPathPolicy` 接口（`src/Bukit.Engine/Output/`）：`string ResolveSafePath(string outputRoot, string relativePath)`
- [x] 11.2 新增 `SafePathResolver` 实现（使用 `PlatformPathHelper` + 路径遍历检测）
- [x] 11.3 新增 `OutputPathSecurityException` 异常类
- [x] 11.4 修改 `FileWriter`：注入 `IOutputPathPolicy`，所有写入前调用
- [x] 11.5 修改 `DirectoryCopy`：注入或传递 `outputRoot` 给 `IOutputPathPolicy` 校验
- [x] 11.6 修改 `BuildManifestTracker`：删除操作前校验
- [x] 11.7 `RouteSecurityValidator` 保持独立（URL 层）

## Task 12: VariantBuildPipeline stage decomposition (5.1)
- [x] 12.1 提取 `BootstrapThemeStage`（theme bootstrap + layout detection）
- [x] 12.2 提取 `BuildDataModuleStage`（module 构建）
- [x] 12.3 提取 `GenerateRoutesStage`（route pipeline）
- [x] 12.4 提取 `RunPluginDeriveStage`（plugin derive pages）
- [x] 12.5 提取 `BuildSeoStage`（SEO alternates + taxonomy）
- [x] 12.6 提取 `RenderPagesStage`（renderer 创建 + 并行渲染）
- [x] 12.7 提取 `SyncAssetsStage`（asset + static 同步）
- [x] 12.8 提取 `RunPluginAfterBuildStage`（plugin after build hook）
- [x] 12.9 提取 `GenerateReportStage`（report 生成）
- [x] 12.10 重构 `ExecuteAsync` 为 stage 调度编排
- [x] 12.11 新增集成回归测试：拆分前后构建输出一致

## Task 13: Plugin dual-track documentation (5.2)
- [x] 13.1 更新 `guide/user/20-external-plugins.md`：新增"插件分类与安全级别"章节
- [x] 13.2 包含分类表：Built-in / Process / Future WASM / Section — trust level + 适用场景
- [x] 13.3 更新 `guide/user/20-external-plugins.zh-CN.md`：同步中文翻译
- [x] 13.4 process 插件章节明确声明：无 sandbox，等同本地命令执行

# Task Dependencies

- **Task 1 (P0-1)** 无依赖，可立即开始，是 Task 11 (5.3) 的前置
- **Task 2 (P0-2)**、**Task 3 (P2-4)**、**Task 7 (P2-2)**、**Task 8 (P2-3)** 无依赖，可并行
- **Task 4 (P0-3)** CLI 部分无依赖；文档部分可与 Task 13 合并
- **Task 5 (P1-1)** 无依赖（独立于 PluginRunner 逻辑）
- **Task 6 (P1-3)** 无依赖
- **Task 9 (P1-2)** 可独立实现，但需验证线程安全
- **Task 10 (P1-4)** 无依赖
- **Task 11 (5.3)** 依赖 Task 1（使用 `PlatformPathHelper`）
- **Task 12 (5.1)** 无硬依赖，建议最后执行（大型重构）
- **Task 13 (5.2)** 无依赖，可与其他任务并行；部分与 Task 4 文档重叠

# Parallel Execution Plan

**Wave 1（并行 6 个）**：T1, T2, T3, T5, T6, T7
**Wave 2（并行 4 个）**：T4, T8, T9, T10
**Wave 3（串行）**：T11（依赖 T1）→ T12 → 收尾
**Wave 4（并行）**：T13（文档，随时可做）
