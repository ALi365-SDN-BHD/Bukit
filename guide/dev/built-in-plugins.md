# Built-in Plugins (BuiltIn) Artifacts and Boundaries

This page describes the "output contracts" of built-in plugins (what files/pages are generated, what config they depend on, how they behave under multilingual settings).

Implementation directory: `src/Bukit.Engine/Plugins/BuiltIn/`

Related docs: [Plugin System](./plugins.md), [i18n & SEO](./i18n-seo.md), [Engine Fixed Outputs](./engine-outputs.zh-CN.md)

## sitemap (IAfterBuildPlugin)

File: `SitemapPlugin.cs`
- Output: `<outputDir>/sitemap.xml`
- Dependency: `site.url` must be configured (skipped otherwise)
- Includes: `/`, `/blog/`, `/pages/`, all routed content pages, all derived routes
- lastmod: routed pages prefer `fields.update_time`, fallback to `publishAt`
- Exclusion: pages with `<meta name="robots" content="noindex|none ...">` are excluded
- Multilingual: `merged` mode generates at root; `split` generates per language directory

## rss (IAfterBuildPlugin)

File: `RssPlugin.cs`
- Output: `<outputDir>/rss.xml`
- Dependency: `site.url` must be configured
- Input: routed content only (no derived pages)
- Multilingual: same `merged`/`split` semantics as sitemap

## search-index (IAfterBuildPlugin)

File: `SearchIndexPlugin.cs`
- Output: `<outputDir>/search.json`
- Fields: `id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt`
- `site.searchIncludeDerived` controls whether derived pages are included
- Multilingual: per-language `search.json`; `index` mode generates root `search.index.json`

## taxonomy (IDerivePagesPlugin + IAfterBuildPlugin)

File: `TaxonomyPlugin.cs`

Derives pages from `meta.tags` / `meta.categories`:
- `/tags/` 鈫?`tags/index.html`
- `/tags/<slug>/` 鈫?`tags/<slug>/index.html`
- `/categories/` 鈫?`categories/index.html`
- `/categories/<slug>/` 鈫?`categories/<slug>/index.html`

Template: default `pages/page.html`, configurable via `taxonomy.template`/`taxonomy.indexTemplate`/`taxonomy.termTemplate`

## Route Validation for Derived Pages

All derive-pages plugins (Pagination, Archive, Taxonomy) share the same route validation pipeline:

1. **Per-plugin conflict check** — `PluginRunner.ApplyDeriveConflictPolicy` checks each derived page against content routes and previously accepted derived routes using normalized URL and outputPath comparison.
2. **Final inventory validation** — `RouteInventoryValidator.ValidateFinalRoutes` checks the complete route set (content + derived + list routes) before rendering begins.
3. **Doctor integration** — `bukit doctor` runs content route validation via `RouteInventoryValidator.BuildContentRoutesAsync` + `ValidateContentRoutes`, detecting conflicts without a full build.

All derived pages respect `site.outputPathEncoding` (applied via `RoutePathBuilder.BuildOutputPathFromUrl`).

