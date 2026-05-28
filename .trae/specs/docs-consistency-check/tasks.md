# Tasks

## Implementation Order
All tasks are in dependency order. Tasks marked [P] can run in parallel.

- [ ] Task 1: 创建 DocsIssue 模型和 Markdown 文档扫描器
  - [ ] 1.1 创建 `src/Bukit.Cli/Commands/DocsCheck/DocsIssue.cs` — 统一 issue 模型（FilePath, Line, Severity, CheckType, Message）
  - [ ] 1.2 创建 `src/Bukit.Cli/Commands/DocsCheck/DocFileScanner.cs` — 扫描 README*.md、guide/**/*.md、src/skills/**/SKILL.md，返回文件列表
  - **验证**: `dotnet build src/Bukit.Cli -c Release` 通过

- [ ] Task 2: 实现 CLI 命令路径提取器 [P]
  - [ ] 2.1 创建 `src/Bukit.Cli/Commands/DocsCheck/CommandPathExtractor.cs`
  - [ ] 2.2 实现 `ExtractAllCommandPaths(CliCommandRegistry)` — 递归遍历所有 `CliCommandSpec` 及其 `Subcommands`，生成完整命令路径（如 `"theme create"`, `"seo audit"`）
  - [ ] 2.3 实现 `ExtractCommandOptions(CliCommandSpec)` — 提取每个命令的选项列表
  - **验证**: 单元测试验证提取的命令数 >= 实际 CLI 命令数

- [ ] Task 3: 实现 CLI 命令覆盖率检查 (P0) [P]
  - [ ] 3.1 创建 `src/Bukit.Cli/Commands/DocsCheck/CliCoverageChecker.cs`
  - [ ] 3.2 从文档文本中用正则提取 `bukit <cmd>` 引用模式
  - [ ] 3.3 交叉验证：文档引用 vs canonical 命令列表
  - [ ] 3.4 反向验证：canonical 命令中哪些没有文档覆盖
  - **验证**: 对当前 repo 运行，应检测出 `data inspect`, `visual generate`, `completion`, `lint` 缺失

- [ ] Task 4: 实现 site.yaml 字段提取器 [P]
  - [ ] 4.1 创建 `src/Bukit.Cli/Commands/DocsCheck/ConfigFieldExtractor.cs`
  - [ ] 4.2 实现 `ExtractAllConfigPaths(Type)` — 反射 `AppConfig` 及嵌套 record，递归生成 YAML 路径（PascalCase → snake_case）
  - [ ] 4.3 从文档文本中用正则提取 `site.*`、`content.*`、`build.*` 等 YAML 路径引用
  - **验证**: 单元测试验证提取的字段数与 AppConfig 属性数一致

- [ ] Task 5: 实现 site.yaml 字段检查 [P]
  - [ ] 5.1 创建 `src/Bukit.Cli/Commands/DocsCheck/ConfigFieldChecker.cs`
  - [ ] 5.2 交叉验证：文档引用 vs canonical 字段列表
  - [ ] 5.3 反向验证：canonical 字段中哪些没有文档覆盖
  - **验证**: 对当前 repo 运行，应检测出 `site.analytics.*` 只在 README 有覆盖

- [ ] Task 6: 实现文件引用检查 [P]
  - [ ] 6.1 创建 `src/Bukit.Cli/Commands/DocsCheck/FileRefChecker.cs`
  - [ ] 6.2 从文档中提取文件路径引用（如 `src/Bukit.Cli/Commands/BuildCommand.cs`）
  - [ ] 6.3 验证每个引用路径在 repo 中真实存在
  - **验证**: 对当前 repo 运行，应继承原 `check-doc-asset-consistency` 的检测能力

- [ ] Task 7: 实现 README 示例可解析性检查 [P]
  - [ ] 7.1 创建 `src/Bukit.Cli/Commands/DocsCheck/ExampleParserChecker.cs`
  - [ ] 7.2 从 README 代码块中提取 `bukit` 命令行
  - [ ] 7.3 用 `CliParser.Parse(registry, args)` 做 dry-run 验证
  - **验证**: 对当前 README.md 示例运行，应全部通过或报告具体错误

- [ ] Task 8: 实现 Skill-CLI 一致性检查 [P]
  - [ ] 8.1 创建 `src/Bukit.Cli/Commands/DocsCheck/SkillCliChecker.cs`
  - [ ] 8.2 读取 `src/skills/bukit-cli-reference/SKILL.md` 提取所有 CLI 命令引用
  - [ ] 8.3 读取其他 SKILL.md 提取 CLI 命令引用
  - [ ] 8.4 交叉验证：非 cli-reference skill 引用的命令是否在 cli-reference 中有定义
  - **验证**: 对当前 skills 运行，应检测出版本号 `2.x.x` 等不一致

- [ ] Task 9: 组装 DocsCheckCommand 命令入口
  - [ ] 9.1 创建 `src/Bukit.Cli/Commands/DocsCheckCommand.cs`
  - [ ] 9.2 实现 flag 解析（`--cli`, `--config-fields`, `--file-refs`, `--examples`, `--skills`）
  - [ ] 9.3 默认全部运行
  - [ ] 9.4 实现输出格式化（file:line + severity + message）
  - [ ] 9.5 实现 exit code 逻辑（有 ERROR → 1，仅有 WARN → 0）
  - **验证**: `dotnet build src/Bukit.Cli -c Release` 通过

- [ ] Task 10: 注册命令到 Program.cs
  - [ ] 10.1 在 `Program.cs` 的 legacy switch 中添加 `"docs"` case，路由到 `DocsCheckCommand.RunAsync`
  - [ ] 10.2 在 `HelpPrinter` 中添加 docs check 帮助信息
  - **验证**: `dotnet run --project src/Bukit.Cli -c Release -- docs check --help` 输出帮助信息

- [ ] Task 11: 端到端验证
  - [ ] 11.1 Release build: `dotnet build bukit.slnx -c Release`
  - [ ] 11.2 运行 `dotnet run --project src/Bukit.Cli -c Release -- docs check` 检查实际输出
  - [ ] 11.3 运行 `dotnet run --project src/Bukit.Cli -c Release -- docs check --cli` 验证 CLI 检查
  - [ ] 11.4 确认 exit code 行为正确（有发现时非 0）
  - [ ] 11.5 所有现有测试通过: `dotnet test tests/Bukit.Cli.Tests -c Release`

- [ ] Task 12: 更新 governance-checklist.md
  - [ ] 12.1 将 `pwsh ./scripts/check-doc-asset-consistency.ps1` 替换为 `bukit docs check`
  - **验证**: 文档可读

# Task Dependencies
- Task 2, 3, 4 依赖 Task 1（需要 DocsIssue 模型和扫描器）
- Task 5 依赖 Task 4（需要 ConfigFieldExtractor）
- Task 9 依赖 Task 2-8（所有 checker 就绪后组装）
- Task 10 依赖 Task 9
- Task 11 依赖 Task 10
- Task 12 无依赖，可最后做
- [P] 标记的任务可并行执行
