# Tasks

- [x] Task 1: 删除 `BuildOutputPathFromUrl` 中冗余的路径分隔符替换，修复测试中的预期值
  - [x] 1.1 删除 `RoutePathBuilder.cs` 第 54 行 `outputPath = outputPath.Replace('/', Path.DirectorySeparatorChar);`
  - [x] 1.2 将 `RoutePathBuilderTests.cs` 中两处 `expected.Replace('/', System.IO.Path.DirectorySeparatorChar)` 改为直接使用 `expected`
  - [x] 1.3 运行 `dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Release` 验证全部 23 个测试通过

# Task Dependencies
- 无
