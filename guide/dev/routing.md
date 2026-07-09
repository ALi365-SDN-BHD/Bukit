# Routing

Routing is split between `Bukit.Routing.RouteGenerator` and
`Bukit.Engine.RoutePipeline`.

## Route Sources

Priority:

1. Full route override through the front matter `route` object's `url` and
   `template` fields, or top-level `url` plus `template`.
2. Partial override on top of collection routing.
3. `site.collections.<key>.permalink`.
4. `site.permalinks.<type>`.

If no rule matches, config validation fails during route generation.

## Removed Fields

Top-level `outputPath` and the `outputPath` field inside the front matter
`route` object are rejected. Output paths are derived from normalized internal
URLs.

## List Route Graph

`ListRouteGraphBuilder` builds collection list pages, filtered lists, taxonomy
routes, pagination metadata, and route plans consumed by render and SEO stages.

## Conflict Detection

`RouteInventoryValidator` validates content routes, list routes, derived routes,
and static HTML routes. Conflicts must be fixed by adjusting slugs, route URLs,
collection rules, list routes, or taxonomy prefixes.
