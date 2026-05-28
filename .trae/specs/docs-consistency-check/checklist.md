# Docs Consistency Check — Verification Checklist

## Code Structure
- [ ] `DocsIssue.cs` 存在且包含 FilePath, Line, Severity, CheckType, Message 字段
- [ ] `DocFileScanner.cs` 存在且扫描 README*.md、guide/**/*.md、src/skills/**/SKILL.md
- [ ] `CommandPathExtractor.cs` 存在且从 `CliCommandRegistry` 递归提取所有命令路径
- [ ] `ConfigFieldExtractor.cs` 存在且从 `AppConfig` 反射提取 YAML 字段路径
- [ ] `CliCoverageChecker.cs` 存在
- [ ] `ConfigFieldChecker.cs` 存在
- [ ] `FileRefChecker.cs` 存在
- [ ] `ExampleParserChecker.cs` 存在
- [ ] `SkillCliChecker.cs` 存在
- [ ] `DocsCheckCommand.cs` 存在且组装所有 checker
- [ ] `Program.cs` 中注册了 `docs` 命令路由

## Build
- [ ] `dotnet build src/Bukit.Cli -c Release` 零警告通过
- [ ] `dotnet build bukit.slnx -c Release` 零警告通过

## Functional Verification
- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check` 正常执行并输出结果
- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check --cli` 仅执行 CLI 覆盖率检查
- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check --config-fields` 仅执行字段检查
- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check --file-refs` 仅执行文件引用检查
- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check --examples` 仅执行示例检查
- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check --skills` 仅执行 Skill 一致性检查
- [ ] 所有 flag 可组合使用（如 `--cli --config-fields`）
- [ ] 无 flag 时默认运行全部 5 类检查

## Detection Accuracy
- [ ] 能检测出 `data inspect` 作为幽灵命令（CLI 存在但文档无覆盖）
- [ ] 能检测出 `visual generate` 作为幽灵命令
- [ ] 能检测出 `completion` 作为幽灵命令
- [ ] 能检测出 `lint` 作为幽灵命令
- [ ] 能检测出 `site.analytics.*` 字段仅在 README 覆盖
- [ ] 能检测出文档中不存在的文件路径引用
- [ ] 不会误报文档中合法的文件路径引用

## Exit Code Behavior
- [ ] 无 ERROR 时 exit code = 0
- [ ] 有 ERROR 时 exit code = 1
- [ ] 仅有 WARN 时 exit code = 0

## Regression
- [ ] `dotnet test tests/Bukit.Cli.Tests -c Release` 全部通过
- [ ] `dotnet test tests/Bukit.Engine.Tests -c Release` 全部通过

## Documentation
- [ ] `governance-checklist.md` 中引用已更新为 `bukit docs check`
