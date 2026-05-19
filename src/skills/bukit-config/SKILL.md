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
| `site` | Site metadata and global behavior | name, title, url, baseUrl, language, collections, plugins, externalPlugins |
| `content` | Content source definition | provider (notion/markdown), sources, media |
| `build` | Build behavior | output, clean, draft, listPageContentMode |
| `theme` | Theme configuration | name, layouts, assets, static, params |
| `taxonomy` | Taxonomy configuration | template, kinds, pageSize, outputMode |
| `logging` | Log level | level (debug/info/warn/error) |

## Scenario Templates

### Blog (Markdown content source)

```yaml
site:
  name: my-blog
  title: My Blog
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
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
| `externalAssemblyTrustMode` | string | `warn` | External assembly trust mode: `strict`/`warn` |
| `searchIncludeDerived` | bool | false | Whether search index includes derived pages |
| `externalProtocolIncludeRoutedPages` | bool | false | Whether external protocol plugins receive routed pages |
| `collections` | map | — | Collection route definitions |
| `plugins` | map | — | Plugin toggles (`{pluginName: {enabled: false}}`) |
| `externalPlugins` | map | — | External plugin configuration |
| `externalAssemblyAllowlist` | map | — | External assembly allowlist (`{filename: SHA256}`) |

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

### theme Node

| Field | Type | Default | Description |
|------|------|--------|------|
| `name` | string | — | Theme name (corresponds to `themes/<name>/`) |
| `layouts` | string | `layouts` | Template subdirectory name |
| `assets` | string | `assets` | Asset subdirectory name (SCSS, etc. that need processing) |
| `static` | string | `static` | Static file subdirectory name (copied directly) |
| `params` | map | — | Theme parameters, accessed as `{{ site.params.xxx }}` in templates |

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
      title: Tags          # Display name
      template: pages/tag.html
      indexEnabled: true
    - key: categories
      title: Categories
      singularTitlePrefix: "Category: "   # Page title prefix
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
| `pagination.enabled` | bool | — | Enable pagination |
| `pagination.pageSize` | int | 10 | Entries per page (positive integer) |
| `output.rss` | bool | true | Whether the collection generates RSS |
| `output.sitemap` | bool | true | Whether the collection is included in sitemap |
| `output.archive` | bool | false | Whether to generate yearly archives |

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
