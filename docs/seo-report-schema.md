# SEO Audit Report Schema

Bukit writes `seo-report.json` at every build output root. The report is a machine-readable SEO URL inventory and CI artifact.

Current schema:

- `schema`: `https://bukit.dev/schemas/seo-report.v1.json`
- `schemaVersion`: `1.0`
- JSON Schema file: [docs/schemas/seo-report.v1.schema.json](schemas/seo-report.v1.schema.json)

## Stability Contract

Bukit keeps this report diff-friendly:

- `routes` are sorted by `url`, then `outputPath`.
- `issues` are sorted by severity, route, code, then message.
- `schemaVersion` changes only when fields are removed, renamed, or their meaning changes.
- New optional fields may be added without changing the major version.
- `generatedAt` is intentionally time-dependent and should be ignored by strict artifact diffs.

## Top-Level Shape

```json
{
  "schema": "https://bukit.dev/schemas/seo-report.v1.json",
  "schemaVersion": "1.0",
  "generatedAt": "2026-05-14T00:00:00+00:00",
  "siteName": "example",
  "siteUrl": "https://example.com",
  "baseUrl": "/",
  "routes": [],
  "issues": [],
  "summary": {
    "routeCount": 0,
    "indexableCount": 0,
    "nonIndexableCount": 0,
    "errorCount": 0,
    "warningCount": 0
  }
}
```

## Route Inventory Fields

Each route entry represents one final generated route from `SeoIndex`:

- `url`: route URL.
- `outputPath`: generated file path relative to the build output.
- `title`: computed SEO title.
- `description`: computed SEO description.
- `canonical`: computed canonical URL.
- `robots`: robots directive, when configured.
- `indexable`: `false` when robots contains `noindex` or `none`.
- `lastModified`: SEO index lastmod timestamp.
- `contentType`: page/content type from the source item or `list`.
- `sourceItemId`: source content id when the route is content-backed.
- `sitemapIncluded`: whether the canonical appears in `sitemap.xml`.
- `searchIncluded`: whether the route appears in `search.json`.
- `rssIncluded`: whether the route appears in `rss.xml`.
- `alternates`: HTML/sitemap hreflang alternates.
- `schemaTypes`: JSON-LD `@type` values found in the SEO model.

## Issue Fields

Each issue has:

- `severity`: `error` or `warning`.
- `code`: stable machine-readable issue code.
- `route`: route URL, or `null` for site-level issues.
- `message`: human-readable explanation.

`bukit seo audit --dir dist` returns non-zero when errors exist. `--strict` also fails on warnings.

Before evaluating issue counts, `bukit seo audit` validates the report contract:

- `schema` must match the current schema URL.
- `schemaVersion` must match the supported version.
- `routes`, `issues`, and `summary` must exist with the required field types.
- each route must expose the core URL inventory fields used by CI diffs.
- each issue must expose `severity`, `code`, and `message`, with severity limited to `error` or `warning`.

Contract failures return exit code `2`, separate from SEO audit failures (`1`).
