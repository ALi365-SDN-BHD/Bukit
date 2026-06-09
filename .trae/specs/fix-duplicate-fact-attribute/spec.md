# Fix Duplicate Fact Attribute Spec

## Why
`RouteGeneratorTests.cs` 第 10 行和第 12 行有重复的 `[Fact]` 属性，导致 `dotnet build` 报错 CS0579，构建失败。

## What Changes
- 删除 `tests/Bukit.Routing.Tests/RouteGeneratorTests.cs` 第 10 行多余的 `[Fact]` 属性

## Impact
- Affected specs: none
- Affected code: `tests/Bukit.Routing.Tests/RouteGeneratorTests.cs`

## MODIFIED Requirements
### Requirement: RouteGeneratorTests 编译通过
`RouteGeneratorTests.cs` 的 `Generate_WithCollectionPermalink` 方法只有一个 `[Fact]` 属性。

#### Scenario: 编译成功
- **WHEN** 运行 `dotnet build bukit.slnx -c Release`
- **THEN** 构建成功，无 CS0579 错误
