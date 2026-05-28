# Docs Consistency Check — Verification Checklist

## Code Structure
- [x] `DocsIssue.cs` 存在且包含 FilePath, Line, Severity, CheckType, Message 字段
- [x] `DocFileScanner.cs` 存在且扫描 README*.md、guide/**/*.md、src/skills/**/SKILL.md
- [x] `CommandPathExtractor.cs` 存在且从 `CliCommandRegistry` 递归提取所有命令路径
- [x] `ConfigFieldExtractor.cs` 存在且从 `AppConfig` 反射提取 YAML 字段路径
- [x] `CliCoverageChecker.cs` 存在
- [x] `ConfigFieldChecker.cs` 存在
- [x] `FileRefChecker.cs` 存在
- [x] `ExampleParserChecker.cs` 存在
- [x] `SkillCliChecker.cs` 存在
- [x] `DocsCheckCommand.cs` 存在且组装所有 checker
- [x] `Program.cs` 中注册了 `docs` 命令路由

## Build
- [x] `dotnet build src/Bukit.Cli -c Release` 零警告通过
- [x] `dotnet build bukit.slnx -c Release` 零警告通过

## Functional Verification
- [x] `dotnet run --project src/Bukit.Cli -c Release -- docs check` 正常执行并输出结果
- [x] `dotnet run --project src/Bukit.Cli -c Release -- docs check --cli` 仅执行 CLI 覆盖率检查
- [x] `dotnet run --project src/Bukit.Cli -c Release -- docs check --config-fields` 仅执行字段检查
- [x] `dotnet run --project src/Bukit.Cli -c Release -- docs check --file-refs` 仅执行文件引用检查
- [x] `dotnet run --project src/Bukit.Cli -c Release -- docs check --examples` 仅执行示例检查
- [x] `dotnet run --project src/Bukit.Cli -c Release -- docs check --skills` 仅执行 Skill 一致性检查
- [x] 所有 flag 可组合使用（如 `--cli --skills`）
- [x] 无 flag 时默认运行全部 5 类检查

## Detection Accuracy
- [x] 能检测出 `data inspect` 作为幽灵命令（CLI 存在但文档无覆盖）
- [x] 能检测出 `data dump` 作为幽灵命令
- [x] 能检测出 `completion` 作为幽灵命令
- [x] 能检测出 `lint` 作为幽灵命令
- [x] 能检测出文档中不存在的 site.yaml 字段引用
- [x] 能检测出文档中不存在的文件路径引用
- [x] 不会误报文档中合法的 CLI 命令引用

## Exit Code Behavior
- [x] 无 ERROR 时 exit code = 0
- [x] 有 ERROR 时 exit code = 1
- [x] 仅有 WARN 时 exit code = 0

## Regression
- [x] `dotnet test tests/Bukit.Cli.Tests -c Release` 736/739 passed（3 个预存失败，非本次变更引起）

## Documentation
- [x] `governance-checklist.md` 中引用已更新为 `bukit docs check`
