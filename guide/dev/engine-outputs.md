# Engine Fixed Outputs (Content-Independent Output)

In addition to pages rendered from content routing, the engine and built-in plugins generate some fixed outputs (e.g., homepage, blog/pages aggregation, SEO files). These are stable contracts: theme development must account for their corresponding templates and paths.

Implementation: `src/Bukit.Engine/SiteEngine.cs`

## Fixed Page Outputs

Regardless of content volume, the engine always generates:

- `/` → `index.html`
  - Uses template: `pages/index.html`
  - Model: `ListPageModel` (provides `site`, `pages`; sorted by `publish_date` descending)
- `/blog/` → `blog/index.html`
  - Uses template: `pages/list.html`
  - Model: `ListPageModel` (only items with URL starting `/blog/`)
- `/pages/` → `pages/index.html`
  - Uses template: `pages/list.html`
  - Model: `ListPageModel` (only items with URL starting `/pages/`)

Notes:
- These pages are parallel to `RouteGenerator` per-page routing; custom routes won't affect whether fixed pages are generated
- Fixed aggregate pages only use routed content, not derived pages

## Static Directory Copy Rules

Each build variant (single language or per-language subdirectory) will:

- Copy `theme.static` to the output root as-is
- Copy `theme.assets` to `assets/` in the output root

When referencing resources in templates, use `site.base_url` for path construction (see [Theme Development](./theme.md)).

## Safe Output FileSystem

All output write/delete operations are guarded by `SafeOutputFileSystem` (`src/Bukit.Engine/Output/SafeOutputFileSystem.cs`) which implements `IOutputFileSystem`:

- All relative paths are resolved against the build output root
- Path traversal (`../`), absolute paths, and cross-drive paths are rejected
- Stale file cleanup (pages, assets, static, media, plugin outputs) uses this guard
- This ensures no output operation can escape the designated output directory

## Relationship with Built-in Plugins

Some built-in plugins include these fixed pages in their outputs (e.g., sitemap includes `/`, `/blog/`, `/pages/`).

See: [Built-in Plugins](./built-in-plugins.md)
