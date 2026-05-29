# Tasks

- [ ] Task 1: Program.cs 删除旧路径 7 条死分支
  - 修改 `/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Program.cs`
  - 从旧路径 switch（第 63-89 行）中删除以下 7 行：
    - L67: `"build" => await BuildCommand.RunAsync(reader),`
    - L68: `"deploy" => await DeployCommand.RunAsync(reader),`
    - L69: `"dev" => await DevCommand.RunAsync(reader),`
    - L70: `"preview" => await PreviewCommand.RunAsync(reader),`
    - L74: `"doctor" => await DoctorCommand.RunAsync(reader),`
    - L75: `"lint" => await LintCommand.RunAsync(reader),`
    - L79: `"clone" => await CloneCommand.RunAsync(reader),`
  - 删除后 switch 从 22 分支缩减到 15 分支
  - 保留所有其他分支不变

- [ ] Task 2: 构建和全量测试
  - `dotnet build bukit.slnx -c Release`: 0 错误 0 警告
  - `dotnet test bukit.slnx -c Release`: 全部通过

- [ ] Task 3: 产出 ArgReader 删除阻断分析文档
  - 在 spec.md 中已记录阻断原因
  - 明确标注：ArgReader 无法删除，62 处引用 / 29 个文件
  - 删除前置条件：16 个子命令命令全部迁移到 CliBoundCommand

# Task Dependencies
- Task 2 依赖 Task 1
- Task 3 无依赖
