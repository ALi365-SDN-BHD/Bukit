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
      llmsTxtMaxArticles: 20
      aiBotMode: allow
      llmsTxtOptionalLinks:
        - title: Documentation
          url: https://example.com/docs/
          description: Public documentation
```

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
