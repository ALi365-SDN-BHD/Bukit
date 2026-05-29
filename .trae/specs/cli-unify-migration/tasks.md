# Tasks

## 阶段一：基础设施（顺序执行）

- [x] Task 1: 创建 CliBoundCommandFactory 统一适配器
  - 新建 `src/Bukit.Cli/Cli/Binding/CliBoundCommandFactory.cs`
  - 实现 `Create(ArgReader reader, CliCommandSpec? spec)` 方法：
    - `spec` 不为 null 时，遍历 `spec.Options`，对 Flag 类型用 `reader.HasFlag`、其他类型用 `reader.GetOption`
    - 位置参数：从 `reader.GetArg(1)` 开始提取（因为 index 0 是命令名），传入 CliBoundCommand 的 arguments
    - `spec` 为 null 时，回退到提取全量已知选项（保持向后兼容）
    - 输出：去空值后的 `Dictionary<string, string?>` + `IReadOnlyList<string> arguments`

- [x] Task 2: 扩展 CliParseResult 支持子命令
  - 修改 `src/Bukit.Cli/Cli/Parsing/CliParseResult.cs`
  - 将当前 `sealed record CliParseResult` 改为基类 `abstract record CliParseResult`
  - 新增 `sealed record SimpleParseResult : CliParseResult`（当前行为）
  - 新增 `sealed record SubcommandParseResult : CliParseResult`，包含 `SubcommandName: string` 和 `InnerResult: CliParseResult` 字段
  - 注意：需要调整 CliParser 的返回类型

- [x] Task 3: 扩展 CliParser 支持子命令递归
  - 修改 `src/Bukit.Cli/Cli/Parsing/CliParser.cs`
  - 在 `Parse` 方法开头添加子命令检测逻辑：
    1. 如果 `command.Subcommands` 非空且 `args` 第一个非选项 token 匹配某个子命令名
    2. 提取子命令名及其后续参数（`args[1..]`）
    3. 递归调用 `Parse(subcommandSpec, remainingArgs)`
    4. 返回 `SubcommandParseResult(command, boundCommand, diagnostics, subName, innerResult)`
  - 保留现有的选项/参数解析逻辑不变

- [x] Task 4: 扩展 CliCommandRegistry 子命令解析
  - 修改 `src/Bukit.Cli/Cli/Metadata/CliCommandRegistry.cs`
  - 添加 `ResolveSubcommand(CliCommandSpec parent, string subName)` 方法
  - 从 parent.Subcommands 列表中按 Name/Aliases 查找

## 阶段二：适配器替换（可并行）

- [x] Task 5: 替换 BuildCommand 适配器
  - 修改 `BuildCommand.cs`：`RunAsync(ArgReader)` 中删除内联字典，改为 `CliBoundCommandFactory.Create(reader, BukitCliSpecs.CreateRegistry().Resolve("build"))`
  - 注意：BuildCommand 当前也手动处理 `--clean` 的 Flag 逻辑（HasFlag + reader.GetOption 组合），工厂应正确复现此行为

- [x] Task 6: 替换 DeployCommand 适配器
  - 修改 `DeployCommand.cs`：`RunAsync(ArgReader)` 同样替换为 CliBoundCommandFactory

- [x] Task 7: 替换 DoctorCommand 适配器
  - 修改 `DoctorCommand.cs`：`RunAsync(ArgReader)` 替换为 CliBoundCommandFactory

- [x] Task 8: 替换 PreviewCommand 适配器
  - 修改 `PreviewCommand.cs`：`RunAsync(ArgReader)` 替换为 CliBoundCommandFactory

- [x] Task 9: 替换 LintCommand 适配器
  - 修改 `LintCommand.cs`：删除 `ParseOptions(ArgReader)` 私有方法
  - `RunAsync(ArgReader)` 改为 `CliBoundCommandFactory.Create(reader, spec)`

- [x] Task 10: 替换 DataCommand 适配器
  - 修改 `DataCommand.cs`：删除 `ParseOptions(ArgReader)` 私有方法
  - `RunAsync(ArgReader)` 改为 `CliBoundCommandFactory.Create(reader, spec)`
  - 注意：DataCommand 的适配器包含位置参数提取（`reader.GetArg(1)` 作为子命令），工厂需正确处理

- [x] Task 11: 替换 CloneCommand 适配器（清除外部 Builder）
  - 修改 `CloneCommand.cs`：`RunAsync(ArgReader)` 改为 `CliBoundCommandFactory.Create(reader, cloneSpec)` + `RunAsync(command)`
  - 删除 `RunAsync(CliBoundCommand, ArgReader)` 双参重载
  - 删除 `CloneCommandOptions.BuildCommand(ArgReader)` 方法
  - Checklist：确认 CloneCommand 中不再有 ArgReader 引用（除 RunAsync 入口本身）

## 阶段三：Program.cs 重构（依赖阶段一完成）

- [x] Task 12: 修改 Program.cs 新路径支持子命令
  - 修改 `Program.cs`：
    1. 删除 `spec.Subcommands is null or { Count: 0 }` 限制
    2. 当 `CliParser.Parse` 返回 `SubcommandParseResult` 时，fall through 到旧路径
    3. SimpleParseResult → 现有 switch dispatch
    4. 保持旧路径回退逻辑不变

- [x] Task 13: 配置路径解析统一
  - `ConfigPathResolver.Resolve(ArgReader)` 仍有 30 处调用，保持不变
  - `Resolve(string?, string?)` 已被所有迁移命令使用

## 阶段四：验证

- [x] Task 14: 构建和全量测试
  - `dotnet build bukit.slnx -c Release`: 0 错误 0 警告 ✅
  - `dotnet test bukit.slnx -c Release`: 2936 通过 / 0 失败 ✅
  - 架构约束测试通过 ✅

# Task Dependencies
- Task 2 依赖 Task 1（因为 CliParseResult 的扩展与工厂类无关，可并行，但按逻辑顺序更好）
- Task 3 依赖 Task 2（CliParser 返回新的 CliParseResult 类型）
- Task 4 可与 Task 1-3 并行
- Task 5-11 依赖 Task 1（需要 CliBoundCommandFactory 已就绪）
- Task 5-11 之间可完全并行
- Task 12 依赖 Task 3-4（需要子命令解析和注册表方法就绪）
- Task 13 依赖 Task 5-11（需要命令迁移完成）
- Task 14 依赖所有前置任务
