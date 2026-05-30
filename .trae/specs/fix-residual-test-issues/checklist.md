# SafeUrl + Task.WhenAny 残余修复 + 测试命名修正 Checklist

## P0: SafeUrl public visibility
- [x] `SafeUrl.cs` 第 3 行改为 `public static class SafeUrl`
- [x] `dotnet build` Release 0 warning / 0 error
- [x] `SafeUrlTests` 全部通过（40 个测试）
- [x] `InternalsVisibleTo("Bukit.Content")` 保留未删除
- [x] 架构测试 `InternalsVisibleTo_MustOnlyExposeTo_TestOrSiblingAssemblies` 通过

## P1: Task.WhenAny false-green elimination
- [x] `RunAsync_CIEnvWithAllowExternalPlugins_BuildSucceeds` 使用 `Assert.Same(buildTask, completed);` 而非 `if (completed == buildTask)`
- [x] `RunAsync_NonCIEnv_ExternalPluginsWorkNormally` 使用 `Assert.Same(buildTask, completed);` 而非 `if (completed == buildTask)`
- [x] 两个测试运行通过（`dotnet test --filter "FullyQualifiedName~BuildCommandTests"`）

## P2: Test naming correction
- [x] `RunAsync_JobsFour_RunsSuccessfully` 已重命名为 `RunAsync_JobsFour_StartsBuildWithoutArgumentError`
- [x] 重命名后测试运行通过

## 全局质量门禁
- [x] `dotnet build` Release 0 warning / 0 error
- [x] `dotnet test` 无回归
