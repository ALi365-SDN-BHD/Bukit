# Tasks

- [x] Task 1: DevCommand.cs 新增 RunAsync(ArgReader) 适配器
  - 在 `RunAsync(CliBoundCommand command)` 方法之前插入：
    ```csharp
    public static Task<int> RunAsync(ArgReader reader)
    {
        var spec = BukitCliSpecs.CreateRegistry().Resolve("dev");
        return RunAsync(CliBoundCommandFactory.Create(reader, spec));
    }
    ```
  - 确保添加必要的 `using`（`Bukit.Cli.Cli.Binding` 已有，检查是否需要 `using Bukit.Cli;`）

- [x] Task 2: DevCommand.cs 删除 RunAsync(string[]) 及其手动 switch
  - 删除 `RunAsync(string[] args)` 方法（第 30-53 行，共 24 行）
  - 该方法现在完全由 `RunAsync(ArgReader)` + `CliBoundCommandFactory` 替代
  - 保留 `ExtractOptions` 方法（被 `RunAsync(CliBoundCommand)` 使用）
  - 保留 `RunAsync(CliBoundCommand)` 和 `RunCoreAsync`

- [x] Task 3: Program.cs 旧路径 DevCommand 调用对齐
  - 第 69 行：`"dev" => await DevCommand.RunAsync(args[1..])` 改为 `"dev" => await DevCommand.RunAsync(reader)`
  - 与其他 6 个已迁移命令（build/deploy/doctor/preview/lint/clone）保持一致

- [x] Task 4: 构建和全量测试
  - `dotnet build bukit.slnx -c Release`: 0 错误 0 警告 ✅
  - `dotnet test bukit.slnx -c Release`: 2936 通过 / 0 失败 ✅

# Task Dependencies
- Task 2 依赖 Task 1（先加新方法再删旧方法，保证编译不中断）
- Task 3 可与 Task 1-2 并行（Program.cs 调用签名变化等待 DevCommand.cs 就绪后一起编译）
- Task 4 依赖所有前置任务
