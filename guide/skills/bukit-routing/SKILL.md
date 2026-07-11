---
name: bukit-routing
description: Use when working with Bukit route rules, collection permalinks, list routes, filtered lists, taxonomy routes, or route conflicts.
---

# Bukit Routing

## Route Contract

Every content document needs a non-empty collection, even when it has a full
route override or a matching type permalink. Resolution order is:

1. complete route override;
2. `site.collections.<collection>`;
3. `site.permalinks.<type>`;
4. fail when no rule matches.

A partial override overlays the already resolved base route. Output paths are
derived from URLs; `outputPath` fields are rejected.

For `type: article, collection: news`, collection rules and all grouping/output
consumers use `news`, while the type permalink and SEO article classification
use `article`. `{type}` expands `article`; `{collection}` expands `news`.
A permalink only supplies a route pattern and never replaces collection
membership.

| Consumer | Key |
|---|---|
| Collection route, lists, pagination, filtered lists, archive | `collection` |
| RSS/feed, sitemap output policy, field scope, collection schema mode | `collection` |
| Type permalink, SEO Article/BlogPosting decision | `type` |
| Search | Separate `type`, `contentType`, and `collection` fields |

Data modules do not require collection and do not enter page routing or
collection indexes. Type and collection never derive from each other.

## Common Mistakes

- Treating a type permalink or route override as collection membership.
- Grouping lists or feeds by type instead of collection.

List route graph generation covers collection lists, filtered lists, taxonomy
routes, and pagination. Verify changes with `bukit build` and route reports.
