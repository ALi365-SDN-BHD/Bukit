---
name: bukit-routing
description: Use when deciding or debugging Bukit URLs — site.collections rules and permalink placeholders, site.permalinks by type, front matter url/template overrides, the home page, collection list routes, pagination, filtered lists, taxonomy (tags/categories) pages, archives, URL percent-decoding, and route conflicts.
---

# Bukit Routing

Each content document gets exactly one route (URL + template). Lists, taxonomy pages,
aliases, and static HTML are added afterwards, then all routes are checked together
for conflicts.

## Rule order for a content document

1. Full override: front matter with both `url` and `template` (top level or under
   `route:`).
2. Base rule: `site.collections.<collection>` (`permalink` + `template`), else
   `site.permalinks.<type>` (pattern only; the template must come from theme.yaml),
   else fail with `No route rule matches content document '<id>' (collection=..., type=...)`.
3. Partial override on top of the base rule: `url` alone or `template` alone.

A partial override still needs a base rule, and every content document needs a
collection even with a full override.

```yaml
site:
  collections:
    post:
      permalink: /blog/{year}/{month}/{slug}/   # must include {slug}
      template: pages/post.html
      listRoute: /blog/
      listTitle: Blog
      listTemplate: pages/list.html
      pagination: { enabled: true, pageSize: 10, urlPattern: "page/:num/" }
    page:
      permalink: /{slug}/
      template: pages/page.html
  permalinks:
    article: /articles/{slug}/        # by document type
```

Other collection keys:

- `indexPolicy: {minimumItems: 3, belowMinimum: noindex-follow}` (defaults `0` /
  `index`): list, pagination, and filtered-list pages with fewer items get
  `noindex,follow`. `noindexWhenEmpty: true` equals `{minimumItems: 1, belowMinimum:
  noindex-follow}`; declaring both fails.
- `schemaFailMode: off|warn|strict` overrides `build.schemaFailMode` for documents in
  this collection (`bukit-content`, "Model schema").
- `output.rss` (default `true`), `output.feedTitle` / `output.feedDescription`
  (default `site.title` / `site.description`) configure the collection feed
  (`bukit-outputs`).

Placeholders (case-insensitive): `{slug}`, `{title}` (slugified), `{year}`, `{month}`,
`{day}` (from `publishAt`, UTC), `{type}`, `{collection}`. Unknown placeholders stay in
the URL literally, so check spelling.

```yaml
---
title: About
collection: page
url: /about-us/                 # partial: keeps the collection template
---
```

```yaml
---
title: Landing
collection: page
route: { url: /landing.html, template: pages/landing.html }   # full override
---
```

## URL shape

- A last segment ending in `.html`, `.htm`, `.xml`, `.json`, or `.txt` (no trailing
  slash) is written as that file. Everything else becomes `<path>/index.html` with a
  trailing slash.
- Patterns or URLs ending in asset extensions (`.css`, `.js`, images, fonts, `.pdf`,
  archives, media, Office files) are rejected; such files belong in `static/`.
- URLs must start with `/`, with no query, fragment, `..`, encoded `/` or `\`, control
  characters, trailing dot/space segments, or Windows device names.
- `page.url` is root-relative without `baseUrl`; templates prepend `site.base_url`.

## Percent-encoded URLs

Front matter `url` and `aliases` that are percent-encoded are decoded one layer
(`/%e8%81%94%e7%b3%bb/` → `/联系/`) and the build warns
`event=route.url.decoded ... hint=fix_url_in_source` or `event=route.alias.decoded`.
Write the real Unicode path in the source instead. Decoding is skipped (value kept as
is) when it would produce `/`, `\`, `.`, `%`, control characters, invalid UTF-8, or
invisible characters such as zero-width or non-ASCII spaces. Permalink patterns,
slugs, and list routes are not decoded.

## Home page

`/` is always the home list route, rendered with theme.yaml `templates.home` or
`pages/index.html`. `items` holds all routed content, newest first, unpaginated. A
content page with `url: /` conflicts with it.

## Collection lists and pagination

A collection gets a list page only when it has `listRoute` (starting with `/`). Its
template is `listTemplate`, else a theme.yaml entry with `accepts.kind: list`. Items
sort by `publishAt` descending, then `content.ordering.tieBreakField`, then URL.

Pagination (`site.collections.<c>.pagination`): `enabled` (false), `pageSize` (10),
`urlPattern` (`page/:num/`; relative, must contain `:num`, `{num}`, or `{page}`),
`firstPageUsesListRoute` (true). Page N lives at `<listRoute><urlPattern>`.

## Filtered lists

```yaml
site:
  collections:
    post:
      listRoute: /blog/            # required, or filtered lists are silently skipped
      filteredLists:
        - field: category
          operator: equals         # equals | contains (use value) | in (use values)
          value: engineering
          listRoute: /blog/engineering/
          title: Engineering
          emptyBehavior: skip      # render (default) | skip
```

Matching is case-insensitive and also compares slugs (`Release Notes` =
`release-notes`); list fields match if any element matches. Filtered lists are always
paginated using `pageSize` from the filter or the collection, even when collection
pagination is disabled. Templates receive `filter`, `collection`, and `pagination`.

## Taxonomy pages

Without a `taxonomy:` section, any `tags` or `categories` in front matter automatically
creates `/tags/`, `/tags/<term>/` (paginated `page/N/`), and the same for categories.
These need theme templates matching `accepts.kind: taxonomy_index` and
`taxonomy_term` inside the theme layouts directory; otherwise the build fails. Fixes:
add the templates, set `taxonomy.outputMode: data` (no pages, data only), define
`taxonomy.kinds` without that field, or disable the built-in with
`site.plugins.taxonomy: false`. `fields_only` still builds pages and still needs the
templates.

```yaml
taxonomy:
  outputMode: both          # both | pages | data | fields_only
  pageSize: 10
  emptyTerms: render        # render | noindex | skip
  kinds:
    - key: tags             # front matter field
      routePrefix: /topics  # default /<kind>
      termTemplate: pages/taxonomy-term.html
      indexTemplate: pages/taxonomy-index.html
  indexEnabled: true        # build /tags/ index pages; per kind: kinds[].indexEnabled
  itemFields: [cover, date] # extra fields on each term-page item
  pinField: pinned          # default; true/yes/1 pins an entry
  pinOrderField: pin_order  # any value pins; lower first
  pinFieldBySource: { news: Featured }   # per source name; also pinOrderFieldBySource
  tieBreakField: title      # default content.ordering.tieBreakField, then title
  tieBreakOrder: asc        # asc | desc
```

Pinning and tie-break apply to term pages and term feeds only. `emptyTerms` handles
terms with no pages: `noindex` renders them with `noindex`, `skip` builds no page.
`itemFields` values appear on each item and under `fields.<key>.value`; `date` falls
back to the publish date.

With `kinds`, only listed kinds are built. Term pages get `taxonomy` and `items`; index
pages get the term list in `page.fields.terms.value`. Use `util.term_slug` to build
term links that match.

## Archives

`site.collections.<c>.output.archive: true` builds year and month archive pages for
that collection. Requirements and limits:

- The collection must define `listRoute`.
- Only the first collection with `archive: true` gets archives.
- URLs are fixed: `<listRoute>archive/`, `<listRoute>archive/<yyyy>/`,
  `<listRoute>archive/<yyyy>/<MM>/`.
- The theme needs a template with `accepts.kind: archive`.
- `output.archiveDetail` (`depth`, `template`, `routePrefix`) passes validation but is
  ignored by the build today. Do not rely on it.

## Route conflicts

All final routes (home, content, lists, taxonomy, aliases, static HTML) are compared
case-insensitively by URL and output path. A clash fails with `Route conflict on
url|outputPath: <key>. Conflicting routes: ...` (BKT-0201). Usual causes: duplicate
slugs in one pattern, `url: /`, `addToCollections` clones with the same permalink, a
taxonomy prefix equal to a `listRoute`, or a rendered `static/*.html` matching a page.
Derived-page conflicts follow `site.deriveConflictPolicy`.

## Verify

```bash
bukit build
cat dist/.bukit/routes.json     # when build.report.enabled (default)
```
