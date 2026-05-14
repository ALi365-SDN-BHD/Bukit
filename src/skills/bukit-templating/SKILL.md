---
name: bukit-templating
description: Use when using bukit to write or modify Scriban templates, encountering bukit template rendering errors, needing to access page/site/data in bukit templates, using layout inheritance in bukit, or working with bukit list pages, pagination, or multi-language conditional rendering
---

# Bukit Scriban Template Development

## Overview

Bukit uses the [Scriban](https://github.com/scriban/scriban) template engine, supporting `{% layout "path" %}` inheritance, `{{ include "path" }}` partial templates, and full variable and data access.

**REQUIRED BACKGROUND:** Template files are located under `themes/<name>/layouts/` — directory structure and static asset organization are covered in bukit-theme.
**REQUIRED SUB-SKILL:** Verify template rendering with `bukit build`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "Scriban 模板"、"layout 继承不生效"、"模板渲染报错"、"{{ page.title }}" |
| English | "Scriban template", "layout inheritance not working", "template render error", "bukit template syntax" |
| Bahasa Melayu | "templat Scriban", "pewarisan layout tidak berfungsi", "ralat render templat", "sintaks templat bukit" |

## Data Model

Three main data objects available in templates:

### `site` — Site Global Info

| Variable | Type | Description |
|------|------|------|
| `site.name` | string | Site name |
| `site.title` | string | Site title |
| `site.url` | string/null | Site full URL |
| `site.description` | string/null | Site description; also the SEO fallback for generated home/list/taxonomy/pagination pages |
| `site.base_url` | string | Root path. Empty string when `/`, otherwise `/subpath/` |
| `site.language` | string | Current language |
| `site.params` | object | Mapping of `theme.params` |
| `site.modules` | object | Data modules (content with `mode: data`) |
| `site.data` | object | Data built from `sources[].mode: data` or data module builder |

### `page` — Current Page Info

| Variable | Type | Description |
|------|------|------|
| `page.title` | string | Page title |
| `page.url` | string | Page URL (relative, base_url not included) |
| `page.content` | string | Page HTML content |
| `page.summary` | string/null | Page summary |
| `page.publish_date` | DateTime/null | Publish date |
| `page.fields` | object | Metadata fields, e.g., `page.fields.tags`, `page.fields.author` |

Each field has a `{type: string, value: ...}` structure:
```html
{{ page.fields.tags.value }}              ← Direct value
{{ for tag in page.fields.tags.value }}   ← If it's an array
```

### `pages` — Page List (list pages only)

Only available in index.html and list.html templates. An array of `PageInfo` objects. Each element has `title`, `url`, `content`, `summary`, `publish_date`, `fields`.

## SEO and Head Output

Bukit's default SEO mode is `site.seo.renderMode: inject`. In this mode, templates should provide a normal `<head>` and a `<title>`, but should not include SEO or Analytics partials unless the user intentionally wants theme-owned head output. The engine injects canonical, description, robots, OG/Twitter, hreflang, JSON-LD, and GA4.

Use `partials/seo.html` and `partials/analytics.html` only for `renderMode: theme`. When writing explicit SEO partials, escape all HTML attributes with `| html.escape`; JSON-LD entries from `page.seo.json_ld` are already serialized by the engine.

## Layout Inheritance

Bukit supports a custom `{% layout %}` directive (must be the first non-blank line):

```html
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <div>{{ page.content }}</div>
</article>
```

- `{% layout %}` must be the **first non-blank line**
- `{{ content }}` in the layout template is replaced with the child template's body
- Nested inheritance is supported (child inherits parent layout, parent inherits grandparent layout)
- Path relative to `layouts/` directory
- Supports both single and double quotes: `{% layout 'layouts/base.html' %}`
- `{{ layout "..." }}` syntax has the same effect

### Typical base.html

```html
<!DOCTYPE html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8" />
  <title>{{ page.title }} - {{ site.title }}</title>
  <link href="{{ site.base_url }}/assets/style.css" rel="stylesheet">
</head>
<body>
  {{ include "partials/header.html" }}
  <main>
    {{ content }}         ← Child template content injected here
  </main>
  {{ include "partials/footer.html" }}
</body>
</html>
```

## Common Patterns

### Single Page Template (pages/page.html)

```html
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <div class="content">
    {{ page.content }}
  </div>
</article>
```

### Post Template (pages/post.html)

```html
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  {{ if page.publish_date }}
    <time>{{ page.publish_date | date.to_string "%Y-%m-%d" }}</time>
  {{ end }}
  <div class="content">{{ page.content }}</div>
</article>
```

### Homepage Template (pages/index.html)

```html
{% layout "layouts/base.html" %}

<h1>{{ site.title }}</h1>

{{ for p in pages }}
  <article>
    <h2><a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a></h2>
    {{ if p.publish_date }}
      <small>{{ p.publish_date | date.to_string "%Y-%m-%d" }}</small>
    {{ end }}
    {{ if p.summary }}
      <p>{{ p.summary }}</p>
    {{ end }}
  </article>
{{ end }}
```

The `pages` array is sorted by publish date in descending order.

### List Page Template (pages/list.html)

```html
{% layout "layouts/base.html" %}

<ul>
{{ for p in pages }}
  <li>
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
  </li>
{{ end }}
</ul>
```

### Pagination

When pagination is enabled for taxonomy or list pages, `pages` only contains entries for the current page. Pagination info is passed through page metadata and used as needed in templates.

### Accessing Custom Fields

```html
<!-- Single-value field -->
{{ page.fields.author.value }}

<!-- Multi-select / array -->
{{ for tag in page.fields.tags.value }}
  <span class="tag">{{ tag }}</span>
{{ end }}

<!-- Nested object field -->
{{ page.fields.seo.value.title }}
```

### Conditional Rendering

```html
{{ if page.fields.cover.value }}
  <img src="{{ page.fields.cover.value }}" alt="{{ page.title }}">
{{ else }}
  <img src="{{ site.base_url }}/assets/default-cover.jpg">
{{ end }}

{{ if page.publish_date > date.parse "2024-01-01" }}
  <span class="badge">New</span>
{{ end }}
```

### Include Partial Templates

```html
{{ include "partials/header.html" }}
{{ include "partials/card.html" }}
```

### Multi-Language Conditional Rendering

```html
{{ if site.language == "en" }}
  <a href="/en/about/">About</a>
{{ else }}
  <a href="/zh-CN/about/">About</a>
{{ end }}
```

## Built-in Functions

Bukit reuses Scriban's built-in functions, including:

| Category | Functions |
|------|------|
| Date | `date.now`, `date.parse`, `date.to_string` |
| String | `string.downcase`, `string.upcase`, `string.slice` |
| Array | `array.size`, `array.limit`, `array.offset` |
| Math | `math.round`, `math.ceil`, `math.floor` |
| Type Conversion | `to_string`, `to_int` |

Bukit's Scriban context has `EnableRelaxedMemberAccess`, `EnableRelaxedTargetAccess`, and `EnableNullIndexer` enabled — accessing nonexistent properties returns null rather than throwing errors.

## Template File Layout Convention

```
layouts/
  layouts/      ← Layout templates (base.html, can add more custom layouts)
  pages/        ← Page templates (page.html, post.html, index.html, list.html)
  partials/     ← Partial templates (header.html, footer.html, ...)
```

Template paths in site.yaml collection configs are referenced without the `layouts/` prefix. For example, `template: pages/post.html` resolves to `layouts/pages/post.html`.

## Common Errors

| Symptom | Cause | Fix |
|---------|------|------|
| `Template not found: xxx` | Template path incorrect | Check template and site.collections template paths in site.yaml |
| `Template parse error` | Scriban syntax error | Check `{{` `}}` matching and expression syntax |
| `Render failed` | Variable access error during rendering | Use `{{ if xxx }}{{ end }}` to check variable existence first |
| layout not working | `{% layout %}` is not the first non-blank line | Ensure the first line (excluding blank lines) is `{% layout %}` |
| `page.content` is empty | Content not rendered or body key mismatch | Check content source config |
| `site.data` is empty | Data module not correctly configured | Confirm `sources[].mode: data`, check `bukit doctor` |
| `pages` not available in non-list templates | `pages` is only passed to list/index templates | Use `page` for single page templates |
| Variable output shows HTML escaped | Scriban defaults to escaping | Use `{{ variable | html.raw }}` |
| Chinese characters garbled | Template file encoding issue | Ensure template file is UTF-8 (without BOM) |
| base_url path joins with double slashes | `base_url` ends with `/` causing `//` in URLs | `site.base_url` is empty string when `/`, use `{{ site.base_url }}/xxx` directly |
