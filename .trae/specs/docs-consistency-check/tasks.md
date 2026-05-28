# Tasks

## Implementation Order
All tasks are in dependency order. Tasks marked [P] can run in parallel.

- [x] Task 1: 创建 DocsIssue 模型和 Markdown 文档扫描器
  - [x] 1.1 创建 `src/Bukit.Cli/Commands/DocsCheck/DocsIssue.cs` — 统一 issue 模型（FilePath, Line, Severity, CheckType, Message）
  - [x] 1.2 创建 `src/Bukit.Cli/Commands/DocsCheck/DocFileScanner.cs` — 扫描 README*.md、guide/**/*.md、src/skills/**/SKILL.md，返回文件列表
  - **验证**: `dotnet build src/Bukit.Cli -c Release` 通过 ✅

- [x] Task 2: 实现 CLI 命令路径提取器 [P]
  - [x] 2.1 创建 `src/Bukit.Cli/Commands/DocsCheck/CommandPathExtractor.cs`
  - [x] 2.2 实现 `ExtractAllCommandPaths(CliCommandRegistry)` — 递归遍历所有 `CliCommandSpec` 及其 `Subcommands`，生成完整命令路径
  - [x] 2.3 实现 `ExtractCommandOptions(CliCommandSpec)` / `ResolveSpec`
  - **验证**: 构建通过，能正确提取所有命令路径 ✅

- [x] Task 3: 实现 CLI 命令覆盖率检查 (P0) [P]
  - [x] 3.1 创建 `src/Bukit.Cli/Commands/DocsCheck/CliCoverageChecker.cs`
  - [x] 3.2 从文档文本中用正则提取 `bukit <cmd>` 引用模式，过滤非命令文本
  - [x] 3.3 交叉验证：文档引用 vs canonical 命令列表
  - [x] 3.4 反向验证：canonical 命令中哪些没有文档覆盖
  - **验证**: 检测出 `data inspect/dump`, `completion`, `lint` 等幽灵命令 ✅

- [x] Task 4: 实现 site.yaml 字段提取器 [P]
  - [x] 4.1 创建 `src/Bukit.Cli/Commands/DocsCheck/ConfigFieldExtractor.cs`
  - [x] 4.2 实现 `ExtractAllConfigPaths(Type)` — 反射 `AppConfig` 及嵌套 record，递归生成 YAML 路径
  - [x] 4.3 从文档文本中用正则提取 YAML 路径引用，过滤只保留已知顶级键
  - **验证**: 构建通过，AOT 兼容 ✅

- [x] Task 5: 实现 site.yaml 字段检查 [P]
  - [x] 5.1 创建 `src/Bukit.Cli/Commands/DocsCheck/ConfigFieldChecker.cs`
  - [x] 5.2 交叉验证：文档引用 vs canonical 字段列表
  - [x] 5.3 反向验证：canonical 字段中哪些没有文档覆盖
  - **验证**: 能检测出文档中不存在的字段引用 ✅

- [x] Task 6: 实现文件引用检查 [P]
  - [x] 6.1 创建 `src/Bukit.Cli/Commands/DocsCheck/FileRefChecker.cs`
  - [x] 6.2 从文档中提取文件路径引用
  - [x] 6.3 验证每个引用路径在 repo 中真实存在
  - **验证**: 能检测出不存在的文件路径引用 ✅

- [x] Task 7: 实现 README 示例可解析性检查 [P]
  - [x] 7.1 创建 `src/Bukit.Cli/Commands/DocsCheck/ExampleParserChecker.cs`
  - [x] 7.2 从 README 代码块中提取 `bukit` 命令行
  - [x] 7.3 用 `CliParser.Parse(registry, args)` 做 dry-run 验证
  - **验证**: 检测出 README 示例中的无效选项 ✅

- [x] Task 8: 实现 Skill-CLI 一致性检查 [P]
  - [x] 8.1 创建 `src/Bukit.Cli/Commands/DocsCheck/SkillCliChecker.cs`
  - [x] 8.2 读取 `src/skills/bukit-cli-reference/SKILL.md` 提取所有 CLI 命令引用
  - [x] 8.3 读取其他 SKILL.md 提取 CLI 命令引用
  - [x] 8.4 交叉验证：非 cli-reference skill 引用的命令是否在 cli-reference 中有定义
  - **验证**: 检测出版本号 `2.x.x` 漂移及多个不一致 ✅

- [x] Task 9: 组装 DocsCheckCommand 命令入口
  - [x] 9.1 创建 `src/Bukit.Cli/Commands/DocsCheck/DocsCheckCommand.cs`
  - [x] 9.2 实现 flag 解析（`--cli`, `--config-fields`, `--file-refs`, `--examples`, `--skills`）
  - [x] 9.3 默认全部运行
  - [x] 9.4 实现输出格式化（file:line + severity + message）
  - [x] 9.5 实现 exit code 逻辑（有 ERROR → 1，仅有 WARN → 0）
  - **验证**: `dotnet build src/Bukit.Cli -c Release` 通过 ✅

- [x] Task 10: 注册命令到 Program.cs
  - [x] 10.1 在 `Program.cs` 的 legacy switch 中添加 `"docs"` case
  - [x] 10.2 在 `BukitCliSpecs` 中添加 docs 命令定义（含 help 输出）
  - **验证**: `dotnet run -- docs check --help` 输出帮助信息 ✅

- [x] Task 11: 端到端验证
  - [x] 11.1 Release build: `dotnet build bukit.slnx -c Release` ✅ (0 warnings, 0 errors)
  - [x] 11.2 运行 `bukit docs check` 所有检查 ✅
  - [x] 11.3 运行各独立 flag ✅ (--cli, --config-fields, --file-refs, --examples, --skills)
  - [x] 11.4 确认 exit code 行为正确 ✅ (ERROR→1, WARN→0)
  - [x] 11.5 现有测试通过: CLI tests 736/739 passed (3 pre-existing failures) ✅

- [x] Task 12: 更新 governance-checklist.md
  - [x] 12.1 将 `pwsh ./scripts/check-doc-asset-consistency.ps1` 替换为 `bukit docs check`
  - **验证**: 文档可读 ✅

# Task Dependencies
- Task 2, 3, 4 依赖 Task 1（需要 DocsIssue 模型和扫描器）
- Task 5 依赖 Task 4（需要 ConfigFieldExtractor）
- Task 9 依赖 Task 2-8（所有 checker 就绪后组装）
- Task 10 依赖 Task 9
- Task 11 依赖 Task 10
- Task 12 无依赖，可最后做
- [P] 标记的任务可并行执行
