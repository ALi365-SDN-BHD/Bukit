---
name: bukit-templating
description: Use when writing or debugging Bukit Scriban templates — template lookup and selection, layout directive and includes, the site/page/items/pagination/taxonomy/filter/seo model, Bukit helper functions, bukit.templates.yaml capabilities, loop limits, and render errors.
---

# Bukit Templating (Scriban)

Templates are Scriban (`{{ ... }}`), not Liquid or Go templates. `{% ... %}` is only
used for the first-line layout directive and theme shortcodes; Liquid tags like
`{% for %}` print literally.

## Lookup and selection

Files are found in this order: `<siteRoot>/layouts/` (override, if it exists), theme
`layouts/`, parent theme `layouts/`.

- Content page: route template (front matter override or collection `template`), else
  the first theme.yaml `templates` entry whose `accepts` (`type`, `collection`,
  `kind: detail`) matches, else an error.
- Home: the theme.yaml `templates` entry `home` (must be `required: true`), else `pages/index.html`.
- Other kinds: `list`, `taxonomy_index`, `taxonomy_term`, `pagination`, `archive`,
  `search`.

## Layouts and partials

```html
{% layout "layouts/base.html" %}
<article>
  <h1>{{ page.title | html.escape }}</h1>
  {{ page.content }}
</article>
```

The directive must be the whole first non-blank line; `{{ layout "layouts/base.html" }}`
is accepted too. The child renders first; the layout outputs it with `{{ content }}`.
Layouts can chain up to 10 levels.

```html
{{ base = site.base_url }}{{ if base == "/" }}{{ base = "" }}{{ end }}<!DOCTYPE html>
<html lang="{{ site.language | html.escape }}">
<head>
  <meta charset="utf-8">
  <title>{{ page.title | html.escape }}</title>
  <link rel="stylesheet" href="{{ base }}/assets/css/main.css">
</head>
<body>
  {{ include "partials/header.html" }}
  {{ content }}
</body>
</html>
```

`include` partials see the caller's variables. A missing include renders an empty
string silently; `bukit doctor` reports it. In SEO `inject` mode Core rewrites the
`<title>` and adds meta tags, so a simple `<title>` is enough.

## Model

Keys are snake_case. User data keys (`page.fields`, `site.data`, `site.params.<key>`) keep
the author's spelling and are case-sensitive. Missing members return null (no error).
Treat `site`, `pages`, and `items` as read-only: copy a value into a local variable
before changing it, because writing to the shared model can fail the render.

| Root | Available on |
|---|---|
| `site` | all templates |
| `page`, `pages` | all page templates (`pages` = routed pages; on taxonomy term pages `pages` is the term's entries, same as `items`) |
| `items` | home, collection, filtered, taxonomy, pagination lists |
| `pagination` | paginated lists (absent otherwise; test with `if pagination`) |
| `collection` | collection and filtered lists (`collection` with its `key`); also set on taxonomy term pages with key `page`, so test `taxonomy` first |
| `filter` | filtered lists (`field`, `operator`, `value`, `values`) |
| `taxonomy` | taxonomy term pages (`kind`, `term`, `slug`, `url`, `count`, ...) |
| `seo` | same as `page.seo` |
| `content` | inside a layout |

`site`: `name`, `title`, `description`, `url`, `base_url` (`"/"` or `"/sub"`, no
trailing slash; includes `/<lang>` for prefixed languages), `language` (the current
language, from `site.languages`: the variant language, or the only entry), `build_year`,
`params` (from `theme.params`), `data.<source>`, `data_index.<source>.<scope>.<key>`,
`data_files.<file>`, `modules.<type>`, `related_pages[<id>]`. `site.data` also holds
the built-in keys `taxonomy` (all terms, usable on any page) and `pages_by_id`. There
is no `site.menus` or `site.languages`.

`page`: `title`, `url` (root-relative, without `base_url`), `content` (HTML),
`summary`, `table_of_contents` (`level`, `text`, `id`, `url`), `publish_date`,
`updated_at`, `fields.<key>.value`, `content_record`, `seo`. There is no `page.date`
or `page.tags`; use `page.publish_date` and `page.fields.tags.value`.

`page.seo`: `title`, `document_title`, `description`, `canonical`, `robots`, `og`,
`twitter`, `article`, `alternates` (`hreflang`, `href`), `json_ld`.

`pagination`: `page`, `page_size`, `total_pages`, `total_items`, `has_prev`,
`has_next`, `prev_url`, `next_url` (root-relative).

List items on the home and collection lists have no `content` unless the template
needs it (see capabilities). Taxonomy term items are `{id, title, url, publish_date,
summary, fields}`; the taxonomy index lists terms in `page.fields.terms.value`.

### Related pages

Enable `site.related` (default off) to score pages by shared values:

```yaml
site:
  related:
    enabled: true
    threshold: 80        # minimum score (default)
    limit: 5             # default
    indices:             # default tags 80, categories 60
      - { name: tags, weight: 80 }
      - { name: date, weight: 20 }   # tags|categories|keywords|collection|type|date
```

There is no `page.related`. Results are a map keyed by document id:

```scriban
{{ for r in site.related_pages[page.content_record.id] }}
<a href="{{ r.url }}">{{ r.title }}</a>
{{ end }}
```

Each entry has `title`, `url`, `score`. Pages with no match have no entry.

## Escaping and URLs

There is no automatic HTML escaping. Escape text with `| html.escape`;
`page.content` is trusted HTML. Build links from `site.base_url` plus `page.url`:

```scriban
{{ base = site.base_url }}{{ if base == "/" }}{{ base = "" }}{{ end }}
{{ for item in items }}
  <li><a href="{{ base }}{{ item.url }}">{{ item.title | html.escape }}</a>
  {{ if item.publish_date }}<time>{{ item.publish_date | date.to_string "%Y-%m-%d" }}</time>{{ end }}</li>
{{ end }}
{{ if pagination && pagination.has_next }}<a href="{{ base }}{{ pagination.next_url }}">Next</a>{{ end }}
{{ if page.fields.tags }}{{ for tag in page.fields.tags.value }}
  <a href="{{ base }}/tags/{{ tag | util.term_slug }}/">{{ tag | html.escape }}</a>
{{ end }}{{ end }}
```

## Bukit functions

Besides Scriban built-ins (the `string`, `array`, `date`, `math`, and `html` modules):

| Function | Notes |
|---|---|
| `util.format_date v "yyyy-MM-dd"` | .NET format strings (use `date.to_string "%Y-%m-%d"` for strftime). |
| `util.truncate v 100`, `util.titleize v`, `util.slugify v` | |
| `util.term_slug` | Same slug as taxonomy URLs. |
| `util.json_string` | JSON string with quotes; does not escape `</script>`. |
| `comp.render "name" ...` | Theme components (`bukit-theme`). |
| `render_section {type: "hero", ...}` | Theme sections. |
| `shortcode "name" "a1" ...` | Theme shortcode, up to four arguments; only when `theme.shortcodes` is set (`bukit-theme`). |
| `image` functions `srcset` and `img` | Produce `?w=N` URLs that no build step serves; prefer plain `<img>` plus image processing. |

There is no translation helper, `markdownify`, `relative_url`, or asset fingerprinting.

## Capabilities manifest

Optional `<theme layouts>/bukit.templates.yaml`:

```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true   # list items receive page.content
```

Paths must exist inside the layouts directory and each entry needs at least one
capability. `build.listPageContentMode` (`always|never|auto`) overrides it. Template
changes are picked up by the next build; do not delete `.cache` for that.

## Limits and errors

- Scriban's loop limit (1000 iterations) applies (`Exceeding number of iteration limit
  1000`). On large sites
  use `limit:` (`for p in pages limit:10`) or pagination.
- `Template not found` (BKT-0301), parse error (0302), layout depth (0303), component
  failure (0304), `Render failed: <path> at <span>: <cause>` (0399, exit 3).
- `bukit doctor` checks required templates, parse errors, includes, and the manifest.
