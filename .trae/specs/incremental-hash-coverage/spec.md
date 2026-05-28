# 增量构建 Hash 覆盖补强 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P1-2  
> 前置依赖：`core-hardening-p0-p1` 已实现 `RenderDependencyHasher` 框架

## Why

当前 `RenderDependencyHasher.Compute()` 已覆盖 site/title/seo/analytics/theme/taxonomy/plugins/modules/data 等核心配置，但遗漏了以下 6 个字段：

- `Site.Url` — 影响 sitemap canonical、RSS feed URL、OG 标签
- `Site.Languages` — 影响多语言输出目录结构
- `Site.DefaultLanguage` — 影响默认语言过滤逻辑
- `Site.SitemapMode` — 影响 sitemap 输出模式（split/merged/index）
- `Site.RssMode` — 影响 RSS 输出模式
- `Site.SearchMode` — 影响 search index 输出模式

当用户修改这些字段时，页面应重新渲染，否则会出现"改了 URL/语言配置但输出仍是旧内容"的静默错误。

## What Changes

- 在 `RenderDependencyHasher.Compute()` 中追加 6 个缺失字段的哈希
- `Site.Languages` 按排序后逐个追加（确保顺序不影响哈希）
- `Site.SitemapMode`/`Site.RssMode`/`Site.SearchMode` 使用默认值 `"split"` 回退后再追加
- 新增 `RenderDependencyHasherTests.cs` 测试每个新字段变更后 hash 变化
- 新增集成测试验证修改配置后页面重新渲染

## Impact

- Affected specs: 无
- Affected code:
  - `src/Bukit.Engine/Incremental/RenderDependencyHasher.cs` — 追加 6 个字段
  - `tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs` — 新增测试文件

## ADDED Requirements

### Requirement: 缺失字段补全

The system SHALL include the following 6 fields in `RenderDependencyHasher.Compute()`:

| 字段 | 访问路径 | 特殊处理 |
|------|---------|---------|
| `Url` | `config.Site.Url` | 直接追加 |
| `Languages` | `config.Site.Languages` | 排序后逐个追加 |
| `DefaultLanguage` | `config.Site.DefaultLanguage` | 直接追加 |
| `SitemapMode` | `config.Site.SitemapMode` | 回退 `"split"` 后追加 |
| `RssMode` | `config.Site.RssMode` | 回退 `"split"` 后追加 |
| `SearchMode` | `config.Site.SearchMode` | 回退 `"split"` 后追加 |

#### Scenario: 修改 Site.Url 后 hash 变化

- **GIVEN** `RenderDependencyHasher` 实例
- **WHEN** 两次调用 `Compute()`，仅 `config.Site.Url` 不同
- **THEN** 两次 hash 不同

#### Scenario: 修改 Languages 列表后 hash 变化

- **GIVEN** `RenderDependencyHasher` 实例
- **WHEN** 两次调用 `Compute()`，`config.Site.Languages` 从 `["en","zh"]` 变为 `["en","zh","fr"]`
- **THEN** 两次 hash 不同

#### Scenario: 修改输出模式后 hash 变化

- **GIVEN** `RenderDependencyHasher` 实例
- **WHEN** 两次调用 `Compute()`，`config.Site.SitemapMode` 从 `"split"` 变为 `"merged"`
- **THEN** 两次 hash 不同

#### Scenario: Languages 顺序不影响 hash

- **GIVEN** `RenderDependencyHasher` 实例
- **WHEN** 两次调用 `Compute()`，`config.Site.Languages` 分别为 `["en","zh"]` 和 `["zh","en"]`
- **THEN** 两次 hash 相同（因排序后追加）
