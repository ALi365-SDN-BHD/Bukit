# Routing

Routing is split between `Bukit.Routing.RouteGenerator` and
`Bukit.Engine.RoutePipeline`.

## Route Sources

Priority:

1. Complete route override through the front matter `route` object's `url` and
   `template` fields, or top-level `url` plus `template`.
2. `site.collections.<collection>.permalink`.
3. `site.permalinks.<type>`.

If no rule matches, config validation fails during route generation.
A partial override overlays the already resolved collection or type-permalink
base route.

Every routed content document must have a non-empty collection before route
selection. This remains true when a complete override or matching type
permalink exists because collection also controls lists, feeds, sitemap output,
schema scope, and output policy. Data modules are not routed and do not require
collection.

For `type: article` and `collection: news`, `{type}` expands to `article` and
`{collection}` expands to `news`. A `site.collections.news` rule is matched by
collection; otherwise `site.permalinks.article` supplies the route pattern.
The permalink chooses a URL pattern but never assigns collection membership.

## Removed Fields

Top-level `outputPath` and the `outputPath` field inside the front matter
`route` object are rejected. Output paths are derived from normalized internal
URLs.

## List Route Graph

`ListRouteGraphBuilder` builds collection list pages, filtered lists, taxonomy
routes, pagination metadata, and route plans consumed by render and SEO stages.
Collection lists, pagination, filtered lists, archives, RSS/feed selection,
sitemap output policy, field scopes, and collection schema mode use collection
only. SEO Article/BlogPosting selection uses type only, and search stores type,
contentType, and collection separately.

## Conflict Detection

`RouteInventoryValidator` validates content routes, list routes, derived routes,
and static HTML routes. Conflicts must be fixed by adjusting slugs, route URLs,
collection rules, list routes, or taxonomy prefixes.

SEO breadcrumb resolution consumes that final inventory. It selects only real
strict URL ancestors, ignores case and trailing-slash differences, excludes the
root, and always appends the current non-home route. It never title-cases a
missing URL segment into a synthetic parent. Visible route metadata titles take
precedence, followed by resolved list titles, content/derived titles, and
managed static page titles.
