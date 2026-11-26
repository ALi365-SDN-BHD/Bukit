# Design: named site queries

- **Closed operations.** Filter, stable sort, distinct, group, limit; no expressions, so a query
  cannot depend on render order or reach other data.
- **One source of matching.** Filters reuse `FilteredListMatcher`, so `site.queries` and
  `filteredLists` agree on text, slug, date and list-item matching.
- **Pages and entries without a page.** `from` covers both (F13), so queries can list data rows;
  `url` is null for the latter, as in content links.
- **After derived fields.** Queries read backrefs (F3) and derived values (F9).
- **Entry shape.** The content-link entry plus `select`, keeping site data small and stable.
- **Incremental builds.** Results are an internal site data projection like `site.related_pages`,
  tracked as one collection; per-query granularity is a possible later refinement.
- **Naming.** `hasBackref` follows the camelCase of other config keys (the roadmap draft wrote
  `has_backref`).
