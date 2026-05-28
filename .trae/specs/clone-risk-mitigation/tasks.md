# Tasks

## Task 1: README 措辞修正 ✅
- [x] 1.1 修改 `README.md` — "Clone any website's design" → "Extract design tokens and scaffold a Bukit-compatible theme"
- [x] 1.2 `README.zh-CN.md` 无 clone 提及，跳过

## Task 2: CloneTokens.FromJson 解析失败警告 ✅
- [x] 2.1 `FromJson()` 返回 `(CloneTokens tokens, string? error)` 元组
- [x] 2.2 `catch` 块设置 error 为 `ex.Message`
- [x] 2.3 `CloneCommand` 检查 error，非空时输出 `✖ Failed to parse tokens: ...` 并 `return 2`
- [x] 2.4 所有测试适配（18 个调用点加 `.tokens` 访问器）

## Task 3: 验证 ✅
- [x] 3.1 `dotnet format --verify-no-changes` 通过
- [x] 3.2 730 Bukit.Cli.Tests 通过
- [x] 3.3 1030 Bukit.Engine.Tests 通过
