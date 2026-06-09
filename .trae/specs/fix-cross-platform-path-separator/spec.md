# 修复跨平台路径分隔符 Spec

## Why
`RoutePathBuilder.BuildOutputPathFromUrl` 在 line 54 将 `/` 替换为 `Path.DirectorySeparatorChar`（Windows 上为 `\`），但紧接着调用 `NormalizeOutputPath` 又把 `\` 统一转回 `/`。这导致 macOS 上测试通过（两者都是 `/`），但 Windows CI 上 4 个测试失败——实际返回值始终用 `/`，而测试期望值却用了 `\`。

## What Changes
- 删除 `BuildOutputPathFromUrl` 中冗余的 `outputPath.Replace('/', Path.DirectorySeparatorChar)` 行（line 54）
- 测试中移除 `expected.Replace('/', System.IO.Path.DirectorySeparatorChar)`，改用 `/` 直接比较

## Impact
- Affected specs: 无
- Affected code: `src/Bukit.Routing/RoutePathBuilder.cs#L54`, `tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs#L33,L43`

## MODIFIED Requirements
### Requirement: BuildOutputPathFromUrl
系统 SHALL 将 URL 转换为输出路径，路径内部始终使用 `/` 作为分隔符，不依赖操作系统路径分隔符。

#### Scenario: 跨平台行为一致
- **GIVEN** URL 为 `/hello-world/`
- **WHEN** 调用 `BuildOutputPathFromUrl(url)`
- **THEN** 返回的路径使用 `/` 作为分隔符，无论运行在 Windows 还是 Unix
