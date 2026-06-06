# 内容系统（Markdown / Notion / sources）

本页描述内容系统的输入、输出与约定：ContentItem/Meta/Fields、Markdown Front Matter、Notion 字段归一化、以及 sources 组合模式。

实现参考：
- `src/Bukit.Content/ContentItem.cs`
- `src/Bukit.Content/ContentField.cs`
- `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit.Content/Notion/NotionContentProvider.cs`
- `src/Bukit.Engine/SiteEngine.cs`（mode=data 的处理与 modules 注入）

## 核心模型：ContentItem / Meta / Fields

### ContentItem

ContentItem 是引擎的统一输入模型，包含：
- `Id/Title/Slug/PublishAt/ContentHtml`
- `Meta`：影响引擎决策的元信息
- `Fields`：面向模板消费的自定义字段（`<key>.type/value`）

### Meta（引擎决策）

常见 Meta 键：
- `collection`：对应 site.collections 中的 key（推荐，用于匹配路由与模板）
- `type`：可选内容分类或主题模板匹配键；不触发核心默认路由
- `draft`：草稿标记（见下方"草稿过滤"一节）
- `language`：多语言过滤（内容项的语言归属）
- `route` 或 `url/outputPath/template`：路由覆盖（见 [路由](./routing.zh-CN.md)）
- `source` / `sourcePath` / `notionPageId`：来源信息
- `sourceMode`：当启用 sources 时，用于区分 `content` / `data`

### Fields（模板消费）

模板使用统一入口：
- `page.fields.<key>.type`
- `page.fields.<key>.value`

字段 key 通常会被归一化（尤其是 Notion），建议模板与建模都使用“下划线小写”风格，例如 `seo_title`、`reading_time`。

### Sitemap / SEO 约定

- `fields.update_time`（date 或可解析日期的文本）会被用于 sitemap `<lastmod>`（优先级高于 `publishAt`）
- 若页面最终输出的 HTML 包含 `<meta name="robots" content="noindex">`（或 `<meta name="sitemap" content="exclude">`），则该页面会被从 sitemap 中剔除

## 草稿过滤

当 `build.draft` 为 `false`（默认）时，引擎会在内容加载后、路由生成前过滤掉草稿内容项。

判定规则（`SiteEngine.cs`）：若 `meta.draft` 的值为 `true`（bool）、`"true"` 或 `"True"`（字符串），该内容项会被移除。

### Markdown 中标记草稿

在 Front Matter 中设置 `draft: true`：

```yaml
---
title: 未完成的文章
draft: true
---
```

### Notion 中标记草稿

在 Notion 数据库中添加一个 `Draft` checkbox 属性（勾选表示草稿）。Notion provider 会将其归一化为 `meta.draft = true`。

### 与 build.draft 的交互

- `build.draft: false`（默认）：草稿内容项被过滤，不会生成路由和页面
- `build.draft: true`：所有内容项（包括草稿）都会参与构建
- CLI 覆盖：`bukit build --draft` 强制启用草稿渲染

## Markdown 模式

### 文件与目录

`content.provider: markdown` 时，从 `content.markdown.dir`（默认 `content/`）递归读取 `*.md` 文件。

### Front Matter

支持 YAML Front Matter：

```yaml
---
type: page
title: 关于我们
slug: about
publishAt: 2026-01-01T00:00:00Z
tags: [bukit, starter]
categories: docs
summary: 一句话摘要
seo_title: 自定义 SEO 标题
---
```

规则要点（由 `MarkdownFolderProvider` 实现）：
- `title` 缺失时，会从正文的第一个 `# ` 标题提取；仍缺失则回退为 slug
- `slug` 默认是文件名（不含扩展名），可在 Front Matter 覆盖
- `publishAt` 可选，缺失则使用文件最后修改时间
- `tags/categories` 支持字符串（逗号分隔）或数组，会统一归一化为列表

### Reserved keys 与 Fields 构建

以下键会被视为保留键，不会作为一般字段进入 `page.fields`（但 tags/categories/summary 会以固定方式写入 fields）：
- `title/slug/type/publishAt/language/tags/categories/summary`
- `route/url/outputPath/template`

## Notion 模式

### 环境变量

Notion token 强制来自环境变量：
- `NOTION_TOKEN`

缺失会在配置校验阶段直接报错（`ConfigValidator`）。

### 固定字段（用于引擎决策）

Notion provider 会从数据库 properties 中解析以下字段（字段名大小写敏感，遵循 Notion UI 显示名）：
- `Title`（title）：标题
- `Slug`（rich_text 或 formula.string）：slug
- `Type`（select/multi_select）：类型（默认 `post`）
- `PublishAt` 或 `Date`（date）：发布时间（默认 now）
- 过滤与排序：由 `content.notion.filter*`、`content.notion.sort*` 控制

### 自定义字段进入 page.fields：fieldPolicy

Notion provider 会把 properties 映射为 `fields`，但会做两层筛选：

1. 保留字段剔除：`published/title/slug/type/publishat` 不会进入 fields
2. fieldPolicy 筛选：
   - `whitelist`：仅允许 `fieldPolicy.allowed` 中列出的字段
   - `all`：除保留字段外全部进入 fields

字段名归一化规则：把原字段名转换为“下划线小写 + 非字母数字变下划线”，例如：
- `SEO Title` → `seo_title`
- `PublishAt` → `publishat`

### 特殊字段提升到 Meta：language / i18nKey

Notion provider 会把以下 fields（若存在）提升到 Meta，供引擎做多语言与 alternates 关联：
- `language` → `meta.language`
- `i18n_key` / `i18nkey` → `meta.i18nKey`

## sources 组合模式（多数据库 + data 模块）

当你需要 Pages/Posts/Modules 多库组合时，使用：

```yaml
content:
  provider: sources
  sources:
    - type: notion
      name: pages
      mode: content
      notion:
        databaseId: "..."
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "..."
        fieldPolicy: { mode: all }
```

mode 的语义：
- `content`：参与路由生成与页面渲染
- `data`：不生成路由；会被引擎分组、排序后注入 `site.modules`（见 [Modules](./modules-data.zh-CN.md)）

### sources 模式下自动注入的 meta 字段

使用 `sources` 组合时，`CompositeContentProvider` 会为每个 `ContentItem` 自动注入以下 meta 字段：

| Meta 字段 | 说明 |
|---|---|
| `sourceKey` | 数据源名称（对应 `sources[].name`，无 name 时为索引序号如 `0`、`1`） |
| `sourceMode` | 数据源模式（`content` 或 `data`） |
| `sourceId` | 原始 ContentItem ID（不含 sourceKey 前缀） |

ContentItem 的 `Id` 会被修改为 `{sourceKey}:{originalId}` 格式以保证多源唯一性。

这些来源字段会进入 canonical fields；模板通过 `page.fields` 访问，插件通过 `ContentDocument.CustomFields` 或 `ContentFieldReader` 读取，用于区分内容来源。

## 统一媒体本地化（跨数据源）

当前图片本地化由引擎统一处理，而不是由具体数据源处理：
- 入口：`SiteEngine.BuildAsync` 在 `provider.LoadAsync()` 之后执行统一重写管线
- 组件：
  - `src/Bukit.Content/Media/ImageAssetLocalizer.cs`
  - `src/Bukit.Content/Media/ContentImageRewritePipeline.cs`
  - `src/Bukit.Content/Media/MediaFailure.cs`

### 统一处理范围

ContentHtml 中支持以下模式的图片 URL 本地化：
- `<img src="...">` — 标准图片标签
- `data-src="..."` — 懒加载图片（任意元素）
- `<video poster="...">` — 视频封面图
- `srcset="url1 1x, url2 2x"` — 响应式图片（`<img>` 或 `<source>` 上的 srcset 属性）

Fields 中命中 `content.media.fieldKeys` 的字段也会被处理：
- 字符串类型的字段值：直接本地化
- 字符串列表类型的字段值（如 Notion files 属性包含多张图片）：逐个 URL 本地化

### Notion 页面级 cover/icon

Notion provider 现在会自动提取页面级别的 `cover` 和 `icon`：
- `page.cover`（external 或 file 类型）→ 写入 `fields.cover`
- `page.icon`（external 或 file 类型，emoji 会被忽略）→ 写入 `fields.icon`
- 仅在 fields 中不存在同名键时注入（数据库属性优先）

### Markdown 图片语法

Markdown provider 支持标准图片语法：
- `![alt](url)` → 生成 `<img src="url" alt="alt" />`
- `![alt](url "title")` → 生成 `<img src="url" alt="alt" title="title" />`
- 生成的 `<img>` 标签会被 media 管线正常处理

### Notion files 属性

Notion `files` 类型属性支持多文件：
- 单文件：存储为 `ContentField("file", url)`
- 多文件：存储为 `ContentField("files", [url1, url2, ...])`
- 两种类型都会被 media 管线处理

### 默认行为

- 远程图片下载到 `assets/uploads`
- 页面内 URL 替换为 `/assets/uploads/...`
- 下载失败或源地址缺失时回退为 `/assets/images/noneimg-news.jpg`
- 文件命名基于 URL 的 SHA256 哈希前缀 + 扩展名，缓存查找时会自动匹配已有文件（忽略扩展名差异）

### 失败报告

构建结束后，media 管线会输出本地化失败的汇总报告：
- 每个失败的 URL 都会记录具体原因（HTTP 状态码、Content-Type 拒绝、文件过大、SSRF 拦截等）
- 日志格式：`event=media.localize_summary failed=N`，后跟逐条失败明细

### 配置位置

- 仅支持 `content.media.*`
- Notion 专属媒体配置已移除，且不做兼容读取。

## 相关专题

- Notion 字段与配置约定：[config-site-yaml.md](./config-site-yaml.zh-CN.md)
- 企业官网内容建模（Pages/Posts/Modules）：[modules-data.md](./modules-data.zh-CN.md)
- AI 建站（与内容模型结合）：[chatgpt/README.zh-CN.md](../ai/chatgpt/README.zh-CN.md)、[intent-cli.md](./intent-cli.md)
