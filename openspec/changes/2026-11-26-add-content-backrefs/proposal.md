# Add content backrefs

## Why

`content.links` (F2) lets a route page list its cities and attractions, but a city or attraction page
cannot list the routes that pass through it. Templates cannot work this out alone, and SEO, search and
the mini program need the list in the page fields. This is item F3 of the roadmap
`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`, for travelWeb.

## What Changes

- A `content.links` entry can add `backref: { name, limit, select }`.
- Every routed page of the target collection gets `<name>`: the content pages that link to it, each
  once, in collection list order, at most `limit` (0 keeps all). It also gets `<name>_count`, the total
  before the limit.
- Config validation rejects an invalid name, a negative limit, reserved `select` names and two
  writers of one field in one collection (links and backrefs together).
- An existing field on a target page is kept and reported as `content.link_output_conflict` under
  the target page collection's `schemaFailMode`.

## Impact

- Public API: none. `ContentLinkBackrefConfig` is internal, like the F2 types.
- Config: reader, strict fields, JSON schema and semantic validation of `content.links[].backref`.
- Engine: `ContentLinkResolver` writes backrefs after the links; `CollectionRouteIndex.OrderEntries`
  shares the collection order.
- Docs: `04-site-yaml-config.md`, the `bukit-content-model` skill and CHANGELOG in three languages.
- Out of scope: filters on the backref list (left to named queries, F4), backrefs on data targets,
  and nested backrefs.
