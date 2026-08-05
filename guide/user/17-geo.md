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

## Citations and provenance

Content-level `geo.citations[]` entries connect an article to the sources it
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

## Outputs

| Output | Purpose |
|---|---|
| `llms.txt` | Compact list of important pages and links. |
| `llms-full.txt` | Larger full-content file when enabled. |
| `.bukit/geo-report.json` | Audit data for `geo audit`. |
| publish audit fields | Whether AI-readable representations were generated and included. |

## Audit

```bash
bukit build --clean
bukit geo audit --dir dist
```

Use `publish audit` together with GEO when you need a release-level check of
HTML, semantic HTML, JSON, Markdown, JSON-LD, feeds, sitemap, search, and llms
representations.
