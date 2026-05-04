# 内置插件（BuiltIn）产物与边界

本页描述内置插件的“输出契约”（会生成哪些文件/页面、依赖哪些配置、在多语言下如何表现）。当你修改插件或依赖它们的主题时，应当优先维护本页以避免行为漂移。

内置插件实现目录：`src/Bukit.Engine/Plugins/BuiltIn/`

相关文档：
- [插件体系](./plugins.md)
- [多语言与 SEO](./i18n-seo.md)
- [引擎固定产物](./engine-outputs.md)

## sitemap（IAfterBuildPlugin）

文件：`SitemapPlugin.cs`

- 输出：`<outputDir>/sitemap.xml`
- 依赖：必须配置 `site.url`（否则直接跳过不生成）
- 包含路由：
  - 引擎固定页：`/`、`/blog/`、`/pages/`
  - 所有 routed 内容页
  - 所有 derived 路由（来自 derive-pages 插件，例如 taxonomy/pagination/archive）
- lastmod 规则：
  - routed 内容页：优先使用 `fields.update_time`（可解析日期），取不到则回退到 `publishAt`
  - derived 路由：使用各 derive-pages 插件返回的 `LastModified`
- 屏蔽规则（基于最终 HTML meta）：
  - 若页面 HTML 含 `<meta name="robots" content="noindex|none ...">`，该页面会被从 sitemap 中剔除
  - 兼容：`<meta name="sitemap" content="exclude|noindex|false|0">`

多语言行为：
- 当 `site.languages` 非空且 `site.sitemapMode == merged`：该插件会在语言子目录跳过生成（由引擎在根目录生成 merged sitemap）
- 其他模式：各语言输出目录各自生成 `sitemap.xml`

## rss（IAfterBuildPlugin）

文件：`RssPlugin.cs`

- 输出：`<outputDir>/rss.xml`
- 依赖：必须配置 `site.url`（否则直接跳过不生成）
- 输入：只使用 routed 内容（不包含 derived）

多语言行为：
- 当 `site.languages` 非空且 `site.rssMode == merged`：该插件会在语言子目录跳过生成（由引擎在根目录生成 merged rss）
- 其他模式：各语言输出目录各自生成 `rss.xml`

## search-index（IAfterBuildPlugin）

文件：`SearchIndexPlugin.cs`

- 输出：`<outputDir>/search.json`
- 依赖：不依赖 `site.url`（可在纯相对链接站点使用）
- 内容字段：
  - `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
- `url` 生成规则：用 `site.baseUrl` 拼接页面 `route.url`（结果为站内路径）

是否包含派生页：
- 由 `site.searchIncludeDerived` 控制：
  - false：只索引 routed
  - true：索引 routed + derived

多语言行为：
- 每个语言变体目录都会生成各自的 `search.json`
- 如果 `site.searchMode == index`，引擎会在根目录额外生成 `search.index.json`（聚合指向各语言索引）

## taxonomy（IDerivePagesPlugin + IAfterBuildPlugin）

文件：`TaxonomyPlugin.cs`

根据内容的 `meta.tags` / `meta.categories` 派生页：

- `/tags/` → `tags/index.html`
- `/tags/<slug>/` → `tags/<slug>/index.html`
- `/categories/` → `categories/index.html`
- `/categories/<slug>/` → `categories/<slug>/index.html`

说明：
- 派生页使用模板：默认 `pages/page.html`
- 可配置：`taxonomy.template` / `taxonomy.indexTemplate` / `taxonomy.termTemplate`
- 支持按 kind 覆盖：`taxonomy.templates.tags.*` / `taxonomy.templates.categories.*`
- 优先级：kind 级别 index/term > 全局 index/term > kind 级别 template > 全局 template > 默认 `pages/page.html`
- 页面内容为插件生成的简单 HTML（ul/li 列表），仍会写入 `page.content`（兼容旧主题）
- 同时注入结构化字段（便于主题直接渲染列表，而不是解析 HTML）：
  - index 页（`/tags/`、`/categories/`）：`page.fields.terms.type == "list"`，`page.fields.terms.value[]` 为 `{ title, slug, url, count }`
- term 页（`/tags/<slug>/`、`/categories/<slug>/`）：
  - `page.fields.items.type == "list"`，`page.fields.items.value[]` 为 `{ title, url, publish_date, summary? }`
  - `page.fields.taxonomy.value` 为 `{ kind, term, slug, count }`
  - `page.fields.pagination.value` 为 `{ page, page_size, total, total_pages, has_prev, has_next }`
- term 页 items 排序：
  - 默认按 `publishAt` 倒序
  - 支持置顶：`pinned=true` 的条目会排在最前，然后再按 `publishAt` 倒序
  - 可选置顶顺序：当配置了 `pinOrderField`（或 source 级别的 `pinOrderFieldBySource`）时，置顶条目会先按 `pinOrder` 升序排序，再按 `publishAt` 倒序
  - `pinOrder` 存在时即视为置顶（即使没有显式 `pinned=true`）
- slug 规则：字母数字保留，其余压缩为 `-`（小写）
- term 页支持分页路由：`/<kind>/<slug>/page/<n>/`（pageSize 由 `taxonomy.pageSize` 控制）
- AfterBuild 阶段输出 `taxonomy.json`，包含所有分类维度及其 term 列表的结构化数据
- taxonomy 索引页可禁用：`taxonomy.indexEnabled=false`（或 `taxonomy.kinds[].indexEnabled=false`）
- taxonomy 置顶字段配置：
  - 全局字段：`taxonomy.pinField`（默认 `pinned`）、`taxonomy.pinOrderField`（可选）
  - 多数据源字段映射：`taxonomy.pinFieldBySource[sourceKey]`、`taxonomy.pinOrderFieldBySource[sourceKey]`
  - 未配置 bySource 时，所有数据源统一使用全局字段名

Notion 补充：
- taxonomy 只看 meta，不看 `page.fields.*`；因此 Notion 的 `tags/categories` 建议优先使用 `multi_select`
- 如果你的 Notion `tags/categories` 使用 `relation`，Notion provider 会把 relation 目标页的 `title`（回退 `slug`，再回退 `id`）提升为 `meta.tags/meta.categories` 的 term 列表，确保 taxonomy 生成可读的分类/标签
- 当 relation 目标页不在当前 database query 结果里时，会额外请求 Notion `/v1/pages/{id}` 补齐目标页 title/slug（最多 200 个，避免请求爆炸）
- 空分类/空标签页自动生成（避免点击后 404）：
  - 如果存在 `mode: data` 且 `name: categories`（或 `name: tags`）的内容源，引擎会把该数据源的条目作为 taxonomy term 列表；即使该 term 当前没有任何文章引用，也会生成对应的 term 页（slug 优先取条目的 slug）。
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

当 blog 文章数 > 10 时，派生分页：

- `/blog/page/2/` → `blog/page/2/index.html`
- …直到最后一页

说明：
- 派生页使用模板：`pages/page.html`
- 页面内容由插件生成（包含 Prev/Next 链接）
- pageSize 当前固定为 10（如需可配置化，应当把参数纳入稳定配置字段）

## archive（IDerivePagesPlugin）

文件：`ArchivePlugin.cs`

按年月派生归档页：

- `/blog/archive/` → `blog/archive/index.html`
- `/blog/archive/<year>/` → `blog/archive/<year>/index.html`
- `/blog/archive/<year>/<month>/` → `blog/archive/<year>/<month>/index.html`

说明：
- 派生页使用模板：`pages/page.html`
- 页面内容由插件生成（ul/li 列表，年页链接到月页，月页链接到文章）

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
