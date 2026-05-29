# Checklist: Fix P2-4 ~ P2-7

## P2-4：PageRenderDispatcher 性能优化
- [x] `PageRenderDispatcher.DispatchAsync` 中 5 个 `lock(stageMetricsLock)` 已删除（L140、L178、L201、L434改为`.Merge()`、L444改为`.Merge()`），2 处 `stageMetricsLock` 变量声明已删除
- [x] `DispatchAsync` 中 L63-66 `foreach (var entry in entries) currentKeys.TryAdd(...)` 预循环**保留**——`currentKeys` 被 `BuildManifestTracker.DeleteStaleManifestOutputs` 用于增量构建过时文件清理，删除会导致构建产物被误删
- [x] `RenderSpecialListsAsync` 中 L418-421 预循环**保留**——同上原因
- [x] 新增 `BuildStageMetricsCollectorConcurrencyTests`，4 个测试验证并发场景下 Increment/AddDuration/Merge/MixedOperations 计数与渲染数一致

## P2-5：DoctorCommand 错误可见性
- [x] `DoctorCommand.CheckThemeParamsConsistency` 的裸 `catch {}` 已抽出为 `internal static AppendFileOrWarn(string file, StringBuilder dst)`，内部 `catch (Exception ex) { Console.WriteLine($"⚠ Failed to read {file}: {ex.Message}"); }`
- [x] 新增 `DoctorCommandAppendFileOrWarnTests`，3 个测试覆盖正常读取、不可读文件警告、文件不存在警告

## P2-6：BuildPathUtils 路径边界
- [x] `BuildPathUtils.MakeAbsolute` 新增 `(string, string, bool enforceWithinRoot)` 重载
- [x] 原 `MakeAbsolute(string, string)` 重载保持向后兼容（委托到新重载传 `false`）
- [x] `BuildPathUtils.ResolveThemeDirInternal` 主题路径分支使用 `enforceWithinRoot: true`
- [x] `ThemePathResolver.ResolveThemeDirs` 主题路径分支使用 `enforceWithinRoot: true`
- [x] 越界路径（绝对路径越界、相对路径 `..` 逃逸）抛 `ConfigException(DiagnosticCode.ConfigPathTraversal)`，message 含 `path outside root boundary`
- [x] `IsWindowsDeviceName` 从 `private` 改为 `internal` 以便 `ThemeNameSanitizer` 复用
- [x] 单测覆盖：`BuildPathUtilsTests` 4 个新测试 + `ThemePathResolverTests` 1 个新测试

## P2-7：ThemeBootstrapper extends 消毒
- [x] 新增 `src/Bukit.Engine/ThemeNameSanitizer.cs` 静态类，提供 `TrySanitize` API
- [x] Sanitizer 拒绝：null/whitespace、绝对路径、`..` 段、路径分隔符 `/\`、控制字符 < 32、Windows 设备名、限制字符集 `[A-Za-z0-9_\-.]`
- [x] `ThemeBootstrapper` 中 `themeManifest.Extends` 经 sanitizer 校验：失败 → `log.Warn` + 跳过父主题（不中断构建）
- [x] `ThemePathResolver.Resolve` 中 `theme.Extends` 同样经 sanitizer 校验：失败 → `logger.Warn` + 跳过父主题
- [x] `ThemePathResolver.Resolve` 中 `theme.Name` 经 sanitizer 校验：失败 → 抛 `ConfigException`
- [x] `ThemeNameSanitizerTests` 覆盖 7 种拒绝/接受场景
- [x] `ThemeBootstrapperSanitizationTests` 覆盖恶意 extends 不加载父主题但构建继续 + 恶意 name 抛 ConfigException

## 整体质量门禁
- [x] `dotnet build bukit.slnx -c Release -warnaserror` 0 警告 0 错误
- [x] `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release` 全绿（1069 通过）
- [x] `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release` 全绿（743 通过）
- [x] `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release` 全绿（540 通过）
- [x] `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 改动的所有 `.cs` 文件均 ≤ 600 行
- [x] Spec 引用记录修复对应的 4 个 P2 编号（P2-4、P2-5、P2-6、P2-7）
