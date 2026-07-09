# 02 Core Concepts

Bukit Core is a static publishing pipeline. It turns configured content sources
into normalized content documents, routes them, renders them with Scriban, and
writes HTML plus machine-readable outputs.

## Pipeline

1. `ConfigLoader` reads `site.yaml`, rejects unknown fields, applies defaults,
   and validates provider, collection, taxonomy, theme, and deploy settings.
2. `ContentPipeline` loads `content.sources[]` through Markdown or Notion
   providers and builds a canonical content graph.
3. `RoutePipeline` filters data-only items, applies route rules, builds static
   list routes, and rejects conflicts.
4. `VariantBuildPipeline` renders one language variant, runs built-in plugin
   stages, copies assets, and writes reports.
5. `I18nOutputMerger` combines per-language outputs when `site.languages` is set.
6. `BuildReportPipeline` writes projections, SEO reports, publish audit inputs,
   route data, metrics, and security report data.

## Main Objects

| Object | Meaning |
|---|---|
| `AppConfig` | Strict `site.yaml` model. |
| `ContentDocument` | Normalized item loaded from a source. |
| `RouteInfo` | URL, output path, and template selected for an item. |
| `SiteModel` | Global template object exposed as `site`. |
| `PageInfo` | Current page object exposed as `page`. |
| `ListPageModel` | List route template object with `pages`, `items`, and `pagination`. |

## Core Boundary

Core is the stable generator and publishing tool. Labs workflows can integrate
with Core, but Core docs only assume the static command set from
`BukitCliSpecs.cs`.
