# Bukit Core Agent Skills

These skills describe stable Bukit Core behavior only. They are generated from
the current source tree and must stay aligned with `BukitCliSpecs.cs`,
`AppConfig.cs`, and the Core build pipeline.

## Skills

| Skill | Use |
|---|---|
| `using-bukit` | Gateway skill for Bukit Core tasks. |
| `bukit-cli-reference` | Static Core CLI command reference. |
| `bukit-config` | `site.yaml` fields and validation. |
| `bukit-content` | Markdown, Notion, media, and data sources. |
| `bukit-routing` | Route rules, list routes, taxonomy, pagination. |
| `bukit-templating` | Scriban model and template rendering. |
| `bukit-theme` | Local theme runtime and manifests. |
| `bukit-i18n` | Language variants and merged outputs. |
| `bukit-seo` | SEO reports and metadata rendering. |
| `bukit-geo` | GEO and llms outputs. |
| `bukit-preview` | Static preview server. |
| `bukit-dev` | LiveReload development server. |
| `bukit-deploy` | GitHub Pages deployment. |
| `bukit-debug` | Focused troubleshooting and metrics. |

Do not load Labs skills unless the user explicitly asks for Labs, preview, or
experimental workflows.
