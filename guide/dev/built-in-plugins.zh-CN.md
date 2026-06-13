# 内置插件（BuiltIn）产物与边界

本页描述内置插件的“输出契约”（会生成哪些文件/页面、依赖哪些配置、在多语言下如何表现）。当你修改插件或依赖它们的主题时，应当优先维护本页以避免行为漂移。

内置插件实现目录：`src/Bukit.Engine/Plugins/BuiltIn/`

P3 publish outputs 说明：sitemap、feed、search、llms/llms-full、robots、agent manifest 由 publish projection pipeline（`PublishRepresentationRegistry`）统一拥有。部分 projection adapter 仍复用历史 generator/plugin 类（如 `SitemapGenerator`、`RssGenerator`、`SearchIndexBuilder`、`LlmsTxtPlugin`），但这些 aggregate 文件不再由默认 `IAfterBuildPlugin` 拥有。

相关文档：
- [插件体系](./plugins.zh-CN.md)
- [多语言与 SEO](./i18n-seo.zh-CN.md)
- [引擎固定产物](./engine-outputs.zh-CN.md)

## sitemap（publish projection adapter）

来源：`PublishRepresentationRegistry` adapter 复用 sitemap generator helpers。

- 输出：`<outputDir>/sitemap.xml`
- 依赖：必须配置 `site.url`（否则直接跳过不生成）
- 包含路由：
  - 引擎固定页：`/`、`/blog/`、`/pages/`
  - 所有 routed 内容页
  - 所有 derived 路由（来自 derive-pages 插件，例如 taxonomy/pagination/archive）
- 增强字段（v3.0+）：
  - `<priority>`：默认 `site.sitemapDetail.defaultPriority`（0.5），可通过 front matter `sitemap.priority` 按页覆盖
  - `<changefreq>`：默认 `site.sitemapDetail.defaultChangefreq`（weekly），可通过 front matter `sitemap.changefreq` 按页覆盖
  - `<image:image>`：当 `site.sitemapDetail.imageEnabled: true` 时，从 front matter `sitemap.images` 提取图片信息
  - `<video:video>`：当 `site.sitemapDetail.videoEnabled: true` 时，从 front matter `sitemap.videos` 提取视频信息
- lastmod 规则：
  - routed 内容页：优先使用 `fields.update_time`（可解析日期），取不到则回退到 `publishAt`
  - derived 路由：使用各 derive-pages 插件返回的 `LastModified`
- 屏蔽规则（基于最终 HTML meta）：
  - 若页面 HTML 含 `<meta name="robots" content="noindex|none ...">`，该页面会被从 sitemap 中剔除
  - 兼容：`<meta name="sitemap" content="exclude|noindex|false|0">`

多语言行为：
- 当 `site.languages` 非空且 `site.sitemapMode == merged`：根目录输出由 i18n root projection adapter 生成
- 其他模式：各语言输出目录各自生成 `sitemap.xml`

## feed（publish projection adapter，v3.0 替代原 rss 插件）

来源：`PublishRepresentationRegistry` adapter 复用 RSS、Atom、JSON Feed generator helpers。

- 输出：根据 `site.feed.formats` 生成多种格式：
  - `rss` → `<outputDir>/rss.xml`（RSS 2.0）
  - `atom` → `<outputDir>/feed/atom.xml`（Atom 1.0）
  - `json` → `<outputDir>/feed/feed.json`（JSON Feed 1.1）
- 依赖：必须配置 `site.url`（否则直接跳过不生成）
- 输入：只使用 routed 内容（不包含 derived）
- 配置项：
  - `site.feed.formats`：要生成的格式列表，默认 `["rss"]`
  - `site.feed.limit`：每 feed 最大条目数，默认 20
  - `site.feed.path`：feed 文件基础路径，默认 `feed`
- 每 collection 独立 feed：
  - `collection.output.feedPath`：自定义 feed 路径（如 `blog-feed`）
  - `collection.output.feedTitle`：自定义 feed 标题
  - `collection.output.feedDescription`：自定义 feed 描述
- front matter：`feed.exclude: true` 排除某页面；`feed.enclosure` 支持播客附件
- 插件开关 key：`site.plugins.feed`（不再使用 `rss`）

多语言行为：
- `site.rssMode` 在 1.0 已移除，不再作为可配置项用于 Feed 模式控制。
- 1.0 配置下，feed 按语言目录输出，并由 `site.feed` 与 `site.plugins.feed` 默认行为驱动。

## search-index（publish projection adapter）

来源：`PublishRepresentationRegistry` adapter 复用 `SearchIndexBuilder`；adapter 也会写可选的 `bukit-search.html` UI partial。

- 输出：`<outputDir>/search.json` + 可选 `bukit-search.html`
- 依赖：不依赖 `site.url`（可在纯相对链接站点使用）
- 内容字段：
  - `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
  - 新增 `weight`：当 front matter 设置 `searchWeight` 时写入，前端加权排序
- front matter 增强（v3.0+）：
  - `searchWeight`：搜索权重（默认 1，值越高排序越靠前）
  - `searchExclude: true`：从搜索索引中排除该页面
- `url` 生成规则：用 `site.baseUrl` 拼接页面 `route.url`（结果为站内路径）
- 内置搜索 UI（v3.0+）：
  - 配置 `site.search.ui: "default"` 启用
  - 支持 `site.search.uiTheme`（light/dark/auto）
  - 支持 `site.search.placeholderText` 自定义占位文本
  - 输出 `bukit-search.html`（零依赖 ~5KB JS），可被模板 `{{ include }}` 引用

是否包含派生页：
- 由 `site.searchIncludeDerived` 控制：
  - false：只索引 routed
  - true：索引 routed + derived

多语言行为：
- 每个语言变体目录都会生成各自的 `search.json`
- 如果 `site.search.mode == index`，引擎会在根目录额外生成 `search.index.json`（聚合指向各语言索引）

## taxonomy（IDerivePagesPlugin + IAfterBuildPlugin）

文件：`TaxonomyPlugin.cs`

根据内容的 `meta.tags` / `meta.categories` 派生页：

- `/tags/` → `tags/index.html`
- `/tags/<slug>/` → `tags/<slug>/index.html`
- `/categories/` → `categories/index.html`
- `/categories/<slug>/` → `categories/<slug>/index.html`

说明：
- 派生页使用模板：默认 `pages/page.html`
- 优先级：kind 级别 index/term > 全局 index/term > kind 级别 template > 全局 template > 默认 `pages/page.html`
- 页面内容为插件生成的简单 HTML（ul/li 列表），仍会写入 `page.content`（兼容旧主题）
- 同时注入结构化字段（便于主题直接渲染列表，而不是解析 HTML）：
  - index 页（`/tags/`、`/categories/`）：`page.fields.terms.type == "list"`，`page.fields.terms.value[]` 为 `{ title, slug, url, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
- term 页（`/tags/<slug>/`、`/categories/<slug>/`）：
  - `page.fields.items.type == "list"`，`page.fields.items.value[]` 为 `{ title, url, publish_date, summary? }`
  - `page.fields.taxonomy.value` 为 `{ kind, term, slug, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
  - `page.fields.pagination.value` 为 `{ page, page_size, total, total_pages, has_prev, has_next }`
- term 页 items 排序：
  - 默认按 `publishAt` 倒序
  - 支持置顶：`pinned=true` 的条目会排在最前，然后再按 `publishAt` 倒序
  - 可选置顶顺序：当配置了 `pinOrderField`（或 source 级别的 `pinOrderFieldBySource`）时，置顶条目会先按 `pinOrder` 升序排序，再按 `publishAt` 倒序
  - `pinOrder` 存在时即视为置顶（即使没有显式 `pinned=true`）
- slug 规则：字母数字保留，其余压缩为 `-`（小写）；支持 Unicode 拉丁字符音译（é→e, ß→ss, æ→ae 等）
- term 页支持分页路由：`/<kind>/<slug>/page/<n>/`（pageSize 由 `taxonomy.pageSize` 控制）
- AfterBuild 阶段输出 `taxonomy.json`（schema v2），包含所有分类维度及其 term 列表的结构化数据
- taxonomy 索引页可禁用：`taxonomy.indexEnabled=false`（或 `taxonomy.kinds[].indexEnabled=false`）
- taxonomy 置顶字段配置：
  - 全局字段：`taxonomy.pinField`（默认 `pinned`）、`taxonomy.pinOrderField`（可选）
  - 多数据源字段映射：`taxonomy.pinFieldBySource[sourceKey]`、`taxonomy.pinOrderFieldBySource[sourceKey]`
  - 未配置 bySource 时，所有数据源统一使用全局字段名

### term 元数据（v3.0.0+）

每个 taxonomy term 可携带额外元数据，支持两种来源：

1. **data 模式数据源**（`content/data/tags.yaml` 等）：
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10          # 排序权重，越大越靠前（默认 0）
  parent: tech        # 父级 term slug（层次化分类）
```

2. **_index.md 约定**（仿 Hugo）：`content/_taxonomy/<kind>/<slug>/_index.md`

```yaml
---
description: Everything about ML and AI
image: /assets/images/ml-cover.png
weight: 10
parent: tech
---
```

### 层次化分类（v3.0.0+）

通过 `taxonomy.kinds[].hierarchical: true` 启用，自动计算父子关系：

```yaml
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true
```

启用后：
- 每个 term 自动计算 `children`（直接子级）和 `ancestors`（祖先链，从根到当前）
- 模板中可用于面包屑导航：`page.fields.taxonomy.value.children` / `ancestors`
- JSON 输出 `taxonomy.json` 也包含 `children` 和 `ancestors` 数组

### term 可见性控制

设置 `IsVisible: false` 可隐藏内部使用的 term（不会出现在索引页的 `terms.value[]` 中，但详情页仍可访问）。

### RSS feeds for taxonomy terms（v3.0.0+）

每个 term 自动生成独立 RSS 2.0 feed：`<output>/<kind>/<slug>/feed.xml`

### 别名重定向（v3.0.0+）

term 的 `Aliases` 字段配置的别名会自动生成 HTML redirect 页面：
`<output>/<kind>/<alias_slug>/index.html` → redirect to `/<kind>/<slug>/`

### term 排序规则

- 索引页和 JSON 输出中，term 按 `Weight` 降序（权重越大越靠前），同权重按 DisplayName 升序
- 不可见 term（`IsVisible=false`）不会出现在索引页中

Notion 补充：
- taxonomy 只看 meta，不看 `page.fields.*`；因此 Notion 的 `tags/categories` 建议优先使用 `multi_select`
- 如果你的 Notion `tags/categories` 使用 `relation`，Notion provider 会把 relation 目标页的 `title`（回退 `slug`，再回退 `id`）提升为 `meta.tags/meta.categories` 的 term 列表，确保 taxonomy 生成可读的分类/标签
- 当 relation 目标页不在当前 database query 结果里时，会额外请求 Notion `/v1/pages/{id}` 补齐目标页 title/slug（最多 200 个，避免请求爆炸）
- 空分类/空标签页自动生成（避免点击后 404）：
  - 如果存在 `mode: data` 且 `name: categories`（或 `name: tags`）的内容源，引擎会把该数据源的条目作为 taxonomy term 列表；即使该 term 当前没有任何文章引用，也会生成对应的 term 页（slug 优先取条目的条目 slug）。
  - 如果使用 Notion 内容源，引擎会从 Notion 数据库 schema 中提取 `select/multi_select/status` 的 `options[].name`，自动确保 `tags/categories`（以及 `taxonomy.kinds[].key` 对应字段）的 term 页存在。

模板示例（taxonomy term 页分页）：
```scriban
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <ul>
  {{ for item in page.fields.items.value }}
    <li>
      <a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a>
      {{ if item.publish_date }}
        <small>{{ item.publish_date | date.to_string "%Y-%m-%d" }}</small>
      {{ end }}
    </li>
  {{ end }}
  </ul>

  <nav class="pagination">
    {{ if page.fields.pagination.value.has_prev }}
      <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page - 1 }}/">Prev</a>
    {{ end }}
    <span>Page {{ page.fields.pagination.value.page }} / {{ page.fields.pagination.value.total_pages }}</span>
    {{ if page.fields.pagination.value.has_next }}
      <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page + 1 }}/">Next</a>
    {{ end }}
  </nav>
</article>
```

## pages-index（IDerivePagesPlugin）

文件：`PagesIndexPlugin.cs`

生成一个“全站按 id 索引”的结构化数据，注入到模板变量：

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`

用途：
- 当模板里只有一个 pageId（例如 Notion relation 返回的 id 列表）时，可以用它查到该页面的 URL/标题等信息

说明：
- pages-index 与内容源无关：只要构建能产出 routed 内容页（posts/pages 等），就会进入索引
- 该索引只覆盖 routed 内容页，不包含 derived 路由（taxonomy/pagination/archive 等）
- `mode: data` 的内容项不会生成 routed 页面，因此不会进入 `pages_by_id`（除非被 Notion 补全写入）
- 可选：对 Notion relation 的 pageId 做“批量补全”，把不在本站的页面也加入索引（需要 `NOTION_TOKEN`）：
- 补全只发生在构建阶段（derive-pages），模板里读取 `site.data.pages_by_id[...]` 不会触发 API 请求
- 补全得到的页面会自动解析 Notion properties 到 `fields`（无需额外指定字段名）
- 仅当站点使用 Notion 内容源时才会启用补全；其他内容源下该配置会被忽略
- `field_keys`：指定要扫描哪些字段来收集 relation pageId（字段值应为 `page.fields.<key>.value[]` 的 id 列表）。不指定则不会做任何补全，只会生成本站 routed 页面索引。
- 补全的页面会自动提取 Notion 页面顶层 `cover` 和 `icon` 字段，注入到 `fields` 中（与主内容管道的 `InjectPageCoverAndIcon` 行为一致）
- 补全的页面字段中的图片 URL（cover、icon 等 `content.media.fieldKeys` 指定的字段）会自动通过 `ImageAssetLocalizer` 下载到本地并重写为本地路径，避免产出页面仍引用 Notion S3 临时 URL
- relation ID 匹配支持带源前缀的键格式（如 `posts_content:pageId`）：如果某个 pageId 已经以 `sourceKey:pageId` 的形式存在于索引中，则不会重复发起 Notion API 请求

```yaml
theme:
  params:
    pages_index:
      resolve_notion:
        enabled: true
        field_keys: ["related_posts", "payments", "categories"]
        max_items: 200
        concurrency: 4
        max_rps: 3
        max_retries: 5
        request_delay_ms: 0
        cache_mode: readwrite   # off | readwrite | readonly
        cache_path: .cache/notion/pages-index.json
```

## pagination（IDerivePagesPlugin）

文件：`PaginationPlugin.cs`

当 blog 文章数超过 pageSize 时，为每个启用分页的 collection 派生分页页：

- `/blog/page/2/` → `blog/page/2/index.html`
- …直到最后一页

说明：
- 派生页使用模板：优先 `pages/pagination.html`，回落 `pages/page.html`
- 页面内容由插件生成（包含 Prev/Next 链接）
- 支持多 collection 独立分页（v3.0+）：
  - 每个 `pagination.enabled: true` 的 collection 独立生成分页页
  - `pagination.pageSize`：每页条目数，默认 10
  - `pagination.urlPattern`：URL 模式，`:num` 占位符（默认 `page/:num/`，可设为 `p/:num/`）
  - `pagination.firstPageUsesListRoute`：第 1 页是否使用 listRoute（默认 true）
- 注入字段：
  - `page.fields.items.value[]`：当前页文章列表（`{title, url, publish_date, summary?}`）
  - `page.fields.pagination.value`：`{page, page_size, total_pages, has_prev, has_next}`

## archive（IDerivePagesPlugin）

文件：`ArchivePlugin.cs`

按内容发布时间派生归档页：

- `/blog/archive/` → 归档总索引页
- `/blog/archive/<year>/` → 年份页
- `/blog/archive/<year>/<month>/` → 月份页
- `/blog/archive/<year>/<month>/<day>/` → 日页（v3.0+，`depth: daily`）

说明：
- 派生页使用模板：默认 `pages/page.html`（v3.0+ 可通过 `collection.output.archiveDetail.template` 自定义）
- 页面内容由插件生成（ul/li 链接列表）
- 增强配置（v3.0+）：
  - `collection.output.archiveDetail.depth`：`yearly` / `monthly`（默认）/ `daily`
  - `collection.output.archiveDetail.template`：自定义模板路径
  - `collection.output.archiveDetail.routePrefix`：自定义 URL 前缀（默认 `archive`）

## path-report（IAfterBuildPlugin，外部插件）

文件：`src/plugins/PathReportPlugin/PathReportPlugin.cs`

调试用插件，构建后生成路径审计报告。

- 输出：`<outputDir>/_debug/paths-report.json`
- Order：`int.MaxValue`（最后执行）
- 报告内容：rootDir、cacheDir、distDir、themeRoot、layoutsDir、assetsDir，以及各目录下的文件列表

### 配置

```yaml
site:
  plugins:
    path-report:
      enabled: true
      options:
        wechatMaterialUpload:
          enabled: false
          file: assets/imgs/default.png
          type: image
          wechat:
            appIdEnv: WECHAT_APP_ID
            appSecretEnv: WECHAT_APP_SECRET
```

| 选项 | 类型 | 默认值 | 说明 |
|---|---:|---|---|
| `wechatMaterialUpload.enabled` | bool | `false` | 是否在构建后上传素材到微信公众号 |
| `wechatMaterialUpload.file` | string | `assets/imgs/default.png` | 要上传的文件（相对于输出目录） |
| `wechatMaterialUpload.type` | string | `image` | 素材类型 |
| `wechatMaterialUpload.wechat.appIdEnv` | string | - | 存放 AppID 的环境变量名 |
| `wechatMaterialUpload.wechat.appSecretEnv` | string | - | 存放 AppSecret 的环境变量名 |

注意：上传的文件路径受安全约束，不能逃逸出输出目录。

## llms-txt（publish projection adapter）

来源：`PublishRepresentationRegistry` adapter 复用 `LlmsTxtPlugin` writer helpers。

生成面向 AI 友好的站点产物，用于生成式引擎优化（GEO）：

- **llms.txt**：遵循 [llmstxt.org](https://llmstxt.org) 标准的 Markdown 索引文件，包含 Documentation、Articles 和 Optional 节。由 `site.seo.geo.llmsTxt` 控制（默认：true）。文章数量限制为 `site.seo.geo.llmsTxtMaxArticles`（默认：20）。
- **llms-full.txt**：所有可索引页面的全文导出（去除 HTML）。由 `site.seo.geo.llmsFullTxt` 控制（默认：false）。
- **AI 爬虫 robots.txt 规则**：为已知 AI 爬虫 user-agent（GPTBot、ChatGPT-User、Google-Extended、Claude-Web、ClaudeBot、Anthropic-AI、PerplexityBot、Cohere-AI、CCBot、Diffbot、FacebookBot、OAI-SearchBot）添加 `Allow`/`Disallow` 指令。由 `site.seo.geo.aiBotMode`（`allow`/`block`/`selective`）控制。

配置示例：

```yaml
site:
  seo:
    geo:
      enabled: true
      llmsTxt: true
      llmsFullTxt: false
      llmsTxtMaxArticles: 20
      aiBotMode: allow
      aiBotAllowList: [GPTBot, PerplexityBot]
      aiBotBlockList: [CCBot]
```

相关文档：[GEO 架构](./geo.zh-CN.md)

## related-content（IDerivePagesPlugin，v3.0+）

文件：`RelatedContentPlugin.cs`

根据 tags/categories/keywords/collection/date 多维度加权匹配，为每篇文章计算相关内容：

- 配置：`site.related.enabled: true` 启用
- `site.related.threshold`：最低分数阈值（默认 80）
- `site.related.limit`：每页最多推荐数（默认 5）
- `site.related.indices`：匹配维度与权重，默认 tags(80) + categories(60)
- 支持的维度：`tags`、`categories`、`keywords`、`collection`/`type`、`date`
- 数据注入：`context.Data["__related_pages"]`，按 content item ID 索引的字典
- 排除规则：自动跳过 archive 和 pagination 派生页

## alias（IDerivePagesPlugin，v3.0+）

文件：`AliasPlugin.cs`

根据 front matter `aliases` 字段生成 HTML redirect 页面：

- 每个 alias 生成一个 HTML 文件，包含 `<meta http-equiv="refresh">` 和 `<link rel="canonical">`
- 支持单个字符串或列表：`aliases: /old-url/` 或 `aliases: [/old1/, /old2/]`
- URL 自动规范化（补全首尾 `/`）
- 生成页面 marked 为 `type: redirect`，自动排除 sitemap

## data-files（IDerivePagesPlugin，v3.0+）

文件：`DataFilesPlugin.cs`

加载 `data/` 目录下的 YAML/JSON/TOML 数据文件：

- 数据注入：`context.Data["__data_files"]`
- 支持嵌套子目录（递归加载）
- 多语言支持：`data/{lang}/` 子目录按语言加载
- 多语言时：共享根级文件 + 语言特定覆盖

## menu（IAfterBuildPlugin，v3.0+）

文件：`MenuPlugin.cs`

输出 `menus.json` 并注入 `context.Data["menus"]`：

- 配置：`site.menus.main` / `site.menus.footer` 等多菜单
- 支持无限层级嵌套（`children` 字段）
- 按 `weight` 排序（权重越小越靠前）
- 模板中通过 `site.menus.main` / `site.menus.footer` 访问

## image-processing（IAfterBuildPlugin，v3.0+）

文件：`ImageProcessingPlugin.cs`

基于 CLI 工具（ImageMagick）的图片多尺寸变体生成：

- 配置：`theme.images.enabled: true` 启用
- 对 `assets/` 下的 JPG/PNG 图片生成多尺寸变体（如 `-480w`、`-768w`、`-1200w`）
- `theme.images.sizes`：尺寸列表，默认 `[480, 768, 1200]`
- `theme.images.quality`：图片质量，默认 80
- 数据注入：`context.Data["__image_srcsets"]`（srcset 属性数据）
- 依赖：需安装 ImageMagick（`magick` 或 `convert` 命令）；未安装时跳过并输出警告

## 派生页路由校验

所有 derive-pages 插件（Pagination、Archive、Taxonomy）共享同一路由校验管线：

1. **逐插件冲突检查** — `PluginRunner.ApplyDeriveConflictPolicy` 对每个派生页进行规范化 URL 和 outputPath 比较，检查是否与内容路由和已接受的派生路由冲突。
2. **最终清单校验** — `RouteInventoryValidator.ValidateFinalRoutes` 在渲染开始前检查完整路由集（内容 + 派生 + 列表路由）。
3. **Doctor 集成** — `bukit doctor` 通过 `RouteInventoryValidator.BuildContentRoutesAsync` + `ValidateContentRoutes` 运行内容路由校验，无需完整构建即可检测冲突。

所有派生页均遵循 `site.outputPathEncoding`（通过 `RoutePathBuilder.BuildOutputPathFromUrl` 应用）。
