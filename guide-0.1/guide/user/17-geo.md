# 17 Generative Engine Optimization (GEO): llms.txt, AI Crawlers & Structured Data

GEO makes your Bukit site discoverable and readable by AI-driven search engines — ChatGPT Search, Perplexity, Google AI Overviews, Bing Copilot — beyond traditional SEO.

See runnable examples: `examples/starter/site.i18n.seo.yaml`

## What You Will Get

- `llms.txt` and `llms-full.txt` generation for AI engines
- Automatic AI crawler `robots.txt` rules (12 recognized bots)
- FAQPage, HowTo, Article structured data from content front matter
- GEO audit with a numeric GEO Score (0–100)
- Build-time diagnostic warnings for missing or malformed GEO data

## Step 1: Enable GEO

GEO configuration lives under `site.seo.geo`. All fields have sensible defaults — the simplest config:

```yaml
site:
  seo:
    enabled: true
    geo:
      enabled: true
```

This alone generates `llms.txt` and allows AI crawlers. No additional config needed for basic GEO.

## Step 2: Configure AI Crawler Access

Control which AI bots can crawl your site:

```yaml
site:
  seo:
    geo:
      aiBotMode: selective       # allow | block | selective
      aiBotAllowList:
        - GPTBot
        - PerplexityBot
        - Google-Extended
      aiBotBlockList:
        - CCBot
```

**Recognized AI bots**: GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI, PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot.

| Mode | Behavior |
|------|---------|
| `allow` | All AI bots allowed (default) |
| `block` | All AI bots blocked |
| `selective` | Allow listed bots, block others |

## Step 3: Add GEO Structured Data to Content

Add `geo:` fields in your content front matter to generate rich Schema.org JSON-LD for AI engines:

### FAQ Page

```yaml
---
title: Frequently Asked Questions
collection: page
geo:
  schema_type: FAQPage
  faq:
    - question: What content sources does Bukit support?
      answer: Notion, Markdown, and local files.
    - question: How do I deploy?
      answer: GitHub Pages via bukit deploy.
---
```

### HowTo Guide

```yaml
---
title: How to Build a Blog with Bukit
collection: post
geo:
  schema_type: HowTo
  about: Static Site Generation
  steps:
    - name: Download Bukit
      text: Download the binary from GitHub Releases for your platform.
      image: /assets/images/download.png
    - name: Initialize Your Site
      text: Run bukit init my-blog.
    - name: Create Content
      text: Add markdown files in the content/ directory.
  citations:
    - title: Bukit Documentation
      url: https://bukit.dev/docs/
```

### Article with Author

```yaml
---
title: The Future of Static Sites
collection: post
geo:
  schema_type: Article
  about: Web Development
  date_reviewed: "2026-05-19"
  author:
    name: John Doe
    url: https://example.com/about
    same_as:
      - https://github.com/johndoe
      - https://twitter.com/johndoe
---
```

## Step 4: Generate llms-full.txt (Optional)

By default, `llms.txt` is generated with page titles and summaries. To include full page content for deeper AI context:

```yaml
site:
  seo:
    geo:
      llmsFullTxt: true
```

> **Note**: `llms-full.txt` can be very large. Only enable if AI engines need full page context.

## Step 5: Customize llms.txt

Control article count and add external links:

```yaml
site:
  seo:
    geo:
      llmsTxtMaxArticles: 30       # Default: 20
      llmsTxtOptionalLinks:
        - title: GitHub Repository
          url: https://github.com/user/repo
          description: Source code and issue tracker
        - title: API Documentation
          url: https://example.com/api/
          description: REST API reference
```

## Step 6: Run GEO Audit

```bash
bukit build
bukit geo audit --dir dist
```

Sample output:

```
=== GEO Audit ===
  llms.txt: present
  llms-full.txt: present
  robots.txt: present
  Geo-enhanced routes: 5
  Schema types: Article, FAQPage, HowTo, Person, WebPage
  GEO Score: 85/100
```

### GEO Score Breakdown

| Criterion | Max Points |
|-----------|-----------|
| llms.txt generated | 25 |
| llms-full.txt generated | 15 |
| At least one GEO-enhanced route | 10 |
| Article schema coverage | 15 |
| FAQPage or HowTo used | 15 |
| Person author schema used | 10 |
| SpeakableSpecification used | 5 |
| Multiple routes with GEO coverage | 5 |

## Common Issues

| Issue | Cause | Fix |
|------|------|------|
| llms.txt not generated | `geo.enabled: false` or `geo.llmsTxt: false` | Enable GEO + llmsTxt in site.yaml |
| FAQPage schema not appearing | `geo.faq` array is empty or missing | Add at least one FAQ entry with non-empty question/answer |
| HowTo schema not appearing | `geo.steps` array is empty or missing | Add at least one step with non-empty name/text |
| GEO Score is 0 or low | No llms.txt, no GEO front matter | Enable llmsTxt, add `geo:` fields to content |
| Build warnings about empty fields | FAQ/HowTo items missing question/name/text | Fill all required fields — the build log tells you which |

## What Gets Generated

After build, check your output directory:

```
dist/
  llms.txt           # AI-readable site index (Markdown)
  llms-full.txt      # Full page content (if enabled)
  robots.txt         # AI crawler rules (if robotsTxt enabled)
```

### Sample llms.txt

```markdown
# My Blog
> A blog about static site generation

## Documentation
- [Home Page](https://example.com/): Welcome to my blog
- [About](https://example.com/about/): About this site

## Articles
- [Getting Started](https://example.com/blog/getting-started/): A beginner's guide
- [Advanced Tips](https://example.com/blog/advanced-tips/): Power user techniques

## Optional
- [GitHub Repository](https://github.com/user/repo): Source code
```

## Next Steps

- [12 CLI Reference](./12-cli-reference.md) — `bukit geo audit` command details
- [11 I18n & SEO](./11-i18n-seo.md) — Traditional SEO configuration
- [Dev: GEO Architecture](../dev/geo.md) — Implementation details
