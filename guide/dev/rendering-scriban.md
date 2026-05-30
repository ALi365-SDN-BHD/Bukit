# Rendering and Templates (Scriban)

The rendering layer is responsible for rendering engine-generated models into HTML using Scriban.

Implementation: `src/Bukit.Rendering/Models.cs`, `src/Bukit.Rendering/Scriban/` (10 files: ScribanTemplateRenderer, RenderSectionFunction, RenderComponentFunction, TemplateContextBuilder, FileTemplateLoader, ImageFunctions, ComponentFunctions, SectionRenderHelper, SectionDataResolverAccessor, ScribanModelBinder)

## Unified Rendering Pipeline

Page, list, and static HTML rendering now share a single dispatch loop in `PageRenderDispatcher.DispatchAsync()` (implementation: `src/Bukit.Engine/PageRenderDispatcher.cs`). Three entry kinds are defined in `RenderEntry.cs`:

| Kind | Source | Rendering Method |
|---|---|---|
| `Page` | Content items with routes | `renderer.RenderPage(template, pageModel)` |
| `List` | Special list routes (homepage, taxonomy, pagination) | `renderer.RenderList(template, listModel)` |
| `Static` | `.html` files in `static/` when `theme.staticTemplate` is set | `renderer.RenderPage(template, pageModel)` |

All three share the same incremental build skip logic, SEO injection, and error handling.

## Template Variable Spell Check

When `EnableRelaxedMemberAccess` is enabled (default), Scriban silently returns `null` for typo variables like `{{ page.titel }}`. Bukit's `doctor` command now includes template variable spell check via `ScribanTemplateLinter` that parses all `.html` templates using Scriban's AST and cross-references against a whitelist of known model fields.

Implementation: `src/Bukit.Rendering/Scriban/ScribanTemplateLinter.cs`

## Directory Conventions

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

- `static/`: Static assets. Non-HTML files copied as-is. When `theme.staticTemplate` is set, `.html` files are rendered through Scriban using the unified dispatch loop (same pipeline as content pages). Implementation: `src/Bukit.Engine/RenderEntry.cs` → `ForStaticDir()`.
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
