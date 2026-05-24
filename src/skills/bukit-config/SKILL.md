---
name: bukit-config
description: Use when using bukit to create or modify site.yaml, asking about the meaning of a bukit configuration field, encountering bukit config validation errors, or needing to configure a specific Bukit feature (collections, taxonomy, i18n, plugins, media) through YAML
---

# Bukit Site Configuration

## Overview

`site.yaml` is Bukit's single configuration entry point, following the convention-over-configuration philosophy. Six top-level nodes: `site`, `content`, `build`, `theme`, `taxonomy`, `logging`. Most fields have sensible defaults — a minimal working site.yaml needs only about 20 lines.

**REQUIRED SUB-SKILL:** Verify config changes with `bukit build`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "配置 site.yaml"、"bukit 配置文件"、"collections 怎么配"、"taxonomy 配置" |
| English | "configure site.yaml", "bukit config file", "how to set up collections", "taxonomy setup" |
| Bahasa Melayu | "konfigurasi site.yaml", "fail konfigurasi bukit", "cara sediakan collections", "tetapan taxonomy" |

## Config Model Quick Reference

| Node | Responsibility | Key Fields |
|------|------|---------|
| `site` | Site metadata and global behavior | name, title, url, baseUrl, language, collections, plugins, externalPlugins, feed, sitemapDetail, related, menus, search, pagination |
| `content` | Content source definition | provider (notion/markdown), sources, media |
| `build` | Build behavior | output, clean, draft, listPageContentMode |
| `theme` | Theme configuration | name, layouts, assets, static, params |
| `taxonomy` | Taxonomy configuration | template, kinds, pageSize, outputMode |
| `logging` | Log level | level (debug/info/warn/error) |
| `deploy` | Deployment configuration | provider, branch, output, message |

## Scenario Templates

### Blog (Markdown content source)

```yaml
site:
  name: my-blog
  title: My Blog
  baseUrl: /
  url: https://example.com
  language: zh-CN
  timezone: Asia/Shanghai
  feed:
    formats: [rss, atom]
    limit: 20
  sitemapDetail:
    defaultPriority: 0.5
    defaultChangefreq: weekly
  search:
    ui: default
    uiTheme: light
  related:
    enabled: true
    threshold: 80
    limit: 5
  menus:
    main:
      - identifier: home
        name: Home
        url: /
        weight: 1
      - identifier: blog
        name: Blog
        url: /blog/
        weight: 2
  collections:
    post:
      permalink: /blog/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
        urlPattern: page/:num/
      output:
        rss: true
        archive:
          enabled: true
          depth: monthly
    page:
      permalink: /{slug}/
      template: pages/page.html

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: post

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static

taxonomy:
  template: pages/page.html
  pageSize: 10
  kinds:
    - key: tags
    - key: categories

logging:
  level: info
```

### Documentation Site (flat URLs)

```yaml
site:
  name: my-docs
  title: Project Docs
  baseUrl: /
  collections:
    doc:
      permalink: /docs/{slug}/
      template: pages/page.html
      listRoute: /docs/
```

### Multilingual Site

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  languages: [zh-CN, en]
  defaultLanguage: zh-CN
  sitemapMode: merged
  rssMode: merged
  searchMode: merged
```

### Notion-Driven Site

```yaml
site:
  name: my-notion-site
  title: My Notion Site
  baseUrl: /
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/

content:
  provider: notion
  notion:
    databaseId: "your-database-id"
    filterProperty: Published
    filterType: checkbox_true

theme:
  name: starter
```

## Field Reference

### site Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `name` | string | **Required** | Site identifier |
| `title` | string | **Required** | Site title, used as `{{ site.title }}` in templates |
| `url` | string | — | Full site URL, must start with `http://` or `https://`; required for absolute canonical, hreflang, schema URLs, sitemap, and RSS |
| `description` | string | — | Site description; SEO fallback for home, list, taxonomy, pagination, and pages without `summary`/`seo_desc` |
| `baseUrl` | string | `/` | Site root path, must start with `/`. For subdirectory deployment use `/subpath/` |
| `language` | string | `zh-CN` | Default content language |
| `timezone` | string | `Asia/Shanghai` | IANA timezone identifier |
| `languages` | string[] | — | Language list for multilingual, e.g., `[zh-CN, en]` |
| `defaultLanguage` | string | First language | Default language in multilingual mode |
| `outputPathEncoding` | string | `none` | Output path encoding: `none`/`slug`/`urlencode`/`sanitize`. Applies to both content and derived pages (pagination, archive, taxonomy). |
| `sitemapMode` | string | `split` | Sitemap mode: `split`/`merged`/`index` |
| `rssMode` | string | `split` | RSS mode: `split`/`merged` |
| `searchMode` | string | `split` | Search index mode: `split`/`merged`/`index` |
| `autoSummary` | bool | false | Auto-generate summaries |
| `autoSummaryMaxLength` | int | 200 | Max auto-summary length (1-5000) |
| `pluginFailMode` | string | `strict` | Plugin failure policy: `strict`/`warn` |
| `deriveConflictPolicy` | string | `fail` | Derived page route conflict policy: `fail`/`warn`/`last-wins`. Content-page conflicts always fail regardless of this setting. |
| `searchIncludeDerived` | bool | false | Whether search index includes derived pages |
| `externalProtocolIncludeRoutedPages` | bool | false | Whether external protocol plugins receive routed pages |
| `collections` | map | — | Collection route definitions |
| `plugins` | map | — | Plugin toggles (`{pluginName: {enabled: false}}`). Key `feed` replaces old `rss` |
| `externalPlugins` | map | — | External plugin configuration |
| `feed` | map | — | Feed config: `formats`, `limit`, `path` |
| `sitemapDetail` | map | — | Sitemap detail: `defaultPriority`, `defaultChangefreq`, `imageEnabled`, `videoEnabled` |
| `related` | map | — | Related content: `enabled`, `threshold`, `limit`, `indices` |
| `menus` | map | — | Multi-menu navigation definitions |
| `search` | map | — | Search UI: `ui`, `uiTheme`, `placeholderText` |
| `pagination` | map | — | Global pagination defaults: `pageSize` |

SEO-oriented configs should include both `site.url` and `site.description`. Without `site.url`, canonical and schema URLs fall back to relative paths and audit emits warnings. Without `site.description`, generated home/list/taxonomy/pagination routes usually emit `seo.description_missing` unless the route has its own summary.
| `permalinks` | map | — | Global permalink custom placeholders |

### content Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `provider` | string | **Required** | Content provider: `notion` or `markdown` |
| `sources` | array | — | Multi-source content config (mutually exclusive with provider) |
| `notion` | map | — | Notion content source config |
| `markdown` | map | — | Markdown content source config |
| `media` | map | — | Media file handling (download, URL rewriting) |

#### content.notion

| Field | Type | Default | Description |
|------|------|--------|------|
| `databaseId` | string | **Required** | Notion database ID |
| `pageSize` | int | 50 | Pagination size (1-100) |
| `maxItems` | int | — | Max items to fetch |
| `renderContent` | bool | — | Whether to render page block content |
| `renderConcurrency` | int | — | Block rendering concurrency |
| `maxRps` | int | — | API request rate limit |
| `maxRetries` | int | — | Request failure retry count |
| `filterProperty` | string | `Published` | Filter property name |
| `filterType` | string | `checkbox_true` | Filter type: `checkbox_true`/`none` |
| `sortProperty` | string | — | Sort property name |
| `sortDirection` | string | `ascending` | Sort direction: `ascending`/`descending` |
| `includeSlugs` | string[] | — | Only include pages with specified slugs |
| `includeSlugProperty` | string | `Slug` | Slug property name |
| `cacheMode` | string | `off` | Cache mode: `off`/`readwrite`/`readonly` |
| `cacheDir` | string | — | Cache directory |
| `fieldPolicy.mode` | string | `whitelist` | Field policy: `whitelist`/`all` |
| `fieldPolicy.allowed` | string[] | — | Allowed field name list for whitelist mode |

#### content.markdown

| Field | Type | Default | Description |
|------|------|--------|------|
| `dir` | string | `content` | Markdown file directory (relative path) |
| `defaultType` | string | `page` | Default content type (maps to collection) |
| `maxItems` | int | — | Max item count |
| `includePaths` | string[] | — | Specific file paths to include |
| `includeGlobs` | string[] | — | Glob pattern filtering |

#### content.media

| Field | Type | Default | Description |
|------|------|--------|------|
| `downloadToLocal` | bool | true | Whether to download remote images locally |
| `downloadDir` | string | `assets/uploads` | Download directory |
| `urlBase` | string | `/assets/uploads` | URL prefix after replacement in HTML |
| `defaultImageUrl` | string | `/assets/images/noneimg-news.jpg` | Default image URL |
| `fieldKeys` | string[] | `[cover,image,thumbnail,...]` | Image field keys to process |
| `maxConcurrency` | int | 4 | Download concurrency |
| `maxRetries` | int | 3 | Download retry count |
| `timeoutMs` | int | 10000 | Download timeout (milliseconds) |
| `maxFileSizeBytes` | int | 52428800 | Max file size (50MB) |
| `blockPrivateNetworks` | bool | true | Block downloading from internal network addresses |

#### content.sources (Multi-source mode)

```yaml
content:
  sources:
    - type: notion
      mode: content        # content or data
      notion:
        databaseId: "xxx"
    - type: markdown
      mode: data           # data type goes to site.data for template use
      name: faq            # Optional data module name
      markdown:
        dir: data/faq
```

`sources` is mutually exclusive with `provider`. When `mode` is `data`, content goes to `site.data.<name>` or `site.data` and does not generate page routes.

### build Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `output` | string | `dist` | Output directory (relative path, must not contain `..`) |
| `clean` | bool | true | Whether to clear output directory before build |
| `draft` | bool | false | Whether to render drafts (pages with draft: true) |
| `listPageContentMode` | string | `auto` | List page content mode: `auto`/`always`/`never` |
| `schemaFailMode` | string | `warn` | Schema validation failure behavior: `warn` or `strict` |

### theme Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `name` | string | — | Theme name (corresponds to `themes/<name>/`) |
| `layouts` | string | `layouts` | Template subdirectory name |
| `assets` | string | `assets` | Asset subdirectory name (SCSS, etc. that need processing) |
| `static` | string | `static` | Static file subdirectory name (copied directly) |
| `staticTemplate` | string | — | When set, renders `static/` `.html` files through Scriban (injecting `page.content`); otherwise copies raw |
| `params` | map | — | Theme parameters, accessed as `{{ site.params.xxx }}` in templates |
| `extends` | string | — | Parent theme name (cascade lookup for theme inheritance) |
| `shortcodes` | map | — | Reusable HTML snippets for Markdown/Scriban |
| `components` | map | — | Reusable template components with typed props |
| `scss` | map | — | SCSS compilation config |
| `images` | map | — | Image optimization config |

### taxonomy Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `template` | string | `pages/page.html` | Default template for taxonomy term pages |
| `indexTemplate` | string | — | Taxonomy index page template |
| `termTemplate` | string | — | Taxonomy term page template (overrides template) |
| `pageSize` | int | 10 | Entries per page |
| `indexEnabled` | bool | true | Whether to generate taxonomy index pages |
| `outputMode` | string | `both` | Output mode: `both`/`pages`/`data`/`fields_only` |
| `pinField` | string | `pinned` | Pin field name |
| `pinOrderField` | string | — | Pin ordering field |
| `itemFields` | string[] | — | Fields extracted from page metadata as taxonomy basis |
| `kinds` | array | — | Custom taxonomy definitions |

#### taxonomy.kinds

```yaml
taxonomy:
  kinds:
    - key: tags            # Field name (required)
      kind: tags           # URL path segment (default: key)
      title: Tags          # Display name (default: key)
      singularTitlePrefix: "Tag: "  # Term page title prefix
      template: pages/tag.html
      indexTemplate: pages/tag-index.html
      termTemplate: pages/tag-term.html
      indexEnabled: true
      hierarchical: false  # Enable parent-child hierarchy (computes children + ancestors)
    - key: categories
      kind: categories
      title: Categories
      hierarchical: true   # Categories often benefit from hierarchy
```

**kinds[].hierarchical** (new in v3.0.0):
- When `true`, computes `children` and `ancestors` arrays for each term
- Terms with `parent` metadata create parent-child relationships
- Template variables: `page.fields.taxonomy.value.children` (slug[]), `page.fields.taxonomy.value.ancestors` (slug[])
- Index page terms: `page.fields.terms.value[].children`, `page.fields.terms.value[].ancestors`
- JSON output: `taxonomy.json` includes `children` and `ancestors` per term

#### Term metadata fields (via data source or _index.md)

Each term can carry additional metadata loaded from `taxonomy_ensure_terms` data sources or `content/_taxonomy/<kind>/<slug>/_index.md`:

| Field | Type | Description |
|------|------|------|
| `description` | string | Term description text |
| `image` | string | Term cover image URL |
| `weight` | int | Sort weight (higher = first, default 0) |
| `parent` | string | Parent term slug (for hierarchy) |

Example data file (`content/data/tags.yaml`):
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10
  parent: tech
- title: Python
  slug: python
  weight: 5
```

### logging Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `level` | string | `info` | Log level: `debug`/`info`/`warn`/`error` |

Use `--log-format json` via CLI to switch to JSON format output.

## Collection Configuration (site.collections)

Collection routing is Bukit's core routing model. Each entry must declare:

| Field | Type | Required | Description |
|------|------|------|------|
| `permalink` | string | **Yes** | URL pattern, **must include `{slug}`**. Supports `{slug}`/`{year}`/`{month}`/`{day}`/`{type}` |
| `template` | string | **Yes** | Template file path (e.g., `pages/post.html`) |
| `listRoute` | string | — | List page route, must start with `/` |
| `listTemplate` | string | — | Template for list page rendering (e.g., `pages/blog-list.html`); defaults to `pages/list.html` |
| `pagination.enabled` | bool | — | Enable pagination |
| `pagination.pageSize` | int | 10 | Entries per page (positive integer) |
| `output.rss` | bool | true | Whether the collection generates RSS |
| `output.sitemap` | bool | true | Whether the collection is included in sitemap |
| `output.archive` | bool | false | Whether to generate yearly archives |
| `output.archiveDetail` | map | — | Archive detail config: `depth` (yearly/monthly/daily), `template`, `routePrefix` |
| `filteredLists` | array | — | Sub-list pages filtered by field value. Each entry: `{field, value, listRoute, listTemplate?}`. Items whose front matter `field` value matches `value` are grouped into a separate list page. |
| `schema` | array | — | Content field validation schema. Each entry: `{name, type, label, required, default}` |

## Feed Configuration (site.feed)

Controls multi-format feed generation (RSS 2.0, Atom, JSON Feed). Replaces the old `rssMode` for format selection.

| Field | Type | Default | Description |
|------|------|--------|------|
| `feed.formats` | string[] | `["rss"]` | Feed formats: `rss`, `atom`, `json` |
| `feed.limit` | int | 20 | Max items per feed |
| `feed.path` | string | `feed` | Base path prefix for feed files |

Per-collection feed customization (in `site.collections.<key>.output`):

| Field | Type | Default | Description |
|------|------|--------|------|
| `output.rss` | bool | true | Enable feed for this collection |
| `output.feedPath` | string | — | Per-collection feed directory (e.g., `blog-feed` → `/blog-feed/atom.xml`) |
| `output.feedTitle` | string | — | Per-collection feed title (defaults to `site.title`) |
| `output.feedDescription` | string | — | Per-collection feed description |

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]
    limit: 20
    path: feed

collections:
  post:
    output:
      rss: true
      feedPath: blog-feed
      feedTitle: "My Blog Posts"
      feedDescription: "Latest blog articles"
```

Plugin toggle: use key `feed` (replaces the old `rss` key):
```yaml
site:
  plugins:
    feed:
      enabled: false   # Disable all feed generation
```

## Sitemap Detail Configuration (site.sitemapDetail)

Controls sitemap.xml enhancement fields.

| Field | Type | Default | Description |
|------|------|--------|------|
| `sitemapDetail.defaultPriority` | double | 0.5 | Default `<priority>` value (0.0–1.0) |
| `sitemapDetail.defaultChangefreq` | string | `weekly` | Default `<changefreq>`: always/hourly/daily/weekly/monthly/yearly/never |
| `sitemapDetail.imageEnabled` | bool | false | Enable Image Sitemap extension (`xmlns:image`) |
| `sitemapDetail.videoEnabled` | bool | false | Enable Video Sitemap extension (`xmlns:video`) |

Front matter overrides:
```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
  images:
    - url: "/images/hero.jpg"
      caption: "Hero image"
      title: "Main hero"
  videos:
    - url: "https://youtube.com/watch?v=xxx"
      title: "Tutorial"
      thumbnail: "/images/thumb.jpg"
---
```

```yaml
site:
  sitemapDetail:
    defaultPriority: 0.5
    defaultChangefreq: "weekly"
    imageEnabled: true
    videoEnabled: false
```

## Related Content Configuration (site.related)

Controls automatic related content recommendation.

| Field | Type | Default | Description |
|------|------|--------|------|
| `related.enabled` | bool | false | Enable related content computation |
| `related.threshold` | int | 80 | Minimum score to include (0–1000) |
| `related.limit` | int | 5 | Max related items per page |
| `related.indices` | array | `[{name:tags,weight:80},{name:categories,weight:60}]` | Matching dimensions and weights |

Each index in `indices`: `{name: string, weight: int}`. Supported names: `tags`, `categories`, `keywords`, `collection`/`type`, `date`.

```yaml
site:
  related:
    enabled: true
    threshold: 80
    limit: 5
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
      - name: keywords
        weight: 40
```

Data available in templates via `context.Data["__related_pages"]` — a dictionary keyed by content item ID, each value is a list of `{title, url, score}`.

## Menu Configuration (site.menus)

Defines multi-menu navigation with nested children support.

```yaml
site:
  menus:
    main:
      - identifier: home
        name: Home
        url: /
        weight: 1
      - identifier: blog
        name: Blog
        url: /blog/
        weight: 2
        children:
          - identifier: tech
            name: Tech
            url: /blog/tags/tech/
            weight: 1
    footer:
      - identifier: about
        name: About
        url: /about/
        weight: 1
```

| Field | Type | Required | Description |
|------|------|------|------|
| `identifier` | string | Yes | Unique menu item identifier |
| `name` | string | Yes | Display name |
| `url` | string | Yes | Link URL |
| `weight` | int | 1 | Sort order (lower = first) |
| `children` | array | — | Nested sub-menu items (same structure) |

Data available in templates via `context.Data["menus"]` and as `menus.json` in output.

## Search Detail Configuration (site.search)

Controls search index and built-in search UI.

| Field | Type | Default | Description |
|------|------|--------|------|
| `search.ui` | string | `default` | Built-in search UI: `default` or `false` to disable |
| `search.uiTheme` | string | `light` | UI theme: `light` / `dark` / `auto` |
| `search.placeholderText` | string | — | Search input placeholder text |

```yaml
site:
  search:
    ui: "default"
    uiTheme: "dark"
    placeholderText: "Search articles..."
```

Front matter:
```yaml
---
searchWeight: 5       # Higher = ranked higher (default 1)
searchExclude: true   # Exclude from search index
---
```

## Pagination Global Configuration (site.pagination)

Global pagination defaults.

| Field | Type | Default | Description |
|------|------|--------|------|
| `pagination.pageSize` | int | 10 | Global default page size |

Per-collection pagination enhancements (in `site.collections.<key>.pagination`):

| Field | Type | Default | Description |
|------|------|--------|------|
| `urlPattern` | string | `page/:num/` | URL pattern with `:num` placeholder (e.g., `p/:num/`) |
| `firstPageUsesListRoute` | bool | true | Whether page 1 uses `listRoute` directly instead of `page/1/` |

```yaml
collections:
  post:
    pagination:
      enabled: true
      pageSize: 10
      urlPattern: "p/:num/"
      firstPageUsesListRoute: true
```

## Archive Detail Configuration

Per-collection archive customization.

```yaml
collections:
  post:
    output:
      archive:
        enabled: true
        depth: "daily"              # yearly | monthly | daily
        template: "pages/archive.html"
        routePrefix: "archives"     # Custom URL prefix (default: "archive")
```

## Data Files (data/ directory)

Place YAML/JSON/TOML data files in `data/` directory for template access. Supports multi-language data with `data/{lang}/` subdirectories.

```
data/
  authors.yaml
  navigation.json
  zh-CN/
    strings.yaml
  en/
    strings.yaml
```

Data is automatically loaded and injected into `context.Data["__data_files"]` by the DataFilesPlugin. Nested directories are loaded recursively.

## Config Examples

### Shortcodes

```yaml
theme:
  shortcodes:
    youtube: '<div class="video"><iframe src="https://www.youtube.com/embed/{{ $1 }}"></iframe></div>'
    callout: '<div class="callout callout-{{ $1 }}">{{ $2 }}</div>'
```

### Theme Inheritance

```yaml
theme:
  name: my-custom-theme
  extends: official-blog-theme
```

### Schema Validation

```yaml
collections:
  posts:
    schema:
      - name: featured
        type: bool
        required: true
      - name: rating
        type: number
        min: 1
        max: 5
      - name: status
        type: string
        enum: [draft, published]
        default: draft
```

Supported schema keys: `name`, `type`, `label`, `required`, `default`, `enum`, `format`, `min`, `max`. Formats include `url`/`uri`, `email`, `date`/`datetime`, and `slug`; defaults are applied before schema validation.

### Environment Overrides

Use `BUKIT_` variables with `__` nesting for CI/CD scalar overrides:

```bash
BUKIT_SITE__URL=https://example.com
BUKIT_SITE__TITLE="Production Site"
BUKIT_BUILD__CLEAN=false
BUKIT_CONTENT__MARKDOWN__DIR=posts
```

### Image Optimization

```yaml
theme:
  images:
    enabled: true
    formats: [webp, avif]
    sizes: [480, 768, 1200]
    quality: 85
```

### SCSS Compilation

```yaml
theme:
  scss:
    enabled: true
```

## CLI Parameter Overrides

`bukit build` supports overriding some config via CLI parameters:

```
--output <dir>      Override build.output
--base-url <url>    Override site.baseUrl
--site-url <url>    Override site.url
--draft             Override build.draft = true
--clean             Force clean
--no-clean          Disable clean
--incremental       Enable incremental build
--no-incremental    Disable incremental build
--jobs <n>          Override parallel rendering concurrency
```

These overrides only affect the current build and do not modify site.yaml.

## Common Config Errors

| Error Message | Cause | Fix |
|---------|------|------|
| `site.name is required` | Site name not set | Add `site.name: my-site` |
| `site.title is required` | Title not set | Add `site.title: My Site` |
| `site.baseUrl must start with '/'` | baseUrl format incorrect | Change to `/` or `/subpath/` |
| `site.collections.xxx.permalink must include {slug}` | Permalink missing {slug} placeholder | Add `{slug}`, e.g., `/blog/{slug}/` |
| `site.collections.xxx.template is required` | Collection has no template | Add `template: pages/post.html` |
| `site.collections.xxx.listRoute must start with '/'` | listRoute format incorrect | Change to `/blog/` |
| `content.provider is required` | Content source not specified | Set `provider: markdown` or `provider: notion` |
| `NOTION_TOKEN is required...` | Notion API key not set | Set env var `NOTION_TOKEN` |
| `content.notion.databaseId is required` | Database ID not filled | Enter Notion database ID |
| `site.timezone '...' is not a valid time zone identifier` | Invalid timezone | Use IANA timezone name, e.g., `Asia/Shanghai` |
| `build.output must not contain '..'` | Path contains `..` traversal | Use relative path like `dist` |
| `taxonomy.pageSize must be a positive integer` | Page size not a positive integer | Set to a positive integer |
| `site.collections keys must be non-empty` | Collection name is empty string | Ensure collection names are non-empty |
| `site.languages has duplicate language` | Language list has duplicates | Remove duplicates |
| `site.defaultLanguage must be included in site.languages` | Default language not in list | Add defaultLanguage to languages |

## Checklist: New Site Config Review

Use `bukit doctor` to auto-check most items. After config, verify each:

- [ ] `site.name` and `site.title` are set
- [ ] `site.baseUrl` starts with `/`
- [ ] `site.url` (if set) starts with `http://` or `https://`
- [ ] Each collection in `site.collections` has a permalink (with `{slug}`) and template
- [ ] `content.provider` is set with corresponding sub-config filled in
- [ ] In Notion mode, `NOTION_TOKEN` env var is set
- [ ] `build.output` is a valid relative path
- [ ] `theme` node points to an existing theme directory
- [ ] For multilingual, `languages` and `defaultLanguage` are consistent
