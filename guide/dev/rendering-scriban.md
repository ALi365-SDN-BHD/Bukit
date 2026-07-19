# Scriban Rendering

`Bukit.Rendering` owns the Scriban renderer, template loader, context binding,
component functions, section functions, and image helper functions.

## Models

| Model | Template Object |
|---|---|
| `SiteModel` | `site` |
| `PageInfo` | `page` |
| `ListPageModel` | list template root |
| `ListPaginationModel` | `pagination` |
| `SeoModel` | `page.seo` and list SEO context |

Analytics is intentionally absent from the rendering model. Scriban has no
`site.analytics` object, and themes cannot read provider configuration or
render a compatibility Analytics partial. Analytics output belongs to the
Core built-in plugin after template rendering.

The `Title` property on `SeoModel` is the semantic title used by OG, Twitter,
JSON-LD, search, and semantic title audits. Its `DocumentTitle` property is exposed as
`page.seo.document_title` and is the final HTML document title source. Core
builders always populate it; injection falls back to the `Title` property for
models produced by older callers.

## Template Selection

`ThemeTemplateResolver` resolves content templates from configured routes and
theme manifest entries. `TemplateCapabilitiesResolver` reads template capability
metadata from `bukit.templates.yaml`, such as search snippet support and list
page content requirements. The resolver fingerprints current manifest text;
static root/include/layout analysis is local to the current call. Returned
capability field collections are snapshots rather than mutable cache entries.

## Rendering Flow

`RenderPipeline` renders content pages, list routes, and optional static HTML
entries. Before writing, `PageRenderDispatcher` applies one HTML transform
pipeline to all three entry types. Core SEO transformation runs first, followed
by ordered transforms contributed by enabled built-in plugins such as
Analytics. Analytics remains active when SEO rendering is disabled or uses
`theme`/`off` mode.

Incremental rendering compares content, route, template, SEO, Analytics, and
model dependency hashes. Analytics dependencies include both switches,
execution mode, provider order, provider type, unique key, and normalized
options, so an output-affecting config change forces re-rendering. The render
dependency hash also includes all three document title config values.

In inject mode, the head post-processor uses a shared, quote-aware standard-head
scanner. It removes managed title/SEO elements only inside `<head>...</head>`,
then emits one encoded document title. The same scanner and title inspector feed
build diagnostics and the final report audit, including entity decoding and
whitespace normalization. Missing heads are diagnosed rather than synthesized.

The Analytics transform likewise does not synthesize `<head>` or `<body>`.
It replaces only well-formed, current Bukit-managed Analytics blocks and leaves
unmarked third-party scripts or damaged markers untouched. Provider values are
encoded at their HTML or JavaScript output boundary.

## Safety

Output writes go through safe path validation. Template paths are resolved from
theme layouts and cannot be treated as arbitrary filesystem escape hatches.
