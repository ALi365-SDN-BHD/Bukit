# --strict warn mode + Seed Push Scope Declaration Spec

## Why
1. `--strict` 当前无阈值控制，真实 demo 导入时 template residues 可能导致误杀（如按钮文案、企业 slogan）。需要支持 `fail|warn` 模式。
2. sections/faqs/media/components 的 seed 文件已生成，但报告未明确标注其 scope（审查用 vs 推送用），容易误导用户认为这些内容已进入 Notion。

其他问题（P1-1 build-source+content-source 组合校验、P1-2 多数据库 site.yaml、P2-2 友好错误提示）已完成修复。

## What Changes
- `--strict` 改为接受可选值 `fail|warn`，默认 `fail`
- `HtmlDemoImportOptions.Strict` 字段类型从 `bool` 改为 `string? StrictMode`
- strict=warn 时不调用 `ThrowIfStrictDiagnostics`，仅输出 warning
- ImportReportWriter 在 notion 模式下增加 "Seed Push Scope" 章节

## Impact
- Affected specs: import-html-demo
- Affected code: [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs), [ImportCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs), [ImportModels.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ImportModels.cs), [HtmlDemoImporter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs), [ImportReportWriter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ImportReportWriter.cs)

## MODIFIED Requirements

### Requirement: --strict SHALL support fail|warn mode
The `--strict` option SHALL accept an optional string value: `fail` (default when flag present) or `warn`.

#### Scenario: --strict with warn mode
- **GIVEN** `--strict warn` and template residues are detected
- **WHEN** import runs
- **THEN** residues ARE reported as warnings
- **AND** import succeeds (exit 0)

#### Scenario: --strict with fail mode (default when no value)
- **GIVEN** `--strict` (flag present, no value)
- **WHEN** template residues are detected
- **THEN** import fails (existing behavior preserved)

#### Scenario: --strict not present
- **GIVEN** no `--strict` flag
- **WHEN** template residues are detected
- **THEN** residues ARE reported but import succeeds (existing behavior preserved)

## ADDED Requirements

### Requirement: Import report SHALL declare seed push scope
The import report in notion mode SHALL include a "Seed Push Scope" section listing which collections are pushed and which are for review only.

#### Scenario: notion mode report
- **GIVEN** `--content-source notion`
- **WHEN** import report is generated
- **THEN** a "Seed Push Scope" section SHALL list:
  - pages/posts/companies/services → default Notion push
  - sections/faqs/media/components → generated for review only
