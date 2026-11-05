# 17 GEO

GEO features prepare machine-readable content for AI retrieval and quality
review. The primary config lives under `site.seo.geo`.

## Config

```yaml
site:
  seo:
    geo:
      enabled: true
      llmsTxt: true
      llmsFullTxt: false
      llmsTxtMaxArticles: 0
      aiBotMode: allow
      llmsTxtOptionalLinks:
        - title: Documentation
          url: https://example.com/docs/
          description: Public documentation
```

`llmsTxtMaxArticles: 0` is unlimited and writes every published, indexable
article in each collection. A positive integer limits each collection to that
many articles, and a negative value fails configuration validation. The default
remains `20` when the field is omitted. Use `0` for collections whose article
count grows over time, while monitoring the generated `llms.txt` file size.

## AI crawlers

`aiBotMode` writes one `robots.txt` group per known AI crawler. Bukit sorts
crawlers by what they fetch pages for:

| Purpose | Crawlers | Effect of blocking |
|---|---|---|
| Training | `GPTBot`, `ClaudeBot`, `Applebot-Extended`, `Meta-ExternalAgent`, `CCBot`, `Bytespider`, `Amazonbot`, `cohere-ai`, `Diffbot`, plus the legacy tokens `FacebookBot`, `anthropic-ai`, `Claude-Web` | Content is not used to train models. The site can still appear in AI answers. |
| Training and grounding | `Google-Extended` | Content is not used to train Gemini, and it is also not used for grounding in Gemini Apps and Vertex AI. Google Search is not affected. |
| AI search | `OAI-SearchBot`, `Claude-SearchBot`, `PerplexityBot` | The site drops out of AI search results and cannot be cited. |
| User fetch | `ChatGPT-User`, `Claude-User`, `Perplexity-User` | Assistants cannot open the page when a user asks about it. |

| Mode | Result |
|---|---|
| `allow` (default) | Every known crawler is allowed. |
| `block-training` | Training crawlers are blocked; AI search and user-fetch crawlers stay allowed. Use this to opt out of training while staying citable. `Google-Extended` is blocked too, which also opts out of Gemini grounding; the build logs `event=robots.ai_grounding_blocked` to say so. Add it to `aiBotAllowList` to keep Gemini grounding (and Gemini training). |
| `block` | Every known crawler is blocked, including AI search. The site disappears from AI answers. |
| `selective` | With `aiBotAllowList`, only listed crawlers are allowed and other known crawlers are blocked. With only `aiBotBlockList`, just those crawlers are blocked. The build warns with `event=robots.ai_search_blocked` when this blocks AI search or user-fetch crawlers you did not list. |

In every mode, `aiBotAllowList` names are allowed and `aiBotBlockList` names
are blocked; a name on both lists is blocked. Names Bukit does not know are
added as extra groups.

```yaml
site:
  url: https://example.com
  seo:
    robotsTxt:
      enabled: true
    geo:
      aiBotMode: block-training
```

The policy only takes effect when `site.seo.geo.enabled` and
`site.seo.robotsTxt.enabled` are `true` and `site.url` is set. Otherwise any
non-default `aiBotMode` or AI bot list makes the build log
`event=robots.ai_policy_inactive` as a warning. A static `robots.txt` in your
project is kept as is, so the policy is not written; the build logs the same
event at info level.

The publish audit reports `publish.ai_crawler_policy_conflict` only when an AI
search or user-fetch crawler is blocked for an indexable page. Blocking
training crawlers alone is not reported.

Crawler names change often. The list reflects vendor documentation as of
mid-2026; check each vendor's crawler page before relying on it.

## FAQ and HowTo content

`geo.schema_type: FAQPage` requires a nonempty `faq` list in the page's geo block; `HowTo`
requires a nonempty `steps` list in that block. If the required list is absent or empty,
Bukit omits that FAQPage/HowTo JSON-LD node instead of generating an invalid
node or silently changing it to an Article. Configured generic WebPage and
WebSite nodes remain available.

`site.seo.diagnostics` controls the missing-list diagnostic: `warn` logs
`geo.faq_items_missing` or `geo.howto_steps_missing`, `strict` fails the build,
and `off` suppresses the diagnostic but still does not emit the invalid node.
Nonempty lists retain every item for per-field diagnostics, including incomplete
maps and non-map/null items. A FAQ item requires nonblank `question` and `answer`;
a HowTo step requires nonblank `name` and `text`. Missing or blank values report
`geo.faq_empty_question`, `geo.faq_empty_answer`, `geo.howto_step_empty_name` or
`geo.howto_step_empty_text` through the same warn/strict/off policy. Non-map/null
items have both required fields missing.

Only complete items appear in JSON-LD, even when diagnostics are `off`. Mixed
lists emit their valid items; HowTo positions stay contiguous. If no complete
items remain, Bukit omits the specialized node without an Article fallback,
while preserving generic nodes. Incomplete items do not disappear before
diagnostics just because another item is valid.

## Citations and provenance

Content-level `citations` entries under the frontmatter `geo` field connect an
article to the sources it
references. Each entry accepts an optional `relation` field; the only allowed
values are `citation` and `based-on`:

```yaml
geo:
  citations:
    - title: Supporting doc
      url: https://source.example/support
      relation: citation
    - title: Primary report
      url: https://source.example/report
      relation: based-on
```

Omitting `relation` keeps the historical `citation` semantics. Every entry is
emitted in the article's `citation` array; only entries with `relation:
based-on` are additionally emitted in `isBasedOn`. Article-family pages also
always emit `mainEntityOfPage` pointing at the canonical URL:

```json
{
  "@type": "NewsArticle",
  "mainEntityOfPage": { "@type": "WebPage", "@id": "https://example.com/news/item/" },
  "citation": [
    { "@type": "WebPage", "name": "Supporting doc", "url": "https://source.example/support" },
    { "@type": "WebPage", "name": "Primary report", "url": "https://source.example/report" }
  ],
  "isBasedOn": [
    { "@type": "WebPage", "name": "Primary report", "url": "https://source.example/report" }
  ]
}
```

`based-on` must be declared explicitly per citation; Bukit never infers it
from titles, URLs, `original_source`, or repost wording. A `based-on`
declaration describes a derivation relationship only: it does not prove authority or ranking.
The existing standalone `WebPage` node with `mentions` continues to
be emitted for compatibility. Invalid relation values are reported by
`site.seo.diagnostics` (warning in `warn` mode, build failure in `strict`
mode) and are never silently rewritten.

## LLMS curation

Each page can opt into llms curation through content-level `llms` metadata
under the frontmatter `geo` field. The contract is limited to three fields:

```yaml
geo:
  llms:
    visibility: auto     # auto | include | exclude
    tier: primary        # primary | optional
    priority: 10         # integer from -100 to 100
```

Rules and precedence:

- Non-indexable pages are always excluded; `visibility: include` cannot
  override `noindex` or any other indexability boundary.
- `visibility: exclude` removes the page from both `llms.txt` and
  `llms-full.txt`.
- `visibility: include` bypasses the `llmsTxtMaxArticles` auto limit; auto
  pages fill the remaining slots. `llmsTxtMaxArticles: 0` keeps auto pages
  unlimited.
- `tier: optional` moves the page into the single `## Optional` section of
  `llms.txt`; configured `llmsTxtOptionalLinks` follow in configured order.
- `priority:` orders pages inside Bukit output only (higher first, then
  publish date, then canonical URL). It is a stable internal sort key and
  does not signal priority to external AI systems.
- Unknown fields, unknown enum values, and out-of-range priority values are
  reported by `site.seo.diagnostics` (warning in `warn` mode, build failure
  in `strict` mode); invalid metadata is treated as excluded, never as
  auto-included.

Curation changes llms projections only. Sitemap, search, RSS, robots, and
`SeoIndexEntry.Indexable` are not affected.

## Outputs

| Output | Purpose |
|---|---|
| `llms.txt` | Compact list of important pages and links. Its `## Optional` section links `agent-manifest.json`. |
| `llms-full.txt` | Larger full-content file when enabled. |
| `content/<page output path>.md` and `.json` | Markdown and JSON version of each content page, for example `content/blog/hello/index.html.md`. |
| `agent-manifest.json` | Machine-readable index of every indexable page with links to its Markdown and JSON versions. |
| `.bukit/geo-report.json` | Audit data for `geo audit`. |
| publish audit fields | Whether AI-readable representations were generated and included. |

### How AI agents find the Markdown and JSON versions

In the default `renderMode: inject`, each indexable content page gets two
links in its `<head>`:

```html
<link rel="alternate" type="text/markdown" href="/content/blog/hello/index.html.md" />
<link rel="alternate" type="application/json" href="/content/blog/hello/index.html.json" />
```

Bukit leaves them out for `noindex` and unpublished pages, for pages that set
`seo_inject: false`, and when `site.seo.geo.enabled` is `false`. Expired pages
keep their links, because their Markdown and JSON versions stay published; only
aggregate files such as the sitemap and `agent-manifest.json` drop them. If the theme's `<head>` already
links the same URL, Bukit does not add that link again.

In `renderMode: theme` or `off`, the theme owns the `<head>`, so Bukit does not
add these links. Add them in your layout if you want them; the URL is the base
URL plus `content/`, the page's output path, and `.md` or `.json`.

Page links in `llms.txt` stay on the canonical HTML URLs. Agents that want
Markdown can follow the `agent-manifest.json` link.

## Audit

```bash
bukit build --clean
bukit geo audit --dir dist
```

Use `publish audit` together with GEO when you need a release-level check of
HTML, semantic HTML, JSON, Markdown, JSON-LD, feeds, sitemap, search, and llms
representations.
