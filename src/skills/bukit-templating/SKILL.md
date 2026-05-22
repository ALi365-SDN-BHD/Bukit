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
| `page.content` | string | Rendered HTML body (note: `needs_page_content: true` in `bukit.templates.yaml` controls whether content is loaded for list pages) |
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

## Shortcodes

Shortcodes allow reusable HTML snippets in both Markdown content and Scriban templates.

### Configuration (site.yaml)

```yaml
theme:
  shortcodes:
    youtube: '<div class="video"><iframe src="https://www.youtube.com/embed/{{ $1 }}"></iframe></div>'
    callout: '<div class="callout callout-{{ $1 }}">{{ $2 }}</div>'
```

Parameters are positional, referenced as `{{ $1 }}`, `{{ $2 }}`, etc.

### Usage in Markdown

```
{% youtube "dQw4w9WgXcQ" %}
{% callout "warning" "This is important!" %}
```

Shortcodes are processed during rendering and work even with HTML-encoded content from the Markdown pipeline.

### Usage in Scriban

```
{{ shortcode "youtube" "dQw4w9WgXcQ" }}
```

---

## Components

Components are declared in site.yaml with typed props and used in Scriban templates via `{{ comp.render "Name" args }}`.

### Configuration (site.yaml)

```yaml
theme:
  components:
    PostCard:
      template: "partials/post-card.html"
      props:
        title: ""
        url: ""
```

### Component Template (partials/post-card.html)

```html
<div class="post-card">
  <h3>{{ title }}</h3>
  <a href="{{ url }}">Read more</a>
</div>
```

### Usage in Scriban

```
{{ for p in pages }}
{{ comp.render "PostCard" p.title p.url }}
{{ end }}
```

Components inherit the parent template's global variables (`page`, `site`, etc.) and receive their own props as local variables bound by name.

---

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

---

## Schema-Driven Template Generation

When a user has defined `collection.schema` in site.yaml, use these patterns to generate precise templates.

### Schema Field → Template Pattern Map

| Schema Type | Template Pattern |
|---|---|
| `string` | `{{ page.fields.KEY.value }}` |
| `boolean` | `{{ if page.fields.KEY.value }}...{{ end }}` |
| `date` | `{{ page.fields.KEY.value | date.to_string "%Y-%m-%d" }}` |
| `number` | `{{ page.fields.KEY.value }}` |
| `array` | `{{ for item in page.fields.KEY.value }}...{{ end }}` |
| `object` | `{{ page.fields.KEY.value.SUBKEY }}` |
| `image` (url) | `<img src="{{ page.fields.KEY.value }}" alt="{{ page.title }}" class="field-cover">` |
| `select` | `{{ page.fields.KEY.value }}` |
| `multi_select` | `{{ for tag in page.fields.KEY.value }}<span class="tag">{{ tag }}</span>{{ end }}` |
| `email` | `<a href="mailto:{{ page.fields.KEY.value }}">{{ page.fields.KEY.value }}</a>` |

### Auto-Generated Post Template (Schema-Aware)

For schema `[title, date, tags, cover, author, summary]`:
```html
{% layout "layouts/base.html" %}

<article class="article">
  <header class="article-header">
    {{ if page.fields.cover.value }}
      <img class="article-cover" src="{{ page.fields.cover.value }}" alt="{{ page.title }}">
    {{ end }}
    <h1>{{ page.title }}</h1>
    <div class="article-meta">
      {{ if page.fields.author.value }}<span class="meta-author">{{ page.fields.author.value }}</span>{{ end }}
      {{ if page.publish_date }}<time>{{ page.publish_date | date.to_string "%Y-%m-%d" }}</time>{{ end }}
    </div>
    {{ if page.summary }}<p class="article-summary">{{ page.summary }}</p>{{ end }}
    {{ if page.fields.tags.value }}
      <div class="article-tags">
        {{ for tag in page.fields.tags.value }}
          <a class="tag" href="{{ site.base_url }}/tags/{{ tag | string.downcase }}/">{{ tag }}</a>
        {{ end }}
      </div>
    {{ end }}
  </header>
  <div class="content">{{ page.content }}</div>
</article>
```

### Auto-Generated List Card Partial
```html
<li class="card">
  {{ if item.fields.cover.value }}
    <img class="card-image" src="{{ item.fields.cover.value }}" alt="{{ item.title }}" loading="lazy">
  {{ end }}
  <div class="card-content">
    <h2 class="card-title"><a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a></h2>
    <div class="card-meta">
      {{ if item.publish_date }}<time>{{ item.publish_date | date.to_string "%Y-%m-%d" }}</time>{{ end }}
      {{ if item.fields.author.value }}<span>· {{ item.fields.author.value }}</span>{{ end }}
    </div>
    {{ if item.summary }}<p class="card-summary">{{ item.summary }}</p>{{ end }}
  </div>
</li>
```

## Advanced Component Composition Patterns

### Pattern 1: Slot-Based Partial
```html
{{ with title = "Featured" }}
  {{ body = "" }}<ul>...</ul>{{ end }}
  {{ include "partials/section.html" }}
{{ end }}
```

### Pattern 2: Conditional Layout Switching
```html
{{ if page.fields.layout_type.value == "landing" }}
  {{ include "layouts/base-landing.html" }}{{ content }}
{{ else }}
  {{ include "partials/header.html" }}<main>{{ content }}</main>{{ include "partials/footer.html" }}
{{ end }}
```

### Pattern 3: Breadcrumb Navigation
```html
<nav class="breadcrumb" aria-label="Breadcrumb">
  <ol>
    <li><a href="{{ site.base_url }}/">Home</a></li>
    {{ for seg in page.url | string.slice 1 | string.split "/" }}
      {{ if seg != "" }}<li><a href="{{ site.base_url }}/{{ seg }}/">{{ seg }}</a></li>{{ end }}
    {{ end }}
    <li aria-current="page">{{ page.title }}</li>
  </ol>
</nav>
```

### Pattern 4: Reading Time Estimate
```html
{{ word_count = page.content | string.split " " | array.size }}
{{ reading_time = word_count / 200 | math.ceil }}
{{ if reading_time < 1 }}{{ reading_time = 1 }}{{ end }}
<span class="reading-time">{{ reading_time }} min read</span>
```

### Pattern 5: Empty State
```html
{{ if pages.size == 0 }}
  <div class="empty-state"><p>No content yet.</p></div>
{{ else }}
  <ul class="card-list">{{ for p in pages }}...{{ end }}</ul>
{{ end }}
```

## SEO Best Practice Templates

### JSON-LD Structured Data

**Article:**
```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "{{ page.title | html.escape }}",
  {{ if page.fields.cover.value }}"image": "{{ page.fields.cover.value | html.escape }}",{{ end }}
  {{ if page.publish_date }}"datePublished": "{{ page.publish_date | date.to_string "%Y-%m-%d" }}",{{ end }}
  {{ if page.fields.author.value }}"author": { "@type": "Person", "name": "{{ page.fields.author.value | html.escape }}" },{{ end }}
  "description": "{{ page.summary | html.escape }}"
}
</script>
```

**BreadcrumbList:**
```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [
    { "@type": "ListItem", "position": 1, "name": "Home", "item": "{{ site.url }}{{ site.base_url }}/" }
  ]
}
</script>
```

### Sitemap & RSS Links
Always include in base.html `<head>`:
```html
<link rel="alternate" type="application/rss+xml" href="{{ site.base_url }}/rss.xml" />
<link rel="sitemap" type="application/xml" href="{{ site.base_url }}/sitemap.xml" />
```

## Performance Optimization

### Image Lazy Loading
```html
<img src="{{ item.fields.cover.value }}" alt="{{ item.title }}" loading="lazy" decoding="async">
```

### Conditional Script Loading
```html
{{ if page.fields.has_search.value }}
  <script src="{{ site.base_url }}/assets/search.js" defer></script>
{{ end }}
```
