# Routing System (Configuration-Driven Paths)

Maps `ContentItem` to `RouteInfo(url, outputPath, template)`.

Implementation: `src/Bukit.Routing/RouteGenerator.cs`, `src/Bukit.Routing/RoutePathBuilder.cs`, `src/Bukit.Engine/RouteInventoryValidator.cs`

## Collection-Driven Routing (Primary Model)

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

Each collection requires `permalink` (must contain `{slug}`) and `template`.

## Permalink Patterns

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
```

Placeholders: `{slug}`, `{year}`, `{month}`, `{day}`, `{type}`

Priority (high to low):
1. Full route override (url + outputPath + template) 
2. Partial route override (url only or url + template)
3. Collection Rules (`site.collections`)
4. Permalink Patterns (`site.permalinks`)

There is no built-in `post`/`page` route fallback. If none of these rules match, route generation throws a `ConfigException` and doctor/build asks the site to add explicit routing config.

## Route Override

### Full Override

When all three of `url`, `outputPath`, `template` are present in meta, routing is completely overridden. Notion `url`/`outputPath`/`template` fields are promoted to meta.

```yaml
route:
  url: /custom/
  outputPath: custom/index.html
  template: pages/page.html
```

Or top-level fields: `url:`, `outputPath:`, `template:`.

### Partial Override (url-only)

When only `url` is provided, Bukit auto-derives `outputPath` from the URL using `RoutePathBuilder.BuildOutputPathFromUrl`. The `template` inherits from collection/permalink/theme template resolution rules.

```yaml
url: /my-slug/
# outputPath → my-slug/index.html (auto-derived)
# template   → follows collection rule
```

Rules:
- `url` must be present (normalized with leading/trailing slashes)
- `outputPath` is auto-derived; any manually-supplied value is ignored
- `template` if omitted, inherits from collection/permalink/theme template resolution rules
- `outputPath`-only override is **not supported**

## outputPath Encoding: `none`/`slug`/`urlencode`/`sanitize`

`site.outputPathEncoding` controls output directory name encoding. Applies to both content pages and derived pages (pagination, archive, taxonomy).

## Route Path Utilities

All routing logic shares `RoutePathBuilder` (`src/Bukit.Routing/RoutePathBuilder.cs`):

| Method | Purpose |
|--------|---------|
| `NormalizeUrl(url)` | Ensure leading/trailing slashes |
| `NormalizeListRoute(url)` | List route normalization (defaults to `/`) |
| `BuildOutputPathFromUrl(url, encoding)` | URL → output path with `index.html` |
| `NormalizeOutputPath(path, encoding)` | Apply encoding to path segments |

Used by: `RouteGenerator`, `PaginationPlugin`, `ArchivePlugin`, `TaxonomyPlugin`, `PageRenderDispatcher`, `SeoAlternatesService.BuildListRoutesCore`, `I18nOutputMerger`.

## Route Conflict Detection

`RouteInventoryValidator` (`src/Bukit.Engine/RouteInventoryValidator.cs`) validates route uniqueness at two points:

1. **After content route generation** — `ValidateContentRoutes` checks content-page URL/outputPath conflicts. Throws `ConfigException` on duplicate.
2. **Before rendering** — `ValidateFinalRoutes` checks the complete inventory (content + derived + list routes).

`bukit doctor` also runs content route validation without a full build.

### Derived Page Conflicts

Controlled by `site.deriveConflictPolicy`: `fail` (default, throws `InvalidOperationException`), `warn` (skip + log), `last-wins` (accept derived page). Detection runs per-plugin in `PluginRunner.ApplyDeriveConflictPolicy` and in final `ValidateFinalRoutes`.

Content-page-vs-content-page conflicts **always fail** — `deriveConflictPolicy` does not apply to them.

## Fixed Aggregation Pages

The engine generates `/`, `/blog/`, `/pages/` (and any configured `listRoute`) regardless of content.
