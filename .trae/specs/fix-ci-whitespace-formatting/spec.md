# Fix CI Whitespace Formatting Spec

## Why
CI 构建在 `dotnet format whitespace` 检查阶段失败（exit code 2），共 13 处 WHITESPACE 错误分布在 3 个文件中。所有 3132 个测试全部通过（0 failed），问题仅限于代码格式化。

## What Changes
- 修复 `DiagnosticCode.cs` 第 15-22 行的 Route 枚举值对齐空格（删除多余空格）
- 修复 `RenderDependencyHasherTests.cs` 第 243/263/277 行的长行对象初始化器换行
- 修复 `ProtocolEchoPlugin/Program.cs` 第 73-74 行的长行换行

## Impact
- Affected specs: 无（纯格式化修复）
- Affected code: `src/Bukit.Shared/DiagnosticCode.cs`, `tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs`, `tests/ProtocolEchoPlugin/Program.cs`

## Problem Analysis

### 根因：本地开发环境与 CI 的 dotnet format 规则冲突

| 文件 | 问题类型 | 详情 |
|------|---------|------|
| `DiagnosticCode.cs:15-22` | 多余空格 | Route 枚举值使用了额外空格对齐 `=` 号（如 `RouteConflict              = 0x0201`），与 editorconfig 的 `indent_size=4` 规则冲突 |
| `RenderDependencyHasherTests.cs:243,263,277` | 长行未换行 | `new CollectionConfig { ... }` 对象初始化器在一行内包含多个属性，需要按 `\n\s\s\s\s...` 格式换行 |
| `ProtocolEchoPlugin/Program.cs:73-74` | 长行未换行 | 链式 `??` 表达式跨越两行，对齐空格不符合规则 |

### 修复原则

所有修复完全遵循 `.editorconfig` 规则：
- `indent_style = space`, `indent_size = 4`
- `end_of_line = lf`
- `trim_trailing_whitespace = true`

### 验证方式

修复后在本地运行 `dotnet format whitespace --verify-no-changes` 应返回 exit 0。

## MODIFIED Requirements

### Requirement: CI Build Must Pass
CI 的 `dotnet format whitespace` 检查 SHALL 对所有 .cs 文件返回 0 错误。

#### Scenario: All whitespace errors resolved
- **WHEN** 在 CI 环境运行 `dotnet format whitespace --verify-no-changes`
- **THEN** 所有 3 个文件的 13 处 WHITESPACE 错误均已被修复，exit code 为 0
