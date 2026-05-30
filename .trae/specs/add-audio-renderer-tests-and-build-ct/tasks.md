# Tasks

## 9.1 AudioBlockRenderer 专项测试（验证已存在）

- [x] Task 1: 验证 AudioBlockRenderer 安全测试已存在且通过
  - [x] 运行 Content 测试集确认 `AudioBlockRenderer_DangerousUrl_ReturnsNull`、`AudioBlockRenderer_ExternalUrl_RendersRelNoopener`、`AudioBlockRenderer_WithoutCaption_RendersAudioLinkOnly` 三个测试通过
  - [x] 确认测试覆盖四种 URL：`javascript:alert(1)` → null、`data:text/html,...` → null、`//evil.com/a.mp3` → null、`https://example.com/a.mp3` → 输出 audio + rel

## 9.2 BuildCommand 增加 CancellationToken

- [x] Task 2: 修改 BuildCommand.RunAsync 签名，增加 CancellationToken 参数
  - [x] 在 `BuildCommand.cs` 第 10 行增加 `CancellationToken cancellationToken = default` 参数
  - [x] 第 42 行 `engine.BuildAsync()` 调用传入 `cancellationToken`

- [x] Task 3: 运行 CLI 测试确认向后兼容
  - [x] 运行 `Bukit.Cli.Tests` 全部测试（771 通过，0 失败）
  - [x] 确认所有现有测试通过（默认值 `default` 行为不变）

- [x] Task 4: 运行 Content + Engine 测试确认无回归
  - [x] 运行 `Bukit.Content.Tests` 全部测试（563 通过，0 失败）
  - [x] 运行 `Bukit.Engine.Tests` 全部测试（1098 通过，2 预存失败，与本修改无关）

# Task Dependencies

- Task 1 无依赖，可独立执行
- Task 2 无依赖，可独立执行
- Task 3 依赖 Task 2
- Task 4 依赖 Task 2
- Task 1 和 Task 2 可并行
