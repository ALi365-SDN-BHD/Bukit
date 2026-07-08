---
name: bukit-seo
description: Use when configuring Bukit SEO, search metadata, robots/canonical/JSON-LD behavior, SEO reports, `seo audit`, `seo diff`, or SEO regression gates.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Cli.Tests/SeoReportValidatorTests.cs"
  - "tests/Bukit.Engine.Tests/SeoDiagnosticsTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs"
  - "src/Bukit-Core/Bukit.Cli/Commands/SeoReportValidator.cs"
  - "src/Bukit-Core/Bukit.Engine/BuildReporter.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit SEO

SEO is configured under `site.seo` and audited from build reports.

## Config

```yaml
site:
  seo:
    enabled: true
    renderMode: inject
    diagnostics: warn
    defaultImage: /assets/og-default.png
    robotsTxt:
      enabled: true
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
```

Use `renderMode: inject` as the default. Switch to theme-owned head output only when the theme explicitly owns all SEO tags.

## Content Fields

Useful front matter:

```yaml
seoTitle: Custom title
seoDescription: Custom description
seoImage: /assets/cover.jpg
canonical: https://example.com/canonical-url/
robots: index,follow
```

## Commands

```bash
bukit build
bukit seo audit --dir dist
bukit seo audit --dir dist --strict
bukit seo diff --baseline old.json --current new.json --max-new-errors 0
```

`seo audit` validates `.bukit/seo-report.json`. `--external` also checks external links and media URLs.

## Debug Checklist

- Confirm `site.url` and `site.baseUrl`.
- Confirm each route has title and description.
- Check canonical URLs after i18n or collection route changes.
- Keep robots policy explicit for non-indexable pages.
- Run `publish audit` before deployment when machine-readable outputs matter.
