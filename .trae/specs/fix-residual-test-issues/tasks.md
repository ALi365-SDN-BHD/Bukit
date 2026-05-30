# Tasks

## Task 1: SafeUrl 从 internal 改为 public (P0)
- [x] 1.1 修改 `src/Bukit.Shared/SafeUrl.cs` 第 3 行：`internal static class SafeUrl` → `public static class SafeUrl`
- [x] 1.2 验证 `dotnet build` 通过（Release 0 warning/0 error）
- [x] 1.3 运行 `dotnet test` 确保 `SafeUrlTests` 和所有引用 SafeUrl 的测试通过

## Task 2: 修复两个 CI 插件测试的 Task.WhenAny 假绿 (P1)
- [x] 2.1 修改 `tests/Bukit.Cli.Tests/BuildCommandTests.cs` 中 `RunAsync_CIEnvWithAllowExternalPlugins_BuildSucceeds`：`if (completed == buildTask)` → `Assert.Same(buildTask, completed);`
- [x] 2.2 修改 `tests/Bukit.Cli.Tests/BuildCommandTests.cs` 中 `RunAsync_NonCIEnv_ExternalPluginsWorkNormally`：`if (completed == buildTask)` → `Assert.Same(buildTask, completed);`
- [x] 2.3 运行 CLi 测试验证两个修改后的测试仍通过

## Task 3: 重命名 RunAsync_JobsFour_RunsSuccessfully (P2)
- [x] 3.1 在 `tests/Bukit.Cli.Tests/BuildCommandTests.cs` 中重命名 `RunAsync_JobsFour_RunsSuccessfully` → `RunAsync_JobsFour_StartsBuildWithoutArgumentError`
- [x] 3.2 运行 CLi 测试验证重命名后测试通过

# Task Dependencies

- **Task 1** (P0) 无依赖，可立即开始
- **Task 2** (P1) 无依赖，可与 Task 1 并行
- **Task 3** (P2) 无依赖，可与 Task 1、Task 2 并行

三个任务完全独立，可并行执行。
