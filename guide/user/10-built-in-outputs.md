# Built-in Outputs

Bukit Core 1.0 includes built-in runtime behavior for common static-site outputs.

## Common Outputs

| Output | Config area |
|---|---|
| Pages and list pages | `content.sources`, `site.collections` |
| Taxonomy pages and data | `taxonomy`, content fields |
| Pagination | `site.collections.*.pagination`, `site.pagination` |
| Archives | `site.collections.*.output.archive` |
| Feeds | `site.feed`, collection output |
| Sitemap | `site.sitemapMode`, `site.sitemapDetail` |
| Search indexes | `site.search`, `site.searchIncludeDerived` |
| Related content | `site.related` |
| Menus | `site.menus` |
| SEO reports | `site.seo`, `.bukit/seo-report.json` |
| GEO reports and AI files | `site.seo.geo`, `.bukit/geo-report.json` |

## Built-in Plugin Toggles

Core only loads built-in plugin sources. Use `site.plugins` to disable a known built-in when necessary.

```yaml
site:
  plugins:
    Menu:
      enabled: false
```

Unknown or misspelled toggles are configuration problems. Prefer leaving built-ins enabled unless you have a focused reason to change output behavior.
