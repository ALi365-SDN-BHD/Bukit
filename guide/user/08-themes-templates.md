# 08 Themes And Templates

Core themes are local filesystem themes resolved by `ThemePathResolver` and
bootstrapped by `ThemeBootstrapper`.

## Theme Layout

```text
themes/site/
  theme.yaml
  layouts/
    layouts/base.html
    pages/page.html
    pages/list.html
    pages/post.html
    pages/search.html
  assets/
    style.css
  static/
    robots-extra.txt
```

## Config

```yaml
theme:
  name: site
  layouts: layouts
  assets: assets
  static: static
  staticTemplate: pages/page.html
  params:
    brand: Bukit
```

Default `layouts`, `assets`, and `static` paths resolve under
`themes/<theme.name>/`. Custom path values are resolved from the site root.

## Template Objects

| Object | Available In | Source |
|---|---|---|
| `site` | all templates | `SiteModel` |
| `page` | page and list templates | `PageInfo` |
| `pages` | page and list templates | page index on detail pages; list items on list routes |
| `items` | list templates | alias for list items |
| `pagination` | paginated lists | `ListPaginationModel` |
| `collection`, `taxonomy`, `filter` | list routes | list route metadata |

On detail pages, `pages` is the page index for the current render batch. Each
index item exposes page metadata but has empty `content`; use `page.content` for
the current page body. On list routes, the existing `pages` and `items`
semantics remain unchanged. Templates should not use the presence or absence of
`pages` to distinguish detail pages from list routes; use the selected template
or explicit route context instead.

`page.seo.title` is the SEO title used by Open Graph, Twitter, search, and the
existing SEO title rules. It is not the content title used by page-level
JSON-LD: JSON-LD `name` and `headline` values use the final visible content
title (the resolved title from matching route metadata when present, otherwise
the canonical content title). `page.seo.document_title` is the separately
resolved final HTML document title. Themes should render it with compatibility
fallbacks:

```scriban
<title>{{ if page.seo }}{{ if page.seo.document_title && page.seo.document_title != "" }}{{ page.seo.document_title | html.escape }}{{ else }}{{ page.seo.title | html.escape }}{{ end }}{{ else }}{{ page.title | html.escape }}{{ end }}</title>
```

The starter theme implements the same fallback with nested Scriban `if`
expressions so older SEO model producers remain compatible.

## Layouts

Scriban layout directives are parsed before rendering. A page template can start
with:

```scriban
{% layout "layouts/base.html" %}
```

The base layout renders child content with `{{ content }}`.

## SEO Head Ownership

- `site.seo.renderMode: inject` makes Core own `<title>` and the other managed
  SEO tags. Core removes every existing `<title>` inside the standard
  `<head>...</head>` and writes one encoded `page.seo.document_title`.
- `theme` exposes `page.seo` but leaves the HTML unchanged; the theme owns the
  final title.
- `off` still exposes `page.seo` and runs diagnostics, but performs no SEO head
  injection. The final build report also audits the emitted HTML.
- A content field `seo_inject: false` skips Core mutation for that content page,
  but does not opt the output out of the final HTML audit.

In `inject` mode Core also replaces the head's `application/ld+json` scripts
with the JSON-LD it generates (WebSite, WebPage, BreadcrumbList, Article types,
FAQPage, HowTo, Organization, LocalBusiness, Person). To publish another schema
type such as `TouristTrip`, `Product` or `Event`, either:

- mark the head script `data-bukit-seo="keep"`; it stays where the theme wrote
  it, before Core's block, byte for byte;
- put the script in `<body>`, which injection never changes; or
- switch to `renderMode: theme` and render the whole head yourself.

An unmarked head script whose content differs from Core's JSON-LD is dropped
with the build warning `seo.theme_json_ld_replaced route=<url> count=<n>`
(collapsed into `event=diagnostics.summary` when more than five pages hit it; it
stays a warning under `diagnostics: strict`). A pretty-printed or re-escaped echo of
`page.seo.json_ld` is not reported. An incremental build reports the warning again
for unchanged pages it skips, so it matches a clean build. Core does not deduplicate kept scripts against its own JSON-LD:
keep only types Core does not generate, or the page publishes, for example, two
`BreadcrumbList` blocks; the build then warns
`seo.theme_json_ld_duplicate_type route=<url> types=<types>`, naming each
top-level type (the root object, root array items and `@graph` items) that a
Core block also declares. It stays a warning under `diagnostics: strict`. Kept head JSON-LD is validated by the
publish audit (`seo.json_ld_invalid`) and its types appear in `schemaTypes`.

Managed-tag scanning is limited to the standard head. An SVG `<title>` in the
body is not removed. If rendered HTML has no complete standard head, Core does
not synthesize one; diagnostics and the final audit report the gap.

## JSON In Templates

`util.json_string` writes a value's text as a JSON string literal and
`util.to_json` serializes any value: objects, arrays, strings, numbers,
booleans, null and dates. Both escape `<`, `>`, `&`, U+2028 and U+2029 as
`\u003c`-style escapes, so content from Markdown or Notion cannot close a
`<script>` element early. `util.to_json` sorts object keys (ordinal), so the
output is byte-for-byte stable. Dictionary keys that become the same text
(such as `1` and `"1"`) keep only the first; template functions are written as
`null`. Both work in page, component and section
templates.

Scriban's built-in `object.to_json` uses reflection that the Native AOT
`bukit` binary disables; Bukit maps it to the same implementation as
`util.to_json`, so either name works.

```scriban
{{ trip = {"@context": "https://schema.org", "@type": "TouristTrip", "name": page.title} }}
<script type="application/ld+json" data-bukit-seo="keep">{{ trip | util.to_json }}</script>
```

## Numbers From Data Files And Front Matter

Whole numbers read from `data/*.json` and `data/*.yaml` (in lists or not), and
front matter fields that hold a single number, are 64-bit integers, while a
number written in a template, such as `2`, is a 32-bit integer. Items of a
front matter list such as `city_ids: [1, 2]` are strings. Both print the same and compare equal with `==`, but Scriban's
array.contains uses .NET equality, which treats the two integer types as
different. Searching a list from data for a number typed in the template
therefore returns `false`, even when the list holds that number:

```scriban
{{ site.data_files.trip.city_ids | array.contains 2 }}
```

The same happens when a front matter list is searched for any number, from the
template, another front matter field or a data file, because its items are
text; searching it for a string (`"2"`) works. Compare with `==` instead, which
converts between these types, either through array.filter with an anonymous
function or in a loop:

```scriban
{{ has_city = (site.data_files.trip.city_ids | array.filter @(do; ret $0 == 2; end) | array.size) > 0 }}

{{ has_city = false }}
{{ for id in site.data_files.trip.city_ids }}{{ if id == 2 }}{{ has_city = true }}{{ end }}{{ end }}
```

array.contains works as expected for a data file list searched for a number
from a data file or a single-number front matter field (for example, a page's
id), and for lists of strings searched for a string.

## Loop Limit

Scriban stops a template that runs too many loop iterations. The default limit
is 1000; set `build.templateLoopLimit` to a positive integer to change it for
page, component and section templates:

```yaml
build:
  templateLoopLimit: 5000
```

The limit counts every iteration inside one outermost `for` or `while` loop,
including nested loops and loops in functions or includes called from it. Two
nested loops over 40 pages and 30 tags therefore count 1200 iterations and fail,
although neither loop reaches 1000 alone. A component or section rendered from
the loop starts its own count. The build fails with `Render failed` and a cause
such as `Exceeding number of iteration limit` that names the setting and its
current value.

Before raising the limit, remove nested loops that only filter. array.filter
and array.map with an anonymous function do not count toward the limit:

```scriban
{{ for city in cities }}
  {{ spots = pages | array.filter @(do; ret ($0.url | string.starts_with city.url); end) }}
  {{ city.title }}: {{ spots | array.size }}
{{ end }}
```

`limit:` (`for p in pages limit:10`) and pagination also keep loops short.

## Live Reload And Capability Decisions

An optional `bukit.templates.yaml` in the resolved layouts directory declares
per-template capabilities:

```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true
      supports_pagination: true
      supports_taxonomy: true
      supports_search_snippets: true
      fields:
        - key: summary
          type: string
          label: Summary
```

Each declared template must exist under layouts and declare at least one
capability. Template paths must remain relative to that directory.

Manifest parsing and static template dependency analysis are content-sensitive.
A subsequent resolver/build call in the same process observes manifest
appearance, deletion or correction, plus changes to root templates, includes,
and layout directive targets. This is a next-call correctness guarantee, not a
promise of instantaneous watcher delivery or removal of every render cache.
