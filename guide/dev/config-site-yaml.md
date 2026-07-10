# Config Contract

The config contract is enforced at four layers: strict YAML key validation,
default application, typed model validation, and JSON Schema generation.

## Core Files

| File | Role |
|---|---|
| `AppConfig.cs` | Typed model and defaults. |
| `ConfigStrictFieldValidator.cs` | Unknown field rejection. |
| `SiteDefaultsApplier*.cs` | Default conversion from YAML nodes. |
| `ConfigValidator.cs` | Cross-field validation. |
| `ProviderValidators.cs` | Markdown, Notion, media, and deploy rules. |
| `CollectionsValidator.cs` | Collection routes, list routes, pagination, filters. |
| `ConfigJsonSchemaGenerator.cs` | Generated JSON Schema. |

## Important Rules

- `site.name`, `site.title`, and `content.sources` are required.
- `content.sources[].type` is `markdown` or `notion`.
- `content.sources[].mode` is `content` or `data`.
- `site.collections` keys must match collection references from content sources.
- Unknown fields are fatal.
- Provider secrets are read from environment variables, not config files.
- `deploy.provider` supports only `github-pages`.
- SEO document title templates accept only case-insensitive `{pageTitle}`,
  `{siteTitle}`, and `{separator}` placeholders. The page template requires
  `{pageTitle}`; the home template requires `{pageTitle}` or `{siteTitle}`.
- `titleSeparator` may be empty. Template results are whitespace-normalized in
  the Core model and HTML-encoded only at the rendering boundary.

## Field Families

Every field family must stay synchronized across the typed model, strict
validator, schema generator, user docs, and tests:

| Family | Primary model | Notes |
|---|---|---|
| Site identity and i18n | `SiteConfig`, `I18nValidator` | Includes `baseUrl`, language variants, timezone, output path encoding, sitemap mode, and plugin failure policies. |
| SEO/GEO/analytics | `SeoConfig`, `AnalyticsConfig` | Includes schema switches, robots, organization metadata, llms outputs, and AI bot mode. |
| Collections and list routes | `CollectionConfig`, `CollectionsValidator` | Includes collection permalink, list route, pagination, output, archive detail, and filtered lists. |
| Content sources | `ContentSourceConfig`, `ProviderValidators` | Includes Markdown paths, Notion filters, Notion cache, field policy, and `propertyMap` keys. |
| Content schema | `ContentModelSchemaConfig` | Includes canonical mappings, custom fields, field scopes, entity mappings, relation mappings, and media policy. |
| Media localization | `MediaConfig`, `ProviderValidators` media validation | Includes local download paths, field keys, retry controls, SSRF guard, and size/time limits. |
| Build reports | `BuildConfig`, `BuildReportConfig` | Includes report enabled state, security fail mode, fingerprinting, dotfile/symlink handling, and language jobs. |
| Theme | `ThemeConfig`, `ThemeManifestStrictValidator` | Includes local theme roots, params, shortcodes, components, SCSS, images, and component validation. |
| Taxonomy | `TaxonomyConfig`, `TaxonomyKindConfig` | Includes output mode, page size, pin fields, per-source pin fields, templates, hierarchy, and route prefix. |
| Deploy | `DeployConfig`, `ProviderValidators` deploy validation | GitHub Pages only; no deploy option bag surface exists. |

`site.sitemapDetail` is the sitemap detail config object. There is no sitemap
object nested directly under `site`; collection-level output has its own
`site.collections.<name>.output.sitemap` flag.

## Validation Coupling

When a config field changes, update all of these together:

1. `AppConfig`.
2. Strict field validator.
3. Default applier or collection reader.
4. Runtime validator.
5. JSON Schema generator.
6. User guide, dev guide, and skills.
7. Config contract tests.
8. `scripts/checks/config-docs-contract.sh` when the changed field is part of
   the documented public config surface.

This prevents docs, schema, loader, and runtime behavior from drifting.
