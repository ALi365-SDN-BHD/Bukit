---
name: bukit-content-to-template
description: Use when the user has defined a content schema (in site.yaml collections) and needs to generate precise, field-aware Scriban templates — helping bridge the gap between content structure and visual presentation

status: beta
since: "v3.0.0"
verified_by:
  - "src/Bukit.Engine/"
source_anchors:
  - "src/Bukit.Engine/"
guide_chapters:
  - "guide/user/08-themes-templates.md"
---

# Bukit Content-to-Template Generator

## Overview

This skill bridges the gap between content schema (defined in site.yaml) and Scriban templates. Given a collection's schema definition, it generates precise templates that render every field correctly — including proper HTML structure, CSS classes, and layout patterns.

**REQUIRED BACKGROUND:** Content schema in bukit-config. Template syntax in bukit-templating. Design tokens in bukit-design-tokens.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "Schema 生成模板"、"集合 schema"、"内容类型模板"、"字段映射模板" |
| English | "schema driven template", "generate template from schema", "collection schema template", "field-aware template" |
| Bahasa Melayu | "templat dipacu skema", "jana templat dari skema", "templat skema koleksi" |

## Workflow: Schema → Template

### Step 1: Parse Collection Schema
```yaml
collections:
  blog:
    template: pages/post.html
    listTemplate: pages/list.html
    schema:
      - name: title
        type: string
        required: true
      - name: date
        type: date
        required: true
      - name: tags
        type: array
      - name: cover
        type: string
      - name: author
        type: string
      - name: featured
        type: boolean
```

### Step 2: Field → Template Mapping

| Field Type | HTML Element | Scriban Pattern |
|---|---|---|
| `string` (required) | `<span>` | `{{ page.fields.KEY.value }}` |
| `string` (optional) | `<span>` with if-guard | `{{ if page.fields.KEY.value }}...{{ end }}` |
| `text` | `<span>` | `{{ page.fields.KEY.value }}` — `text` and `string` are interchangeable in schema definitions. ContentField.Type is `"text"` for both. |
| `date` | `<time>` | `{{ page.fields.KEY.value | date.to_string "%Y-%m-%d" }}` |
| `boolean` | badge/toggle | `{{ if page.fields.KEY.value }}<span class="badge">X</span>{{ end }}` |
| `number` | `<span>` | `{{ page.fields.KEY.value }}` |
| `array` (strings) | `<div>` list | `{{ for item in page.fields.KEY.value }}...{{ end }}` |
| `image` (url) | `<img>` | `<img src="{{ page.fields.KEY.value }}" alt="{{ page.title }}">` |
| `select` | `<span>` | `{{ page.fields.KEY.value }}` |
| `multi_select` | tag list | `{{ for tag in page.fields.KEY.value }}<span class="tag">{{ tag }}</span>{{ end }}` |

### Step 3: Generate Single Page Template
For the blog schema above:
```html
{% layout "layouts/base.html" %}

<article class="article">
  <header class="article-header">
    {{ if page.fields.featured.value }}<span class="badge badge-featured">Featured</span>{{ end }}
    {{ if page.fields.tags.value }}
      <div class="article-tags">
        {{ for tag in page.fields.tags.value }}
          <a class="tag" href="{{ site.base_url }}/tags/{{ tag | string.downcase }}/">{{ tag }}</a>
        {{ end }}
      </div>
    {{ end }}
    <h1>{{ page.title }}</h1>
    <div class="article-meta">
      {{ if page.fields.author.value }}<span class="meta-author">{{ page.fields.author.value }}</span> · {{ end }}
      {{ if page.publish_date }}<time>{{ page.publish_date | date.to_string "%B %d, %Y" }}</time>{{ end }}
    </div>
    {{ if page.summary }}<p class="article-summary">{{ page.summary }}</p>{{ end }}
  </header>
  {{ if page.fields.cover.value }}
    <img class="article-cover" src="{{ page.fields.cover.value }}" alt="{{ page.title }}">
  {{ end }}
  <div class="content">{{ page.content }}</div>
</article>
```

### Step 4: Generate List Card Partial
```html
<li class="card">
  {{ if item.fields.cover.value }}
    <div class="card-image-wrapper">
      <img class="card-image" src="{{ item.fields.cover.value }}" alt="{{ item.title }}" loading="lazy">
    </div>
  {{ end }}
  <div class="card-content">
    {{ if item.fields.tags.value }}
      <div class="card-tags">
        {{ for tag in item.fields.tags.value | array.limit 2 }}
          <span class="tag tag-sm">{{ tag }}</span>
        {{ end }}
      </div>
    {{ end }}
    <h2 class="card-title"><a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a></h2>
    <div class="card-meta">
      {{ if item.fields.author.value }}<span>{{ item.fields.author.value }}</span> · {{ end }}
      {{ if item.publish_date }}<time>{{ item.publish_date | date.to_string "%b %d, %Y" }}</time>{{ end }}
    </div>
    {{ if item.summary }}<p class="card-summary">{{ item.summary }}</p>{{ end }}
  </div>
</li>
```

## Field CSS Snippets

```css
/* Tags */
.tag { display: inline-block; padding: 2px var(--space-2); border-radius: var(--radius-sm); background: var(--color-surface-muted); color: var(--color-text-muted); font-size: var(--font-size-xs); font-weight: var(--font-weight-medium); }
.tag:hover { background: var(--color-primary); color: #fff; }

/* Badge */
.badge { display: inline-block; padding: 2px var(--space-2); border-radius: var(--radius-sm); font-size: var(--font-size-xs); font-weight: var(--font-weight-bold); text-transform: uppercase; }
.badge-featured { background: var(--color-accent); color: #fff; }

/* Article Cover */
.article-cover { width: 100%; max-height: 480px; object-fit: cover; border-radius: var(--radius-md); margin-bottom: var(--space-8); }

/* Article Meta */
.article-meta { display: flex; flex-wrap: wrap; gap: var(--space-2); color: var(--color-text-muted); font-size: var(--font-size-sm); }

/* Card */
.card-image { width: 100%; height: 200px; object-fit: cover; }
.card-meta { display: flex; flex-wrap: wrap; gap: var(--space-1); color: var(--color-text-muted); font-size: var(--font-size-sm); }

/* Empty State */
.empty-state { padding: var(--space-12); text-align: center; color: var(--color-text-muted); border: 2px dashed var(--color-border); border-radius: var(--radius-md); }
```

## Complete Example: Knowledge Base

Schema: `[title, section(select), order(number), last_updated(date), difficulty(select)]`

Generated template:
```html
{% layout "layouts/base.html" %}
<article class="article docs-article">
  <header class="article-header">
    <div class="docs-meta-top">
      {{ if page.fields.section.value }}<a href="{{ site.base_url }}/docs/{{ page.fields.section.value }}/">{{ page.fields.section.value }}</a>{{ end }}
      {{ if page.fields.difficulty.value }}<span class="difficulty difficulty-{{ page.fields.difficulty.value }}">{{ page.fields.difficulty.value }}</span>{{ end }}
    </div>
    <h1>{{ page.title }}</h1>
    {{ if page.fields.last_updated.value }}<time>Updated {{ page.fields.last_updated.value | date.to_string "%B %d, %Y" }}</time>{{ end }}
  </header>
  <div class="content">{{ page.content }}</div>
</article>
```

## Common Issues: Symptom-Cause-Fix

| Symptom | Likely Cause | Fix |
|---|---|---|
| Template renders blank where a field should appear | Field name in template does not match the collection schema or source property casing | Compare `site.collections.<key>.schema[].name` with the template access path and use the exact field key |
| Build fails or renders errors for optional fields | Template accesses `.value` on a field that may be absent | Guard optional fields with `{{ if page.fields.X && page.fields.X.value }}...{{ end }}` or use a fallback |
| Arrays render as `System...` or a single unformatted value | Array, multi-select, or tags field is printed directly instead of iterated | Render arrays with `{{ for item in page.fields.X.value }}...{{ end }}` and add an empty-state branch when needed |
| Dates show in the wrong format or fail formatting | Field is a string, null, or uses a provider-specific date value rather than a normalized date | Prefer normalized page dates such as `page.publish_date`, or validate schema type/format before applying date filters |
| List card works on detail pages but fails on list pages | Template uses `page.fields` inside a loop where the current item is named `item` | Use `item.fields.X.value` inside list loops and reserve `page.fields.X.value` for the current detail page |
| Links contain duplicate slashes or missing prefixes | Template concatenates `site.base_url`, item URL, and manual slashes inconsistently | Use a single convention, for example `{{ site.base_url }}{{ item.url }}`, and verify generated links under subdirectory deployment |
| Image fields render broken images | Field contains a Notion file object, relative path, missing default, or unprocessed media URL | Confirm media processing configuration, guard empty values, and provide a default image where cards require visual consistency |
| Schema-required fields still appear missing in output | Content validation is in warn mode, or existing content predates the schema | Run a config/build validation pass, fix content records, and switch to strict validation only after migration |
| Taxonomy/tag links lead to 404s | Template slugifies labels differently from Bukit's taxonomy route generation | Use the route data generated by taxonomy when available, or keep tag slugs normalized in content |
| Generated template ignores design tokens | Hard-coded CSS classes or inline values bypass the token system | Use semantic classes backed by `var(--color-*)`, `var(--spacing-*)`, and `var(--radius-*)` tokens |
| Template variable renders blank | Scriban `EnableRelaxedMemberAccess` silently returns null for typos | Run `bukit doctor` to perform template variable spell check |

## Content Pipeline Extension (IContentStage)

The content loading pipeline is organized as a sequence of independent stages. Plugin developers can inject custom stages by implementing `IContentStage`:

```
Before (monolithic)                After (stage-based)
─────────────────                  ──────────────────────
ContentPipeline.ExecuteAsync()     ContentPipeline.ExecuteAsync()
  (single method)                    ├─ ContentLoadStage
                                     ├─ ImageLocalizeStage
                                     ├─ DraftFilterStage
                                     ├─ SchemaDefaultsStage
                                     └─ SchemaValidateStage
                                   (custom stages injectable)
```

### Stage Contract

```csharp
public interface IContentStage
{
    string Name { get; }
    Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken);
}
```

### Built-in Stages

| Order | Stage | Responsibility | Per-stage Log |
|---|---|---|---|
| 1 | `ContentLoad` | Creates provider, loads items | `event=content.stage stage=ContentLoad duration_ms=234` |
| 2 | `ImageLocalize` | Downloads/localizes remote images | `event=content.stage stage=ImageLocalize duration_ms=156` |
| 3 | `DraftFilter` | Filters draft items | `event=content.stage stage=DraftFilter duration_ms=1` |
| 4 | `SchemaDefaults` | Applies schema defaults | `event=content.stage stage=SchemaDefaults duration_ms=3` |
| 5 | `SchemaValidate` | Validates against schema | `event=content.stage stage=SchemaValidate duration_ms=12` |

### Injecting Custom Stages

Custom stages receive `ContentStageInput` (with `Items`, `BodyStore`, `Config`, `RootDir`, `MediaCacheDir`, `Logger`) and return `ContentStageOutput` (with `Items`, `BodyStore`, `StageName`, `DurationMs`, `SchemaErrors`). Each stage's output feeds the next stage's input in a pipeline pattern.

## Common Mistakes

| Mistake | Fix |
|---|---|
| Missing `{{ if }}` guard for optional fields | Wrap optional fields in `{{ if page.fields.X.value }}...{{ end }}` |
| Using `page.fields.X` instead of `.value` | Always use `page.fields.X.value` |
| Not escaping user content | Use `{{ value | html.escape }}` |
| Missing `loading="lazy"` on list images | Add `loading="lazy"` to card images |
| No empty state for list pages | Add `{{ if pages.size == 0 }}...empty...{{ end }}` |
| Shortcode values not encoded in theme shortcodes | HTML-encode shortcode params: `{{ $1 | html.escape }}` (P1-1) |

## Performance Notes

### Image Rewrite Performance (P0-1)

Content image URL rewriting (e.g., localizing remote Notion images) uses a single-pass handwritten `HtmlMediaReferenceScanner` parser instead of multi-round regex scanning. This eliminates ~2x per-page CPU overhead for HTML documents containing multiple image references. Content authors with many inline images benefit most from this optimization.

### Shortcode Safety (P1-1)

When defining `theme.shortcodes`, always HTML-encode dynamic parameters. The engine now enforces `WebUtility.HtmlEncode` on shortcode values at the processor level, but explicit `html.escape` in template definitions is still the recommended defense-in-depth practice:

```yaml
theme:
  shortcodes:
    alert: '<div class="alert alert-{{ $1 | html.escape }}">{{ $2 | html.escape }}</div>'
```
