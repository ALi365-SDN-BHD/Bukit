# Checklist

## 9.1 AudioBlockRenderer 专项测试

- [x] `AudioBlockRenderer_DangerousUrl_ReturnsNull` 测试通过，覆盖 `javascript:alert(1)`、`data:text/html,...`、`//evil.com/audio.mp3`
- [x] `AudioBlockRenderer_ExternalUrl_RendersRelNoopener` 测试通过，确认 `https://cdn.example.com/track.mp3` 输出包含 `rel="noopener noreferrer"`
- [x] `AudioBlockRenderer_WithoutCaption_RendersAudioLinkOnly` 测试通过，确认完整 `<audio>` + `<a>` 输出
- [x] Content 测试集 `Bukit.Content.Tests` 全部通过（563 ✅）

## 9.2 BuildCommand CancellationToken

- [x] `BuildCommand.RunAsync` 签名包含 `CancellationToken cancellationToken = default` 参数
- [x] `engine.BuildAsync(...)` 调用传入了 `cancellationToken`
- [x] 不传 token 的调用路径（Program.cs、DeployCommand.cs、CloneVerifier.cs）编译通过（默认值 `default`）
- [x] `Bukit.Cli.Tests` 全部测试通过（771 ✅）
- [x] `Bukit.Engine.Tests` 全部测试通过（1098 ✅，2 预存失败与本修改无关）
