---
name: bukit-seo
description: Use when configuring Bukit SEO metadata (site.seo, titles, canonical, Open Graph, Twitter, JSON-LD, render modes, per-page SEO front matter) or running and interpreting `bukit seo audit` and `bukit seo diff`.
---

# Bukit SEO

Core builds an SEO model for every page (canonical, description, robots, Open Graph,
Twitter, article metadata, hreflang alternates, JSON-LD, final `<title>`), then either
injects it into HTML or hands it to the theme. Every build writes
`<output>/.bukit/seo-report.json`.

## Configuration

```yaml
site:
  url: https://example.com          # needed for absolute canonicals, sitemap, feeds
  title: Example
  description: Default description
  seo:
    enabled: true
    renderMode: inject              # inject|theme|off
    diagnostics: warn               # off|warn|strict
    homeTitleTemplate: "{siteTitle}"
    pageTitleTemplate: "{pageTitle}{separator}{siteTitle}"
    titleSeparator: " | "
    defaultImage: /assets/og.png
    twitterSite: "@example"
    defaultAuthor: Example Desk
    defaultAuthorType: Person       # Person|Organization
    organization:
      type: Organization            # Organization|NewsMediaOrganization
      name: Example
      url: https://example.com/
      logo: /assets/logo.png
      sameAs: [https://www.linkedin.com/company/example/]
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
      breadcrumbHome: false
      breadcrumbParent: categories  # must be a configured taxonomy
    robotsTxt: { enabled: false }   # see bukit-outputs
    geo: { enabled: true }          # see bukit-geo
```

Title templates accept only `{pageTitle}`, `{siteTitle}`, `{separator}`
(case-insensitive). `pageTitleTemplate` must contain `{pageTitle}`;
`homeTitleTemplate` must contain `{pageTitle}` or `{siteTitle}`. Other placeholders,
unbalanced braces, or empty templates fail config validation.

## Render modes

| Mode | Behavior |
|---|---|
| `inject` | Core writes the head tags and replaces all existing `<title>` elements with exactly one encoded document title. Adds JSON/Markdown alternate links for indexable content pages. |
| `theme` | No HTML changes. The theme renders the head from `page.seo.*` (for example `page.seo.document_title` and the JSON-LD). Audits still run. |
| `off` | No injection; model still exposed; diagnostics still run. |

The per-page HTML checks (see "Build-time diagnostics") read the final HTML in every
mode, including pages with `seo_inject: false`. In `theme` or `off` mode the theme must
write the canonical, robots, hreflang, and JSON-LD tags itself, or each page warns and
`diagnostics: strict` fails the build.

`page.seo.title` is the semantic page title; `page.seo.document_title` is the final
`<title>` text. A single page can opt out of injection with `seo_inject: false`.

## Per-page front matter

| Field | Purpose |
|---|---|
| `seo_title` | SEO title override. |
| `seo_desc` | Meta description (not `seo_description`). Falls back to `summary`, then `site.description`. |
| `canonical` | Explicit canonical URL. |
| `robots`, `noindex`, `nofollow` | Robots directives. |
| `image`, `cover`, `seo_image`, `og_image` | Social image. |
| `schema_type`, `seo_article` | JSON-LD type selection (`Article`, `BlogPosting`, `NewsArticle`, ...). |
| `twitter_creator` | Twitter creator handle. |
| `sitemapexclude`, `searchexclude`, `feedexclude` | Exclude from those outputs. |
| `geo:` | GEO structured data; see `bukit-geo`. |

`og:type` is `article` for article schema types, otherwise `website`. The Twitter card
is `summary_large_image` when an image exists. `site.collections.<c>.noindexWhenEmpty:
true` makes empty list pages `noindex,follow` and removes them from sitemap, search,
and llms output.

## Build-time diagnostics

Logged during build, controlled by `site.seo.diagnostics`: `seo.site_url_missing`,
`seo.canonical_relative`, `seo.canonical_external`, `seo.canonical_double_slash`,
`seo.canonical_duplicate_index`, `seo.hreflang_x_default_missing`, per-page HTML checks
(`seo.head_missing`, `seo.canonical_missing`, `seo.canonical_duplicate`,
`seo.robots_missing`, `seo.hreflang_missing`, `seo.json_ld_missing`), GEO schema and
`geo.llms_*` curation checks.
`strict` makes any of these stop the build at the first one hit (exit 2), except
`seo.og_locale_region_missing`, which stays a warning; `off` silences them.

## Audit

```bash
bukit build
bukit seo audit --dir dist            # exit 1 on errors
bukit seo audit --dir dist --strict   # also exit 1 on warnings
bukit seo audit --dir dist --external # live HTTP checks of external links and images
```

`--dir` defaults to `dist`; pass the real `build.output`. `--report <file>` reads a
specific report. A missing default report exits 1 ("Run a full build first").

Common issue codes:

| Severity | Codes |
|---|---|
| Error | `seo.title_missing`, `seo.document_title_missing`, `seo.document_title_multiple`, `seo.document_title_empty`, `seo.document_title_mismatch` (inject mode), `seo.inject_canonical_missing`, `seo.canonical_points_to_noindex`, `seo.noindex_in_sitemap`, `seo.json_ld_invalid`, `seo.robots_txt_blocks_indexable` |
| Warning | `seo.title_too_long`, `seo.document_title_too_long` (display width > 60, CJK counts 2), `seo.description_missing`, `seo.description_too_long` (> 160), `seo.title_duplicate`, `seo.description_duplicate`, `seo.canonical_not_absolute`, `seo.canonical_http`, `seo.json_ld_not_rendered` (theme mode without JSON-LD), `seo.og_image_missing_file`, `seo.og_image_too_small`, `seo.html_head_missing` |

A missing output file reports only `seo.output_file_missing`. A page without `<head>`
also reports `seo.html_head_missing` and `seo.document_title_missing`.

## Regression diff

```bash
cp dist/.bukit/seo-report.json baseline-seo.json          # on the main branch
bukit seo diff --baseline baseline-seo.json --current dist/.bukit/seo-report.json \
  --max-new-errors 0 --max-new-warnings 5 \
  --fail-on-route-removed --fail-on-indexable-drop
```

Without any threshold flag, diff only prints and exits 0. `--max-new-issues n` budgets
errors and warnings together. `--fail-on-new-code a,b` fails when listed codes appear. Exit 1 means a budget was exceeded; 2 means bad input.
Do not diff a SEO report against a publish report.

Related: `bukit-outputs` (sitemap, robots), `bukit-geo`, `bukit-seo-insights`
(search-console and analytics joins), `bukit-publish-deploy`.
