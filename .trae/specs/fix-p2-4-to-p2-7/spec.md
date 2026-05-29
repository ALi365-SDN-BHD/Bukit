# Fix P2-4 ~ P2-7 (Engineering Optimization Batch) Spec

## Why

`bukit-deep-audit-report-2026-05-29.md` 在 P2 优先级中识别出 4 项工程优化问题（P2-4 ~ P2-7），覆盖**性能**（冗余锁与无效预循环）、**工程质量**（裸 catch 静默吞错）、**安全**（路径与主题继承缺乏消毒）。本 spec 一次性、系统性地修复这 4 项问题，提升性能与安全基线，同时为后续 P3 长期治理打好基础。

四个问题虽分散在不同模块，但同属"低风险、高 ROI、可独立验证"的工程优化类别，且修改面均较小（每项 < 100 行），适合在一个 spec 内并行修复。

## What Changes

### P2-4（性能）：`PageRenderDispatcher.cs` 冗余 lock + 无效预循环

- **删除冗余 `lock(stageMetricsLock)` 三处**（[PageRenderDispatcher.cs:57](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L57)、[L140](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L140)、[L178](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L178)、[L201](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L201)）：`BuildStageMetricsCollector` 内部已使用 `ConcurrentDictionary.AddOrUpdate`（[BuildStageMetrics.cs:44-57](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/BuildStageMetrics.cs#L44-L57)），其 `Increment` 和 `AddDuration` 是线程安全的，外层 `lock` 既不必要也降低并行度。
- **删除 `DispatchAsync` 的 `currentKeys` 预循环**（[PageRenderDispatcher.cs:63-66](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L63-L66)）：预循环在 `Parallel.ForEachAsync` 之前同步遍历所有 entries 仅用于 `currentKeys.TryAdd`，但 `currentKeys` 在 dispatch 内部并未再被使用（外部清理通过 manifest 完成）。同时审计指出 [L420](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L420)（`RenderSpecialListsAsync` 的预循环）类似——需确认是否同样可移除或必须保留。
- **保留并验证**：`SpecialListRenderer.MergeCollectors` 路径如果非线程安全则 lock 仍必要；需要单独评估 [L434](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L434)、[L444](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L444) 的合并 lock。

### P2-5（工程质量）：`DoctorCommand.cs:341-342` 裸 `catch {}` 加日志

- 当前代码（[DoctorCommand.cs:339-343](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/DoctorCommand.cs#L339-L343)）在主题参数一致性检查时静默吞掉 `File.ReadAllText` 错误，导致权限/IO 失败时用户无法定位。
- **改为 `catch (Exception ex)` 并通过 `Console.WriteLine($"⚠ Failed to read {file}: {ex.Message}")` 输出警告**，遵循 DoctorCommand 现有 `⚠`/`✔`/`✖` 输出风格。

### P2-6（安全）：`BuildPathUtils.MakeAbsolute` 绝对路径绕过验证

- 当前实现（[BuildPathUtils.cs:13-21](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/BuildPathUtils.cs#L13-L21)）：当 `path` 已为绝对路径时直接返回，**不验证是否在 `rootDir` 边界内**。
- **新增重载** `MakeAbsolute(string rootDir, string path, bool enforceWithinRoot)`：当 `enforceWithinRoot=true` 时，若解析后的绝对路径不在 `Path.GetFullPath(rootDir)` 子树内，抛 `ConfigException`，诊断码沿用 `DiagnosticCode.PathOutsideRoot`（若不存在则新增）。
- **现有 4 个非主题调用方保持向后兼容**（`BuildPlanner.cs:30`、`ContentProviderFactory.cs:56/83/146`）：均显式不传 `enforceWithinRoot`，行为不变。
- **主题路径调用方**（`BuildPathUtils.cs:95-115`、`ThemePathResolver.cs:97-113`）传 `enforceWithinRoot=true`：主题的 `layouts`/`assets`/`static` 不应跨越 site root 边界。
- **拒绝条件**：路径越界、含 `..` 解析后逃逸、Windows 设备名（已有 `TryGetWindowsPathIssue` 可复用判断）。

### P2-7（安全）：`ThemeBootstrapper.cs` `extends` 字段消毒

- 当前实现（[ThemeBootstrapper.cs:48-58](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeBootstrapper.cs#L48-L58)）：`themeManifest.Extends` 直接拼入 `Path.Combine(rootDir, "themes", extends)`，未拒绝 `..`/绝对路径/路径分隔符/控制字符。
- **新增 `ThemeNameSanitizer` 静态类**（`src/Bukit.Engine/ThemeNameSanitizer.cs`）：
  - `TrySanitize(string? raw, out string sanitized, out string? error)` 规则：拒绝 null/空白、绝对路径、`..` 段、`/` 或 `\` 分隔符、控制字符、Windows 设备名；只允许 `[A-Za-z0-9_\-.]+`。
- **`ThemeBootstrapper` 和 `ThemePathResolver` 共用** sanitizer：拒绝时通过 `log.Warn` 输出并视 `extends` 为 `null`（即不加载父主题），并附诊断码（沿用 BKT-04 同类）。
- **同步同样验证 `theme.Name`**（在 ThemePathResolver 入口处），防止 `name: ../etc` 攻击。

## Impact

- **Affected specs**: 无新增能力，纯修复型。与历史 BKT-04（theme.yaml 模板路径边界）、BKT-06（pipeline 名义化）逻辑同源。
- **Affected code**:
  - `src/Bukit.Engine/PageRenderDispatcher.cs`（删 lock、删预循环）
  - `src/Bukit.Engine/BuildPathUtils.cs`（新重载 + 边界校验）
  - `src/Bukit.Engine/ThemePathResolver.cs`（调用边界校验 + 调用 sanitizer）
  - `src/Bukit.Engine/ThemeBootstrapper.cs`（调用 sanitizer）
  - `src/Bukit.Engine/ThemeNameSanitizer.cs`（**新增**）
  - `src/Bukit.Cli/Commands/DoctorCommand.cs`（catch 加日志）
  - `tests/Bukit.Engine.Tests/`（新增 PageRenderDispatcherConcurrencyTests、BuildPathUtilsBoundaryTests、ThemeNameSanitizerTests、ThemeBootstrapperSanitizationTests 或扩展现有 *Tests.cs）
  - `tests/Bukit.Cli.Tests/`（扩展 DoctorCommandTests 覆盖 catch 路径）

## ADDED Requirements

### Requirement: Theme Name Sanitization

The system SHALL sanitize `theme.name` and `theme.extends` to prevent path traversal and reject illegitimate values before path composition.

#### Scenario: Reject `..` segment in extends

- **WHEN** `theme.yaml` 声明 `extends: "../malicious"` 或 `extends: "../../etc"`
- **THEN** ThemeBootstrapper 视 `extends` 为 `null`（不加载父主题）并通过 logger.Warn 输出形如 `theme extends rejected: '..' segment not allowed` 的告警，不抛异常导致构建中断

#### Scenario: Reject absolute path in extends

- **WHEN** `theme.yaml` 声明 `extends: "/usr/local/themes/foo"` 或 `extends: "C:\\themes\\foo"`
- **THEN** sanitizer 拒绝并输出 `theme extends rejected: absolute paths not allowed`

#### Scenario: Accept valid theme names

- **WHEN** `extends: "my-parent_theme.v2"`
- **THEN** sanitizer 接受，并完成正常的父主题加载

### Requirement: Build Path Boundary Enforcement (Opt-in)

The system SHALL provide an opt-in mode for `BuildPathUtils.MakeAbsolute` that rejects paths resolving outside the site root.

#### Scenario: Reject absolute path outside root

- **WHEN** 调用 `MakeAbsolute("/site", "/etc/passwd", enforceWithinRoot: true)`
- **THEN** 抛 `ConfigException`，message 含 `path outside root boundary`，并附 `rootDir` 与传入路径用于诊断

#### Scenario: Reject relative path with `..` escape

- **WHEN** 调用 `MakeAbsolute("/site", "../../../etc/passwd", enforceWithinRoot: true)`
- **THEN** 同样抛 `ConfigException`

#### Scenario: Accept path inside root

- **WHEN** 调用 `MakeAbsolute("/site", "themes/foo/layouts", enforceWithinRoot: true)`
- **THEN** 返回 `/site/themes/foo/layouts`，不抛异常

#### Scenario: Backward compatible default

- **WHEN** 调用单参数版本 `MakeAbsolute("/site", "/etc/passwd")`（不传 enforceWithinRoot）
- **THEN** 保持现有行为（返回 `/etc/passwd`），不破坏现有 4 个调用方

## MODIFIED Requirements

### Requirement: DoctorCommand Theme Params File Read

DoctorCommand SHALL log a warning (rather than silently swallow) when a layout file cannot be read during theme params consistency check.

#### Scenario: IO failure produces visible warning

- **WHEN** `CheckThemeParamsConsistency` 在读 layouts 目录的某个 `.html` 文件时遇到权限拒绝或 IO 异常
- **THEN** DoctorCommand 输出 `⚠ Failed to read <file-path>: <ex.Message>` 到 stdout，但继续处理后续文件，不中断 doctor 流程

### Requirement: PageRenderDispatcher Metrics Collection

PageRenderDispatcher SHALL collect stage metrics without extraneous locking around already-thread-safe `BuildStageMetricsCollector`.

#### Scenario: Concurrent dispatch produces consistent metrics

- **WHEN** 并发执行 `DispatchAsync` 处理 1000 个 RenderEntry（混合 Page/List/Static）
- **THEN** `stageMetrics.Snapshot()` 中 `pageRender + listBuild + staticRender` 的计数总和等于实际渲染数，且执行不抛异常、不出现 race 导致的丢更新

#### Scenario: Predispatch loop removed

- **WHEN** `DispatchAsync` 被调用且 `currentKeys` 在外层未被使用（验证调用方）
- **THEN** 内部移除同步预循环 `foreach (var entry in entries) currentKeys.TryAdd(...)`，转为在主 `Parallel.ForEachAsync` 内部按需写入或直接删除该参数路径

## REMOVED Requirements

无。
