# Bukit Plugin Cheatsheet

## Built-in Plugins

| Plugin | Type | Output | Dependency |
|---|---|---|---|
| sitemap | AfterBuild | `sitemap.xml` | `site.url` required |
| rss | AfterBuild | `rss.xml` | `site.url` required |
| search-index | AfterBuild | `search.json` | None |
| taxonomy | DerivePages+AfterBuild | `/tags/`, `/categories/`, `taxonomy.json` | `meta.tags`/`meta.categories` |
| pagination | DerivePages | `/blog/page/2/` etc. | Blog posts > 10 |
| archive | DerivePages | Archive pages | Blog content |
| pages-index | DerivePages | `site.data.pages_by_id` | None |
| path-report | AfterBuild (external) | `_debug/paths-report.json` | Debug |

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

## sitemap Plugin
- Output: `sitemap.xml` (requires `site.url`)
- Includes: `/`, `/blog/`, `/pages/`, all routed + derived pages
- Excludes: pages with `noindex` meta
- Multilingual: controlled by `site.sitemapMode`

## rss Plugin
- Output: `rss.xml` (requires `site.url`)
- Input: routed content only
- Multilingual: controlled by `site.rssMode`

## search-index Plugin
- Output: `search.json` (does not require `site.url`)
- Fields: id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt
- `site.searchIncludeDerived: true` includes derived pages

## taxonomy Plugin
- Pages: `/tags/`, `/tags/<slug>/`, `/categories/`, `/categories/<slug>/`
- Template: `pages/page.html` (configurable)
- Pagination: `taxonomy.pageSize` (default 10)

### Taxonomy Templates
```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/tax-index.html
  termTemplate: pages/tax-term.html
  pageSize: 10
```

Index page variables: `page.fields.terms.value[]` (title/slug/url/count)
Detail page variables: `page.fields.items.value[]`, `page.fields.taxonomy.value`, `page.fields.pagination.value`

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
