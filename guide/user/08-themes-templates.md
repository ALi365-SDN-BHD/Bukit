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
`page.seo.json_ld` is not reported. The warning comes only from pages rendered in
that build, so an incremental build of unchanged pages is silent; run a clean build
to see every page. Core does not deduplicate kept scripts against its own JSON-LD:
keep only types Core does not generate, or the page publishes, for example, two
`BreadcrumbList` blocks. Kept head JSON-LD is validated by the
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
