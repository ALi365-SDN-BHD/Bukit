# GEO (Generative Engine Optimization) Architecture

This document explains the implementation of Bukit's Generative Engine Optimization (GEO) system — how llms.txt, AI crawler rules, and GEO structured data are generated during builds.

Implementation references:
- `src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs`
- `src/Bukit.Cli/Commands/GeoCommand.cs`
- `src/Bukit.Engine/SeoModelBuilder.cs` (GEO front matter parsing)
- `src/Bukit.Config/AppConfig.cs` (SeoGeoConfig model)

Related docs: [Built-in Plugins](./built-in-plugins.md), [SEO & i18n](./i18n-seo.md), [User: 17 GEO](../../guide/user/17-geo.md)

## Overview

GEO extends traditional SEO with artifacts and structured data optimized for AI-driven search engines (ChatGPT Search, Perplexity, Google AI Overviews, Bing Copilot). Three layers:

1. **Static artifacts** — `llms.txt`, `llms-full.txt`, AI crawler `robots.txt` rules
2. **Structured data** — FAQPage, HowTo, Person, Article, Speakable JSON-LD from front matter
3. **Audit diagnostics** — 7 `geo.*` diagnostic codes + GEO Score

## Configuration Model

All GEO config lives under `site.seo.geo`:

| Field | Type | Default | Implemented In |
|------|------|------|------|
| `enabled` | bool | `true` | LlmsTxtPlugin, SeoModelBuilder |
| `llmsTxt` | bool | `true` | LlmsTxtPlugin |
| `llmsFullTxt` | bool | `false` | LlmsTxtPlugin |
| `llmsTxtMaxArticles` | int | `20` | LlmsTxtPlugin |
| `aiBotMode` | string | `"allow"` | LlmsTxtPlugin (robots.txt) |
| `aiBotAllowList` | string[] | — | LlmsTxtPlugin |
| `aiBotBlockList` | string[] | — | LlmsTxtPlugin |
| `llmsTxtOptionalLinks` | array | — | LlmsTxtPlugin |

## Build Pipeline

### 1. Content Loading

GEO front matter is parsed during content loading via `SeoModelBuilder`. The `geo:` key in front matter is read as a structured object. No build-phase changes to content loading.

### 2. Derive Pages Phase

No GEO-specific work in this phase. GEO only operates in after-build.

### 3. After-Build Phase

`LlmsTxtPlugin.AfterBuild(context)` executes:

1. **Check enabled**: Returns immediately if `!geo.Enabled`
2. **llms.txt generation** (if `geo.LlmsTxt`):
   - Iterates `context.Routed` + `context.DerivedRouted`
   - Filters to `Indexable` entries from `context.SeoIndex`
   - Groups into **Documentation** (non-post pages) and **Articles** (posts, sorted by `PublishAt` descending)
   - Limits articles to `geo.LlmsTxtMaxArticles`
   - Appends **Optional** section from `geo.LlmsTxtOptionalLinks`
   - Writes to `<outputDir>/llms.txt`
3. **llms-full.txt generation** (if `geo.LlmsFullTxt`):
   - Iterates all indexable routes
   - Strips HTML tags from content
   - Concatenates with `---` dividers
   - Writes to `<outputDir>/llms-full.txt`
4. **AI crawler rules** (appended to `robots.txt` or inline):
   - Recognizes 12 AI bot user-agents
   - Applies `allow`/`block`/`selective` mode

### 4. SEO Index Integration

`LlmsTxtPlugin` reuses the existing `context.SeoIndex` (built by `SeoIndexBuilder`) to determine which pages are indexable. Pages with `robots: noindex` are excluded from llms.txt.

## Front Matter GEO Model

Parsed from content front matter under the `geo:` key. Implementation in `SeoModelBuilder`:

| Middleware Field | Type | Schema.org Output |
|------|------|-----------------|
| `schema_type` | string | Overrides `@type`: BlogPosting (default), Article, NewsArticle, FAQPage, HowTo |
| `faq` | array of {question, answer} | `FAQPage` with `Question`/`Answer` items |
| `steps` | array of {name, text, image?, url?} | `HowTo` with `HowToStep` items |
| `author` | {name, url, same_as} | `Person` with `sameAs` links |
| `citations` | array of {title, url} | `WebPage` with `mentions` |
| `same_as` | string[] | `sameAs` on primary entity |
| `about` | string | `about` property |
| `date_reviewed` | string | `dateReviewed` (ISO 8601) |
| `speakable.xpath` | string | `SpeakableSpecification` |

## GEO Audit

Implementation: `src/Bukit.Cli/Commands/GeoCommand.cs`

Reads `seo-report.json` (produced by `bukit build`) and calculates:

### GEO Score (0–100)

| Criterion | Max Points | Source |
|-----------|-----------|--------|
| llms.txt generated | 25 | File existence check |
| llms-full.txt generated | 15 | File existence check |
| At least 1 GEO-enhanced route | 10 | Route metadata check |
| Article schema coverage | 15 | Ratio of GEO routes to total routes |
| FAQPage/HowTo used | 15 | Schema type detection |
| Person author schema | 10 | Author field presence |
| SpeakableSpecification | 5 | XPath field presence |
| Multi-route GEO coverage | 5 | Count of GEO routes > 1 |

### Diagnostic Codes

Generated during `bukit build` diagnostics (when `site.seo.diagnostics` is `warn` or `strict`):

| Code | Severity | Trigger |
|------|---------|---------|
| `geo.llms_txt_missing` | warning | GEO enabled but llms.txt not found |
| `geo.llms_full_txt_missing` | warning | llmsFullTxt enabled but file not found |
| `geo.schema_type_missing` | info | Content has pub date but no GEO fields |
| `geo.faq_empty_question` | error | FAQ item has empty question |
| `geo.faq_empty_answer` | error | FAQ item has empty answer |
| `geo.howto_step_empty_name` | error | HowTo step has empty name |
| `geo.howto_step_empty_text` | error | HowTo step has empty text |
| `geo.citation_url_invalid` | warning | Citation URL not absolute |
| `geo.author_no_sameas` | info | Author defined but no sameAs links |
| `geo.speakable_path_invalid` | warning | XPath does not start with `/` |

## AI Crawler Bot List

Hardcoded in `LlmsTxtPlugin`:

```csharp
static readonly string[] AiBots = {
    "GPTBot", "ChatGPT-User",            // OpenAI
    "Google-Extended",                    // Google AI
    "Claude-Web", "ClaudeBot", "Anthropic-AI",  // Anthropic
    "PerplexityBot",                      // Perplexity
    "Cohere-AI",                          // Cohere
    "CCBot", "Diffbot",                   // Common Crawl / Diffbot
    "FacebookBot",                        // Meta
    "OAI-SearchBot"                       // OpenAI Search
};
```

robots.txt rule generation logic:

| `aiBotMode` | For Each Bot | Unlisted Bots |
|------------|-------------|--------------|
| `allow` | `Allow: /` | (no rule) |
| `block` | `Disallow: /` | (no rule) |
| `selective` | Allow if in `aiBotAllowList`, Disallow if in `aiBotBlockList` | `Disallow: /` |

## CLI Entry Points

| Command | Purpose | Key Flags |
|---------|------|---------|
| `bukit build` | Build with GEO artifact generation | (reads site.seo.geo config) |
| `bukit geo audit` | Audit existing dist for GEO readiness | `--dir <path>` |

GEO audit reads `seo-report.json` from the build output directory. It does not require a re-build.

## File Outputs

| File | Plugin | Config Required |
|------|--------|----------------|
| `llms.txt` | LlmsTxtPlugin | `geo.enabled && geo.llmsTxt` |
| `llms-full.txt` | LlmsTxtPlugin | `geo.enabled && geo.llmsFullTxt` |
| `robots.txt` (AI rules) | LlmsTxtPlugin | `geo.enabled && seo.robotsTxt.enabled` |

llms.txt content structure follows [llmstxt.org](https://llmstxt.org) specification: `# Title` → `> Description` → `## Documentation` → `## Articles` → `## Optional`.
