# Public Preview Scope

This document defines which Bukit capabilities are ready for public preview and which remain experimental.

## Recommended for Public Preview

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

## Preview / Experimental

| Capability | Status |
|---|---|
| Theme registry | Experimental — theme discovery, search, and registry install are not covered by the Bukit 1.0 GA compatibility promise |
| Clone-to-theme workflow | Preview — browser extraction to theme generation |
| External plugin ecosystem (non-AOT) | Experimental — dynamic plugin loading |
| Advanced AI automation | Experimental — multi-step AI build pipelines |
| BukitJalil local control panel | Experimental — local web UI for site management |

## Not Included (Not on Roadmap)

| Capability |
|---|
| SaaS hosting platform |
| Visual drag-and-drop editor |
| Built-in CMS backend (beyond Notion integration) |
| Runtime server-side rendering |
| Real-time preview server |
