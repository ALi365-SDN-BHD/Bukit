# Bukit Core 1.0 User Guide

This guide is for people building and publishing sites with Bukit Core 1.0. It
focuses on the stable Core surface: Markdown and Notion content sources,
filesystem themes, Scriban templates, built-in outputs, local preview,
LiveReload development, SEO/GEO reports, and GitHub Pages deployment.

## Core Reading Path

| Goal | Read |
|---|---|
| Build your first site | [01 Quick Start](./01-quick-start.md) |
| Understand the model | [02 Core Concepts](./02-core-concepts.md) |
| Lay out a project | [03 Project Structure](./03-project-structure.md) |
| Configure `site.yaml` | [04 Site YAML Config](./04-site-yaml-config.md) |
| Author Markdown content | [05 Markdown Content](./05-markdown-content.md) |
| Use Notion as CMS | [06 Notion Content](./06-notion-content.md) |
| Combine multiple sources | [07 Multi-Source Data](./07-multi-source-data.md) |
| Work with themes and templates | [08 Themes and Templates](./08-themes-templates.md) |
| Use data modules | [09 Modules Data](./09-modules-data.md) |
| Understand generated outputs | [10 Built-in Outputs](./10-built-in-outputs.md) |
| Configure i18n and SEO | [11 I18n and SEO](./11-i18n-seo.md) |
| Look up commands | [12 CLI Reference](./12-cli-reference.md) |
| Deploy to GitHub Pages | [13 Deploy GitHub Pages](./13-deploy-github-pages.md) |
| Diagnose problems | [14 Troubleshooting](./14-troubleshooting.md) |
| Copy common patterns | [15 Recipes](./15-recipes.md) |
| Find field and option names quickly | [16 Parameter Cheatsheet](./16-parameter-cheatsheet.md) |
| Prepare AI-readable outputs | [17 GEO](./17-geo.md) |
| Migrate JS lists to static routes | [18 Static List Routes Migration](./18-static-list-routes-migration.md) |

## Stable Core Commands

Bukit Core 1.0 exposes these commands:

`build`, `doctor`, `config`, `preview`, `dev`, `clean`, `version`,
`completion`, `seo`, `geo`, `publish`, and `deploy`.

Subcommands in the stable surface are `config check`, `config schema`,
`seo audit`, `seo diff`, `geo audit`, `publish audit`, and `publish diff`.

## What This Guide Does Not Treat as Core

Historical and Labs workflows are not part of the default Core user path. They
are intentionally kept outside this guide's main reading sequence. If you use a
Labs workflow, verify its own Labs document and do not assume it is available in
the Core CLI.

## Good Default Workflow

```bash
bukit config check
bukit doctor
bukit build
bukit dev
```

Use `preview` when you only want to serve an already-built output directory.
Use `dev` when you want file watching, incremental rebuilds, and browser reload.
