# Tasks

- [x] Task 1: ScssCompiler 进程异步化
  - 将 `CompileIfEnabled` 改为返回 `Task`，方法签名改为异步
  - 将 `process.WaitForExit(5000)` 替换为 `await process.WaitForExitAsync(CancellationToken)`，超时用 `CancellationTokenSource(5000)` 实现
  - 将 `FindSassCli` 中的 `process.WaitForExit(3000)` 替换为异步等待（或保持同步——这是启动时一次性检测，不热路径）
  - 验证：`dotnet build src/Bukit.Engine/Bukit.Engine.csproj`

- [x] Task 2: ImageOptimizer 进程异步化
  - 将 `OptimizeIfEnabled` 改为返回 `Task`，方法签名改为异步
  - 将 `ConvertToWebp`、`ConvertToAvif` 中的 `process.WaitForExit(10000)` 替换为 `await process.WaitForExitAsync(CancellationToken)`
  - 将 `FindImageTool` 中的 `process.WaitForExit(3000)` 替换为异步等待（或保持同步——一次性检测）
  - 验证：`dotnet build src/Bukit.Engine/Bukit.Engine.csproj`

- [x] Task 3: AssetPipeline 重构为真异步并行
  - 将 `ExecuteCore` 重命名为 `ExecuteCoreAsync`，改为返回 `Task<AssetPipelineResult>`，添加 `async` 关键字
  - 移除外层 `Task.Run` 包装，`ExecuteAsync` 直接调用 `ExecuteCoreAsync`
  - 将 4 个独立子操作（static、assets+SCSS+optimize、tokens、media）用 `Task.WhenAll` 并行化
  - 对 `DirectoryCopy.Sync` 调用用 `Task.Run` 包裹（因为内部是同步文件 I/O，无法简单改为 async）
  - 确保 `cancellationToken` 正确传播到各个子任务
  - 每个子任务独立收集 StageMetrics，最后 Merge
  - 验证：`dotnet build src/Bukit.Engine/Bukit.Engine.csproj`

- [x] Task 4: 运行现有测试确认无回归
  - 运行 `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release`
  - 1072 通过 / 0 失败
  - 运行 `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release`
  - 542 通过 / 0 失败
  - 运行 `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release`
  - 748 通过 / 4 失败（均预存：DeployCommandTests + ThemeInstallCommandTests）

# Task Dependencies
- Task 1 和 Task 2 可并行执行（互不依赖）
- Task 3 依赖 Task 1 和 Task 2 完成
- Task 4 依赖 Task 3 完成
