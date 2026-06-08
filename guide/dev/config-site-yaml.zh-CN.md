# 配置（site.yaml）字段参考

本文档是 `site.yaml` 的权威字段参考，来源于：
- 配置模型：`src/Bukit.Config/AppConfig.cs`
- 加载逻辑：`src/Bukit.Config/ConfigLoader.cs`
- 校验规则：`src/Bukit.Config/ConfigValidator.cs`

示例配置：
- `examples/starter/site.yaml`

## 顶层结构

```yaml
site: {}
content: {}
build: {}
theme: {}
taxonomy: {}
logging: {}
```

## 路径字段通用校验规则

以下路径类字段均受 `ConfigValidator.RejectPathTraversal` 约束：

- 必须为相对路径（不能以 `/` 或驱动器号开头）
- 不能包含 `..` 路径遍历段

适用字段：`build.output`, `theme.layouts`, `theme.assets`, `theme.static`, `theme.name`, `content.media.downloadDir`, `content.markdown.dir`, `content.markdown.includePaths[]`

违反时报错示例：`"{fieldName} must be a relative path."` 或 `"{fieldName} must not contain '..' path traversal segments."`

## site

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `site.name` | string | 是 | - | 站点内部标识 |
| `site.title` | string | 是 | - | 站点标题（模板变量 `site.title`） |
| `site.url` | string | 否 | null | 站点绝对 URL（用于 sitemap/rss）；必须以 `http://` 或 `https://` 开头 |
| `site.description` | string | 否 | null | 站点描述（模板变量 `site.description`） |
| `site.autoSummary` | bool | 否 | false | 未提供 `meta.summary` 时是否从正文提取摘要并回填。环境变量 `BUKIT_AUTO_SUMMARY=1/true/yes` 可强制开启 |
| `site.autoSummaryMaxLength` | int | 否 | 200 | 自动摘要最大长度（字符数）；校验值域 1--5000。环境变量 `BUKIT_AUTO_SUMMARY_MAXLEN` 可覆盖 |
| `site.baseUrl` | string | 是 | `/` | GitHub Pages 子路径（例如 `/my-repo`）；必须以 `/` 开头 |
| `site.outputPathEncoding` | string | 否 | `none` | `none` \| `slug` \| `urlencode` \| `sanitize`（`sanitize`：空格替换为 `-`，移除 `<>:"|?*` 和控制字符，连续 `-` 压缩，段末 `.`/空格移除）。对内容页和派生页（分页、归档、分类）均生效。 |
| `site.language` | string | 否 | `zh-CN` | 单语言模式下的语言标识 |
| `site.languages` | string[] | 否 | null | 多语言输出（例如 `["zh-CN","en-US"]`）；数组非空时至少包含一项非空字符串；不可重复（忽略大小写） |
| `site.defaultLanguage` | string | 否 | `site.languages[0]` | 必须包含在 `site.languages` 中 |
| `site.sitemapMode` | string | 否 | `split` | `split` \| `merged` \| `index` |
| `site.search.mode` | string | 否 | `split` | `split` \| `merged` \| `index` |
| `site.searchIncludeDerived` | bool | 否 | false | 是否把插件派生页纳入搜索索引（语义见 SearchIndex 插件） |
| `site.pluginFailMode` | string | 否 | `strict` | `strict`（插件失败中断构建）\| `warn`（记录错误继续） |
| `site.plugins` | object | 否 | null | 插件开关与配置；支持 `site.plugins.<name>: bool` 或 `site.plugins.<name>.enabled/options` |
| `site.deriveConflictPolicy` | string | 否 | `fail` | 派生页路由冲突策略：`fail`（中断）\| `warn`（跳过+告警）\| `last-wins`（派生页覆盖）。内容页之间的冲突始终报错，不受此设置影响。 |
| `site.externalAssemblyTrustMode` | string | 否 | `warn` | 外部 DLL 信任模式：`warn`（告警但允许）\| `strict`（仅 allowlist 内 DLL 可用） |
| `site.externalAssemblyAllowlist` | object | 否 | null | 文件名 → SHA256 的映射表（用于 DLL 白名单校验） |
| `site.externalProtocolIncludeRoutedPages` | bool | 否 | false | 是否将完整 routedPages 传递给 after-build 阶段的协议插件 |
| `site.timezone` | string | 否 | `Asia/Shanghai` | 时间相关处理的默认时区；必须为有效的 IANA/Windows 时区标识符 |
| `site.permalinks` | object | 否 | null | 按内容类型自定义 URL 模式；键为类型名（如 `post`），值为 URL 模式字符串（支持 `{year}/{month}/{day}/{slug}/{type}` 占位符）；详见 [路由系统](./routing.zh-CN.md) |
| `site.collections` | object | 否 | null | collection 驱动路由配置。每个集合至少声明 `permalink` 与 `template`，可选 `listRoute`、`pagination`、`output`；字段校验改用 `content.modelSchema.fieldScopes` |

### site.collections（collection 驱动路由）

示例：

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
      output:
        rss: true
        sitemap: true
        archive: true
      schema:
        - name: author
          type: string
          label: 作者
          required: true
          default: ""
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

`schema` 字段定义集合的自定义字段结构，每项包含：

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `name` | string | 是 | — | 字段名 |
| `type` | string | 否 | `string` | `string` \| `number` \| `bool` \| `date` \| `list` |
| `label` | string | 否 | — | 字段显示标签 |
| `required` | bool | 否 | false | 是否必填 |
| `default` | any | 否 | — | 默认值 |

### site.plugins（插件开关与配置）

支持两种写法：

```yaml
site:
  plugins:
    sitemap: false
```

```yaml
site:
  plugins:
    path-report:
      enabled: true
      options: {}
```

规则：
- `site.plugins` 的键必须为非空字符串。
- `site.plugins.<name>: bool` 兼容旧写法，仅控制开关。
- `site.plugins.<name>.enabled` 默认 `true`。
- `site.plugins.<name>.options` 为对象，供插件读取自定义参数（键值结构由插件自行定义）。

## content

content 在 Bukit 1.0 只支持 `content.sources[]`。`content.provider` 是移除字段，出现时会被拒绝。

### content.sources[]

`content.sources[]` 是唯一内容入口。即使只有一个 Markdown 或 Notion 来源，也必须写成 sources 列表。

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `content.sources[].type` | string | 否 | - | 可选兼容字段；1.0 starter 不写，推荐通过 `markdown` 或 `notion` 子对象表达来源 |
| `content.sources[].name` | string | 否 | null | 可选名称；若填写必须唯一 |
| `content.sources[].mode` | string | 否 | `content` | `content`（生成路由并渲染）\| `data`（不生成路由，只注入 `site.modules`） |
| `content.sources[].notion` | object | 视 type | - | type=notion 必填 |
| `content.sources[].markdown` | object | 视 type | - | type=markdown 必填 |

### content.sources[].notion

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `databaseId` | string | 是 | - | Notion Database ID |
| `pageSize` | int | 否 | 50 | Notion query page_size |
| `maxItems` | int | 否 | null | 最多拉取条数（正整数）；达到即停止 |
| `renderContent` | bool | 否 | null | 是否渲染正文；未设置时由内部策略决定（通常为 true） |
| `fieldPolicy.mode` | string | 否 | `whitelist` | `whitelist` \| `all` |
| `fieldPolicy.allowed` | string[] | 否 | null | whitelist 模式下允许进入 `page.fields` 的字段列表 |
| `filterProperty` | string | 否 | `Published` | 过滤字段名（配合 filterType） |
| `filterType` | string | 否 | `checkbox_true` | `checkbox_true` \| `none` |
| `sortProperty` | string | 否 | null | 排序字段名 |
| `sortDirection` | string | 否 | `ascending` | `ascending` \| `descending`（只有设置 sortProperty 才生效） |
| `includeSlugs` | string[] | 否 | null | 指定 slug 列表，仅拉取这些页面（数据库 query 过滤） |
| `includeSlugProperty` | string | 否 | `Slug` | includeSlugs 对应字段名（当前过滤使用 rich_text.equals）；当 `includeSlugs` 非空时必填 |
| `cacheMode` | string | 否 | `off` | `off` \| `readwrite` \| `readonly`（Notion 正文渲染缓存） |
| `cacheDir` | string | 否 | null | 缓存目录（相对 config 所在目录；不填时默认 `<rootDir>/.cache/notion`）；设置时必须为非空字符串 |
| `renderConcurrency` | int | 否 | null | 正文渲染并发度（正整数；默认本地 4、CI 2） |
| `maxRps` | int | 否 | null | Notion 请求全局限速（正整数；默认 3，包含 query + blocks） |
| `maxRetries` | int | 否 | null | 429 最大重试次数（非负整数；遵循 Retry-After 退避） |

注意（Breaking Change）：
- 媒体本地化相关配置已从 `content.notion` 移除，不再读取：
  - `downloadImagesToLocal`
  - `imageDownloadDir`
  - `imageUrlBase`
  - `defaultImageUrl`
- 统一改为 `content.media`（见下节），且不做兼容回退。

校验与运行时约束：
- Notion token 必须来自环境变量：`NOTION_TOKEN`（缺失会直接报错）
- filterType!=none 时，filterProperty 必须非空

运行时观测：
- Notion 内容源加载结束会输出 `event=notion.stats`，用于查看请求总数与限流等待情况：
  - `requests`：Notion HTTP 请求总数（包含 query、blocks、429 重试）
  - `throttle_wait_count` / `throttle_wait_ms`：因 `maxRps` 节流带来的等待次数/累计毫秒

### content.sources[].markdown

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `dir` | string | 否 | `content` | Markdown 内容目录（相对 config 所在目录） |
| `defaultType` | string | 否 | null | 未指定 type 时注入的可选元数据；不作为核心默认路由依据 |
| `maxItems` | int | 否 | null | 最多读取多少篇（正整数；按路径排序后截断） |
| `includePaths` | string[] | 否 | null | 只读取指定路径（相对 dir；可省略 `.md`） |
| `includeGlobs` | string[] | 否 | null | 只读取匹配的 glob（匹配相对路径，分隔符使用 `/`）；每项必须为非空字符串 |

### content.media（统一媒体本地化）

`content.media` 对所有内容源生效（Notion / Markdown / 未来新增 source）。

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `downloadToLocal` | bool | 否 | `true` | 是否把远程图片下载到本地 assets |
| `downloadDir` | string | 否 | `assets/uploads` | 下载目录（相对 config 所在目录）；若为默认值，实际落到当前主题 assets 下的 `uploads` |
| `urlBase` | string | 否 | `/assets/uploads` | 渲染后图片 URL 前缀 |
| `defaultImageUrl` | string | 否 | `/assets/images/noneimg-news.jpg` | 图片缺失或下载失败时回退地址 |
| `fieldKeys` | string[] | 否 | `["cover","image","thumbnail","og_image","icon"]` | 对 `page.fields` 中哪些 key 执行图片 URL 本地化 |
| `maxConcurrency` | int | 否 | `4` | 图片本地化并发数（正整数） |
| `maxRetries` | int | 否 | `3` | 下载失败最大重试次数（非负整数） |
| `timeoutMs` | int | 否 | `10000` | 单次下载超时时间（毫秒，正整数） |
| `maxFileSizeBytes` | long | 否 | `52428800` | 单个图片最大文件大小（字节，默认 50MB）；超过该大小的远程图片会被跳过 |
| `blockPrivateNetworks` | bool | 否 | `true` | 是否拦截对私有网络地址（127.0.0.0/8、10.0.0.0/8、172.16.0.0/12、192.168.0.0/16 等）的图片下载请求（SSRF 防护） |
| `retryBaseDelayMs` | int | 否 | `500` | 下载重试的基础延迟时间（毫秒，非负整数）；实际延迟按指数退避计算 |

示例：

```yaml
content:
  sources:
    - type: notion
      notion:
        databaseId: "..."
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/noneimg-news.jpg
    fieldKeys: [cover, image, thumbnail, og_image, icon]
    maxConcurrency: 4
    maxRetries: 3
    timeoutMs: 10000
```

## build

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `build.output` | string | 是 | `dist` | 输出目录（相对 config 所在目录） |
| `build.clean` | bool | 否 | true | 构建前清理输出目录 |
| `build.draft` | bool | 否 | false | 是否渲染草稿（草稿规则见内容系统） |
| `build.listPageContentMode` | string | 否 | `auto` | 固定列表页中 `pages[*].content` 的装配模式：`auto` \| `always` \| `never` |

`build.listPageContentMode` 只影响引擎固定生成的 3 个列表页：

- `/`
- `/blog/`
- `/pages/`

行为说明：

- `auto`：优先读取 `layouts/bukit.templates.yaml` 中对模板能力的显式声明；未声明时再回退到兼容性启发式
- `always`：总是为列表页装配 `pages[*].content`
- `never`：列表页中的 `pages[*].content` 为空字符串

`bukit.templates.yaml` 已经从“列表页正文声明”扩展为通用模板能力清单；当前引擎实际消费的字段仍是 `needs_page_content`，但同一文件也可声明 `supports_pagination`、`supports_taxonomy`、`supports_search_snippets` 等能力，用于主题自描述和 doctor 校验。详见：[template-capabilities.md](./template-capabilities.zh-CN.md)

推荐实践：

- 列表卡片优先使用 `summary`
- 只有明确需要正文片段时，才让列表页依赖 `content`
- 若主题明确需要列表页正文，建议在 `layouts/bukit.templates.yaml` 中声明模板能力

## theme

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `theme.name` | string | 否 | null | 主题名（与 `themes/<name>` 配合使用） |
| `theme.layouts` | string | 否 | `layouts` | 模板目录 |
| `theme.assets` | string | 否 | `assets` | 资源目录（会拷贝到输出的 `assets/`） |
| `theme.static` | string | 否 | `static` | 静态目录（会原样拷贝到输出根） |
| `theme.params` | object | 否 | null | 任意参数字典，注入模板变量 `site.params` |
| `theme.extends` | string | 否 | — | 父主题名。子主题级联父主题的模板、静态文件、资源 |
| `theme.shortcodes` | map<string, string> | 否 | — | 可复用 HTML 片段（Markdown `{% name %}` / Scriban `{{ shortcode }}`） |
| `theme.components` | map<string, object> | 否 | — | 带 props 的可复用模板组件（Scriban `{{ comp.render }}`） |
| `theme.scss` | object | 否 | — | SCSS 编译配置 `{enabled, entryPoint, outputDir}` |
| `theme.images` | object | 否 | — | 图片优化配置 `{enabled, formats, sizes, quality}` |

`theme.layouts`, `theme.assets`, `theme.static`, `theme.name` 均受路径通用规则约束（见"路径字段通用校验规则"一节）。

## taxonomy

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `taxonomy.template` | string | 否 | `pages/page.html` | taxonomy 派生页模板默认值（用于 index/term） |
| `taxonomy.indexTemplate` | string | 否 | null | taxonomy 索引页模板（例如 `/tags/`、`/categories/`）；为空时回退到 `taxonomy.template` |
| `taxonomy.termTemplate` | string | 否 | null | taxonomy 具体项页模板（例如 `/tags/<slug>/`、`/categories/<slug>/`）；为空时回退到 `taxonomy.template` |
| `taxonomy.kinds` | list | 否 | null | 通用化 taxonomy 定义列表；配置后将按列表循环生成任意 kind（不再仅限 tags/categories）。每项至少包含 `key`，可选 `kind/title/singularTitlePrefix/template/indexTemplate/termTemplate/indexEnabled/hierarchical` |
| `taxonomy.kinds[].hierarchical` | bool | 否 | false | (v3.0.0+) 是否启用层次化分类。启用后自动计算每个 term 的 `children` 和 `ancestors`，写入模板变量和 JSON 输出 |
| `taxonomy.templates.tags.template` | string | 否 | null | tags 派生页默认模板（为空时回退到 `taxonomy.template`） |
| `taxonomy.templates.tags.indexTemplate` | string | 否 | null | tags 索引页模板（为空时回退到 `taxonomy.indexTemplate` 或 `taxonomy.templates.tags.template`） |
| `taxonomy.templates.tags.termTemplate` | string | 否 | null | tags 具体项页模板（为空时回退到 `taxonomy.termTemplate` 或 `taxonomy.templates.tags.template`） |
| `taxonomy.templates.categories.template` | string | 否 | null | categories 派生页默认模板（为空时回退到 `taxonomy.template`） |
| `taxonomy.templates.categories.indexTemplate` | string | 否 | null | categories 索引页模板（为空时回退到 `taxonomy.indexTemplate` 或 `taxonomy.templates.categories.template`） |
| `taxonomy.templates.categories.termTemplate` | string | 否 | null | categories 具体项页模板（为空时回退到 `taxonomy.termTemplate` 或 `taxonomy.templates.categories.template`） |
| `taxonomy.outputMode` | string | 否 | `both` | taxonomy 输出模式：`both`（同时生成页面和结构化数据）\| `pages`（仅生成 HTML 页面）\| `data`（仅生成 JSON 数据）\| `fields_only`（仅注入 fields，不生成任何文件） |
| `taxonomy.itemFields` | string[] | 否 | null | term 页条目暴露哪些 fields（如 `[cover, image, date]`）；每项必须为非空字符串；未配置时条目仅包含基础信息（title/url/summary 等） |
| `taxonomy.pageSize` | int | 否 | 10 | taxonomy term 页分页大小（分类/标签详情页） |
| `taxonomy.indexEnabled` | bool | 否 | true | 是否生成 taxonomy 索引页（例如 `/tags/`、`/categories/`） |
| `taxonomy.pinField` | string | 否 | `pinned` | 置顶字段名；term 页中该字段为 true 的条目排在最前 |
| `taxonomy.pinOrderField` | string | 否 | null | 置顶排序字段名；置顶条目按此字段升序排列后再按 `publishAt` 倒序 |
| `taxonomy.pinFieldBySource` | object | 否 | null | 多数据源置顶字段映射（键为 sourceKey，值为字段名）；未配置时使用全局 `pinField` |
| `taxonomy.pinOrderFieldBySource` | object | 否 | null | 多数据源置顶排序字段映射（键为 sourceKey，值为字段名）；未配置时使用全局 `pinOrderField` |

说明：
- `taxonomy.kinds` 是 1.0 的标准 taxonomy 配置方式。1.0 文档与示例应显式列出所需 kind（例如 `tags` / `categories`）。
- `taxonomy.templates.<kind>.*` 的旧 fallback 已改为迁移语境；在 1.0 运行口径中不再作为默认行为。
- `taxonomy.kinds[]` 校验：`key` 必填；`kind`, `title`, `singularTitlePrefix`, `template`, `indexTemplate`, `termTemplate` 均为可选，但设置时必须为非空字符串。
- `taxonomy.kinds[].hierarchical`：启用后自动计算层次关系。term 通过 `parent` 元数据（data 源或 `_index.md`）关联父级；无 `parent` 的 term 为根节点。
- term 元数据支持两种加载源：
  1. **data 模式数据源**：`taxonomy_ensure_terms` 字典中的条目（如 `content/data/tags.yaml`），支持 `description`、`image`、`weight`、`parent` 字段
  2. **_index.md 约定**（仿 Hugo）：`content/_taxonomy/<kind>/<slug>/_index.md`，YAML front matter 格式
- RSS feeds：每个 term 自动生成 `<output>/<kind>/<slug>/feed.xml`
- 别名重定向：`Aliases` 配置的 term 自动生成 HTML redirect 页面

完整示例：

```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/taxonomy-index.html
  termTemplate: pages/taxonomy-term.html
  kinds:
    - key: tags
      kind: tags
      title: Tags
      singularTitlePrefix: Tag
      termTemplate: pages/tag.html
    - key: categories
      kind: categories
      title: Categories
      singularTitlePrefix: Category
      termTemplate: pages/category.html
    - key: series
      kind: series
      title: Series
      singularTitlePrefix: Series
      template: pages/series.html
  templates:
    tags:
      template: pages/tag.html
      indexTemplate: pages/tag-index.html
      termTemplate: pages/tag-term.html
    categories:
      template: pages/category.html
      indexTemplate: pages/category-index.html
      termTemplate: pages/category-term.html
```

优先级规则（从高到低）：
1. `taxonomy.templates.<kind>.indexTemplate` / `taxonomy.templates.<kind>.termTemplate`
2. `taxonomy.indexTemplate` / `taxonomy.termTemplate`
3. `taxonomy.templates.<kind>.template`
4. `taxonomy.template`
5. 默认 `pages/page.html`

## logging

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---:|---:|---|---|
| `logging.level` | string | 否 | `info` | `debug` \| `info` \| `warn` \| `error`（CI 模式可能被提升为 warn） |

校验：`logging.level` 必须为 `debug`、`info`、`warn`、`error` 之一，其他值会导致配置校验失败。
