# Publish Audit Report Schema

Bukit writes `.bukit/publish-audit-report.json` on every build. This is the primary Machine Readability & Trust Audit artifact for generated content.

Current schema:

- `schema`: `https://bukit.dev/schemas/publish-audit-report.v1.json`
- `schemaVersion`: `1.0`
- JSON Schema file: [docs/schemas/publish-audit-report.v1.schema.json](schemas/publish-audit-report.v1.schema.json)

## Top-Level Shape

```json
{
  "schema": "https://bukit.dev/schemas/publish-audit-report.v1.json",
  "schemaVersion": "1.0",
  "generatedAt": "2026-06-05T00:00:00+00:00",
  "siteName": "example",
  "siteUrl": "https://example.com",
  "baseUrl": "/",
  "documents": [],
  "issues": [],
  "summary": {
    "documentCount": 0,
    "indexableCount": 0,
    "nonIndexableCount": 0,
    "errorCount": 0,
    "warningCount": 0
  }
}
```

## Document Fields

Each document represents one final published route:

- `routeUrl`, `outputPath`, `canonical`, `indexable`, and `lastModified` identify the route.
- `title`, `description`, `language`, `author`, `organization`, `source`, `originalSource`, and `reviewStatus` expose trust and provenance metadata.
- `entityNames`, `representationKinds`, and `schemaTypes` expose machine-readable discovery data.
- `sitemapIncluded`, `searchIncluded`, and `rssIncluded` show whether aggregate outputs include the route.

## Issue Codes

Publish audit issue codes use the `publish.*` prefix. Current checks include:

- Semantic HTML structure: `publish.semantic_main_missing`, `publish.semantic_article_missing`, `publish.semantic_header_missing`, `publish.semantic_nav_missing`, `publish.semantic_footer_missing`, `publish.heading_h1_missing`, `publish.heading_level_skip`, `publish.time_missing`, `publish.image_alt_missing`, `publish.figure_caption_missing`, and `publish.initial_html_unreadable`.
- JSON-LD consistency: `publish.jsonld_title_mismatch`, `publish.jsonld_description_mismatch`, `publish.jsonld_author_mismatch`, and `publish.jsonld_date_mismatch`.
- Trust metadata: `publish.author_missing`, `publish.source_missing`, `publish.source_references_missing`, `publish.review_status_missing`, `publish.updated_at_missing`, and `publish.entity_missing`.
- Publish document completeness: `publish.summary_missing` and `publish.entity_summary_missing`.
- Representation coverage: `publish.representation_missing`.
- Aggregate output compatibility: `publish.sitemap_missing_route`, `publish.search_missing_route`, `publish.rss_missing_route`, and `publish.ai_crawler_policy_conflict`.

## CLI

`bukit publish audit --dir dist` validates `.bukit/publish-audit-report.json` and returns non-zero when errors exist. `--strict` also fails on warnings.

`bukit publish diff` compares two publish audit reports and supports the same regression budgets as `seo diff`.

SEO and GEO reports remain available as compatibility and derived views, but publish audit is the primary report for machine readability, provenance, trust, and representation gaps.
