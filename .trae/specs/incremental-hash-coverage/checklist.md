# 增量构建 Hash 覆盖补强 Checklist

## 实现段
- [x] `RenderDependencyHasher.Compute()` 追加了 `Site.Url`
- [x] `RenderDependencyHasher.Compute()` 追加了 `Site.Languages`（排序后逐一追加）
- [x] `RenderDependencyHasher.Compute()` 追加了 `Site.DefaultLanguage`
- [x] `RenderDependencyHasher.Compute()` 追加了 `Site.SitemapMode`（回退 `"split"`）
- [x] `RenderDependencyHasher.Compute()` 追加了 `Site.RssMode`（回退 `"split"`）
- [x] `RenderDependencyHasher.Compute()` 追加了 `Site.SearchMode`（回退 `"split"`）
- [x] 无 AOT 不兼容代码（不使用反射/动态加载）
- [x] 不破坏现有 CLI
- [x] 不破坏现有 examples/starter

## 单元测试段
- [x] `RenderDependencyHasherTests` 存在于 `tests/Bukit.Engine.Tests/`
- [x] 测试 `Site.Url` 变更使 hash 不同
- [x] 测试 `Site.Languages` 变更使 hash 不同
- [x] 测试 `Site.DefaultLanguage` 变更使 hash 不同
- [x] 测试 `Site.SitemapMode` 变更使 hash 不同
- [x] 测试 Languages 顺序不影响 hash
- [x] 测试 null Languages 不会追加
- [x] 测试 hash 确定性（相同配置 → 相同 hash）
- [x] 测试现有字段不受影响

## 回归验证
- [x] `dotnet build bukit.slnx -c Release` 通过，0 警告 0 错误
- [x] `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 全部 Bukit.Engine.Tests 通过（1023 个）
- [x] 全部 Bukit.Cli.Tests 通过
- [x] 全部 Bukit.Content.Tests 通过
- [x] 全部 Bukit.Rendering.Tests 通过
