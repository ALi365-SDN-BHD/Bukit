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
| `pages` | list templates | list items |
| `items` | list templates | alias for list items |
| `pagination` | paginated lists | `ListPaginationModel` |
| `collection`, `taxonomy`, `filter` | list routes | list route metadata |

`page.seo.title` is the semantic title used by Open Graph, Twitter, JSON-LD,
search, and the existing SEO title rules. `page.seo.document_title` is the
separately resolved final HTML document title. Themes should render it with
compatibility fallbacks:

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

Managed-tag scanning is limited to the standard head. An SVG `<title>` in the
body is not removed. If rendered HTML has no complete standard head, Core does
not synthesize one; diagnostics and the final audit report the gap.
