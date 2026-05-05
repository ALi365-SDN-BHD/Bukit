# Theme Development (Themes) and Parameter Usage

A theme is templates + assets + static files. Uses Scriban template engine.

Related: [Rendering (Scriban)](./rendering-scriban.md), [Modules Data](./modules-data.md)

Example themes: `examples/starter/themes/alt/`, `examples/starter/themes/seo-best-practice/`

## Directory Structure

```text
themes/<name>/
  layouts/        # Scriban template root
  assets/         # Copied to output /assets/
  static/         # Copied as-is to output root
```

Within layouts: `layouts/` (base.html), `pages/` (index/list/post/page), `partials/` (header/footer/seo)

## Theme Resolution Rules

When `theme.name` is non-empty and `theme.layouts/assets/static` are at defaults:
- layoutsDir = `themes/<name>/layouts`
- assetsDir = `themes/<name>/assets`
- staticDir = `themes/<name>/static`

## Theme Commands

```bash
bukit theme list --config site.yaml
bukit theme use alt --config site.yaml
```

## Required Templates

`pages/index.html`, `pages/list.html`, `pages/post.html`, `pages/page.html`

Themes can declare capabilities via `layouts/bukit.templates.yaml`:

```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true
      supports_pagination: true
```

## Layout and Include

```scriban
{{ layout "layouts/base.html" }}
<h1>{{ page.title }}</h1>
{{ page.content }}
```

In base.html: `{{ content }}` as placeholder. Include: `{{ include "partials/header.html" }}`

## Theme Parameters (theme.params → site.params)

```yaml
theme:
  params:
    brand: ALT THEME
```

Template: `{{ site.params.brand }}`

## Static Assets and base_url

```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
```

When baseUrl is `/`, `site.base_url` is injected as empty string.

## Themes and Modules

```scriban
{{ if site.modules && site.modules.navigation }}
  {{ for item in site.modules.navigation }}
    <a href="{{ item.fields.link.value }}">{{ item.title }}</a>
  {{ end }}
{{ end }}
```
