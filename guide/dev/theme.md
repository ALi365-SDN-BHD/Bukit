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

## Theme Security

### Path Boundary Enforcement (P2-6)

Since v3.x, all theme path resolution (layouts/assets/static) is validated via `BuildPathUtils.MakeAbsolute(rootDir, path, enforceWithinRoot: true)`. When a resolved theme path escapes the project root (e.g., via `../` traversal or absolute paths), the engine throws `ConfigException` with diagnostic code `BKT-0004` (ConfigPathTraversal) and refuses to continue.

### Theme Name Sanitizer (P2-7)

When a theme declares `extends: <parent-theme>` or `name: <theme-name>`, the `ThemeNameSanitizer` applies **7 layers of sanitization** before any `Path.Combine`:

1. Null/empty check
2. Absolute path rejection
3. `..` traversal rejection
4. Path separator character rejection (`/`, `\`)
5. Control character rejection (U+0000–U+001F, U+007F–U+009F)
6. Windows device name rejection (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
7. Invalid filename character rejection (`<`, `>`, `:`, `"`, `|`, `?`, `*`)

For `extends`: if sanitization fails, the engine logs a warning and **skips** loading the parent theme (graceful degradation). For `theme.name`: if sanitization fails, the engine throws `ConfigException`. Implementation: `src/Bukit.Engine/ThemeNameSanitizer.cs`.

### Shortcode HTML Encoding (P1-1)

Shortcode parameter values are HTML-encoded before template substitution. This prevents stored XSS when content authors use shortcodes with scripts or HTML in parameter values. The encoding uses `WebUtility.HtmlEncode` (matching ASP.NET conventions). When defining custom shortcodes, always output parameter values through `{{ $n | html.escape }}` in your Scriban templates.

### Block Renderer Color Safety (P1-2)

All Notion block renderers (Callout, ToDo, Toggle, Bookmark, Equation) use HTML-encoded color values when constructing CSS class attributes. This prevents HTML injection through Notion color property values.
