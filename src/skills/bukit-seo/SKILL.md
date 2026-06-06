---
name: bukit-seo
description: Use when using bukit to configure traditional SEO (site.seo node, renderMode, organization, schema, robotsTxt), adding seo_title/seo_desc/canonical/og_image front matter fields, running bukit seo audit or bukit seo diff, interpreting seo.* diagnostic codes, setting up Open Graph / Twitter Card / JSON-LD / sitemap / robots.txt, or troubleshooting SEO audit failures

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Engine/SeoDiagnostics.cs"
  - "src/Bukit.Engine/SeoAuditReportWriter.cs"
source_anchors:
  - "src/Bukit.Engine/SeoDiagnostics.cs"
  - "src/Bukit.Engine/SeoAuditReportWriter.cs"
guide_chapters:
  - "guide/user/11-i18n-seo.md"
---

# Bukit Traditional Search Engine Optimization (SEO)

## Overview

Bukit SEO covers the traditional search engine optimization pipeline — **configuration** (`site.seo` with 5 sub-nodes), **two rendering modes** (engine inject vs theme responsible), **content Front Matter** (priority fallback chain), **6 Schema.org JSON-LD types**, **build-time diagnostics** (11 codes), compatibility `seo-report.json`, and **CLI audit/diff** commands with CI/CD regression gating. For machine readability, provenance, trust, and representation coverage, use `bukit publish audit`.

**REQUIRED BACKGROUND:** SEO config lives under `site.seo` in site.yaml — you must understand the config model in bukit-config first. GEO features live under `site.seo.geo` — see bukit-geo.
**REQUIRED SUB-SKILL:** Build sites with `bukit build`, audit with `bukit seo audit`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "SEO"、"搜索引擎优化"、"Open Graph"、"Twitter Card"、"JSON-LD"、"canonical"、"meta description"、"sitemap"、"robots.txt"、"seo_title"、"seo audit"、"seo diff" |
| English | "SEO", "search engine optimization", "Open Graph", "Twitter Card", "JSON-LD", "canonical URL", "meta description", "sitemap", "robots.txt", "seo_title", "seo audit", "seo diff" |
| Bahasa Melayu | "SEO", "pengoptimuman enjin carian", "Open Graph", "Twitter Card", "JSON-LD", "URL kanonikal", "penerangan meta", "peta laman", "robots.txt" |

## Configuration

The `site.seo` node controls all traditional SEO behavior. Enable the parent node, then adjust sub-nodes as needed:

```yaml
site:
  seo:
    enabled: true              # Master switch (default: true)
    renderMode: "inject"       # "inject" = engine injects tags, omit = theme responsible
    diagnostics: "warn"        # "off" / "warn" / "strict" (default: "warn")
    defaultImage: /assets/images/default.png   # Fallback OG/Twitter image
    twitterSite: "@myhandle"   # Twitter site handle for twitter:site
    organization:              # Organization schema (optional, for site-wide JSON-LD)
      name: My Company
      url: https://example.com
      logo: https://example.com/logo.png
    robotsTxt:                 # robots.txt generation control
      enabled: true            # (default: false)
    schema:                    # Schema.org JSON-LD toggles
      webPage: true            # Generate WebPage (default: true)
      collectionPage: true     # Use CollectionPage for list pages (default: true)
      searchAction: true       # Generate SearchAction (default: true)
    geo:                       # GEO sub-node — see bukit-geo skill
      enabled: true
```

### `site.seo` Field Reference

| Node | Field | Type | Default | Description |
|------|------|------|--------|------|
| `seo` | `enabled` | bool | `true` | Master switch — disables ALL SEO output |
| `seo` | `renderMode` | string | — | `"inject"` = engine injects into `<head>`; omit = theme renders tags |
| `seo` | `diagnostics` | string | `"warn"` | `"off"` = skip all; `"warn"` = log warnings; `"strict"` = errors + abort build |
| `seo` | `defaultImage` | string? | — | Fallback image for OG/Twitter when content has no image |
| `seo` | `twitterSite` | string? | — | Twitter `@handle` for `twitter:site` tag |
| `seo.organization` | `name` | string? | — | Organization name for JSON-LD |
| `seo.organization` | `url` | string? | — | Organization URL for JSON-LD |
| `seo.organization` | `logo` | string? | — | Organization logo URL for JSON-LD |
| `seo.robotsTxt` | `enabled` | bool | `false` | Generate `robots.txt` (requires `site.url`) |
| `seo.schema` | `webPage` | bool | `true` | Generate WebPage JSON-LD per route |
| `seo.schema` | `collectionPage` | bool | `true` | Use CollectionPage for list/taxonomy/pagination routes |
| `seo.schema` | `searchAction` | bool | `true` | Add SearchAction to WebSite JSON-LD |

### Diagnostic Modes

| `diagnostics` Value | Build Behavior | Effect on Logs |
|------|------|------|
| `"off"` | All diagnostics skipped | No SEO log output |
| `"warn"` | Diagnostics run, issues logged as warnings | Build completes with warnings |
| `"strict"` | Diagnostics run, issues logged as errors | Build aborts on first SEO error |

## Render Modes

Bukit supports two mutually exclusive ways to output SEO tags:

### Engine Inject Mode (`renderMode: "inject"`)

The engine directly modifies HTML after rendering — it finds `</head>`, removes old managed SEO tags, and inserts fresh ones. Used by the starter theme.

**When to use:** Starter theme or any theme that does NOT include `SeoPartial.html`.

**What gets injected:** canonical link, meta description, meta robots, OG tags (title/description/url/type/image/site_name/locale), article tags (published_time/modified_time/author/tag), Twitter Card tags, hreflang alternates, JSON-LD scripts, Google Analytics GA4.

### Theme Responsible Mode (no `renderMode`)

The theme template explicitly includes SEO tags via Scriban. Use `SeoPartial.html` in the `<head>`:

```html
<head>
  ...
  {{ include "partials/SeoPartial.html" }}
</head>
```

**When to use:** Custom themes that need full control over SEO tag placement and formatting.

**What the theme must render:** All tags listed in the inject mode above — the engine will NOT inject anything.

**Required config:** Set `plugins.llms-txt.enabled: false` to disable the LlmsTxtPlugin if not needed.

## Sitemap Enhancement (site.sitemapDetail)

Bukit supports enhanced sitemap features beyond the standard `<loc>` + `<lastmod>`:

```yaml
site:
  sitemapDetail:
    defaultPriority: 0.5          # Default <priority> (0.0–1.0)
    defaultChangefreq: "weekly"   # Default <changefreq>
    imageEnabled: false           # Enable Image Sitemap extension
    videoEnabled: false           # Enable Video Sitemap extension
```

### Per-Page Overrides (Front Matter)

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
  images:
    - url: "/images/hero.jpg"
      caption: "Hero image"
      title: "Main hero"
  videos:
    - url: "https://youtube.com/watch?v=xxx"
      title: "Tutorial"
      thumbnail: "/images/thumb.jpg"
---
```

### Image Sitemap Extension

When `site.sitemapDetail.imageEnabled: true`, each `<url>` can include `<image:image>` entries. Images are resolved from front matter `sitemap.images` per page.

### Video Sitemap Extension

When `site.sitemapDetail.videoEnabled: true`, each `<url>` can include `<video:video>` entries. Videos are resolved from front matter `sitemap.videos` per page.

## Front Matter SEO Fields

Content files (Markdown / Notion) can override SEO values through Front Matter. Fields are resolved with a **three-tier priority fallback**:

### Priority Fallback Chain

```
Content Front Matter  >  Content Metadata  >  Site Config
      (highest)            (mid)               (lowest)
```

| SEO Field | Front Matter Key | Metadata Fallback | Site Config Fallback |
|------|------|------|------|
| **Title** | `seo_title` | `title` | `site.title` |
| **Description** | `seo_desc` | `summary` | `site.description` |
| **Canonical URL** | `canonical` | auto: `site.url + baseUrl + route.Url` | — |
| **Robots** | `robots` | — | — |
| **OG/Twitter Image** | `og_image` | `cover` → `image` | `site.seo.defaultImage` |
| **Author** | `author` | — | — |
| **Tags** | `tags` (YAML list) | — | — |
| **Twitter Creator** | `twitter_creator` | — | — |

### Content Front Matter Example

```yaml
---
title: My Blog Post
type: post
seo_title: Better SEO Title for Search Engines
seo_desc: A custom meta description that appears in search snippets.
canonical: https://example.com/blog/custom-canonical/
robots: index, follow
og_image: https://example.com/assets/images/hero.png
author: Jane Doe
twitter_creator: "@janedoe"
tags:
  - seo
  - optimization
---
```

## JSON-LD Structured Data

Bukit generates Schema.org JSON-LD on every page. The generated types depend on the page type and configuration:

### Standard Types (Always Generated)

| # | @type | Scope | Condition |
|---|------|------|------|
| 1 | **WebSite** | Global (once) | Always — name + url + optional SearchAction |
| 2 | **Organization** | Global (once) | `site.seo.organization` is configured |
| 3 | **WebPage** | Per-route | `seo.schema.webPage: true` (default) |
| 4 | **CollectionPage** | Per-route (list) | `seo.schema.collectionPage: true` + page is a list/taxonomy/pagination page |
| 5 | **BreadcrumbList** | Per-route | Route path has at least one segment beyond root |
| 6 | **BlogPosting** | Per-route (post) | Content `type: post` (default article type) |

### BlogPosting / Article Fields

Posts generate rich article JSON-LD with:
- `headline`, `description`, `url`, `image`
- `datePublished`, `dateModified` (if update_time exists)
- `author` → embedded Person with name/url
- `keywords` (from tags)

### SEO-related ItemList

Collection/list pages with field items generate `ItemList` JSON-LD with `itemListElement` (position + name + url).

## SEO Audit

Run `bukit seo audit` after a full build to check traditional SEO health. It reads `dist/.bukit/seo-report.json` by default. The legacy path `dist/seo-report.json` is still supported as a fallback for backward compatibility. Use `bukit publish audit` for the primary machine-readability and trust report.

```
bukit seo audit [--dir <dir>] [--report <path>] [--strict] [--external]
```

| Option | Default | Description |
|------|--------|------|
| `--dir` | `dist` | Output directory containing `seo-report.json` |
| `--report` | `<dir>/.bukit/seo-report.json` | Explicit report path. If omitted, checks `dist/.bukit/seo-report.json` first, then falls back to `dist/seo-report.json`. |
| `--strict` | off | Treat warnings as errors (exit code 1) |
| `--external` | off | Perform live HTTP HEAD/GET validation of canonical URLs, links, and images |

### Sample Output

```
SEO audit: routes=42 errors=2 warnings=7
error seo.title_missing /blog/my-post/ SEO title is missing.
error seo.output_file_missing /old-page/ Output file is missing for route /old-page/.
warning seo.description_missing /about/ SEO description is missing.
warning seo.canonical_not_absolute /blog/another/ Canonical URL should be absolute: /blog/another/.
```

### External Audit (`--external`)

With `--external`, the command performs live HTTP validation of every canonical URL, every `<a href>` link, and every `og:image`/`twitter:image`/`<img src>` on each route:

| External Code | When It Appears |
|------|------|
| `seo.external_fetch_failed` | Network error (DNS, connection refused) |
| `seo.external_fetch_timeout` | Request exceeded 15s timeout |
| `seo.external_http_status` | HTTP status >= 400 |
| `seo.external_image_mime` | Image URL returned non-image Content-Type |

### Exit Codes

| Code | Meaning |
|------|------|
| 0 | Pass — no errors (and no warnings if `--strict`) |
| 1 | Fail — errors found (or warnings with `--strict`) |
| 2 | Report not found or invalid format |

## SEO Diff (Regression Detection)

`bukit seo diff` compares two `seo-report.json` files (baseline vs current) to detect SEO regressions in CI/CD pipelines.

```
bukit seo diff --baseline <old-report.json> --current <new-report.json> \
  [--max-new-errors N] [--max-new-warnings N] [--max-new-issues N] \
  [--fail-on-new-code code1,code2] [--fail-on-route-removed] [--fail-on-indexable-drop]
```

### Budget Gate Parameters

| Parameter | Description |
|------|------|
| `--max-new-errors N` | Fail if new errors exceed N |
| `--max-new-warnings N` | Fail if new warnings exceed N |
| `--max-new-issues N` | Fail if total new issues exceed N |
| `--fail-on-new-code c1,c2` | Fail on any issue matching given codes |
| `--fail-on-route-removed` | Fail if any route was removed |
| `--fail-on-indexable-drop` | Fail if any route changed from indexable to non-indexable |

### Sample Output

```
SEO diff: newIssues=3 newErrors=1 newWarnings=2 resolvedIssues=5 addedRoutes=1 removedRoutes=0 indexableDrops=0
+ error seo.title_missing /new-page/ SEO title is missing.
+ warning seo.description_missing /new-page/ SEO description is missing.
+ warning seo.canonical_http /new-page/ Prefer HTTPS canonical URLs where possible: http://example.com/new-page/.
- route /deleted-page/
```

### CI/CD Integration

```yaml
# GitHub Actions example
- name: SEO regression check
  run: |
    bukit seo diff \
      --baseline dist/.bukit/seo-report.json \
      --current ./new-build/.bukit/seo-report.json \
      --max-new-errors 0 \
      --max-new-warnings 5 \
      --fail-on-route-removed \
      --fail-on-indexable-drop
```

## Diagnostic Codes

### Build-Time Diagnostics (run during `bukit build`)

| Code | Trigger |
|------|------|
| `seo.site_url_missing` | `site.url` is empty — absolute SEO URLs require it |
| `seo.canonical_duplicate_index` | Multiple routes share the same canonical URL |
| `seo.canonical_double_slash` | Canonical URL path contains `//` |
| `seo.canonical_external` | Canonical starts with `http` but domain doesn't match `site.url` |
| `seo.hreflang_x_default_missing` | Alternates defined but no `x-default` entry |
| `seo.head_missing` | HTML output has no `<head>` element |
| `seo.canonical_missing` | HTML head has no canonical link |
| `seo.canonical_duplicate` | HTML head has multiple canonical links |
| `seo.robots_missing` | SEO model has robots directive but no meta robots in head |
| `seo.hreflang_missing` | Alternates defined but no hreflang links in head |
| `seo.json_ld_missing` | JSON-LD data exists but no script tag in head |

### Audit Report Diagnostics (in `seo-report.json`)

**Output & HTML:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.output_file_missing` | error | Output HTML file missing for a route |
| `seo.html_head_missing` | warning | HTML has no `<head>` section |
| `seo.inject_canonical_missing` | error | Inject mode didn't add canonical link |

**Title & Description:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.title_missing` | error | SEO title is empty |
| `seo.title_too_long` | warning | Title exceeds 60 characters |
| `seo.title_duplicate` | warning | Same title used by multiple routes |
| `seo.description_missing` | warning | SEO description is empty |
| `seo.description_too_long` | warning | Description exceeds 160 characters |
| `seo.description_duplicate` | warning | Same description used by multiple routes |

**Canonical URL:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.canonical_not_absolute` | warning | Canonical is a relative path (needs `site.url`) |
| `seo.canonical_has_fragment` | warning | Canonical contains `#fragment` |
| `seo.canonical_http` | warning | Canonical uses HTTP instead of HTTPS |
| `seo.canonical_points_to_noindex` | error | Canonical points to a noindex route |
| `seo.canonical_sitemap_mismatch` | warning | Model canonical differs from SeoIndex canonical |
| `seo.site_url_missing_for_absolute_canonical` | warning | Absolute canonical set but `site.url` is empty |

**Open Graph & Twitter Images:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.og_image_missing_file` | warning | OG image file not found on disk |
| `seo.og_image_mime_unknown` | warning | OG image MIME type unknown |
| `seo.og_image_mime_invalid` | warning | OG image not a recognized image format |
| `seo.og_image_too_small` | warning | OG image below 300×157 minimum |
| `seo.og_image_not_absolute` | warning | OG image is a relative URL |
| `seo.og_image_external_unverified` | warning | OG image external URL couldn't be verified |
| `seo.twitter_image_*` | warning | Same checks as OG images, prefixed `twitter_image_` |

**Sitemap & Robots.txt:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.sitemap_missing_url` | warning | Indexable route not in sitemap |
| `seo.noindex_in_sitemap` | error | Noindex route appears in sitemap |
| `seo.sitemap_xml_invalid_root` | error | Sitemap root element not `urlset` or `sitemapindex` |
| `seo.sitemap_xml_invalid` | error | Sitemap is not valid XML |
| `seo.robots_txt_sitemap_missing` | warning | robots.txt doesn't declare Sitemap URL |
| `seo.robots_txt_blocks_indexable` | error | robots.txt has `Disallow: /` but routes are indexable |

**Hreflang:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.hreflang_self_missing` | warning | Alternates don't include current page URL |
| `seo.hreflang_x_default_missing` | warning | Alternates missing `x-default` entry |
| `seo.hreflang_invalid_locale` | warning | Invalid locale format in hreflang |
| `seo.hreflang_target_missing` | warning | Hreflang target URL not in generated URL inventory |
| `seo.hreflang_return_missing` | warning | Hreflang target doesn't link back (bidirectional check) |

**JSON-LD Schema:**

| Code | Severity | Trigger |
|------|---------|---------|
| `seo.json_ld_invalid` | error | JSON-LD is not valid JSON |
| `seo.json_ld_type_missing` | warning | JSON-LD doesn't declare `@type` |
| `seo.schema_website_name_missing` | warning | WebSite missing non-empty `name` |
| `seo.schema_website_url_invalid` | warning | WebSite `url` is not absolute |
| `seo.schema_website_searchaction_missing` | warning | WebSite missing SearchAction when site search enabled |
| `seo.schema_searchaction_missing` | warning | potentialAction doesn't contain SearchAction |
| `seo.schema_searchaction_invalid` | warning | potentialAction is not an object or array |
| `seo.schema_searchaction_type_missing` | warning | SearchAction missing `@type` |
| `seo.schema_searchaction_target_missing` | warning | SearchAction missing `target` |
| `seo.schema_searchaction_target_not_absolute` | warning | SearchAction target is not absolute URL |
| `seo.schema_searchaction_query_input_missing` | warning | SearchAction missing `query-input` |
| `seo.schema_blogposting_headline_missing` | error | BlogPosting missing `headline` |
| `seo.schema_blogposting_date_published_missing` | error | BlogPosting missing `datePublished` |
| `seo.schema_blogposting_author_missing` | warning | BlogPosting missing `author` |
| `seo.schema_blogposting_image_missing` | warning | BlogPosting missing `image` |
| `seo.schema_itemlist_elements_missing` | error | ItemList has empty `itemListElement` |
| `seo.schema_itemlist_item_invalid` | error | ItemList entry is not an object |
| `seo.schema_itemlist_position_missing` | error | ItemList entry missing `position` |
| `seo.schema_itemlist_name_missing` | error | ItemList entry missing `name` |
| `seo.schema_itemlist_url_missing` | warning | ItemList entry missing `url` |

## Common Issues

| Issue | Cause | Fix |
|------|------|------|
| SEO tags not appearing in HTML | `seo.enabled: false` or wrong renderMode | Enable SEO; check if using inject or theme responsible mode |
| `seo.site_url_missing` / `seo.canonical_not_absolute` | `site.url` is empty | Set `site.url: https://example.com` — required for absolute canonical, sitemap, RSS |
| `seo.title_missing` / `seo.title_too_long` | Content has no title or title > 60 chars | Set `title` or `seo_title` in front matter; keep under 60 chars |
| `seo.description_missing` | No description in content or config | Set `seo_desc` in front matter, or `summary` in metadata, or `site.description` |
| `seo.canonical_http` | Canonical uses HTTP | Ensure `site.url` uses `https://`, or set `canonical` to HTTPS URL in front matter |
| `seo.canonical_points_to_noindex` | Canonical links to a page with `robots: noindex` | Fix canonical or remove noindex from target |
| `seo.sitemap_missing_url` | Indexable route not in sitemap | Ensure `output.sitemap: true` on the collection |
| `seo.noindex_in_sitemap` | Noindex page appearing in sitemap | Set `robots: noindex` correctly; Bukit auto-excludes from sitemap |
| `seo.robots_txt_blocks_indexable` | `robots.txt` has `Disallow: /` but pages are indexable | Review robotsTxt and AI bot config; either allow crawling or set pages to noindex |
| `seo.json_ld_invalid` | Corrupted JSON-LD output | Check for encoding issues; run `bukit seo audit` to identify affected routes |
| `seo.schema_blogposting_headline_missing` | Post content has no title | Ensure post has a non-empty title |
| `seo.schema_blogposting_author_missing` | Post has no author metadata | Add `author: Name` to content front matter |
| `seo.og_image_too_small` | OG image below 300×157 | Use images at least 1200×630 for optimal social sharing |
| Inject mode canonical missing | HTML has no `</head>` tag, or `renderMode` not set to `inject` | Verify HTML structure; set `renderMode: inject` or use theme responsible mode |
| Sitemap not generated | `site.url` is empty or no indexable routes | Set `site.url`; ensure at least one route is indexable |
| robots.txt not generated | `robotsTxt.enabled: false` or `site.url` empty | Set `robotsTxt.enabled: true` and `site.url` |
| `seo.hreflang_return_missing` | Language A links to B but B doesn't link back | Ensure bidirectional hreflang links across all language versions |

### Diagnostic Codes Cross-Reference

Build-time errors related to rendering carry diagnostic codes that may appear alongside SEO issues:

| Build Code | Meaning | When It Appears |
|---|---|---|
| `BKT-0301` | RenderTemplateNotFound | A template referenced by schema/route doesn't exist; check `bukit doctor` |
| `BKT-0302` | RenderTemplateParseError | Scriban syntax error in a template; renders fail for affected pages |
| `BKT-0303` | RenderLayoutNestingExceeded | Circular layout reference; check `{% layout %}` chains |
| `BKT-0401` | SchemaStrictModeBlocked | Schema validation in strict mode blocks the entire build |

When SEO audit generates warnings about missing pages or inconsistent metadata, cross-check with `bukit doctor` for these codes.

## SEO Security (P1-6)

### External Link Audit SSRF Protection

`SeoExternalAuditor` validates all external links via `SsrfGuard.SsrfSafeConnectAsync` before connecting. This prevents SEO audits from being used as SSRF probes against internal networks. The guard rejects:

- Loopback: 127.0.0.0/8, ::1
- RFC1918 private: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
- Link-local: 169.254.0.0/16

### SEO Diagnostic Code Range (P3-1)

SEO-related diagnostic codes use the expanded range `BKT-0801` – `BKT-08FF` (256 code positions):

| Code | Description |
|---|---|
| `BKT-0801` | SeoAuditFailed — SEO external link audit or index validation failure |
