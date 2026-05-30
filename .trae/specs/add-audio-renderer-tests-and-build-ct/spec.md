# AudioBlockRenderer 专项测试 + BuildCommand CancellationToken 规范

## Why

两个独立但相关的代码质量改进：
1. **AudioBlockRenderer 专项测试**：SafeUrl 工具函数测试只能保证工具本身安全，不能保证 block renderer 始终调用它。需要在 renderer 层增加专项安全测试，覆盖危险输入（XSS、协议走私、协议相对 URL）和正常输出（audio 标签 + rel 属性）。
2. **BuildCommand CancellationToken**：现在 BuildCommand.RunAsync 不接收 CancellationToken，测试只能用 Task.WhenAny 做超时保护但不能主动取消后台构建。而整个底层（SiteEngine → BuildPipeline → PageRenderDispatcher → Parallel.ForEachAsync）已完整支持 CancellationToken，仅 CLI 入口层缺失。

## What Changes

### 9.1 AudioBlockRenderer 专项测试（已存在，仅需确认）
- **状态**：所需测试已存在于 `NotionBlockRendererEdgeCasesTests.cs`
  - `AudioBlockRenderer_DangerousUrl_ReturnsNull`：覆盖 `javascript:alert(1)`、`data:text/html,...`、`//evil.com/audio.mp3`
  - `AudioBlockRenderer_ExternalUrl_RendersRelNoopener`：覆盖 `https://...` 输出 `rel="noopener noreferrer"`
  - `AudioBlockRenderer_WithoutCaption_RendersAudioLinkOnly`：覆盖完整 `<audio>` + `<a>` 输出
- **动作**：确认测试通过并标记完成

### 9.2 BuildCommand 增加 CancellationToken
- `BuildCommand.RunAsync` 签名增加 `CancellationToken cancellationToken = default` 参数
- 将 `cancellationToken` 传给 `engine.BuildAsync(...)`
- 三个调用点（Program.cs、DeployCommand.cs、CloneVerifier.cs）默认无需改动（默认值 `default`）
- BuildCommand 测试可传入 `cts.Token` 实现真正的构建取消

## Impact

- Affected specs: 无
- Affected code:
  - `src/Bukit.Cli/Commands/BuildCommand.cs`（9.2 修改）
  - `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs`（9.1 验证）
  - `tests/Bukit.Cli.Tests/BuildCommandTests.cs`（9.2 验证）

## ADDED Requirements

### Requirement: BuildCommand 支持 CancellationToken
BuildCommand.RunAsync SHALL 接受可选的 CancellationToken 参数，并传递给 SiteEngine.BuildAsync，使得调用方可以主动取消构建过程。

#### Scenario: BuildCommand 传递 CancellationToken 给引擎
- **WHEN** 调用 `BuildCommand.RunAsync(command, cancellationToken)`
- **THEN** `cancellationToken` 被传递给 `engine.BuildAsync(config, rootDir, overrides, cancellationToken)`

#### Scenario: 不传 CancellationToken 时保持向后兼容
- **WHEN** 调用 `BuildCommand.RunAsync(command)`（不传 cancellationToken）
- **THEN** 使用 `default`（CancellationToken.None），行为与当前完全一致

#### Scenario: 测试可以主动取消构建
- **WHEN** 测试中 `cts.Cancel()` 被调用
- **THEN** 构建过程中的 `Parallel.ForEachAsync` 等可取消操作应抛出 `OperationCanceledException`

## REMOVED Requirements

无
