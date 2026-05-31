---
name: bukit-templating
description: Use when using bukit to write or modify Scriban templates, encountering bukit template rendering errors, needing to access page/site/data in bukit templates, using layout inheritance in bukit, or working with bukit list pages, pagination, or multi-language conditional rendering

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Engine/Plugins/BuiltIn/PagesIndexPlugin.cs"
source_anchors:
  - "src/Bukit.Engine/Plugins/BuiltIn/PagesIndexPlugin.cs"
guide_chapters:
  - "guide/user/08-themes-templates.md"
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
| `site.menus` | object | Menu definitions from `site.menus` config (e.g., `site.menus.main`, `site.menus.footer`). Each menu is a list of `{identifier, name, url, children}` |
| `site.related_pages` | object | Related content data from RelatedContentPlugin. Dictionary keyed by content item ID → list of `{title, url, score}` |
| `site.data_files` | object | Data files loaded from `data/` directory by DataFilesPlugin. Supports nested keys for namespaces and multi-language subdirectories. |

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
```

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

### Taxonomy Pages

Taxonomy index pages (e.g., `/tags/`, `/categories/`) and term pages (e.g., `/tags/python/`) receive dedicated variables:

#### Term Page Variables (`page.fields`)

| Variable | Type | Description |
|------|------|------|
| `page.fields.items.value` | list | Articles in this term (current page) |
| `page.fields.taxonomy.value` | object | Term metadata (see below) |
| `page.fields.pagination.value` | object | Pagination info |

Each item in `items.value`:
```scriban
{{ for item in page.fields.items.value }}
  {{ item.title }}        # Article title
  {{ item.url }}           # Article URL
  {{ item.publish_date }}  # Publish date
  {{ item.summary }}       # Article summary (if available)
{{ end }}
```

#### taxonomy.value Fields

| Field | Type | Description |
|------|------|------|
| `kind` | string | Kind name (e.g., "tags", "categories") |
| `term` | string | Term display name |
| `slug` | string | Term URL slug |
| `count` | int | Total article count |
| `description` | string? | Term description (from data source) |
| `image` | string? | Term cover image (from data source) |
| `weight` | int? | Sort weight (only present if non-zero) |
| `parent` | string? | Parent term slug |
| `children` | string[]? | Child term slugs (hierarchical=true only) |
| `ancestors` | string[]? | Ancestor chain (hierarchical=true only) |
| `aliases` | string[]? | Term aliases |

#### pagination.value Fields

| Field | Type | Description |
|------|------|------|
| `page` | int | Current page number |
| `page_size` | int | Items per page |
| `total` | int | Total item count |
| `total_pages` | int | Total pages |
| `has_prev` | bool | Has previous page |
| `has_next` | bool | Has next page |

#### Index Page Variables (`page.fields`)

| Variable | Type | Description |
|------|------|------|
| `page.fields.terms.value` | list | All visible terms sorted by weight |

Each term in `terms.value[]`:
```scriban
{{ for term in page.fields.terms.value }}
  {{ term.title }}        # Display name
  {{ term.slug }}         # URL slug
  {{ term.url }}          # Term page URL
  {{ term.count }}        # Article count
  {{ term.description }}  # Optional description
  {{ term.image }}        # Optional cover image
  {{ term.weight }}       # Optional sort weight
  {{ term.children }}     # Optional child slugs (hierarchical)
  {{ term.ancestors }}    # Optional ancestor chain (hierarchical)
  {{ term.aliases }}      # Optional aliases
{{ end }}
```

#### Complete Term Page Template Example

```scriban
{% layout "layouts/base.html" %}

{{ items = page.fields.items.value }}
{{ tax = page.fields.taxonomy.value }}
{{ pager = page.fields.pagination.value }}

<header>
  <h1>{{ page.title }}</h1>
  {{ if tax.description }}
    <p class="description">{{ tax.description }}</p>
  {{ end }}
  {{ if tax.ancestors }}
    <nav class="breadcrumb">
    {{ for ancestor in tax.ancestors }}
      / <a href="{{ site.base_url }}/{{ tax.kind }}/{{ ancestor }}/">{{ ancestor }}</a>
    {{ end }}
    </nav>
  {{ end }}
</header>

{{ for item in items }}
  <article>
    <h2><a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a></h2>
    {{ if item.summary }}<p>{{ item.summary }}</p>{{ end }}
  </article>
{{ end }}

<nav class="pagination">
  {{ if pager.has_prev }}
    <a href="{{ site.base_url }}/{{ tax.kind }}/{{ tax.slug }}/page/{{ pager.page - 1 }}/">Prev</a>
  {{ end }}
  <span>Page {{ pager.page }} of {{ pager.total_pages }}</span>
  {{ if pager.has_next }}
    <a href="{{ site.base_url }}/{{ tax.kind }}/{{ tax.slug }}/page/{{ pager.page + 1 }}/">Next</a>
  {{ end }}
</nav>
```

#### Feed/Redirect Output

- **RSS feeds**: `/<kind>/<slug>/feed.xml` generated automatically for each term with articles
- **Alias redirects**: `/<kind>/<alias_slug>/index.html` → `/<kind>/<slug>/` (HTML meta refresh)

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
| `${variable}` appears empty silently | `EnableRelaxedMemberAccess` causes nonexistent properties to return null | Run `bukit doctor` to detect unknown template variables |

---

## Template Variable Spell Check (`bukit doctor`)

Bukit's Scriban engine has `EnableRelaxedMemberAccess` enabled — accessing a nonexistent property on `page`/`site`/`pages` silently returns `null` instead of throwing an error. This means typos like `{{ page.titel }}` render as empty strings without any build error.

### How `bukit doctor` Detects Typos

The `doctor` command now includes a **template variable spell check** section (`--- Template variable spell check ---`) that:

1. Parses every `.html` template in `layouts/` using Scriban's AST
2. Extracts all variable references (e.g., `page.title`, `site.seo.og.image`, `page.fields.tags`)
3. Cross-references against a **known field whitelist** for `page`, `site`, `pages`, and loop variables (`p`, `item`)
4. Reports unknown variables with file location and message

### Known Fields Whitelist

**`site` root fields:** `name`, `title`, `url`, `description`, `base_url`, `language`, `params`, `modules`, `data`, `menus`, `related_pages`, `data_files`, `seo` → `og`/`twitter`/`article`, `analytics` → `enabled`/`google_analytics_id`

**`page` root fields:** `title`, `url`, `content`, `summary`, `publish_date`, `fields`, `seo` → `title`/`desc`/`canonical`/`og` → `title`/`description`/`image`/`url`/`type`/`site_name`/`locale`, `twitter` → `card`/`title`/`description`/`image`, `article` → `published_time`/`modified_time`/`author`/`tag`, `robots`/`schema_type`/`alternates`/`json_ld`, `table_of_contents`

**`pages`/`p`/`item` loop variables:** `title`, `url`, `content`, `summary`, `publish_date`, `fields`

### Example Output

```
--- Template variable spell check ---
⚠ pages/index.html: Unknown variable 'site.settings.theme' — did you mean 'site.params'?
⚠ pages/post.html: Unknown variable 'page.auther' — did you mean 'page.fields.author.value'?
⚠ partials/card.html: Unknown variable 'p.exerpt' — did you mean 'p.summary'?

(2 warning(s) in 2 file(s))
✔ No unknown template variables detected
```

### Whitelist for Custom Fields

Access to `page.fields.KEY` and `p.fields.KEY` is always allowed since `fields` contains user-defined metadata. Similarly, `site.params.KEY`, `site.data.KEY`, and `site.modules.KEY` accept any sub-key.

### Adding Custom Whitelist Entries

If you add custom Scriban globals or extend the data model via plugins, the linter's whitelist is defined at build time in `ScribanModelKnownFields`. Plugin developers should contribute known fields to this registry.

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

### Sitemap & Feed Links
Always include in base.html `<head>`:
```html
<link rel="alternate" type="application/rss+xml" href="{{ site.base_url }}/rss.xml" />
<link rel="alternate" type="application/atom+xml" href="{{ site.base_url }}/feed/atom.xml" />
<link rel="alternate" type="application/json" href="{{ site.base_url }}/feed/feed.json" />
<link rel="sitemap" type="application/xml" href="{{ site.base_url }}/sitemap.xml" />
```

Feed format availability depends on `site.feed.formats` config (rss, atom, json).

## Built-in Search UI

When `site.search.ui: "default"` is configured, the SearchIndexPlugin generates a `bukit-search.html` partial. Include it in templates:

```html
{{ include "bukit-search.html" }}
```

The search UI is a zero-dependency (~5KB) JavaScript component with:
- Input debounce search
- Title + content weighted scoring (with `searchWeight` front matter support)
- Keyboard navigation (↑ ↓ Enter Escape)
- Highlighted search results
- Light/dark theme support via `site.search.uiTheme`

```yaml
site:
  search:
    ui: "default"
    uiTheme: "dark"
    placeholderText: "Search..."
```

## Menu Template Pattern

Access menus configured in `site.menus`:

```html
<nav>
  <ul>
    {{ for item in site.menus.main }}
      <li>
        <a href="{{ item.url }}">{{ item.name }}</a>
        {{ if item.children }}
          <ul>
            {{ for child in item.children }}
              <li><a href="{{ child.url }}">{{ child.name }}</a></li>
            {{ end }}
          </ul>
        {{ end }}
      </li>
    {{ end }}
  </ul>
</nav>
```

## Related Content Pattern

Access related content from RelatedContentPlugin:

```html
{{ page_id = page.title }}
{{ related = site.related_pages[page_id] }}
{{ if related }}
  <section class="related-posts">
    <h2>Related Articles</h2>
    <ul>
      {{ for r in related }}
        <li><a href="{{ r.url }}">{{ r.title }}</a></li>
      {{ end }}
    </ul>
  </section>
{{ end }}
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

---

## Image Helper Functions

Bukit provides Scriban template functions for responsive images.

### `image.img` — Complete Responsive `<img>` Tag

```
{{ image.img "/images/photo.jpg" "Alt text" "480,768,1200" "cover" }}
```

Parameters: `(src, alt, sizes, className)`

Generates:
```html
<img src="/images/photo.jpg"
     srcset="/images/photo.jpg?w=480 480w, /images/photo.jpg?w=768 768w, /images/photo.jpg?w=1200 1200w"
     sizes="(max-width: 480px) 480px, (max-width: 768px) 768px, 1200px"
     alt="Alt text" class="cover"
     loading="lazy" decoding="async" />
```

### `image.srcset` — Generate `srcset` Attribute Only

```
{{ image.srcset "/images/photo.jpg" "480,768,1200" }}
```

Use when you need custom control over the `<img>` tag:
```html
<img src="/images/photo.jpg"
     srcset="{{ image.srcset "/images/photo.jpg" "480,768,1200" }}"
     sizes="(min-width: 1200px) 1200px, 100vw"
     alt="Custom" />
```

## Template Capabilities — Field Declarations

The `bukit.templates.yaml` manifest now supports optional field declarations for each template. This allows AI Skills and tooling to generate precise templates from content schema.

### Extended Manifest Format

```yaml
templates:
  pages/post.html:
    capabilities:
      needs_page_content: true
    fields:
      - key: cover
        type: string
        label: Cover Image
        suggestion: '<img src="{{ page.fields.cover.value }}" alt="{{ page.title }}" class="article-cover" loading="lazy" />'
      - key: tags
        type: array
        label: Tags
        suggestion: '{{ for tag in page.fields.tags.value }}<a class="tag" href="{{ site.base_url }}/tags/{{ tag | string.downcase }}/">{{ tag }}</a>{{ end }}'
      - key: author
        type: string
        label: Author
        suggestion: '<span class="meta-author">{{ page.fields.author.value }}</span>'
```

### Field Declaration Properties

| Property | Required | Description |
|---|---|---|
| `key` | Yes | Field name matching schema definition |
| `type` | No | Field type hint (string, date, array, boolean, etc.) |
| `label` | No | Human-readable label |
| `suggestion` | No | Suggested Scriban code snippet for rendering this field |

### How `suggestion` Works in Practice

When a user asks "generate a post template for my blog schema," the AI Skill (bukit-content-to-template) reads the field declarations from `bukit.templates.yaml` and uses the `suggestion` snippets as building blocks to compose the complete template. If no `suggestion` is provided, the AI falls back to type-based defaults from the Schema Field → Template Pattern Map.

## Template Security Best Practices (P1-1/P1-2)

### Shortcode HTML Encoding

All shortcode parameter values are HTML-encoded via `WebUtility.HtmlEncode` before template substitution. When defining custom shortcodes in `theme.shortcodes`, always use `html.escape` filter for parameter values:

```yaml
theme:
  shortcodes:
    card: '<div class="card">{{ $1 | html.escape }}</div>'
```

Without encoding, a content author could inject scripts via `{% card "<script>alert(1)</script>" %}` — the engine now prevents this at the ShortcodeProcessor level.

### Notion Block Renderer Color Safety

All Notion block renderers (CalloutBlockRenderer, ToDoBlockRenderer, ToggleBlockRenderer, BookmarkBlockRenderer, EquationBlockRenderer) now HTML-encode color property values when constructing CSS class attributes. This prevents class attribute injection through Notion color fields.

### Image Tag Safety

Image tags generated via `image.img` and `image.srcset` helpers use `WebUtility.HtmlEncode` on all attribute values combined with an `IsSafeImageSource` protocol whitelist (http/https/data only).

### Scriban Output Encoding

For user-supplied content in templates, always use Scriban's built-in filters:
- `{{ value | html.escape }}` — HTML attribute/value context
- `{{ value | object.format "json" }}` — JSON/script context
- `{{ site.analytics.google_analytics_id | html.escape }}` — IDs in script contexts
