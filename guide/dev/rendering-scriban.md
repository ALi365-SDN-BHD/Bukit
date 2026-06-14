# Rendering With Scriban

Bukit renders content, list pages, and static HTML through the Scriban runtime.

Source anchors:

- `src/Bukit.Rendering/Scriban/`
- `src/Bukit.Engine/PageRenderDispatcher.cs`
- `src/Bukit.Engine/ScribanTemplateLinter.cs`
- `src/Bukit.Engine/TemplateCapabilitiesResolver.cs`

## Render Entry Kinds

| Kind | Source | Render path |
|---|---|---|
| `Page` | Routed content documents | page template |
| `List` | homepage, collection lists, taxonomy, pagination, archive | list or derived template |
| `Static` | `.html` files in `theme.static` when `theme.staticTemplate` is configured | static template path |

All three share route security, incremental skip logic, SEO injection, and
write safety.

## Template Variables

| Variable | Meaning |
|---|---|
| `site` | Site metadata, base URL, params, modules, menus, data |
| `page` | Current page or list model |
| `pages` | Page collections available to the renderer |
| `data` | Data-mode source output and generated data |
| `content` | Layout body content |

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
  {{ content }}
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

## List Template

```html
{% layout "layouts/base.html" %}
<main>
  <h1>{{ page.title }}</h1>
  {{ for item in page.items }}
    <article>
      <h2><a href="{{ item.url }}">{{ item.title }}</a></h2>
      {{ if item.summary }}<p>{{ item.summary }}</p>{{ end }}
    </article>
  {{ end }}
</main>
```

## Linting and Diagnostics

`bukit doctor` parses templates, checks includes/layouts, validates capability
metadata, and reports suspicious template variables. Use it before changing
renderer behavior.

## Safety Rules

- Keep path resolution inside the theme root.
- Escape user-facing fields unless the field is intentionally sanitized HTML.
- Keep SEO ownership aligned with `site.seo.renderMode`.
- Add `theme.yaml.templates` metadata when a template has a runtime role.

