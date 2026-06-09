# Bukit Core 1.0 Stability Scope

This document defines the stability commitment for Bukit Core 1.0 and which capabilities remain in preview.

## Stable in Bukit Core 1.0

| Capability | Description |
|---|---|
| Markdown static sites | Build and deploy sites from local Markdown files |
| Notion-backed content sites | Use Notion databases as CMS with `NOTION_TOKEN` |
| GitHub Pages deployment | Deploy output to GitHub Pages via Actions or CLI |
| Theme development | Create and customize themes with Scriban templates |
| SEO/GEO validation | Built-in SEO outputs + `bukit geo audit` + llms.txt |
| AI-assisted configuration | `intent.yaml` workflow with validate/apply/doctor/build loop |
| Multilingual sites | i18n via `site.languages`, merged sitemaps, hreflang |
| Modules (`mode=data`) | Structured data for company websites (banners, nav, FAQs) |
| External plugins (AOT-safe) | Plugin protocol for built-in style extensions |
| Incremental build | `--incremental` flag with manifest-based skipping |

## Preview / Next Stage

| Capability | Status |
|---|---|
| Theme registry | Preview — theme discovery, search, and registry install are not covered by the Bukit Core 1.0 stability commitment |
| Clone-to-theme workflow | Preview — browser extraction to theme generation |
| Import html-demo workflow | Preview — HTML import to theme generation |
| External plugin ecosystem (non-AOT) | Preview — dynamic plugin loading |
| Advanced AI automation | Preview — multi-step AI build pipelines |
| BukitJalil local control panel | Preview — local web UI for site management |

## Not Included (Not on Roadmap)

| Capability |
|---|
| SaaS hosting platform |
| Visual drag-and-drop editor |
| Built-in CMS backend (beyond Notion integration) |
| Runtime server-side rendering |
| Real-time preview server |
