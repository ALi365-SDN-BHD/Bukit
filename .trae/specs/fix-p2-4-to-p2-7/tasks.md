# Tasks: Fix P2-4 ~ P2-7

> 修复采用并行策略：将 4 个问题分为 **2 个独立子代理任务束**（无文件冲突），每束包含 Red → Green → Refactor 完整循环，并各自负责自身的测试与构建验证。

- [x] Task 1: 性能与工程质量修复束（P2-4 + P2-5）
  - 文件域: `src/Bukit.Engine/PageRenderDispatcher.cs`、`src/Bukit.Cli/Commands/DoctorCommand.cs`、`tests/Bukit.Engine.Tests/`、`tests/Bukit.Cli.Tests/`
  - [x] SubTask 1.1（Red）: 新建 `tests/Bukit.Engine.Tests/BuildStageMetricsCollectorConcurrencyTests.cs`：4 个测试覆盖 Increment/AddDuration/Merge/MixedOperations 的并发线程安全，验证 `BuildStageMetricsCollector` 无需外层 lock。
  - [x] SubTask 1.2（Green/Refactor for P2-4）: `PageRenderDispatcher.cs` 删除 2 处 `stageMetricsLock` 变量（L57、L416）和 5 处 `lock(stageMetricsLock)` 包裹（L140、L178、L201、L434、L444）。L434/L444 处 `stageMetrics = MergeCollectors(...)` 改为 `stageMetrics.Merge(...)`（调用线程安全的 `BuildStageMetricsCollector.Merge` 实例方法）。**保留 `currentKeys` 预循环**——经查 `currentKeys` 由 `BuildManifestTracker.DeleteStaleManifestOutputs` 用于增量构建的过时文件清理（[BuildManifestTracker.cs:74-77](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Incremental/BuildManifestTracker.cs#L74-L77)），删除会导致清理逻辑错误删除当前构建产物。审计报告中此项为"中"优先级，安全考量优先于微优化，故保留并记录。
  - [x] SubTask 1.3（Red for P2-5）: 新建 `tests/Bukit.Cli.Tests/DoctorCommandAppendFileOrWarnTests.cs`，3 个测试覆盖：正常读取追加内容、不可读文件输出警告、文件不存在输出警告。
  - [x] SubTask 1.4（Green for P2-5）: 修改 `DoctorCommand.cs`：抽出 `internal static AppendFileOrWarn(string file, StringBuilder dst)` 方法，内部 `catch (Exception ex) { Console.WriteLine($"⚠ Failed to read {file}: {ex.Message}"); }`，替代原裸 `catch {}`。`CheckThemeParamsConsistency` 调用该方法。
  - [x] SubTask 1.5（Verify）: `dotnet build` 0 警告 0 错误；`Engine.Tests` 1069 通过；`Cli.Tests` 743 通过；`dotnet format --verify-no-changes` 通过。

- [x] Task 2: 安全消毒修复束（P2-6 + P2-7）
  - 文件域: `src/Bukit.Engine/BuildPathUtils.cs`、`src/Bukit.Engine/ThemePathResolver.cs`、`src/Bukit.Engine/ThemeBootstrapper.cs`、新增 `src/Bukit.Engine/ThemeNameSanitizer.cs`、`tests/Bukit.Engine.Tests/`
  - [x] SubTask 2.1（Red for P2-6）: `BuildPathUtilsTests.cs` 新增 4 个 `MakeAbsolute_Should_*` 测试。
  - [x] SubTask 2.2（Green for P2-6）: `BuildPathUtils.cs` 新增 `MakeAbsolute(string, string, bool enforceWithinRoot)` 重载，越界路径抛 `ConfigException(DiagnosticCode.ConfigPathTraversal)`；原重载委托保持兼容；`ResolveThemeDirInternal` 和 `ThemePathResolver.ResolveThemeDirs` 主题分支启用 `enforceWithinRoot: true`；`IsWindowsDeviceName` 改为 internal。
  - [x] SubTask 2.3（Red for P2-7）: 新建 `ThemeNameSanitizerTests.cs`，7 个测试覆盖 `..`、绝对路径、路径分隔符、控制字符、Windows 设备名、null/whitespace、合法名。
  - [x] SubTask 2.4（Green for P2-7）: 新建 `src/Bukit.Engine/ThemeNameSanitizer.cs` 实现 `TrySanitize(string?, out string, out string?)`，规则全部覆盖。
  - [x] SubTask 2.5（Green for P2-7 集成）: `ThemeBootstrapper.cs` 对 `themeManifest.Extends` 调用 sanitizer，失败 `log.Warn` 并跳过父主题；`ThemePathResolver.cs` 对 `theme.Extends` 同样处理；对 `theme.Name` 失败抛 `ConfigException`。新建 `ThemeBootstrapperSanitizationTests.cs` 5 个集成测试。
  - [x] SubTask 2.6（Verify）: `dotnet build` 0 警告 0 错误；`Engine.Tests` 1069 通过；`Content.Tests` 540 通过；`dotnet format` 通过。

# Task Dependencies

- Task 1 和 Task 2 **完全独立**（文件域不重叠），由 2 个子代理并行执行（实际：Task 2 由子代理完成，Task 1 因子代理结果丢失改由主代理直接完成）。
- Task 2.5 依赖 2.2 + 2.4 完成（同一束内顺序执行）。
- 最终主代理负责跨任务束的统一验证：build + Engine/Cli/Content 测试 + format，全部通过。
