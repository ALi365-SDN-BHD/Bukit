# Modules and Data

Core 1.0 data is built from data-mode content sources and built-in generated outputs.

## Data Sources

```yaml
content:
  sources:
    - type: markdown
      name: team
      mode: data
      markdown:
        dir: data/team
```

Use data-mode sources for reusable lists such as FAQs, staff profiles, product metadata, or navigation cards.

## Template Access

Templates can read data through the renderer's `data` object. Keep source names stable because templates depend on them.

```html
{{ for member in data.team }}
  <article>
    <h2>{{ member.title }}</h2>
    {{ if member.summary }}<p>{{ member.summary }}</p>{{ end }}
  </article>
{{ end }}
```

## Generated Data

Built-in plugins can derive page indexes, taxonomy data, pagination pages, archives, related content, aliases, menus, and processed image outputs. Toggle built-ins through `site.plugins` only when you intentionally need to disable one.

```yaml
site:
  plugins:
    RelatedContent:
      enabled: false
```
