# Give pages to only some collection entries

## Why

Every entry of a content collection gets a page. travelWeb's city table has 18 rows and only 8 get
pages; the other 10 are only linked and listed. Roadmap item F13
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `site.collections.<name>.routeWhen: { field, eq }`: only entries whose top-level field equals the
  value get a page.
- Entries without a page leave every page output (lists, feeds, sitemap, search, alternates) but
  stay loaded as content link targets with a null `url`. They do not link themselves and get no
  backref list.

## Impact

- Public API: none. `CollectionConfig.RouteWhen` and `CollectionRouteWhenConfig` are internal and
  JsonInclude'd for i18n worktree workers.
- Engine: `CollectionPagePolicy.HasPage` replaces the data-item test in `RoutePipeline`,
  `RouteInventoryValidator`, `SeoAlternatesService` and content link validation.
- Out of scope: other operators (not equal, contains), and the two-source alternative.
