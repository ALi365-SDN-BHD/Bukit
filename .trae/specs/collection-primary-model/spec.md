# Collection 成为唯一推荐模型 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P0-2  
> 当前状态：`collection` > `type` 回退链已存在，但缺少 deprecation warning 和统一方法

## Why

代码库中存在 5 个重复的 `GetCollection()` 实现（RouteGenerator、CollectionRouteIndex、I18nOutputMerger、SeoAlternatesService、RssGenerator），逻辑均为 `collection → type → "page"` 回退。当前：

- 没有对 `type=post/page`（不含 collection）的 deprecation warning
- Notion provider 不自动提升 `Collection` 字段到 `meta.collection`
- `{type}` permalink 占位符仅读 `meta.type`，不支持 `{collection}`
- `/blog/`、`/pages/` 默认 listRoute 硬编码在 3 个文件中

## What Changes

- 提取共享 `ContentItem.GetCollection()` 扩展方法到 `Bukit.Engine.Abstractions`，消除 5 个重复实现
- 当 `meta.collection` 不存在且 `meta.type` 是 `post` 或 `page` 时输出 deprecation warning
- Notion provider 自动将 `Collection` 字段提升到 `meta.collection`
- `{collection}` permalink 占位符支持（`{type}` 保持不变作为兼容层）
- 不影响现有行为（collection 回退逻辑不变，仅新增 warning）
- 合集测试：collection 优先、type fallback、conflict detection、listRoute 不重复

## Impact

- Affected specs: `core-hardening-p0-p1`（ConfigDeprecationScanner 模式复用）
- Affected code:
  - `src/Bukit.Engine.Abstractions/ContentItemExtensions.cs` — **新建**，共享 `GetCollection()` 扩展方法
  - `src/Bukit.Routing/RouteGenerator.cs` — 使用扩展方法，新增 `{collection}` 占位符
  - `src/Bukit.Engine/Plugins/BuiltIn/CollectionRouteIndex.cs` — 使用扩展方法
  - `src/Bukit.Engine/I18nOutputMerger.cs` — 使用扩展方法
  - `src/Bukit.Engine/SeoAlternatesService.cs` — 使用扩展方法
  - `src/Bukit.Engine/RssGenerator.cs` — 使用扩展方法
  - `src/Bukit.Content/Notion/NotionContentProvider.cs` — 自动提升 `Collection` 字段
  - `src/Bukit.Engine/Stages/` — 新增 `CollectionWarningStage`

## ADDED Requirements

### Requirement: 共享 GetCollection 扩展方法

The system SHALL provide a single extension method `ContentItem.GetCollection(string defaultCollection = "page")` in `Bukit.Engine.Abstractions` namespace.

- 读取 `meta["collection"]` → 非空时返回
- 回退到 `meta["type"]` → 非空时返回
- 最终回退到 `defaultCollection` 参数

所有 GetCollection 调用点 SHALL 替换为此扩展方法。

#### Scenario: collection 优先

- **GIVEN** ContentItem 有 `meta.collection = "news"` 和 `meta.type = "post"`
- **WHEN** 调用 `item.GetCollection()`
- **THEN** 返回 `"news"`

#### Scenario: type 回退

- **GIVEN** ContentItem 无 `meta.collection`，有 `meta.type = "post"`
- **WHEN** 调用 `item.GetCollection()`
- **THEN** 返回 `"post"`

#### Scenario: 最终默认值

- **GIVEN** ContentItem 无 `meta.collection` 也无 `meta.type`
- **WHEN** 调用 `item.GetCollection("page")`
- **THEN** 返回 `"page"`

### Requirement: Legacy type 使用时的 Deprecation Warning

The system SHALL emit a deprecation warning when content uses `type=post` or `type=page` without `collection`.

Warning format:
```
[DEPRECATED] Content "item-id" uses type=<type> without collection. Legacy routing is enabled. Please migrate to content.collection and site.collections.
```

#### Scenario: type=post 无 collection 触发 warning

- **GIVEN** ContentItem 有 `meta.type = "post"` 且无 `meta.collection`
- **WHEN** 构建运行
- **THEN** 日志输出 `[DEPRECATED]` warning

#### Scenario: 有 collection 时不触发

- **GIVEN** ContentItem 有 `meta.collection = "blog"`
- **WHEN** 构建运行
- **THEN** 无 deprecation warning

#### Scenario: type ≠ post/page 时不触发

- **GIVEN** ContentItem 有 `meta.type = "custom"` 且无 `meta.collection`
- **WHEN** 构建运行
- **THEN** 无 deprecation warning（非标准 type 被视为有意使用）

### Requirement: Notion Collection 字段自动提升

Notion provider SHALL automatically promote a Notion `Collection` property to `meta.collection`.

- 字段名匹配大小写不敏感：`Collection`、`collection`、`COLLECTION`
- 类型支持 `select`、`status`、`rich_text`
- 同时存在 `Type` 和 `Collection` 时两者都设置
- 不配置时不影响现有行为

#### Scenario: Notion 有 Collection 字段

- **GIVEN** Notion 数据库有名为 `Collection` 的 select 字段，值为 `products`
- **WHEN** Notion provider 加载内容
- **THEN** `ContentItem.Meta["collection"]` = `"products"`

#### Scenario: Notion 无 Collection 字段

- **GIVEN** Notion 数据库无 `Collection` 字段
- **WHEN** Notion provider 加载内容
- **THEN** `ContentItem.Meta` 不包含 `"collection"` key（行为不变）

### Requirement: {collection} Permalink 占位符

The system SHALL support `{collection}` placeholder in collection permalink patterns, resolving to the item's effective collection name.

#### Scenario: {collection} 展开为 collection 值

- **GIVEN** CollectionConfig permalink = `"/{collection}/{slug}/"`，ContentItem collection = `"products"`
- **WHEN** 路由生成
- **THEN** URL = `"/products/my-slug/"`
