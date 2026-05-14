# 11 Multilingual & SEO: languages, Output Modes & Common Pitfalls

The hardest part of multilingual sites is not "translating content" but "URL structure, SEO artifacts, and linking between languages." This page explains these points with copy-ready configs and examples.

See runnable examples:

- `examples/starter/site.i18n.yaml`
- `examples/starter/site.i18n.merged.yaml`
- `examples/starter/site.i18n.index.yaml`
- `examples/starter/site.i18n.seo.yaml`

## What You Will Get

- How to enable multilingual (minimal config)
- How to tag content with language (Markdown/Notion)
- How to choose sitemap/rss/search split/merged/index modes
- How to fix the most common SEO/path issues under GitHub Pages

## Step 1: Enable Multilingual

Minimal multilingual config:

```yaml
site:
  language: zh-CN
  languages:
    - zh-CN
    - en-US
  defaultLanguage: zh-CN
```

Notes:

- `site.language` is the "current site default language" (can also be understood as the primary language)
- `site.languages` indicates which languages you want to output
- `defaultLanguage` is used to decide the URL organization strategy for the default language (depends on theme and output mode)

## Step 2: Tag Each Piece of Content with language

### Markdown

Write `language` in Front Matter:

```yaml
---
type: page
title: Hello
slug: greeting
language: en-US
---
```

See example: `examples/starter/content/greeting-en.md`.

### Notion

Add a `language` field (recommended: select or rich_text) in the database, with values like `zh-CN`/`en-US`. It will be promoted to meta for the engine to filter on.

For Notion details, see: [06 Content Notion](./06-notion-content.md).

## URL Structure: Where Does a Multilingual Site Output

Common output structure (example):

```text
dist/
  zh-CN/
    index.html
    pages/...
  en-US/
    index.html
    pages/...
  sitemap.xml or zh-CN/sitemap.xml (depending on mode)
```

Actual paths depend on your theme and routing rules, but the general pattern is:

- Each language gets a "language root directory" (e.g., `zh-CN/`, `en-US/`)
- Site-level artifacts (sitemap/rss/search) can be output at the root or within language directories

## How to Choose sitemap/rss/search Output Modes

All three artifact types support the same mode selection (using sitemap as an example):

### split: One Per Language

```yaml
site:
  sitemapMode: split
  rssMode: split
  searchMode: split
```

Suitable for:

- Stronger independent site experience per language (each language has its own sitemap/rss/search)
- You want search engines to treat each language as relatively independent entry points

### merged: One Combined

```yaml
site:
  sitemapMode: merged
  rssMode: merged
  searchMode: merged
```

Suitable for:

- Few languages, small content volume
- You want site-level artifacts to be as simple as possible (one at the root directory)

### index: Root Output Index, Pointing to Each Language's Files

```yaml
site:
  sitemapMode: index
  searchMode: index
```

> **Note**: `rssMode` only supports `split` / `merged`, does not support `index`.

Suitable for:

- Many languages, large content volume
- You want to keep per-language artifacts while providing a "master entry point"

## Engine-Level SEO and Theme Output

Bukit computes a unified `page.seo` model in the engine. Themes only need to render it. This keeps canonical, OG, Twitter, JSON-LD, and multilingual hreflang rules centralized instead of reimplementing URL logic in every theme.

Main template fields:

| Template Field | Description |
|---|---|
| `page.seo.title` | SEO title |
| `page.seo.description` | SEO description |
| `page.seo.canonical` | Canonical URL generated from `site.url + baseUrl + page.url` |
| `page.seo.robots` | robots meta, emitted only when a page/config provides it |
| `page.seo.og.*` | Open Graph title, description, URL, image, type |
| `page.seo.twitter.*` | Twitter Card title, description, image, site account |
| `page.seo.alternates` | Data for HTML `<link rel="alternate" hreflang=...>` |
| `page.seo.json_ld` | WebSite, Organization, BreadcrumbList, BlogPosting JSON-LD |
| `site.analytics.google_analytics_id` | GA4 Measurement ID |
| `site.analytics.enabled` | Analytics output switch |

### SEO Field Priority

Page fields win first, then regular content fields, then site-level fallbacks:

1. Page fields: `seo_title`, `seo_desc`, `canonical`, `robots`, `og_image`, `author`, `update_time`
2. Regular content fields: `summary`, `cover`, `image`, `publishAt`
3. Site fields: `site.title`, `site.description`, `site.seo.defaultImage`

Content with `type: post` or collection `post` also emits `BlogPosting` JSON-LD.

### Configure site.seo

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    defaultImage: /assets/og-default.png
    twitterSite: "@your_account"
    organization:
      name: Example Inc
      url: https://example.com/about
      logo: https://example.com/logo.png
```

`site.seo.enabled` defaults to `true`. When set to `false`, the engine does not build `page.seo`, and the new SEO partial emits nothing.

### Themes Are Not Force-Injected

The engine does not rewrite HTML and does not automatically inject SEO tags when a theme lacks an SEO partial. The theme must render the model explicitly in `<head>`:

```scriban
<title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
{{ include "partials/seo.html" }}
{{ include "partials/analytics.html" }}
```

If a theme already has custom SEO logic, avoid duplicate tags. Prefer removing hand-built canonical/OG/Twitter/JSON-LD logic and rendering `page.seo` instead.

## Google Analytics (GA4)

Bukit supports GA4 `gtag` configuration only. The field name is `google_analytics_id`:

```yaml
site:
  analytics:
    google_analytics_id: G-XXXXXXXXXX
```

By default, once the ID is configured and `enabled` is not `false`, the new `partials/analytics.html` emits:

```html
<script async src="https://www.googletagmanager.com/gtag/js?id=G-XXXXXXXXXX"></script>
```

Disable Analytics:

```yaml
site:
  analytics:
    enabled: false
    google_analytics_id: G-XXXXXXXXXX
```

## SEO Trinity: site.url, baseUrl, Theme SEO Snippets

### 1) site.url: Determines Absolute Links

If you are deploying to GitHub Pages at `https://user.github.io/my-repo/`:

```yaml
site:
  url: https://user.github.io/my-repo
```

You can also override on the command line:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site-url https://user.github.io/my-repo
```

### 2) baseUrl: Determines Resource and Link Prefix

Same GitHub Pages sub-path scenario:

```yaml
site:
  baseUrl: /my-repo
```

Typical symptoms of baseUrl misconfiguration:

- Homepage loads, but CSS/images 404
- URLs in sitemap/rss point to wrong paths

### 3) Theme: Whether to Output canonical/alternates/meta

HTML details for SEO are typically controlled by the theme. Suggestions:

- Compare with `examples/starter/themes/seo-best-practice/` template patterns
- Confirm the theme includes `partials/seo.html` in `<head>`
- Confirm multilingual pages output `alternate hreflang`

## Common Pitfalls & Fixes Checklist

### 1) Multilingual content "cross-contaminating" each other

Symptom: Chinese content appears in English lists, or vice versa.

Fix:

- Confirm every piece of content has `language` set
- In Notion mode, confirm the `language` field exists and values are consistent (`en-US` must not be written as `en`)

### 2) URLs in sitemap are wrong

Fix:

- Set `site.url`
- Set the correct `site.baseUrl` (especially for GitHub Pages sub-paths)
- Rebuild (don't just change the file without rebuilding)

### 3) 404 after deployment (only happens with multilingual)

Fix checklist:

- Does GitHub Pages publish directory point to `dist/` (not `dist/zh-CN`)
- Does the theme's homepage link correctly concatenate the language prefix
- If you want the default language without a prefix, the theme/routing strategy needs to cooperate (first get the example theme working)
