# Named site queries

## Why

Ordered lists (cities with routes, top six featured routes, routes by month, attraction types) are
generated outside Bukit into `order.json` and `months.json`; templates read `D.order` in 44
places. Roadmap item F4 (`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `site.queries.<name>: { from, where, sort, select, distinct, group, limit }`, read in templates as
  `site.queries.<name>`.
- Filters: `{ field, eq }`, `{ field, in }` (filtered-list matching, list fields match any item),
  `{ hasBackref }`. Stable sort by value or by position in a value list; missing values last.
- `distinct` returns field values; `group` returns `{ value, items }` per listed value; `limit`
  applies per group.

## Impact

- Config: `SiteConfig.Queries` (internal), `SiteQueryConfigReader`, strict keys, schema.
- Engine: `SiteQueryResolver` runs in `RoutePipeline` after derived fields; results reach templates
  through the internal `__queries` site data key, aliased as `site.queries`, and are tracked by
  incremental builds as one collection (`site.queries`).
- `FilteredListMatcher` gains an overload; `ContentLinkResolver.Entry` becomes internal.
- No public API change. Docs: `04-site-yaml-config.md`, templating, config and build skills,
  troubleshooting, CHANGELOG.
- Out of scope: per-page parameters (use `array.filter`), sorting by enum definition order (F8).
