# 10 Built-In Outputs

Build output includes rendered pages, copied assets, static files, reports, and
machine-readable projections.

## Common Output Paths

| Output | Purpose |
|---|---|
| `index.html` and route `index.html` files | Rendered HTML pages. |
| `assets/` | Theme assets and localized media. |
| `.bukit/build-report.json` | Build summary, timings, render counts, routes, plugin executions. |
| `.bukit/routes.json` | Route inventory. |
| `.bukit/seo-report.json` | SEO quality data consumed by `seo audit`. |
| `.bukit/publish-audit-report.json` | Publish-readiness data consumed by `publish audit`. |
| `.bukit/security-report.json` | Output safety and publish security data. |
| `sitemap.xml` | Sitemap output, controlled by `site.sitemapMode`. |
| `feed/` and feed files | RSS, Atom, or JSON Feed depending on `site.feed`. |
| `search.json` and search UI files | Search index and optional UI. |
| `llms.txt`, `llms-full.txt` | GEO outputs when enabled. |
| `robots.txt` | Generated when SEO robots config requires it. |

## Plugin Outputs

Built-in after-build plugins write aggregate outputs. External dynamic plugin
commands may write artifacts when invoked, but they are not part of the static
Core build output contract.
