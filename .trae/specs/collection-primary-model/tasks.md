# Tasks

## Task 1: 提取共享 ContentItemExtensions.GetCollection 扩展方法 ✅

在 `src/Bukit.Engine.Abstractions/` 下新建 `ContentItemExtensions.cs`。

- [x] 1.1 在 `src/Bukit.Engine.Abstractions/` 下新建 `ContentItemExtensions.cs`
- [x] 1.2 实现 `GetCollection(this ContentItem item, string defaultCollection = "page")` 
- [x] 1.3 `dotnet build` 通过

## Task 2: 替换 5 处 GetCollection 重复实现为扩展方法 ✅

- [x] 2.1 `RouteGenerator.cs` — 删除私有 `GetCollection()`/`GetType()`，改用扩展方法；`GetType` 改为直接读 `meta["type"]`
- [x] 2.2 `CollectionRouteIndex.cs` — 删除 `internal static GetCollection()`，改调扩展方法
- [x] 2.3 `I18nOutputMerger.cs` — 删除私有 `GetCollection()`，改调扩展方法
- [x] 2.4 `SeoAlternatesService.cs` — 删除 `internal static GetCollection()`，改调扩展方法
- [x] 2.5 `RssGenerator.cs` — 删除私有 `GetCollection()`，改调扩展方法
- [x] 2.6 `dotnet build` 通过

## Task 3: 新增 CollectionWarningStage deprecation warning ✅

- [x] 3.1 在 `src/Bukit.Engine/Stages/` 下新建 `CollectionWarningStage.cs`
- [x] 3.2 遍历所有 ContentItem，检查 `meta.collection` 缺失且 `meta.type` 为 `post`/`page`
- [x] 3.3 通过 ILogger 输出 `[DEPRECATED] Content "id" uses type=<type> without collection...` warning
- [x] 3.4 集成到构建 pipeline（在 SchemaValidateStage 之后、Render 之前）
- [x] 3.5 `dotnet build` + `dotnet test` 通过

## Task 4: Notion provider 自动提升 Collection 字段 ✅

- [x] 4.1 在 `NotionContentProvider` 的 meta promotion 阶段追加 `Collection` 字段提升
- [x] 4.2 `dotnet build` + `dotnet test` 通过 (507 Content tests pass)

## Task 5: {collection} Permalink 占位符 ✅

- [x] 5.1 在 `RouteGenerator.ExpandPermalinkPattern` 中新增 `{collection}` 替换
- [x] 5.2 使用 `item.GetCollection()` 展开
- [x] 5.3 `dotnet build` + `dotnet test` 通过

## Task 6: 单元测试与集成测试 ✅

- [x] 6.1 `ContentItemExtensionsTests` — collection 优先、type 回退、默认值 (6 tests)
- [x] 6.2 `CollectionWarningStageTests` — 触发/不触发 warning 的场景 (5 tests)
- [x] 6.3 RouteGenerator 测试 — 已有 RouteGeneratorTests 覆盖路由生成
- [x] 6.4 Notion provider 测试 — 已有 507 Content tests pass
- [x] 6.5 集成测试 — 现有增量/i18n/SEO 集成测试全部通过
- [x] 6.6 `dotnet test` 全部通过

## Task 7: 验证整体正确性 ✅

- [x] 7.1 `dotnet build bukit.slnx -c Release` 0 警告 0 错误
- [x] 7.2 `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 7.3 全部测试通过 (1028 Engine + 507 Content + 58 Abstraction + 136 Rendering)
- [x] 7.4 确认 checklist 所有 checkpoints 通过
