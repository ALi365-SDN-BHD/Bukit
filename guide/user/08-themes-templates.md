# Themes and Templates

Bukit Core 1.0 theme work is filesystem-based. There is no Core theme command surface.

## Directory Contract

```text
themes/<name>/
  theme.yaml
  layouts/
    layouts/base.html
    pages/index.html
    pages/page.html
    pages/post.html
    pages/list.html
    partials/header.html
    partials/footer.html
  assets/
    style.css
  static/
```

Point `site.yaml` at the theme:

```yaml
theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
  params:
    accent: "#0b5fff"
```

## theme.yaml

```yaml
name: starter
version: 1.0.0
engine: bukit
display_name: Starter
description: Minimal Core theme
capabilities:
  seo: true
  geo: true
  i18n: true
templates:
  home:
    template: pages/index.html
    required: true
  page:
    template: pages/page.html
    accepts:
      collection: page
  post:
    template: pages/post.html
    accepts:
      collection: post
assets:
  css:
    - assets/style.css
```

Theme inheritance, when used, belongs in `theme.yaml`. Site-level inheritance fields are not Core 1.0.

## Base Layout

```html
<!doctype html>
<html lang="{{ page.language | default site.language }}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{ page.title }} - {{ site.title }}</title>
  <link rel="stylesheet" href="{{ site.base_url }}/assets/style.css">
</head>
<body>
  {{ include "partials/header.html" }}
  {{ content }}
  {{ include "partials/footer.html" }}
</body>
</html>
```

## Page Template

```html
{% layout "layouts/base.html" %}
<main>
  <h1>{{ page.title }}</h1>
  <article>{{ page.content }}</article>
</main>
```

## List Template Model

List templates for collection lists, paginated list pages, taxonomy pages, and
filtered lists should use the stable list model:

| Field | Description |
|---|---|
| `site` | Site metadata and theme params |
| `page` | Current list page metadata, URL, summary, and SEO |
| `items` | Current page of list items |
| `pagination` | `page`, `page_size`, `total_items`, `total_pages`, `has_prev`, `has_next`, `prev_url`, `next_url` |
| `collection` | Collection context; its key member names the collection |
| `taxonomy` | Taxonomy context when rendering taxonomy pages |
| `filter` | Filter context when rendering `filteredLists` pages |
| `seo` | Same SEO model as `page.seo` |

```html
{% layout "layouts/base.html" %}
<main>
  <h1>{{ page.title }}</h1>

  {{ for item in items }}
    <article>
      <h2><a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a></h2>
      {{ if item.summary }}<p>{{ item.summary }}</p>{{ end }}
    </article>
  {{ end }}

  {{ if pagination && pagination.total_pages > 1 }}
    <nav>
      {{ if pagination.has_prev }}<a href="{{ pagination.prev_url }}">Previous</a>{{ end }}
      <span>{{ pagination.page }} / {{ pagination.total_pages }}</span>
      {{ if pagination.has_next }}<a href="{{ pagination.next_url }}">Next</a>{{ end }}
    </nav>
  {{ end }}
</main>
```

`pages` is still available as a backwards-compatible alias for the current list
items. Existing `page.fields.items.value`, `page.fields.pagination.value`,
`page.fields.taxonomy.value`, and `page.fields.filter.value` access patterns
continue to work for older themes, but new templates should prefer the stable
top-level fields above.

If you are replacing JavaScript pagination or browser-side filters, see
[18 Static List Routes Migration](./18-static-list-routes-migration.md) for the
full template migration path.

## Verify

```bash
bukit doctor
bukit build
```

`doctor` checks theme manifest fields, template parse errors, layout chains, includes, missing templates, and template capability warnings.
