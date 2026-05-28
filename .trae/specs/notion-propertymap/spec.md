# Notion PropertyMap 字段可配置映射 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P0-3

## Why

当前 Notion provider 的字段名是硬编码的 — `ExtractTitle` 读 `Title`、`ExtractSlug` 读 `Slug`、`ExtractType` 读 `Type`、`ExtractPublishAt` 先读 `PublishAt` 再回退 `Date`，meta 提升链中字段名也是固定的（`language`、`i18n_key`、`summary` 等）。

真实 Notion 数据库的字段名千变万化：`Publish At`（含空格）、`SEOTitle`、`簡介`、`发布日期`、`Lang`、`分类` 等。当前硬编码映射无法适配这些场景。

## What Changes

- 在 `NotionConfig` 中新增 `PropertyMap` 配置块，允许用户覆盖默认字段名
- 修改 `NotionPropertyParser` 的提取方法，先查 propertyMap 再回退默认值
- 修改 `NotionContentProvider` 的 meta 提升链，使用 propertyMap 映射后的字段名
- Doctor 命令新增 `--notion-schema` 标志，验证 mapped 字段在数据库中是否存在
- 不配置 propertyMap 时行为不变（完全向后兼容）

## Impact

- Affected specs: 无
- Affected code:
  - `src/Bukit.Config/AppConfig.cs` — `NotionConfig` 新增 `PropertyMap` 和 `SeoFieldMapConfig`
  - `src/Bukit.Config/ProviderValidators.cs` — 验证 propertyMap 字段
  - `src/Bukit.Content/Notion/NotionPropertyParser.cs` — 提取方法接受 propertyMap 参数
  - `src/Bukit.Content/Notion/NotionContentProvider.cs` — 传递 propertyMap 到提取和提升链
  - `src/Bukit.Content/Notion/NotionProviderOptions.cs` — 新增 propertyMap 字段
  - `src/Bukit.Engine/ContentProviderFactory.cs` — 映射 config → options
  - `src/Bukit.Cli/Commands/DoctorCommand.cs` — 新增 `--notion-schema` 检查

## ADDED Requirements

### Requirement: NotionPropertyMap 配置

The system SHALL support `content.notion.propertyMap` to override default Notion field names.

```yaml
content:
  notion:
    propertyMap:
      title: Title
      slug: Slug
      type: Type
      publishAt: PublishAt
      language: language
      i18nKey: i18n_key
      summary: Summary
      collection: Collection
```

| Map Key | 默认 Notion 字段名 | Meta 目标 | 用途 |
|---------|-------------------|----------|------|
| `title` | `Title` | `ContentItem.Title` | 页面标题 |
| `slug` | `Slug` | `ContentItem.Slug` | URL slug |
| `type` | `Type` | `meta.type` | 内容类型 |
| `publishAt` | `PublishAt`/`Date` | `ContentItem.PublishAt` | 发布日期 |
| `language` | `language` | `meta.language` | 语言 |
| `i18nKey` | `i18n_key` | `meta.i18nKey` | 国际化键 |
| `summary` | `summary` | `meta.summary` | 摘要 |
| `collection` | `collection` | `meta.collection` | 集合归属 |

#### Scenario: 用户配置了不同字段名

- **GIVEN** `propertyMap: { title: "SEO Title", slug: "URL Slug" }`
- **WHEN** Notion provider 加载含字段 `SEO Title` 和 `URL Slug` 的页面
- **THEN** `ContentItem.Title` = `SEO Title` 的值，`ContentItem.Slug` = `URL Slug` 的值

#### Scenario: 不配置 propertyMap

- **GIVEN** `content.notion.propertyMap` 未配置
- **WHEN** Notion provider 加载内容
- **THEN** 使用默认字段名映射，行为不变

### Requirement: Doctor --notion-schema 检查

The system SHALL support `bukit doctor --notion-schema` to validate mapped properties against the Notion database.

检查逻辑：
1. 连接 Notion API 获取数据库 schema
2. 遍历 propertyMap 中的每个 mapped 字段名
3. 检查是否在数据库 schema 中存在
4. 检查属性类型是否与预期一致

输出示例：
```
Notion Schema Check for database xxx:
  ✓ title    → "Title" (title) — OK
  ✓ slug     → "Slug" (rich_text) — OK
  ✗ publishAt → "Publish At" — NOT FOUND in database
  ✗ type     → "Category" — type mismatch: expected select, got multi_select
  ⚠ language → not mapped (using default "language")
```

#### Scenario: --notion-schema 检测到缺失字段

- **WHEN** `propertyMap.publishAt: "Publish Date"` 但数据库中不存在该字段
- **THEN** Doctor 输出 `NOT FOUND` 并返回非零退出码

## MODIFIED Requirements

### Requirement: NotionPropertyParser 提取方法接受 propertyMap

`ExtractTitle`、`ExtractSlug`、`ExtractType`、`ExtractPublishAt` 方法 SHALL 接受可选的 `NotionPropertyMap?` 参数。

当 propertyMap 非 null 时，优先使用 mapped 名称查找；否则使用默认名称。

#### Scenario: ExtractPublishAt 使用 mapped 名称

- **GIVEN** `propertyMap.PublishAt = "Release Date"`
- **WHEN** 调用 `ExtractPublishAt(properties, propertyMap)`
- **THEN** 先查找 `"Release Date"`，再回退 `"Date"`（Date 始终作为 secondary fallback）
