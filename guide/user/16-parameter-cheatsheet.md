# 16 Parameter Cheat Sheet: All on One Page (Field/Meaning/Example)

This page is for quick lookup. For more complete authoritative field references and validation details, see: [guide/dev/config-site-yaml](../dev/config-site-yaml.md) and [guide/dev/cli](../dev/cli.md).

## Common CLI Parameters

| Parameter | Meaning | Common Example |
|---|---|---|
| `--config <path>` | Use specified config file (also determines relative path base) | `--config site.yaml` |
| `--site <name>` | Multi-site: reads `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | Override output directory | `--output dist` |
| `--base-url <path>` | Override baseUrl (commonly used for GitHub Pages) | `--base-url /my-repo` |
| `--site-url <url>` | Override site absolute URL (sitemap/rss) | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | Clean output directory before build | `--clean` |
| `--draft` | Render draft content (if site convention supports it) | `--draft` |
| `--no-incremental` | Disable incremental build (for troubleshooting) | `--no-incremental` |
| `--cache-dir <dir>` | Specify cache directory | `--cache-dir .cache` |
| `--metrics <path>` | Output build metrics JSON | `--metrics metrics.json` |
| `--log-format <text|json>` | Log format (CI recommends json) | `--log-format json` |

## site.* (Site-Level)

| Field | Meaning | Example |
|---|---|---|
| `site.name` | Site internal identifier | `starter` |
| `site.title` | Site display title | `Bukit Starter` |
| `site.description` | Site description (optional) | `A site built with Bukit` |
| `site.baseUrl` | Deployment sub-path | `/` or `/my-repo` |
| `site.url` | Site absolute URL (SEO) | `https://user.github.io/my-repo` |
| `site.language` | Default language | `zh-CN` |
| `site.languages` | Multilingual list | `[zh-CN, en-US]` |
| `site.defaultLanguage` | Multilingual default language | `zh-CN` |
| `site.timezone` | Timezone | `Asia/Shanghai` |
| `site.pluginFailMode` | Plugin failure strategy | `strict` / `warn` |
| `site.plugins` | Plugin switches and parameters | `sitemap: false` / `path-report: { enabled: true, options: {...} }` |
| `site.sitemapMode` | Sitemap output mode | `split` / `merged` / `index` |
| `site.rssMode` | RSS output mode | `split` / `merged` |
| `site.searchMode` | Search output mode | `split` / `merged` / `index` |
| `site.autoSummary` | Auto-extract summary from body when not provided | `true` / `false` |
| `site.autoSummaryMaxLength` | Max length of auto summary (characters) | `200` |

## content.* (Content System)

### provider=markdown

| Field | Meaning | Example |
|---|---|---|
| `content.provider` | Content source type | `markdown` |
| `content.markdown.dir` | Markdown root directory | `content` |
| `content.markdown.defaultType` | Default type | `page` |

### provider=notion

| Field | Meaning | Example |
|---|---|---|
| `content.provider` | Content source type | `notion` |
| `content.notion.databaseId` | Database ID | `xxxxxxxx-xxxx-...` |
| `content.notion.pageSize` | Page size (optional) | `50` |
| `content.notion.filterProperty` | Filter field name | `Published` |
| `content.notion.filterType` | Filter type | `checkbox_true` |
| `content.notion.sortProperty` | Sort field name | `PublishAt` |
| `content.notion.sortDirection` | Sort direction | `descending` |
| `content.notion.fieldPolicy.mode` | Field policy | `whitelist` / `all` |
| `content.notion.fieldPolicy.allowed` | Whitelist fields (normalized keys) | `[seo_title, seo_desc]` |

### provider=sources (Composite Mode)

| Field | Meaning | Example |
|---|---|---|
| `content.provider` | Content source type | `sources` |
| `content.sources[].type` | Source type | `markdown` / `notion` |
| `content.sources[].name` | Source name | `pages` / `posts` / `modules` |
| `content.sources[].mode` | Behavior mode | `content` / `data` |
| `content.sources[].markdown` | Markdown sub-config | `{ dir: content, defaultType: page }` |
| `content.sources[].notion` | Notion sub-config | `{ databaseId: "...", fieldPolicy: { mode: all } }` |

## build.* (Build Output)

| Field | Meaning | Example |
|---|---|---|
| `build.output` | Output directory | `dist` |
| `build.clean` | Clean before build | `true` |
| `build.draft` | Render drafts | `false` |

## theme.* (Theme & Templates)

| Field | Meaning | Example |
|---|---|---|
| `theme.name` | Theme name (themes/&lt;name&gt;) | `alt` |
| `theme.layouts` | Templates directory (when not using theme.name) | `layouts` |
| `theme.assets` | Assets directory (when not using theme.name) | `assets` |
| `theme.static` | Static directory (when not using theme.name) | `static` |
| `theme.params` | Theme parameters (readable by templates) | `{ brand: starter }` |

## taxonomy.* (Taxonomy / Tags & Categories)

| Field | Meaning | Example |
|---|---|---|
| `taxonomy.template` | Default term template | `pages/taxonomy-term.html` |
| `taxonomy.indexTemplate` | Index page template | `pages/taxonomy-index.html` |
| `taxonomy.termTemplate` | Term page template (overrides global) | `pages/taxonomy-term-alt.html` |
| `taxonomy.outputMode` | Output mode | `both` / `pages` / `data` / `fields_only` |
| `taxonomy.pageSize` | Per-term pagination size (default 10) | `20` |
| `taxonomy.indexEnabled` | Generate index pages (default true) | `false` |
| `taxonomy.pinField` | Pin field name (default `pinned`) | `sticky` |
| `taxonomy.pinOrderField` | Pin ordering field | `pin_weight` |
| `taxonomy.itemFields` | Extra meta fields to inject | `[summary, image, author]` |
| `taxonomy.kinds[].key` | Kind identifier (for identification) | `tags` / `categories` |
| `taxonomy.kinds[].kind` | Kind name (template/routing) | `tags` |
| `taxonomy.kinds[].title` | Index page title | `All Tags` |
| `taxonomy.kinds[].hierarchical` | Enable hierarchical taxonomy (v3.0.0+) | `true` / `false` |
| `taxonomy.tags` / `taxonomy.categories` | Legacy tags/categories template config | `indexTemplate` / `termTemplate` |

## logging.* (Logging)

| Field | Meaning | Example |
|---|---|---|
| `logging.level` | Log level | `info` |
