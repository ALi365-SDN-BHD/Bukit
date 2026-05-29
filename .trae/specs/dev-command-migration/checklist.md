# Checklist

## DevCommand 代码
- [x] DevCommand.RunAsync(ArgReader) 方法存在
- [x] DevCommand.RunAsync(ArgReader) 使用 CliBoundCommandFactory.Create(reader, spec)
- [x] DevCommand.RunAsync(ArgReader) 委托给 RunAsync(CliBoundCommand)
- [x] DevCommand.RunAsync(string[]) 方法已删除
- [x] DevCommand.RunAsync(CliBoundCommand) 不变
- [x] DevCommand.RunCoreAsync 不变
- [x] DevCommand.ExtractOptions 不变
- [x] 手动 for+switch 解析已消除

## Program.cs
- [x] 旧路径不再有 `args[1..]` 直接传 DevCommand 的特例
- [x] 旧路径 DevCommand 分支改为 `await DevCommand.RunAsync(reader)`
- [x] 新路径 DevCommand 分支不变（`await DevCommand.RunAsync(simple.BoundCommand)`）

## ArgReader 引用一致性
- [x] DevCommand.cs 中不再有原始 `string[]` 参数解析
- [x] 所有调用 DevCommand 的路径都通过 ArgReader 或 CliBoundCommand

## 构建和测试
- [x] dotnet build bukit.slnx -c Release: 0 错误 0 警告
- [x] dotnet test bukit.slnx -c Release: 2936 通过 / 0 失败
- [x] DevCommand 相关测试通过
