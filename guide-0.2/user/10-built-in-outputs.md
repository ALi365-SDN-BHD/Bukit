# Built-in Outputs

Bukit Core 1.0 includes built-in runtime behavior for common static-site outputs.

## Common Outputs

| Output | Config area |
|---|---|
| Pages and list pages | `content.sources`, `site.collections` |
| Fixed filtered list pages | `site.collections.*.filteredLists` |
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

Use filtered list pages for fixed, manually selected filters that belong to a
collection with `listRoute`; each filtered list matches one explicit `field`
with `operator: equals`, `contains`, or `in`. Filtered lists are paginated at
build time, so a fixed route such as `/companies/malaysia/` can also generate
`/companies/malaysia/page/2/` when the matched item count exceeds `pageSize`.
Use taxonomy when Bukit should derive one page per term from fields such as
tags, categories, or topics.

For a practical migration path from browser-side list behavior to these
build-time outputs, see [18 Static List Routes Migration](./18-static-list-routes-migration.md).
