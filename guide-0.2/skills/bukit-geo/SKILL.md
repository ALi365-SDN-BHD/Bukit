---
name: bukit-geo
description: Use when configuring Bukit GEO, `llms.txt`, `llms-full.txt`, AI crawler robots policy, GEO report validation, `geo audit`, or AI-search readiness checks.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Engine.Tests/GeoDiagnosticsTests.cs"
  - "tests/Bukit.Engine.Tests/GeoSeoModelBuilderTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Cli/Commands/GeoCommand.cs"
  - "src/Bukit-Core/Bukit.Engine/BuildReporter.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit GEO

GEO is configured under `site.seo.geo` and audited from generated output.

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
```

`aiBotMode` controls AI crawler treatment. Use allow/block lists only when there is a clear crawler policy.

## Commands

```bash
bukit build
bukit geo audit --dir dist
```

`geo audit` checks `.bukit/geo-report.json`, `llms.txt`, optional `llms-full.txt`, `robots.txt`, and GEO score fields.

## Content Hints

Add structured facts in content where useful: summary, entities, FAQ-like sections, steps, source references, and reviewed status. GEO depends on clean content semantics as much as config.
