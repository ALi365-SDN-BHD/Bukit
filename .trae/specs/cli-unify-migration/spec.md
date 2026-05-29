# CLI 统一迁移 Spec

## Why
Bukit 的 CLI 解析层当前存在新旧并存：21 个命令中有 14 个仅使用旧版 ArgReader，7 个同时拥有 ArgReader 和 CliBoundCommand 两套入口。四种不同的适配器模式造成代码重复，且子命令命令（theme/template/seo 等）无法走通新的类型安全解析路径。本次迁移分两步：统一适配器为一个公共类，让 CliParser 支持子命令递归解析，最终使所有命令都能走新路径。

## What Changes
- 新增 `CliBoundCommandFactory` 公共类，统一 ArgReader → CliBoundCommand 转换
- 替换所有 8 个命令中的四套适配器模式为统一调用 **BREAKING**（仅内部重构，公开 API 不变）
- 扩展 `CliParseResult` 支持子命令嵌套结构
- 扩展 `CliParser.Parse` 支持子命令递归解析
- 扩展 `CliCommandRegistry` 支持子命令查找
- 修改 `Program.cs` 的新路径 dispatch，使其能处理带子命令的 spec
- 删除不再需要的 `ArgReader` 类型参数（ConfigPathResolver）

## Impact
- Affected specs: 无已有 spec
- Affected code: `CliBoundCommandFactory`(新), `CliParser`, `CliParseResult`, `CliCommandRegistry`, `Program.cs`, `BuildCommand.cs`, `CloneCommand.cs`, `CloneCommandOptions.cs`, `DataCommand.cs`, `DeployCommand.cs`, `DevCommand.cs`, `DoctorCommand.cs`, `LintCommand.cs`, `PreviewCommand.cs`, `ConfigPathResolver.cs`

## ADDED Requirements

### Requirement 1: CliBoundCommandFactory 统一适配器
系统 SHALL 提供一个 `CliBoundCommandFactory` 类，暴露一个 `Create(ArgReader, CliCommandSpec?)` 静态方法，根据 spec 中定义的 Options 列表自动从 ArgReader 提取对应的选项值并构建 CliBoundCommand。

#### Scenario: 从 spec 驱动的自动映射
- **WHEN** 调用 `CliBoundCommandFactory.Create(reader, buildSpec)` 且 ArgReader 包含 `--config path/to/site.yaml --clean`
- **THEN** 返回的 CliBoundCommand 的 `GetString("--config")` 返回 `"path/to/site.yaml"`，`GetBool("--clean")` 返回 `true`

#### Scenario: Flag 类型选项自动检测
- **WHEN** spec 中某选项的 `Type == CliOptionType.Flag`
- **THEN** 工厂自动使用 `reader.HasFlag(optionName)` 而不是 `reader.GetOption(optionName)` 来解析

#### Scenario: 未在 spec 中声明的选项被跳过
- **WHEN** ArgReader 包含 `--unknown-flag` 但 spec 中无此选项
- **THEN** 该选项不出现在生成的 CliBoundCommand 中

#### Scenario: null spec 回退到全量提取
- **WHEN** 调用 `CliBoundCommandFactory.Create(reader, null)` (spec 为 null)
- **THEN** 工厂提取 ArgReader 中所有可识别的选项（向后兼容无 spec 的场景）

### Requirement 2: 适配器模式统一替换
系统 SHALL 将所有 8 个命令中各自的 ArgReader→CliBoundCommand 适配器替换为对 CliBoundCommandFactory 的统一调用，消除四套重复模式。

#### Scenario: BuildCommand 内联字典被替换
- **WHEN** BuildCommand.RunAsync(ArgReader) 被调用
- **THEN** 其内部使用 `CliBoundCommandFactory.Create(reader, buildSpec)` 替代当前的内联字典构造

#### Scenario: CloneCommand 外部 Builder 被替换
- **WHEN** CloneCommand.RunAsync(ArgReader) 被调用
- **THEN** 使用 `CliBoundCommandFactory.Create(reader, cloneSpec)` 替代 CloneCommandOptions.BuildCommand
- **AND** CloneCommandOptions.BuildCommand(ArgReader) 方法和 CloneCommand.RunAsync(CliBoundCommand, ArgReader) 重载被删除

#### Scenario: LintCommand/DataCommand ParseOptions 方法被替换
- **WHEN** LintCommand.RunAsync(ArgReader) 被调用
- **THEN** 使用 `CliBoundCommandFactory.Create(reader, spec)` 替代 LintCommand.ParseOptions(reader)

#### Scenario: DevCommand 手动 switch 保持现状
- **WHEN** DevCommand 同时保留 `RunAsync(CliBoundCommand)` 和 `RunAsync(string[])` 重载
- **THEN** `RunAsync(string[])` 不被 CliBoundCommandFactory 替换（因为它不接收 ArgReader，是特殊情况，后续再处理）

### Requirement 3: CliParser 子命令递归解析
系统 SHALL 扩展 `CliParser.Parse` 方法以支持子命令。当解析过程中遇到匹配子命令名的位置参数时，解析器应递归进入子命令的 spec 进行后续参数解析。

#### Scenario: 带子命令的解析
- **WHEN** `CliParser.Parse(themeSpec, ["create", "--name", "my-theme"])` 被调用且 themeSpec 包含名为 "create" 的子命令
- **THEN** 返回的 CliParseResult 类型为 `SubcommandParseResult`，其 `SubcommandName` 为 `"create"`，`InnerResult` 为子命令的解析结果，其中 `GetString("--name")` 返回 `"my-theme"`

#### Scenario: 无子命令的命令走原路径
- **WHEN** 命令 spec 没有子命令或位置参数不匹配任何子命令名
- **THEN** 返回的 CliParseResult 类型为 `SimpleParseResult`（保持当前行为）

#### Scenario: 子命令帮助 (–help)
- **WHEN** args 中包含 `--help` 或 `-h` 且位于子命令之后
- **THEN** 解析结果标记为帮助请求，绑定到最深层匹配的子命令 spec

### Requirement 4: CliCommandRegistry 子命令查找
系统 SHALL 扩展 `CliCommandRegistry` 添加 `ResolveSubcommand(CliCommandSpec parent, string subName)` 方法，从父命令的 Subcommands 列表中按名称查找子命令 spec。

#### Scenario: 子命令查找成功
- **WHEN** `registry.ResolveSubcommand(themeSpec, "create")` 被调用
- **THEN** 返回 themeSpec.Subcommands 中 Name 为 "create" 的 CliCommandSpec

#### Scenario: 子命令不匹配
- **WHEN** 子命令名不匹配任何子命令
- **THEN** 返回 null

### Requirement 5: Program.cs 调度统一
系统 SHALL 修改 Program.cs 使子命令路径也走 CliParser 解析，消除新路径对 `spec.Subcommands is null or { Count: 0 }` 的限制。

#### Scenario: 子命令命令走新路径
- **WHEN** 用户执行 `bukit theme create --name my-theme`
- **THEN** Program.cs 走新路径 dispatch：CliParser.Parse 递归解析子命令 → CliParseResult → dispatch 到 ThemeCommand

#### Scenario: 旧路径回退保留
- **WHEN** 新路径无法匹配命令或解析失败
- **THEN** 回退到旧路径 ArgReader 方式 dispatch（保持兼容性）

## MODIFIED Requirements
无。

## REMOVED Requirements
### Requirement: CloneCommandOptions.BuildCommand
**Reason**: 被 CliBoundCommandFactory 统一替代
**Migration**: CloneCommand.RunAsync(ArgReader) 改用 CliBoundCommandFactory.Create(reader, cloneSpec)

### Requirement: CloneCommand.RunAsync(CliBoundCommand, ArgReader) 双参重载
**Reason**: ArgReader 参数仅用于 ThemeCommand.SetThemeAsync，改为从命令参数中提取
**Migration**: 内部逻辑重构为仅传 CliBoundCommand，不再透传 ArgReader
