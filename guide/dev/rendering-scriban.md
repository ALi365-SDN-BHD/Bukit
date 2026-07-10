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
| `AnalyticsModel` | `site.analytics` |

The `Title` property on `SeoModel` is the semantic title used by OG, Twitter,
JSON-LD, search, and semantic title audits. Its `DocumentTitle` property is exposed as
`page.seo.document_title` and is the final HTML document title source. Core
builders always populate it; injection falls back to the `Title` property for
models produced by older callers.

## Template Selection

`ThemeTemplateResolver` resolves content templates from configured routes and
theme manifest entries. `TemplateCapabilitiesResolver` reads template capability
metadata such as search snippet support and list page content requirements.

## Rendering Flow

`RenderPipeline` renders content pages, list routes, and optional static HTML
entries. Incremental rendering compares content, route, template, SEO, and
model dependency hashes. The render dependency hash includes all three document
title config values.

In inject mode, the head post-processor uses a shared, quote-aware standard-head
scanner. It removes managed title/SEO elements only inside `<head>...</head>`,
then emits one encoded document title. The same scanner and title inspector feed
build diagnostics and the final report audit, including entity decoding and
whitespace normalization. Missing heads are diagnosed rather than synthesized.

## Safety

Output writes go through safe path validation. Template paths are resolved from
theme layouts and cannot be treated as arbitrary filesystem escape hatches.
