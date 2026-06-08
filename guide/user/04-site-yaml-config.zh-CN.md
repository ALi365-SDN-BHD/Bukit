# 04 配置（site.yaml）：字段说明、默认行为与常见写法

`site.yaml` 是你的站点“控制面板”。你可以把它理解成：**内容从哪来、输出到哪、用什么主题、额外生成哪些文件**。

本页面向普通用户，按“最常用场景”解释字段；如果你需要权威字段表与校验细节，请看开发者文档：[guide/dev/config-site-yaml](../dev/config-site-yaml.zh-CN.md)。

## 覆盖优先级（非常重要）

同一个配置项，最终生效的优先级从高到低是：

1. CLI 参数（例如 `--output/--base-url/--site-url/--clean/--draft`）
2. `site.yaml`
3. 引擎默认值

常见误解：你改了 `site.yaml`，但 CLI 里仍然带着 `--output dist2`，所以“看起来没生效”。

## 最小可用配置（Markdown）

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: alt
logging:
  level: info
```

对照可运行示例：`examples/starter/site.yaml`。

## 顶层块：site / content / build / theme / deploy / logging

### site：站点级信息（SEO、多语言、插件策略都在这里）

常用字段（用户最常改的）：

| 字段 | 作用 | 常见示例 |
|---|---|---|
| `site.name` | 站点内部标识（建议全小写、短） | `starter` |
| `site.title` | 展示标题（用于模板/SEO） | `Bukit Starter` |
| `site.baseUrl` | 站点部署子路径（GitHub Pages 常用） | `/` 或 `/my-repo` |
| `site.url` | 站点绝对域名（用于 sitemap/rss） | `https://user.github.io/my-repo` |
| `site.language` | 默认语言 | `zh-CN` |
| `site.languages` | 多语言列表（启用 i18n） | `[zh-CN, en-US]` |
| `site.defaultLanguage` | 多语言下的默认语言 | `zh-CN` |
| `site.timezone` | 时区（影响日期展示与一些默认行为） | `Asia/Shanghai` |
| `site.pluginFailMode` | 插件失败策略 | `strict` / `warn` |
| `site.plugins` | 插件开关与插件参数 | `sitemap: false` 或 `path-report: { enabled: true, options: {...} }` |
| `site.externalPlugins` | 外部进程插件配置 | `my-plugin: { runtime: process, entry: ..., hooks: [...] }`。同时支持 `maxStdoutBytes`/`maxStderrBytes`（输出限制）、`allowEnvironment`（环境变量透传）、`timeoutMs`、`capabilities`（沙箱：`emit-outputs` / `derive-pages`）、`options`。 |
| `site.externalPluginPolicy` | 外部插件安全策略 | `deny` / `warn` / `allow`（默认：`warn`）。`deny` 阻止所有外部插件；`warn` 加载但记录警告；`allow` 静默加载。无效值会导致构建错误（`BKT-0002`）。 |
| `site.autoSummary` | 未提供 summary 时是否从正文提取摘要 | `true` / `false` |
| `site.autoSummaryMaxLength` | 自动摘要最大长度（字符数） | `200` |
| `site.outputPathEncoding` | 输出路径编码策略（处理中文/特殊字符） | `none` / `slug` / `urlencode` / `sanitize` |
| `site.permalinks` | 按类型自定义 URL 结构 | `post: "/{year}/{month}/{slug}/"` |
| `site.collections` | collection 驱动路由配置（推荐） | `post: { permalink, template, listRoute }` |
| `site.seo` | 引擎级 SEO 模型配置 | `enabled/defaultImage/twitterSite/organization` |
| `site.analytics` | 统计代码配置（GA4） | `google_analytics_id: G-...` |

与输出相关的模式（多语言时很关键）：

| 字段 | 作用 | 常见值 |
|---|---|---|
| `site.sitemapMode` | sitemap 输出模式 | `merged` / `split` / `index` |
| `site.searchMode` | search 输出模式 | `merged` / `split` / `index` |

这些模式怎么选见：[11-多语言与SEO](./11-i18n-seo.zh-CN.md)。

### site：v3.0 新增配置（Feed、Sitemap、搜索、相关内容、菜单、分页）

| 字段 | 作用 | 常见值 |
|---|---|---|
| `site.feed.formats` | Feed 格式列表 | `["rss", "atom", "json"]` |
| `site.feed.limit` | 每个 feed 最大条目 | `20` |
| `site.feed.path` | Feed 输出路径前缀 | `feed` |
| `site.sitemapDetail.defaultPriority` | Sitemap 默认 priority | `0.5` |
| `site.sitemapDetail.defaultChangefreq` | Sitemap 默认 changefreq | `weekly` |
| `site.sitemapDetail.imageEnabled` | 启用图片 Sitemap | `true` / `false` |
| `site.sitemapDetail.videoEnabled` | 启用视频 Sitemap | `true` / `false` |
| `site.search.ui` | 内置搜索 UI | `default` / `false` |
| `site.search.uiTheme` | 搜索 UI 主题 | `light` / `dark` / `auto` |
| `site.search.placeholderText` | 搜索框占位文本 | `"搜索..."` |
| `site.related.enabled` | 启用相关内容推荐 | `true` / `false` |
| `site.related.threshold` | 相关度阈值 | `80` |
| `site.related.limit` | 每页最多推荐数 | `5` |
| `site.menus` | 多菜单定义 | 见 [19-新功能](./19-new-features-v3.zh-CN.md) |
| `site.pagination.pageSize` | 全局分页大小 | `10` |

📖 详细用法见：[19-v3.0新增功能](./19-new-features-v3.zh-CN.md)。

### site：SEO 与 Google Analytics（可选）

Bukit 会在构建时为每个页面计算统一的 `page.seo` 模型，主题可以直接渲染 canonical、description、robots、OG、Twitter、hreflang 和 JSON-LD。

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    defaultImage: /assets/og-default.png
    twitterSite: "@your_account"
    organization:
      name: Example Inc
      url: https://example.com/about
      logo: https://example.com/logo.png
  analytics:
    google_analytics_id: G-XXXXXXXXXX
```

字段说明：

| 字段 | 默认值 | 说明 |
|---|---:|---|
| `site.seo.enabled` | `true` | 是否生成 `page.seo` 模型；设为 `false` 后新 SEO partial 不会输出 SEO 标签 |
| `site.seo.defaultImage` | 空 | 页面没有 `og_image/cover/image` 时使用的默认分享图 |
| `site.seo.twitterSite` | 空 | 输出 `twitter:site`，例如 `@your_account` |
| `site.seo.organization.name/url/logo` | 空 | 用于 Organization JSON-LD |
| `site.analytics.enabled` | `true` | 是否允许输出统计代码 |
| `site.analytics.google_analytics_id` | 空 | GA4 Measurement ID，例如 `G-XXXXXXXXXX` |

Analytics 只支持 GA4 `gtag`。只要配置了 `site.analytics.google_analytics_id`，且没有设置 `enabled: false`，新版 starter partial 就会输出 Google Analytics 代码。

如果要关闭统计代码：

```yaml
site:
  analytics:
    enabled: false
    google_analytics_id: G-XXXXXXXXXX
```

注意：引擎只负责计算 `page.seo` 与 `site.analytics`，不会强行改写 HTML。主题需要在 `<head>` 显式 include SEO/Analytics partial，具体见：[08-主题与模板](./08-themes-templates.zh-CN.md)。

### site：自动摘要（可选）

当文章没有提供 `summary` 时，可以开启“自动摘要”从正文内容提取一段纯文本作为摘要，并写入 `meta.summary`，因此 taxonomy/RSS/search.json/模板里读取 `summary` 都能拿到值。

```yaml
site:
  autoSummary: true
  autoSummaryMaxLength: 200
```

### site：自定义 URL 结构（Permalinks）

推荐优先使用 `site.collections`，`site.permalinks` 主要用于兼容。

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

默认情况下，`post` 类型的文章 URL 为 `/blog/<slug>/`，`page` 类型为 `/pages/<slug>/`。如果你想自定义 URL 结构（例如包含日期），可以使用 `site.permalinks`：

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
```

效果：发布于 2025-03-15 的文章 `my-post`，其 URL 将变为 `/2025/03/my-post/`。

可用的占位符：

| 占位符 | 说明 | 示例值 |
|---|---|---|
| `{slug}` | 文章 slug | `my-post` |
| `{year}` | 发布年份（4 位） | `2025` |
| `{month}` | 发布月份（2 位） | `03` |
| `{day}` | 发布日期（2 位） | `15` |
| `{type}` | 内容类型 | `post` |

可以同时为多个类型配置不同模式：

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

注意：如果某篇文章设置了 `route.url` 和可选 `route.template`，路由覆盖的优先级高于 permalinks。`outputPath` 始终从最终 URL 派生；顶层 `outputPath` 和嵌套 `route.outputPath` 在 Bukit 1.0 中都会被拒绝。

### content：内容来源（Markdown / Notion / 多源）

Bukit 1.0 对单源和多源项目都统一使用 `content.sources[]`。

#### Markdown source

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
```

| 字段 | 作用 | 说明 |
|---|---|---|
| `content.sources[].markdown.dir` | Markdown 根目录 | 递归读取 `*.md` |
| `content.sources[].collection` | 该 source 的默认 collection | 同一目录都属于同一 collection 时使用 |
| `content.sources[].markdown.maxItems` | 最多读取多少篇 | 正整数；用于大仓库限额 |
| `content.sources[].markdown.includePaths` | 只读取指定路径 | 相对 `markdown.dir`；可省略 `.md` |
| `content.sources[].markdown.includeGlobs` | 只读取匹配的 glob | 匹配相对路径，分隔符使用 `/` |

Markdown 内容写法见：[05-内容-Markdown](./05-markdown-content.zh-CN.md)。

#### Notion source

```yaml
content:
  sources:
    - type: notion
      name: pages
      mode: content
      collection: page
      notion:
        databaseId: "xxxx"
        pageSize: 50
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        fieldPolicy:
          mode: whitelist
          allowed:
            - seo_title
            - seo_desc
            - cover
```

| 字段 | 作用 | 说明 |
|---|---|---|
| `content.sources[].notion.maxItems` | 最多拉取多少条 | 正整数；用于大库限额 |
| `content.sources[].notion.includeSlugs` | 只拉取指定 slug | 数据库 query 过滤（便于单篇调试） |
| `content.sources[].notion.includeSlugProperty` | includeSlugs 对应字段 | 默认 `Slug`；建议 rich_text |
| `content.sources[].notion.cacheMode` | Notion 渲染缓存模式 | `off`/`readwrite`/`readonly` |
| `content.sources[].notion.cacheDir` | 缓存目录 | 相对 config 所在目录；不填时默认 `<rootDir>/.cache/notion` |
| `content.sources[].notion.renderConcurrency` | 正文渲染并发度 | 正整数；默认本地 4、CI 2 |
| `content.sources[].notion.maxRps` | Notion 请求全局限速 | 正整数；默认 3（包含数据库 query + blocks children） |
| `content.sources[].notion.maxRetries` | 429 最大重试次数 | 非负整数；遵循 `Retry-After` 退避 |

Notion 模式的前提：

- 必须设置环境变量 `NOTION_TOKEN`（严禁写进仓库文件）

详细见：[06-内容-Notion](./06-notion-content.zh-CN.md)。

### 环境变量覆盖（CI/CD）

任意标量配置可以通过 `BUKIT_` 前缀环境变量覆盖，使用双下划线 `__` 表示层级，字段名使用大写下划线形式：

```bash
BUKIT_SITE__TITLE="Production Site"
BUKIT_SITE__URL="https://example.com"
BUKIT_CONTENT__MARKDOWN__DIR="posts"
BUKIT_BUILD__CLEAN=false
```

这些覆盖会在读取 `site.yaml` 后、配置校验前应用。适合 CI/CD 注入部署 URL、输出开关或内容目录。

#### 多源组合，支持 `mode: data`

```yaml
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

关键点：

- `mode: content` 的源会生成路由与页面
- `mode: data` 的源不会生成路由，会注入 `site.modules`（详见：[09-Modules-结构化数据](./09-modules-data.zh-CN.md)）
- 当 `mode: data` 的 source 配置为 `name: categories`（或 `name: tags`）时，会被用于 taxonomy：即使某个分类/标签当前没有任何文章引用，也会生成对应的空聚合页，避免点击后 404。

### build：输出目录与构建策略

| 字段 | 作用 | 常见示例 |
|---|---|---|
| `build.output` | 输出目录 | `dist` |
| `build.clean` | 构建前是否清理输出目录 | `true` |
| `build.draft` | 是否渲染草稿内容 | `false`（默认） |
| `build.listPageContentMode` | 列表页里的 `pages[*].content` 装配策略 | `auto` |
| `build.schemaFailMode` | Schema 校验失败时的行为 | `warn` / `strict` |

等价的 CLI 参数：

- `--output <dir>` 覆盖 `build.output`
- `--clean/--no-clean` 覆盖 `build.clean`
- `--draft` 覆盖 `build.draft`

`build.listPageContentMode` 只影响 3 个固定列表页：

- 首页 `/`
- 博客列表 `/blog/`
- 页面列表 `/pages/`

它不会影响详情页 `page.content`，只控制列表页中 `pages[*].content` 是否预先带正文：

- `auto`：主题已显式声明需要正文时才带；未声明时再走兼容逻辑
- `always`：总是带正文
- `never`：不带正文，`pages[*].content` 为空字符串

推荐写法：

```yaml
build:
  output: dist
  listPageContentMode: auto
```

### theme：主题位置与参数

最推荐的写法是只指定 `theme.name`，主题目录放在 `themes/<name>/`：

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

如果你不使用 themes 目录，也可以显式指定各目录：

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

theme 支持的完整字段：

| 字段 | 类型 | 示例 | 说明 |
|---|---|---|---|
| `name` | 字符串 | `alt` | 主题名（对应 `themes/<name>/`） |
| `source` | 字符串 | `https://github.com/user/theme.git@v1.0.0` | 远程主题 Git URL（可选版本标签）。本地缓存；后续构建不会自动 pull（可复现）。`bukit-theme.lock.json` 记录已解析的 commit。 |
| `params` | 映射 | `{brand: my-site}` | 传递给主题的自定义参数 |
| `layouts` | 字符串 | `layouts` | 自定义布局模板目录 |
| `assets` | 字符串 | `assets` | 自定义资源目录（SCSS/JS/图片） |
| `static` | 字符串 | `static` | 自定义静态文件目录（原样拷贝） |
| `shortcodes` | 映射 | `shortcode_name: template_string` | 可复用 HTML 片段（Markdown `{% %}` 或 Scriban `{{ shortcode }}`） |
| `components` | 映射 | `name: {template, props}` | 带 props 的模板组件（Scriban `{{ comp.render }}`） |
| `scss` | 对象 | `{enabled, entryPoint, outputDir}` | SCSS → CSS 自动编译（需系统安装 sass） |
| `images` | 对象 | `{enabled, formats, sizes, quality}` | 图片自动优化转换 WebP/AVIF（需 cwebp/magick） |
| `extends` | 字符串 | 父主题名 | 主题继承（子主题级联父主题模板、静态文件、资源） |

主题与模板变量见：[08-主题与模板](./08-themes-templates.zh-CN.md)。

### logging：日志等级（一般不用频繁改）

```yaml
logging:
  level: info
```

CI 场景下建议配合 `--log-format json`，便于收集与排查（见：[12-命令行参考](./12-cli-reference.zh-CN.md)）。

### deploy：部署配置（可选）

控制 `bukit deploy` 命令的部署行为：

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "bukit deploy"
  cname: example.com
```

| 字段 | 说明 | 默认值 |
|------|------|--------|
| `deploy.provider` | 部署目标平台（目前仅 `github-pages`） | — |
| `deploy.branch` | 目标 Git 分支 | `gh-pages` |
| `deploy.message` | Git 提交信息 | `bukit deploy` |
| `deploy.cname` | 自定义域名（会写入 CNAME 文件） | — |

CLI 覆盖：
- `--branch <name>` 覆盖 `deploy.branch`
- `--message <text>` 覆盖 `deploy.message`
- `--dry-run` 仅预览，不实际推送
- `--skip-build` 跳过构建，直接部署已有 dist/

详见：[13-部署到 GitHub Pages](./13-deploy-github-pages.zh-CN.md) 和 [bukit-deploy skill](../../src/skills/bukit-deploy/SKILL.md)。

### collections：内容 Front Matter 字段校验（Schema）

通过 `site.collections` 可以为每种内容类型定义 Front Matter 字段校验规则：

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      schema:
        - name: title
          type: string
          label: 文章标题
          required: true
        - name: publishAt
          type: date
          label: 发布时间
          required: true
        - name: tags
          type: list
          label: 标签
          default: []
        - name: featured
          type: bool
          label: 精选文章
          default: false
        - name: priority
          type: number
          label: 优先级
          default: 0
```

schema 字段说明：

| 字段 | 类型 | 说明 |
|---|---|---|
| `name` | string | Front Matter 字段名 |
| `type` | string | `string`/`number`/`bool`/`date`/`list` |
| `label` | string | 诊断展示名称 |
| `required` | bool | 是否必填 |
| `default` | any | 缺失时自动应用的默认值 |
| `enum` | string[] | 允许值列表 |
| `format` | string | `url`/`uri`/`email`/`date`/`datetime`/`slug` |
| `min` / `max` | number | 数值范围；用于字符串时按长度检查 |

校验失败时，由 `build.schemaFailMode` 控制行为：
- `warn`：输出警告但继续构建
- `strict`：校验失败立即中断构建

## 常见配置场景（可直接抄）

### 1）GitHub Pages 子路径（baseUrl）

如果站点部署在 `https://user.github.io/my-repo/`，那么：

- `site.baseUrl` 应该是 `/my-repo`
- `site.url` 应该是 `https://user.github.io/my-repo`

构建命令示例：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### 2）多语言最小配置

```yaml
site:
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

对照示例：`examples/starter/site.i18n.yaml`。

### 3）Modules（data）最小配置

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown: { dir: content }
    - type: markdown
      name: modules
      mode: data
      markdown: { dir: data, defaultType: module }
```

对照示例：`examples/starter/site.modules.yaml` 与 `examples/starter/data/*.md`。

## 常见坑（快速自查）

- `site.url` 没设：sitemap/rss 的链接可能不正确（可以用 `--site-url` 覆盖）
- `site.baseUrl` 配错：GitHub Pages 打开后资源 404（CSS/JS/图片路径错）
- 相对路径基准搞错：`dir: content` 不是相对命令行所在目录，而是相对 `site.yaml` 所在目录
- Notion token 写进 YAML：不允许且不安全，必须用 `NOTION_TOKEN` 环境变量
