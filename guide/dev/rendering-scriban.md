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

## Template Selection

`ThemeTemplateResolver` resolves content templates from configured routes and
theme manifest entries. `TemplateCapabilitiesResolver` reads template capability
metadata such as search snippet support and list page content requirements.

## Rendering Flow

`RenderPipeline` renders content pages, list routes, and optional static HTML
entries. Incremental rendering compares content, route, template, SEO, and
model dependency hashes.

## Safety

Output writes go through safe path validation. Template paths are resolved from
theme layouts and cannot be treated as arbitrary filesystem escape hatches.
