# Bukit Engine Outputs

This page documents files Bukit generates during a build, including configured pages, plugin artifacts, and report files.

---

## Configured Page Outputs

The only built-in page default is the site home route `/`, rendered with the theme's `home` template. `home` defaults to `pages/index.html` and is required unless `templates.home.template` overrides it.

| Page | Output Path | Template | Model |
|------|-------------|----------|-------|
| `/` | `index.html` | `templates.home.template` (default `pages/index.html`) | `ListPageModel` |
| collection `listRoute` | derived from `listRoute` | `collection.listTemplate` or theme `kind: list` match | `ListPageModel` |

Notes:
- `/blog/` and `/pages/` are not engine-fixed outputs. They appear only when site config declares collection list routes or equivalent derived pages.
- List pages only use routed content, not derived pages.

## Projection-Generated Aggregate Outputs

| File | Source | Notes |
|------|--------|-------|
| `sitemap.xml` | Publish projection adapter | Requires `site.url`. Multilingual: merged at root when `sitemapMode == merged`. |
| `rss.xml` | Publish projection adapter | Requires `site.url`. RSS 2.0 format. |
| `feed/atom.xml` | Publish projection adapter | Atom 1.0 format (when `site.feed.formats` includes `atom`). |
| `feed/feed.json` | Publish projection adapter | JSON Feed 1.1 (when `site.feed.formats` includes `json`). |
| `robots.txt` | Publish projection adapter via `RobotsTxtWriter` | Crawler policy generated before publish audit. |
| `llms.txt` | Publish projection adapter via `LlmsTxtPlugin` | AI crawler content summary. Configure via `site.seo.geo`. |
| `llms-full.txt` | Publish projection adapter via `LlmsTxtPlugin` | Full content for AI ingestion. |
| `search.json` | Publish projection adapter via `SearchIndexBuilder` | Search index. Configure via `site.search`. |

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

---

## Build Reports (dist/.bukit/)

When `build.report.enabled` is `true`, the engine writes these files:

| File | Content |
|------|---------|
| `build-report.json` | Build metadata (version, timings, environment, summary with page/route/asset/media/plugin/warning/error/schema error counts, incremental stats, generated file list) |
| `routes.json` | All routes with url, outputPath, template, source, kind, language |
| `assets.json` | All static assets with path, source, sha256 hash, size |
| `incremental-manifest.json` | Cache hit/miss counts, per-variant render counts, render reason breakdowns |

---

## Publish, SEO & GEO Reports (dist/.bukit/)

Always generated (not gated by `build.report.enabled`):

| File | Content |
|------|---------|
| `publish-audit-report.json` | Primary machine-readability and trust audit: semantic HTML, provenance, review status, entity metadata, representation coverage, and aggregate output consistency |
| `security-report.json` | Security checks (route traversal, unsafe slug, plugin output path, remote theme lock) |
| `seo-report.json` | SEO schema audit (`seo-report.v1`) checks per route: metadata completeness, canonical correctness, sitemap/rss inclusion, schema types, GEO indicators |
| `geo-report.json` | Derived GEO report: GEO Score (0-100), llms.txt/llms-full.txt status, geo-enhanced route list with schema types |

`seo-report.json` is generated under `.bukit` only. `bukit seo audit` resolves `.bukit/seo-report.json` by default; pass `--report` for explicit compatibility paths (for example `dist/.bukit/publish-audit-report.json`). `bukit publish audit` resolves `.bukit/publish-audit-report.json` by default and does not fall back to root paths.

## Publish Projections (dist/)

The publish projection pipeline writes machine-readable alternate representations before publish audit runs:

| File | Content |
|------|---------|
| `content/*.json` | Per-document canonical JSON projection with title, summary, body, provenance, trust, entities, relations, media, and canonical URL |
| `content/*.md` | Per-document Markdown/text projection for RAG and knowledge ingestion |
| `agent-manifest.json` | Indexable document manifest with canonical ids, language, review status, entities, and available HTML/semantic HTML/JSON/Markdown/JSON-LD representations |

`PublishRepresentationRegistry` is the internal source of truth for document representation kinds (`html`, `semantic-html`, `json`, `markdown`, optional `jsonld`) and aggregate outputs (`feed`, `atom`, `jsonfeed`, `sitemap`, `search`, `llms`, `llms-full`, `robots`, `agent-manifest`). Built-in JSON, Markdown, agent manifest outputs, and aggregate projections execute through `IPublishProjection.Project(PublishProjectionContext)` and return route-level output inventory. Aggregate projections call the same feed, sitemap, search, llms.txt, llms-full.txt, and robots.txt generators used by the build pipeline, then publish audit consumes the returned projection results before falling back to file inspection. `agent-manifest.json` is written only by the projection pipeline. Publish audit validates that declared JSON and Markdown representation files exist, records a `representations[]` inventory for every publish document, and checks that JSON, Markdown, `llms.txt`, `llms-full.txt`, and agent manifest metadata match the publish document route, identity, language, trust, provenance, and entities.

---

## Visual Report (dist/.bukit/)

Generated by the `visual-feedback` external protocol plugin:

| File | Content |
|------|---------|
| `visual-report.json` | AI-powered 5-dimension visual quality assessment per page per viewport width (layout, readability, color, a11y, responsive, each 0-100) |
| `screenshots/*.png` | Full-page screenshots at each configured viewport width |

---

## Metrics Output

When `--metrics <path>` is passed to `bukit build`:

| File | Content |
|------|---------|
| `<metrics-path>` | JSON build metrics: item count, rendered/skipped counts, timing breakdown |

---

## Summary: Complete Output Directory

After a full build with all features enabled:

```text
dist/
├── .bukit/
│   ├── build-report.json
│   ├── routes.json
│   ├── assets.json
│   ├── incremental-manifest.json
│   ├── security-report.json
│   ├── seo-report.json
│   ├── publish-audit-report.json
│   ├── geo-report.json
│   ├── visual-report.json        (if visual-feedback plugin enabled)
│   └── screenshots/
│       ├── -w375.png
│       ├── -w1440.png
│       └── ...
├── sitemap.xml
├── rss.xml
├── robots.txt
├── llms.txt
├── llms-full.txt
├── search.json
├── feed/                         (if atom/json formats enabled)
│   ├── atom.xml
│   └── feed.json
├── index.html                    (site pages)
├── assets/                       (theme assets)
└── ...                           (additional routed pages)
```

---

## Relationship with Built-in Plugins

Some built-in plugins include fixed pages in their outputs (e.g., sitemap includes `/`, `/blog/`, `/pages/`).

See: [Built-in Plugins](./built-in-plugins.md)
