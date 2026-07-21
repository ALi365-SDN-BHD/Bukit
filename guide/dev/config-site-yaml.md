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
- `site.analytics` allows only `enabled`, `productionOnly`, `consent`, `csp`,
  and `providers`.
  Provider objects are type-specific and reject fields owned by another
  provider even when those fields are empty.
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
| SEO/GEO | `SeoConfig` | Includes schema switches, robots, organization metadata, llms outputs, and AI bot mode. |
| Analytics | `AnalyticsConfig`, consent/CSP config records, `AnalyticsProviderConfig` | Core built-in plugin output policy, explicit Google Consent Mode v2 policy, CSP requirements reporting, and an ordered list of GA4, GTM, Plausible, or Umami providers. |
| Collections and list routes | `CollectionConfig`, `CollectionsValidator` | Includes collection permalink, list route, pagination, output, archive detail, and filtered lists. |
| Content sources | `ContentSourceConfig`, `ProviderValidators` | Includes Markdown paths, Notion filters, Notion cache, field policy, and `propertyMap` keys. |
| Content schema | `ContentModelSchemaConfig` | Includes canonical mappings, custom fields, field scopes, entity mappings, relation mappings, and media policy. |
| Media localization | `MediaConfig`, `ProviderValidators` media validation | Includes local download paths, field keys, retry controls, SSRF guard, and size/time limits. |
| Build reports | `BuildConfig`, `BuildReportConfig` | Includes report enabled state, security fail mode, fingerprinting, dotfile/symlink handling, and language jobs. |
| Theme | `ThemeConfig`, `ThemeManifestStrictValidator` | Includes local theme roots, params, shortcodes, components, SCSS, images, and component validation. |
| Taxonomy | `TaxonomyConfig`, `TaxonomyKindConfig` | Includes output mode, page size, pin fields, per-source pin fields, templates, hierarchy, and route prefix. |
| Deploy | `DeployConfig`, `ProviderValidators` deploy validation | GitHub Pages only; no deploy option bag surface exists. |

## Reliability-Sensitive Values

| Field | Runtime contract |
|---|---|
| `site.search.maxContentLength` | Positive UTF-16 code-unit limit for search `content` across document, list, plugin, publish-projection, and i18n outputs. Default `8000`; schema/runtime minimum `1`. |
| `content.media.maxConcurrency` | Positive active-download limit within one rewrite operation or localized body store. Default `4`; not a process-global network limit. |
| `build.followSymlinks` | Applies only to supported copy paths. Default Core recursive publication scanners still skip directory symlinks/reparse points. |

These are existing config fields. The reliability fixes changed their runtime
enforcement, not their YAML shape or schema identity.

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

## Analytics Contract

Analytics uses `site.plugins.analytics.enabled` for built-in plugin lifecycle
and `site.analytics.enabled` for feature output. The runtime requires both
switches, at least one provider, and an execution mode allowed by
`site.analytics.productionOnly`. Analytics is not an external plugin option
bag and is not bound into the Scriban site model.

Strict provider ownership is:

| Type | Allowed fields |
|---|---|
| `google-analytics` | `type`, `measurementId` |
| `google-tag-manager` | `type`, `containerId` |
| `plausible` | `type`, `domain`, `snippetMode`, `scriptUrl` |
| `umami` | `type`, `websiteId`, `scriptUrl` |

Validation normalizes Plausible IDN domains for duplicate detection and
requires script URLs to be absolute HTTPS `.js` URLs without credentials,
fragments, or non-default ports. Provider values are never arbitrary script
text. Plausible requires explicit `site-specific` or `legacy` `snippetMode`
and an explicit `scriptUrl`; there is no legacy URL default. For Plausible
Cloud, mode and the `/js/pa-<site-id>.js` path must agree.

Any GA or GTM provider requires `site.analytics.consent.google`. Only
`mode: advanced` is accepted; all four Consent Mode v2 defaults are required
as `granted|denied`, and `waitForUpdateMs` is optional from 0 through 5000.
Consent config without a Google provider is rejected. The fixed default
command precedes all Google bootstrap/config fragments; CMP updates remain
site-owned (or GTM-template-owned when GTM is present).

`site.analytics.csp.mode` accepts only `requirements-report` and requires
build reports. Analytics report v2 contains deterministic hashes and origins,
sets `completePolicy: false`, and never claims to be a complete deployment
policy. Core does not accept a static nonce because per-response nonce
generation belongs to the serving layer.

Breaking removal: the former googleAnalyticsId and disableInPreview keys are
unknown fields. They are not aliases or deprecated inputs, and no loader,
normalizer, environment variable, or runtime fallback may restore them.
