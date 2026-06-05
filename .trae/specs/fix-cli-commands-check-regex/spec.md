# Fix CLI Commands Check Regex Spec

## Why
`scripts/quality-gate.sh` 在 Check 12 (CLI commands consistency) 中误报 `scope` 和 `theme scope` 为未文档化的 CLI 命令，导致 CI 失败。实际原因是 `check-cli-commands.py` 的正则表达式错误匹配了 `CliOptionSpec` 中的 `ValueName: "scope"`。

## What Changes
- 修复 `src/skills/scripts/check-cli-commands.py` 中的正则表达式，排除 `ValueName:` 匹配
- 同样修复主题命令解析部分的相同正则

## Impact
- Affected specs: 无（bug fix）
- Affected code: `src/skills/scripts/check-cli-commands.py`（两处正则修改）

## MODIFIED Requirements

### Requirement: CLI Command Name Extraction
The system SHALL extract command names from `CliCommandSpec` declarations only, not from `CliOptionSpec` declarations.

#### Scenario: ValueName in CliOptionSpec is ignored
- **WHEN** source code contains `CliOptionSpec("--template", ..., ValueName: "scope")`
- **THEN** "scope" is NOT extracted as a command name

#### Scenario: Name in CliCommandSpec is extracted
- **WHEN** source code contains `new CliCommandSpec(Name: "build", ...)`
- **THEN** "build" IS extracted as a command name

#### Scenario: Subcommand Name is extracted
- **WHEN** source code contains `Name: "check"` inside a `Subcommands:` block
- **THEN** "check" IS extracted as a subcommand of the parent command
