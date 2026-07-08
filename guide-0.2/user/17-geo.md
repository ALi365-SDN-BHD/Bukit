# GEO

GEO support generates AI-readable discovery outputs and validates them through a build report.

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
        - title: Docs
          url: https://example.com/docs/
          description: Main documentation
```

`aiBotMode` values are `allow`, `block`, and `selective`.

## Build and Audit

```bash
bukit build
bukit geo audit --dir dist
```

`geo audit` validates `.bukit/geo-report.json` and generated GEO outputs.

## SEO Relationship

GEO sits under `site.seo.geo` because AI discovery output depends on correct canonical URLs, site metadata, and route inventory.

For release checks, run:

```bash
bukit publish audit --dir dist
```
