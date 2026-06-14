# Built-in Plugins (BuiltIn) Artifacts and Boundaries

This page describes the “output contracts” of built-in plugins: what files/pages they generate, what configuration they depend on, and how they behave in multilingual builds. When you modify these plugins or themes that depend on them, maintain this page first to avoid behavioral drift.

Built-in plugin implementation directory: `src/Bukit.Engine/Plugins/BuiltIn/`

P3 publish outputs note: sitemap, feed, search, llms/llms-full, robots, and agent manifest are owned by the publish projection pipeline (`PublishRepresentationRegistry`). Some projection adapters still reuse historical generator/plugin classes such as `SitemapGenerator`, `RssGenerator`, `SearchIndexBuilder`, and `LlmsTxtPlugin`, but those aggregate files are no longer default `IAfterBuildPlugin` ownership.

Related docs:
- [Plugin System](./plugins.md)
- [Multilingual and SEO](./i18n-seo.md)
- [Engine Fixed Outputs](./engine-outputs.zh-CN.md)

## sitemap (publish projection adapter)

Source: `PublishRepresentationRegistry` adapter via sitemap generator helpers.

- Output: `<outputDir>/sitemap.xml`
- Dependency: `site.url` must be configured; otherwise generation is skipped
- Included routes:
  - Engine fixed pages: `/`, `/blog/`, `/pages/`
  - All routed content pages
  - All derived routes from derive-pages plugins, such as taxonomy/pagination/archive
- Enhanced fields (v3.0+):
  - `<priority>`: defaults to `site.sitemapDetail.defaultPriority` (0.5), overridable per page through front matter `sitemap.priority`
  - `<changefreq>`: defaults to `site.sitemapDetail.defaultChangefreq` (weekly), overridable per page through front matter `sitemap.changefreq`
  - `<image:image>`: when `site.sitemapDetail.imageEnabled: true`, image information is extracted from front matter `sitemap.images`
  - `<video:video>`: when `site.sitemapDetail.videoEnabled: true`, video information is extracted from front matter `sitemap.videos`
- `lastmod` rules:
  - Routed content pages: prefer `fields.update_time` when it can be parsed as a date; otherwise fall back to `publishAt`
  - Derived routes: use the `LastModified` value returned by each derive-pages plugin
- Exclusion rules based on final HTML meta tags:
  - If page HTML contains `<meta name="robots" content="noindex|none ...">`, the page is removed from the sitemap
  - Compatibility: `<meta name="sitemap" content="exclude|noindex|false|0">`

Multilingual behavior:
- When `site.languages` is non-empty and `site.sitemapMode == merged`, root generation is driven by the i18n root projection adapter
- In other modes, each language output directory generates its own `sitemap.xml`

## feed (publish projection adapter, replaces the original rss plugin in v3.0)

Source: `PublishRepresentationRegistry` adapter via RSS, Atom, and JSON Feed generator helpers.

- Output: generates multiple formats according to `site.feed.formats`:
  - `rss` → `<outputDir>/rss.xml` (RSS 2.0)
  - `atom` → `<outputDir>/feed/atom.xml` (Atom 1.0)
  - `json` → `<outputDir>/feed/feed.json` (JSON Feed 1.1)
- Dependency: `site.url` must be configured; otherwise generation is skipped
- Input: routed content only; derived pages are not included
- Configuration:
  - `site.feed.formats`: list of formats to generate, default `["rss"]`
  - `site.feed.limit`: maximum number of entries per feed, default 20
  - `site.feed.path`: base path for feed files, default `feed`
- Per-collection independent feeds:
  - `collection.output.feedPath`: custom feed path, such as `blog-feed`
  - `collection.output.feedTitle`: custom feed title
  - `collection.output.feedDescription`: custom feed description
- Front matter: `feed.exclude: true` excludes a page; `feed.enclosure` supports podcast enclosures
- Plugin switch key: `site.plugins.feed`; `rss` is no longer used

Multilingual behavior:
- Feed mode in multilingual builds is no longer configured via `site.rssMode` in 1.0.
- In 1.0 configs, feeds are generated per language using `site.feed` and `site.plugins.feed` defaults.

## search-index (publish projection adapter)

Source: `PublishRepresentationRegistry` adapter via `SearchIndexBuilder`; the adapter also writes the optional `bukit-search.html` UI partial.

- Output: `<outputDir>/search.json` plus optional `bukit-search.html`
- Dependency: does not depend on `site.url`; it can be used on sites with purely relative links
- Content fields:
  - `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
  - New `weight`: written when front matter sets `searchWeight`, for weighted frontend sorting
- Front matter enhancements (v3.0+):
  - `searchWeight`: search weight; default 1, higher values sort earlier
  - `searchExclude: true`: excludes the page from the search index
- `url` generation rule: joins `site.baseUrl` with the page `route.url`, producing an internal site path
- Built-in search UI (v3.0+):
  - Enable with `site.search.ui: "default"`
  - Supports `site.search.uiTheme` (light/dark/auto)
  - Supports `site.search.placeholderText` for custom placeholder text
  - Outputs `bukit-search.html` with zero dependencies and about 5 KB of JS; templates can reference it with `{{ include }}`

Whether derived pages are included:
- Controlled by `site.searchIncludeDerived`:
  - false: index routed pages only
  - true: index routed + derived pages

Multilingual behavior:
- Each language variant directory generates its own `search.json`
- If `site.search.mode == index`, the engine additionally generates `search.index.json` at the root, aggregating references to each language index

## taxonomy (IDerivePagesPlugin + IAfterBuildPlugin)

File: `TaxonomyPlugin.cs`

Derives pages from content `meta.tags` / `meta.categories`:

- `/tags/` → `tags/index.html`
- `/tags/<slug>/` → `tags/<slug>/index.html`
- `/categories/` → `categories/index.html`
- `/categories/<slug>/` → `categories/<slug>/index.html`

Notes:
- Derived pages use templates from explicit taxonomy config or from the active theme's `templates.accepts.kind` declarations
- Priority: kind-level index/term > global index/term > kind-level template > global template > theme template kind match
- Page content is simple HTML generated by the plugin (ul/li lists), still written to `page.content` for compatibility with older themes
- Structured fields are also injected so themes can render lists directly instead of parsing HTML:
  - Index pages (`/tags/`, `/categories/`): `page.fields.terms.type == "list"`, and `page.fields.terms.value[]` is `{ title, slug, url, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
- Term pages (`/tags/<slug>/`, `/categories/<slug>/`):
  - `page.fields.items.type == "list"`, and `page.fields.items.value[]` is `{ title, url, publish_date, summary? }`
  - `page.fields.taxonomy.value` is `{ kind, term, slug, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
  - `page.fields.pagination.value` is `{ page, page_size, total, total_pages, has_prev, has_next }`
- Term page item sorting:
  - Default: by `publishAt` descending
  - Pinning support: entries with `pinned=true` appear first, followed by `publishAt` descending
  - Optional pin order: when `pinOrderField` or source-level `pinOrderFieldBySource` is configured, pinned entries sort by `pinOrder` ascending first, then by `publishAt` descending
  - The presence of `pinOrder` implies pinned status even without explicit `pinned=true`
- Slug rules: alphanumerics are preserved, everything else is compressed to `-` and lowercased; Unicode Latin transliteration is supported (`é`→`e`, `ß`→`ss`, `æ`→`ae`, etc.)
- Term pages support pagination routes: `/<kind>/<slug>/page/<n>/`, with pageSize controlled by `taxonomy.pageSize`
- During AfterBuild, outputs `taxonomy.json` (schema v2), containing structured data for all taxonomy dimensions and their term lists
- Taxonomy index pages can be disabled with `taxonomy.indexEnabled=false` or `taxonomy.kinds[].indexEnabled=false`
- Taxonomy pin field configuration:
  - Global fields: `taxonomy.pinField` (default `pinned`) and `taxonomy.pinOrderField` (optional)
  - Multi-source field mapping: `taxonomy.pinFieldBySource[sourceKey]`, `taxonomy.pinOrderFieldBySource[sourceKey]`
  - When bySource is not configured, all data sources use the global field names

### Term Metadata (v3.0.0+)

Each taxonomy term can carry extra metadata from two sources:

1. **data-mode content source** (`content/data/tags.yaml`, etc.):
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10          # Sort weight; higher values come first (default 0)
  parent: tech        # Parent term slug for hierarchical taxonomy
```

2. **_index.md convention** (Hugo-style): `content/_taxonomy/<kind>/<slug>/_index.md`

```yaml
---
description: Everything about ML and AI
image: /assets/images/ml-cover.png
weight: 10
parent: tech
---
```

### Hierarchical Taxonomy (v3.0.0+)

Enable it with `taxonomy.kinds[].hierarchical: true`; parent-child relationships are calculated automatically:

```yaml
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true
```

After enabling:
- Each term automatically calculates `children` (direct children) and `ancestors` (ancestor chain from the root to the current term)
- Templates can use `page.fields.taxonomy.value.children` / `ancestors` for breadcrumb navigation
- The JSON output `taxonomy.json` also includes `children` and `ancestors` arrays

### Term Visibility Control

Set `IsVisible: false` to hide internal-use terms. They do not appear in index page `terms.value[]`, but their detail pages remain accessible.

### RSS Feeds for Taxonomy Terms (v3.0.0+)

Each term automatically generates an independent RSS 2.0 feed: `<output>/<kind>/<slug>/feed.xml`

### Alias Redirects (v3.0.0+)

Aliases configured in a term’s `Aliases` field automatically generate HTML redirect pages:
`<output>/<kind>/<alias_slug>/index.html` → redirect to `/<kind>/<slug>/`

### Term Sorting Rules

- In index pages and JSON output, terms are sorted by `Weight` descending (higher weight first), then by DisplayName ascending for equal weights
- Invisible terms (`IsVisible=false`) do not appear in index pages

Notion notes:
- Taxonomy reads `meta`, not `page.fields.*`; therefore Notion `tags/categories` should preferably use `multi_select`
- If your Notion `tags/categories` use `relation`, the Notion provider promotes the relation target page’s `title` (fallback to `slug`, then fallback to `id`) into the `meta.tags/meta.categories` term list, ensuring taxonomy generates readable categories/tags
- When the relation target page is not present in the current database query result, Notion `/v1/pages/{id}` is requested additionally to complete the target page title/slug, up to 200 pages to avoid request explosions
- Empty category/tag pages are generated automatically to avoid 404s after clicking:
  - If a content source with `mode: data` and `name: categories` or `name: tags` exists, the engine uses entries from that data source as the taxonomy term list. Even if the term is not currently referenced by any article, a corresponding term page is generated; the item slug is preferred as the slug.
  - If a Notion content source is used, the engine extracts `options[].name` from `select/multi_select/status` fields in the Notion database schema, automatically ensuring that term pages exist for `tags/categories` and for fields corresponding to `taxonomy.kinds[].key`.

Template example (taxonomy term page pagination):
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

## pages-index (IDerivePagesPlugin)

File: `PagesIndexPlugin.cs`

Generates structured “site-wide index by id” data and injects it into template variables:

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`

Use case:
- When a template only has a pageId, such as a list of ids returned by a Notion relation, it can use this index to look up that page’s URL, title, and other information

Notes:
- pages-index is independent of content sources: as long as the build produces routed content pages such as posts/pages, they enter the index
- This index only covers routed content pages, not derived routes such as taxonomy/pagination/archive
- Content items with `mode: data` do not generate routed pages and therefore do not enter `pages_by_id`, unless they are written by Notion completion
- Optional: for Notion relation pageIds, perform “batch completion” to add pages outside the current site into the index; this requires `NOTION_TOKEN`
- Completion only happens during the build phase (derive-pages). Reading `site.data.pages_by_id[...]` in templates does not trigger API requests
- Completed pages automatically parse Notion properties into `fields`; no extra field names need to be specified
- Completion is enabled only when the site uses a Notion content source; under other content sources this configuration is ignored
- `field_keys`: specifies which fields to scan for relation pageIds. Field values should be id lists at `page.fields.<key>.value[]`. If omitted, no completion is performed; only the index of this site’s routed pages is generated.
- Completed pages automatically extract top-level Notion page `cover` and `icon` fields and inject them into `fields`, matching the behavior of `InjectPageCoverAndIcon` in the main content pipeline
- Image URLs in completed page fields, such as cover/icon and other fields specified by `content.media.fieldKeys`, are automatically downloaded by `ImageAssetLocalizer` and rewritten to local paths, avoiding generated pages that still reference temporary Notion S3 URLs
- Relation ID matching supports keys with source prefixes, such as `posts_content:pageId`: if a pageId already exists in the index as `sourceKey:pageId`, no duplicate Notion API request is issued

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

## pagination (IDerivePagesPlugin)

File: `PaginationPlugin.cs`

When a blog has more posts than pageSize, derives paginated pages for each collection with pagination enabled:

- `/blog/page/2/` → `blog/page/2/index.html`
- … through the final page

Notes:
- Derived pages use the collection's explicit pagination template when configured, otherwise the active theme must declare a template accepting `kind: pagination`
- Page content is generated by the plugin and includes Prev/Next links
- Supports independent pagination for multiple collections (v3.0+):
  - Each collection with `pagination.enabled: true` generates its own paginated pages
  - `pagination.pageSize`: number of items per page, default 10
  - `pagination.urlPattern`: URL pattern, with `:num` placeholder; default `page/:num/`, can be set to `p/:num/`
  - `pagination.firstPageUsesListRoute`: whether the first page uses listRoute, default true
- Injected fields:
  - `page.fields.items.value[]`: article list for the current page (`{title, url, publish_date, summary?}`)
  - `page.fields.pagination.value`: `{page, page_size, total_pages, has_prev, has_next}`

## archive (IDerivePagesPlugin)

File: `ArchivePlugin.cs`

Derives archive pages by content publish time:

- `/blog/archive/` → archive root index page
- `/blog/archive/<year>/` → year page
- `/blog/archive/<year>/<month>/` → month page
- `/blog/archive/<year>/<month>/<day>/` → day page (v3.0+, `depth: daily`)

Notes:
- Derived pages use `collection.output.archiveDetail.template` when configured, otherwise the active theme must declare a template accepting `kind: archive`
- Page content is generated by the plugin as ul/li link lists
- Enhanced configuration (v3.0+):
  - `collection.output.archiveDetail.depth`: `yearly` / `monthly` (default) / `daily`
  - `collection.output.archiveDetail.template`: custom template path
  - `collection.output.archiveDetail.routePrefix`: custom URL prefix, default `archive`

## path-report (IAfterBuildPlugin, external plugin)

File: `src/plugins/PathReportPlugin/PathReportPlugin.cs`

Debugging plugin that generates a path audit report after build.

- Output: `<outputDir>/_debug/paths-report.json`
- Order: `int.MaxValue`, executed last
- Report content: rootDir, cacheDir, distDir, themeRoot, layoutsDir, assetsDir, and file lists under each directory

### Configuration

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

| Option | Type | Default | Description |
|---|---:|---|---|
| `wechatMaterialUpload.enabled` | bool | `false` | Whether to upload material to a WeChat Official Account after build |
| `wechatMaterialUpload.file` | string | `assets/imgs/default.png` | File to upload, relative to the output directory |
| `wechatMaterialUpload.type` | string | `image` | Material type |
| `wechatMaterialUpload.wechat.appIdEnv` | string | - | Environment variable name that stores the AppID |
| `wechatMaterialUpload.wechat.appSecretEnv` | string | - | Environment variable name that stores the AppSecret |

Note: the uploaded file path is constrained for security and cannot escape the output directory.

## llms-txt (publish projection adapter)

Source: `PublishRepresentationRegistry` adapter reusing `LlmsTxtPlugin` writer helpers.

Generates AI-friendly site artifacts for generative engine optimization (GEO):

- **llms.txt**: Markdown index file following the [llmstxt.org](https://llmstxt.org) standard, containing Documentation, Articles, and Optional sections. Controlled by `site.seo.geo.llmsTxt`, default true. Article count is limited by `site.seo.geo.llmsTxtMaxArticles`, default 20.
- **llms-full.txt**: full-text export of all indexable pages with HTML stripped. Controlled by `site.seo.geo.llmsFullTxt`, default false.
- **AI crawler robots.txt rules**: adds `Allow`/`Disallow` directives for known AI crawler user-agents: GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI, PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot. Controlled by `site.seo.geo.aiBotMode` (`allow`/`block`/`selective`).

Configuration example:

```yaml
site:
  seo:
    geo:
      enabled: true
      llmsTxt: true
      llmsFullTxt: false
      llmsTxtMaxArticles: 20
      aiBotMode: allow
      aiBotAllowList: [GPTBot, PerplexityBot]
      aiBotBlockList: [CCBot]
```

Related docs: [GEO Architecture](./geo.md)

## related-content (IDerivePagesPlugin, v3.0+)

File: `RelatedContentPlugin.cs`

Calculates related content for each article by weighted matching across tags/categories/keywords/collection/date dimensions:

- Configuration: enable with `site.related.enabled: true`
- `site.related.threshold`: minimum score threshold, default 80
- `site.related.limit`: maximum recommendations per page, default 5
- `site.related.indices`: matching dimensions and weights, default tags(80) + categories(60)
- Supported dimensions: `tags`, `categories`, `keywords`, `collection`/`type`, `date`
- Data injection: `context.Data["__related_pages"]`, a dictionary indexed by content item ID
- Exclusion rules: archive and pagination derived pages are automatically skipped

## alias (IDerivePagesPlugin, v3.0+)

File: `AliasPlugin.cs`

Generates HTML redirect pages from front matter `aliases`:

- Each alias generates one HTML file containing `<meta http-equiv="refresh">` and `<link rel="canonical">`
- Supports a single string or a list: `aliases: /old-url/` or `aliases: [/old1/, /old2/]`
- URLs are automatically normalized by completing leading/trailing `/`
- Generated pages are marked as `type: redirect` and automatically excluded from the sitemap

## data-files (IDerivePagesPlugin, v3.0+)

File: `DataFilesPlugin.cs`

Loads YAML/JSON/TOML data files under the `data/` directory:

- Data injection: `context.Data["__data_files"]`
- Supports nested subdirectories with recursive loading
- Multilingual support: `data/{lang}/` subdirectories are loaded by language
- In multilingual mode: shared root-level files + language-specific overrides

## menu (IAfterBuildPlugin, v3.0+)

File: `MenuPlugin.cs`

Outputs `menus.json` and injects `context.Data["menus"]`:

- Configuration: multiple menus such as `site.menus.main` / `site.menus.footer`
- Supports unlimited nested levels through the `children` field
- Sorted by `weight`; lower weight comes first
- Templates access menus through `site.menus.main` / `site.menus.footer`

## image-processing (IAfterBuildPlugin, v3.0+)

File: `ImageProcessingPlugin.cs`

Generates multiple image size variants through a CLI tool (ImageMagick):

- Configuration: enable with `theme.images.enabled: true`
- Generates multi-size variants for JPG/PNG images under `assets/`, such as `-480w`, `-768w`, `-1200w`
- `theme.images.sizes`: size list, default `[480, 768, 1200]`
- `theme.images.quality`: image quality, default 80
- Data injection: `context.Data["__image_srcsets"]` (srcset attribute data)
- Dependency: ImageMagick must be installed (`magick` or `convert` command); if not installed, the plugin skips processing and outputs a warning

## Derived Page Route Validation

All derive-pages plugins (Pagination, Archive, Taxonomy) share the same route validation pipeline:

1. **Per-plugin conflict check** — `PluginRunner.ApplyDeriveConflictPolicy` checks each derived page for conflicts with content routes and already accepted derived routes by comparing normalized URLs and outputPath values.
2. **Final inventory validation** — `RouteInventoryValidator.ValidateFinalRoutes` checks the complete route set (content + derived + list routes) before rendering begins.
3. **Doctor integration** — `bukit doctor` runs content route validation through `RouteInventoryValidator.BuildContentRoutesAsync` + `ValidateContentRoutes`, detecting conflicts without a full build.

All derived pages follow `site.outputPathEncoding`, applied through `RoutePathBuilder.BuildOutputPathFromUrl`.

## visual-feedback (ProcessPluginHost, external protocol)

Plugin directory: `src/plugins/VisualFeedbackPlugin/`

- Hook: `after-build`
- Output: `<outputDir>/.bukit/visual-report.json`
- Dependency: Playwright (`npx playwright`) for screenshot capture; OpenAI-compatible API for AI analysis
- Configuration: `plugins.visual-feedback.options`

Configuration reference (`site.yaml`):
```yaml
plugins:
  visual-feedback:
    enabled: true
    pluginFailMode: warn
    options:
      baseUrl: "http://localhost:4173"
      aiProvider: "openai"         # openai | azure-openai | custom
      aiModel: "gpt-4o"           # requires vision-capable model
      aiApiKey: "${OPENAI_API_KEY}"
      aiEndpoint: null             # custom endpoint when aiProvider != openai
      captureWidths: [375, 768, 1440]
      outputReport: ".bukit/visual-report.json"
      screenshotDir: ".bukit/screenshots"
```

**Report structure:**
- `summary`: overall scores (layout / readability / color / a11y / responsive, each 0-100) and aggregate issues
- `pages[]`: per-page results with per-width screenshot analysis and AI feedback text

**Behavior:**
- After build completes, captures full-page screenshots at each configured viewport width for every page
- Sends screenshots to the configured AI vision model for 5-dimension quality assessment
- Writes structured report to `.bukit/visual-report.json`
- Without `aiApiKey`: screenshots are still captured, but scores default to 0 with a config-notice message

**Multilingual:** single report with all language-specific pages under their respective URLs.

**Related CLI:** `bukit visual generate` generates standalone Playwright screenshot-comparison tests.
