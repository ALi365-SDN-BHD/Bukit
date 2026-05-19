# Built-in Plugins (BuiltIn) Artifacts and Boundaries

This page describes the "output contracts" of built-in plugins (what files/pages are generated, what config they depend on, how they behave under multilingual settings).

Implementation directory: `src/Bukit.Engine/Plugins/BuiltIn/`

Related docs: [Plugin System](./plugins.md), [i18n & SEO](./i18n-seo.md), [Engine Fixed Outputs](./engine-outputs.zh-CN.md), [GEO](./geo.md)

## Plugin Overview (9 plugins)

| Plugin | Hook | Key Output |
|--------|------|-----------|
| **CollectionRouteIndex** | (internal index) | In-memory route index grouped by collection |
| **TaxonomyPlugin** | derive-pages + after-build | Taxonomy index/term pages |
| **PaginationPlugin** | derive-pages | Paginated list pages |
| **PagesIndexPlugin** | derive-pages | Page index JSON for template consumption |
| **ArchivePlugin** | derive-pages | Yearly/monthly archive pages |
| **SitemapPlugin** | after-build | `sitemap.xml` |
| **RssPlugin** | after-build | `rss.xml` |
| **SearchIndexPlugin** | after-build | `search.json` / `search.index.json` |
| **LlmsTxtPlugin** | after-build | `llms.txt` / `llms-full.txt` + AI crawler rules |


## collection-route-index (Internal Index)

File: `CollectionRouteIndex.cs`

This is not a plugin hook implementation but an internal in-memory index consumed by multiple plugins (Pagination, Archive, LlmsTxt, Taxonomy). It groups all routed content items by `collection` key and provides sorted lookup:

- `GetByCollection(collectionKey)` — ordered by `PublishAt` descending
- `GetByRoutePrefix(prefix)` — filtered by URL prefix
- `GetOrBuild(context)` — lazy-build and cache in `context.Data`

Collection key resolution: content `meta["collection"]` → fallback to `meta["type"]`.


## taxonomy (IDerivePagesPlugin + IAfterBuildPlugin)

File: `TaxonomyPlugin.cs`

Derives pages from `meta.tags` / `meta.categories`:
- `/tags/` → `tags/index.html`
- `/tags/<slug>/` → `tags/<slug>/index.html`
- `/categories/` → `categories/index.html`
- `/categories/<slug>/` → `categories/<slug>/index.html`

Template: default `pages/page.html`, configurable via `taxonomy.template`/`taxonomy.indexTemplate`/`taxonomy.termTemplate`

Config-driven by `taxonomy` node in site.yaml. Custom taxonomy kinds (beyond tags/categories) are supported via `taxonomy.kinds[].key`.


## pagination (IDerivePagesPlugin)

File: `PaginationPlugin.cs`

Generates additional list pages when a collection has more items than `pageSize`. Requires `site.collections.<key>.pagination.enabled: true`.

- Triggers when `posts.Count > pageSize`
- Generates pages at `<listRoute>/page/2/`, `<listRoute>/page/3/`, ...
- Uses `pages/pagination.html` template when detected via `TemplateCapabilitiesResolver.SupportsPagination()`, otherwise falls back to `pages/page.html`
- Each page exposes `fields.pagination` (page/page_size/total_pages) and `fields.items` (slice of the collection)

Config example:

```yaml
site:
  collections:
    post:
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
```


## pages-index (IDerivePagesPlugin)

File: `PagesIndexPlugin.cs`

Generates a JSON page index consumed by templates that need to iterate all pages. The index includes each page's `id`, `title`, `url`, `slug`, `type`, and field values.

- For Notion-sourced content, can optionally fetch supplementary page data via `INotionPageFetcher`
- Cached in `build-manifest` for incremental builds
- Consumed by templates via `site.data.pages_index`


## archive (IDerivePagesPlugin)

File: `ArchivePlugin.cs`

Generates hierarchical archive pages from the collection with `listRoute`:

- **Archive index**: `<listRoute>/archive/` — lists all years
- **Year pages**: `<listRoute>/archive/2026/` — lists months within that year
- **Month pages**: `<listRoute>/archive/2026/05/` — lists posts from that month

Collection resolved via `site.collections` matching (first collection with `listRoute` that has content). Each archive page exposes `fields.year`, `fields.month`, and `fields.posts` (list of post info).


## sitemap (IAfterBuildPlugin)

File: `SitemapPlugin.cs`
- Output: `<outputDir>/sitemap.xml`
- Dependency: `site.url` must be configured (skipped otherwise)
- Includes: `/`, `/blog/`, `/pages/`, all routed content pages, all derived routes
- lastmod: routed pages prefer `fields.update_time`, fallback to `publishAt`
- Exclusion: pages with `<meta name="robots" content="noindex|none ...">` are excluded
- Multilingual: `merged` mode generates at root; `split` generates per language directory


## rss (IAfterBuildPlugin)

File: `RssPlugin.cs`
- Output: `<outputDir>/rss.xml`
- Dependency: `site.url` must be configured
- Input: routed content only (no derived pages)
- Multilingual: same `merged`/`split` semantics as sitemap


## search-index (IAfterBuildPlugin)

File: `SearchIndexPlugin.cs`
- Output: `<outputDir>/search.json`
- Fields: `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
- `site.searchIncludeDerived` controls whether derived pages are included
- Multilingual: per-language `search.json`; `index` mode generates root `search.index.json`


## llms-txt (IAfterBuildPlugin)

File: `LlmsTxtPlugin.cs`

Generates AI-friendly site artifacts for generative engine optimization (GEO):

- **llms.txt**: Markdown index file following [llmstxt.org](https://llmstxt.org) standard with Documentation, Articles, and Optional sections. Controlled by `site.seo.geo.llmsTxt` (default: true). Limits articles to `site.seo.geo.llmsTxtMaxArticles` (default: 20).
- **llms-full.txt**: Full-text export of all indexable pages (stripped HTML). Controlled by `site.seo.geo.llmsFullTxt` (default: false).
- **AI crawler robots.txt rules**: Adds `Allow`/`Disallow` directives for known AI crawler user-agents (GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI, PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot). Controlled by `site.seo.geo.aiBotMode` (`allow`/`block`/`selective`).

Config example:

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

Related: [GEO architecture](./geo.md)


## Route Validation for Derived Pages

All derive-pages plugins (Pagination, Archive, Taxonomy) share the same route validation pipeline:

1. **Per-plugin conflict check** — `PluginRunner.ApplyDeriveConflictPolicy` checks each derived page against content routes and previously accepted derived routes using normalized URL and outputPath comparison.
2. **Final inventory validation** — `RouteInventoryValidator.ValidateFinalRoutes` checks the complete route set (content + derived + list routes) before rendering begins.
3. **Doctor integration** — `bukit doctor` runs content route validation via `RouteInventoryValidator.BuildContentRoutesAsync` + `ValidateContentRoutes`, detecting conflicts without a full build.

All derived pages respect `site.outputPathEncoding` (applied via `RoutePathBuilder.BuildOutputPathFromUrl`).
