# DevCommand 完成 CLI 统一迁移 Spec

## Why
DevCommand 是 21 个命令中唯一一个旧路径不接收 `ArgReader` 而是直接接收 `string[]` 的命令，内部仍保留手动 `for+switch` 解析。CLI 统一迁移的基础设施（`CliBoundCommandFactory`、`CliParser` 子命令支持）已就绪，DevCommand 只需三步便可消除这一残存特例，使所有命令的 ArgReader→CliBoundCommand 适配模式完全一致。

## What Changes
- DevCommand 新增 `RunAsync(ArgReader)` 方法，由 `CliBoundCommandFactory` 驱动 **BREAKING**（仅内部重构，CLI 行为不变）
- 删除 `RunAsync(string[])` 方法及其内部手动 `for+switch` 解析（共 22 行死代码）
- Program.cs 旧路径 DevCommand 分支对齐其他命令：`args[1..]` → `reader`

## Impact
- Affected specs: `cli-unify-migration`（已完成，这是该迁移的最后一块拼图）
- Affected code: `DevCommand.cs`、`Program.cs`

## ADDED Requirements

### Requirement 1: DevCommand.RunAsync(ArgReader)
DevCommand SHALL 提供 `RunAsync(ArgReader reader)` 方法，其行为完全等价于被删除的 `RunAsync(string[])`。

#### Scenario: 从 ArgReader 构建 CliBoundCommand
- **WHEN** `DevCommand.RunAsync(ArgReader)` 被调用且 reader 包含 `--port 3000 --no-watch`
- **THEN** 内部通过 `CliBoundCommandFactory.Create(reader, devSpec)` 构建 CliBoundCommand，`GetInt("--port")` 返回 3000，`GetBool("--no-watch")` 返回 true
- **AND** 委托给 `RunAsync(CliBoundCommand)` 完成执行

#### Scenario: 默认参数保持一致
- **WHEN** 不传 `--host` 和 `--port`
- **THEN** `ExtractOptions` 返回的 host 仍为 `"localhost"`，port 仍为 `35729`（由 ExtractOptions 内部处理，不依赖 ArgReader 或 CliBoundCommand 提供默认值）

## REMOVED Requirements

### Requirement: DevCommand.RunAsync(string[]) 手动 switch 解析
**Reason**: `CliBoundCommandFactory` 和 `BukitCliSpecs` 已提供等价的类型安全解析
**Migration**: 原参数 `--config`/`--site`/`--host`/`--port`/`--output`/`--no-watch` 由 `CliBoundCommandFactory.Create(reader, spec)` 自动提取，spec 已在 `BukitCliSpecs` 中定义为 `dev`

### Requirement: Program.cs 旧路径直接传 string[] 给 DevCommand
**Reason**: 这是 21 个命令中唯一绕过 ArgReader 的特例
**Migration**: `"dev" => await DevCommand.RunAsync(args[1..])` 改为 `"dev" => await DevCommand.RunAsync(reader)`
