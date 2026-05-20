# 04 Configuration (site.yaml): Field Descriptions, Defaults & Common Patterns

`site.yaml` is your site's "control panel." Think of it as defining: **where content comes from, where it outputs, which theme to use, and what extra files to generate**.

This page is oriented toward regular users, explaining fields by "most common scenarios"; if you need the authoritative field reference and validation details, see the developer docs: [guide/dev/config-site-yaml](../dev/config-site-yaml.md).

## Override Priority (Very Important)

For the same config item, the final effective priority from highest to lowest is:

1. CLI parameters (e.g., `--output/--base-url/--site-url/--clean/--draft`)
2. `site.yaml`
3. Engine defaults

Common misunderstanding: you changed `site.yaml`, but the CLI still carries `--output dist2`, so it "looks like it didn't take effect."

## Minimal Working Config (Markdown)

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
build:
  output: dist
  clean: true
theme:
  name: alt
logging:
  level: info
```

See running example: `examples/starter/site.yaml`.

## Top-Level Blocks: site / content / build / theme / logging

### site: Site-Level Info (SEO, Multilingual, Plugin Strategy all here)

Common fields (most frequently edited by users):

| Field | Purpose | Common Examples |
|---|---|---|
| `site.name` | Internal site identifier (recommended: lowercase, short) | `starter` |
| `site.title` | Display title (used by templates/SEO) | `Bukit Starter` |
| `site.baseUrl` | Deployment sub-path (common for GitHub Pages) | `/` or `/my-repo` |
| `site.url` | Site absolute domain (used by sitemap/rss) | `https://user.github.io/my-repo` |
| `site.language` | Default language | `zh-CN` |
| `site.languages` | Multilingual list (enables i18n) | `[zh-CN, en-US]` |
| `site.defaultLanguage` | Default language under multilingual | `zh-CN` |
| `site.timezone` | Timezone (affects date display and some default behaviors) | `Asia/Shanghai` |
| `site.pluginFailMode` | Plugin failure strategy | `strict` / `warn` |
| `site.plugins` | Plugin switches and plugin parameters | `sitemap: false` or `path-report: { enabled: true, options: {...} }` |
| `site.autoSummary` | Whether to auto-extract summary from body when not provided | `true` / `false` |
| `site.autoSummaryMaxLength` | Max length for auto summary (characters) | `200` |
| `site.outputPathEncoding` | Output path encoding strategy (handling Chinese/special characters) | `none` / `slug` / `urlencode` / `sanitize` |
| `site.permalinks` | Custom URL structure by type | `post: "/{year}/{month}/{slug}/"` |
| `site.collections` | Collection-driven routing config (recommended) | `post: { permalink, template, listRoute }` |

Output-related modes (critical for multilingual):

| Field | Purpose | Common Values |
|---|---|---|
| `site.sitemapMode` | Sitemap output mode | `merged` / `split` / `index` |
| `site.rssMode` | RSS output mode | `merged` / `split` |
| `site.searchMode` | Search output mode | `merged` / `split` / `index` |

How to choose these modes: [11 Multilingual & SEO](./11-i18n-seo.md).

### site: Auto Summary (Optional)

When an article does not provide `summary`, you can enable "auto summary" to extract a plain text snippet from the body content and write it to `meta.summary`, so that taxonomy/RSS/search.json/template reads of `summary` all get a value.

```yaml
site:
  autoSummary: true
  autoSummaryMaxLength: 200
```

### site: Custom URL Structure (Permalinks)

It is recommended to prioritize `site.collections`; `site.permalinks` is mainly for compatibility.

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/blog-list.html   # Optional: template for list page
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

By default, `post` type articles have a URL of `/blog/<slug>/`, and `page` type have `/pages/<slug>/`. If you want to customize the URL structure (e.g., including dates), you can use `site.permalinks`:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
```

Effect: an article `my-post` published on 2025-03-15 will have the URL `/2025/03/my-post/`.

Available placeholders:

| Placeholder | Description | Example Value |
|---|---|---|
| `{slug}` | Article slug | `my-post` |
| `{year}` | Publish year (4-digit) | `2025` |
| `{month}` | Publish month (2-digit) | `03` |
| `{day}` | Publish day (2-digit) | `15` |
| `{type}` | Content type | `post` |

You can configure different patterns for multiple types simultaneously:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

Note: If an article has route overrides (url/outputPath/template) set via Meta or Notion fields, route overrides take priority over permalinks.

### content: Content Source (Markdown / Notion / Multi-Source)

You can only choose one provider:

- `markdown`: Read Markdown from a local folder
- `notion`: Read from a Notion database
- `sources`: Combine multiple sources (recommended for pages + posts + modules split)

#### provider=markdown

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
```

| Field | Purpose | Notes |
|---|---|---|
| `content.markdown.dir` | Markdown root directory | Recursively reads `*.md` |
| `content.markdown.defaultType` | Default type when type is not declared | Commonly `page` |
| `content.markdown.maxItems` | Maximum items to read | Positive integer; for large repo limits |
| `content.markdown.includePaths` | Only read specified paths | Relative to `content.markdown.dir`; `.md` can be omitted |
| `content.markdown.includeGlobs` | Only read matching globs | Matches relative paths, separator is `/` |

Markdown content authoring: [05 Content Markdown](./05-markdown-content.md).

#### provider=notion

```yaml
content:
  provider: notion
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

| Field | Purpose | Notes |
|---|---|---|
| `content.notion.maxItems` | Maximum items to fetch | Positive integer; for large db limits |
| `content.notion.includeSlugs` | Only fetch specified slugs | DB query filter (for single-page debugging) |
| `content.notion.includeSlugProperty` | Field used by includeSlugs | Default `Slug`; recommended rich_text |
| `content.notion.cacheMode` | Notion render cache mode | `off`/`readwrite`/`readonly` |
| `content.notion.cacheDir` | Cache directory | Relative to config dir; defaults to `<rootDir>/.cache/notion` |
| `content.notion.renderConcurrency` | Content body render concurrency | Positive integer; default local 4, CI 2 |
| `content.notion.maxRps` | Notion request global rate limit | Positive integer; default 3 (includes db query + blocks children) |
| `content.notion.maxRetries` | Max retries on 429 | Non-negative integer; respects `Retry-After` backoff |

Prerequisites for Notion mode:

- Environment variable `NOTION_TOKEN` must be set (strictly forbidden to write into repo files)

See details: [06 Content Notion](./06-notion-content.md).

#### provider=sources (Multi-source composition, supports mode=data)

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      markdown:
        dir: content
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Key points:

- Sources with `mode: content` generate routes and pages
- Sources with `mode: data` do not generate routes, they are injected into `site.modules` (see: [09 Modules Structured Data](./09-modules-data.md))
- When a `mode: data` source is configured with `name: categories` (or `name: tags`), it is used for taxonomy: even if a certain category/tag currently has no articles referencing it, a corresponding empty aggregation page is generated to avoid 404s on clicks.

### build: Output Directory & Build Strategy

| Field | Purpose | Common Examples |
|---|---|---|
| `build.output` | Output directory | `dist` |
| `build.clean` | Whether to clean output before build | `true` |
| `build.draft` | Whether to render draft content | `false` (default) |
| `build.listPageContentMode` | Assembly strategy for `pages[*].content` in list pages | `auto` |

Equivalent CLI parameters:

- `--output <dir>` overrides `build.output`
- `--clean/--no-clean` overrides `build.clean`
- `--draft` overrides `build.draft`

`build.listPageContentMode` only affects 3 fixed list pages:

- Homepage `/`
- Blog list `/blog/`
- Page list `/pages/`

It does not affect detail page `page.content`, only controls whether `pages[*].content` in list pages carries body content:

- `auto`: Carry body only when the theme has explicitly declared the need; otherwise fall through compatibility logic
- `always`: Always carry body
- `never`: No body, `pages[*].content` is an empty string

Recommended config:

```yaml
build:
  output: dist
  listPageContentMode: auto
```

### theme: Theme Location & Parameters

The most recommended approach is to only specify `theme.name`, with the theme directory under `themes/<name>/`:

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

If you are not using the themes directory, you can also explicitly specify each directory:

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

Theme and template variables: [08 Themes & Templates](./08-themes-templates.md).

#### Static Template Rendering (staticTemplate)

By default, files in `static/` (configured via `theme.static`) are copied as-is to the output directory without going through Scriban rendering. To render static HTML files through templates (so they can use `{{ site }}`, `{{ page }}`, and theme partials), set `theme.staticTemplate`:

```yaml
theme:
  name: starter
  staticTemplate: pages/page.html    # Use this template to render static/ HTML files
```

When `staticTemplate` is set:
- Every `.html` file in `static/` is read as `page.content` and rendered through the specified template
- Non-HTML files (CSS, JS, images) are still copied directly
- This allows `about.html`, `contact.html`, etc. to share the site's header/footer/CTA without duplication

Without `staticTemplate` (default), `static/` files are copied raw — useful for standalone pages that don't need templating.

### logging: Log Level (usually no need to change frequently)

```yaml
logging:
  level: info
```

In CI scenarios, it is recommended to combine with `--log-format json` for easier collection and troubleshooting (see: [12 CLI Reference](./12-cli-reference.md)).

## Common Config Scenarios (Copy-Ready)

### 1) GitHub Pages Sub-Path (baseUrl)

If the site is deployed at `https://user.github.io/my-repo/`, then:

- `site.baseUrl` should be `/my-repo`
- `site.url` should be `https://user.github.io/my-repo`

Build command example:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### 2) Minimal Multilingual Config

```yaml
site:
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

See example: `examples/starter/site.i18n.yaml`.

### 3) Minimal Modules (data) Config

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: content
      mode: content
      markdown: { dir: content, defaultType: page }
    - type: markdown
      name: modules
      mode: data
      markdown: { dir: data, defaultType: module }
```

See example: `examples/starter/site.modules.yaml` and `examples/starter/data/*.md`.

### 4) Filtered List Pages (filteredLists)

When you need multiple list pages from the same collection, filtered by a field value (e.g., companies grouped by region), use `filteredLists`:

```yaml
site:
  collections:
    page:
      permalink: /companies/{slug}/
      template: pages/company_detail.html
      listRoute: /companies/
      listTemplate: pages/company_list.html
      filteredLists:
        - field: Type
          value: "已进驻中国企业"
          listRoute: /china-companies/
          listTemplate: pages/company_list.html
        - field: Type
          value: "马来西亚本地企业"
          listRoute: /malaysia-companies/
          listTemplate: pages/company_list.html
```

Each filtered list entry requires:
| Field | Type | Required | Description |
|------|------|------|------|
| `field` | string | **Yes** | Front matter field name to filter by |
| `value` | string | **Yes** | The field value that items must match |
| `listRoute` | string | **Yes** | The URL path for this filtered list |
| `listTemplate` | string | — | Template for rendering; defaults to the collection's `listTemplate` |

Items whose front matter `field` value matches `value` are grouped into a separate list page at the specified `listRoute`. If no `listTemplate` is provided, the collection's `listTemplate` is used as fallback.

## Common Pitfalls (Quick Self-Check)

- `site.url` not set: sitemap/rss links may be incorrect (can be overridden with `--site-url`)
- `site.baseUrl` misconfigured: GitHub Pages resources 404 after opening (CSS/JS/image path wrong)
- Relative path base misunderstood: `dir: content` is not relative to the CLI working directory, but relative to the directory containing `site.yaml`
- Notion token written into YAML: not allowed and unsafe, must use `NOTION_TOKEN` environment variable
