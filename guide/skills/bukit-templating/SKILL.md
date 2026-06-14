---
name: bukit-templating
description: Use when writing or debugging Bukit Scriban templates, layout inheritance, includes, template variables, content loops, partials, components, or template capability warnings.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Rendering.Tests/ScribanTemplateRendererTests.cs"
  - "tests/Bukit.Engine.Tests/ScribanTemplateLinterTests.cs"
source_anchors:
  - "src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs"
  - "src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs"
  - "src/Bukit.Engine/ScribanTemplateLinter.cs"
  - "src/Bukit.Engine/TemplateCapabilitiesResolver.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Templating

Bukit templates use Scriban. This skill covers authoring and diagnostics; use `bukit-theme` for directory and `theme.yaml` structure.

## Core Variables

| Variable | Meaning |
|---|---|
| `site` | Site metadata, params, base URL, menus, config-derived data |
| `page` | Current content item or generated page |
| `pages` | Content and derived page collections |
| `data` | Data-mode content sources and generated data |

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

## List Template Pattern

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

## Diagnostics

Use:

```bash
bukit doctor
bukit build
```

Doctor checks parsing, layout chains, includes, unknown variables, template capabilities, missing/stale declarations, and unused params.

## Safety Rules

- Keep asset URLs rooted with `site.base_url`.
- Escape user-facing fields unless a field is known to be sanitized HTML.
- Keep SEO head ownership aligned with `site.seo.renderMode`.
- Add `theme.yaml.templates` entries when a template has a role the engine should understand.
