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
