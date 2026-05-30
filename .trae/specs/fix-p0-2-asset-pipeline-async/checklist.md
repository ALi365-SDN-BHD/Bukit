# Checklist

- [x] ScssCompiler.CompileIfEnabled 使用 `await Process.WaitForExitAsync()` 等待 sass CLI 完成
- [x] ImageOptimizer.OptimizeIfEnabled 使用 `await Process.WaitForExitAsync()` 等待图片工具完成
- [x] AssetPipeline.ExecuteAsync 不再使用外层 `Task.Run`，直接调用异步 `ExecuteCoreAsync`
- [x] 4 个子操作（static/assets/tokens/media）通过 `Task.WhenAll` 并行执行
- [x] `DirectoryCopy.Sync` 同步调用通过 `Task.Run` 隔离在并行组内
- [x] CancellationToken 正确传播到所有子任务
- [x] 子任务独立收集 StageMetrics，最终 Merge 到父级
- [x] `dotnet build src/Bukit.Engine/Bukit.Engine.csproj` 0 错误 0 警告
- [x] 现有 Engine 测试全部通过（1072/1072）
- [x] AssetPipeline 的公共接口（ExecuteAsync 签名）对调用方无变更
