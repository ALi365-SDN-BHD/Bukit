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

## Template Security

### Shortcode HTML Encoding (P1-1)

Shortcode parameter values are HTML-encoded via `WebUtility.HtmlEncode` before template substitution in `ShortcodeProcessor.cs`. This prevents stored XSS attacks where content authors could inject `<script>` tags through shortcode parameters like `{% card "<script>alert(1)</script>" %}`.

When defining custom shortcodes in `theme.shortcodes`, use Scriban's `html.escape` filter in parameter output:
```yaml
theme:
  shortcodes:
    card: '<div class="card">{{ $1 | html.escape }}</div>'
```

### Block Renderer Color Safety (P1-2)

All Notion block renderers (Callout, ToDo, Toggle, Bookmark, Equation) now use manually HTML-encoded color values when constructing `class="notion-{color}"` CSS attributes. This prevents HTML injection through Notion's color property values.

### Image Tag Safety

All image tags generated via `BuildImgTag`/`BuildSrcset` in `ImageHelper` use `WebUtility.HtmlEncode` on attribute values combined with an `IsSafeImageSource` protocol whitelist.

## Rendering Module Structure (P2-3)

The rendering module has been decomposed from a single `ScribanTemplateRenderer.cs` (~422 lines) into 10 independent files under `src/Bukit.Rendering/Scriban/`:

| File | Responsibility |
|---|---|
| `ScribanTemplateRenderer.cs` | Core rendering orchestrator |
| `RenderSectionFunction.cs` | `render_section` Scriban function |
| `RenderComponentFunction.cs` | `render_component` Scriban function |
| `TemplateContextBuilder.cs` | Template context and model construction |
| `FileTemplateLoader.cs` | Template file resolution with path safety |
| `ImageFunctions.cs` | Image processing Scriban functions |
| `ComponentFunctions.cs` | Component system Scriban functions (instance-based, all readonly) |
| `SectionRenderHelper.cs` | Section rendering logic |
| `SectionDataResolverAccessor.cs` | Section data resolver bridge |
| `ScribanModelBinder.cs` | Model-to-Scriban binding logic |
