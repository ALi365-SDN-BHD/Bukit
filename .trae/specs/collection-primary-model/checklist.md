# Collection 唯一推荐模型 Checklist

## 共享扩展方法
- [x] `ContentItemExtensions.GetCollection()` 存在于 `src/Bukit.Engine.Abstractions/`
- [x] collection 优先、type 回退、默认值三级回退正确
- [x] 5 处旧 GetCollection 实现已删除
- [x] RouteGenerator 的 `{type}` 占位符行为不变
- [x] 所有 5 个文件的 `dotnet build` 通过

## Deprecation Warning
- [x] `CollectionWarningStage` 已集成到构建 pipeline
- [x] `type=post` 无 collection → `[DEPRECATED]` warning
- [x] `type=page` 无 collection → `[DEPRECATED]` warning
- [x] `type=custom` 无 collection → 无 warning
- [x] 有 collection → 无 warning
- [x] Warning 含 item Id 供排查
- [x] 不影响构建结果（warning 不阻断）

## Notion Collection 自动提升
- [x] Notion `Collection` select 字段 → `meta["collection"]`
- [x] Notion 无 Collection 字段 → 行为不变
- [x] `Type` + `Collection` 同时存在 → 两者都设置

## {collection} Permalink 占位符
- [x] `ExpandPermalinkPattern` 支持 `{collection}`
- [x] `/{collection}/{slug}/` 展开正确
- [x] 无 meta.collection 时回退到 `"page"`
- [x] `{type}` 占位符行为不变

## 测试覆盖
- [x] ContentItemExtensionsTests：collection 优先、type 回退、默认值 (6 tests)
- [x] CollectionWarningStageTests：触发/不触发 warning (5 tests)
- [x] {collection} 占位符展开测试 (已有 RouteGeneratorTests)
- [x] Notion Collection 字段提升测试 (已有 NotionContentProviderEndToEndTests)
- [x] collection + permalink 冲突时行为明确 (已有 RouteGeneratorTests)
- [x] listRoute 不重复生成 (已有集成测试)

## 回归验证
- [x] `dotnet build bukit.slnx -c Release` 0 警告 0 错误
- [x] `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 全部 Bukit.Engine.Tests 通过 (1028)
- [x] 全部 Bukit.Cli.Tests 通过
- [x] 全部 Bukit.Content.Tests 通过 (507)
- [x] 全部 Bukit.Rendering.Tests 通过 (136)
- [x] 全部 Bukit.Engine.Abstractions.Tests 通过 (58, +6)
- [x] 不破坏现有 CLI
- [x] 不破坏 examples/starter 构建
