# Audit Hardening Round 2 Checklist

## P0-1: Cross-platform path comparison
- [x] `PlatformPathHelper.PathComparison` 在 Windows 返回 `OrdinalIgnoreCase`，Linux/macOS 返回 `Ordinal`
- [x] `FileWriter.GetSafeFullPath` 使用 `PlatformPathHelper.PathComparison`
- [x] `SafeOutputFileSystem.GetSafeFullPath` 使用 `PlatformPathHelper.PathComparison`
- [x] `BuildPlanner.EnsureOutputDirectoryCanBeCleaned` 使用统一比较
- [x] `DeleteEmptyDirectoriesUpToRoot` 使用统一比较
- [x] Linux/macOS 下大写路径穿越被拒绝（测试通过）
- [x] Windows 下大小写不敏感行为不变（测试通过）
- [x] 正常路径不受影响（测试通过）
- [x] Release build 0 warning / 0 error

## P0-2: Symlink detection and rejection
- [x] `DirectoryCopy.IsSymlink` 正确检测 `FileAttributes.ReparsePoint`
- [x] `DirectoryCopy.Sync` 跳过文件 symlink + warning 日志
- [x] `DirectoryCopy.SyncFilesRecursive` 跳过目录 symlink
- [x] `BuildManifestTracker` 跳过 symlink
- [x] `build.followSymlinks` 默认 false，设为 true 时跳过检测
- [x] 普通文件正常复制不受影响
- [x] Release build 0 warning / 0 error

## P2-4: publishDotFiles with mandatory sensitive deny
- [x] `publishDotFiles: true` 时 `.env` 仍被拒绝
- [x] `publishDotFiles: true` 时 `*.pem` / `*.key` / `*.pfx` / `*.p12` 仍被拒绝
- [x] `publishDotFiles: true` 时 `.git` / `.github` 仍被拒绝
- [x] `publishDotFiles: true` 时 `.well-known/` 正常输出
- [x] `publishDotFiles: true` 时 `.htaccess` 等非敏感 dotfile 正常输出
- [x] `publishDotFiles: false`（默认）行为不变
- [x] Release build 0 warning / 0 error

## P0-3: External process plugin safety
- [x] CI 环境（`CI=true` 或 `BUKIT_CI=true`）默认禁用外部插件
- [x] `--allow-external-plugins` CLI flag 可在 CI 中启用
- [x] 非 CI 环境外部插件正常执行
- [x] 绝对路径 entry 被拒绝（除非 `allowAbsoluteEntry: true`）
- [x] 项目目录内 entry 正常
- [x] 文档包含 trust model 声明
- [x] 文档说明 CI 禁用策略和启用方式
- [x] Release build 0 warning / 0 error

## P1-1: DeriveConflictPolicy last-wins real override
- [x] last-wins 策略下 derived 覆盖 derived（旧条目被移除）
- [x] derived 不可覆盖原始 content 路由（报错或 warning）
- [x] same url different outputPath 正确去重
- [x] different url same outputPath 冲突正确处理
- [x] error 策略回归测试通过
- [x] sitemap/search index 无重复条目
- [x] Release build 0 warning / 0 error

## P1-3: TrackAssetOutputs nullable signature
- [x] `TrackAssetOutputs` 第二个参数为 `string?`
- [x] 调用方 `AssetPipeline` 不再使用 `!` 操作符
- [x] 仅 parent assets 存在时正常
- [x] 仅 child assets 存在时正常
- [x] 两者都存在时正常
- [x] Release build nullable warning 无新增

## P2-2: --jobs illegal input error
- [x] `--jobs abc` → exit code 2 + 错误信息
- [x] `--jobs -1` → exit code 2 + 错误信息
- [x] `--jobs 0` → exit code 2 + 错误信息
- [x] `--jobs 4` 正常构建
- [x] Release build 0 warning / 0 error

## P2-3: AutoSummary via BuildContext
- [x] `BuildContext.AutoSummary` 和 `AutoSummaryMaxLen` 字段存在
- [x] `BuildCommand` 设置 `BuildContext` 字段，不调用 `SetEnvironmentVariable`
- [x] 消费方从 `BuildContext` / `RenderContext` 读取，不读环境变量
- [x] `BUKIT_AUTO_SUMMARY` / `BUKIT_AUTO_SUMMARY_MAXLEN` 环境变量代码已移除
- [x] 单元测试无全局状态污染

## P1-2: Multi-language parallel build
- [x] `build.languageJobs` 配置项存在（默认 1）
- [x] `languageJobs: 4` 时最多 4 语言并行
- [x] `languageJobs > ProcessorCount` 时限制为 `ProcessorCount`
- [x] 并行构建结果与串行一致（文件数量、内容）
- [x] 各语言 manifest 独立无误
- [x] Release build 0 warning / 0 error

## P1-4: Manifest fingerprint sha256 mode
- [x] `build.fingerprintMode` 配置项存在（`size-time` | `sha256`，默认 `size-time`）
- [x] sha256 模式检测内容变更（同大小不同内容）
- [x] size-time 默认行为不变
- [x] 非法值报错
- [x] HTML/static/asset/media 统一使用 fingerprintMode
- [x] Release build 0 warning / 0 error

## 5.3: Unified output path policy
- [x] `IOutputPathPolicy` 接口定义（含 `ResolveSafePath` 方法）
- [x] `SafePathResolver` 实现
- [x] `OutputPathSecurityException` 异常类
- [x] `FileWriter` 使用 `IOutputPathPolicy`
- [x] `DirectoryCopy` 使用 `IOutputPathPolicy`
- [x] `BuildManifestTracker` 删除操作使用 `IOutputPathPolicy`
- [x] `RouteSecurityValidator` 保持独立（URL 层）

## 5.1: VariantBuildPipeline stage decomposition
- [x] 9 个 stage 方法独立可调用
- [x] `ExecuteAsync` 编排 stage 方法
- [x] 拆分前后构建输出一致（集成回归测试）
- [x] 各 stage 可独立单测
- [x] Release build 0 warning / 0 error

## 5.2: Plugin dual-track documentation
- [x] 英文指南包含插件分类表（Built-in / Process / Future WASM / Section）
- [x] 中文指南同步更新
- [x] Process 插件章节有 trust model 和风险声明
- [x] CI 禁用策略文档化

## 全局质量门禁
- [x] `dotnet build` Release 0 warning / 0 error
- [x] `dotnet test` 全部通过（无回归）— 2949 通过，6 预存失败
- [x] `dotnet format --verify-no-changes` 通过
- [x] 无新增 Nullable 警告
