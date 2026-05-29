# Checklist

## 基础设施
- [x] CliBoundCommandFactory.Create(reader, spec) 存在且可编译
- [x] CliBoundCommandFactory 对 Flag 类型选项使用 reader.HasFlag
- [x] CliBoundCommandFactory 对 String/Integer 类型选项使用 reader.GetOption
- [x] CliBoundCommandFactory 正确提取位置参数（跳过命令名 index 0）
- [x] CliBoundCommandFactory 去空值后返回 Dictionary

## CliParseResult 扩展
- [x] CliParseResult 改为 abstract record
- [x] SimpleParseResult 继承 CliParseResult 且行为不变
- [x] SubcommandParseResult 包含 SubcommandName 和 InnerResult
- [x] 现有 CliParser 代码对无子命令 spec 的返回类型调整正确

## CliParser 子命令递归
- [x] CliParser.Parse 在有子命令且 args[0] 匹配子命令名时递归解析
- [x] CliParser.Parse 在无子命令或不匹配时走原路径
- [x] 子命令的 --help 标志被正确识别并绑定到对应 spec

## CliCommandRegistry
- [x] ResolveSubcommand 方法存在
- [x] ResolveSubcommand 通过 Name 和 Aliases 查找子命令
- [x] ResolveSubcommand 在无匹配时返回 null

## 适配器替换
- [x] BuildCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory
- [x] DeployCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory
- [x] DoctorCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory
- [x] PreviewCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory
- [x] LintCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory，ParseOptions 方法已删除
- [x] DataCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory，ParseOptions 方法已删除
- [x] CloneCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory，外部 Builder 已删除
- [x] CloneCommandOptions.BuildCommand(ArgReader) 方法已删除
- [x] CloneCommand.RunAsync(CliBoundCommand, ArgReader) 双参重载已删除

## Program.cs 重构
- [x] 新路径不再限制 spec.Subcommands is null or { Count: 0 }
- [x] SubcommandParseResult fall through 到旧路径
- [x] 旧路径回退逻辑保留

## 配置文件解析
- [x] ConfigPathResolver 非 ArgReader 版本可用于所有迁移后的命令

## 构建和测试
- [x] dotnet build bukit.slnx -c Release: 0 错误 0 警告
- [x] dotnet test bukit.slnx -c Release: 2936 通过 / 0 失败
- [x] 架构约束测试通过（DependencyMatrixTests）
