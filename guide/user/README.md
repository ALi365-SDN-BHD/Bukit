# Bukit Core User Guide

This guide covers the stable Bukit Core user surface: configuration, content
loading, route generation, rendering, built-in outputs, local development,
quality audits, and GitHub Pages deployment.

> Route 2 is Bukit Core's product direction and Route 3 is its current
> operating mode: this guide documents the governed Core 2.0 surface used by
> internal enterprise sites. The repository and its existing license remain
> public, while external use is self-directed and carries no public support,
> SLA, compatibility, product-readiness, or release-cadence commitment. See
> the [current product positioning](../../docs/governance/bukit-core-product-positioning.md).
> Regular public binary releases are paused, exceptional publication requires
> explicit management approval, and Labs or external plugins are outside Core
> release readiness.

## Reading Path

| Goal | Read |
|---|---|
| Build a working site | [01 Quick Start](01-quick-start.md) |
| Learn the mental model | [02 Core Concepts](02-core-concepts.md) |
| Lay out files | [03 Project Structure](03-project-structure.md) |
| Configure `site.yaml` | [04 Site YAML Config](04-site-yaml-config.md) |
| Author Markdown | [05 Markdown Content](05-markdown-content.md) |
| Use Notion | [06 Notion Content](06-notion-content.md) |
| Combine content and data | [07 Multi-Source Data](07-multi-source-data.md) |
| Build themes and templates | [08 Themes and Templates](08-themes-templates.md) |
| Use structured data modules | [09 Modules Data](09-modules-data.md) |
| Understand generated files | [10 Built-in Outputs](10-built-in-outputs.md) |
| Configure languages and metadata | [11 I18n and SEO](11-i18n-seo.md) |
| Look up commands | [12 CLI Reference](12-cli-reference.md) |
| Deploy | [13 Deploy GitHub Pages](13-deploy-github-pages.md) |
| Diagnose failures | [14 Troubleshooting](14-troubleshooting.md) |
| Copy common patterns | [15 Recipes](15-recipes.md) |
| Scan parameters quickly | [16 Parameter Cheatsheet](16-parameter-cheatsheet.md) |
| Prepare AI-readable outputs | [17 GEO](17-geo.md) |
| Migrate list routes | [18 Static List Routes Migration](18-static-list-routes-migration.md) |
| Configure privacy-safe analytics providers | [19 Analytics](19-analytics.md) |
| Understand Core safety and reliability guarantees | [20 Core Safety And Reliability](20-core-safety-reliability.md) |
| Turn local SEO observations into review candidates | [21 SEO Insights](21-seo-insights.md) |

## Governed Core Commands

`build`, `doctor`, `config`, `preview`, `dev`, `clean`, `version`,
`completion`, `seo`, `geo`, `publish`, and `deploy`.

Stable subcommands are `config check`, `config schema`, `seo audit`,
`seo diff`, `seo insights`, `geo audit`, `publish audit`, and `publish diff`.

## Default Validation Loop

```bash
bukit config check
bukit doctor
bukit build --clean
bukit publish audit --dir dist
```

Use `dev` while editing content and templates. Use `preview` when `dist` already
exists and you only need a static file server.
