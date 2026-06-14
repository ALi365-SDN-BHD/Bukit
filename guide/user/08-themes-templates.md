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

## Verify

```bash
bukit doctor
bukit build
```

`doctor` checks theme manifest fields, template parse errors, layout chains, includes, missing templates, and template capability warnings.
