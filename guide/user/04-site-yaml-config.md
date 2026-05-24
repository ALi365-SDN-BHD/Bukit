# 04 Configuration (site.yaml): Field Descriptions, Default Behavior, and Common Patterns

`site.yaml` is your site’s “control panel.” You can think of it as defining: **where content comes from, where it is output, which theme is used, and which extra files are generated**.

This page is for regular users and explains fields by “most common scenarios.” If you need the authoritative field reference and validation details, see the developer documentation: [guide/dev/config-site-yaml](../dev/config-site-yaml.md).

## Override Priority (Very Important)

For the same configuration item, the final effective priority from highest to lowest is:

1. CLI parameters (for example `--output/--base-url/--site-url/--clean/--draft`)
2. `site.yaml`
3. Engine defaults

Common misunderstanding: you changed `site.yaml`, but the CLI still includes `--output dist2`, so it “looks like it did not take effect.”

## Minimal Working Configuration (Markdown)

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

Compare with the runnable example: `examples/starter/site.yaml`.

## Top-Level Blocks: site / content / build / theme / deploy / logging

### site: Site-Level Information (SEO, Multilingual, and Plugin Strategy Are All Here)

Common fields (the ones users edit most often):

| Field | Purpose | Common Example |
|---|---|---|
| `site.name` | Internal site identifier (recommended: short and all lowercase) | `starter` |
| `site.title` | Display title (used by templates/SEO) | `Bukit Starter` |
| `site.baseUrl` | Site deployment subpath (commonly used for GitHub Pages) | `/` or `/my-repo` |
| `site.url` | Absolute site domain (used for sitemap/rss) | `https://user.github.io/my-repo` |
| `site.language` | Default language | `zh-CN` |
| `site.languages` | Multilingual list (enables i18n) | `[zh-CN, en-US]` |
| `site.defaultLanguage` | Default language in multilingual mode | `zh-CN` |
| `site.timezone` | Time zone (affects date display and some default behavior) | `Asia/Shanghai` |
| `site.pluginFailMode` | Plugin failure policy | `strict` / `warn` |
| `site.plugins` | Plugin switches and plugin parameters | `sitemap: false` or `path-report: { enabled: true, options: {...} }` |
| `site.autoSummary` | Whether to extract a summary from the body when `summary` is not provided | `true` / `false` |
| `site.autoSummaryMaxLength` | Maximum auto-summary length (characters) | `200` |
| `site.outputPathEncoding` | Output path encoding strategy (for Chinese/special characters) | `none` / `slug` / `urlencode` / `sanitize` |
| `site.permalinks` | Customize URL structure by type | `post: "/{year}/{month}/{slug}/"` |
| `site.collections` | Collection-driven routing configuration (recommended) | `post: { permalink, template, listRoute }` |
| `site.seo` | Engine-level SEO model configuration | `enabled/defaultImage/twitterSite/organization` |
| `site.analytics` | Analytics code configuration (GA4) | `google_analytics_id: G-...` |

Output-related modes (especially important for multilingual sites):

| Field | Purpose | Common Values |
|---|---|---|
| `site.sitemapMode` | Sitemap output mode | `merged` / `split` / `index` |
| `site.rssMode` | RSS output mode | `merged` / `split` |
| `site.searchMode` | Search output mode | `merged` / `split` / `index` |

For how to choose these modes, see: [11 Multilingual and SEO](./11-i18n-seo.md).

### site: v3.0 New Configuration (Feed, Sitemap, Search, Related Content, Menus, Pagination)

| Field | Purpose | Common Values |
|---|---|---|
| `site.feed.formats` | Feed format list | `["rss", "atom", "json"]` |
| `site.feed.limit` | Maximum entries per feed | `20` |
| `site.feed.path` | Feed output path prefix | `feed` |
| `site.sitemapDetail.defaultPriority` | Default sitemap priority | `0.5` |
| `site.sitemapDetail.defaultChangefreq` | Default sitemap changefreq | `weekly` |
| `site.sitemapDetail.imageEnabled` | Enable image sitemap | `true` / `false` |
| `site.sitemapDetail.videoEnabled` | Enable video sitemap | `true` / `false` |
| `site.search.ui` | Built-in search UI | `default` / `false` |
| `site.search.uiTheme` | Search UI theme | `light` / `dark` / `auto` |
| `site.search.placeholderText` | Search box placeholder text | `"搜索..."` |
| `site.related.enabled` | Enable related content recommendations | `true` / `false` |
| `site.related.threshold` | Relevance threshold | `80` |
| `site.related.limit` | Maximum recommendations per page | `5` |
| `site.menus` | Multiple menu definitions | See [19 New Features](./19-new-features-v3.md) |
| `site.pagination.pageSize` | Global pagination size | `10` |

📖 For detailed usage, see: [19 v3.0 New Features](./19-new-features-v3.md).

### site: SEO and Google Analytics (Optional)

During build, Bukit calculates a unified `page.seo` model for every page. Themes can directly render canonical, description, robots, OG, Twitter, hreflang, and JSON-LD.

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

Field descriptions:

| Field | Default | Description |
|---|---:|---|
| `site.seo.enabled` | `true` | Whether to generate the `page.seo` model; when set to `false`, the new SEO partial will not output SEO tags |
| `site.seo.defaultImage` | Empty | Default share image used when a page has no `og_image/cover/image` |
| `site.seo.twitterSite` | Empty | Outputs `twitter:site`, for example `@your_account` |
| `site.seo.organization.name/url/logo` | Empty | Used for Organization JSON-LD |
| `site.analytics.enabled` | `true` | Whether analytics code output is allowed |
| `site.analytics.google_analytics_id` | Empty | GA4 Measurement ID, for example `G-XXXXXXXXXX` |

Analytics only supports GA4 `gtag`. As long as `site.analytics.google_analytics_id` is configured and `enabled: false` is not set, the new starter partial will output Google Analytics code.

To disable analytics code:

```yaml
site:
  analytics:
    enabled: false
    google_analytics_id: G-XXXXXXXXXX
```

Note: the engine is only responsible for calculating `page.seo` and `site.analytics`; it does not forcibly rewrite HTML. The theme must explicitly include the SEO/Analytics partial in `<head>`. For details, see: [08 Themes and Templates](./08-themes-templates.md).

### site: Auto Summary (Optional)

When an article does not provide `summary`, you can enable “auto summary” to extract a plain-text snippet from the body content as the summary and write it to `meta.summary`. This means taxonomy/RSS/search.json/templates can all get a value when reading `summary`.

```yaml
site:
  autoSummary: true
  autoSummaryMaxLength: 200
```

### site: Custom URL Structure (Permalinks)

Prefer using `site.collections`; `site.permalinks` is mainly for compatibility.

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

By default, articles of type `post` use URLs like `/blog/<slug>/`, and type `page` uses `/pages/<slug>/`. If you want to customize the URL structure (for example to include dates), you can use `site.permalinks`:

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
| `{year}` | Publish year (4 digits) | `2025` |
| `{month}` | Publish month (2 digits) | `03` |
| `{day}` | Publish day (2 digits) | `15` |
| `{type}` | Content type | `post` |

You can configure different patterns for multiple types at the same time:

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

Note: if an article sets route overrides (url/outputPath/template) through Meta or Notion fields, route overrides take priority over permalinks.

### content: Content Source (Markdown / Notion / Multiple Sources)

You can only choose one provider:

- `markdown`: read Markdown from a local folder
- `notion`: read from a Notion database
- `sources`: combine multiple sources (recommended for splitting pages + posts + modules into separate stores)

#### provider=markdown

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
```

| Field | Purpose | Description |
|---|---|---|
| `content.markdown.dir` | Markdown root directory | Recursively reads `*.md` |
| `content.markdown.defaultType` | Default type when `type` is not declared | Commonly `page` |
| `content.markdown.maxItems` | Maximum number of items to read | Positive integer; used to limit large repositories |
| `content.markdown.includePaths` | Only read specified paths | Relative to `content.markdown.dir`; `.md` may be omitted |
| `content.markdown.includeGlobs` | Only read matching globs | Matches relative paths; separator uses `/` |

For Markdown content authoring, see: [05 Content Markdown](./05-markdown-content.md).

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

| Field | Purpose | Description |
|---|---|---|
| `content.notion.maxItems` | Maximum number of items to fetch | Positive integer; used to limit large databases |
| `content.notion.includeSlugs` | Only fetch specified slugs | Database query filter (useful for debugging a single page) |
| `content.notion.includeSlugProperty` | Field corresponding to `includeSlugs` | Default `Slug`; rich_text is recommended |
| `content.notion.cacheMode` | Notion render cache mode | `off`/`readwrite`/`readonly` |
| `content.notion.cacheDir` | Cache directory | Relative to the config directory; defaults to `<rootDir>/.cache/notion` when omitted |
| `content.notion.renderConcurrency` | Body render concurrency | Positive integer; default local 4, CI 2 |
| `content.notion.maxRps` | Global Notion request rate limit | Positive integer; default 3 (includes database query + blocks children) |
| `content.notion.maxRetries` | Maximum retries for 429 | Non-negative integer; follows `Retry-After` backoff |

Prerequisite for Notion mode:

- Environment variable `NOTION_TOKEN` must be set (strictly forbidden to write it into repository files)

For details, see: [06 Content Notion](./06-notion-content.md).

### Environment Variable Overrides (CI/CD)

Any scalar config value can be overridden with a `BUKIT_` environment variable. Use double underscores `__` for nesting and uppercase underscore field names:

```bash
BUKIT_SITE__TITLE="Production Site"
BUKIT_SITE__URL="https://example.com"
BUKIT_CONTENT__MARKDOWN__DIR="posts"
BUKIT_BUILD__CLEAN=false
```

Overrides are applied after loading `site.yaml` and before config validation. This is useful for CI/CD deployment URLs, output switches, or content directories.

#### provider=sources (Multiple Source Composition, Supports mode=data)

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
- Sources with `mode: data` do not generate routes and are injected into `site.modules` (for details, see: [09 Modules Structured Data](./09-modules-data.md))
- When a source with `mode: data` is configured as `name: categories` (or `name: tags`), it is used for taxonomy: even if a category/tag currently has no articles referencing it, a corresponding empty aggregation page is generated to avoid 404 after clicking.

### build: Output Directory and Build Strategy

| Field | Purpose | Common Example |
|---|---|---|
| `build.output` | Output directory | `dist` |
| `build.clean` | Whether to clean the output directory before build | `true` |
| `build.draft` | Whether to render draft content | `false` (default) |
| `build.listPageContentMode` | Assembly strategy for `pages[*].content` in list pages | `auto` |
| `build.schemaFailMode` | Behavior when schema validation fails | `warn` / `strict` |

Equivalent CLI parameters:

- `--output <dir>` overrides `build.output`
- `--clean/--no-clean` overrides `build.clean`
- `--draft` overrides `build.draft`

`build.listPageContentMode` only affects 3 fixed list pages:

- Homepage `/`
- Blog list `/blog/`
- Page list `/pages/`

It does not affect detail-page `page.content`; it only controls whether `pages[*].content` in list pages carries body content in advance:

- `auto`: include body content only when the theme has explicitly declared that it needs it; if not declared, fall back to compatibility logic
- `always`: always include body content
- `never`: do not include body content; `pages[*].content` is an empty string

Recommended pattern:

```yaml
build:
  output: dist
  listPageContentMode: auto
```

### theme: Theme Location and Parameters

The most recommended pattern is to specify only `theme.name`, with the theme directory placed under `themes/<name>/`:

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

If you do not use the themes directory, you can also explicitly specify each directory:

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

Complete fields supported by `theme`:

| Field | Type | Example | Description |
|---|---|---|---|
| `name` | String | `alt` | Theme name (corresponds to `themes/<name>/`) |
| `params` | Map | `{brand: my-site}` | Custom parameters passed to the theme |
| `layouts` | String | `layouts` | Custom layout template directory |
| `assets` | String | `assets` | Custom asset directory (SCSS/JS/images) |
| `static` | String | `static` | Custom static file directory (copied as-is) |
| `shortcodes` | Map | `shortcode_name: template_string` | Reusable HTML snippets (Markdown `{% %}` or Scriban `{{ shortcode }}`) |
| `components` | Map | `name: {template, props}` | Template components with props (Scriban `{{ comp.render }}`) |
| `scss` | Object | `{enabled, entryPoint, outputDir}` | Automatic SCSS → CSS compilation (requires sass installed on the system) |
| `images` | Object | `{enabled, formats, sizes, quality}` | Automatic image optimization and conversion to WebP/AVIF (requires cwebp/magick) |
| `extends` | String | Parent theme name | Theme inheritance (child theme cascades parent theme templates, static files, and assets) |

For theme and template variables, see: [08 Themes and Templates](./08-themes-templates.md).

### logging: Log Level (Usually Does Not Need Frequent Changes)

```yaml
logging:
  level: info
```

In CI scenarios, it is recommended to use this with `--log-format json` to make collection and troubleshooting easier (see: [12 CLI Reference](./12-cli-reference.md)).

### deploy: Deployment Configuration (Optional)

Controls the deployment behavior of the `bukit deploy` command:

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "bukit deploy"
  cname: example.com
```

| Field | Description | Default |
|------|------|--------|
| `deploy.provider` | Deployment target platform (currently only `github-pages`) | — |
| `deploy.branch` | Target Git branch | `gh-pages` |
| `deploy.message` | Git commit message | `bukit deploy` |
| `deploy.cname` | Custom domain (writes a CNAME file) | — |

CLI overrides:
- `--branch <name>` overrides `deploy.branch`
- `--message <text>` overrides `deploy.message`
- `--dry-run` only previews and does not actually push
- `--skip-build` skips build and directly deploys an existing dist/

For details, see: [13 Deploy to GitHub Pages](./13-deploy-github-pages.md) and [bukit-deploy skill](../../src/skills/bukit-deploy/SKILL.md).

### collections: Content Front Matter Field Validation (Schema)

Through `site.collections`, you can define Front Matter field validation rules for each content type:

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

Schema field description:

| Field | Type | Description |
|---|---|---|
| `name` | string | Front Matter field name |
| `type` | string | `string`/`number`/`bool`/`date`/`list` |
| `label` | string | Human-readable diagnostic label |
| `required` | bool | Whether the field is required |
| `default` | any | Default value applied when the field is missing |
| `enum` | string[] | Allowed values |
| `format` | string | `url`/`uri`/`email`/`date`/`datetime`/`slug` |
| `min` / `max` | number | Numeric range; for strings, checks length |

When validation fails, behavior is controlled by `build.schemaFailMode`:
- `warn`: output a warning but continue building
- `strict`: immediately stop the build when validation fails

## Common Configuration Scenarios (Copy-Ready)

### 1) GitHub Pages Subpath (baseUrl)

If the site is deployed at `https://user.github.io/my-repo/`, then:

- `site.baseUrl` should be `/my-repo`
- `site.url` should be `https://user.github.io/my-repo`

Build command example:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### 2) Minimal Multilingual Configuration

```yaml
site:
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

Compare with the example: `examples/starter/site.i18n.yaml`.

### 3) Minimal Modules (data) Configuration

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

Compare with the examples: `examples/starter/site.modules.yaml` and `examples/starter/data/*.md`.

## Common Pitfalls (Quick Self-Check)

- `site.url` is not set: sitemap/rss links may be incorrect (can be overridden with `--site-url`)
- `site.baseUrl` is misconfigured: resources 404 after opening on GitHub Pages (CSS/JS/image paths are wrong)
- Relative path base is misunderstood: `dir: content` is not relative to the command-line working directory, but relative to the directory containing `site.yaml`
- Notion token is written into YAML: this is not allowed and is unsafe; you must use the `NOTION_TOKEN` environment variable
