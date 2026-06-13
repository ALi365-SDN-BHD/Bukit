# 16 参数速查表：一页查完（字段/含义/示例）

本页用于快速查找。更完整的权威字段参考与校验细节见：[guide/dev/config-site-yaml](../dev/config-site-yaml.md) 与 [guide/dev/cli](../dev/cli.md)。

## CLI 常用参数

| 参数 | 含义 | 常用示例 |
|---|---|---|
| `--config <path>` | 使用指定配置文件（同时决定相对路径基准） | `--config site.yaml` |
| `--site <name>` | 多站点读取 `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | 覆盖输出目录 | `--output dist` |
| `--base-url <path>` | 覆盖 baseUrl（GitHub Pages 常用） | `--base-url /my-repo` |
| `--site-url <url>` | 覆盖站点绝对 URL（sitemap/rss） | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | 构建前清理输出目录 | `--clean` |
| `--draft` | 渲染草稿内容（如站点约定支持） | `--draft` |
| `--no-incremental` | 关闭增量构建（排障用） | `--no-incremental` |
| `--cache-dir <dir>` | 指定缓存目录 | `--cache-dir .cache` |
| `--metrics <path>` | 输出构建指标 JSON | `--metrics metrics.json` |
| `--log-format <text|json>` | 日志格式（CI 推荐 json） | `--log-format json` |

## site.*（站点级）

| 字段 | 含义 | 示例 |
|---|---|---|
| `site.name` | 站点内部标识 | `starter` |
| `site.title` | 站点展示标题 | `Bukit Starter` |
| `site.description` | 站点描述（可选） | `A site built with Bukit` |
| `site.baseUrl` | 部署子路径 | `/` 或 `/my-repo` |
| `site.url` | 站点绝对 URL（SEO） | `https://user.github.io/my-repo` |
| `site.language` | 默认语言 | `zh-CN` |
| `site.languages` | 多语言列表 | `[zh-CN, en-US]` |
| `site.defaultLanguage` | 多语言默认语言 | `zh-CN` |
| `site.timezone` | 时区 | `Asia/Shanghai` |
| `site.pluginFailMode` | 插件失败策略 | `strict` / `warn` |
| `site.plugins` | 插件开关与参数 | `sitemap: false` / `path-report: { enabled: true, options: {...} }` |
| `site.sitemapMode` | sitemap 输出模式 | `split` / `merged` / `index` |
| `site.search.mode` | search 输出模式 | `split` / `merged` / `index` |
| `site.autoSummary` | 未提供 summary 时自动从正文提取摘要 | `true` / `false` |
| `site.autoSummaryMaxLength` | 自动摘要最大长度（字符数） | `200` |

## content.*（内容系统）

### sources=markdown

| 字段 | 含义 | 示例 |
|---|---|---|
| `content.sources[].type` | 来源类型 | `markdown` |
| `content.sources[].markdown.dir` | Markdown 根目录 | `content` |
| `content.sources[].collection` | 默认集合 | `posts` |

### sources=notion

| 字段 | 含义 | 示例 |
|---|---|---|
| `content.sources[].type` | 来源类型 | `notion` |
| `content.sources[].notion.databaseId` | 数据库 ID | `xxxxxxxx-xxxx-...` |
| `content.sources[].notion.pageSize` | 分页大小（可选） | `50` |
| `content.sources[].notion.filterProperty` | 过滤字段名 | `Published` |
| `content.sources[].notion.filterType` | 过滤类型 | `checkbox_true` |
| `content.sources[].notion.sortProperty` | 排序字段名 | `PublishAt` |
| `content.sources[].notion.sortDirection` | 排序方向 | `descending` |
| `content.sources[].notion.fieldPolicy.mode` | 字段策略 | `whitelist` / `all` |
| `content.sources[].notion.fieldPolicy.allowed` | 白名单字段（归一化后的 key） | `[seo_title, seo_desc]` |

## build.*（构建输出）

| 字段 | 含义 | 示例 |
|---|---|---|
| `build.output` | 输出目录 | `dist` |
| `build.clean` | 构建前清理 | `true` |
| `build.draft` | 渲染草稿 | `false` |

## theme.*（主题与模板）

| 字段 | 含义 | 示例 |
|---|---|---|
| `theme.name` | 主题名（themes/<name>） | `alt` |
| `theme.layouts` | 模板目录（不用 theme.name 时） | `layouts` |
| `theme.assets` | 资源目录（不用 theme.name 时） | `assets` |
| `theme.static` | 静态目录（不用 theme.name 时） | `static` |
| `theme.params` | 主题参数（模板可读取） | `{ brand: starter }` |

## taxonomy.*（分类/标签）

| 字段 | 含义 | 示例 |
|---|---|---|
| `taxonomy.outputMode` | 输出模式 | `both` / `pages` / `data` / `fields_only` |
| `taxonomy.pageSize` | 每 term 分页大小（默认 10） | `20` |
| `taxonomy.indexEnabled` | 是否生成索引页（默认 true） | `false` |
| `taxonomy.pinField` | 置顶字段名（默认 `pinned`） | `sticky` |
| `taxonomy.pinOrderField` | 置顶排序字段 | `pin_weight` |
| `taxonomy.itemFields` | 额外注入的 meta 字段 | `[summary, image, author]` |
| `taxonomy.kinds[].key` | Kind 标识（区分用） | `tags` / `categories` |
| `taxonomy.kinds[].kind` | Kind 名称（模板/路由用） | `tags` |
| `taxonomy.kinds[].title` | 索引页标题 | `所有标签` |
| `taxonomy.kinds[].hierarchical` | 启用层次化分类（v3.0.0+） | `true` / `false` |
| `taxonomy.tags` / `taxonomy.categories` | 旧版 tags/categories 模板级配置 | `indexTemplate` / `termTemplate` |

## logging.*（日志）

| 字段 | 含义 | 示例 |
|---|---|---|
| `logging.level` | 日志等级 | `info` |
