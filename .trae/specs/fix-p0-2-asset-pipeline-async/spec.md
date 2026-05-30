# AssetPipeline 异步化修复 Spec

## Why
AssetPipeline.ExecuteAsync 当前是 **伪异步**——通过 `Task.Run(() => ExecuteCore(...))` 将纯同步的 ExecuteCore 丢到线程池执行，阻塞线程池线程。大站点构建时（大量静态文件拷贝 + SCSS 编译 + 图片优化），该线程被长时间占用，且内部 4 个独立阶段（静态文件、资源处理、Token 生成、媒体同步）完全顺序执行，无法利用并发优势。

## What Changes
- AssetPipeline.ExecuteAsync 从伪异步改为**真异步并行**：移除外层 `Task.Run`，内部 4 个独立操作通过 `Task.WhenAll` 并行化
- ScssCompiler 和 ImageOptimizer 的外部进程调用改为 `await Process.WaitForExitAsync()`，释放线程等待
- DirectoryCopy.Sync（同步文件 I/O）隔离到独立 `Task.Run`，作为并行组中的一员
- ThemeTokensProcessor 加载/写入改为异步方法（含文件读写的异步化）
- 调用方 VariantBuildPipeline 保持不变（已 `await` AssetPipeline）

## Impact
- Affected specs: 无（纯内部重构）
- Affected code: `src/Bukit.Engine/AssetPipeline.cs`、`src/Bukit.Engine/DirectoryCopy.cs`、`src/Bukit.Engine/ScssCompiler.cs`、`src/Bukit.Engine/ImageOptimizer.cs`、`src/Bukit.Theme/ThemeTokensProcessor.cs`、`src/Bukit.Theme/ThemeTokensLoader.cs`

## ADDED Requirements

### Requirement: AssetPipeline 真异步并行执行
AssetPipeline SHALL 在不阻塞线程池线程的情况下执行资产处理，并将 4 个独立子操作并行化。

#### Scenario: 全量构建——所有目录存在
- **WHEN** StaticDir、AssetsDir、ThemeRoot、MediaDownloadDir 均存在
- **THEN** 静态文件拷贝、资源处理（SCSS+图片优化+拷贝）、Token 生成、媒体同步应当并行执行
- **AND** 所有操作完成后，聚合 4 个子操作的 StageMetrics

#### Scenario: 部分目录缺失
- **WHEN** 只有 StaticDir 和 AssetsDir 存在，ThemeRoot 和 MediaDownloadDir 为 null
- **THEN** 只并行执行静态文件拷贝和资源处理
- **AND** 不应尝试执行不存在的路径

#### Scenario: 资源处理中包含 SCSS 编译和图片优化
- **WHEN** ScssConfig.Enabled=true 且存在 .scss 文件
- **THEN** SCSS 编译应使用 `await Process.WaitForExitAsync()` 而非同步 `WaitForExit`
- **WHEN** ImageConfig.Enabled=true 且存在图片文件
- **THEN** 图片优化应使用 `await Process.WaitForExitAsync()` 而非同步 `WaitForExit`

### Requirement: ScssCompiler 进程异步化
ScssCompiler.CompileIfEnabled SHALL 使用异步方法等待外部 sass CLI 进程完成。

#### Scenario: SCSS 编译成功
- **WHEN** sass CLI 存在且 .scss 文件合法
- **THEN** 使用 `await process.WaitForExitAsync()` 等待编译完成
- **AND** 线程在等待期间可释放回线程池

### Requirement: ImageOptimizer 进程异步化
ImageOptimizer.OptimizeIfEnabled SHALL 使用异步方法等待外部图片工具进程完成。

#### Scenario: 图片优化成功
- **WHEN** cwebp/magick 工具存在且输入图片合法
- **THEN** 使用 `await process.WaitForExitAsync()` 等待优化完成
- **AND** 线程在等待期间可释放回线程池
