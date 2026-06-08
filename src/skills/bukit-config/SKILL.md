---
name: bukit-config
description: Use when using bukit to create or modify site.yaml, asking about the meaning of a bukit configuration field, encountering config validation errors, or needing to configure a specific Bukit feature (collections, taxonomy, i18n, plugins, media) through YAML

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Config/AppConfig.cs"
source_anchors:
  - "src/Bukit.Config/AppConfig.cs"
guide_chapters:
  - "guide/user/04-site-yaml-config.md"
---

# Bukit Site Configuration

## Overview

`site.yaml` is Bukit's single configuration entry point. Seven top-level nodes: `site`, `content`, `build`, `theme`, `taxonomy`, `logging`, `deploy` (planned). Routing and templates are explicit: content must match `site.collections`, `site.permalinks`, route front matter, or theme `templates.accepts`; otherwise doctor/build fails with an actionable config error.

**REQUIRED SUB-SKILL:** Verify config changes with `bukit build`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "配置 site.yaml"、"bukit 配置文件"、"collections 怎么配"、"taxonomy 配置" |
| English | "configure site.yaml", "config file", "how to set up collections", "taxonomy setup" |
| Bahasa Melayu | "konfigurasi site.yaml", "fail konfigurasi bukit", "cara sediakan collections", "tetapan taxonomy" |

## Config Model Quick Reference

| Node | Responsibility | Key Fields |
|------|------|---------|
| `site` | Site metadata and global behavior | name, title, url, baseUrl, language, collections, plugins, externalPlugins, feed, sitemapDetail, related, menus, search, pagination |
| `content` | Content source definition | provider (notion/markdown), sources, media |
| `build` | Build behavior | output, clean, draft, listPageContentMode, assetHashMode |
| `theme` | Theme configuration | name, layouts, assets, static, params |
| `taxonomy` | Taxonomy configuration | template, kinds, pageSize, outputMode |
| `logging` | Log level | level (debug/info/warn/error) |
| `deploy` | Deployment configuration (planned — not yet shown in examples) | provider, branch, message, cname, keepHistory |

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
  sources:
    - type: markdown
      markdown:
        dir: content

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
  sources:
    - type: notion
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
| `rssMode` | string | `split` | **Deprecated in 1.0: removed**; use `site.feed.formats` for feed outputs |
| `searchMode` | string | `split` | Search index mode: `split`/`merged`/`index` |
| `autoSummary` | bool | false | Auto-generate summaries |
| `autoSummaryMaxLength` | int | 200 | Max auto-summary length (1-5000) |
| `pluginFailMode` | string | `strict` | Plugin failure policy: `strict`/`warn` |
| `deriveConflictPolicy` | string | `fail` | Derived page route conflict policy: `fail`/`warn`/`last-wins`. Content-page conflicts always fail regardless of this setting. |
| `searchIncludeDerived` | bool | false | Whether search index includes derived pages |
| `externalProtocolIncludeRoutedPages` | bool | false | Whether external protocol plugins receive routed pages |
| `collections` | map | — | Collection route definitions |
| `plugins` | map | — | Plugin toggles (`{pluginName: {enabled: false}}`). Key `feed` replaces old `rss` |
| `externalPlugins` | map | — | External plugin configuration. Each entry supports `runtime`, `entry`, `hooks`, `timeoutMs`, `maxStdoutBytes`/`maxStderrBytes` (output byte limits), `allowEnvironment` (list of host env vars to expose), and `capabilities` (list of required capabilities: `emit-outputs`, `derive-pages`). When `capabilities` is declared, the plugin is **enforced** at runtime — if its hooks don't match declared capabilities, the build fails. |
| `externalPluginPolicy` | string | `warn` | External plugin safety policy: `deny` (block all), `warn` (load with warning, default), `allow` (load silently). Invalid values throw `ConfigException` with `BKT-0002`. |
| `feed` | map | — | Feed config: `formats`, `limit`, `path` |
| `sitemapDetail` | map | — | Sitemap detail: `defaultPriority`, `defaultChangefreq`, `imageEnabled`, `videoEnabled` |
| `related` | map | — | Related content: `enabled`, `threshold`, `limit`, `indices` |
| `menus` | map | — | Multi-menu navigation definitions |
| `search` | map | — | Search UI: `ui`, `uiTheme`, `placeholderText` |
| `pagination` | map | — | Global pagination defaults: `pageSize` |
| `analytics` | map | — | Analytics configuration: `enabled`, `google_analytics_id`, `disableInPreview` |

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
| `filterType` | string | `checkbox_true` | Filter type: `checkbox_true`/`checkbox_false`/`select_equals`/`status_equals`/`rich_text_equals`/`none` |
| `filterValue` | string | — | Filter value (required for `select_equals`/`status_equals`/`rich_text_equals`) |
| `sortProperty` | string | — | Sort property name |
| `sortDirection` | string | `ascending` | Sort direction: `ascending`/`descending` |
| `includeSlugs` | string[] | — | Only include pages with specified slugs |
| `propertyMap` | map | — | Custom Notion property name → Bukit field name mapping. Supports 12 fields: `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical` |
| `includeSlugProperty` | string | `Slug` | Slug property name |
| `cacheMode` | string | `off` | Cache mode: `off`/`readwrite`/`readonly` |
| `cacheDir` | string | — | Cache directory |
| `fieldPolicy.mode` | string | `whitelist` | Field policy: `whitelist`/`all` |
| `fieldPolicy.allowed` | string[] | — | Allowed field name list for whitelist mode |

#### content.markdown

| Field | Type | Default | Description |
|------|------|--------|------|
| `dir` | string | `content` | Markdown file directory (relative path) |
| `defaultType` | string | — | Optional content type to inject when a Markdown file has neither `collection` nor `type`. Omit it for fully explicit front matter. |
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
| `clean` | bool | true | Whether to clear output directory before build. When enabled, Bukit first checks that the output directory contains a `.bukit-output-marker` file (written on every successful build). Directories without this marker are never cleaned — this prevents accidental deletion of non-Bukit directories. Bukit also refuses to clean the project root, home directory, filesystem root, or `.git` directories. |
| `draft` | bool | false | Whether to render drafts (pages with draft: true) |
| `listPageContentMode` | string | `auto` | List page content mode: `auto`/`always`/`never` |
| `schemaFailMode` | string | `warn` | Schema validation failure behavior: `warn` or `strict` |
| `assetHashMode` | string | — | Asset copy comparison mode. `"sha256"` uses SHA256 content hashing to decide whether a file needs re-copying (recommended for CI and network/remote filesystems). Default (empty/omitted) uses file size + last-write-time comparison. |

### theme Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `name` | string | — | Theme name (corresponds to `themes/<name>/`) |
| `source` | string | — | Remote theme Git URL with optional version, e.g., `https://github.com/user/theme.git@v1.0.0`. The theme is cloned to a local cache on first build and **not** automatically updated on subsequent builds (reproducible builds). A `bukit-theme.lock.json` file records the resolved commit for each `source@ref`. |
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
| `template` | string | — | Optional taxonomy template. If omitted, the active theme must declare matching `templates.*.accepts.kind` entries such as `taxonomy_index` / `taxonomy_term`. |
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
| `listTemplate` | string | — | Template for list page rendering (e.g., `pages/blog-list.html`). If omitted, the active theme must declare a template accepting `kind: list`. |
| `pagination.enabled` | bool | — | Enable pagination |
| `pagination.pageSize` | int | 10 | Entries per page (positive integer) |
| `output.rss` | bool | true | Whether the collection generates RSS |
| `output.sitemap` | bool | true | Whether the collection is included in sitemap |
| `output.archive` | bool | false | Whether to generate yearly archives |
| `output.archiveDetail` | map | — | Archive detail config: `depth` (yearly/monthly/daily), `template`, `routePrefix` |
| `filteredLists` | array | — | Sub-list pages filtered by field value. Each entry: `{field, value, listRoute, listTemplate?}`. Items whose front matter `field` value matches `value` are grouped into a separate list page. |
| `content.modelSchema.fieldScopes.<collection>` | array | — | Scoped content model fields. Each entry: `{name, type/fieldType, label, required, default}` |

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
| `output.feedPath` | string | — | Per-collection feed directory (e.g., `blog-feed` → /blog-feed/atom) |
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

## Analytics Configuration (site.analytics)

Controls Google Analytics (GA4) gtag output via the analytics partial.

| Field | Type | Default | Description |
|------|------|--------|------|
| `analytics.enabled` | bool | true | Whether analytics code output is allowed |
| `analytics.google_analytics_id` | string | — | GA4 Measurement ID (e.g., `G-XXXXXXXXXX`); must start with `G-` |
| `analytics.disableInPreview` | bool | true | Disable analytics during `bukit preview`. Set `false` to enable analytics in preview mode. |

```yaml
site:
  analytics:
    enabled: true
    google_analytics_id: "G-XXXXXXXXXX"
```

When `google_analytics_id` is configured and `enabled` is not `false`, the starter theme's analytics partial outputs the GA4 gtag snippet. To disable, set `enabled: false`.

Template usage:
```scriban
{{ if site.analytics && site.analytics.enabled && site.analytics.google_analytics_id }}
  <script async src="https://www.googletagmanager.com/gtag/js?id={{ site.analytics.google_analytics_id | html.escape }}"></script>
  <script>
    window.dataLayer = window.dataLayer || [];
    function gtag(){dataLayer.push(arguments);}
    gtag('js', new Date());
    gtag('config', '{{ site.analytics.google_analytics_id | html.escape }}');
  </script>
{{ end }}
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
content:
  modelSchema:
    fieldScopes:
      posts:
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

Scoped fields now live under `content.modelSchema.fieldScopes.<collection>`; the old collection-level `schema` key is removed in vNext. Supported field keys: `name`, `type`/`fieldType`, `label`, `required`, `default`, `enum`, `format`, `min`, `max`, `semanticType`, `sourcePolicy`, and `reference`. Type values include `string|text`, `number`, `bool`, `date`, and `array`; formats include `url`/`uri`, `email`, `date`/`datetime`, and `slug`; defaults are applied before schema validation.

### Schema Validation Error Codes

| Code | Description |
|------|-------------|
| `content.required_custom_field_missing` | A required content model custom field is missing. |
| `content.required_collection_field_missing` | A required scoped field is missing for the document collection. |
| `content.unknown_raw_key` | `rejectUnknownRawKeys: true` found a raw key not declared by canonical mappings, custom fields, field scopes, entity mappings, or relation mappings. In vNext this fails during normalization. |
| `content.custom_field_type_mismatch` | The value type does not match the content model field declaration. |
| `content.custom_field_enum_mismatch` | The value is not one of the allowed `enum` values. |
| `content.custom_field_format_mismatch` | The value does not match the expected format (e.g., `url`, `email`, `date`). |
| `content.custom_field_range_mismatch` | Numeric or text-length value is outside the allowed range. |

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
| `site.collections.xxx.template is required` | Collection has no template | Add template: pages/post.html |
| `site.collections.xxx.listRoute must start with '/'` | listRoute format incorrect | Change to `/blog/` |
| `content.sources is required in Bukit 1.0` | Content source not specified | Add `content.sources[]` with a `markdown` or `notion` source |
| `NOTION_TOKEN is required...` | Notion API key not set | Set env var `NOTION_TOKEN` |
| `content.notion.databaseId is required` | Database ID not filled | Enter Notion database ID |
| `site.timezone '...' is not a valid time zone identifier` | Invalid timezone | Use IANA timezone name, e.g., `Asia/Shanghai` |
| `build.output must not contain '..'` | Path contains `..` traversal | Use relative path like `dist` |
| `taxonomy.pageSize must be a positive integer` | Page size not a positive integer | Set to a positive integer |
| `site.collections keys must be non-empty` | Collection name is empty string | Ensure collection names are non-empty |
| `site.languages has duplicate language` | Language list has duplicates | Remove duplicates |
| `site.defaultLanguage must be included in site.languages` | Default language not in list | Add defaultLanguage to languages |
| `Plugin 'xxx' is missing required capability 'yyy' for hook 'zzz'` | Plugin declares capabilities but is missing one needed for its hooks | Add the missing capability to the plugin's `capabilities` list |

---

## Diagnostic Codes (BKT-xxxx)

Starting from v3.x, Bukit exceptions carry stable diagnostic codes in `BKT-XXXX` hex format. These appear in CLI output, logs, and `bukit doctor` output.

| Category | Code Range | Example Codes |
|---|---|---|
| **Config** | `BKT-0001` – `BKT-00FF` | `BKT-0001` RequiredFieldMissing, `BKT-0002` InvalidValue, `BKT-0003` YamlSyntaxError, `BKT-0004` PathTraversal |
| **Theme** | `BKT-0101` – `BKT-01FF` | `BKT-0101` ManifestInvalid, `BKT-0102` ComponentNotFound, `BKT-0104` SourceUnavailable |
| **Route** | `BKT-0201` – `BKT-02FF` | `BKT-0201` RouteConflict, `BKT-0204` ListRouteInvalid |
| **Render** | `BKT-0301` – `BKT-03FF` | `BKT-0301` TemplateNotFound, `BKT-0302` TemplateParseError, `BKT-0303` LayoutNestingExceeded, `BKT-0304` ComponentFailed |
| **Schema** | `BKT-0401` – `BKT-04FF` | `BKT-0401` ValidationFailed, `BKT-0402` StrictModeBlocked |
| **Content** | `BKT-0501` – `BKT-05FF` | `BKT-0501` LoadFailed, `BKT-0502` ProviderUnavailable |
| **Build** | `BKT-0601` – `BKT-06FF` | `BKT-0601` OutputUnsafe, `BKT-0602` OutputNoMarker |
| **Plugin** | `BKT-0701` – `BKT-07FF` | `BKT-0701` ExecutionFailed, `BKT-0702` TimeoutExceeded |
| **SEO** | `BKT-0801` – `BKT-08FF` | `BKT-0801` SeoAuditFailed |
| **GEO** | `BKT-0810` – `BKT-081F` | `BKT-0810` GeoAuditFailed |
| **Media** | `BKT-0901` – `BKT-09FF` | `BKT-0901` MediaDownloadFailed, `BKT-0902` ImageOptimizeFailed, `BKT-0904` MediaSsrfBlocked |

### How to Read Diagnostic Codes

When Bukit encounters an error, it outputs `<code> <message>`:
```
✖ Config error
[BKT-0601] Refusing to clean unsafe output directory: /path/to/dir.
             How to fix: set build.output to a dedicated subdirectory like 'dist' or 'public'.
```

`bukit doctor` uses this format consistently. The code is stable across versions — the same error always produces the same `BKT-XXXX` code.

## Content Pipeline Stages

The content loading pipeline is internally organized as a sequence of independent **stages**, each with its own name and timing:

| Order | Stage Name | Responsibility |
|---|---|---|
| 1 | `ContentLoad` | Creates the content provider and loads all content items |
| 2 | `ImageLocalize` | Downloads and localizes remote images in content HTML |
| 3 | `DraftFilter` | Filters out items with `draft: true` (unless `build.draft: true`) |
| 4 | `ContentGraphValidate` | Validates canonical content records and content model schema rules |
| 5 | `CollectionWarning` | Emits collection-level content warnings |

Each stage logs its completion with duration:
```
event=content.stage stage=ContentLoad duration_ms=234
event=content.stage stage=ImageLocalize duration_ms=156
event=content.stage stage=DraftFilter duration_ms=1
event=content.stage stage=ContentGraphValidate duration_ms=3
event=content.stage stage=CollectionWarning duration_ms=12
```

These stages can be extended or reordered via `IContentStage` (plugin development).

## Common Configuration Issues: Symptom-Cause-Fix

| Symptom | Likely Cause | Fix |
|---|---|---|
| Build succeeds but pages are generated under unexpected URLs | `baseUrl`, collection `permalink`, or CLI `--base-url` override differs from the intended deployment path | Keep `site.baseUrl` as `/` for root deployments, use `/subpath/` for subdirectory deployments, and check CLI overrides before changing `site.yaml` |
| A collection has content files but no list page appears | `listRoute` is missing, points to the wrong path, or no list template can be resolved | Add `listRoute`, verify it starts with `/`, and configure `listTemplate` or a theme `templates.*.accepts.kind: list` rule |
| Markdown pages render with the wrong route or template | Front matter `collection` is missing/mismatched, or no route/template rule matches the item | Align front matter `collection` with `site.collections`, or add an explicit front matter `template` / theme `templates.*.accepts` rule |
| Notion pages are fetched but fields are missing in templates | `fieldPolicy.mode: whitelist` excludes properties needed by templates | Add required properties to `fieldPolicy.allowed`, or use `fieldPolicy.mode: all` when exploring a new database schema |
| Taxonomy pages are empty or terms are missing | `taxonomy.kinds[].key` does not match the content field name, or `taxonomy.outputMode` disables page output | Match `key` to the front matter or Notion property name and use `outputMode: both` or `pages` when term pages should be generated |
| Pagination URLs duplicate or conflict with other routes | `pagination.urlPattern`, `listRoute`, or collection `permalink` overlap | Keep pagination paths under the list route, for example `listRoute: /blog/` with `urlPattern: page/:num/` |
| SEO audit warns about missing canonical, sitemap, or schema URLs | `site.url` is missing or not absolute | Set `site.url` to the production origin, including `https://`, and keep `site.baseUrl` for path prefix only |
| Config works locally but fails in CI | Environment overrides with `BUKIT_` changed scalar values unexpectedly | Print the effective config in CI diagnostics and review `BUKIT_*` variables before editing `site.yaml` |
| Theme templates or assets cannot be found | `theme.name`, `theme.layouts`, `theme.assets`, or `theme.static` does not match the active theme directory | Verify `themes/<theme.name>/` exists and keep layout paths relative to the theme root conventions |
| Schema validation warnings appear for fields that exist | Schema `type`, `format`, or `enum` does not match actual Markdown front matter or Notion property values | Normalize content values, add `default` where safe, or temporarily use `build.schemaFailMode: warn` while migrating content |
| Image downloads fail silently or images appear broken | `content.media.blockPrivateNetworks` is blocking internal image hosts (default: `true`) | If images are on a private network, add the host to the allowlist or use a public CDN; see SSRF Protection below |
| Deprecation warnings about old config fields | Config uses legacy field names (e.g., `rss` → `feed`, `outputPath` → `permalink`) | Run `bukit doctor` to see migration suggestions; follow the Config Deprecation Scanner table below |

## SSRF Protection (P1-3/P1-6)

`content.media.blockPrivateNetworks` defaults to `true` and blocks image downloads from internal/private network addresses. This is powered by `SsrfGuard.cs` which rejects:

- Loopback: 127.0.0.0/8, ::1
- RFC1918 private: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
- Link-local: 169.254.0.0/16

SSRF protection is also applied to `CloneCommand` (theme asset downloads) and `SeoExternalAuditor` (SEO external link audits) via `SsrfGuard.SsrfSafeConnectAsync`.

## Config Deprecation Scanner (P3-3)

`ConfigDeprecationScanner` detects legacy config patterns and emits migration warnings during `bukit doctor` and build:

| Legacy Pattern | Replacement | Rule |
|---|---|---|
| `site.rss` | `site.feed` | RSS→Feed migration |
| `site.collections.<k>.outputPath` | `site.collections.<k>.permalink` | OutputPath→Permalink |
| `content.notion.rootPageId` | `content.notion.rootBlockId` | PageId→BlockId |
| `content.markdown.rootPageId` | `content.markdown.rootBlockId` | PageId→BlockId |
| `theme.sourceRef` | `theme.source` with `@` version | SourceRef→Source |
| `site.rssMode` | `site.feed.formats` | RssMode→Feed formats |
| `build.outputPath` | `build.output` | OutputPath→Output |

## JSON Schema (P3-2)

`bukit config schema` generates a complete JSON Schema for `site.yaml` including collection routing fields (`permalink`, `template`, `listRoute`, `listTemplate`, `pagination`, `output`, `filteredLists`) and the vNext content model schema surface (`content.modelSchema.canonicalMappings`, `customFields`, `fieldScopes`, `entityMappings`, `relationMappings`, and `media`).

## Checklist: New Site Config Review

Use `bukit doctor` to auto-check most items. After config, verify each:

- [ ] `site.name` and `site.title` are set
- [ ] `site.baseUrl` starts with `/`
- [ ] `site.url` (if set) starts with `http://` or `https://`
- [ ] Each collection in `site.collections` has a permalink (with `{slug}`) and template
- [ ] `content.sources[]` is set with each source's matching sub-config filled in
- [ ] In Notion mode, `NOTION_TOKEN` env var is set
- [ ] `build.output` is a valid relative path
- [ ] `theme` node points to an existing theme directory
- [ ] For multilingual, `languages` and `defaultLanguage` are consistent
