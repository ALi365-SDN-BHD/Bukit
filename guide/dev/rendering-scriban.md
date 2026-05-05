# Rendering and Templates (Scriban)

The rendering layer is responsible for rendering engine-generated models into HTML using Scriban.

Implementation: `src/Bukit.Rendering/Models.cs`, `src/Bukit.Rendering/Scriban/ScribanModelBinder.cs`, `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`

## Directory Conventions

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

- `static/`: Copied as-is to output root
- `assets/`: Copied to output `assets/`
- `layouts/`: Template root directory

## Template Variable Structure

### site: `site.name`, `site.title`, `site.url`, `site.description`, `site.base_url`, `site.language`, `site.params`, `site.modules`, `site.data`

### page: `page.title`, `page.url`, `page.content`, `page.summary`, `page.publish_date`, `page.fields`

### pages (list pages): Each entry has same structure as page

Whether `pages[*].content` is populated is controlled by `build.listPageContentMode`.

## fields Usage Convention

```scriban
<title>
  {{ if page.fields.seo_title }}
    {{ page.fields.seo_title.value }}
  {{ else }}
    {{ page.title }}
  {{ end }}
  - {{ site.title }}
</title>
```

- Markdown reserved keys don't enter fields; tags/categories/summary are written into fields
- Notion: fields keys normalized to `lowercase_with_underscores`, controlled by `fieldPolicy`
