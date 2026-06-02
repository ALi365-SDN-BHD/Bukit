# Tasks

- [x] Task 1: `--strict` 支持 `fail|warn` 模式
  - [x] BukitCliSpecs: `--strict` 改为 CliOptionType.String (ValueName: "mode")
  - [x] ImportModels: `bool Strict` → `string? StrictMode`（null=未设置, "fail"/"warn"）
  - [x] ImportCommand: 解析 strict 值，flag 无值→"fail"，有值→"fail"或"warn"
  - [x] HtmlDemoImporter: strict=warn 时调用 diagnostics 报告但不 throw
  - [x] 现有 `--strict` 测试适配新字段类型

- [x] Task 2: ImportReportWriter 增加 "Seed Push Scope" 章节
  - [x] notion 模式下的报告增加新章节
  - [x] 列出 pages/posts/companies/services → default Notion push
  - [x] 列出 sections/faqs/media/components → generated for review only

- [x] Task 3: 运行全量测试验证
  - [x] `dotnet build` 0 errors
  - [x] `dotnet test` 全部通过 (3,323 passed)

# Task Dependencies
- Task 1, Task 2 独立
- Task 3 depends on Task 1, Task 2
