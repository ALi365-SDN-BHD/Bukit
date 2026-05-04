# 07 内容（多源 sources）：把 pages / posts / modules 组合起来

当你的站点内容不止一个来源时（例如：页面用 Markdown、博客用 Notion、官网模块用 data），就应该使用 `content.provider: sources`。

本页会讲清楚 sources 的结构、`mode=content|data` 的含义、以及常见组合的完整示例。

## 你将获得什么

- sources 的字段解释（type/name/mode）
- 3 套可复制的组合配置（全 Markdown、全 Notion、混合模式）
- 如何把 modules 作为 data 注入 `site.modules`

## sources 的基本结构

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      markdown:
        dir: content
        defaultType: page
```

### 字段说明

| 字段 | 作用 | 建议 |
|---|---|---|
| `type` | 来源类型：`markdown` 或 `notion` | 按来源选择 |
| `name` | 来源名字（用于标识/排查） | 短、清晰，例如 `pages`、`posts`、`modules` |
| `mode` | `content` 或 `data` | 默认用 `content`；modules 用 `data` |

关键语义：

- `mode: content`：会生成路由与页面（常规内容）
- `mode: data`：不会生成路由；会被分组注入 `site.modules.<type>[]`（结构化内容块）

补充：taxonomy（分类/标签）与 `mode: data`

- 如果你有“分类数据库/标签数据库”，可以把它作为一个 `mode: data` 的 source 加进来，并将 `name` 设置为 `categories` 或 `tags`。
- 构建时会把该 data 源的条目当作 taxonomy term 列表：即使某个分类/标签当前没有任何文章引用，也会生成对应的空聚合页（避免点击菜单后 404）。

## 组合示例 1：全 Markdown（内容 + modules）

对照可运行示例：`examples/starter/site.modules.yaml`。

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: content
      mode: content
      markdown:
        dir: content
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

配套的 modules 模拟数据文件：

- `data/banner-1.md`
- `data/faq-main.md`
- `data/nav-home.md`

详见：[09-Modules-结构化数据](./09-Modules-结构化数据.md)。

## 组合示例 2：全 Notion（多数据库：pages + posts + modules）

适用场景：

- 团队内容全部由 Notion 管理，但希望拆库（权限/流程/字段差异）

```yaml
content:
  provider: sources
  sources:
    - type: notion
      name: pages
      mode: content
      notion:
        databaseId: "db_pages"
        filterProperty: Published
        filterType: checkbox_true
        fieldPolicy: { mode: whitelist, allowed: [seo_title, seo_desc, cover] }
    - type: notion
      name: posts
      mode: content
      notion:
        databaseId: "db_posts"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        fieldPolicy: { mode: all }
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "db_modules"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
    - type: notion
      name: categories
      mode: data
      notion:
        databaseId: "db_categories"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

注意：

- 三个数据库都需要同一个 `NOTION_TOKEN` 能访问（或者拆仓库/拆工作流）
- modules 数据库里应该有 `type/order/locale/enabled` 等字段（见：[09-Modules-结构化数据](./09-Modules-结构化数据.md)）

## 组合示例 3：混合（页面 Markdown + 博客 Notion + modules Markdown）

适用场景：

- 官网页面由开发在仓库里维护（更稳定）
- 博客/新闻由运营在 Notion 里维护（更灵活）
- 官网模块先用 Markdown data 快速跑通，再逐步迁移到 Notion

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      markdown:
        dir: content/pages
        defaultType: page
    - type: notion
      name: posts
      mode: content
      notion:
        databaseId: "db_posts"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        fieldPolicy: { mode: whitelist, allowed: [seo_title, seo_desc, cover, reading_time] }
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

## 排查建议（sources 常见问题）

### 1）某个来源的内容“没进站点”

先做三件事：

1. `doctor --config site.yaml` 看是否有配置校验错误
2. 确认该 source 的 `dir/databaseId` 指向正确
3. 如果是 Notion，确认 filter 是否把内容过滤掉（例如 Published 没打勾）

### 2）modules 没出现在模板里

确认：

- source 的 `mode` 是 `data`
- modules 内容项里有 `type`（用于分组到 `site.modules.<type>`）
- 主题模板确实在读取 `site.modules`（见：[08-主题与模板](./08-主题与模板.md)）
