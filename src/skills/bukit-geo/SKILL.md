---
name: bukit-geo
description: Use when using bukit to optimize a site for AI-driven search engines (ChatGPT Search, Perplexity, Google AI Overviews, Bing Copilot), generating or troubleshooting llms.txt / llms-full.txt, adding FAQ/HowTo structured data in content front matter, running bukit geo audit, interpreting GEO Score, or resolving geo.* diagnostic warnings

status: beta
since: "v3.0.0"
verified_by:
  - "src/Bukit.Engine/SeoDiagnostics.cs"
  - "src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs"
source_anchors:
  - "src/Bukit.Engine/SeoDiagnostics.cs"
  - "src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs"
guide_chapters:
  - "guide/user/17-geo.md"
---

# Bukit Generative Engine Optimization (GEO)

## Overview

GEO optimizes Bukit sites for AI-driven search engines — ChatGPT Search, Perplexity, Google AI Overviews, Bing Copilot — beyond traditional SEO. Bukit implements GEO through three layers: **static artifacts** (llms.txt / llms-full.txt / AI crawler rules), **structured data** (FAQPage / HowTo / Person / Article / Speakable via front matter), and **audit diagnostics** (10 geo.* codes + GEO Score).

**REQUIRED BACKGROUND:** GEO config lives under `site.seo.geo` in site.yaml — you must understand bukit-config for the parent `site.seo` node.
**REQUIRED SUB-SKILL:** Build sites with `bukit build`, audit with `bukit geo audit`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "GEO"、"生成式引擎优化"、"llms.txt"、"FAQ 结构化数据"、"AI 搜索引擎"、"ChatGPT 搜索"、"Perplexity"、"Google AI Overviews"、"geo audit" |
| English | "GEO", "generative engine optimization", "llms.txt", "FAQ structured data", "AI search engine", "ChatGPT Search", "Perplexity", "Bing Copilot" |
| Bahasa Melayu | "GEO", "pengoptimuman enjin generatif", "llms.txt", "data berstruktur FAQ", "enjin carian AI", "ChatGPT Search" |

## Configuration

GEO configuration lives under `site.seo.geo` in site.yaml. All fields have sensible defaults — a minimal config needs nothing beyond enabling the parent `site.seo`.

```yaml
site:
  seo:
    enabled: true
    geo:
      enabled: true             # Master switch (default: true)
      llmsTxt: true             # Generate llms.txt (default: true)
      llmsFullTxt: false        # Generate llms-full.txt with full page content (default: false)
      llmsTxtMaxArticles: 20    # Max articles listed in llms.txt (default: 20)
      aiBotMode: allow          # allow | block | selective
      aiBotAllowList:           # Allowed crawlers (selective mode)
        - GPTBot
        - PerplexityBot
      aiBotBlockList:           # Blocked crawlers
        - CCBot
      llmsTxtOptionalLinks:     # External links in llms.txt Optional section
        - title: GitHub Repository
          url: https://github.com/user/repo
          description: Source code and issue tracker
```

| Field | Type | Default | Description |
|------|------|--------|------|
| `enabled` | bool | `true` | Master switch for all GEO features |
| `llmsTxt` | bool | `true` | Generate `llms.txt` index file |
| `llmsFullTxt` | bool | `false` | Generate `llms-full.txt` with full page text |
| `llmsTxtMaxArticles` | int | `20` | Max articles in llms.txt Articles section |
| `aiBotMode` | string | `"allow"` | `allow` — all bots allowed; `block` — all AI bots blocked; `selective` — use allow/block lists |
| `aiBotAllowList` | string[] | — | Bot user-agents to allow (selective mode) |
| `aiBotBlockList` | string[] | — | Bot user-agents to block |
| `llmsTxtOptionalLinks` | array | — | External links for llms.txt Optional section |

### AI Crawler Bots

Bukit recognizes these AI crawler user-agents for robots.txt rules:

- `GPTBot` / `ChatGPT-User` (OpenAI)
- `Google-Extended` (Google AI)
- `Claude-Web` / `ClaudeBot` / `Anthropic-AI` (Anthropic)
- `PerplexityBot` (Perplexity)
- `Cohere-AI` (Cohere)
- `CCBot` / `Diffbot` (Common Crawl / Diffbot)
- `FacebookBot` (Meta)
- `OAI-SearchBot` (OpenAI Search)

## Build Artifacts

### llms.txt

Follows the [llmstxt.org](https://llmstxt.org) standard — a Markdown-format site index for AI engines:

```markdown
# Site Title
> Site description

## Documentation
- [Home Page](https://example.com/): Description if available
- [About](https://example.com/about/): About the site

## Articles
- [Latest Post](https://example.com/blog/post-slug/): Post summary
- [Earlier Post](https://example.com/blog/other-slug/): Another summary

## Optional
- [GitHub Repository](https://github.com/user/repo): Source code
```

Content is organized into: **Documentation** (non-post pages, or "Pages" if no root page), **Articles** (posts sorted by publish date, limited by `llmsTxtMaxArticles`), **Optional** (external links from `llmsTxtOptionalLinks`).

### llms-full.txt

When `llmsFullTxt: true`, generates a file with every indexable page's full text content (stripped of HTML), separated by `---` dividers. Useful for AI engines needing richer context. File can be large — default off.

### AI Crawler robots.txt Rules

Based on `aiBotMode`:

| Mode | Behavior |
|------|---------|
| `allow` | All AI bots: `Allow: /` |
| `block` | All AI bots: `Disallow: /` |
| `selective` | `aiBotAllowList` → `Allow: /`; `aiBotBlockList` → `Disallow: /`; unlisted bots → `Disallow: /` |

## Front Matter GEO Fields

Add structured data in content front matter under the `geo` key to generate rich Schema.org JSON-LD:

```yaml
---
title: How to Build a Blog with Bukit
type: post
geo:
  schema_type: HowTo          # BlogPosting | Article | NewsArticle | FAQPage | HowTo
  about: Static Site Generator
  date_reviewed: "2026-05-19"
  faq:
    - question: What content sources does Bukit support?
      answer: Notion, Markdown, and local files.
    - question: How do I deploy?
      answer: GitHub Pages, Vercel, Netlify, and more.
  steps:
    - name: Install Bukit
      text: Download the binary from GitHub Releases.
      image: /assets/images/install.png
    - name: Initialize Site
      text: Run bukit init my-site.
  citations:
    - title: Schema.org HowTo
      url: https://schema.org/HowTo
    - title: llmstxt.org Specification
      url: https://llmstxt.org
  same_as:
    - https://github.com/user/repo
    - https://twitter.com/user
  author:
    name: John Doe
    url: https://example.com/about
    same_as:
      - https://github.com/johndoe
      - https://linkedin.com/in/johndoe
  speakable:
    xpath: /html/body/article
---
```

### Field Reference

| Front Matter Field | Type | Generated Schema |
|------|------|-----------------|
| `geo.schema_type` | string | Overrides article type: `BlogPosting` (default), `Article`, `NewsArticle`, `FAQPage`, `HowTo` |
| `geo.faq` | array | `FAQPage` with `Question` / `Answer` items |
| `geo.steps` | array | `HowTo` with `HowToStep` items (name, text, image?, url?) |
| `geo.author` | object | `Person` with `sameAs` links |
| `geo.citations` | array | `WebPage` with `mentions` (linked citations) |
| `geo.same_as` | string[] | `sameAs` on the primary entity |
| `geo.about` | string | `about` property on Article/WebPage |
| `geo.date_reviewed` | string | `dateReviewed` on Article (ISO 8601 date) |
| `geo.speakable.xpath` | string | `SpeakableSpecification` for voice assistants |

### Schema Type Behavior

When `schema_type` is set:

| schema_type | Requirements | JSON-LD Output |
|------------|------------|------|
| `FAQPage` | `geo.faq` must be present and non-empty | `FAQPage` with `mainEntity` → `Question`/`Answer` |
| `HowTo` | `geo.steps` must be present and non-empty | `HowTo` with `step` → `HowToStep` items |
| `Article` / `NewsArticle` / `BlogPosting` | None (BlogPosting is default) | Standard article schema + `about` + `dateReviewed` + author `Person` |

`Person` and `SpeakableSpecification` are generated independently — they appear alongside the main schema type when their respective fields are filled.

## GEO Audit

Run `bukit geo audit` (reference bukit-cli-reference) to check GEO readiness from generated audit artifacts. Use `bukit publish audit` for the broader machine-readability and trust gate.

```
=== GEO Audit ===
  llms.txt: present
  llms-full.txt: missing
  robots.txt: present
  geo-report.json: present
  Geo-enhanced routes: 3
  Schema types: Article, FAQPage, HowTo, Person, WebPage
  GEO Score: 75/100
```

### GEO Score (0–100)

| Criterion | Max Points |
|-----------|-----------|
| llms.txt generated | 25 |
| llms-full.txt generated | 15 |
| At least one GEO-enhanced route | 10 |
| Article schema type coverage (ratio × 15) | 15 |
| FAQPage or HowTo used | 15 |
| Person author schema used | 10 |
| SpeakableSpecification used | 5 |
| Multiple routes with GEO coverage | 5 |

### Diagnostic Codes

GEO diagnostics run during `bukit build` (when `site.seo.diagnostics` is `warn` or `strict`) and appear in build logs plus publish/SEO audit reports:

| Code | Severity | Trigger |
|------|---------|---------|
| `geo.llms_txt_missing` | warning | GEO enabled + `llmsTxt: true` but `llms.txt` was not generated |
| `geo.llms_full_txt_missing` | warning | GEO enabled + `llmsFullTxt: true` but `llms-full.txt` was not generated |
| `geo.schema_type_missing` | info | Content has publish date but no `geo.schema_type` and no other GEO fields |
| `geo.faq_empty_question` | error | FAQ item has empty or missing question text |
| `geo.faq_empty_answer` | error | FAQ item has empty or missing answer text |
| `geo.howto_step_empty_name` | error | HowTo step has empty or missing name |
| `geo.howto_step_empty_text` | error | HowTo step has empty or missing text |
| `geo.citation_url_invalid` | warning | Citation URL is not a valid absolute URI |
| `geo.author_no_sameas` | info | Author defined but has no `sameAs` social/profile links |
| `geo.speakable_path_invalid` | warning | Speakable XPath does not start with `/` |

### GEO Audit Output Files

- `dist/.bukit/publish-audit-report.json` — primary machine-readability and trust audit report consumed by `bukit publish audit`
- `dist/.bukit/seo-report.json` — compatibility SEO audit report
- `dist/.bukit/geo-report.json` — standalone GEO report (GEO Score, llms.txt status, geo-enhanced route details)

## Common Issues

| Issue | Cause | Fix |
|------|------|------|
| llms.txt not generated | `geo.enabled: false` or `geo.llmsTxt: false` or no indexable routes | Enable GEO + llmsTxt; ensure content has indexable routes (not `noindex`) |
| `geo.faq_empty_question` / `geo.faq_empty_answer` | FAQ item missing `question` or `answer` field | Add non-empty question and answer to every FAQ entry |
| `geo.howto_step_empty_name` / `geo.howto_step_empty_text` | Step missing `name` or `text` | Add non-empty name and text to every step |
| `geo.citation_url_invalid` | Citation URL is relative or malformed | Use absolute URLs: `https://schema.org/HowTo` |
| `geo.author_no_sameas` | Author defined but no social links | Add `same_as` under `geo.author` with GitHub/Twitter/LinkedIn URLs |
| `geo.speakable_path_invalid` | XPath doesn't start with `/` | Use an absolute XPath: `/html/body/article` |
| FAQPage/HowTo schema not appearing | `geo.faq` or `geo.steps` array is empty or missing | Fill the array with at least one item; also set `schema_type: FAQPage` or `HowTo` |
| GEO Score is 0 or low | No llms.txt, no GEO routes | Enable llmsTxt, add `geo:` fields to content front matter |
| `aiBotMode: selective` has no effect | No lists configured | Set `aiBotAllowList` and/or `aiBotBlockList` |
| llms.txt has "No indexable pages found" | All pages have `robots: noindex` or content is empty | Remove `noindex` from at least one page or set `robots: index` |

## GEO Diagnostic Code Range (P3-1)

GEO-related diagnostic codes use the expanded range `BKT-0810` – `BKT-081F` (16 code positions):

| Code | Description |
|---|---|
| `BKT-0810` | GeoAuditFailed — GEO readiness audit failure |
|
