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
- How to configure traditional SEO and generative engine optimization (GEO)
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
    renderMode: inject        # inject | off — controls HTML <head> tag injection
    diagnostics: warn         # warn | strict | off — build-time SEO quality checks
    defaultImage: /assets/og-default.png
    twitterSite: "@your_account"
    robotsTxt:
      enabled: true           # generate robots.txt (default: true)
    schema:
      webPage: true           # WebPage JSON-LD for every page
      collectionPage: true    # CollectionPage JSON-LD for list routes
      searchAction: true      # SearchAction for sitelinks searchbox
    organization:
      name: Example Inc
      url: https://example.com/about
      logo: https://example.com/logo.png
```

`site.seo.enabled` defaults to `true`. When set to `false`, the engine does not build `page.seo`, and the new SEO partial emits nothing.

#### Render Mode (`renderMode`)

| Value | Behavior |
|-------|----------|
| `inject` (default) | Engine injects SEO tags (canonical, description, OG, Twitter, JSON-LD) into HTML `<head>`. Theme must include `partials/seo.html` and `partials/analytics.html`. |
| `off` | Engine builds `page.seo` model but does **not** inject tags. Theme is responsible for all tag rendering. |

#### Diagnostics (`diagnostics`)

| Value | Behavior |
|-------|----------|
| `warn` (default) | SEO issues are logged as warnings; build continues |
| `strict` | SEO issues cause build to fail (for CI enforcement) |
| `off` | No SEO diagnostics are emitted |

Diagnostics check: missing canonical, duplicate canonicals, double-slash canonicals, external-domain canonicals, missing hreflang `x-default`, missing HTML `<head>`, missing JSON-LD, and GEO validation errors.

#### Schema Switches (`schema`)

Each schema type can be independently toggled:

| Field | Default | Description |
|-------|---------|-------------|
| `schema.webPage` | `true` | `WebPage` JSON-LD for every content page |
| `schema.collectionPage` | `true` | `CollectionPage` JSON-LD for list/taxonomy/archive pages |
| `schema.searchAction` | `true` | `SearchAction` JSON-LD with Sitelinks Searchbox |

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

## Generative Engine Optimization (GEO)

> 💡 **For a step-by-step setup guide, see the dedicated chapter: [17 GEO](./17-geo.md).** This section covers the technical reference for GEO configuration and audit.

GEO optimises your site for AI-powered search engines like ChatGPT Search, Perplexity, Google AI Overviews, and Bing Copilot. It goes beyond traditional SEO to help AI engines accurately crawl, understand, and cite your content.

### Configuration

```yaml
site:
  seo:
    geo:
      enabled: true            # master switch (default: true)
      llmsTxt: true            # generate llms.txt (default: true)
      llmsFullTxt: false       # generate llms-full.txt with full page content (default: false)
      llmsTxtMaxArticles: 20   # max articles in llms.txt (default: 20)
      aiBotMode: allow          # allow | block | selective
      aiBotAllowList:           # bots to allow (used in selective mode)
        - GPTBot
      aiBotBlockList:           # bots to block
        - CCBot
      llmsTxtOptionalLinks:     # external links in llms.txt Optional section
        - title: GitHub Repository
          url: https://github.com/user/repo
          description: Source code
```

### llms.txt and llms-full.txt

When enabled, Bukit generates two files in the output directory:

- **`llms.txt`** — A structured Markdown index of your site following the [llmstxt.org](https://llmstxt.org) standard. It contains site title, description, a list of pages/documents, recent articles (sorted by date), and an optional "Optional" section with external links.
- **`llms-full.txt`** — A full-content version containing the complete text of every indexable page, separated by Markdown headers. Useful for AI engines that need richer context.

### AI Bot robots.txt Rules

Bukit automatically adds AI crawler directives to `robots.txt` for these bots:

GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI,
PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot

Three modes are available:
- **`allow`** (default) — All AI bots are permitted
- **`block`** — All AI bots are disallowed
- **`selective`** — `aiBotAllowList` gets `Allow: /`, `aiBotBlockList` gets `Disallow: /`

### Front Matter GEO Fields

Add structured data to your content front matter under the `geo` key:

```yaml
---
title: How to Build a Blog with Bukit
type: post
geo:
  schema_type: HowTo         # BlogPosting | Article | NewsArticle | FAQPage | HowTo
  about: Static site generators
  date_reviewed: "2026-05-19"
  faq:
    - question: What content sources does Bukit support?
      answer: Notion, Markdown, and local files.
    - question: How to deploy?
      answer: GitHub Pages, Vercel, Netlify, and more.
  steps:
    - name: Install Bukit
      text: Run dotnet tool install.
      image: https://example.com/step1.png
      url: https://example.com/docs/install
    - name: Initialize a site
      text: Run bukit init my-site.
  citations:
    - title: Schema.org HowTo
      url: https://schema.org/HowTo
  same_as:
    - https://github.com/user/repo
    - https://twitter.com/user
  author:
    name: Alice
    url: https://alice.dev
    same_as:
      - https://github.com/alice
      - https://linkedin.com/in/alice
  speakable:
    xpath: /html/body/article
---
```

Each field generates corresponding JSON-LD structured data:

| Field | Schema Type Generated |
|-------|----------------------|
| `faq` | FAQPage with Question/Answer |
| `steps` | HowTo with HowToStep |
| `author` | Person with sameAs |
| `citations` | WebPage with mentions |
| `schema_type` | Article / NewsArticle / BlogPosting |
| `about` | about property on article |
| `date_reviewed` | dateReviewed on article |
| `same_as` | sameAs on article |
| `speakable` | SpeakableSpecification |

### GEO Audit

Run `bukit geo audit` to check your site's GEO readiness:

```
=== GEO Audit ===
  llms.txt: present
  llms-full.txt: missing
  robots.txt: present
  Geo-enhanced routes: 3
  Schema types: Article, FAQPage, HowTo, Person, WebPage
  GEO Score: 75/100
```

The **GEO Score** (0–100) measures your site's readiness for AI search engines. It awards points for:
- llms.txt generation (25 pts)
- llms-full.txt generation (15 pts)
- GEO-enhanced routes (10 pts)
- Schema type coverage on articles (up to 15 pts)
- FAQPage or HowTo usage (15 pts)
- Person author markup (10 pts)
- Speakable markup (5 pts)
- Multi-route GEO coverage (5 pts)

Diagnostic codes (`geo.*`) appear in both build logs and `seo-report.json`:
- `geo.faq_empty_question` / `geo.faq_empty_answer`
- `geo.howto_step_empty_name` / `geo.howto_step_empty_text`
- `geo.citation_url_invalid`
- `geo.author_no_sameas`
- `geo.speakable_path_invalid`
- `geo.schema_type_missing`
- `geo.llms_txt_missing`

### SEO Audit Report (`seo-report.json`)

After every build, Bukit writes `seo-report.json` to the output directory. This structured JSON report contains:

- **Route inventory** — every route with its title, description, canonical URL, robots status, sitemap/search/RSS inclusion, schema types, and hreflang alternates
- **Issue list** — each issue has a severity (`error`/`warning`), error code, affected route, and description
- **Summary** — total routes, indexable count, error/warning counts, GEO score and breakdown

Use `bukit seo audit` to validate the report against your quality standards:

```bash
bukit seo audit --dir dist --config site.yaml           # check current report
bukit seo audit --dir dist --strict                     # warnings also fail
bukit seo audit --dir dist --external                   # also check external links
bukit seo diff --dir dist --config site.yaml            # compare against previous report
bukit seo diff --max-new-errors 3                       # cap allowed new errors
bukit seo diff --fail-on-indexable-drop                 # fail if pages drop from index
```

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
