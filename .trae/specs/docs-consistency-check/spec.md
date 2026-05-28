# Docs Consistency Check Spec

## Why
README、guide/dev、src/skills 三套知识源独立维护，CLI 命令变更时容易出现描述不一致。目前存在 4 个"幽灵命令"在所有文档中缺失，多处在 CLI 命令覆盖率、site.yaml 字段、版本号上漂移。现有 `scripts/check-doc-asset-consistency` 脚本仅检查 5 个硬编码路径，覆盖范围极窄。需要一个以 canonical source（代码）为准的自动化 docs consistency check 命令。

## What Changes
- 新增 `bukit docs check` CLI 命令，含 5 个可选子 flag
- 直接复用 `BukitCliSpecs.CreateRegistry()` 作为 CLI 命令的 canonical source
- 直接反射 `AppConfig` 作为 site.yaml 字段的 canonical source
- 直接复用 `CliParser.Parse()` 验证文档示例可解析性
- 替代现有 `scripts/check-doc-asset-consistency.*` 脚本

## Impact
- Affected specs: 无（全新功能）
- Affected code: `src/Bukit.Cli/Commands/DocsCheckCommand.cs`（新建），`src/Bukit.Cli/Program.cs`（注册路由），`src/Bukit.Cli/Cli/BukitCliSpecs.cs`（可选注册元数据）
- Affected scripts: `scripts/check-doc-asset-consistency.ps1`, `scripts/check-doc-asset-consistency.sh`（后续可废弃）
- Affected docs: `guide/dev/governance-checklist.md`（更新引用）

## ADDED Requirements

### Requirement: Docs Check CLI Command
The system SHALL provide a `bukit docs check` command that runs documentation consistency checks against canonical sources in the codebase.

#### Scenario: Run all checks by default
- **WHEN** user runs `bukit docs check` without flags
- **THEN** all 5 check types execute and a summary of issues is printed

#### Scenario: Run specific check types
- **WHEN** user runs `bukit docs check --cli --config-fields`
- **THEN** only CLI command coverage and config field checks execute

#### Scenario: No issues found
- **WHEN** all checks pass with zero issues
- **THEN** exit code is 0 and "OK" message is printed

#### Scenario: Issues found
- **WHEN** one or more checks produce ERROR-level issues
- **THEN** exit code is 1 and each issue is printed with file:line location

### Requirement: CLI Command Coverage Check (--cli)
The system SHALL verify that CLI commands referenced in documentation exist in the canonical CLI registry and that no CLI commands lack documentation coverage.

#### Scenario: Document references unknown command
- **WHEN** a document references `bukit foo` but `foo` is not in `BukitCliSpecs`
- **THEN** an ERROR is reported with file path and line number

#### Scenario: CLI command lacks documentation coverage
- **WHEN** a command exists in `BukitCliSpecs` but is not mentioned in any README, guide/dev, or skills file
- **THEN** a WARN is reported listing the uncovered command

#### Scenario: CLI command mentioned with wrong options
- **WHEN** a document shows a command with options not defined in `BukitCliSpecs`
- **THEN** a WARN is reported

### Requirement: site.yaml Field Check (--config-fields)
The system SHALL verify that site.yaml field paths referenced in documentation exist in the canonical AppConfig model.

#### Scenario: Document references non-existent field
- **WHEN** a document mentions `site.fooBar` but no such property exists in `AppConfig`
- **THEN** an ERROR is reported

#### Scenario: Config field lacks documentation coverage
- **WHEN** a field exists in `AppConfig` but is not referenced in any documentation
- **THEN** a WARN is reported

### Requirement: File Reference Check (--file-refs)
The system SHALL verify that file paths referenced in documentation exist in the repository.

#### Scenario: Document references non-existent file
- **WHEN** a document mentions `src/Foo/Bar.cs` but the file does not exist
- **THEN** an ERROR is reported

### Requirement: README Example Check (--examples)
The system SHALL verify that command examples in README files can be successfully parsed by the CLI parser.

#### Scenario: Example parses successfully
- **WHEN** a README code block contains `bukit build --clean` and `CliParser.Parse()` succeeds
- **THEN** no issue is reported

#### Scenario: Example fails to parse
- **WHEN** a README code block contains a command with invalid options and `CliParser.Parse()` returns diagnostics
- **THEN** an ERROR is reported with the parse failure details

### Requirement: Skill-CLI Consistency Check (--skills)
The system SHALL verify that CLI commands referenced in skill SKILL.md files are consistent with the bukit-cli-reference skill.

#### Scenario: Skill references command not in cli-reference
- **WHEN** a skill outside bukit-cli-reference mentions `bukit foo` but `foo` is not documented in bukit-cli-reference/SKILL.md
- **THEN** a WARN is reported

### Requirement: Canonical Source Extraction
The system SHALL extract canonical data without maintaining duplicate definitions.

#### Scenario: CLI commands extracted from registry
- **WHEN** the check runs
- **THEN** all command paths are derived from `BukitCliSpecs.CreateRegistry()` by recursively traversing `CliCommandSpec` and its `Subcommands`

#### Scenario: Config fields extracted from AppConfig
- **WHEN** the config field check runs
- **THEN** all YAML field paths are derived by reflecting over `AppConfig` and its nested record types, converting PascalCase property names to snake_case YAML keys
