# 多语言与 SEO（sitemap/rss/search 模式）

本文档说明多语言输出结构以及 sitemap/rss/search 的模式与边界。

实现参考：
- 配置字段：`src/Bukit.Config/AppConfig.cs`
- 校验：`src/Bukit.Config/ConfigValidator.cs`
- 构建与 i18n 输出：`src/Bukit.Engine/SiteEngine.cs`

## 多语言输出结构

当设置 `site.languages`（非空）时：
- 输出目录按语言分子目录：`dist/<lang>/...`
- 每个语言变体会用 `baseUrl + /<lang>` 作为变体 baseUrl（内部用于生成 url 与 sitemap alternates）
- `site.defaultLanguage` 必须包含在 `site.languages` 中；未设置时默认为列表第一项

当未设置 `site.languages` 时：
- 视为单语言模式，仅输出 `dist/...`

## sitemapMode

| 值 | 行为 | 适用场景 |
|---|---|---|
| `split` | 各语言变体各自生成 sitemap | 简单直观，默认模式 |
| `merged` | 生成合并 sitemap，包含 hreflang alternates（依赖 i18nKey 关联） | 需要 SEO 更严格的多语言站 |
| `index` | 生成 sitemap index，指向各语言的 sitemap.xml | 语言多且希望分拆 |

重要约束：
- `site.url` 为空时无法生成绝对 URL，因此 merged/index 相关策略可能被跳过或退化
- merged alternates 依赖内容项的 `meta.i18nKey`（Notion 提升字段 i18n_key/i18nkey）来建立跨语言同一内容的关联

## feedMode（1.0 约定）

`site.rssMode` 在 Bukit 1.0 的用户配置中已移除。Feed 模式由 `site.feed`（尤其是 `site.feed.formats`）和 feed 插件配置控制；旧的 merged/split 行为在 1.0 配置中不支持。

## site.search.mode

| 值 | 行为 |
|---|---|
| `split` | 各语言生成 search.json |
| `merged` | 合并生成 search.json |
| `index` | 生成 search index（聚合指向各语言的索引） |

searchIncludeDerived：
- 控制是否把插件派生页（DerivedRouted）纳入搜索索引；是否生效取决于 SearchIndex 插件实现

## baseUrl 与站点 URL 的使用边界

- `site.baseUrl`：用于构建站内相对链接与输出路径前缀语义（尤其是 GitHub Pages 子路径）
- `site.url`：用于生成 sitemap/rss 等需要绝对 URL 的场景

建议：
- GitHub Pages 部署时同时配置 baseUrl 与 url，或用 `--base-url`/`--site-url` 在 CI 中覆盖
