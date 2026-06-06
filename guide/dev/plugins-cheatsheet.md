# Bukit Plugin Cheatsheet

## Built-in Plugins

| Plugin | Type | Output | Dependency |
|---|---|---|---|
| taxonomy | DerivePages+AfterBuild | `/tags/`, `/categories/`, `taxonomy.json` | `meta.tags`/`meta.categories` |
| pagination | DerivePages | `/blog/page/2/` etc. | Blog posts > 10 |
| archive | DerivePages | Archive pages | Blog content |
| pages-index | DerivePages | `site.data.pages_by_id` | None |
| path-report | AfterBuild (external) | `_debug/paths-report.json` | Debug |
| visual-feedback | AfterBuild (external) | `dist/.bukit/visual-report.json`, `dist/.bukit/screenshots/*.png` | Playwright, OpenAI-compatible API (optional) |

## Plugin Configuration

```yaml
site:
  plugins:
    sitemap: true          # Shorthand toggle
    path-report:
      enabled: true
      options: {}          # Custom parameters
```

## Failure Policy: `site.pluginFailMode: strict|warn`

## Publish Projection Outputs
- `sitemap.xml`: projection-owned; requires `site.url`
- `rss.xml`, `feed/atom.xml`, `feed/feed.json`: projection-owned; requires `site.url`
- `search.json` and optional `bukit-search.html`: projection-owned
- `llms.txt`, `llms-full.txt`, `robots.txt`, `agent-manifest.json`: projection-owned

## sitemap Projection
- Output: `sitemap.xml` (requires `site.url`)
- Includes: `/`, `/blog/`, `/pages/`, all routed + derived pages
- Excludes: pages with `noindex` meta
- Multilingual: controlled by `site.sitemapMode`

## feed Projection
- Output: `rss.xml` (requires `site.url`)
- Input: routed content only
- Multilingual: controlled by `site.rssMode`

## search Projection
- Output: `search.json` (does not require `site.url`)
- Fields: id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt
- `site.searchIncludeDerived: true` includes derived pages

## taxonomy Plugin
- Pages: `/tags/`, `/tags/<slug>/`, `/categories/`, `/categories/<slug>/`
- Template: `pages/page.html` (configurable)
- Pagination: `taxonomy.pageSize` (default 10)
- Custom kinds: `taxonomy.kinds[]` for arbitrary taxonomy dimensions

### Taxonomy Templates
```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/tax-index.html
  termTemplate: pages/tax-term.html
  pageSize: 10
  kinds:
    - key: tags
      kind: tags
      title: Tags
    - key: categories
      kind: categories
      title: Categories
      hierarchical: true   # Enable parent-child hierarchy
    - key: series
      kind: series
      title: Series         # Custom taxonomy dimension
```

### Taxonomy Template Variables

**Index page** (`/tags/`):
- `page.fields.terms.value[]` → `{ title, slug, url, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`

**Detail page** (`/tags/<slug>/`):
- `page.fields.items.value[]` → `{ title, url, publish_date, summary }`
- `page.fields.taxonomy.value` → `{ kind, term, slug, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
- `page.fields.pagination.value` → `{ page, page_size, total, total_pages, has_prev, has_next }`

### New Fields (v3.0.0+)

| Field | Type | Source | Description |
|------|------|------|------|
| `description` | string? | data source or _index.md | Term description text |
| `image` | string? | data source or _index.md | Term cover image |
| `weight` | int? | data source or _index.md | Sort weight (higher = first) |
| `parent` | string? | data source or _index.md | Parent term slug |
| `children` | string[]? | auto-computed (hierarchical) | Child term slugs |
| `ancestors` | string[]? | auto-computed (hierarchical) | Ancestor slug chain |
| `aliases` | string[]? | data source | Alias list (auto redirect) |

### Auto-Generated Outputs (v3.0.0+)

| Artifact | Path | Description |
|------|------|------|
| `taxonomy.json` | `<output>/taxonomy.json` | Structured data (schema v2) |
| RSS feeds | `<output>/<kind>/<slug>/feed.xml` | Per-term RSS 2.0 |
| Alias redirects | `<output>/<kind>/<alias>/index.html` | HTML meta refresh redirect |

### Taxonomy Snippet

```scriban
{{ layout "layouts/base.html" }}
<h1>{{ page.title }}</h1>
<ul>
{{ for item in page.fields.items.value }}
  <li>
    <a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a>
    {{ if item.publish_date }}
      <time>{{ item.publish_date | date.to_string "%Y-%m-%d" }}</time>
    {{ end }}
  </li>
{{ end }}
</ul>
{{ if page.fields.pagination.value.has_prev }}
  <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page - 1 }}/">Prev</a>
{{ end }}
{{ if page.fields.pagination.value.has_next }}
  <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page + 1 }}/">Next</a>
{{ end }}
```

## visual-feedback Plugin (external protocol)
- **Hook**: after-build
- **Type**: External (ProcessPluginHost)
- **Source**: `src/plugins/VisualFeedbackPlugin/`
- **Outputs**:
  - `dist/.bukit/visual-report.json` — AI-powered 5-dimension visual quality report
  - `dist/.bukit/screenshots/*.png` — full-page screenshots per page per viewport width
- **Config**: `plugins.visual-feedback.options` (baseUrl, aiProvider, aiModel, aiApiKey, captureWidths, etc.)
- **Dependencies**: Playwright (screenshot capture), OpenAI-compatible API (AI analysis, optional)
- **CLI related**: `bukit visual generate` — generates Playwright screenshot-comparison test scripts

## Image Localization (`content.media`)

```yaml
content:
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/default.jpg
    fieldKeys: [cover, image, thumbnail, og_image]
```
