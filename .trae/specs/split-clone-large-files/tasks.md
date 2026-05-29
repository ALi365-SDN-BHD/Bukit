# Tasks

> 重要约束：本 spec 最多使用 2 个 sub coding agent，因此任务按"独立可并行"的方式编成 2 组。
> Agent A 负责 P2-1 核心（CloneCommand）+ Models 拆分；Agent B 负责 Writer / Generator / Verifier 三大文件拆分。
> 两组之间无源文件交叉，可同时进行。最终由主 agent 跑构建/测试验证。

## Agent A — Command 编排与 Models（5 tasks）

- [x] Task 1: 从 `CloneCommand.cs` 提取 `CloneCommandOptions.cs`
- [x] Task 2: 从 `CloneCommand.cs` 提取 `CloneInputLoader.cs`
- [x] Task 3: 从 `CloneCommand.cs` 提取 `CloneAssetDownloader.cs`
- [x] Task 4: 从 `CloneCommand.cs` 提取 `CloneFidelityRunner.cs`
- [x] Task 5: 拆分 `CloneModels.cs` 为 4 个文件

## Agent B — Writer / Generator / Verifier（5 tasks）

- [x] Task 6: 拆分 `CloneContentWriter.cs`
- [x] Task 7: 拆分 `CloneThemeGenerator.cs`
- [x] Task 8: 拆分 `CloneFidelityGenerator.cs`
- [x] Task 9: 拆分 `CloneVerifier.cs`
- [x] Task 10: 主 agent 验证 + 基线对照

# 执行结果（2026-05-29）

- 构建：`dotnet build bukit.slnx -c Release` → 0 警告 0 错误
- 测试：`Bukit.Cli.Tests` 743 / 743 通过
- 主 agent 收尾补丁：在 `CloneCommand.cs` 末尾添加 `ParseVisualThreshold` 与 `CountBehaviors` 的 `private static` thin wrapper（委托给 CloneCommandOptions/CloneAssetDownloader），保证 4 个反射测试通过；`CloneCommandOptions.ParseVisualThreshold` visibility 从 private 提升为 internal

# Task Dependencies

- Task 1 → Task 2 → Task 3 → Task 4
- Task 5 与 Task 1-4 互不依赖
- Task 6 与 Task 3 有跨调用：通过 `CloneContentWriter` 中保留 thin wrapper（`AssetFileName`/`AssetSubdir` 等）解决
- Task 7、8、9 互不依赖
- Task 10 依赖所有前置任务
