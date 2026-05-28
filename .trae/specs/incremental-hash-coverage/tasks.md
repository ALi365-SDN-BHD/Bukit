# Tasks

## Task 1: RenderDependencyHasher 补充 6 个缺失字段 ✅

在 `RenderDependencyHasher.Compute()` 中追加 `Site.Url`、`Site.Languages`、`Site.DefaultLanguage`、`Site.SitemapMode`、`Site.RssMode`、`Site.SearchMode` 的哈希。

- [x] 1.1 读取 `RenderDependencyHasher.cs` 源码，确认现有 hash 追加模式
- [x] 1.2 追加 `Site.Url`（直接追加，非 null 时）
- [x] 1.3 追加 `Site.Languages`（排序后逐一追加）
- [x] 1.4 追加 `Site.DefaultLanguage`（直接追加，非 null/空时）
- [x] 1.5 追加 `Site.SitemapMode`（回退 `"split"` 后追加）
- [x] 1.6 追加 `Site.RssMode`（回退 `"split"` 后追加）
- [x] 1.7 追加 `Site.SearchMode`（回退 `"split"` 后追加）
- [x] 1.8 `dotnet build` 通过

## Task 2: 新增 RenderDependencyHasherTests 单元测试 ✅

新增 `tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs`，覆盖每个新字段和边界情况。

- [x] 2.1 测试 `Site.Url` 变更使 hash 不同
- [x] 2.2 测试 `Site.Languages` 变更使 hash 不同
- [x] 2.3 测试 `Site.DefaultLanguage` 变更使 hash 不同
- [x] 2.4 测试 `Site.SitemapMode` 变更使 hash 不同
- [x] 2.5 测试 `Languages` 顺序不影响 hash（因排序）
- [x] 2.6 测试 `null` Languages 不回退（不追加）
- [x] 2.7 测试所有输出模式回退默认值 `"split"`
- [x] 2.8 测试完整 hash 确定性（相同配置两次调用产生相同 hash）
- [x] 2.9 `dotnet test` 通过

## Task 3: 验证整体正确性 ✅

- [x] 3.1 `dotnet build` Release 0 警告 0 错误
- [x] 3.2 `dotnet format --verify-no-changes` 通过
- [x] 3.3 全部 Bukit.Engine.Tests 通过（1023 个）
- [x] 3.4 确认 checklist 所有 checkpoints 通过

# Task Dependencies

- Task 2 依赖于 Task 1（测试需要实现完成才能编译）✅
- Task 3 依赖于 Task 1 + Task 2 ✅
