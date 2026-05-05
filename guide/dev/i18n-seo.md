# Internationalization and SEO (sitemap/rss/search modes)

Implementation: `src/Bukit.Config/AppConfig.cs`, `src/Bukit.Engine/SiteEngine.cs`

## Multilingual Output Structure

When `site.languages` is set:
- Output uses per-language subdirectories: `dist/<lang>/...`
- Each variant uses `baseUrl + /<lang>` internally
- `site.defaultLanguage` must be in `site.languages`

Without `site.languages`: single-language mode, `dist/...`

## sitemapMode

| Value | Behavior |
|---|---|
| `split` | Each language generates its own sitemap |
| `merged` | Generates merged sitemap with hreflang alternates |
| `index` | Sitemap index pointing to per-language sitemap.xml |

Constraints: `site.url` must be set for absolute URLs; merged alternates rely on `meta.i18nKey`.

## rssMode: `split` (per language) or `merged`

## searchMode: `split`, `merged`, or `index`

`searchIncludeDerived` controls whether derived pages are in the search index.

## baseUrl and site.url Boundaries

- `site.baseUrl`: internal relative links, output path prefix
- `site.url`: absolute URLs for sitemap/rss
